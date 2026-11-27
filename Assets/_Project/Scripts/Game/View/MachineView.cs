using System.Collections;
using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Assembles a machine from its saved layout and mirrors the session state. Owns no game state.
    // Chassis: dual-grid tiles on every grid corner. Parts sit on cells. Inputs stand on top, the output on the ground right.
    public sealed class MachineView : MonoBehaviour
    {
        public const float Cell = 1.1f, Depth = 0.9f;
        private const float PipeZ = 0.35f, VialZ = 0.2f, VialStep = 0.62f;

        public static bool AnimateTests = true;

        private readonly List<ControlView> _controls = new();
        private readonly List<DisplayView> _displays = new();
        private readonly List<LiquidContainerView> _inputs = new();
        private readonly List<LiquidContainerView> _vials = new();
        private LiquidContainerView _output;
        private Transform _stream;
        private Renderer _fixedLamp;
        private TestTriggerView _trigger;
        private MachineSession _session;
        private MachineKit _kit;
        private Look _look;
        private MachineLayout _layout;
        private Transform _root;
        private float _baseH;
        private int[] _testedInputs;
        private List<Liquid> _stages;
        private Coroutine _play;

        public int Focus { get; private set; }
        public int ControlCount => _controls.Count;
        public Bounds Bounds { get; private set; }
        public bool Animating => _play != null;

        public bool IsTestTrigger(Collider c) => c != null && _trigger != null && c.GetComponentInParent<TestTriggerView>() == _trigger;

        public void Build(MachineSession session, MachineKit kit)
        {
            if (_session != null)
            {
                _session.Changed -= Refresh;
                _session.Tested -= OnTested;
            }
            if (_play != null) StopCoroutine(_play);
            _play = null;
            if (_root != null) Destroy(_root.gameObject);
            _controls.Clear();
            _displays.Clear();
            _inputs.Clear();
            _vials.Clear();
            _testedInputs = null;

            _session = session;
            _kit = kit;
            var def = session.Definition;
            _layout = def.Layout;
            _look = Look.From(kit, _layout.Colors);
            var rng = new Rng(_layout.DecorSeed);
            _root = new GameObject("Machine").transform;
            _root.SetParent(transform, false);
            _baseH = -ModelMinY(kit.Get("Base_" + _layout.BaseStyle));

            BuildChassis();
            BuildBase();

            var glyphs = GlyphSet.Create(def);
            for (int i = 0; i < def.Displays.Count; i++)
                _displays.Add(DisplayView.Create(kit, _look.ForPart(_layout.Displays[i]), _root, Center(_layout.Displays[i]), def.Displays[i], _layout.Displays[i], glyphs));
            for (int i = 0; i < def.Controls.Count; i++)
                _controls.Add(ControlView.Create(kit, _look.ForPart(_layout.Controls[i]), _root, Center(_layout.Controls[i]), i, def.Controls[i], _layout.Controls[i], glyphs.Labels[i]));
            foreach (var f in _layout.Fillers)
            {
                var go = kit.Spawn(f.Style, _root, Center(f), _look.ForPart(f));
                var spin = MachineKit.FindOptional(go.transform, "Spin");
                if (spin != null) spin.gameObject.AddComponent<Spinner>().Axis = Art.AxisIn(spin, _root, Vector3.forward);
            }

            var freeTops = new List<int>();
            for (int x = 0; x < _layout.Columns; x++) freeTops.Add(x);
            foreach (var input in _layout.Inputs)
            {
                var c = LiquidContainerView.Create(kit, _look, _root, input.Style, new Vector3(ColumnX(input.Column), TopY(input.Column), 0.4f));
                c.SetFill(1f, 0f);
                _inputs.Add(c);
                freeTops.Remove(input.Column);
            }
            BuildTops(rng, freeTops);
            BuildOutput(def);

            Bounds = MeasureBounds();
            Focus = 0;
            _session.Changed += Refresh;
            _session.Tested += OnTested;
            Refresh(true);
        }

        // ---------------------------------------------------------------- geometry
        private float GridX(int i) => (i - _layout.Columns / 2f) * Cell;
        private float ColumnX(int x) => GridX(x) + Cell / 2f;
        private float TopY(int x) => _baseH + (_layout.TopOf(x) + 1) * Cell;
        private float RightEdge => GridX(_layout.Columns);

        private Vector3 Center(Placement p) =>
            new(GridX(p.X) + p.W * Cell / 2f, _baseH + (p.Y + p.H / 2f) * Cell, 0f);

        private void BuildChassis()
        {
            for (int j = 0; j <= _layout.Rows; j++)
                for (int i = 0; i <= _layout.Columns; i++)
                {
                    int mask = (_layout.IsBody(i - 1, j - 1) ? 1 : 0) | (_layout.IsBody(i, j - 1) ? 2 : 0)
                        | (_layout.IsBody(i - 1, j) ? 4 : 0) | (_layout.IsBody(i, j) ? 8 : 0);
                    if (mask != 0) _kit.Spawn("Tile_" + mask, _root, new Vector3(GridX(i), _baseH + j * Cell, 0f), _look);
                }
        }

        private void BuildBase()
        {
            string piece = "Base_" + _layout.BaseStyle;
            bool feet = _layout.BaseStyle == "Foot";
            for (int x = 0; x < _layout.Columns; x++)
            {
                if (!_layout.IsBody(x, 0)) continue;
                bool runStart = !_layout.IsBody(x - 1, 0), runEnd = !_layout.IsBody(x + 1, 0);
                int runIndex = 0;
                for (int k = x; _layout.IsBody(k - 1, 0); k--) runIndex++;
                if (feet && !runStart && !runEnd && runIndex % 2 == 1) continue;
                float offset = feet ? (runStart ? -0.25f : runEnd ? 0.25f : 0f) : 0f;
                _kit.Spawn(piece, _root, new Vector3(ColumnX(x) + offset, _baseH, 0f), _look);
            }
        }

        private void BuildTops(Rng rng, List<int> free)
        {
            // Plunger on the leftmost free top; lever (or no room on top) bolted to the left side.
            free.Sort();
            if (_layout.TestStyle != "Test_Lever" && free.Count >= 2)
            {
                _trigger = TestTriggerView.Create(_kit, _look, _root, _layout.TestStyle, new Vector3(ColumnX(free[0]), TopY(free[0]), 0f));
                free.RemoveAt(0);
            }
            else
            {
                int row = Mathf.Min(_layout.TopOf(0), 1);
                _trigger = TestTriggerView.Create(_kit, _look, _root, "Test_Lever", new Vector3(GridX(0) - 0.02f, _baseH + (row + 0.5f) * Cell, 0.25f));
            }
            rng.Shuffle(free);
            // The "fixed" lamp lives on the rightmost free top.
            int lampCol = -1;
            foreach (int x in free) lampCol = Mathf.Max(lampCol, x);
            if (lampCol >= 0)
            {
                free.Remove(lampCol);
                var lamp = _kit.Spawn("Lamp", _root, new Vector3(ColumnX(lampCol), TopY(lampCol), 0f), _look, "FixedLamp");
                _fixedLamp = MachineKit.Find(lamp.transform, "Bulb").GetComponent<Renderer>();
            }
            bool hero = _kit.Decor.Length > 0 && free.Count >= 2 && rng.Chance(0.6);
            for (int i = 0; i < free.Count; i++)
            {
                var pos = new Vector3(ColumnX(free[i]), TopY(free[i]), 0f);
                if (i == 0 && hero) Instantiate(_kit.Decor[rng.Range(0, _kit.Decor.Length)], _root, false).transform.localPosition = pos + new Vector3(0f, 0f, 0.4f);
                else if (rng.Chance(0.55)) _kit.Spawn("Top_" + _layout.TopStyle, _root, pos, _look);
            }
        }

        // Body exit → (vials) → output container standing on the ground to the right.
        private void BuildOutput(MachineDefinition def)
        {
            int last = _layout.Columns - 1;
            var exit = new Vector3(RightEdge, _baseH + (_layout.TopOf(last) + 0.5f) * Cell, PipeZ);
            float x = RightEdge + 0.7f;
            _output = LiquidContainerView.Create(_kit, _look, _root, _layout.Output.Style, Vector3.zero);
            float inletY = _output.Inlet != null ? _root.InverseTransformPoint(_output.Inlet.position).y : 1.5f;

            if (_layout.ShowSteps && def.Pipeline.Count > 0)
            {
                int n = def.Pipeline.Count;
                float bottom = inletY + 0.45f, top = Mathf.Max(exit.y, bottom + VialStep * 2f);
                int perCol = Mathf.Max(2, Mathf.FloorToInt((top - bottom) / VialStep) + 1);
                var prev = exit;
                for (int i = 0; i < n; i++)
                {
                    int col = i / perCol, row = i % perCol;
                    float y = col % 2 == 0 ? top - row * VialStep : top - (perCol - 1 - row) * VialStep;
                    var pos = new Vector3(x + col * 0.7f, y, VialZ);
                    var vial = LiquidContainerView.Create(_kit, _look, _root, "Vial", pos);
                    _vials.Add(vial);
                    Route(prev, new Vector3(pos.x, pos.y, PipeZ));
                    prev = new Vector3(pos.x, pos.y, PipeZ);
                }
                x += ((n - 1) / perCol + 1) * 0.7f + 0.5f;
                exit = prev;
            }

            float outX = x + 0.4f;
            _output.transform.localPosition = new Vector3(outX, 0f, 0.3f);
            float high = Mathf.Max(exit.y, inletY + 0.35f);
            var turn = new Vector3(exit.x + 0.35f, exit.y, PipeZ);
            if (high > exit.y + 0.01f)
            {
                Route(exit, turn);
                Route(turn, new Vector3(turn.x, high, PipeZ));
                exit = new Vector3(turn.x, high, PipeZ);
            }
            Route(exit, new Vector3(outX, high, PipeZ));
            Route(new Vector3(outX, high, PipeZ), new Vector3(outX, inletY, PipeZ));

            _stream = Art.Prim(PrimitiveType.Cylinder, _root, Vector3.zero, Vector3.one, Color.white).transform;
            _stream.gameObject.SetActive(false);
        }

        // Axis-aligned pipe runs: horizontal first, then vertical.
        private void Route(Vector3 a, Vector3 b)
        {
            var corner = new Vector3(b.x, a.y, a.z);
            Segment(a, corner);
            Segment(corner, b);
        }

        private void Segment(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) return;
            var p = _kit.Spawn("Pipe", _root, a, _look);
            p.transform.localRotation = Quaternion.FromToRotation(Vector3.right, (b - a).normalized);
            p.transform.localScale = new Vector3(len, 1f, 1f);
            _kit.Spawn("Flange", _root, a, _look).transform.localRotation = p.transform.localRotation;
            _kit.Spawn("Flange", _root, b, _look).transform.localRotation = p.transform.localRotation;
        }

        private static float ModelMinY(GameObject model)
        {
            float min = 0f;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var b = mf.sharedMesh.bounds;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                    min = Mathf.Min(min, m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2) * 2 - 1))).y);
            }
            return min;
        }

        private Bounds MeasureBounds()
        {
            var b = new Bounds(new Vector3(0f, _baseH, 0f), Vector3.zero);
            foreach (var r in _root.GetComponentsInChildren<MeshRenderer>()) b.Encapsulate(r.bounds);
            foreach (var c in _inputs) b.Encapsulate(c.LiquidBounds);
            b.Encapsulate(_output.LiquidBounds);
            return b;
        }

        // ---------------------------------------------------------------- focus
        public bool TryGetControl(Collider c, out ControlView view)
        {
            view = c != null ? c.GetComponentInParent<ControlView>() : null;
            return view != null && _controls.Contains(view);
        }

        // Focus indices: controls first, then the test trigger (index ControlCount).
        public bool TriggerFocused => Focus == _controls.Count;

        public void SetFocus(int index)
        {
            Focus = Values.Wrap(index, _controls.Count + 1);
            for (int i = 0; i < _controls.Count; i++) _controls[i].SetFocused(i == Focus);
            _trigger.SetFocused(TriggerFocused);
        }

        private Vector2 FocusPosition(int i) => i < _controls.Count
            ? (Vector2)_controls[i].transform.localPosition
            : (Vector2)(_trigger.transform.localPosition + _trigger.FocusPoint);

        // Spatial navigation: the nearest control roughly in the pressed direction.
        public void MoveFocus(Vector2 dir)
        {
            var from = FocusPosition(Focus);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i <= _controls.Count; i++)
            {
                if (i == Focus) continue;
                var d = FocusPosition(i) - from;
                float along = Vector2.Dot(d, dir);
                if (along <= 0.05f) continue;
                float score = along + Mathf.Abs(d.x * dir.y - d.y * dir.x) * 2f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) SetFocus(best);
        }

        public void Pulse(int control) => _controls[control].Pulse();

        // ---------------------------------------------------------------- state & test animation
        private void Refresh() => Refresh(false);

        private void Refresh(bool instant)
        {
            var def = _session.Definition;
            var state = _session.CopyState();
            for (int i = 0; i < _controls.Count; i++) _controls[i].Show(state[def.ControlSlot(i)], instant);
            foreach (var d in _displays) d.Show(state, instant);

            bool outputShown = _testedInputs != null;
            bool stale = outputShown && (_session.LastOutput == null || !SameInputs());
            if (stale || !outputShown)
            {
                if (stale) Skip();
                _testedInputs = null;
                for (int p = 0; p < _inputs.Count; p++)
                {
                    _inputs[p].Show(def.InputPool[_session.SelectedInputs[p]]);
                    _inputs[p].SetFill(1f, instant ? 0f : 0.35f);
                }
                _output.SetFill(0f, instant ? 0f : 0.3f);
                foreach (var v in _vials) v.SetFill(0f, instant ? 0f : 0.25f);
            }

            bool solved = _session.Solved;
            if (_fixedLamp != null) Art.Paint(_fixedLamp, solved ? Color.green : _look.Dark * 1.6f, solved ? Color.green * 3f : Color.black, 0.9f);
            SetFocus(Focus);
        }

        private bool SameInputs()
        {
            for (int i = 0; i < _testedInputs.Length; i++)
                if (_testedInputs[i] != _session.SelectedInputs[i]) return false;
            return true;
        }

        private void OnTested()
        {
            var def = _session.Definition;
            var inputs = new List<Liquid>();
            foreach (int i in _session.SelectedInputs) inputs.Add(def.InputPool[i]);
            _stages = MachineRules.Stages(def, _session.CopyState(), inputs);
            _testedInputs = (int[])_session.SelectedInputs.Clone();
            _trigger.Play();
            if (_play != null) StopCoroutine(_play);
            _play = null;
            if (AnimateTests) _play = StartCoroutine(Play(inputs));
            else Finish();
        }

        private IEnumerator Play(List<Liquid> inputs)
        {
            for (int p = 0; p < _inputs.Count; p++) _inputs[p].Show(inputs[p]);
            _output.SetFill(0f, 0.15f);
            foreach (var v in _vials) v.SetFill(0f, 0f);
            yield return new WaitForSeconds(0.15f);

            int vis = 0;
            foreach (var l in inputs) vis += l.Viscosity;
            float drain = Mathf.Lerp(0.45f, 0.9f, vis / (float)(Values.Max * inputs.Count));
            foreach (var c in _inputs) c.SetFill(0f, drain);
            yield return new WaitForSeconds(drain);

            for (int i = 0; i < _vials.Count; i++)
            {
                _vials[i].Show(_stages[i]);
                _vials[i].SetFill(1f, 0.18f);
                yield return new WaitForSeconds(0.14f);
            }

            var output = _stages[_stages.Count - 1];
            _output.Show(output);
            float fill = Mathf.Lerp(0.5f, 0.9f, output.Viscosity / (float)Values.Max);
            _output.SetFill(1f, fill);
            ShowStream(output);
            for (float t = 0f; t < fill; t += Time.deltaTime)
            {
                PlaceStream(output);
                yield return null;
            }
            _stream.gameObject.SetActive(false);
            _play = null;
        }

        // Jumps to the end of the current test animation.
        public void Skip()
        {
            if (_play == null) return;
            StopCoroutine(_play);
            _play = null;
            Finish();
        }

        private void Finish()
        {
            foreach (var c in _inputs) c.SetFill(0f, 0f);
            for (int i = 0; i < _vials.Count; i++)
            {
                _vials[i].Show(_stages[i]);
                _vials[i].SetFill(1f, 0f);
            }
            _output.Show(_stages[_stages.Count - 1]);
            _output.SetFill(1f, 0f);
            _stream.gameObject.SetActive(false);
        }

        private void ShowStream(Liquid l)
        {
            var color = Art.LiquidColor(l.Color);
            Art.Paint(_stream.GetComponent<Renderer>(), color, color * 0.6f, 0.2f + l.Refraction * 0.15f);
            _stream.gameObject.SetActive(_output.Inlet != null);
        }

        private void PlaceStream(Liquid l)
        {
            if (_output.Inlet == null) return;
            var top = _root.InverseTransformPoint(_output.Inlet.position);
            float bottom = _root.InverseTransformPoint(new Vector3(0f, _output.SurfaceY, 0f)).y;
            float len = Mathf.Max(0.05f, top.y - bottom);
            float width = Mathf.Lerp(0.05f, 0.16f, l.Viscosity / (float)Values.Max);
            _stream.localPosition = new Vector3(top.x, top.y - len / 2f, top.z);
            _stream.localScale = new Vector3(width, len / 2f, width);
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.Changed -= Refresh;
            _session.Tested -= OnTested;
        }
    }
}
