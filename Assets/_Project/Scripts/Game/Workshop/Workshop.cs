using UnityEngine;

namespace Contraption.Game
{
    // The workshop room and the work table. The machine stands on the table at MachineScale (kit pieces are
    // modelled at 1.1 m per cell; the table is real-world size), the manual lies front-left, the logbook front-right.
    public sealed class Workshop
    {
        public const float MachineScale = 0.3f;
        public const float TableTop = 1.0f;

        public Transform Root { get; }
        public Vector3 MachineAnchor => new(0f, TableTop, 0.35f);
        public Vector3 ManualRest => new(-1.3f, TableTop, -0.72f);
        public Vector3 LogRest => new(1.3f, TableTop, -0.72f);
        public Vector3 RackOrigin => new(-2.05f, TableTop, 0.3f);
        // Table top corners and the rack/book tops: everything that must stay sharp.
        public static readonly Vector3[] SharpPoints =
        {
            new(-2.55f, TableTop, -1.25f), new(2.55f, TableTop, -1.25f), new(-2.55f, TableTop + 0.4f, 1.25f), new(2.55f, TableTop + 0.4f, 1.25f),
        };

        // Intro flight: (camera position, look-at) keys through the room, ending where the table framing takes over.
        public static readonly (Vector3 pos, Vector3 look)[] IntroPath =
        {
            (new Vector3(-11f, 3.4f, -8f), new Vector3(-2f, 3f, 9f)),
            (new Vector3(-7f, 2.8f, 2f), new Vector3(6f, 2.6f, 9f)),
            (new Vector3(1f, 2.6f, 5f), new Vector3(13f, 2.4f, 2f)),
            (new Vector3(6f, 2.6f, -3f), new Vector3(0f, 1.2f, 0f)),
        };

        public Workshop(MachineKit kit)
        {
            Root = new GameObject("Workshop").transform;
            var look = kit.DefaultLook();
            var room = kit.Spawn("Workshop_Room", Root, Vector3.zero, look);
            kit.Spawn("Table", Root, Vector3.zero, look);
            // Textured Tripo props keep their own materials; matte them so they sit with the painted kit.
            foreach (var r in kit.Spawn("Workshop_Props", Root, Vector3.zero, look).GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)
                {
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_Smoothness", 0.15f);
                }
            foreach (var t in room.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("Lamp")) continue;
                var light = new GameObject("Lamp light").AddComponent<Light>();
                light.transform.SetParent(t, false);
                light.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.range = 9f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
            }
        }
    }
}
