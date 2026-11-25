using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class ControlView : MonoBehaviour
    {
        private const float AnimSpeed = 540f;

        private ControlDef _def;
        private Transform _moving;
        private Renderer _frame;
        private float _angle, _targetAngle;
        private float _pressTimer;
        private Vector3 _restPos;

        public int Index { get; private set; }
        public ControlKind Kind => _def.Kind;

        public static ControlView Create(Transform parent, Vector3 pos, int index, ControlDef def, int labelGlyph)
        {
            var go = new GameObject($"{def.Kind} {index}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.6f);
            box.center = new Vector3(0f, 0f, -0.3f);

            var v = go.AddComponent<ControlView>();
            v.Index = index;
            v._def = def;
            v.BuildBody();
            var label = Glyphs.Build(go.transform, new Vector3(0f, -0.4f, -0.05f), 0.05f);
            Glyphs.Paint(label, labelGlyph, Art.Charcoal, Art.Cream, 0f);
            return v;
        }

        private void BuildBody()
        {
            var t = transform;
            _frame = Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.01f), new Vector3(1f, 1f, 0.02f), Art.Charcoal).GetComponent<Renderer>();
            Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.03f), new Vector3(0.9f, 0.9f, 0.03f), Art.Cream);

            _moving = new GameObject("Moving").transform;
            _moving.SetParent(t, false);

            switch (_def.Kind)
            {
                case ControlKind.Button:
                    Art.Disc(t, new Vector3(0f, 0.05f, -0.08f), 0.5f, 0.1f, Art.Brass);
                    _moving.localPosition = new Vector3(0f, 0.05f, -0.16f);
                    Art.Disc(_moving, Vector3.zero, 0.36f, 0.12f, Art.Glow * 0.8f);
                    break;
                case ControlKind.Lever:
                    Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, -0.06f), new Vector3(0.14f, 0.65f, 0.04f), Art.Charcoal);
                    _moving.localPosition = new Vector3(0f, 0.05f, -0.08f);
                    Art.Prim(PrimitiveType.Cube, _moving, new Vector3(0f, 0.17f, 0f), new Vector3(0.07f, 0.34f, 0.07f), Art.Ochre);
                    Art.Prim(PrimitiveType.Sphere, _moving, new Vector3(0f, 0.36f, 0f), Vector3.one * 0.15f, Art.Leather);
                    break;
                default:
                    bool dial = _def.Kind == ControlKind.Dial;
                    float d = dial ? 0.6f : 0.45f;
                    for (int i = 0; i < _def.Positions; i++)
                    {
                        var tick = Art.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.03f, 0.07f, 0.02f), Art.Charcoal).transform;
                        tick.localRotation = Quaternion.Euler(0f, 0f, AngleFor(i));
                        tick.localPosition = new Vector3(0f, 0.05f, -0.05f) + tick.localRotation * new Vector3(0f, d / 2f + 0.06f, 0f);
                    }
                    _moving.localPosition = new Vector3(0f, 0.05f, -0.1f);
                    Art.Disc(_moving, Vector3.zero, d, 0.12f, dial ? Art.Brass : Art.Ochre);
                    Art.Prim(PrimitiveType.Cube, _moving, new Vector3(0f, d * 0.3f, -0.07f), new Vector3(0.05f, d * 0.4f, 0.03f), Art.Charcoal);
                    break;
            }
            _restPos = _moving.localPosition;
        }

        private float AngleFor(int pos) => _def.Kind switch
        {
            ControlKind.Lever => pos == 1 ? -35f : 215f,
            ControlKind.Dial => -pos * 360f / _def.Positions,
            ControlKind.Knob => Mathf.Lerp(135f, -135f, _def.Positions > 1 ? pos / (float)(_def.Positions - 1) : 0f),
            _ => 0f,
        };

        public void Show(int position, bool instant)
        {
            _targetAngle = AngleFor(position);
            if (instant) _angle = _targetAngle;
            ApplyPose();
        }

        public void Pulse() => _pressTimer = 0.15f;

        public void SetFocused(bool focused) =>
            Art.Paint(_frame, focused ? Art.Glow : Art.Charcoal, focused ? Art.Glow * 0.6f : Color.black);

        private void Update()
        {
            _angle = Mathf.MoveTowardsAngle(_angle, _targetAngle, AnimSpeed * Time.deltaTime);
            _pressTimer = Mathf.Max(0f, _pressTimer - Time.deltaTime);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_def.Kind == ControlKind.Lever) _moving.localRotation = Quaternion.Euler(_angle, 0f, 0f);
            else if (_def.Kind != ControlKind.Button) _moving.localRotation = Quaternion.Euler(0f, 0f, _angle);
            _moving.localPosition = _restPos + new Vector3(0f, 0f, _pressTimer > 0f ? 0.05f : 0f);
        }
    }
}
