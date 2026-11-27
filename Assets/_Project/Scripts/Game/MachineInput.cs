using System;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // Mouse is optional: everything is reachable with keyboard or gamepad. Navigate with one hand, manipulate with the other.
    // Keys: arrows move focus, W/D turn up, S/A turn down (joystick: W/A/S/D push), Space use, Z undo, R reset, T test, [ ] input, Tab port.
    // Gamepad: d-pad/left stick focus, right stick turn/push, A use, X undo, Y reset, B test, LB/RB input, Select port.
    // Mouse: click use (joystick: click a side, centre to centre), right click / scroll back.
    public sealed class MachineInput
    {
        public int ActivePort;
        private Vector2Int _lastLeft, _lastRight;

        public void Tick(MachineSession s, MachineView v, Camera cam, bool keyboardFree, Func<Vector2, bool> overGui)
        {
            if (v.Animating && AnyPress()) v.Skip();

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
                        var local = cv.transform.InverseTransformPoint(hit.point) - cv.StickCenter;
                        Do(s, v, cv.Index, local.magnitude < 0.12f * cv.Scale ? ActionKind.Center : StickAction(new Vector2(local.x, local.y)));
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
                if (kb.leftArrowKey.wasPressedThisFrame) v.MoveFocus(Vector2.left);
                if (kb.rightArrowKey.wasPressedThisFrame) v.MoveFocus(Vector2.right);
                if (kb.upArrowKey.wasPressedThisFrame) v.MoveFocus(Vector2.up);
                if (kb.downArrowKey.wasPressedThisFrame) v.MoveFocus(Vector2.down);
                if (kb.wKey.wasPressedThisFrame) Turn(s, v, Vector2Int.up);
                if (kb.sKey.wasPressedThisFrame) Turn(s, v, Vector2Int.down);
                if (kb.aKey.wasPressedThisFrame) Turn(s, v, Vector2Int.left);
                if (kb.dKey.wasPressedThisFrame) Turn(s, v, Vector2Int.right);
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
                var left = Flick(pad.leftStick.ReadValue(), ref _lastLeft);
                if (pad.dpad.left.wasPressedThisFrame) left = Vector2Int.left;
                if (pad.dpad.right.wasPressedThisFrame) left = Vector2Int.right;
                if (pad.dpad.up.wasPressedThisFrame) left = Vector2Int.up;
                if (pad.dpad.down.wasPressedThisFrame) left = Vector2Int.down;
                if (left != Vector2Int.zero) v.MoveFocus(left);
                var right = Flick(pad.rightStick.ReadValue(), ref _lastRight);
                if (right != Vector2Int.zero) Turn(s, v, right);
                if (pad.buttonSouth.wasPressedThisFrame) Use(s, v, v.Focus);
                if (pad.buttonWest.wasPressedThisFrame) s.Undo();
                if (pad.buttonNorth.wasPressedThisFrame) s.Reset();
                if (pad.buttonEast.wasPressedThisFrame) s.Test();
                if (pad.leftShoulder.wasPressedThisFrame) CycleInput(s, -1);
                if (pad.rightShoulder.wasPressedThisFrame) CycleInput(s, 1);
                if (pad.selectButton.wasPressedThisFrame) ActivePort = (ActivePort + 1) % s.Definition.InputPorts;
            }
        }

        private static bool AnyPress() =>
            (Keyboard.current?.anyKey.wasPressedThisFrame ?? false)
            || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            || (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));

        // A stick counts once per push past the threshold.
        private static Vector2Int Flick(Vector2 stick, ref Vector2Int last)
        {
            var dir = stick.magnitude < 0.6f ? Vector2Int.zero
                : Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2Int((int)Mathf.Sign(stick.x), 0) : new Vector2Int(0, (int)Mathf.Sign(stick.y));
            bool fresh = dir != last;
            last = dir;
            return fresh ? dir : Vector2Int.zero;
        }

        private static ActionKind StickAction(Vector2 d) =>
            Mathf.Abs(d.x) > Mathf.Abs(d.y)
                ? (d.x > 0 ? ActionKind.PushRight : ActionKind.PushLeft)
                : (d.y > 0 ? ActionKind.PushUp : ActionKind.PushDown);

        public void CycleInput(MachineSession s, int dir) => s.SelectInput(ActivePort, s.SelectedInputs[ActivePort] + dir);

        // Primary use: press a button, flip a lever, advance a dial/knob, centre a joystick.
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

        // Up/right turn a control up, down/left turn it down; a joystick is pushed that way.
        private static void Turn(MachineSession s, MachineView v, Vector2Int dir)
        {
            if (v.Focus >= v.ControlCount) return;
            var kind = s.Definition.Controls[v.Focus].Kind;
            if (kind == ControlKind.Button) { Use(s, v, v.Focus); return; }
            if (kind == ControlKind.Joystick) { Do(s, v, v.Focus, StickAction(dir)); return; }
            Do(s, v, v.Focus, dir.x + dir.y > 0 ? ActionKind.Increment : ActionKind.Decrement);
        }

        private static void Do(MachineSession s, MachineView v, int c, ActionKind a)
        {
            if (s.Apply(new Move(c, a))) v.Pulse(c);
        }
    }
}
