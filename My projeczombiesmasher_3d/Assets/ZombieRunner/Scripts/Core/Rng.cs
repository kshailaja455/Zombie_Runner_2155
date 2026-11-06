using UnityEngine;

namespace ZombieRunner
{
    /// <summary>Small deterministic RNG (xorshift) so generated content is repeatable per seed.</summary>
    public class Rng
    {
        uint s;
        public Rng(int seed) { s = (uint)(seed * 747796405 + 2891336453u); if (s == 0) s = 1; Next(); }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public float Value => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Value;
        public int Range(int a, int bExclusive) => bExclusive <= a ? a : a + (int)(Next() % (uint)(bExclusive - a));
        public bool Chance(float p) => Value < p;
        public T Pick<T>(T[] arr) => arr[Range(0, arr.Length)];
        public float Sign => Value < 0.5f ? -1f : 1f;
        public Vector3 InsideUnitSphere() { Vector3 v; do { v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); } while (v.sqrMagnitude > 1f); return v; }
        public Color Vary(Color c, float amount) { float k = 1f + Range(-amount, amount); return new Color(c.r * k, c.g * k, c.b * k, c.a); }
    }

    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>Tileable value noise in [0,1]; period is in cells.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        /// <summary>Tileable fractal noise; u,v in [0,1).</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float persistence = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0; int p = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u * p, v * p, p, seed + i * 31) * amp;
                norm += amp; amp *= persistence; p *= 2;
            }
            return sum / norm;
        }

        /// <summary>Non-tiling 3D-ish noise for mesh displacement.</summary>
        public static float Smooth3(Vector3 p, int seed)
        {
            return (Mathf.PerlinNoise(p.x + seed * 0.37f, p.y + p.z * 0.71f) + Mathf.PerlinNoise(p.z - seed * 0.13f, p.x * 0.83f + p.y)) * 0.5f;
        }
    }
}
