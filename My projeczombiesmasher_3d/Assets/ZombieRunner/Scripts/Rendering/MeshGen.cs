using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    /// <summary>
    /// Procedural mesh primitives with world-scaled UVs (so textures do not stretch) and tangents
    /// (so normal maps light correctly). Meshes are cached by their parameters.
    /// </summary>
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvSize)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(uvSize.x, 0)); uv.Add(new Vector2(uvSize.x, uvSize.y)); uv.Add(new Vector2(0, uvSize.y));
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }

            public Mesh Build(string name, bool recalcNormals = false)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                if (recalcNormals || n.Count != v.Count) m.RecalculateNormals(); else m.SetNormals(n);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Unit box centred on origin, UVs in metres * density.</summary>
        public static Mesh Box(Vector3 size, float uvDensity = 0.5f)
        {
            string key = $"box{size.x:F2}_{size.y:F2}_{size.z:F2}_{uvDensity:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            Vector3 h = size * 0.5f;
            float d = uvDensity;
            // +Z
            b.Quad(new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), Vector3.forward, new Vector2(size.x * d, size.y * d));
            // -Z
            b.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), Vector3.back, new Vector2(size.x * d, size.y * d));
            // +X
            b.Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Vector3.right, new Vector2(size.z * d, size.y * d));
            // -X
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), Vector3.left, new Vector2(size.z * d, size.y * d));
            // +Y
            b.Quad(new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.up, new Vector2(size.x * d, size.z * d));
            // -Y
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), Vector3.down, new Vector2(size.x * d, size.z * d));
            mesh = b.Build(key);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Cylinder along Y, centred on origin. radiusTop lets it taper (cone when 0).</summary>
        public static Mesh Cylinder(float radiusBottom, float radiusTop, float height, int segments = 12, bool caps = true, float uvDensity = 0.5f)
        {
            string key = $"cyl{radiusBottom:F2}_{radiusTop:F2}_{height:F2}_{segments}_{caps}_{uvDensity:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            float hy = height * 0.5f;
            float circ = Mathf.PI * 2f * Mathf.Max(radiusBottom, radiusTop) * uvDensity;
            float slope = (radiusBottom - radiusTop) / Mathf.Max(0.0001f, height);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                var nrm = new Vector3(cs, slope, sn).normalized;
                b.v.Add(new Vector3(cs * radiusBottom, -hy, sn * radiusBottom)); b.n.Add(nrm); b.uv.Add(new Vector2(i / (float)segments * circ, 0));
                b.v.Add(new Vector3(cs * radiusTop, hy, sn * radiusTop)); b.n.Add(nrm); b.uv.Add(new Vector2(i / (float)segments * circ, height * uvDensity));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                b.t.Add(a0); b.t.Add(a1); b.t.Add(b0);
                b.t.Add(b0); b.t.Add(a1); b.t.Add(b1);
            }
            if (caps)
            {
                Cap(b, -hy, radiusBottom, segments, false, uvDensity);
                if (radiusTop > 0.001f) Cap(b, hy, radiusTop, segments, true, uvDensity);
            }
            mesh = b.Build(key);
            cache[key] = mesh;
            return mesh;
        }

        static void Cap(Builder b, float y, float r, int seg, bool up, float d)
        {
            int c = b.v.Count;
            var nrm = up ? Vector3.up : Vector3.down;
            b.v.Add(new Vector3(0, y, 0)); b.n.Add(nrm); b.uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                b.v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); b.n.Add(nrm);
                b.uv.Add(new Vector2(Mathf.Cos(a) * r * d + 0.5f, Mathf.Sin(a) * r * d + 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                if (up) { b.t.Add(c); b.t.Add(c + i + 2); b.t.Add(c + i + 1); }
                else { b.t.Add(c); b.t.Add(c + i + 1); b.t.Add(c + i + 2); }
            }
        }

        /// <summary>UV sphere (ellipsoid via transform scale) with optional noise displacement for rocks, dunes, foliage.</summary>
        public static Mesh Sphere(int lat = 10, int lon = 14, float noise = 0f, int seed = 0, float noiseFreq = 1.6f, bool flatBottom = false)
        {
            string key = $"sph{lat}_{lon}_{noise:F2}_{seed}_{noiseFreq:F2}_{flatBottom}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float th = u * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)) * 0.5f;
                    if (noise > 0f)
                    {
                        // seam-safe: sample noise at the wrapped position
                        var q = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        float nz = Noise.Smooth3(q * noiseFreq + Vector3.one * 10f, seed) - 0.5f;
                        p *= 1f + nz * noise * 2f;
                    }
                    if (flatBottom && p.y < -0.05f) p.y = -0.05f;
                    b.v.Add(p);
                    b.uv.Add(new Vector2(u * 3f, v * 1.5f));
                }
            }
            for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int i = y * (lon + 1) + x;
                b.t.Add(i); b.t.Add(i + lon + 1); b.t.Add(i + 1);
                b.t.Add(i + 1); b.t.Add(i + lon + 1); b.t.Add(i + lon + 2);
            }
            mesh = b.Build(key, true);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Capsule along Y with total height h and radius r (body segments for humanoids).</summary>
        public static Mesh Capsule(float r, float h, int seg = 10)
        {
            string key = $"cap{r:F3}_{h:F3}_{seg}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            int rings = seg / 2;
            float half = Mathf.Max(0f, h * 0.5f - r);
            int rows = 0;
            for (int ring = 0; ring <= rings * 2 + 1; ring++)
            {
                float phi; float yoff;
                if (ring <= rings) { phi = ring / (float)rings * Mathf.PI * 0.5f; yoff = -half; }
                else { phi = Mathf.PI * 0.5f + (ring - rings - 1) / (float)rings * Mathf.PI * 0.5f; yoff = half; }
                float y = -Mathf.Cos(phi) * r + yoff;
                float rr = Mathf.Sin(phi) * r;
                for (int x = 0; x <= seg; x++)
                {
                    float th = x / (float)seg * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(th) * rr, y, Mathf.Sin(th) * rr);
                    b.v.Add(p);
                    var c = new Vector3(0, yoff, 0);
                    b.n.Add((p - c).normalized);
                    b.uv.Add(new Vector2(x / (float)seg, (y + h * 0.5f) / Mathf.Max(0.01f, h)));
                }
                rows++;
            }
            for (int y = 0; y < rows - 1; y++)
            for (int x = 0; x < seg; x++)
            {
                int i = y * (seg + 1) + x;
                b.t.Add(i); b.t.Add(i + seg + 1); b.t.Add(i + 1);
                b.t.Add(i + 1); b.t.Add(i + seg + 1); b.t.Add(i + seg + 2);
            }
            mesh = b.Build(key);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat ground strip in XZ with gentle height noise on the outer edge (for verges and terrain).</summary>
        public static Mesh Ground(float width, float length, int segX, int segZ, float uvDensity, float edgeNoise, int seed, float edgeRise = 0f)
        {
            string key = $"gnd{width:F1}_{length:F1}_{segX}_{segZ}_{uvDensity:F2}_{edgeNoise:F2}_{seed}_{edgeRise:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            for (int z = 0; z <= segZ; z++)
            for (int x = 0; x <= segX; x++)
            {
                float fx = x / (float)segX, fz = z / (float)segZ;
                float px = (fx - 0.5f) * width, pz = fz * length;
                float edge = Mathf.Abs(fx - 0.5f) * 2f;
                // Seamless along Z: use periodic noise so consecutive chunks line up.
                float n = Noise.Value(fx * 6f, fz * 4f, 4, seed) - 0.5f;
                float y = (n * edgeNoise + edgeRise * edge * edge) * Mathf.SmoothStep(0f, 1f, edge);
                if (z == 0 || z == segZ) y = edgeRise * edge * edge * Mathf.SmoothStep(0f, 1f, edge);
                b.v.Add(new Vector3(px, y, pz));
                b.uv.Add(new Vector2(px * uvDensity, pz * uvDensity));
            }
            for (int z = 0; z < segZ; z++)
            for (int x = 0; x < segX; x++)
            {
                int i = z * (segX + 1) + x;
                b.t.Add(i); b.t.Add(i + segX + 1); b.t.Add(i + 1);
                b.t.Add(i + 1); b.t.Add(i + segX + 1); b.t.Add(i + segX + 2);
            }
            mesh = b.Build(key, true);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Two crossed vertical quads (grass tufts, ferns, light shafts).</summary>
        public static Mesh CrossQuad(float w, float h)
        {
            string key = $"xq{w:F2}_{h:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            float hw = w * 0.5f;
            b.Quad(new Vector3(-hw, 0, 0), new Vector3(hw, 0, 0), new Vector3(hw, h, 0), new Vector3(-hw, h, 0), Vector3.back, Vector2.one);
            b.Quad(new Vector3(hw, 0, 0), new Vector3(-hw, 0, 0), new Vector3(-hw, h, 0), new Vector3(hw, h, 0), Vector3.forward, Vector2.one);
            b.Quad(new Vector3(0, 0, -hw), new Vector3(0, 0, hw), new Vector3(0, h, hw), new Vector3(0, h, -hw), Vector3.right, Vector2.one);
            b.Quad(new Vector3(0, 0, hw), new Vector3(0, 0, -hw), new Vector3(0, h, -hw), new Vector3(0, h, hw), Vector3.left, Vector2.one);
            mesh = b.Build(key);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Torus in the XZ plane (shield ring, magnet ring, tyres seen from the side).</summary>
        public static Mesh Torus(float radius, float tube, int seg = 24, int tubeSeg = 8)
        {
            string key = $"tor{radius:F2}_{tube:F2}_{seg}_{tubeSeg}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                for (int j = 0; j <= tubeSeg; j++)
                {
                    float bb = j / (float)tubeSeg * Mathf.PI * 2f;
                    var dir = Mathf.Cos(bb) * c.normalized + Mathf.Sin(bb) * Vector3.up;
                    b.v.Add(c + dir * tube); b.n.Add(dir); b.uv.Add(new Vector2(i / (float)seg * 4f, j / (float)tubeSeg));
                }
            }
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < tubeSeg; j++)
            {
                int k = i * (tubeSeg + 1) + j;
                b.t.Add(k); b.t.Add(k + 1); b.t.Add(k + tubeSeg + 1);
                b.t.Add(k + 1); b.t.Add(k + tubeSeg + 2); b.t.Add(k + tubeSeg + 1);
            }
            mesh = b.Build(key);
            cache[key] = mesh;
            return mesh;
        }
    }
}
