using System;
using UnityEngine;

namespace Contraption.Game
{
    // Flies the camera through the workshop along Workshop.IntroPath and lands on the table framing.
    // Any key or click skips it. Calls onDone when the camera has arrived.
    public sealed class IntroFlight : MonoBehaviour
    {
        private const float Duration = 8f;

        private Camera _cam;
        private Func<(Vector3 pos, Vector3 look)> _end;
        private Action _onDone;
        private float _t;

        public static IntroFlight Play(Camera cam, Func<(Vector3 pos, Vector3 look)> end, Action onDone)
        {
            var f = cam.gameObject.AddComponent<IntroFlight>();
            f._cam = cam;
            f._end = end;
            f._onDone = onDone;
            return f;
        }

        public void Skip() => _t = Duration;

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            var keys = Workshop.IntroPath;
            var end = _end();
            int n = keys.Length + 1;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / Duration)) * (n - 1);
            int i = Mathf.Min(Mathf.FloorToInt(u), n - 2);
            float f = u - i;
            Vector3 P(int k) => k <= 0 ? keys[0].pos : k >= n - 1 ? end.pos : keys[k].pos;
            Vector3 L(int k) => k <= 0 ? keys[0].look : k >= n - 1 ? end.look : keys[k].look;
            var pos = CatmullRom(P(i - 1), P(i), P(i + 1), P(i + 2), f);
            var look = Vector3.Lerp(L(i), L(i + 1), Mathf.SmoothStep(0f, 1f, f));
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation(look - pos);
            if (_t < Duration) return;
            _onDone?.Invoke();
            Destroy(this);
        }

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) =>
            0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t);
    }
}
