using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // Pause menu with settings (IMGUI placeholder until the diegetic UI). Esc / Start opens and closes it; the game pauses.
    // Keys: ↑↓ choose, ←→ change, Enter/Space activate. Gamepad: d-pad/left stick, A activate, B/Start close. Mouse works too.
    public sealed class PauseMenu
    {
        private enum Item { Resume, Master, Effects, Ambience, Music, Magnify, Quick, AnimateTests, Quit }
        private static readonly Item[] Items = (Item[])System.Enum.GetValues(typeof(Item));
        private const float Step = 0.1f;

        private int _focus;
        private Vector2Int _lastStick;

        public bool Open { get; private set; }

        public void Toggle()
        {
            Open = !Open;
            _focus = 0;
            Time.timeScale = Open ? 0f : 1f;
            Sfx.Play(Open ? "book_lift" : "book_down");
        }

        public void Tick()
        {
            int move = 0, change = 0;
            bool activate = false, close = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move = -1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move = 1;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) change = -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) change = 1;
                activate |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                close |= kb.escapeKey.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.leftStick.ReadValue();
                var dir = s.magnitude < 0.6f ? Vector2Int.zero
                    : Mathf.Abs(s.x) > Mathf.Abs(s.y) ? new Vector2Int((int)Mathf.Sign(s.x), 0) : new Vector2Int(0, (int)Mathf.Sign(s.y));
                if (dir != _lastStick) { move = move != 0 ? move : -dir.y; change = change != 0 ? change : dir.x; }
                _lastStick = dir;
                if (pad.dpad.up.wasPressedThisFrame) move = -1;
                if (pad.dpad.down.wasPressedThisFrame) move = 1;
                if (pad.dpad.left.wasPressedThisFrame) change = -1;
                if (pad.dpad.right.wasPressedThisFrame) change = 1;
                activate |= pad.buttonSouth.wasPressedThisFrame;
                close |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }

            if (close) { Toggle(); return; }
            if (move != 0)
            {
                _focus = (_focus + move + Items.Length) % Items.Length;
                Sfx.Play("focus_move");
            }
            if (change != 0) Change(Items[_focus], change);
            if (activate) Activate(Items[_focus]);
        }

        private void Change(Item item, int dir)
        {
            switch (item)
            {
                case Item.Master: SoundSettings.Master += dir * Step; break;
                case Item.Effects: SoundSettings.Effects += dir * Step; Sfx.Play("ctl_dial_click"); break;
                case Item.Ambience: SoundSettings.Ambience += dir * Step; break;
                case Item.Music: SoundSettings.Music += dir * Step; break;
                case Item.Magnify:
                case Item.Quick:
                case Item.AnimateTests: Activate(item); break;
            }
        }

        private void Activate(Item item)
        {
            switch (item)
            {
                case Item.Resume: Toggle(); break;
                case Item.Magnify: Accessibility.MagnifyFocus = !Accessibility.MagnifyFocus; Sfx.Play("ctl_lever_up"); break;
                case Item.Quick: Accessibility.Quick = !Accessibility.Quick; Sfx.Play("ctl_lever_up"); break;
                case Item.AnimateTests: MachineView.AnimateTests = !MachineView.AnimateTests; Sfx.Play("ctl_lever_up"); break;
                case Item.Quit:
                    Time.timeScale = 1f;
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        // Call from OnGUI (GUI.matrix already scaled); w/h in GUI units.
        public void Draw(float w, float h)
        {
            const float pw = 420f, row = 34f;
            float ph = Items.Length * row + 70f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var area = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(area, "");
            GUI.Label(new Rect(area.x, area.y + 10f, pw, 30f), "<b><size=20>PAUSED</size></b>", new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            for (int i = 0; i < Items.Length; i++)
            {
                var r = new Rect(area.x + 20f, area.y + 50f + i * row, pw - 40f, row - 4f);
                if (i == _focus)
                {
                    GUI.color = new Color(0.25f, 1f, 0.85f, 0.35f);
                    GUI.DrawTexture(r, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                DrawItem(Items[i], r, i);
            }
        }

        private void DrawItem(Item item, Rect r, int index)
        {
            var label = new Rect(r.x + 8f, r.y + 4f, 150f, r.height - 8f);
            var control = new Rect(r.x + 160f, r.y + 8f, r.width - 220f, r.height - 12f);
            var value = new Rect(r.xMax - 52f, r.y + 4f, 48f, r.height - 8f);
            switch (item)
            {
                case Item.Resume:
                    if (GUI.Button(r, "Resume")) { _focus = index; Toggle(); }
                    break;
                case Item.Quit:
                    if (GUI.Button(r, "Quit")) Activate(item);
                    break;
                case Item.Magnify:
                    Toggle(r, "Magnify the selected part (accessibility)", Accessibility.MagnifyFocus, () => Activate(item));
                    break;
                case Item.Quick:
                    Toggle(r, "Quick displays (all at once, no sounds)", Accessibility.Quick, () => Activate(item));
                    break;
                case Item.AnimateTests:
                    Toggle(r, "Animate tests", MachineView.AnimateTests, () => Activate(item));
                    break;
                default:
                    GUI.Label(label, item switch { Item.Master => "Master volume", Item.Effects => "Effects", Item.Ambience => "Ambience", _ => "Music" });
                    float v = Volume(item);
                    float nv = GUI.HorizontalSlider(control, v, 0f, 1f);
                    if (!Mathf.Approximately(nv, v)) { _focus = index; SetVolume(item, nv); }
                    GUI.Label(value, Mathf.RoundToInt(v * 100f) + "%");
                    break;
            }
        }

        private static void Toggle(Rect r, string text, bool on, System.Action flip)
        {
            if (GUI.Toggle(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 8f), on, " " + text) != on) flip();
        }

        private static float Volume(Item item) => item switch
        {
            Item.Master => SoundSettings.Master,
            Item.Effects => SoundSettings.Effects,
            Item.Ambience => SoundSettings.Ambience,
            _ => SoundSettings.Music,
        };

        private static void SetVolume(Item item, float v)
        {
            switch (item)
            {
                case Item.Master: SoundSettings.Master = v; break;
                case Item.Effects: SoundSettings.Effects = v; break;
                case Item.Ambience: SoundSettings.Ambience = v; break;
                default: SoundSettings.Music = v; break;
            }
        }
    }
}
