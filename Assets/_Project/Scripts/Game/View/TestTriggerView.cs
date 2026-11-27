using UnityEngine;

namespace Contraption.Game
{
    // The machine's big test trigger. Clicking it runs a test; it animates on every test, however it was started.
    public sealed class TestTriggerView : MonoBehaviour
    {
        private Transform _moving;
        private GameObject _focusFrame;
        private Vector3 _restPos, _down, _axis;
        private Quaternion _restRot;
        private bool _lever;
        private float _t = 1f;

        public static TestTriggerView Create(MachineKit kit, Transform machine, string style, Vector3 pos)
        {
            var go = new GameObject("Test trigger");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            var v = go.AddComponent<TestTriggerView>();
            var body = kit.Spawn(style, go.transform, Vector3.zero).transform;
            v._lever = style == "Test_Lever";
            v._moving = MachineKit.Find(body, "Moving");
            v._restPos = v._moving.localPosition;
            v._restRot = v._moving.localRotation;
            v._down = Art.AxisIn(v._moving, machine, Vector3.down);
            v._axis = Art.AxisIn(v._moving, machine, Vector3.forward);
            var box = go.AddComponent<BoxCollider>();
            box.center = v._lever ? new Vector3(-0.4f, 0.4f, 0f) : new Vector3(0f, 0.45f, 0.45f);
            box.size = v._lever ? new Vector3(0.6f, 1.6f, 0.6f) : new Vector3(0.8f, 0.9f, 0.8f);
            v.FocusPoint = box.center;

            // Glowing outline in front of the trigger while it has keyboard/gamepad focus.
            v._focusFrame = new GameObject("Focus");
            v._focusFrame.transform.SetParent(go.transform, false);
            float hw = box.size.x / 2f + 0.05f, hh = box.size.y / 2f + 0.05f, z = box.center.z - box.size.z / 2f - 0.05f;
            var c = box.center;
            foreach (var (at, size) in new[]
            {
                (new Vector3(c.x, c.y + hh, z), new Vector3(hw * 2f, 0.05f, 0.03f)), (new Vector3(c.x, c.y - hh, z), new Vector3(hw * 2f, 0.05f, 0.03f)),
                (new Vector3(c.x - hw, c.y, z), new Vector3(0.05f, hh * 2f, 0.03f)), (new Vector3(c.x + hw, c.y, z), new Vector3(0.05f, hh * 2f, 0.03f)),
            })
            {
                var bar = Art.Prim(PrimitiveType.Cube, v._focusFrame.transform, at, size, kit.Glow);
                Art.Paint(bar.GetComponent<Renderer>(), kit.Glow, kit.Glow * 1.5f);
            }
            v._focusFrame.SetActive(false);
            return v;
        }

        // Local point used for spatial focus navigation.
        public Vector3 FocusPoint { get; private set; }

        public void Play() => _t = 0f;

        public void SetFocused(bool focused) => _focusFrame.SetActive(focused);

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / (_lever ? 0.8f : 0.45f));
            // Quick press/pull, slower spring back.
            float k = _t < 0.25f ? _t / 0.25f : 1f - Mathf.SmoothStep(0f, 1f, (_t - 0.25f) / 0.75f);
            if (_lever) _moving.localRotation = Quaternion.AngleAxis(110f * k, _axis) * _restRot;
            else _moving.localPosition = _restPos + _down * 0.14f * k;
        }
    }
}
