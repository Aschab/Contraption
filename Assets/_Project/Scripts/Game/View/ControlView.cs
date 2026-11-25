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
        private Vector3 _restPos, _axis, _pushDir;
        private float _angle, _targetAngle, _pressTimer;

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
            v._axis = Art.AxisIn(v._moving, machine, lever ? Vector3.right : Vector3.forward);
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
            _targetAngle = AngleFor(position);
            if (instant) _angle = _targetAngle;
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
            _pressTimer = Mathf.Max(0f, _pressTimer - Time.deltaTime);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_def.Kind != ControlKind.Button) _moving.localRotation = Quaternion.AngleAxis(_angle, _axis) * _restRot;
            _moving.localPosition = _restPos + (_pressTimer > 0f ? _pushDir * 0.05f : Vector3.zero);
        }
    }
}
