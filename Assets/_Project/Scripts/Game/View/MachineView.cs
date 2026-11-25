using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Assembles a machine from kit pieces and mirrors the session state. Owns no game state.
    // Layout units match ArtSource/Blender/build_kit.py: 1.1m columns, chassis 2.6m tall, bays 0.9m wide.
    public sealed class MachineView : MonoBehaviour
    {
        private const float Col = 1.1f, EndW = 0.35f, BayW = 0.9f, TopY = 2.68f;

        private readonly List<ControlView> _controls = new();
        private readonly List<DisplayView> _displays = new();
        private readonly List<LiquidTankView> _inputs = new();
        private LiquidTankView _output;
        private Renderer _fixedLamp;
        private MachineSession _session;
        private MachineKit _kit;
        private Transform _root;

        public int Focus { get; private set; }
        public int ControlCount => _controls.Count;
        public Bounds Bounds { get; private set; }

        public void Build(MachineSession session, MachineKit kit)
        {
            if (_session != null) _session.Changed -= Refresh;
            if (_root != null) Destroy(_root.gameObject);
            _controls.Clear();
            _displays.Clear();
            _inputs.Clear();

            _session = session;
            _kit = kit;
            var def = session.Definition;
            var rng = new Rng(def.Seed ^ 0xDEC0UL);
            _root = new GameObject("Machine").transform;
            _root.SetParent(transform, false);

            int cols = Mathf.Max(2, Mathf.Max(def.Controls.Count, def.Displays.Count));
            float w = cols * Col;

            for (int c = 0; c < cols; c++) kit.Spawn(kit.Segment, _root, new Vector3(ColumnX(c, cols), 0f, 0f));
            kit.Spawn(kit.EndL, _root, new Vector3(-w / 2f, 0f, 0f));
            kit.Spawn(kit.EndR, _root, new Vector3(w / 2f, 0f, 0f));
            kit.Spawn(kit.Foot, _root, new Vector3(-w / 2f + 0.25f, 0f, 0f));
            kit.Spawn(kit.Foot, _root, new Vector3(w / 2f - 0.25f, 0f, 0f));

            var glyphs = Glyphs.Make(def.Seed, Values.Levels + def.Controls.Count);
            var labels = glyphs[Values.Levels..];
            int dOff = (cols - def.Displays.Count) / 2, cOff = (cols - def.Controls.Count) / 2;
            for (int i = 0; i < def.Displays.Count; i++)
                _displays.Add(DisplayView.Create(kit, _root, new Vector3(ColumnX(i + dOff, cols), 1.95f, -0.02f), def.Displays[i], glyphs[..Values.Levels]));
            for (int i = 0; i < def.Controls.Count; i++)
                _controls.Add(ControlView.Create(kit, _root, new Vector3(ColumnX(i + cOff, cols), 0.72f, -0.02f), i, def.Controls[i], labels[i]));

            // Bays: inputs on the left, output on the right.
            float leftEdge = -w / 2f - EndW, rightEdge = w / 2f + EndW;
            for (int p = 0; p < def.InputPorts; p++)
            {
                float x = leftEdge - BayW / 2f - p * BayW;
                Bay(x);
                _inputs.Add(LiquidTankView.Create(kit, _root, new Vector3(x, 0.56f, 0.05f), 1f));
            }
            float outX = rightEdge + BayW / 2f;
            Bay(outX);
            _output = LiquidTankView.Create(kit, _root, new Vector3(outX, 0.56f, 0.05f), 1.12f);
            float minX = leftEdge - def.InputPorts * BayW, maxX = rightEdge + BayW;

            // Pipes from the bays into the chassis.
            Pipe(minX + BayW / 2f, leftEdge, 2.3f);
            Pipe(rightEdge, outX, 2.3f);

            // Top: decor props and crests, seeded per machine.
            var topCols = new List<int>();
            for (int c = 0; c < cols; c++) topCols.Add(c);
            rng.Shuffle(topCols);
            int decorCount = kit.Decor.Length == 0 ? 0 : Mathf.Clamp(cols / 2, 1, kit.Decor.Length);
            var decor = new List<GameObject>(kit.Decor);
            rng.Shuffle(decor);
            for (int i = 0; i < topCols.Count; i++)
            {
                var pos = new Vector3(ColumnX(topCols[i], cols), TopY, 0.4f);
                if (i < decorCount) Instantiate(decor[i], _root, false).transform.localPosition = pos;
                else if (rng.Chance(0.6)) kit.Spawn(kit.Top, _root, pos);
            }

            var lamp = kit.Spawn(kit.Light, _root, new Vector3(outX, 2.45f, 0.1f), "FixedLamp");
            lamp.transform.localScale = Vector3.one * 0.7f;
            _fixedLamp = MachineKit.Find(lamp.transform, "Bulb").GetComponent<Renderer>();

            Bounds = new Bounds(new Vector3((minX + maxX) / 2f, 1.5f, 0f), new Vector3(maxX - minX, 4.2f, 1f));
            Focus = 0;
            _session.Changed += Refresh;
            Refresh(true);
        }

        private void Bay(float x)
        {
            _kit.Spawn(_kit.Bay, _root, new Vector3(x, 0f, 0f));
            _kit.Spawn(_kit.Foot, _root, new Vector3(x, 0f, 0f));
        }

        private void Pipe(float fromX, float toX, float y)
        {
            var p = _kit.Spawn(_kit.Pipe, _root, new Vector3(fromX, y, 0.3f));
            p.transform.localScale = new Vector3(toX - fromX, 1f, 1f);
            _kit.Spawn(_kit.Flange, _root, new Vector3(fromX, y, 0.3f));
            _kit.Spawn(_kit.Flange, _root, new Vector3(toX, y, 0.3f));
        }

        private static float ColumnX(int i, int count) => (i - (count - 1) / 2f) * Col;

        public bool TryGetControl(Collider c, out ControlView view)
        {
            view = c != null ? c.GetComponentInParent<ControlView>() : null;
            return view != null && _controls.Contains(view);
        }

        public void SetFocus(int index)
        {
            if (_controls.Count == 0) return;
            Focus = Values.Wrap(index, _controls.Count);
            for (int i = 0; i < _controls.Count; i++) _controls[i].SetFocused(i == Focus);
        }

        public void Pulse(int control) => _controls[control].Pulse();

        private void Refresh() => Refresh(false);

        private void Refresh(bool instant)
        {
            var def = _session.Definition;
            var state = _session.CopyState();
            for (int i = 0; i < _controls.Count; i++) _controls[i].Show(state[def.ControlSlot(i)], instant);
            foreach (var d in _displays) d.Show(state, instant);
            for (int p = 0; p < _inputs.Count; p++) _inputs[p].Show(def.InputPool[_session.SelectedInputs[p]]);
            _output.Show(_session.LastOutput);
            bool solved = _session.Solved;
            Art.Paint(_fixedLamp, solved ? Color.green : _kit.Dark * 1.6f, solved ? Color.green * 3f : Color.black, 0.9f);
            SetFocus(Focus);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Changed -= Refresh;
        }
    }
}
