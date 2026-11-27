using UnityEngine;

namespace Contraption.Game
{
    // Idle spin for decorative fans.
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.forward;
        public float Speed = 90f;

        private void Update() => transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self);
    }
}
