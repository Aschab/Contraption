using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Builds a machine's visuals from its definition and mirrors the session state. Owns no game state.
    public sealed class MachineView : MonoBehaviour
    {
        private const float Cell = 1.1f;

        private readonly List<ControlView> _controls = new();
        private readonly List<DisplayView> _displays = new();
        private readonly List<LiquidTankView> _inputs = new();
        private LiquidTankView _output;
        private Renderer _fixedLamp;
        private MachineSession _session;
        private Transform _root;

        public int Focus { get; private set; }
        public int ControlCount => _controls.Count;
        public Bounds Bounds { get; private set; }

        public void Build(MachineSession session)
        {
            if (_session != null) _session.Changed -= Refresh;
            if (_root != null) Destroy(_root.gameObject);
            _controls.Clear();
            _displays.Clear();
            _inputs.Clear();

            _session = session;
            var def = session.Definition;
            _root = new GameObject("Machine").transform;
            _root.SetParent(transform, false);

            int cols = Mathf.Max(2, Mathf.Max(def.Controls.Count, def.Displays.Count));
            float w = cols * Cell + 0.6f;

            // Chassis
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 1.3f, 0.45f), new Vector3(w, 2.6f, 0.9f), Art.Charcoal);
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 2.65f, 0.45f), new Vector3(w + 0.1f, 0.15f, 1f), Art.Ochre);
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 0.02f, 0.45f), new Vector3(w + 0.1f, 0.15f, 1f), Art.Ochre);
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 1.95f, -0.01f), new Vector3(w - 0.3f, 1.05f, 0.04f), Art.Cream * 0.85f);
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 0.72f, -0.01f), new Vector3(w - 0.3f, 1.15f, 0.04f), Art.Cream * 0.85f);
            foreach (float x in new[] { -w / 2f + 0.3f, w / 2f - 0.3f })
                Art.Prim(PrimitiveType.Cube, _root, new Vector3(x, -0.3f, 0.45f), new Vector3(0.3f, 0.5f, 0.5f), Art.Steel);
            _fixedLamp = Art.Prim(PrimitiveType.Sphere, _root, new Vector3(0f, 2.9f, 0.3f), Vector3.one * 0.3f, Art.Unlit).GetComponent<Renderer>();

            var glyphs = Glyphs.Make(def.Seed, Values.Levels + def.Controls.Count);
            var labels = glyphs[Values.Levels..];

            for (int i = 0; i < def.Displays.Count; i++)
                _displays.Add(DisplayView.Create(_root, new Vector3(Column(i, def.Displays.Count), 1.95f, -0.04f), def.Displays[i], glyphs[..Values.Levels]));
            for (int i = 0; i < def.Controls.Count; i++)
                _controls.Add(ControlView.Create(_root, new Vector3(Column(i, def.Controls.Count), 0.72f, -0.04f), i, def.Controls[i], labels[i]));

            // Input module (left) and output module (right)
            float inX = -w / 2f - 0.15f - def.InputPorts * 0.35f;
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(inX, 1.0f, 0.5f), new Vector3(def.InputPorts * 0.7f + 0.2f, 2.0f, 0.7f), Art.Ochre);
            for (int p = 0; p < def.InputPorts; p++)
                _inputs.Add(LiquidTankView.Create(_root, new Vector3(inX + (p - (def.InputPorts - 1) / 2f) * 0.7f, 1.3f, 0.1f), 1.1f, 0.45f));
            float outX = w / 2f + 0.55f;
            Art.Prim(PrimitiveType.Cube, _root, new Vector3(outX, 1.0f, 0.5f), new Vector3(0.9f, 2.0f, 0.7f), Art.Ochre);
            _output = LiquidTankView.Create(_root, new Vector3(outX, 1.4f, 0.05f), 1.4f, 0.6f);
            Art.Prim(PrimitiveType.Cylinder, _root, new Vector3(0f, 2.45f, -0.05f), new Vector3(0.12f, (outX - inX) / 2f, 0.12f), Art.Brass)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Bounds = new Bounds(new Vector3((inX - def.InputPorts * 0.35f - 0.2f + outX + 0.5f) / 2f, 1.3f, 0f),
                new Vector3(outX + 0.5f - (inX - def.InputPorts * 0.35f - 0.2f), 3.6f, 1f));

            Focus = 0;
            _session.Changed += Refresh;
            Refresh(true);
        }

        private static float Column(int i, int count) => (i - (count - 1) / 2f) * Cell;

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
            Art.Paint(_fixedLamp, solved ? Color.green : Art.Unlit, solved ? Color.green * 3f : Color.black, 0.9f);
            SetFocus(Focus);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Changed -= Refresh;
        }
    }
}
