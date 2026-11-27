using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contraption.Game
{
    // The manual (left) and the logbook (right) on the table.
    // Manual: one example per page (inputs → expected output), sealed once proven.
    // Logbook: every test as IN rows + OUT row; OUT cells that differ from the manual's answer for the same inputs are orange.
    // Keys: Q manual, E logbook (look / put down), ←→ turn the last opened book, ↑↓ pick a log entry, Delete removes it, Esc puts both down.
    // Gamepad: LT manual, RT logbook, d-pad as arrows while a book is up, right-stick press deletes. Mouse: click a book to pick it up,
    // click its left/right page to turn, click ✕ to delete an entry, right-click to put it down.
    public sealed class BookDesk
    {
        private const float PageW = 0.38f, PageH = 0.52f;
        private const float Label = 0.08f, Col = 0.052f, RowH = 0.05f, Cell = 0.034f;

        private readonly BookView _manual, _log;
        private MachineSession _session;
        private BookView _active;
        private int _selected = -1, _drawnLogCount = -1;
        private string _drawnVerified = "";

        public bool CapturesArrows => _active != null && _active.Held;

        public BookDesk(MachineKit kit, Transform parent, Camera cam, Vector3 manualPos, Vector3 logPos)
        {
            _manual = BookView.Create(kit, parent, "Book_Manual", manualPos, Quaternion.Euler(0f, 8f, 0f), cam, true);
            _log = BookView.Create(kit, parent, "Book_Log", logPos, Quaternion.Euler(0f, -8f, 0f), cam, false);
            _manual.DrawPage = DrawManualPage;
            _log.DrawPage = DrawLogPage;
        }

        public void Bind(MachineSession session)
        {
            if (_session != null) _session.Changed -= OnChanged;
            _session = session;
            _session.Changed += OnChanged;
            _manual.PageCount = () => _session.Definition.Manual.Count;
            _log.PageCount = () => Mathf.Max(1, (_session.Log.Count + PerPage() - 1) / PerPage());
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

        // Entries are (inputs + 1) rows plus a separator row; rows start 0.1 below the page top and stop 0.03 above the bottom.
        private int PerPage() => Mathf.Max(1, Mathf.FloorToInt((PageH - 0.02f - 0.1f + RowH / 2f - 0.03f) / ((_session.Definition.InputPorts + 2) * RowH)));
        private int LastSpread() => Mathf.Max(0, (_log.PageCount() - 1) / 2);

        // ---------------------------------------------------------------- input
        public void Tick(Camera cam, System.Func<Vector2, bool> overGui, bool keyboardFree)
        {
            var kb = Keyboard.current;
            if (kb != null && keyboardFree)
            {
                if (kb.qKey.wasPressedThisFrame) Toggle(_manual);
                if (kb.eKey.wasPressedThisFrame) Toggle(_log);
                if (kb.escapeKey.wasPressedThisFrame) { _manual.SetHeld(false); _log.SetHeld(false); _active = null; }
                if (CapturesArrows)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame) _active.Turn(-1);
                    if (kb.rightArrowKey.wasPressedThisFrame) _active.Turn(1);
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
                if (CapturesArrows)
                {
                    if (pad.dpad.left.wasPressedThisFrame) _active.Turn(-1);
                    if (pad.dpad.right.wasPressedThisFrame) _active.Turn(1);
                    if (pad.dpad.up.wasPressedThisFrame) Select(-1);
                    if (pad.dpad.down.wasPressedThisFrame) Select(1);
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
            if (mouse.rightButton.wasPressedThisFrame) { book.SetHeld(false); if (_active == book) _active = null; }
            else if (!book.Held) Toggle(book);
            else { _active = book; book.Turn(book.SideOf(hit.point)); }
        }

        private void Toggle(BookView book)
        {
            book.Toggle();
            _active = book.Held ? book : _active == book ? (_manual.Held ? _manual : _log.Held ? _log : null) : _active;
        }

        private void Select(int dir)
        {
            if (_active != _log || _session.Log.Count == 0) return;
            int per = PerPage();
            int first = _log.Spread * 2 * per, last = Mathf.Min(_session.Log.Count, first + 2 * per) - 1;
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
            for (int t = 0; t < Liquid.PropertyCount; t++) d.Symbol(new Vector2(ColX(t), top - 0.03f), 0.026f, PageDrawer.TraitSymbols[t], PageDrawer.Ink);
            float x0 = -PageW / 2f + 0.02f + Label, x1 = x0 + Col * Liquid.PropertyCount;
            d.Line(new Vector2(-PageW / 2f + 0.02f, top - 0.06f), new Vector2(x1, top - 0.06f), 0.0025f, PageDrawer.Ink);
            for (int t = 0; t <= Liquid.PropertyCount; t++) d.Line(new Vector2(x0 + Col * t, top), new Vector2(x0 + Col * t, -PageH / 2f + 0.03f), 0.0018f, PageDrawer.Ink);
        }

        private void Row(PageDrawer d, float y, Liquid l, Liquid? expected, bool into, int poolIndex)
        {
            float lx = -PageW / 2f + 0.02f;
            if (into) d.LiquidIcon(new Vector2(lx + 0.018f, y), 0.012f, l, poolIndex);
            d.FlowMark(new Vector2(lx + 0.058f, y), 0.03f, into);
            for (int t = 0; t < Liquid.PropertyCount; t++)
            {
                var p = (LiquidProperty)t;
                bool wrong = expected.HasValue && expected.Value[p] != l[p];
                d.Grid(new Vector2(ColX(t), y), Cell, l[p], wrong ? PageDrawer.Mismatch : PageDrawer.Fill);
            }
        }

        private void DrawManualPage(PageDrawer d, int page)
        {
            var def = _session.Definition;
            if (page >= def.Manual.Count) return;
            var entry = def.Manual[page];
            float top = PageH / 2f - 0.02f;
            Header(d, top);
            float y = top - 0.1f;
            foreach (var l in entry.Inputs)
            {
                Row(d, y, l, null, true, def.InputPool.IndexOf(l));
                y -= RowH;
            }
            d.Arrow(new Vector2(0.05f, y + 0.01f), new Vector2(0.05f, y - 0.05f), 0.004f);
            y -= 0.08f;
            Row(d, y, entry.Output, null, false, -1);
            for (int i = 0; i <= page; i++) d.Rect(new Vector2((i - page / 2f) * 0.014f, -PageH / 2f + 0.04f), new Vector2(0.007f, 0.007f), PageDrawer.Ink);
            if (_session.Verified[page])
            {
                var seal = new Vector2(PageW / 2f - 0.07f, -PageH / 2f + 0.09f);
                d.Disc(seal, 0.035f, PageDrawer.Seal, 3);
                d.Symbol(seal, 0.03f, Glyphs.MakeRunes(0x5EA1UL, 1, 4, 5)[0], new Color(0.35f, 0.04f, 0.03f), 0.005f);
            }
        }

        private void DrawLogPage(PageDrawer d, int page)
        {
            var def = _session.Definition;
            float top = PageH / 2f - 0.02f;
            Header(d, top);
            int per = PerPage(), rows = def.InputPorts + 1;
            float y = top - 0.1f;
            for (int k = 0; k < per; k++)
            {
                int index = page * per + k;
                if (index >= _session.Log.Count) break;
                var rec = _session.Log[index];
                float entryTop = y + RowH / 2f, entryH = rows * RowH;
                if (index == _selected)
                    d.Rect(new Vector2(0f, entryTop - entryH / 2f), new Vector2(PageW - 0.02f, entryH), d.Paper * 0.86f, 1);
                int manual = _session.ManualEntryFor(rec);
                Liquid? expected = manual >= 0 ? def.Manual[manual].Output : null;
                for (int p = 0; p < rec.Inputs.Length; p++)
                {
                    Row(d, y, def.InputPool[rec.Inputs[p]], null, true, rec.Inputs[p]);
                    y -= RowH;
                }
                Row(d, y, rec.Output, expected, false, -1);
                float bx = -PageW / 2f + 0.02f + 0.04f;
                d.Line(new Vector2(bx, entryTop - 0.008f), new Vector2(bx, entryTop - entryH + 0.008f), 0.002f, PageDrawer.Ink);
                y -= RowH * 0.5f;
                d.Line(new Vector2(-PageW / 2f + 0.02f, y + 0.006f), new Vector2(PageW / 2f - 0.02f, y + 0.006f), 0.0018f, PageDrawer.Ink);
                d.Line(new Vector2(-PageW / 2f + 0.02f, y + 0.002f), new Vector2(PageW / 2f - 0.02f, y + 0.002f), 0.0018f, PageDrawer.Ink);
                DeleteMark(d, new Vector2(PageW / 2f - 0.012f, entryTop - 0.012f), index);
                y -= RowH * 0.5f;
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
