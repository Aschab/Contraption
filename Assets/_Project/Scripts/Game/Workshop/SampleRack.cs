using System;
using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // The input pool as sample tubes in racks on the table's left side. Each tube's tag carries its sample symbol
    // (the same symbol the books use). Tubes loaded into the machine stand raised; clicking a tube loads it into the active port.
    public sealed class SampleRack
    {
        private const int PerRack = 4;
        private const float RackWidth = 0.7f;

        private readonly MachineKit _kit;
        private readonly Transform _root;
        private Vector3 _origin;
        private readonly List<LiquidContainerView> _tubes = new();
        private readonly List<Vector3> _rest = new();
        private readonly List<bool> _loaded = new();
        private MachineSession _session;

        public SampleRack(MachineKit kit, Transform parent, Vector3 leftmostRack)
        {
            _kit = kit;
            _root = new GameObject("Samples").transform;
            _root.SetParent(parent, false);
            _origin = leftmostRack;
        }

        private static int Racks(int poolSize) => Mathf.Max(1, (poolSize + PerRack - 1) / PerRack);

        // Racks stand just left of the machine (call before Bind).
        public void PlaceLeftOf(float x, int poolSize) => _origin.x = x - RackWidth * (Racks(poolSize) - 0.5f);

        public Bounds Area(int poolSize) =>
            new(_origin + new Vector3(RackWidth * (Racks(poolSize) - 1) / 2f, 0.2f, 0f), new Vector3(RackWidth * Racks(poolSize), 0.4f, 0.2f));

        public void Bind(MachineSession session)
        {
            _session = session;
            for (int i = _root.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);
            _tubes.Clear();
            _rest.Clear();
            _loaded.Clear();
            var look = _kit.DefaultLook();
            var pool = session.Definition.InputPool;
            for (int r = 0; r * PerRack < pool.Count; r++)
            {
                var rack = _kit.Spawn("Sample_Rack", _root, _origin + new Vector3(r * RackWidth, 0f, 0f), look).transform;
                for (int k = 0; k < PerRack && r * PerRack + k < pool.Count; k++)
                {
                    int i = r * PerRack + k;
                    var slot = MachineKit.Find(rack, "Slot" + k);
                    var pos = _root.InverseTransformPoint(slot.position);
                    var tube = LiquidContainerView.Create(_kit, look, _root, "Sample_Tube", pos);
                    tube.Brightness = 0.35f;   // dimmer than the machine's tanks so the tags stay readable
                    tube.Audible = false;
                    tube.Show(pool[i]);
                    tube.SetFill(1f, 0f);
                    DrawTag(tube.transform, look.Panel, i);
                    var box = tube.gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.17f, -0.02f);
                    box.size = new Vector3(0.1f, 0.36f, 0.1f);
                    tube.gameObject.AddComponent<SampleTubeMarker>().Index = i;
                    _tubes.Add(tube);
                    _rest.Add(pos);
                    _loaded.Add(Array.IndexOf(session.SelectedInputs, i) >= 0);
                }
            }
        }

        private static void DrawTag(Transform tube, Color paper, int index)
        {
            var face = MachineKit.Find(tube, "TagFace");
            var content = new GameObject("Tag").transform;
            content.SetParent(face, false);
            new PageDrawer(content, paper).SampleSymbol(Vector2.zero, 0.052f, index, PageDrawer.Ink, 0.0065f);
        }

        public void Tick(Camera cam, Func<Vector2, bool> overGui, int activePort)
        {
            if (_session == null) return;
            for (int i = 0; i < _tubes.Count; i++)
            {
                bool loaded = Array.IndexOf(_session.SelectedInputs, i) >= 0;
                var t = _tubes[i].transform;
                if (loaded != _loaded[i]) Sfx.Play(loaded ? "tube_lift" : "tube_drop", t.position);
                _loaded[i] = loaded;
                var target = _rest[i] + (loaded ? new Vector3(0f, 0.07f, 0f) : Vector3.zero);
                t.localPosition = Vector3.MoveTowards(t.localPosition, target, Time.deltaTime * 0.6f);
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var p = mouse.position.ReadValue();
            if (overGui(new Vector2(p.x, Screen.height - p.y))) return;
            if (!Physics.Raycast(cam.ScreenPointToRay(p), out var hit, 100f)) return;
            var marker = hit.collider.GetComponent<SampleTubeMarker>();
            if (marker == null) return;
            if (_session.SelectedInputs[activePort] == marker.Index) Sfx.Play("tube_clink", hit.point);
            _session.SelectInput(activePort, marker.Index);
        }
    }
}
