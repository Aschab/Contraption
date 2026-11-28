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
        private MachineBack _back;
        // Magnifiable parts (controls, then displays) with their resting positions and magnify progress.
        private readonly List<Transform> _parts = new();
        private readonly List<Vector3> _homes = new();
        private readonly List<float> _mag = new();
        // Sound: last focus (no tick on rebuilds), verified entries before the latest test, the pending result.
        private int _lastFocus = -1;
        private bool[] _verifiedBefore = new bool[0];
        private string _result;
        private bool _seal;
        private string _triggerSound = "test_plunger";
        private AudioSource _idle;
        private Vector3 _pivot;
        private float _turn;

        public int Focus { get; private set; }
        // Something is selected (highlighted, magnified). Released by Esc / B or moving the mouse off the machine;
        // Focus keeps the last part so the next arrow press picks up there.
        public bool Engaged { get; private set; }
        public int ControlCount => _controls.Count;
        public Bounds Bounds { get; private set; }
        public bool Animating => _play != null;
        public bool HasBack => _back != null;
        // Turned around to show the rear (cables between the displays in step order). Survives rebuilds.
        public bool ShowingBack { get; private set; }
        public bool Turning => !Mathf.Approximately(_turn, ShowingBack ? 1f : 0f);

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
            _parts.Clear();
            _homes.Clear();
            _mag.Clear();
            _testedInputs = null;

            if (session != _session) ShowingBack = false;
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
            BuildRelief();

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

            foreach (var c in _controls) AddPart(c.transform);
            foreach (var d in _displays) AddPart(d.transform);

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
            BuildPlates(def);
            _idle = Sfx.Loop("machine_idle", _root, new Vector3(0f, _baseH + _layout.Rows * Cell / 2f, 0f), 0.7f, SoundChannel.Ambience);
            if (_idle != null) { _idle.time = Random.Range(0f, _idle.clip.length); _idle.Play(); }
            _back = def.Hints != null && def.Hints.Back ? BuildBack(def) : null;

            UpdateBounds();
            // Turn around the middle of the machine so it stays framed: the rear face lands where the front was.
            _pivot = new Vector3(transform.InverseTransformPoint(Bounds.center).x, 0f, Depth / 2f);
            if (_back == null) ShowingBack = false;
            _turn = ShowingBack ? 1f : 0f;
            ApplyTurn();
            Focus = 0;
            Engaged = false;
            _lastFocus = -1;
            _result = null;
            _session.Changed += Refresh;
            _session.Tested += OnTested;
            Refresh(true);
        }

        // ---------------------------------------------------------------- hints: label plates and the rear
        private float ExitY => _baseH + (_layout.TopOf(_layout.Columns - 1) + 0.5f) * Cell;

        // Labels go into the displays' label slots; Exchange steps get a plate on the output pipe.
        private void BuildPlates(MachineDefinition def)
        {
            if (def.Hints == null) return;
            var rows = new List<List<Plates.Mark>>[def.Displays.Count + 1];
            // Every display that shows a step's register carries that step's label (not just its home display).
            for (int s = 0; s < def.Pipeline.Count; s++)
            {
                var op = def.Pipeline[s];
                if (op.Kind == OpKind.Exchange)
                {
                    var marks = Plates.Sentence(def, s, def.Hints.Label(s), -1);
                    if (marks.Count > 0) (rows[def.Displays.Count] ??= new List<List<Plates.Mark>>()).Add(marks);
                    continue;
                }
                for (int d = 0; d < def.Displays.Count; d++)
                {
                    if (MachineHints.Weight(def.Displays[d], op.Register) == 0) continue;
                    var marks = Plates.Sentence(def, s, def.Hints.Label(s), d);
                    if (marks.Count > 0) (rows[d] ??= new List<List<Plates.Mark>>()).Add(marks);
                }
            }
            for (int i = 0; i < def.Displays.Count; i++) _displays[i].SetLabel(rows[i]);
            if (rows[def.Displays.Count] != null)
                Plates.Build(_root, new Vector3(RightEdge + 0.45f, ExitY + 0.36f, PipeZ - 0.12f), rows[def.Displays.Count], 1.4f, _look, false, 0.22f);
        }

        private MachineBack BuildBack(MachineDefinition def)
        {
            var nodes = new MachineBack.Node[def.Displays.Count];
            var frames = new string[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var p = _layout.Displays[i];
                var c = Center(p);
                nodes[i] = new MachineBack.Node { Center = new Vector2(c.x, c.y), Size = new Vector2(p.W * Cell, p.H * Cell) * 0.9f };
                frames[i] = "Bezel" + Size(p);
            }
            var free = new List<Vector2>();
            for (int y = 0; y < _layout.Rows; y++)
                for (int x = 0; x < _layout.Columns; x++)
                    if (_layout.IsBody(x, y) && !_layout.Displays.Exists(d => d != null && d.Overlaps(x, y, 1, 1)))
                        free.Add(new Vector2(ColumnX(x), _baseH + (y + 0.5f) * Cell));
            int inCol = _layout.Inputs.Count > 0 ? _layout.Inputs[0].Column : 0;
            var start = new Vector2(ColumnX(inCol), TopY(inCol) + 0.12f);
            var end = new Vector2(RightEdge + 0.2f, ExitY);
            return MachineBack.Create(_kit, _look, _root, def, def.Hints, nodes, frames, free, start, end, Depth);
        }

        public void Turn()
        {
            if (_back == null) return;
            ShowingBack = !ShowingBack;
            Sfx.Play("machine_turn", transform.position);
        }

        private void AddPart(Transform t)
        {
            _parts.Add(t);
            _homes.Add(t.localPosition);
            _mag.Add(0f);
        }

        private void Update()
        {
            if (_root == null) return;
            if (Turning)
            {
                _turn = Mathf.MoveTowards(_turn, ShowingBack ? 1f : 0f, Time.deltaTime / 0.9f);
                ApplyTurn();
                if (!Turning) Sfx.Play("machine_turn_end", transform.position);
            }
            Magnify();
        }

        // The focused part slides toward the camera along its view ray (so it stays in place on screen) and grows a little:
        // about twice its size on screen. Accessibility option.
        private const float MagnifyLift = 0.4f, MagnifyGrow = 1.25f, MagnifyTime = 0.2f;

        private void Magnify()
        {
            var cam = Camera.main;
            if (cam == null) return;
            int focused = Accessibility.MagnifyFocus && !ShowingBack && !Turning ? FocusedPart : -1;
            var eye = _root.InverseTransformPoint(cam.transform.position);
            for (int i = 0; i < _parts.Count; i++)
            {
                float target = i == focused ? 1f : 0f;
                if (_mag[i] == 0f && target == 0f) continue;
                _mag[i] = Mathf.MoveTowards(_mag[i], target, Time.deltaTime / MagnifyTime);
                float k = Mathf.SmoothStep(0f, 1f, _mag[i]);
                _parts[i].localPosition = _homes[i] + (eye - _homes[i]) * (MagnifyLift * k);
                _parts[i].localScale = Vector3.one * (1f + (MagnifyGrow - 1f) * k);
            }
        }

        // Index into _parts of the focused control or display; -1 for the test trigger.
        private int FocusedPart => !Engaged ? -1 : Focus < _controls.Count ? Focus : Focus > _controls.Count ? Focus - 1 : -1;

        private void ApplyTurn()
        {
            var rot = Quaternion.Euler(0f, 180f * Mathf.SmoothStep(0f, 1f, _turn), 0f);
            _root.localRotation = rot;
            _root.localPosition = _pivot - rot * _pivot;
        }

        // ---------------------------------------------------------------- geometry
        private float GridX(int i) => (i - _layout.Columns / 2f) * Cell;
        private float ColumnX(int x) => GridX(x) + Cell / 2f;
        private float TopY(int x) => _baseH + (_layout.TopOf(x) + 1) * Cell;
        private float RightEdge => GridX(_layout.Columns);

        // Raised parts stand on a housing in front of the body (matches RAISE in build_kit.py).
        private const float Raise = 0.26f;

        private Vector3 Center(Placement p) =>
            new(GridX(p.X) + p.W * Cell / 2f, _baseH + (p.Y + p.H / 2f) * Cell, p.Depth == Placement.Raised ? -Raise : 0f);

        private static string Size(Placement p) => p.W == 1 && p.H == 1 ? "" : $"_{p.W}x{p.H}";

        // Housings and bezels behind/around parts, and cornices along row boundaries that no part spans.
        private void BuildRelief()
        {
            foreach (var p in _layout.AllPlacements())
            {
                if (p.Depth == Placement.Flush) continue;
                string piece = (p.Depth == Placement.Raised ? "Housing" : "Bezel") + Size(p);
                var c = Center(p);
                if (_kit.Has(piece)) _kit.Spawn(piece, _root, new Vector3(c.x, c.y, 0f), _look);
            }
            if (!_layout.Ledges) return;
            for (int y = 0; y < _layout.Rows - 1; y++)
                for (int x = 0; x < _layout.Columns; x++)
                {
                    if (!_layout.IsBody(x, y) || !_layout.IsBody(x, y + 1) || Spanned(x, y)) continue;
                    _kit.Spawn("Ledge", _root, new Vector3(ColumnX(x), _baseH + (y + 1) * Cell, 0f), _look);
                }
        }

        private bool Spanned(int x, int y)
        {
            foreach (var p in _layout.AllPlacements())
                if (p.Overlaps(x, y, 1, 1) && p.Overlaps(x, y + 1, 1, 1)) return true;
            return false;
        }

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
                _triggerSound = "test_plunger";
                free.RemoveAt(0);
            }
            else
            {
                int row = Mathf.Min(_layout.TopOf(0), 1);
                _trigger = TestTriggerView.Create(_kit, _look, _root, "Test_Lever", new Vector3(GridX(0) - 0.02f, _baseH + (row + 0.5f) * Cell, 0.25f));
                _triggerSound = "test_lever";
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

        // Call after moving the machine.
        public void UpdateBounds() => Bounds = MeasureBounds();

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

        // Focus indices: controls first, then the test trigger (index ControlCount), then the displays (look only).
        public bool TriggerFocused => Focus == _controls.Count;
        public bool DisplayFocused => Focus > _controls.Count;
        private int FocusCount => _controls.Count + 1 + _displays.Count;

        public bool TryGetDisplay(Collider c, out int focusIndex)
        {
            var view = c != null ? c.GetComponentInParent<DisplayView>() : null;
            int i = view != null ? _displays.IndexOf(view) : -1;
            focusIndex = _controls.Count + 1 + i;
            return i >= 0;
        }

        public void SetFocus(int index)
        {
            bool was = Engaged;
            Focus = Values.Wrap(index, FocusCount);
            Engaged = true;
            ApplyFocus();
            if (_lastFocus >= 0 && (_lastFocus != Focus || !was))
            {
                bool magnifies = Accessibility.MagnifyFocus && !TriggerFocused && !ShowingBack;
                Sfx.Play(magnifies ? "magnify_in" : "focus_move", FocusWorld, magnifies ? 0.8f : 1f);
            }
            _lastFocus = Focus;
        }

        public void Release()
        {
            if (!Engaged) return;
            Engaged = false;
            ApplyFocus();
            if (Accessibility.MagnifyFocus && !ShowingBack) Sfx.Play("magnify_out", FocusWorld, 0.7f);
        }

        private void ApplyFocus()
        {
            for (int i = 0; i < _controls.Count; i++) _controls[i].SetFocused(Engaged && i == Focus);
            _trigger.SetFocused(Engaged && TriggerFocused);
            for (int i = 0; i < _displays.Count; i++) _displays[i].SetFocused(Engaged && Focus == _controls.Count + 1 + i);
        }

        private Vector3 FocusWorld => Focus < _controls.Count ? _controls[Focus].transform.position
            : TriggerFocused ? _trigger.transform.position : _displays[Focus - _controls.Count - 1].transform.position;

        // Each control gets its own pitch so two buttons on one machine sound different.
        public void PlayControl(int c, ActionKind a, int before, int after)
        {
            var def = _session.Definition.Controls[c];
            string name = def.Kind switch
            {
                ControlKind.Button => "ctl_button",
                ControlKind.Lever => after > before ? "ctl_lever_up" : "ctl_lever_down",
                ControlKind.Dial => (a == ActionKind.Increment) == (after < before) ? "ctl_dial_wrap" : "ctl_dial_click",
                ControlKind.Knob => _layout.Controls[c].Style.StartsWith("Ctl_Slider") ? "ctl_slider_step" : "ctl_knob_click",
                _ => a == ActionKind.Center ? "ctl_joystick_center" : "ctl_joystick_push",
            };
            Sfx.Play(name, _controls[c].transform.position, 1f, 1f + (c * 37 % 9 - 4) * 0.025f);
        }

        public void PlayBlocked(int c) => Sfx.Play("ctl_blocked", c < _controls.Count ? _controls[c].transform.position : transform.position);

        // Resting positions, so a magnified part doesn't skew navigation.
        private Vector2 FocusPosition(int i) =>
            i < _controls.Count ? (Vector2)_homes[i]
            : i == _controls.Count ? (Vector2)(_trigger.transform.localPosition + _trigger.FocusPoint)
            : (Vector2)_homes[i - 1];

        // Spatial navigation: the nearest control roughly in the pressed direction; false at the machine's edge.
        public bool MoveFocus(Vector2 dir)
        {
            // With nothing selected, the first press just selects the last part again.
            if (!Engaged) { SetFocus(Focus); return true; }
            var from = FocusPosition(Focus);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < FocusCount; i++)
            {
                if (i == Focus) continue;
                var d = FocusPosition(i) - from;
                float along = Vector2.Dot(d, dir);
                if (along <= 0.05f) continue;
                float score = along + Mathf.Abs(d.x * dir.y - d.y * dir.x) * 2f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) SetFocus(best);
            return best >= 0;
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
                if (stale) Skip(false);
                _testedInputs = null;
                for (int p = 0; p < _inputs.Count; p++)
                {
                    _inputs[p].Show(def.InputPool[_session.SelectedInputs[p]]);
                    _inputs[p].SetFill(1f, instant ? 0f : 0.35f);
                }
                _output.SetFill(0f, instant ? 0f : 0.3f);
                foreach (var v in _vials) v.SetFill(0f, instant ? 0f : 0.25f);
            }

            _verifiedBefore = (bool[])_session.Verified.Clone();
            bool solved = _session.Solved;
            // A fixed machine idles calmer.
            if (_idle != null) { Sfx.SetVolume(_idle, solved ? 0.3f : 0.7f); _idle.pitch = solved ? 0.92f : 1f; }
            if (_fixedLamp != null) Art.Paint(_fixedLamp, solved ? Color.green : _look.Dark * 1.6f, solved ? Color.green * 3f : Color.black, 0.9f);
            ApplyFocus();
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
            PrepareResult();
            Sfx.Play(_triggerSound, _trigger.transform.position);
            Sfx.Play("machine_start", transform.position, 0.5f);
            if (_back != null) _back.Pulse();
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
                Sfx.Play("vial_fill", _vials[i].transform.position, 0.6f);
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
            PlayResult();
        }

        // Jumps to the end of the current test animation.
        public void Skip() => Skip(true);

        private void Skip(bool sound)
        {
            if (_play == null) return;
            StopCoroutine(_play);
            _play = null;
            if (sound) Sfx.Play("test_skip");
            else _result = null;
            Finish();
        }

        // match / mismatch / no manual entry for these inputs; the machine-fixed fanfare replaces the last match.
        private void PrepareResult()
        {
            var record = _session.Log[_session.Log.Count - 1];
            int e = _session.ManualEntryFor(record);
            bool wasFixed = _verifiedBefore.Length > 0 && System.Array.TrueForAll(_verifiedBefore, v => v);
            bool ok = e >= 0 && _session.Verified[e];
            _result = _session.Solved && !wasFixed ? "machine_fixed" : e < 0 ? "result_new" : ok ? "result_match" : "result_mismatch";
            _seal = ok && (e >= _verifiedBefore.Length || !_verifiedBefore[e]);
        }

        private void PlayResult()
        {
            if (_result == null) return;
            Sfx.Play(_result);
            if (_seal) Sfx.PlayDelayed(this, 0.35f, "seal_stamp");
            _result = null;
        }

        private void Finish()
        {
            PlayResult();
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
