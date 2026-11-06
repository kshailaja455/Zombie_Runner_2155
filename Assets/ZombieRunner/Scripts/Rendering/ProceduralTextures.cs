using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    public enum TexKind
    {
        Mud, Sand, Asphalt, WetAsphalt, Concrete, Bark, Foliage, Grass, Rock, PaintedMetal, Rust, Wood, Fabric, Skin,
        Facade, FacadeLit, Rubble, Brick, Plain
    }

    /// <summary>
    /// Generates tileable detail textures (albedo luminance + normal map) at runtime.
    /// Albedo textures are near-white detail that the material's base colour tints, so one
    /// texture serves many palettes. Normal maps are encoded so URP decodes them correctly
    /// on both desktop (AG) and mobile (RGB) paths.
    /// </summary>
    public static class ProceduralTextures
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static int Resolution = 256;

        public static Texture2D Albedo(TexKind k) => Get(k, false);
        public static Texture2D Normal(TexKind k) => Get(k, true);
        public static Texture2D Emission(TexKind k) => k == TexKind.FacadeLit ? GetEmission() : null;

        static Texture2D Get(TexKind k, bool normal)
        {
            string key = k + (normal ? "_n" : "_a") + Resolution;
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            int n = (k == TexKind.Facade || k == TexKind.FacadeLit) ? Mathf.Max(256, Resolution) : Resolution;
            float[] h = Height(k, n);
            t = normal ? BuildNormal(h, n, NormalStrength(k)) : BuildAlbedo(k, h, n);
            t.name = "ZR_" + key;
            cache[key] = t;
            return t;
        }

        static float NormalStrength(TexKind k)
        {
            switch (k)
            {
                case TexKind.Bark: case TexKind.Rock: case TexKind.Rubble: case TexKind.Brick: return 4f;
                case TexKind.Mud: case TexKind.Concrete: case TexKind.Asphalt: return 2.5f;
                case TexKind.WetAsphalt: case TexKind.Sand: return 1.5f;
                case TexKind.Skin: case TexKind.Fabric: case TexKind.Plain: return 0.6f;
                default: return 2f;
            }
        }

        /// <summary>Height field per material type, all tileable.</summary>
        static float[] Height(TexKind k, int n)
        {
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n, val;
                switch (k)
                {
                    case TexKind.Mud:
                        val = Noise.Fbm(u, v, 4, 5, 11) * 0.7f + Noise.Fbm(u, v, 16, 3, 12) * 0.3f;
                        val = Mathf.Lerp(val, Mathf.Floor(val * 4f) / 4f, 0.3f); // puddle steps
                        break;
                    case TexKind.Sand:
                        val = 0.5f + 0.5f * Mathf.Sin((v * 10f + Noise.Fbm(u, v, 4, 3, 21) * 3f) * Mathf.PI * 2f) * 0.35f
                              + Noise.Fbm(u, v, 32, 2, 22) * 0.3f;
                        break;
                    case TexKind.Asphalt:
                    case TexKind.WetAsphalt:
                    {
                        float grain = Noise.Fbm(u, v, 64, 2, 31);
                        float patch = Noise.Fbm(u, v, 4, 3, 32);
                        float crack = Crack(u, v, 6, 33);
                        val = grain * 0.5f + patch * 0.4f - crack * 0.6f;
                        break;
                    }
                    case TexKind.Concrete:
                        val = Noise.Fbm(u, v, 8, 4, 41) * 0.6f + Noise.Fbm(u, v, 64, 2, 42) * 0.4f - Crack(u, v, 4, 43) * 0.4f;
                        break;
                    case TexKind.Bark:
                    {
                        float ridges = Mathf.Abs(Mathf.Sin((u * 12f + Noise.Fbm(u, v, 4, 3, 51) * 1.5f) * Mathf.PI));
                        val = ridges * 0.7f + Noise.Fbm(u, v * 0.25f, 16, 3, 52) * 0.3f;
                        break;
                    }
                    case TexKind.Foliage:
                        val = Noise.Fbm(u, v, 16, 4, 61);
                        val = Mathf.Pow(val, 0.8f);
                        break;
                    case TexKind.Grass:
                        val = Noise.Fbm(u, v, 32, 4, 71) * 0.7f + Noise.Fbm(u, v, 4, 2, 72) * 0.3f;
                        break;
                    case TexKind.Rock:
                    case TexKind.Rubble:
                        val = 1f - Mathf.Abs(Noise.Fbm(u, v, 4, 6, 81) * 2f - 1f);
                        val = val * 0.7f + Noise.Fbm(u, v, 32, 2, 82) * 0.3f;
                        break;
                    case TexKind.PaintedMetal:
                    case TexKind.Rust:
                        val = Noise.Fbm(u, v, 8, 4, 91) * 0.3f + 0.6f + (Noise.Fbm(u, v, 4, 5, 92) > 0.62f ? -0.25f : 0f);
                        break;
                    case TexKind.Wood:
                        val = 0.5f + 0.5f * Mathf.Sin((v * 40f + Noise.Fbm(u, v, 4, 3, 101) * 8f)) * 0.5f;
                        val -= (Mathf.Repeat(u * 4f, 1f) < 0.03f) ? 0.5f : 0f; // plank gaps
                        break;
                    case TexKind.Fabric:
                        val = 0.5f + 0.25f * Mathf.Sin(u * n * 0.7f) * Mathf.Sin(v * n * 0.7f) + Noise.Fbm(u, v, 8, 3, 111) * 0.3f;
                        break;
                    case TexKind.Skin:
                        val = 0.6f + Noise.Fbm(u, v, 16, 3, 121) * 0.4f;
                        break;
                    case TexKind.Brick:
                    {
                        float row = Mathf.Floor(v * 16f);
                        float bu = u * 8f + (row % 2f) * 0.5f;
                        bool mortar = Mathf.Repeat(v * 16f, 1f) < 0.12f || Mathf.Repeat(bu, 1f) < 0.06f;
                        val = mortar ? 0.2f : 0.7f + Noise.Fbm(u, v, 32, 2, 131) * 0.3f;
                        break;
                    }
                    case TexKind.Facade:
                    case TexKind.FacadeLit:
                        val = FacadeHeight(u, v);
                        break;
                    default:
                        val = 0.5f + Noise.Fbm(u, v, 8, 2, 1) * 0.1f;
                        break;
                }
                h[y * n + x] = Mathf.Clamp01(val);
            }
            return h;
        }

        // A facade texture tile = 4 x 4 windows. Windows are recessed (low height).
        static float FacadeHeight(float u, float v)
        {
            float wx = Mathf.Repeat(u * 4f, 1f), wy = Mathf.Repeat(v * 4f, 1f);
            bool window = wx > 0.2f && wx < 0.8f && wy > 0.25f && wy < 0.85f;
            bool ledge = wy < 0.08f;
            float baseH = 0.7f + Noise.Fbm(u, v, 16, 3, 141) * 0.2f;
            if (ledge) return 0.95f;
            return window ? 0.15f : baseH;
        }

        static float Crack(float u, float v, int period, int seed)
        {
            float a = Noise.Fbm(u, v, period, 4, seed);
            float line = 1f - Mathf.Clamp01(Mathf.Abs(a - 0.5f) * 40f);
            return line;
        }

        static Texture2D BuildAlbedo(TexKind k, float[] h, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float v = h[i];
                float u = (i % n) / (float)n, w = (i / n) / (float)n;
                Color c;
                switch (k)
                {
                    case TexKind.Mud: c = Color.Lerp(new Color(0.55f, 0.5f, 0.45f), Color.white, v); break;
                    case TexKind.Rust:
                    {
                        float r = Noise.Fbm(u, w, 4, 5, 92);
                        c = r > 0.55f ? new Color(0.75f, 0.42f, 0.25f) * (0.7f + v * 0.3f) : Color.white * (0.75f + v * 0.25f);
                        break;
                    }
                    case TexKind.PaintedMetal:
                    {
                        float r = Noise.Fbm(u, w, 4, 5, 92);
                        c = r > 0.62f ? new Color(0.55f, 0.35f, 0.25f) : Color.white * (0.85f + v * 0.15f);
                        break;
                    }
                    case TexKind.Facade:
                    case TexKind.FacadeLit:
                    {
                        float wx = Mathf.Repeat(u * 4f, 1f), wy = Mathf.Repeat(w * 4f, 1f);
                        bool window = wx > 0.2f && wx < 0.8f && wy > 0.25f && wy < 0.85f;
                        c = window ? new Color(0.12f, 0.13f, 0.15f) : Color.white * (0.65f + v * 0.35f);
                        // grime streaks
                        c *= 1f - Noise.Fbm(u * 0.25f, w, 8, 2, 151) * 0.35f * (1f - w);
                        break;
                    }
                    case TexKind.Foliage:
                        c = Color.Lerp(new Color(0.55f, 0.6f, 0.45f), new Color(1f, 1f, 0.9f), v);
                        break;
                    default:
                        c = Color.white * (0.55f + v * 0.45f);
                        break;
                }
                c.a = 1f;
                px[i] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        static Texture2D BuildNormal(float[] h, int n, float strength)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + (x - 1 + n) % n], r = h[y * n + (x + 1) % n];
                float d = h[((y - 1 + n) % n) * n + x], u = h[((y + 1) % n) * n + x];
                var nrm = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                // R = x, G = y, B = z, A = 1 decodes correctly with both UnpackNormalRGB and UnpackNormalAG(a*=r).
                px[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        static Texture2D GetEmission()
        {
            string key = "facade_emit";
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            int n = 256;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, name = "ZR_FacadeEmission" };
            var px = new Color32[n * n];
            var rng = new Rng(7);
            var lit = new bool[16];
            var tint = new Color[16];
            for (int i = 0; i < 16; i++)
            {
                lit[i] = rng.Chance(0.22f);
                tint[i] = rng.Chance(0.5f) ? new Color(1f, 0.78f, 0.45f) : new Color(0.65f, 0.8f, 1f);
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float wx = Mathf.Repeat(u * 4f, 1f), wy = Mathf.Repeat(v * 4f, 1f);
                int cell = Mathf.FloorToInt(u * 4f) + Mathf.FloorToInt(v * 4f) * 4;
                bool window = wx > 0.2f && wx < 0.8f && wy > 0.25f && wy < 0.85f;
                px[y * n + x] = window && lit[cell] ? tint[cell] * (0.6f + 0.4f * wy) : Color.black;
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            cache[key] = t;
            return t;
        }

        /// <summary>Soft round sprite for particles.</summary>
        public static Texture2D SoftParticle()
        {
            if (cache.TryGetValue("soft", out var t) && t != null) return t;
            int n = 64;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, name = "ZR_SoftParticle" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply(true, false);
            cache["soft"] = t;
            return t;
        }

        /// <summary>Wispy smoke sprite for fog banks and smoke columns.</summary>
        public static Texture2D Smoke()
        {
            if (cache.TryGetValue("smoke", out var t) && t != null) return t;
            int n = 128;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, name = "ZR_Smoke" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float fall = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float a = fall * fall * Noise.Fbm(u, v, 4, 5, 999) * 1.6f;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
            }
            t.Apply(true, false);
            cache["smoke"] = t;
            return t;
        }

        /// <summary>Rounded-rectangle sprite used by the UI (9-sliced).</summary>
        public static Sprite RoundedRect(int radius = 18)
        {
            string key = "rr" + radius;
            if (spriteCache.TryGetValue(key, out var s) && s != null) return s;
            int n = radius * 2 + 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ZR_UI_" + key };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius), cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            spriteCache[key] = s;
            return s;
        }

        /// <summary>Circle sprite for icons.</summary>
        public static Sprite Circle()
        {
            if (spriteCache.TryGetValue("circle", out var s) && s != null) return s;
            int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ZR_UI_Circle" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
            }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            spriteCache["circle"] = s;
            return s;
        }

        /// <summary>Radial red edge used by the danger overlay.</summary>
        public static Sprite EdgeVignette()
        {
            if (spriteCache.TryGetValue("vig", out var s) && s != null) return s;
            int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ZR_UI_Vignette" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.6f);
                t.SetPixel(x, y, new Color(1, 1, 1, d * d));
            }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            spriteCache["vig"] = s;
            return s;
        }

        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    }
}
