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
        private readonly Vector3 _origin;
        private readonly List<LiquidContainerView> _tubes = new();
        private readonly List<Vector3> _rest = new();
        private MachineSession _session;

        public SampleRack(MachineKit kit, Transform parent, Vector3 leftmostRack)
        {
            _kit = kit;
            _root = new GameObject("Samples").transform;
            _root.SetParent(parent, false);
            _origin = leftmostRack;
        }

        // The right edge of the racks, so the machine can keep clear of them.
        public float RightEdge(int poolSize) => _origin.x + RackWidth * (Mathf.Max(1, (poolSize + PerRack - 1) / PerRack) - 0.5f);

        public void Bind(MachineSession session)
        {
            _session = session;
            for (int i = _root.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);
            _tubes.Clear();
            _rest.Clear();
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
                    tube.Show(pool[i]);
                    tube.SetFill(1f, 0f);
                    DrawTag(tube.transform, look.Panel, i);
                    var box = tube.gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.17f, -0.02f);
                    box.size = new Vector3(0.1f, 0.36f, 0.1f);
                    tube.gameObject.AddComponent<SampleTubeMarker>().Index = i;
                    _tubes.Add(tube);
                    _rest.Add(pos);
                }
            }
        }

        private static void DrawTag(Transform tube, Color paper, int index)
        {
            var face = MachineKit.Find(tube, "TagFace");
            var content = new GameObject("Tag").transform;
            content.SetParent(face, false);
            new PageDrawer(content, paper).SampleSymbol(Vector2.zero, 0.03f, index, PageDrawer.Ink, 0.004f);
        }

        public void Tick(Camera cam, Func<Vector2, bool> overGui, int activePort)
        {
            if (_session == null) return;
            for (int i = 0; i < _tubes.Count; i++)
            {
                bool loaded = Array.IndexOf(_session.SelectedInputs, i) >= 0;
                var t = _tubes[i].transform;
                var target = _rest[i] + (loaded ? new Vector3(0f, 0.07f, 0f) : Vector3.zero);
                t.localPosition = Vector3.MoveTowards(t.localPosition, target, Time.deltaTime * 0.6f);
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var p = mouse.position.ReadValue();
            if (overGui(new Vector2(p.x, Screen.height - p.y))) return;
            if (!Physics.Raycast(cam.ScreenPointToRay(p), out var hit, 100f)) return;
            var marker = hit.collider.GetComponent<SampleTubeMarker>();
            if (marker != null) _session.SelectInput(activePort, marker.Index);
        }
    }
}
