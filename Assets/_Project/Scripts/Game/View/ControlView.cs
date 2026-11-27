using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // One control on the panel. The style (footprint/model) only changes the look and the animation;
    // dials/knobs/levers/joysticks rotate their "Moving" part, sliders move it between the Min and Max markers.
    public sealed class ControlView : MonoBehaviour
    {
        private const float AnimSpeed = 540f, SlideSpeed = 4f;

        private ControlDef _def;
        private Look _look;
        private Transform _moving;
        private Renderer _frame;
        private Quaternion _restRot;
        private Vector3 _restPos, _axis, _axis2, _pushDir, _min, _max;
        private float _angle, _targetAngle, _angle2, _targetAngle2, _pressTimer, _slide, _targetSlide;
        private bool _slider, _tallLever;

        public int Index { get; private set; }
        public ControlKind Kind => _def.Kind;
        public Placement Placement { get; private set; }
        // Joystick hit-testing: centre of the stick and its scale in this transform's space.
        public Vector3 StickCenter { get; private set; }
        public float Scale => Placement.W;

        public static ControlView Create(MachineKit kit, Look look, Transform machine, Vector3 pos, int index, ControlDef def, Placement p, int labelGlyph)
        {
            var go = new GameObject($"{def.Kind} {index}");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(p.W * MachineView.Cell - 0.1f, p.H * MachineView.Cell - 0.1f, 0.6f);
            box.center = new Vector3(0f, 0f, -0.3f);

            var v = go.AddComponent<ControlView>();
            v.Index = index;
            v._def = def;
            v._look = look;
            v.Placement = p;
            var body = kit.Spawn(p.Style, go.transform, Vector3.zero, look).transform;
            v._moving = MachineKit.Find(body, "Moving");
            v._frame = MachineKit.FindSuffix(body, "_Frame")?.GetComponent<Renderer>();
            v._restRot = v._moving.localRotation;
            v._restPos = v._moving.localPosition;
            var min = MachineKit.FindOptional(body, "Min");
            var max = MachineKit.FindOptional(body, "Max");
            v._slider = min != null && max != null;
            if (v._slider)
            {
                v._min = v._moving.parent.InverseTransformPoint(min.position);
                v._max = v._moving.parent.InverseTransformPoint(max.position);
            }
            v._tallLever = def.Kind == ControlKind.Lever && p.H > 1;
            bool lever = def.Kind == ControlKind.Lever;
            bool stick = def.Kind == ControlKind.Joystick;
            v._axis = Art.AxisIn(v._moving, machine, lever || stick ? Vector3.right : Vector3.forward);
            v._axis2 = Art.AxisIn(v._moving, machine, Vector3.up);
            v._pushDir = Art.AxisIn(v._moving, machine, Vector3.forward);

            float s = p.W;
            v.StickCenter = new Vector3(0f, 0.05f * s, 0f);
            if (v._slider)
            {
                // Position ticks beside the track.
                Vector3 a = go.transform.InverseTransformPoint(min.position), b = go.transform.InverseTransformPoint(max.position);
                bool horizontal = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
                var side = horizontal ? new Vector3(0f, -0.22f, 0f) : new Vector3(-0.24f, 0f, 0f);
                for (int i = 0; i < def.Positions; i++)
                {
                    var at = Vector3.Lerp(a, b, def.Positions > 1 ? i / (float)(def.Positions - 1) : 0f);
                    Art.Prim(PrimitiveType.Cube, go.transform, new Vector3(at.x, at.y, -0.04f) + side,
                        horizontal ? new Vector3(0.03f, 0.08f, 0.02f) : new Vector3(0.08f, 0.03f, 0.02f), look.Dark);
                }
            }
            else if (def.Kind is ControlKind.Dial or ControlKind.Knob)
            {
                float radius = (def.Kind == ControlKind.Dial ? 0.42f : 0.4f) * s;
                for (int i = 0; i < def.Positions; i++)
                {
                    var rot = Quaternion.Euler(0f, 0f, v.AngleFor(i));
                    var tick = Art.Prim(PrimitiveType.Cube, go.transform, v.StickCenter + new Vector3(0f, 0f, -0.04f) + rot * new Vector3(0f, radius, 0f),
                        new Vector3(0.03f, 0.08f, 0.02f) * Mathf.Sqrt(s), look.Dark).transform;
                    tick.localRotation = rot;
                }
            }
            if (stick)
                foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    Art.Prim(PrimitiveType.Cube, go.transform, v.StickCenter + new Vector3(dir.x, dir.y, 0f) * 0.36f * s + new Vector3(0f, 0f, -0.04f),
                        new Vector3(0.07f, 0.07f, 0.02f) * s, look.Dark).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var label = Glyphs.Build(go.transform, new Vector3(0f, -p.H * MachineView.Cell / 2f + 0.17f, -0.035f), 0.045f, look.Dark);
            Glyphs.Paint(label, labelGlyph, look.Dark, look.Panel * 0.92f, 0f);
            return v;
        }

        private float AngleFor(int pos) => _def.Kind switch
        {
            ControlKind.Lever when _tallLever => pos == 1 ? -12f : -115f,
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
            else if (_slider) _targetSlide = _def.Positions > 1 ? position / (float)(_def.Positions - 1) : 0f;
            else _targetAngle = AngleFor(position);
            if (instant)
            {
                _angle = _targetAngle;
                _angle2 = _targetAngle2;
                _slide = _targetSlide;
            }
            ApplyPose();
        }

        public void Pulse() => _pressTimer = 0.15f;

        public void SetFocused(bool focused)
        {
            if (_frame != null) Art.Paint(_frame, focused ? _look.Glow : _look.Trim, focused ? _look.Glow * 0.8f : Color.black);
        }

        private void Update()
        {
            _angle = Mathf.MoveTowardsAngle(_angle, _targetAngle, AnimSpeed * Time.deltaTime);
            _angle2 = Mathf.MoveTowardsAngle(_angle2, _targetAngle2, AnimSpeed * Time.deltaTime);
            _slide = Mathf.MoveTowards(_slide, _targetSlide, SlideSpeed * Time.deltaTime);
            _pressTimer = Mathf.Max(0f, _pressTimer - Time.deltaTime);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_def.Kind == ControlKind.Joystick)
                _moving.localRotation = Quaternion.AngleAxis(_angle2, _axis2) * Quaternion.AngleAxis(_angle, _axis) * _restRot;
            else if (_def.Kind != ControlKind.Button && !_slider)
                _moving.localRotation = Quaternion.AngleAxis(_angle, _axis) * _restRot;
            var basePos = _slider ? Vector3.Lerp(_min, _max, _slide) : _restPos;
            _moving.localPosition = basePos + (_pressTimer > 0f ? _pushDir * 0.05f : Vector3.zero);
        }
    }
}
