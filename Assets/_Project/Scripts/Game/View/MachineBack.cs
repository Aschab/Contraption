using System.Collections;
using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // The machine's rear (seen when it is turned around): every display's back in a frame, a socket on each one that
    // hosts pipeline steps, and cables from the start badge (empty ring, by the inputs) through the steps in order
    // to the end badge (full disc, by the output). Hints choose which cables and labels exist.
    // Each test sends one glowing bead through the chain; hidden links take the same time but show nothing.
    public sealed class MachineBack : MonoBehaviour
    {
        private const float CableWidth = 0.06f, LinkTime = 0.4f, Flash = 0.3f;
        private Vector3[][] _links;
        private Vector3 _start, _end;
        private Transform _bead;
        private AudioSource _hum;
        private Coroutine _pulse;

        // Rear node of a display: centre and size in machine space (z is ignored; the rear face is at `back`).
        public struct Node
        {
            public Vector2 Center, Size;
        }

        public static MachineBack Create(MachineKit kit, Look look, Transform machine, MachineDefinition def, MachineHints hints,
            Node[] displays, string[] frames, List<Vector2> freeCells, Vector2 start, Vector2 end, float back)
        {
            var v = new GameObject("Back").AddComponent<MachineBack>();
            v.transform.SetParent(machine, false);
            var root = v.transform;
            var facing = Quaternion.Euler(0f, 180f, 0f);
            int steps = def.Pipeline.Count;

            // Which node hosts each step: displays first, then one junction box per Exchange step.
            var hostOf = new int[steps];
            var nodes = new List<Node>(displays);
            var hosted = new List<List<int>>();
            foreach (var _ in displays) hosted.Add(new List<int>());
            int free = 0;
            for (int s = 0; s < steps; s++)
            {
                int home = MachineHints.Home(def, s);
                if (home < 0)
                {
                    var c = free < freeCells.Count ? freeCells[free++] : new Vector2(0f, displays.Length > 0 ? displays[0].Center.y : 0.5f);
                    home = nodes.Count;
                    nodes.Add(new Node { Center = c, Size = new Vector2(0.6f, 0.6f) });
                    hosted.Add(new List<int>());
                }
                hostOf[s] = home;
                hosted[home].Add(s);
            }

            var sockets = new Vector3[nodes.Count];
            for (int n = 0; n < nodes.Count; n++)
            {
                var node = nodes[n];
                bool display = n < displays.Length;
                var c = new Vector3(node.Center.x, node.Center.y, back);
                if (display && kit.Has(frames[n])) kit.Spawn(frames[n], root, c, look).transform.localRotation = facing;
                else if (!display) Art.Prim(PrimitiveType.Cube, root, c + new Vector3(0f, 0f, 0.06f), new Vector3(node.Size.x, node.Size.y, 0.12f), look.Trim);
                if (hosted[n].Count == 0) continue;

                float top = node.Center.y + node.Size.y / 2f;
                sockets[n] = new Vector3(c.x, display ? top - 0.22f : top - 0.15f, back + (display ? 0.08f : 0.16f));
                Socket(root, sockets[n], look);

                var rows = new List<List<Plates.Mark>>();
                foreach (int s in hosted[n])
                {
                    var marks = Plates.Sentence(def, s, hints.Label(s), display ? n : -1);
                    if (marks.Count > 0) rows.Add(marks);
                }
                if (rows.Count > 0)
                    Plates.Build(root, new Vector3(c.x, sockets[n].y - 0.36f, sockets[n].z), rows, node.Size.x - 0.12f, look, true, 0.2f);
            }

            var startPos = new Vector3(start.x, start.y, back + 0.06f);
            var endPos = new Vector3(end.x, end.y, back + 0.06f);
            Badge(root, startPos, look, false);
            Badge(root, endPos, look, true);

            var chain = new List<Vector3> { startPos };
            for (int s = 0; s < steps; s++) chain.Add(sockets[hostOf[s]]);
            chain.Add(endPos);
            v._start = startPos + new Vector3(0f, 0f, 0.08f);
            v._end = endPos + new Vector3(0f, 0f, 0.08f);
            v._links = new Vector3[chain.Count - 1][];
            for (int i = 0; i + 1 < chain.Count; i++)
                if (hints.Link(i)) v._links[i] = Cable(root, chain[i], chain[i + 1], look);
            var bead = Art.Prim(PrimitiveType.Sphere, root, startPos, Vector3.one * CableWidth * 1.7f, look.Glow);
            Art.Paint(bead.GetComponent<Renderer>(), look.Glow, look.Glow * 4f);
            bead.SetActive(false);
            v._bead = bead.transform;
            v._hum = Sfx.Loop("cable_travel", bead.transform, Vector3.zero, 0.5f);
            return v;
        }

        private static void Socket(Transform root, Vector3 p, Look look)
        {
            Cyl(root, p, 0.11f, 0.08f, look.Metal);
            Cyl(root, p + new Vector3(0f, 0f, 0.03f), 0.075f, 0.04f, look.Dark);
        }

        // Start: an empty glowing ring. End: a full glowing disc.
        private static void Badge(Transform root, Vector3 p, Look look, bool full)
        {
            Cyl(root, p, 0.2f, 0.06f, look.Metal);
            var glow = Cyl(root, p + new Vector3(0f, 0f, 0.02f), 0.14f, 0.05f, look.Glow).GetComponent<Renderer>();
            Art.Paint(glow, look.Glow, look.Glow * 2f);
            if (!full) Cyl(root, p + new Vector3(0f, 0f, 0.035f), 0.09f, 0.05f, look.Dark);
        }

        private static GameObject Cyl(Transform root, Vector3 p, float radius, float depth, Color color)
        {
            var go = Art.Prim(PrimitiveType.Cylinder, root, p, new Vector3(radius * 2f, depth / 2f, radius * 2f), color);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        // A hanging cable bulging away from the machine; a loop when both ends share a socket.
        private static Vector3[] Cable(Transform root, Vector3 a, Vector3 b, Look look)
        {
            Vector3 p1, p2;
            var outward = new Vector3(0f, 0f, 0.18f);
            if ((a - b).sqrMagnitude < 0.0001f)
            {
                p1 = a + new Vector3(-0.45f, 0.5f, 0f) + outward;
                p2 = a + new Vector3(0.45f, 0.5f, 0f) + outward;
            }
            else
            {
                float sag = 0.15f + 0.12f * Vector3.Distance(a, b);
                p1 = Vector3.Lerp(a, b, 1f / 3f) + Vector3.down * sag + outward;
                p2 = Vector3.Lerp(a, b, 2f / 3f) + Vector3.down * sag + outward;
            }
            const int n = 16;
            var path = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                path[i] = u * u * u * a + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b;
            }
            var rubber = Color.Lerp(look.Dark, look.Accent, 0.25f);
            for (int i = 0; i < n; i++)
            {
                var d = path[i + 1] - path[i];
                var seg = Art.Prim(PrimitiveType.Cylinder, root, (path[i] + path[i + 1]) / 2f, new Vector3(CableWidth, d.magnitude / 2f + CableWidth * 0.3f, CableWidth), rubber);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            }
            foreach (var end in new[] { a, b })
                Cyl(root, end + new Vector3(0f, 0f, 0.05f), 0.055f, 0.08f, look.Metal);
            return path;
        }

        // One pass of the bead from start to end, called when a test runs.
        public void Pulse()
        {
            if (_pulse != null) StopCoroutine(_pulse);
            _bead.localScale = Vector3.one * CableWidth * 1.7f;
            _pulse = StartCoroutine(Run());
        }

        // The light swells on the start badge (the inputs), travels the cables, and swells again on the end badge.
        private IEnumerator Run()
        {
            Sfx.Play("cable_start", _bead.parent.TransformPoint(_start));
            yield return Swell(_start);
            foreach (var path in _links)
            {
                _bead.gameObject.SetActive(path != null);
                if (_hum != null) { if (path != null) _hum.Play(); else _hum.Stop(); }
                for (float t = 0f; t < LinkTime; t += Time.deltaTime)
                {
                    if (path != null)
                    {
                        float f = Mathf.SmoothStep(0f, 1f, t / LinkTime) * (path.Length - 1);
                        int k = Mathf.Min((int)f, path.Length - 2);
                        _bead.localPosition = Vector3.Lerp(path[k], path[k + 1], f - k);
                    }
                    yield return null;
                }
                if (path != null) Sfx.Play("cable_node", _bead.position, 0.7f);
            }
            if (_hum != null) _hum.Stop();
            Sfx.Play("cable_end", _bead.parent.TransformPoint(_end));
            yield return Swell(_end);
            _bead.gameObject.SetActive(false);
            _pulse = null;
        }

        private IEnumerator Swell(Vector3 at)
        {
            _bead.gameObject.SetActive(true);
            _bead.localPosition = at;
            var size = _bead.localScale;
            for (float t = 0f; t < Flash; t += Time.deltaTime)
            {
                _bead.localScale = size * (1f + 1.5f * Mathf.Sin(t / Flash * Mathf.PI));
                yield return null;
            }
            _bead.localScale = size;
        }
    }
}
