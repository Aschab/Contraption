using System.Collections.Generic;
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
        private const string KitKey = "contraption.lab.kit";

        [SerializeField] private Camera _camera;

        private readonly MachineInput _input = new();
        private readonly AnalyzerHud _hud = new();
        private readonly LabPanel _panel = new();
        private MachineView _view;
        private MachineKit[] _kits;
        private Workshop _workshop;
        private BookDesk _desk;
        private IntroFlight _intro;
        private SampleRack _rack;
        private float _blur, _lean, _leanSide = 1f;
        private const string PanelsKey = "contraption.lab.panels";

        public bool PanelsHidden
        {
            get => PlayerPrefs.GetInt(PanelsKey, 0) == 1;
            set => PlayerPrefs.SetInt(PanelsKey, value ? 1 : 0);
        }
        private bool _panelVisible = true;
        private float _uiScale = 1f;

        public GeneratorSettings Settings { get; private set; }
        public MachineSession Session { get; private set; }
        public DifficultyReport Report { get; private set; }
        public string Status { get; private set; } = "";
        public string SeedText { get; set; } = "1";
        public int GoalDistance { get; private set; }
        public MachineKit[] Kits => _kits;
        public int KitIndex { get; private set; }

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
            Stage.Build(_camera);
            _kits = Resources.LoadAll<MachineKit>("Kits");
            System.Array.Sort(_kits, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (_kits.Length == 0)
            {
                Status = "No kits in Resources/Kits — run Contraption → Build Kits.";
                enabled = false;
                return;
            }
            KitIndex = Mathf.Clamp(PlayerPrefs.GetInt(KitKey, 0), 0, _kits.Length - 1);
            _workshop = new Workshop(_kits[KitIndex]);
            _view = new GameObject("MachineView").AddComponent<MachineView>();
            _view.transform.SetPositionAndRotation(_workshop.MachineAnchor, Quaternion.identity);
            _view.transform.localScale = Vector3.one * Workshop.MachineScale;
            _desk = new BookDesk(_kits[KitIndex], _workshop.Root, _camera, _workshop.ManualRest, _workshop.LogRest);
            _rack = new SampleRack(_kits[KitIndex], _workshop.Root, _workshop.RackOrigin);
            Settings = LoadSettings();
            SeedText = PlayerPrefs.GetString(SeedKey, "1");
            Generate();
            PlayIntro();
        }

        public void PlayIntro()
        {
            if (_intro != null) return;
            _blur = 0f;
            Stage.SetFocus(0f, 0f);
            _intro = IntroFlight.Play(_camera, TableView, () => _intro = null);
        }

        private void Update()
        {
            _uiScale = Mathf.Max(1f, Screen.height / 1000f);
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) _panelVisible = !_panelVisible;

            bool shown = !PanelsHidden;
            float left = shown ? AnalyzerHud.Width * _uiScale : 0f, right = shown && _panelVisible ? LabPanel.Width * _uiScale : 0f;
            _camera.rect = new Rect(left / Screen.width, 0f, 1f - (left + right) / Screen.width, 1f);

            if (_intro != null)
            {
                if ((Keyboard.current?.anyKey.wasPressedThisFrame ?? false) || (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
                    || (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false))
                    _intro.Skip();
                return;
            }
            if (Session != null)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !OverGui(GuiMouse())) GUIUtility.keyboardControl = 0;
                bool keyboardFree = GUIUtility.keyboardControl == 0;
                _desk.Tick(_camera, OverGui, keyboardFree);
                _rack.Tick(_camera, OverGui, _input.ActivePort);
                _input.Tick(Session, _view, _camera, keyboardFree, !_desk.CapturesArrows, OverGui);
            }
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));
            float h = Screen.height / _uiScale, w = Screen.width / _uiScale;
            if (PanelsHidden)
            {
                if (GUI.Button(new Rect(w - 110f, 6f, 104f, 24f), "Show panels")) PanelsHidden = false;
                return;
            }
            if (Session != null) _hud.Draw(new Rect(0f, 0f, AnalyzerHud.Width, h), Session, _input);
            if (_panelVisible) _panel.Draw(new Rect(w - LabPanel.Width, 0f, LabPanel.Width, h), this);
        }

        private bool OverGui(Vector2 guiPos)
        {
            float x = guiPos.x / _uiScale;
            if (PanelsHidden) return x > Screen.width / _uiScale - 112f && guiPos.y / _uiScale < 32f;
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

        public void ResetSettings() => Settings = new GeneratorSettings();

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
            def.Layout ??= LayoutGenerator.Generate(def, def.Seed, Settings);
            int k = System.Array.FindIndex(_kits, x => x.name == def.Kit);
            if (k >= 0) KitIndex = k;
            else def.Kit = _kits[KitIndex].name;
            Session = new MachineSession(def);
            Session.Changed += UpdateGoalDistance;
            UpdateGoalDistance();
            _input.ActivePort = 0;
            _view.transform.position = _workshop.MachineAnchor;
            _view.Build(Session, _kits[KitIndex]);
            // Keep the machine clear of the sample racks on the left.
            float clear = _rack.RightEdge(def.InputPool.Count) + 0.12f - _view.Bounds.min.x;
            if (clear > 0f)
            {
                _view.transform.position += new Vector3(clear, 0f, 0f);
                _view.UpdateBounds();
            }
            _desk.Bind(Session);
            _rack.Bind(Session);
            Frame();
        }

        public void SetKit(int index)
        {
            KitIndex = index;
            PlayerPrefs.SetInt(KitKey, index);
            if (Session == null) return;
            Session.Definition.Kit = _kits[index].name;
            Load(Session.Definition);
        }

        public int SelectedPart { get; set; }

        public void RebuildView()
        {
            _view.Build(Session, _kits[KitIndex]);
            Frame();
        }

        public void NewLayout()
        {
            var def = Session.Definition;
            def.Layout = LayoutGenerator.Generate(def, (ulong)Random.Range(1, int.MaxValue), Settings);
            RebuildView();
        }

        public void NewDecor()
        {
            LayoutEditor.Reroll(Session.Definition.Layout, (ulong)Random.Range(1, int.MaxValue));
            RebuildView();
        }

        // Selectable parts: controls first, then displays.
        public Placement SelectedPlacement(out string label, out IReadOnlyList<PartStyle> styles)
        {
            var def = Session.Definition;
            int n = def.Controls.Count + def.Displays.Count;
            SelectedPart = Values.Wrap(SelectedPart, n);
            if (SelectedPart < def.Controls.Count)
            {
                var c = def.Controls[SelectedPart];
                label = $"{MachineDescriber.ControlName(def, SelectedPart)} ({c.Kind})";
                styles = PartStyles.For(c.Kind);
                return def.Layout.Controls[SelectedPart];
            }
            int d = SelectedPart - def.Controls.Count;
            label = $"Display {d + 1} ({def.Displays[d].Kind})";
            styles = PartStyles.For(def.Displays[d].Kind);
            return def.Layout.Displays[d];
        }

        public void EditStatus(bool ok, string what) => Status = ok ? what : what + ": no room there.";

        public void LoadFile(string path)
        {
            Load(LevelStore.Load(path));
            Status = "Loaded " + System.IO.Path.GetFileName(path);
        }

        public void Save() => Status = "Saved " + LevelStore.Save(Session.Definition);

        private void UpdateGoalDistance() => GoalDistance = Session.SolveFromHere()?.Count ?? -1;

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

        // Looking slightly down at the table: the machine, the books at rest and the sample racks.
        private (Vector3 pos, Vector3 look) TableView()
        {
            var b = _view.Bounds;
            b.Encapsulate(_desk.RestBounds);
            b.Encapsulate(new Bounds(_workshop.RackOrigin + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.2f)));
            return Fit(b, 1f);
        }

        // With a book up, the camera leans toward the machine and backs off so it fits beside the book (58% of the view).
        private (Vector3 pos, Quaternion rot) FocusView(float side)
        {
            var b = _view.Bounds;
            var (pos, look) = Fit(b, 0.58f);
            float tanH = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * _camera.aspect;
            float yaw = Mathf.Atan(0.42f * tanH) * Mathf.Rad2Deg;
            return (pos, Quaternion.AngleAxis(side * yaw, Vector3.up) * Quaternion.LookRotation(look - pos));
        }

        private (Vector3 pos, Vector3 look) Fit(Bounds b, float widthShare)
        {
            var dir = Quaternion.Euler(22f, 0f, 0f) * Vector3.forward;
            float halfV = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            const float margin = 0.12f;
            float dist = Mathf.Max((b.extents.y + b.extents.z * 0.4f + margin) / halfV,
                (b.extents.x + margin) / (halfV * _camera.aspect * widthShare)) + b.extents.z;
            return (b.center - dir * dist, b.center);
        }

        private void Frame()
        {
            if (_intro != null) return;
            int side = _desk.FocusSide;
            if (side != 0) _leanSide = side;
            _lean = Mathf.MoveTowards(_lean, side != 0 ? 1f : 0f, Time.deltaTime * 2.5f);
            var (pos, look) = TableView();
            var rot = Quaternion.LookRotation(look - pos);
            if (_lean > 0f)
            {
                var (fp, fr) = FocusView(_leanSide);
                float k = Mathf.SmoothStep(0f, 1f, _lean);
                pos = Vector3.Lerp(pos, fp, k);
                rot = Quaternion.Slerp(rot, fr, k);
            }
            _camera.transform.SetPositionAndRotation(pos, rot);
            _blur = Mathf.MoveTowards(_blur, 1f, Time.deltaTime / 1.2f);
            Stage.SetFocus(SharpDepth(), _blur);
        }

        // View depth of the farthest point that must stay sharp (table, books, racks, machine).
        private float SharpDepth()
        {
            var cam = _camera.transform;
            float depth = 0f;
            foreach (var p in Workshop.SharpPoints) depth = Mathf.Max(depth, Vector3.Dot(p - cam.position, cam.forward));
            var b = _view.Bounds;
            depth = Mathf.Max(depth, Vector3.Dot(b.max - cam.position, cam.forward));
            return depth + 0.3f;
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
