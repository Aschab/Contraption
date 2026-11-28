using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // The manual (left) and the logbook (right) on the table; one of them can be lifted into view at a time.
    // Manual: up to four examples per page (inputs → expected output), vertically centred, sealed once proven.
    // Logbook: every test as IN rows + OUT row; OUT cells that differ from the manual's answer for the same inputs are orange.
    // Keys: Q manual, E logbook (look / put down), ←→ turn the book in view (past its last page, back toward the machine, puts it down),
    // ↑↓ pick a log entry, Delete removes it, Esc puts it down. Focus moved off the machine's left/right edge lifts a book (Open).
    // Gamepad: LT manual, RT logbook, d-pad/left stick as arrows while a book is up, B puts it down, right-stick press deletes. Mouse: click a book to pick it up,
    // click its left/right page to turn, click ✕ to delete an entry, right-click to put it down.
    public sealed class BookDesk
    {
        private const float PageW = 0.38f, PageH = 0.52f;
        private const float Label = 0.08f, Col = 0.052f;

        private readonly BookView _manual, _log;
        private MachineSession _session;
        private BookView _active;
        private int _selected = -1, _drawnLogCount = -1;
        private string _drawnVerified = "";

        public bool CapturesArrows => _active != null && _active.Held;
        // -1 manual in view (left), +1 logbook in view (right), 0 none.
        public int FocusSide => _active == null || !_active.Held ? 0 : _active == _manual ? -1 : 1;
        public Bounds RestBounds { get; }

        public BookDesk(MachineKit kit, Transform parent, Camera cam, Vector3 manualPos, Vector3 logPos)
        {
            _manual = BookView.Create(kit, parent, "Book_Manual", manualPos, Quaternion.Euler(0f, 8f, 0f), cam, true);
            _log = BookView.Create(kit, parent, "Book_Log", logPos, Quaternion.Euler(0f, -8f, 0f), cam, false);
            var rest = new Bounds(manualPos, new Vector3(0.9f, 0.05f, 0.6f));
            rest.Encapsulate(new Bounds(logPos, new Vector3(0.9f, 0.05f, 0.6f)));
            RestBounds = rest;
            _manual.DrawPage = DrawManualPage;
            _log.DrawPage = DrawLogPage;
        }

        public void Bind(MachineSession session)
        {
            if (_session != null) _session.Changed -= OnChanged;
            _session = session;
            _session.Changed += OnChanged;
            _manual.PageCount = () => (_session.Definition.Manual.Count + PerPage - 1) / PerPage;
            _log.PageCount = () => Mathf.Max(1, (_session.Log.Count + PerPage - 1) / PerPage);
            _selected = -1;
            _manual.ShowSpread(0);
            _log.ShowSpread(0);
            Snapshot();
        }

        private void OnChanged()
        {
            string verified = string.Join(",", _session.Verified);
            if (_session.Log.Count != _drawnLogCount)
            {
                bool added = _session.Log.Count > _drawnLogCount;
                _selected = Mathf.Min(_selected, _session.Log.Count - 1);
                if (added) _log.ShowSpread(LastSpread());
                else _log.Redraw();
            }
            if (verified != _drawnVerified) _manual.Redraw();
            Snapshot();
        }

        private void Snapshot()
        {
            _drawnLogCount = _session.Log.Count;
            _drawnVerified = string.Join(",", _session.Verified);
        }

        private int LastSpread() => Mathf.Max(0, (_log.PageCount() - 1) / 2);

        // ---------------------------------------------------------------- input
        public void Tick(Camera cam, System.Func<Vector2, bool> overGui, bool keyboardFree)
        {
            var kb = Keyboard.current;
            if (kb != null && keyboardFree)
            {
                if (kb.qKey.wasPressedThisFrame) Toggle(_manual);
                if (kb.eKey.wasPressedThisFrame) Toggle(_log);
                if (kb.escapeKey.wasPressedThisFrame && _active != null) Toggle(_active);
                if (CapturesArrows)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame) Page(-1);
                    if (kb.rightArrowKey.wasPressedThisFrame) Page(1);
                    if (kb.upArrowKey.wasPressedThisFrame) Select(-1);
                    if (kb.downArrowKey.wasPressedThisFrame) Select(1);
                    if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) DeleteSelected();
                }
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftTrigger.wasPressedThisFrame) Toggle(_manual);
                if (pad.rightTrigger.wasPressedThisFrame) Toggle(_log);
                var stick = Flick(pad.leftStick.ReadValue());
                if (CapturesArrows)
                {
                    if (pad.buttonEast.wasPressedThisFrame) { Toggle(_active); return; }
                    if (pad.dpad.left.wasPressedThisFrame || stick.x < 0) Page(-1);
                    if (pad.dpad.right.wasPressedThisFrame || stick.x > 0) Page(1);
                    if (pad.dpad.up.wasPressedThisFrame || stick.y > 0) Select(-1);
                    if (pad.dpad.down.wasPressedThisFrame || stick.y < 0) Select(1);
                    if (pad.rightStickButton.wasPressedThisFrame) DeleteSelected();
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || !(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return;
            var pos = mouse.position.ReadValue();
            if (overGui(new Vector2(pos.x, Screen.height - pos.y))) return;
            if (!Physics.Raycast(cam.ScreenPointToRay(pos), out var hit, 100f)) return;
            var marker = hit.collider.GetComponent<LogEntryMarker>();
            if (marker != null && mouse.leftButton.wasPressedThisFrame)
            {
                _selected = marker.Index;
                DeleteSelected();
                return;
            }
            var book = hit.collider.GetComponentInParent<BookView>();
            if (book != _manual && book != _log) return;
            if (mouse.rightButton.wasPressedThisFrame) { if (book.Held) Toggle(book); }
            else if (!book.Held) Toggle(book);
            else book.Turn(book.SideOf(hit.point));
        }

        public void Open(int side)
        {
            var book = side < 0 ? _manual : _log;
            if (!book.Held) Toggle(book);
        }

        // Past the end that faces the machine (manual: last page, logbook: first page) puts the book down.
        private void Page(int dir)
        {
            bool towardMachine = _active == _manual ? dir > 0 : dir < 0;
            int next = _active.Spread + dir;
            if (towardMachine && (next < 0 || next >= _active.SpreadCount)) Toggle(_active);
            else _active.Turn(dir);
        }

        private Vector2Int _lastStick;

        private Vector2Int Flick(Vector2 stick)
        {
            var dir = stick.magnitude < 0.6f ? Vector2Int.zero
                : Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2Int((int)Mathf.Sign(stick.x), 0) : new Vector2Int(0, (int)Mathf.Sign(stick.y));
            bool fresh = dir != _lastStick;
            _lastStick = dir;
            return fresh ? dir : Vector2Int.zero;
        }

        // Only one book is up at a time.
        private void Toggle(BookView book)
        {
            bool up = !book.Held;
            _manual.SetHeld(up && book == _manual);
            _log.SetHeld(up && book == _log);
            _active = up ? book : null;
        }

        private void Select(int dir)
        {
            if (_active != _log || _session.Log.Count == 0) return;
            int first = _log.Spread * 2 * PerPage, last = Mathf.Min(_session.Log.Count, first + 2 * PerPage) - 1;
            _selected = _selected < first || _selected > last ? (dir > 0 ? first : last) : Mathf.Clamp(_selected + dir, first, last);
            _log.Redraw();
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || _selected >= _session.Log.Count) return;
            _session.DeleteLog(_selected);
        }

        // ---------------------------------------------------------------- drawing
        private static float ColX(int trait) => -PageW / 2f + 0.02f + Label + Col * (trait + 0.5f);

        private void Header(PageDrawer d, float top)
        {
            for (int t = 0; t < Liquid.PropertyCount; t++) d.Icon(new Vector2(ColX(t), top - 0.03f), 0.036f, Plates.Trait((LiquidProperty)t), PageDrawer.Ink);
            float x0 = -PageW / 2f + 0.02f + Label, x1 = x0 + Col * Liquid.PropertyCount;
            d.Line(new Vector2(-PageW / 2f + 0.02f, top - 0.06f), new Vector2(x1, top - 0.06f), 0.0025f, PageDrawer.Ink);
            for (int t = 0; t <= Liquid.PropertyCount; t++) d.Line(new Vector2(x0 + Col * t, top), new Vector2(x0 + Col * t, -PageH / 2f + 0.03f), 0.0018f, PageDrawer.Ink);
        }

        private void Row(PageDrawer d, float y, Liquid l, Liquid? expected, bool into, int poolIndex, float cell)
        {
            float lx = -PageW / 2f + 0.02f;
            if (into) d.LiquidIcon(new Vector2(lx + 0.018f, y), cell * 0.4f, l, poolIndex);
            d.FlowMark(new Vector2(lx + 0.058f, y), cell * 0.9f, into);
            for (int t = 0; t < Liquid.PropertyCount; t++)
            {
                var p = (LiquidProperty)t;
                bool wrong = expected.HasValue && expected.Value[p] != l[p];
                d.Grid(new Vector2(ColX(t), y), cell, l[p], wrong ? PageDrawer.Mismatch : PageDrawer.Fill);
            }
        }

        // Both books: four entries per page, each one row per input plus the output row. Rows shrink on
        // multi-input machines so four entries still fit.
        private const int PerPage = 4;
        private const float MaxRow = 0.038f, Gap = 0.022f;
        private const float BodyTop = PageH / 2f - 0.02f - 0.075f, BodyBottom = -PageH / 2f + 0.03f;

        private int EntryRows => _session.Definition.InputPorts + 1;
        private float RowSize => Mathf.Min(MaxRow, (BodyTop - BodyBottom - (PerPage - 1) * Gap) / (PerPage * EntryRows));
        private float EntryHeight => EntryRows * RowSize;

        // Draws one entry down from its top edge; returns the output row's y.
        private float Entry(PageDrawer d, float top, Liquid[] inputs, int[] pool, Liquid output, Liquid? expected)
        {
            float row = RowSize, cell = row * 0.74f, y = top - row / 2f;
            for (int p = 0; p < inputs.Length; p++)
            {
                Row(d, y, inputs[p], null, true, pool[p], cell);
                y -= row;
            }
            Row(d, y, output, expected, false, -1, cell);
            float bx = -PageW / 2f + 0.06f;
            d.Line(new Vector2(bx, top - 0.006f), new Vector2(bx, top - EntryHeight + 0.006f), 0.002f, PageDrawer.Ink);
            return y;
        }

        private static void Separator(PageDrawer d, float y)
        {
            d.Line(new Vector2(-PageW / 2f + 0.02f, y + 0.002f), new Vector2(PageW / 2f - 0.02f, y + 0.002f), 0.0015f, PageDrawer.Ink);
            d.Line(new Vector2(-PageW / 2f + 0.02f, y - 0.002f), new Vector2(PageW / 2f - 0.02f, y - 0.002f), 0.0015f, PageDrawer.Ink);
        }

        private void DrawManualPage(PageDrawer d, int page)
        {
            var def = _session.Definition;
            int first = page * PerPage;
            if (first >= def.Manual.Count) return;
            int count = Mathf.Min(PerPage, def.Manual.Count - first);
            Header(d, PageH / 2f - 0.02f);
            float h = EntryHeight;
            float y = (BodyTop + BodyBottom) / 2f + (count * h + (count - 1) * Gap) / 2f;   // recipes centred vertically
            for (int k = 0; k < count; k++)
            {
                int e = first + k;
                var entry = def.Manual[e];
                var inputs = entry.Inputs.ToArray();
                float outY = Entry(d, y, inputs, System.Array.ConvertAll(inputs, l => def.InputPool.IndexOf(l)), entry.Output, null);
                if (_session.Verified[e])
                {
                    var seal = new Vector2(PageW / 2f - 0.016f, outY);
                    d.Disc(seal, 0.013f, PageDrawer.Seal, 3);
                    d.Symbol(seal, 0.012f, SealMark, new Color(0.35f, 0.04f, 0.03f), 0.0025f);
                }
                y -= h;
                if (k < count - 1) Separator(d, y - Gap / 2f);
                y -= Gap;
            }
        }

        private static readonly int SealMark = Glyphs.MakeRunes(0x5EA1UL, 1, 4, 5)[0];

        // Tests fill the page from the top, oldest first.
        private void DrawLogPage(PageDrawer d, int page)
        {
            var def = _session.Definition;
            Header(d, PageH / 2f - 0.02f);
            float y = BodyTop, h = EntryHeight;
            for (int k = 0; k < PerPage; k++)
            {
                int index = page * PerPage + k;
                if (index >= _session.Log.Count) break;
                var rec = _session.Log[index];
                if (index == _selected)
                    d.Rect(new Vector2(0f, y - h / 2f), new Vector2(PageW - 0.02f, h + Gap * 0.6f), d.Paper * 0.86f, 1);
                int manual = _session.ManualEntryFor(rec);
                Liquid? expected = manual >= 0 ? def.Manual[manual].Output : null;
                Entry(d, y, System.Array.ConvertAll(rec.Inputs, i => def.InputPool[i]), rec.Inputs, rec.Output, expected);
                DeleteMark(d, new Vector2(PageW / 2f - 0.012f, y - 0.012f), index);
                y -= h;
                if (k < PerPage - 1 && index + 1 < _session.Log.Count) Separator(d, y - Gap / 2f);
                y -= Gap;
            }
        }

        private static void DeleteMark(PageDrawer d, Vector2 c, int index)
        {
            const float s = 0.008f;
            d.Line(c + new Vector2(-s, -s), c + new Vector2(s, s), 0.0025f, PageDrawer.Ink);
            d.Line(c + new Vector2(-s, s), c + new Vector2(s, -s), 0.0025f, PageDrawer.Ink);
            var hit = d.Rect(c, new Vector2(s * 3f, s * 3f), d.Paper, 0);
            hit.GetComponent<Renderer>().enabled = false;
            var box = hit.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 20f);
            hit.AddComponent<LogEntryMarker>().Index = index;
        }
    }
}
