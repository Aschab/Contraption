using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Contraption.Game
{
    // Workshop backdrop, lighting and post-processing (bloom for glows, warm grade, vignette), built at runtime.
    public static class Stage
    {
        public static void Build(Camera cam)
        {
            var root = new GameObject("Stage").transform;
            Art.Paint(Art.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.05f, 4f), new Vector3(60f, 0.1f, 16f), new Color(0.36f, 0.27f, 0.22f)).GetComponent<Renderer>(),
                new Color(0.36f, 0.27f, 0.22f), Color.black, 0.15f);
            Art.Paint(Art.Prim(PrimitiveType.Cube, root, new Vector3(0f, 6f, 9f), new Vector3(60f, 14f, 0.2f), new Color(0.22f, 0.15f, 0.12f)).GetComponent<Renderer>(),
                new Color(0.22f, 0.15f, 0.12f), Color.black, 0.1f);
            foreach (float x in new[] { -9f, 9f })
            {
                var window = Art.Prim(PrimitiveType.Cube, root, new Vector3(x, 5f, 8.85f), new Vector3(2.2f, 3.6f, 0.05f), Color.black);
                Art.Paint(window.GetComponent<Renderer>(), new Color(1f, 0.7f, 0.35f), new Color(1f, 0.55f, 0.2f) * 2.2f);
            }

            var fill = new GameObject("Fill light").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.65f, 0.85f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);

            var volume = new GameObject("Post").AddComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.15f);
            grade.contrast.Override(12f);
            grade.saturation.Override(10f);
            grade.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
    }
}
