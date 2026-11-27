using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Contraption.Game
{
    // Fill light and post-processing (bloom for glows, warm grade, vignette, background blur), built at runtime.
    public static class Stage
    {
        private static DepthOfField _dof;

        // Background blur once the camera sits at the table: sharp up to the machine, blurred beyond.
        public static void SetFocus(float distance, float amount)
        {
            if (_dof == null) return;
            _dof.active = amount > 0.01f;
            _dof.gaussianStart.Override(distance + 0.6f);
            _dof.gaussianEnd.Override(distance + 0.6f + Mathf.Lerp(30f, 4f, amount));
            _dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, amount));
        }

        public static void Build(Camera cam)
        {
            var root = new GameObject("Stage").transform;
            // The sun comes in steeply (through the open roof) so the workshop walls don't shade the table.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.transform.rotation = Quaternion.Euler(62f, -30f, 0f);
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
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.highQualitySampling.Override(true);
            _dof.active = false;
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
    }
}
