using System.Linq;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // Composition root of the MachineLab scene: generate → play → inspect → save.
    public sealed class MachineLab : MonoBehaviour
    {
        private const string SettingsKey = "contraption.lab.settings";
        private const string SeedKey = "contraption.lab.seed";

        [SerializeField] private Camera _camera;

        private readonly MachineInput _input = new();
        private readonly AnalyzerHud _hud = new();
        private readonly LabPanel _panel = new();
        private MachineView _view;
        private bool _panelVisible = true;
        private float _uiScale = 1f;

        public GeneratorSettings Settings { get; private set; }
        public MachineSession Session { get; private set; }
        public DifficultyReport Report { get; private set; }
        public string Status { get; private set; } = "";
        public string SeedText { get; set; } = "1";

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
            _view = new GameObject("MachineView").AddComponent<MachineView>();
            Settings = LoadSettings();
            SeedText = PlayerPrefs.GetString(SeedKey, "1");
            Generate();
        }

        private void Update()
        {
            _uiScale = Mathf.Max(1f, Screen.height / 1000f);
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) _panelVisible = !_panelVisible;

            float left = AnalyzerHud.Width * _uiScale, right = _panelVisible ? LabPanel.Width * _uiScale : 0f;
            _camera.rect = new Rect(left / Screen.width, 0f, 1f - (left + right) / Screen.width, 1f);

            if (Session != null)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !OverGui(GuiMouse())) GUIUtility.keyboardControl = 0;
                _input.Tick(Session, _view, _camera, GUIUtility.keyboardControl == 0, OverGui);
            }
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));
            float h = Screen.height / _uiScale, w = Screen.width / _uiScale;
            if (Session != null) _hud.Draw(new Rect(0f, 0f, AnalyzerHud.Width, h), Session, _input);
            if (_panelVisible) _panel.Draw(new Rect(w - LabPanel.Width, 0f, LabPanel.Width, h), this);
        }

        private bool OverGui(Vector2 guiPos)
        {
            float x = guiPos.x / _uiScale;
            return x < AnalyzerHud.Width || (_panelVisible && x > Screen.width / _uiScale - LabPanel.Width);
        }

        private static Vector2 GuiMouse()
        {
            var p = Mouse.current.position.ReadValue();
            return new Vector2(p.x, Screen.height - p.y);
        }

        public void Generate()
        {
            if (!ulong.TryParse(SeedText, out ulong seed)) seed = 1;
            SaveSettings(seed);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = MachineGenerator.Generate(seed, Settings);
            string rejections = string.Join(", ", result.Rejections.Select(kv => $"{kv.Key} ×{kv.Value}"));
            if (!result.Success)
            {
                Status = $"FAILED after {result.Attempts} attempts ({sw.ElapsedMilliseconds} ms). {rejections}";
                return;
            }
            Load(result.Definition);
            Status = $"Seed {seed}: {result.Attempts} attempts, {sw.ElapsedMilliseconds} ms. {rejections}";
        }

        public void StepSeed(int dir)
        {
            ulong.TryParse(SeedText, out ulong seed);
            SeedText = (dir < 0 && seed > 0 ? seed - 1 : seed + 1).ToString();
            Generate();
        }

        public void RandomSeed()
        {
            SeedText = ((ulong)Random.Range(1, int.MaxValue)).ToString();
            Generate();
        }

        public void Load(MachineDefinition def)
        {
            var solve = Solver.Solve(def);
            Report = solve.Solvable ? Difficulty.Evaluate(def, solve) : null;
            if (!solve.Solvable) Status = "Loaded machine is NOT solvable from its start state.";
            Session = new MachineSession(def);
            _input.ActivePort = 0;
            _view.Build(Session);
            Frame();
        }

        public void LoadFile(string path)
        {
            Load(LevelStore.Load(path));
            Status = "Loaded " + System.IO.Path.GetFileName(path);
        }

        public void Save() => Status = "Saved " + LevelStore.Save(Session.Definition);

        public void HintStep()
        {
            var path = Session.SolveFromHere();
            if (path == null) Status = "No solution from this state (reset with R).";
            else if (path.Count > 0 && Session.Apply(path[0])) _view.Pulse(path[0].Control);
        }

        public string SolutionText()
        {
            var path = Session.SolveFromHere();
            if (path == null) return "Unsolvable from here.";
            return path.Count == 0 ? "Already in a goal state — run the tests." : string.Join(" → ", path.Select(m => $"{m.Action} {MachineDescriber.ControlName(Session.Definition, m.Control)}"));
        }

        public void UseCurrentAsStart()
        {
            var def = Session.Definition;
            def.StartState = Session.CopyState();
            Load(def);
            Status = "Start state replaced with the current state.";
        }

        private void Frame()
        {
            var b = _view.Bounds;
            float halfV = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max((b.extents.y + 0.4f) / halfV, (b.extents.x + 0.4f) / (halfV * _camera.aspect)) + 0.5f;
            _camera.transform.position = b.center + new Vector3(0f, 0.8f, -dist);
            _camera.transform.LookAt(b.center);
        }

        private void LateUpdate()
        {
            // Aspect changes when the side panels toggle or the window resizes.
            if (Session != null) Frame();
        }

        private GeneratorSettings LoadSettings()
        {
            string json = PlayerPrefs.GetString(SettingsKey, "");
            if (json.Length == 0) return new GeneratorSettings();
            try { return MachineJson.Deserialize<GeneratorSettings>(json) ?? new GeneratorSettings(); }
            catch { return new GeneratorSettings(); }
        }

        private void SaveSettings(ulong seed)
        {
            PlayerPrefs.SetString(SettingsKey, MachineJson.Serialize(Settings));
            PlayerPrefs.SetString(SeedKey, seed.ToString());
        }
    }
}
