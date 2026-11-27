using UnityEngine;

namespace Contraption.Game
{
    // The machine's big test trigger. Clicking it runs a test; it animates on every test, however it was started.
    public sealed class TestTriggerView : MonoBehaviour
    {
        private Transform _moving;
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
            return v;
        }

        public void Play() => _t = 0f;

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
