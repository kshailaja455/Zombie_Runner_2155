using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieRunner
{
    /// <summary>
    /// Applies a theme's sky, sun, ambient light, fog and URP post-processing volume.
    /// The same function is used by the levels, the menu background and the theme preview cards.
    /// </summary>
    public static class ThemeLighting
    {
        public static void ApplyEnvironment(ThemeProfile p)
        {
            RenderSettings.skybox = MaterialLibrary.Sky(p);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;
            RenderSettings.reflectionIntensity = p.night ? 0.6f : 1f;
        }

        public static Light SetupSun(Light sun, ThemeProfile p)
        {
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.color = p.sunColor;
            sun.intensity = p.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(p.sunEuler);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = p.night ? 0.6f : 0.85f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = sun;
            return sun;
        }

        /// <summary>Creates a global post-processing volume tuned for the theme.</summary>
        public static Volume CreateVolume(ThemeProfile p, Transform parent)
        {
            var go = new GameObject("PostFX_" + p.id);
            if (parent != null) go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ZR_Post_" + p.id;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(p.postExposure);
            ca.contrast.Override(p.contrast);
            ca.saturation.Override(p.saturation);
            ca.colorFilter.Override(p.colorFilter);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(p.bloomThreshold);
            bloom.intensity.Override(QualityManager.Tier == QualityTier.Low ? p.bloomIntensity * 0.5f : p.bloomIntensity);
            bloom.scatter.Override(0.7f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(p.vignette);
            vig.smoothness.Override(0.45f);

            if (QualityManager.Tier != QualityTier.Low)
            {
                var grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(p.filmGrain);
            }

            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.05f);

            if (QualityManager.Tier == QualityTier.High)
            {
                var mb = profile.Add<MotionBlur>(true);
                mb.intensity.Override(0.12f);
            }

            vol.sharedProfile = profile;
            return vol;
        }

        public static void ConfigureCamera(Camera cam, ThemeProfile p)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 450f;
            cam.nearClipPlane = 0.15f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = QualityManager.Tier == QualityTier.Low ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = true;
            }
        }

        /// <summary>Realtime reflection probe that follows the camera (High quality only). Gives wet roads real reflections.</summary>
        public static ReflectionProbe FollowProbe(Transform parent)
        {
            var go = new GameObject("FollowReflectionProbe");
            go.transform.SetParent(parent, false);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(60, 40, 120);
            probe.boxProjection = false;
            probe.importance = 1;
            probe.hdr = true;
            go.AddComponent<ProbeRefresher>();
            return probe;
        }
    }

    public class ProbeRefresher : MonoBehaviour
    {
        ReflectionProbe probe;
        float next;
        void Awake() { probe = GetComponent<ReflectionProbe>(); }
        void Update()
        {
            if (Time.unscaledTime < next || probe == null) return;
            next = Time.unscaledTime + 0.5f;
            probe.RenderProbe();
        }
    }
}
