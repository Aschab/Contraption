using System;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // Mouse is optional: everything is reachable with keyboard or gamepad. Navigate with one hand, manipulate with the other.
    // The test trigger is part of focus navigation; using or turning it runs a test.
    // Moving focus past the machine's left/right edge lifts the manual/logbook (atEdge).
    // Keys: arrows move focus, W/D turn up, S/A turn down (joystick: W/A/S/D push), Space use, Z undo, R reset, T test, [ ] input, Tab port,
    // F turn the machine around (when it has a rear view).
    // Gamepad: d-pad/left stick focus, right stick turn/push, A use, X undo, Y reset, B test, LB/RB input, Select port, R3 turn around.
    // Mouse: hover focuses a control, click use (joystick: click a side, centre to centre), right click / scroll back; click the big plunger/lever to test.
    public sealed class MachineInput
    {
        public int ActivePort;
        private Vector2Int _lastLeft, _lastRight;

        // arrowsFree: false while a book is up (arrows/d-pad turn its pages instead of moving focus).
        public void Tick(MachineSession s, MachineView v, Camera cam, bool keyboardFree, bool arrowsFree, Func<Vector2, bool> overGui, Action<int> atEdge)
        {
            if (v.Animating && AnyPress()) v.Skip();
            if (v.HasBack && ((keyboardFree && (Keyboard.current?.fKey.wasPressedThisFrame ?? false))
                              || (Gamepad.current?.rightStickButton.wasPressedThisFrame ?? false)))
                v.Turn();
            // Controls are out of reach while the machine shows its rear; tests still run (the cables light up).
            if (v.ShowingBack || v.Turning)
            {
                if (keyboardFree && (Keyboard.current?.tKey.wasPressedThisFrame ?? false)) s.Test();
                if (arrowsFree && (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false)) s.Test();
                var m = Mouse.current;
                if (m != null && m.leftButton.wasPressedThisFrame && !overGui(new Vector2(m.position.ReadValue().x, Screen.height - m.position.ReadValue().y))
                    && Physics.Raycast(cam.ScreenPointToRay(m.position.ReadValue()), out var h, 100f) && v.IsTestTrigger(h.collider))
                    s.Test();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                bool blocked = overGui(new Vector2(pos.x, Screen.height - pos.y));
                RaycastHit hit = default;
                bool hitSomething = !blocked && Physics.Raycast(cam.ScreenPointToRay(pos), out hit, 100f);
                if (hitSomething && mouse.delta.ReadValue().sqrMagnitude > 0f)
                {
                    if (v.TryGetControl(hit.collider, out var hovered)) { if (v.Focus != hovered.Index) v.SetFocus(hovered.Index); }
                    else if (v.IsTestTrigger(hit.collider) && !v.TriggerFocused) v.SetFocus(v.ControlCount);
                    else if (v.TryGetDisplay(hit.collider, out int display) && v.Focus != display) v.SetFocus(display);
                }
                if (hitSomething && mouse.leftButton.wasPressedThisFrame && v.IsTestTrigger(hit.collider)) s.Test();
                else if (hitSomething && v.TryGetControl(hit.collider, out var cv))
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
                if (arrowsFree && kb.leftArrowKey.wasPressedThisFrame) Move(v, Vector2Int.left, atEdge);
                if (arrowsFree && kb.rightArrowKey.wasPressedThisFrame) Move(v, Vector2Int.right, atEdge);
                if (arrowsFree && kb.upArrowKey.wasPressedThisFrame) Move(v, Vector2Int.up, atEdge);
                if (arrowsFree && kb.downArrowKey.wasPressedThisFrame) Move(v, Vector2Int.down, atEdge);
                if (kb.wKey.wasPressedThisFrame) Turn(s, v, Vector2Int.up);
                if (kb.sKey.wasPressedThisFrame) Turn(s, v, Vector2Int.down);
                if (kb.aKey.wasPressedThisFrame) Turn(s, v, Vector2Int.left);
                if (kb.dKey.wasPressedThisFrame) Turn(s, v, Vector2Int.right);
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Use(s, v, v.Focus);
                if (kb.zKey.wasPressedThisFrame) Undo(s);
                if (kb.rKey.wasPressedThisFrame) Reset(s);
                if (kb.tKey.wasPressedThisFrame) s.Test();
                if (kb.leftBracketKey.wasPressedThisFrame) CycleInput(s, -1);
                if (kb.rightBracketKey.wasPressedThisFrame) CycleInput(s, 1);
                if (kb.tabKey.wasPressedThisFrame) SwitchPort(s);
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                var left = Flick(pad.leftStick.ReadValue(), ref _lastLeft);
                if (arrowsFree && pad.dpad.left.wasPressedThisFrame) left = Vector2Int.left;
                if (arrowsFree && pad.dpad.right.wasPressedThisFrame) left = Vector2Int.right;
                if (arrowsFree && pad.dpad.up.wasPressedThisFrame) left = Vector2Int.up;
                if (arrowsFree && pad.dpad.down.wasPressedThisFrame) left = Vector2Int.down;
                if (left != Vector2Int.zero && arrowsFree) Move(v, left, atEdge);
                var right = Flick(pad.rightStick.ReadValue(), ref _lastRight);
                if (right != Vector2Int.zero) Turn(s, v, right);
                if (pad.buttonSouth.wasPressedThisFrame) Use(s, v, v.Focus);
                if (pad.buttonWest.wasPressedThisFrame) Undo(s);
                if (pad.buttonNorth.wasPressedThisFrame) Reset(s);
                if (pad.buttonEast.wasPressedThisFrame && arrowsFree) s.Test();   // with a book up, B puts it down
                if (pad.leftShoulder.wasPressedThisFrame) CycleInput(s, -1);
                if (pad.rightShoulder.wasPressedThisFrame) CycleInput(s, 1);
                if (pad.selectButton.wasPressedThisFrame) SwitchPort(s);
            }
        }

        private static void Move(MachineView v, Vector2Int dir, Action<int> atEdge)
        {
            if (!v.MoveFocus(dir) && dir.x != 0) atEdge(dir.x);
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
            if (c == v.ControlCount) { s.Test(); return; }
            if (c > v.ControlCount) return;   // a display: look only
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
            if (v.TriggerFocused) { s.Test(); return; }
            if (v.DisplayFocused) return;
            var kind = s.Definition.Controls[v.Focus].Kind;
            if (kind == ControlKind.Button) { Use(s, v, v.Focus); return; }
            if (kind == ControlKind.Joystick) { Do(s, v, v.Focus, StickAction(dir)); return; }
            Do(s, v, v.Focus, dir.x + dir.y > 0 ? ActionKind.Increment : ActionKind.Decrement);
        }

        private static void Do(MachineSession s, MachineView v, int c, ActionKind a)
        {
            int slot = s.Definition.ControlSlot(c), before = s.State[slot];
            if (s.Apply(new Move(c, a)))
            {
                v.Pulse(c);
                v.PlayControl(c, a, before, s.State[slot]);
            }
            else v.PlayBlocked(c);
        }

        private static void Undo(MachineSession s) => Sfx.Play(s.Undo() ? "undo" : "ctl_blocked");

        private static void Reset(MachineSession s)
        {
            s.Reset();
            Sfx.Play("reset");
        }

        private void SwitchPort(MachineSession s)
        {
            ActivePort = (ActivePort + 1) % s.Definition.InputPorts;
            Sfx.Play("port_switch");
        }
    }
}
