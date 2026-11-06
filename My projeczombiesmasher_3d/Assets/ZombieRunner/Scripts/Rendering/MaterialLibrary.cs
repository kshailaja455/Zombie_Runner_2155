using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    /// <summary>
    /// Creates and caches PBR materials. Uses the base materials from ZRAssetRegistry (so URP shaders
    /// are guaranteed to be in builds) and falls back to Shader.Find in the Editor.
    /// Sets both URP Lit and Built-in Standard property names, so the materials work either way.
    /// </summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

        public static void ClearCache() => cache.Clear();

        static Material NewFrom(Material registryBase, params string[] shaderNames)
        {
            if (registryBase != null) return new Material(registryBase);
            foreach (var n in shaderNames)
            {
                var sh = Shader.Find(n);
                if (sh != null) return new Material(sh);
            }
            Debug.LogError("[ZombieRunner] No usable shader found (" + string.Join(", ", shaderNames) + "). Run the Setup Wizard.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        /// <summary>Standard opaque PBR material.</summary>
        public static Material Lit(string key, Color color, float smoothness = 0.25f, float metallic = 0f,
            TexKind? tex = null, float tiling = 1f, Color? emission = null, Texture2D albedoOverride = null, Texture2D normalOverride = null)
        {
            string k = key;
            if (cache.TryGetValue(k, out var m) && m != null) return m;
            var reg = ZRAssetRegistry.Instance;
            m = NewFrom(reg != null ? reg.litOpaque : null, "Universal Render Pipeline/Lit", "Standard");
            m.name = "ZR_" + key;
            SetColor(m, color);
            m.SetFloat(Smoothness, smoothness);
            m.SetFloat(Glossiness, smoothness);
            m.SetFloat(Metallic, metallic);

            Texture2D albedo = albedoOverride, normal = normalOverride;
            if (tex.HasValue && tex.Value != TexKind.Plain)
            {
                if (albedo == null) albedo = ProceduralTextures.Albedo(tex.Value);
                if (normal == null) normal = ProceduralTextures.Normal(tex.Value);
            }
            if (albedo != null)
            {
                m.SetTexture(BaseMap, albedo); m.SetTexture(MainTex, albedo);
                m.SetTextureScale(BaseMap, Vector2.one * tiling); m.SetTextureScale(MainTex, Vector2.one * tiling);
            }
            if (normal != null)
            {
                m.SetTexture(BumpMap, normal);
                m.SetFloat(BumpScale, 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColor, emission.Value);
                if (tex.HasValue && tex.Value == TexKind.FacadeLit)
                {
                    var em = ProceduralTextures.Emission(TexKind.FacadeLit);
                    m.SetTexture(EmissionMap, em);
                    m.SetTextureScale(EmissionMap, Vector2.one * tiling);
                }
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            cache[k] = m;
            return m;
        }

        /// <summary>Emissive unlit-looking material (neon, fire cores, lit signs).</summary>
        public static Material Emissive(string key, Color color, float intensity)
        {
            return Lit("emit_" + key, color * 0.2f, 0.6f, 0f, null, 1f, color * intensity);
        }

        /// <summary>Transparent lit material (glass, shield bubble, water puddles).</summary>
        public static Material Transparent(string key, Color color, float smoothness = 0.9f, Color? emission = null)
        {
            if (cache.TryGetValue("tr_" + key, out var m) && m != null) return m;
            var reg = ZRAssetRegistry.Instance;
            m = NewFrom(reg != null ? reg.litTransparent : null, "Universal Render Pipeline/Lit", "Standard");
            m.name = "ZR_T_" + key;
            MakeTransparent(m, false);
            SetColor(m, color);
            m.SetFloat(Smoothness, smoothness);
            m.SetFloat(Glossiness, smoothness);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionColor, emission.Value); }
            cache["tr_" + key] = m;
            return m;
        }

        /// <summary>Particle material. Additive for fire/sparks/glow, alpha for smoke/dust/rain.</summary>
        public static Material Particle(string key, bool additive, Texture2D tex = null)
        {
            string k = "pt_" + key;
            if (cache.TryGetValue(k, out var m) && m != null) return m;
            var reg = ZRAssetRegistry.Instance;
            Material b = reg != null ? (additive ? reg.particleAdditive : reg.particleAlpha) : null;
            m = NewFrom(b, "Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Legacy Shaders/Particles/Alpha Blended");
            m.name = "ZR_P_" + key;
            if (b == null) MakeTransparent(m, additive);
            var t = tex != null ? tex : ProceduralTextures.SoftParticle();
            m.SetTexture(BaseMap, t); m.SetTexture(MainTex, t);
            SetColor(m, Color.white);
            cache[k] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        /// <summary>Configures URP Lit / Particles for transparency at runtime (no ShaderGUI available in builds).</summary>
        public static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            // Built-in Standard fallback
            m.SetFloat("_Mode", 3f);
            m.EnableKeyword("_ALPHABLEND_ON");
        }

        /// <summary>A procedural sky material for a theme.</summary>
        public static Material Sky(ThemeProfile p)
        {
            var reg = ZRAssetRegistry.Instance;
            var set = reg != null ? reg.Theme(p.id) : null;
            if (set != null && set.skyboxOverride != null) return set.skyboxOverride;
            if (reg != null && reg.themeSkyboxes != null && reg.themeSkyboxes.Count > (int)p.id && reg.themeSkyboxes[(int)p.id] != null) return reg.themeSkyboxes[(int)p.id];
            string k = "sky_" + p.id;
            if (cache.TryGetValue(k, out var m) && m != null) return m;
            m = NewFrom(reg != null ? reg.skyboxProcedural : null, "Skybox/Procedural");
            m.name = "ZR_Sky_" + p.id;
            ConfigureSky(m, p);
            cache[k] = m;
            return m;
        }

        public static void ConfigureSky(Material m, ThemeProfile p)
        {
            m.SetColor("_SkyTint", p.skyTint);
            m.SetColor("_GroundColor", p.skyGround);
            m.SetFloat("_Exposure", p.skyExposure);
            m.SetFloat("_AtmosphereThickness", p.atmosphere);
            m.SetFloat("_SunSize", p.night ? 0.03f : 0.045f);
            m.SetFloat("_SunSizeConvergence", p.night ? 10f : 5f);
        }
    }

    /// <summary>Named materials for each theme, built once per level.</summary>
    public static class Palette
    {
        public static Material Road(ThemeProfile p)
        {
            var set = ZRAssetRegistry.Instance != null ? ZRAssetRegistry.Instance.Theme(p.id) : null;
            switch (p.road)
            {
                case RoadStyle.Mud: return MaterialLibrary.Lit("road_mud", p.roadColor, 0.45f, 0f, TexKind.Mud, 2f, null, set?.groundAlbedo, set?.groundNormal);
                case RoadStyle.DesertRoad: return MaterialLibrary.Lit("road_desert", p.roadColor, 0.15f, 0f, TexKind.Asphalt, 2f, null, set?.groundAlbedo, set?.groundNormal);
                case RoadStyle.WetAsphalt: return MaterialLibrary.Lit("road_wet", p.roadColor, 0.86f, 0.05f, TexKind.WetAsphalt, 2f, null, set?.groundAlbedo, set?.groundNormal);
                default: return MaterialLibrary.Lit("road_cracked", p.roadColor, 0.2f, 0f, TexKind.Asphalt, 2f, null, set?.groundAlbedo, set?.groundNormal);
            }
        }

        public static Material Verge(ThemeProfile p)
        {
            var set = ZRAssetRegistry.Instance != null ? ZRAssetRegistry.Instance.Theme(p.id) : null;
            switch (p.id)
            {
                case ThemeId.Forest: return MaterialLibrary.Lit("verge_forest", p.verge, 0.15f, 0f, TexKind.Grass, 4f, null, set?.vergeAlbedo, set?.vergeNormal);
                case ThemeId.Desert: return MaterialLibrary.Lit("verge_desert", p.verge, 0.1f, 0f, TexKind.Sand, 3f, null, set?.vergeAlbedo, set?.vergeNormal);
                case ThemeId.DarkCity: return MaterialLibrary.Lit("verge_dark", p.verge, 0.7f, 0f, TexKind.Concrete, 3f, null, set?.vergeAlbedo, set?.vergeNormal);
                default: return MaterialLibrary.Lit("verge_ruin", p.verge, 0.15f, 0f, TexKind.Concrete, 3f, null, set?.vergeAlbedo, set?.vergeNormal);
            }
        }

        public static Material VergeAlt(ThemeProfile p)
        {
            switch (p.id)
            {
                case ThemeId.Forest: return MaterialLibrary.Lit("vergealt_forest", p.vergeAlt, 0.3f, 0f, TexKind.Mud, 4f);
                case ThemeId.Desert: return MaterialLibrary.Lit("vergealt_desert", p.vergeAlt, 0.1f, 0f, TexKind.Sand, 2f);
                case ThemeId.DarkCity: return MaterialLibrary.Lit("vergealt_dark", p.vergeAlt, 0.8f, 0f, TexKind.Asphalt, 3f);
                default: return MaterialLibrary.Lit("vergealt_ruin", p.vergeAlt, 0.1f, 0f, TexKind.Grass, 3f);
            }
        }

        public static Material Bark => MaterialLibrary.Lit("bark", new Color(0.33f, 0.26f, 0.2f), 0.1f, 0f, TexKind.Bark, 1f);
        public static Material DeadBark => MaterialLibrary.Lit("deadbark", new Color(0.42f, 0.38f, 0.33f), 0.1f, 0f, TexKind.Bark, 1f);
        public static Material Pine => MaterialLibrary.Lit("pine", new Color(0.16f, 0.24f, 0.17f), 0.12f, 0f, TexKind.Foliage, 2f);
        public static Material Leaves => MaterialLibrary.Lit("leaves", new Color(0.24f, 0.3f, 0.16f), 0.12f, 0f, TexKind.Foliage, 2f);
        public static Material Moss => MaterialLibrary.Lit("moss", new Color(0.25f, 0.33f, 0.16f), 0.1f, 0f, TexKind.Grass, 2f);
        public static Material Rock => MaterialLibrary.Lit("rock", new Color(0.45f, 0.44f, 0.42f), 0.2f, 0f, TexKind.Rock, 1f);
        public static Material DesertRock => MaterialLibrary.Lit("desertrock", new Color(0.62f, 0.44f, 0.3f), 0.12f, 0f, TexKind.Rock, 1f);
        public static Material Sand => MaterialLibrary.Lit("sand", new Color(0.79f, 0.63f, 0.43f), 0.08f, 0f, TexKind.Sand, 2f);
        public static Material MudWet => MaterialLibrary.Lit("mudwet", new Color(0.24f, 0.18f, 0.12f), 0.85f, 0f, TexKind.Mud, 1f);
        public static Material Wood => MaterialLibrary.Lit("wood", new Color(0.45f, 0.33f, 0.22f), 0.15f, 0f, TexKind.Wood, 1f);
        public static Material OldWood => MaterialLibrary.Lit("oldwood", new Color(0.38f, 0.33f, 0.28f), 0.1f, 0f, TexKind.Wood, 1f);
        public static Material Concrete => MaterialLibrary.Lit("concrete", new Color(0.6f, 0.59f, 0.56f), 0.15f, 0f, TexKind.Concrete, 1f);
        public static Material DarkConcrete => MaterialLibrary.Lit("darkconcrete", new Color(0.35f, 0.35f, 0.36f), 0.3f, 0f, TexKind.Concrete, 1f);
        public static Material Rubble => MaterialLibrary.Lit("rubble", new Color(0.5f, 0.48f, 0.45f), 0.1f, 0f, TexKind.Rubble, 1f);
        public static Material Brick => MaterialLibrary.Lit("brick", new Color(0.55f, 0.32f, 0.26f), 0.12f, 0f, TexKind.Brick, 1f);
        public static Material Rust => MaterialLibrary.Lit("rust", new Color(0.55f, 0.36f, 0.25f), 0.25f, 0.4f, TexKind.Rust, 1f);
        public static Material Steel => MaterialLibrary.Lit("steel", new Color(0.5f, 0.52f, 0.55f), 0.55f, 0.85f, TexKind.PaintedMetal, 1f);
        public static Material DarkSteel => MaterialLibrary.Lit("darksteel", new Color(0.2f, 0.21f, 0.22f), 0.45f, 0.8f, TexKind.PaintedMetal, 1f);
        public static Material Tire => MaterialLibrary.Lit("tire", new Color(0.08f, 0.08f, 0.08f), 0.25f, 0f, TexKind.Rubble, 1f);
        public static Material Glass => MaterialLibrary.Transparent("glass", new Color(0.15f, 0.2f, 0.22f, 0.55f), 0.95f);
        public static Material Burnt => MaterialLibrary.Lit("burnt", new Color(0.12f, 0.11f, 0.1f), 0.2f, 0.3f, TexKind.Rust, 1f);
        public static Material Military => MaterialLibrary.Lit("military", new Color(0.36f, 0.38f, 0.27f), 0.3f, 0.3f, TexKind.PaintedMetal, 1f);
        public static Material DesertMilitary => MaterialLibrary.Lit("desertmil", new Color(0.62f, 0.54f, 0.4f), 0.3f, 0.3f, TexKind.PaintedMetal, 1f);
        public static Material Sandbag => MaterialLibrary.Lit("sandbag", new Color(0.55f, 0.5f, 0.38f), 0.05f, 0f, TexKind.Fabric, 2f);
        public static Material Canvas => MaterialLibrary.Lit("canvas", new Color(0.36f, 0.4f, 0.3f), 0.05f, 0f, TexKind.Fabric, 2f);
        public static Material HazardOrange => MaterialLibrary.Lit("hazard", new Color(0.9f, 0.38f, 0.08f), 0.4f, 0f, TexKind.PaintedMetal, 1f);
        public static Material HazardWhite => MaterialLibrary.Lit("hazardw", new Color(0.85f, 0.85f, 0.82f), 0.4f, 0f, TexKind.PaintedMetal, 1f);
        public static Material PoliceBlue => MaterialLibrary.Lit("policeblue", new Color(0.1f, 0.15f, 0.35f), 0.5f, 0.3f, TexKind.PaintedMetal, 1f);
        public static Material Asphalt => MaterialLibrary.Lit("asphaltchunk", new Color(0.2f, 0.2f, 0.2f), 0.2f, 0f, TexKind.Asphalt, 1f);
        public static Material LaneLine => MaterialLibrary.Lit("laneline", new Color(0.85f, 0.82f, 0.7f), 0.35f, 0f, TexKind.Concrete, 1f);
        public static Material LaneLineYellow => MaterialLibrary.Lit("lineyellow", new Color(0.85f, 0.65f, 0.15f), 0.35f, 0f, TexKind.Concrete, 1f);
        public static Material Hole => MaterialLibrary.Lit("hole", new Color(0.02f, 0.02f, 0.02f), 0f, 0f);
        public static Material Puddle => MaterialLibrary.Lit("puddle", new Color(0.05f, 0.06f, 0.07f), 0.97f, 0.2f);
        public static Material Vine => MaterialLibrary.Lit("vine", new Color(0.2f, 0.3f, 0.14f), 0.1f, 0f, TexKind.Foliage, 3f);
        public static Material Gold => MaterialLibrary.Lit("gold", new Color(1f, 0.76f, 0.25f), 0.85f, 1f, null, 1f, new Color(0.35f, 0.22f, 0.02f));

        public static Material CarPaint(int i)
        {
            var colors = new[]
            {
                new Color(0.42f, 0.1f, 0.08f), new Color(0.12f, 0.2f, 0.32f), new Color(0.65f, 0.63f, 0.6f),
                new Color(0.15f, 0.25f, 0.18f), new Color(0.55f, 0.48f, 0.3f), new Color(0.1f, 0.1f, 0.11f)
            };
            i = Mathf.Abs(i) % colors.Length;
            return MaterialLibrary.Lit("carpaint" + i, colors[i], 0.5f, 0.35f, TexKind.Rust, 1f);
        }

        public static Material Facade(int i, bool lit)
        {
            var colors = new[]
            {
                new Color(0.55f, 0.52f, 0.48f), new Color(0.45f, 0.42f, 0.4f), new Color(0.5f, 0.45f, 0.38f),
                new Color(0.38f, 0.4f, 0.42f), new Color(0.6f, 0.56f, 0.5f)
            };
            i = Mathf.Abs(i) % colors.Length;
            if (lit) return MaterialLibrary.Lit("facadelit" + i, colors[i] * 0.6f, 0.3f, 0f, TexKind.FacadeLit, 1f, Color.white * 1.6f);
            return MaterialLibrary.Lit("facade" + i, colors[i], 0.2f, 0f, TexKind.Facade, 1f);
        }

        public static Material Neon(Color c) => MaterialLibrary.Emissive("neon" + ColorUtility.ToHtmlStringRGB(c), c, 6f);
        public static Material Lamp => MaterialLibrary.Emissive("lamp", new Color(1f, 0.82f, 0.55f), 5f);
        public static Material Fire => MaterialLibrary.Emissive("fire", new Color(1f, 0.45f, 0.1f), 8f);
    }
}
