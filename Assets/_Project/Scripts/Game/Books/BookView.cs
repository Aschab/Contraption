using System;
using UnityEngine;

namespace Contraption.Game
{
    // An open book on the table. It can be lifted in front of the camera (left or right half of the view)
    // and its pages turned. Content is drawn by the owner through PageDrawers, two pages per spread.
    public sealed class BookView : MonoBehaviour
    {
        private const float HoldSpeed = 5f, TurnTime = 0.35f;

        private Camera _cam;
        private bool _leftSide;
        private Vector3 _restPos;
        private Quaternion _restRot;
        private float _hold, _holdTarget, _turn = 1f;
        private int _turnDir;
        private Transform _sheet;

        public PageDrawer Left { get; private set; }
        public PageDrawer Right { get; private set; }
        public int Spread { get; private set; }
        public bool Held => _holdTarget > 0.5f;
        public Func<int> PageCount = () => 0;
        // (drawer, page index); page index may be past the last page (draw nothing or a blank page).
        public Action<PageDrawer, int> DrawPage = (_, _) => { };

        public static BookView Create(MachineKit kit, Transform parent, string model, Vector3 pos, Quaternion rot, Camera cam, bool leftSide)
        {
            var go = new GameObject(model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var v = go.AddComponent<BookView>();
            v._cam = cam;
            v._leftSide = leftSide;
            v._restPos = pos;
            v._restRot = rot;
            var look = kit.DefaultLook();
            var body = kit.Spawn(model, go.transform, Vector3.zero, look).transform;
            v.Left = new PageDrawer(Content(go.transform, MachineKit.Find(body, "PageL")), look.Panel);
            v.Right = new PageDrawer(Content(go.transform, MachineKit.Find(body, "PageR")), look.Panel);
            v._sheet = kit.Spawn("Book_Sheet", go.transform, new Vector3(0f, 0.045f, 0f), look).transform;
            v._sheet.gameObject.SetActive(false);
            var box = go.AddComponent<BoxCollider>();
            // Below the page surface so the delete marks on the pages are hit first.
            box.center = new Vector3(0f, 0.02f, 0f);
            box.size = new Vector3(0.86f, 0.04f, 0.56f);
            return v;
        }

        // Page space: x right, y toward the page top, z into the page.
        private static Transform Content(Transform book, Transform anchor)
        {
            var t = new GameObject("Content").transform;
            t.SetParent(book, false);
            t.localPosition = book.InverseTransformPoint(anchor.position);
            t.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            return t;
        }

        public int SpreadCount => Mathf.Max(1, (PageCount() + 1) / 2);

        public void Redraw()
        {
            Spread = Mathf.Clamp(Spread, 0, SpreadCount - 1);
            DrawSide(Left, Spread * 2);
            DrawSide(Right, Spread * 2 + 1);
        }

        private void DrawSide(PageDrawer d, int page)
        {
            d.Clear();
            DrawPage(d, page);
        }

        public void ShowSpread(int spread)
        {
            Spread = Mathf.Clamp(spread, 0, SpreadCount - 1);
            Redraw();
        }

        public bool Turn(int dir)
        {
            int next = Spread + dir;
            if (next < 0 || next >= SpreadCount || _turn < 1f) return false;
            Spread = next;
            _turnDir = dir;
            _turn = 0f;
            // The page under the turning sheet changes now, the other side when the sheet lands.
            DrawSide(dir > 0 ? Right : Left, dir > 0 ? Spread * 2 + 1 : Spread * 2);
            _sheet.gameObject.SetActive(true);
            return true;
        }

        public void SetHeld(bool held) => _holdTarget = held ? 1f : 0f;
        public void Toggle() => SetHeld(!Held);

        // Which half of the open book a world point is on (for clicks): -1 left page, +1 right page.
        public int SideOf(Vector3 worldPoint) => transform.InverseTransformPoint(worldPoint).x < 0f ? -1 : 1;

        private void LateUpdate()
        {
            _hold = Mathf.MoveTowards(_hold, _holdTarget, HoldSpeed * Time.deltaTime);
            float k = Mathf.SmoothStep(0f, 1f, _hold);
            HeldPose(out var hp, out var hr);
            transform.SetPositionAndRotation(Vector3.Lerp(_restPos, hp, k), Quaternion.Slerp(_restRot, hr, k));

            if (_turn < 1f)
            {
                _turn = Mathf.Min(1f, _turn + Time.deltaTime / TurnTime);
                float a = Mathf.SmoothStep(0f, 180f, _turn);
                _sheet.localRotation = Quaternion.AngleAxis(_turnDir > 0 ? a : 180f - a, Vector3.forward);
                if (_turn >= 1f)
                {
                    _sheet.gameObject.SetActive(false);
                    DrawSide(_turnDir > 0 ? Left : Right, _turnDir > 0 ? Spread * 2 : Spread * 2 + 1);
                }
            }
        }

        // In front of the camera, filling about 42% of the view width on its side (the machine stays visible), pages facing the viewer.
        private void HeldPose(out Vector3 pos, out Quaternion rot)
        {
            var c = _cam.transform;
            float tan = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = 0.86f / (0.42f * 2f * tan * _cam.aspect);
            float viewWidth = 2f * d * tan * _cam.aspect;
            pos = c.position + c.forward * d + c.right * (_leftSide ? -0.27f : 0.27f) * viewWidth - c.up * 0.04f * viewWidth;
            rot = Quaternion.LookRotation(c.up, -c.forward) * Quaternion.Euler(-8f, 0f, _leftSide ? -4f : 4f);
        }
    }
}
