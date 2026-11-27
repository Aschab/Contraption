using System;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // Mouse is optional: everything is reachable with keyboard or gamepad.
    // Mouse: click = use, right click / scroll = turn back.  Keys: ←→ focus, ↑↓ turn, Space use, Z undo, R reset, T test, [ ] input, Tab port.
    // Gamepad: d-pad ←→ focus, ↑↓ turn, A use, X undo, Y reset, B test, LB/RB input, Select port.
    // Joystick control: click its side / Shift+←→ / ↑↓ / right stick push; click centre / Space / A / right click centre it.
    public sealed class MachineInput
    {
        public int ActivePort;
        private Vector2Int _lastStick;

        public void Tick(MachineSession s, MachineView v, Camera cam, bool keyboardFree, Func<Vector2, bool> overGui)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                bool blocked = overGui(new Vector2(pos.x, Screen.height - pos.y));
                if (!blocked && Physics.Raycast(cam.ScreenPointToRay(pos), out var hit, 100f) && v.TryGetControl(hit.collider, out var cv))
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (mouse.leftButton.wasPressedThisFrame && cv.Kind == ControlKind.Joystick)
                    {
                        v.SetFocus(cv.Index);
                        var local = cv.transform.InverseTransformPoint(hit.point) - new Vector3(0f, 0.05f, 0f);
                        Do(s, v, cv.Index, local.magnitude < 0.12f ? ActionKind.Center : StickAction(new Vector2(local.x, local.y)));
                    }
                    else if (mouse.leftButton.wasPressedThisFrame) { v.SetFocus(cv.Index); Use(s, v, cv.Index); }
                    else if (mouse.rightButton.wasPressedThisFrame)
                    {
                        v.SetFocus(cv.Index);
                        Do(s, v, cv.Index, cv.Kind == ControlKind.Joystick ? ActionKind.Center : ActionKind.Decrement);
                    }
                    else if (scroll > 0.1f) Do(s, v, cv.Index, ActionKind.Increment);
                    else if (scroll < -0.1f) Do(s, v, cv.Index, ActionKind.Decrement);
                }
            }

            var kb = Keyboard.current;
            if (kb != null && keyboardFree)
            {
                bool shift = kb.shiftKey.isPressed && FocusedKind(s, v) == ControlKind.Joystick;
                if (kb.leftArrowKey.wasPressedThisFrame) { if (shift) Do(s, v, v.Focus, ActionKind.PushLeft); else v.SetFocus(v.Focus - 1); }
                if (kb.rightArrowKey.wasPressedThisFrame) { if (shift) Do(s, v, v.Focus, ActionKind.PushRight); else v.SetFocus(v.Focus + 1); }
                if (kb.upArrowKey.wasPressedThisFrame) Turn(s, v, true);
                if (kb.downArrowKey.wasPressedThisFrame) Turn(s, v, false);
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Use(s, v, v.Focus);
                if (kb.zKey.wasPressedThisFrame) s.Undo();
                if (kb.rKey.wasPressedThisFrame) s.Reset();
                if (kb.tKey.wasPressedThisFrame) s.Test();
                if (kb.leftBracketKey.wasPressedThisFrame) CycleInput(s, -1);
                if (kb.rightBracketKey.wasPressedThisFrame) CycleInput(s, 1);
                if (kb.tabKey.wasPressedThisFrame) ActivePort = (ActivePort + 1) % s.Definition.InputPorts;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) v.SetFocus(v.Focus - 1);
                if (pad.dpad.right.wasPressedThisFrame) v.SetFocus(v.Focus + 1);
                if (pad.dpad.up.wasPressedThisFrame) Turn(s, v, true);
                if (pad.dpad.down.wasPressedThisFrame) Turn(s, v, false);
                if (pad.buttonSouth.wasPressedThisFrame) Use(s, v, v.Focus);
                if (pad.buttonWest.wasPressedThisFrame) s.Undo();
                if (pad.buttonNorth.wasPressedThisFrame) s.Reset();
                if (pad.buttonEast.wasPressedThisFrame) s.Test();
                if (pad.leftShoulder.wasPressedThisFrame) CycleInput(s, -1);
                if (pad.rightShoulder.wasPressedThisFrame) CycleInput(s, 1);
                if (pad.selectButton.wasPressedThisFrame) ActivePort = (ActivePort + 1) % s.Definition.InputPorts;

                var r = pad.rightStick.ReadValue();
                var dir = r.magnitude < 0.6f ? Vector2Int.zero
                    : Mathf.Abs(r.x) > Mathf.Abs(r.y) ? new Vector2Int((int)Mathf.Sign(r.x), 0) : new Vector2Int(0, (int)Mathf.Sign(r.y));
                if (dir != _lastStick && dir != Vector2Int.zero && FocusedKind(s, v) == ControlKind.Joystick)
                    Do(s, v, v.Focus, StickAction(dir));
                _lastStick = dir;
            }
        }

        private static ControlKind? FocusedKind(MachineSession s, MachineView v) =>
            v.Focus < v.ControlCount ? s.Definition.Controls[v.Focus].Kind : null;

        private static ActionKind StickAction(Vector2 d) =>
            Mathf.Abs(d.x) > Mathf.Abs(d.y)
                ? (d.x > 0 ? ActionKind.PushRight : ActionKind.PushLeft)
                : (d.y > 0 ? ActionKind.PushUp : ActionKind.PushDown);

        public void CycleInput(MachineSession s, int dir) => s.SelectInput(ActivePort, s.SelectedInputs[ActivePort] + dir);

        // Primary use: press a button, flip a lever, advance a dial/knob.
        private static void Use(MachineSession s, MachineView v, int c)
        {
            if (c >= v.ControlCount) return;
            var def = s.Definition.Controls[c];
            switch (def.Kind)
            {
                case ControlKind.Button: Do(s, v, c, ActionKind.Press); break;
                case ControlKind.Joystick: Do(s, v, c, ActionKind.Center); break;
                case ControlKind.Lever:
                    Do(s, v, c, s.State[s.Definition.ControlSlot(c)] == 0 ? ActionKind.Increment : ActionKind.Decrement);
                    break;
                default: Do(s, v, c, ActionKind.Increment); break;
            }
        }

        private static void Turn(MachineSession s, MachineView v, bool up)
        {
            if (v.Focus >= v.ControlCount) return;
            var kind = s.Definition.Controls[v.Focus].Kind;
            if (kind == ControlKind.Button) { Use(s, v, v.Focus); return; }
            if (kind == ControlKind.Joystick) { Do(s, v, v.Focus, up ? ActionKind.PushUp : ActionKind.PushDown); return; }
            Do(s, v, v.Focus, up ? ActionKind.Increment : ActionKind.Decrement);
        }

        private static void Do(MachineSession s, MachineView v, int c, ActionKind a)
        {
            if (s.Apply(new Move(c, a))) v.Pulse(c);
        }
    }
}
