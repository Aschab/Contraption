using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class ControlView : MonoBehaviour
    {
        private const float AnimSpeed = 540f;

        private ControlDef _def;
        private MachineKit _kit;
        private Transform _moving;
        private Renderer _frame;
        private Quaternion _restRot;
        private Vector3 _restPos, _axis, _axis2, _pushDir;
        private float _angle, _targetAngle, _angle2, _targetAngle2, _pressTimer;

        public int Index { get; private set; }
        public ControlKind Kind => _def.Kind;

        public static ControlView Create(MachineKit kit, Transform machine, Vector3 pos, int index, ControlDef def, int labelGlyph)
        {
            var go = new GameObject($"{def.Kind} {index}");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.6f);
            box.center = new Vector3(0f, 0f, -0.3f);

            var model = def.Kind switch
            {
                ControlKind.Button => kit.Button,
                ControlKind.Lever => kit.Lever,
                ControlKind.Dial => kit.Dial,
                ControlKind.Joystick => kit.Joystick,
                _ => kit.Knob,
            };
            var v = go.AddComponent<ControlView>();
            v.Index = index;
            v._def = def;
            v._kit = kit;
            var body = kit.Spawn(model, go.transform, Vector3.zero).transform;
            v._moving = MachineKit.Find(body, "Moving");
            v._frame = MachineKit.FindSuffix(body, "_Frame")?.GetComponent<Renderer>();
            v._restRot = v._moving.localRotation;
            v._restPos = v._moving.localPosition;
            bool lever = def.Kind == ControlKind.Lever;
            bool stick = def.Kind == ControlKind.Joystick;
            v._axis = Art.AxisIn(v._moving, machine, lever || stick ? Vector3.right : Vector3.forward);
            v._axis2 = Art.AxisIn(v._moving, machine, Vector3.up);
            v._pushDir = Art.AxisIn(v._moving, machine, Vector3.forward);

            if (def.Kind is ControlKind.Dial or ControlKind.Knob)
            {
                float radius = def.Kind == ControlKind.Dial ? 0.42f : 0.4f;
                for (int i = 0; i < def.Positions; i++)
                {
                    var rot = Quaternion.Euler(0f, 0f, v.AngleFor(i));
                    var tick = Art.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.05f, -0.04f) + rot * new Vector3(0f, radius, 0f),
                        new Vector3(0.03f, 0.08f, 0.02f), kit.Dark).transform;
                    tick.localRotation = rot;
                }
            }
            if (stick)
                foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    Art.Prim(PrimitiveType.Cube, go.transform, new Vector3(dir.x * 0.36f, 0.05f + dir.y * 0.36f, -0.04f),
                        new Vector3(0.07f, 0.07f, 0.02f), kit.Dark).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var label = Glyphs.Build(go.transform, new Vector3(0f, -0.38f, -0.035f), 0.045f, kit.Dark);
            Glyphs.Paint(label, labelGlyph, kit.Dark, kit.Panel * 0.92f, 0f);
            return v;
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
            if (_def.Kind == ControlKind.Joystick)
            {
                // Tilt toward the pushed direction: X axis lifts the ball up/down, Y axis swings it left/right.
                const float tilt = 28f;
                _targetAngle = position == 1 ? tilt : position == 2 ? -tilt : 0f;
                _targetAngle2 = position == 3 ? tilt : position == 4 ? -tilt : 0f;
            }
            else _targetAngle = AngleFor(position);
            if (instant)
            {
                _angle = _targetAngle;
                _angle2 = _targetAngle2;
            }
            ApplyPose();
        }

        public void Pulse() => _pressTimer = 0.15f;

        public void SetFocused(bool focused)
        {
            if (_frame != null) Art.Paint(_frame, focused ? _kit.Glow : _kit.Trim, focused ? _kit.Glow * 0.8f : Color.black);
        }

        private void Update()
        {
            _angle = Mathf.MoveTowardsAngle(_angle, _targetAngle, AnimSpeed * Time.deltaTime);
            _angle2 = Mathf.MoveTowardsAngle(_angle2, _targetAngle2, AnimSpeed * Time.deltaTime);
            _pressTimer = Mathf.Max(0f, _pressTimer - Time.deltaTime);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_def.Kind == ControlKind.Joystick)
                _moving.localRotation = Quaternion.AngleAxis(_angle2, _axis2) * Quaternion.AngleAxis(_angle, _axis) * _restRot;
            else if (_def.Kind != ControlKind.Button)
                _moving.localRotation = Quaternion.AngleAxis(_angle, _axis) * _restRot;
            _moving.localPosition = _restPos + (_pressTimer > 0f ? _pushDir * 0.05f : Vector3.zero);
        }
    }
}
