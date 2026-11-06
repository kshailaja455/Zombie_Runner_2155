using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieRunner
{
    /// <summary>
    /// Scalable and adaptive graphics. Low / Medium / High presets change render scale, shadows, MSAA,
    /// extra lights, particle and scenery density. Auto picks a preset per platform and then watches the
    /// frame rate, stepping render scale and preset down (or back up) to hold the 60 FPS target.
    /// In the Editor, URP asset values are restored when play mode ends so the project asset is not changed.
    /// </summary>
    public class QualityManager : MonoBehaviour
    {
        public static QualityTier Tier { get; private set; } = QualityTier.High;
        public static bool AllowExtraLights => Tier != QualityTier.Low;

        static QualityManager instance;
        static bool snapshotTaken;
        static float origScale, origShadowDist;
        static int origMsaa, origCascades, origLights;

        float accum; int frames; float window = 3f, goodTime; float renderScale = 1f;
        bool auto;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("ZR_QualityManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<QualityManager>();
            instance.ApplySetting(GameSettings.Quality);
            GameSettings.OnChanged += () => { if (instance != null && instance.lastSetting != GameSettings.Quality) instance.ApplySetting(GameSettings.Quality); };
        }

        QualityTier lastSetting;

        void ApplySetting(QualityTier setting)
        {
            lastSetting = setting;
            auto = setting == QualityTier.Auto;
            var tier = auto ? (Application.isMobilePlatform ? QualityTier.Medium : QualityTier.High) : setting;
            Apply(tier);
        }

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        void Apply(QualityTier tier)
        {
            Tier = tier;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            var urp = Urp;
            if (urp != null)
            {
                if (!snapshotTaken)
                {
                    snapshotTaken = true;
                    origScale = urp.renderScale; origShadowDist = urp.shadowDistance; origMsaa = urp.msaaSampleCount;
                    origCascades = urp.shadowCascadeCount; origLights = urp.maxAdditionalLightsCount;
                }
                switch (tier)
                {
                    case QualityTier.Low:
                        renderScale = Application.isMobilePlatform ? 0.7f : 0.85f;
                        urp.shadowDistance = 35f; urp.msaaSampleCount = 1; urp.shadowCascadeCount = 1; urp.maxAdditionalLightsCount = 0;
                        break;
                    case QualityTier.Medium:
                        renderScale = Application.isMobilePlatform ? 0.85f : 1f;
                        urp.shadowDistance = 60f; urp.msaaSampleCount = 1; urp.shadowCascadeCount = 2; urp.maxAdditionalLightsCount = 4;
                        break;
                    default:
                        renderScale = 1f;
                        urp.shadowDistance = 90f; urp.msaaSampleCount = 4; urp.shadowCascadeCount = 4; urp.maxAdditionalLightsCount = 8;
                        break;
                }
                urp.renderScale = renderScale;
            }
            FX.Density = tier == QualityTier.Low ? 0.4f : tier == QualityTier.Medium ? 0.7f : 1f;
            EnvironmentGenerator.DecorDensity = tier == QualityTier.Low ? 0.55f : tier == QualityTier.Medium ? 0.8f : 1f;
            ProceduralTextures.Resolution = tier == QualityTier.Low ? 128 : 256;
            QualitySettings.lodBias = tier == QualityTier.Low ? 0.6f : tier == QualityTier.Medium ? 1f : 1.5f;
            QualitySettings.anisotropicFiltering = tier == QualityTier.Low ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = tier == QualityTier.Low && Application.isMobilePlatform ? 1 : 0;
#else
            QualitySettings.masterTextureLimit = tier == QualityTier.Low && Application.isMobilePlatform ? 1 : 0;
#endif
        }

        void Update()
        {
            if (!auto) return;
            accum += Time.unscaledDeltaTime; frames++;
            if (accum < window) return;
            float fps = frames / accum;
            accum = 0; frames = 0;
            var urp = Urp;
            if (fps < 50f)
            {
                goodTime = 0;
                if (urp != null && renderScale > 0.7f) { renderScale -= 0.1f; urp.renderScale = renderScale; }
                else if (Tier > QualityTier.Low) Apply(Tier - 1); // takes full effect on next level load for scenery
            }
            else if (fps > 58f)
            {
                goodTime += window;
                if (goodTime > 12f && urp != null && renderScale < 1f) { renderScale = Mathf.Min(1f, renderScale + 0.05f); urp.renderScale = renderScale; goodTime = 0; }
            }
        }

        public static float CurrentFps { get; private set; }
        float fpsAccum; int fpsFrames;
        void LateUpdate()
        {
            fpsAccum += Time.unscaledDeltaTime; fpsFrames++;
            if (fpsAccum > 0.5f) { CurrentFps = fpsFrames / fpsAccum; fpsAccum = 0; fpsFrames = 0; }
        }

        void OnDestroy() => Restore();
        void OnApplicationQuit() => Restore();

        static void Restore()
        {
#if UNITY_EDITOR
            var urp = Urp;
            if (urp != null && snapshotTaken)
            {
                urp.renderScale = origScale; urp.shadowDistance = origShadowDist; urp.msaaSampleCount = origMsaa;
                urp.shadowCascadeCount = origCascades; urp.maxAdditionalLightsCount = origLights;
            }
#endif
        }
    }
}
