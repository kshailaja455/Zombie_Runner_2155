using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    /// <summary>
    /// Fluent helper that assembles a prop from mesh parts and bakes it into ONE mesh with one
    /// sub-mesh per material, so each prop costs one renderer instead of dozens.
    /// </summary>
    public class PropBuilder
    {
        struct Part { public Mesh mesh; public Matrix4x4 m; public Material mat; }
        readonly List<Part> parts = new List<Part>();

        public int PartCount => parts.Count;

        public PropBuilder Add(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            parts.Add(new Part { mesh = mesh, m = Matrix4x4.TRS(pos, rot, scale), mat = mat });
            return this;
        }

        public PropBuilder Box(Vector3 center, Vector3 size, Material mat, Vector3 euler = default, float uv = 0.5f)
            => Add(MeshGen.Box(Round(size), uv), center, Quaternion.Euler(euler), Vector3.one, mat);

        public PropBuilder Cyl(Vector3 center, float radius, float height, Material mat, Vector3 euler = default, int seg = 12, float radiusTop = -1f)
            => Add(MeshGen.Cylinder(R(radius), R(radiusTop < 0 ? radius : radiusTop), R(height), seg), center, Quaternion.Euler(euler), Vector3.one, mat);

        public PropBuilder Cone(Vector3 baseCenter, float radius, float height, Material mat, int seg = 10, Vector3 euler = default)
        {
            var rot = Quaternion.Euler(euler);
            return Add(MeshGen.Cylinder(R(radius), 0f, R(height), seg), baseCenter + rot * Vector3.up * height * 0.5f, rot, Vector3.one, mat);
        }

        public PropBuilder Sphere(Vector3 center, Vector3 size, Material mat, float noise = 0f, int seed = 0, Vector3 euler = default, bool flatBottom = false, int lat = 8, int lon = 12)
            => Add(MeshGen.Sphere(lat, lon, noise, seed % 16, 1.6f, flatBottom), center, Quaternion.Euler(euler), size, mat);

        public PropBuilder Capsule(Vector3 center, float r, float h, Material mat, Vector3 euler = default)
            => Add(MeshGen.Capsule(R(r), R(h)), center, Quaternion.Euler(euler), Vector3.one, mat);

        public PropBuilder Torus(Vector3 center, float radius, float tube, Material mat, Vector3 euler = default)
            => Add(MeshGen.Torus(R(radius), R(tube)), center, Quaternion.Euler(euler), Vector3.one, mat);

        /// <summary>Wheel: a tyre cylinder lying on its side plus a hub.</summary>
        public PropBuilder Wheel(Vector3 center, float r, float w, bool flat = false)
        {
            var e = new Vector3(0, 0, 90);
            Cyl(center, r, w, Palette.Tire, e, 14);
            Cyl(center + new Vector3(Mathf.Sign(center.x) * w * 0.05f, 0, 0), r * 0.55f, w * 1.04f, Palette.DarkSteel, e, 10);
            if (flat) Box(center + Vector3.down * r * 0.8f, new Vector3(w, r * 0.3f, r * 1.3f), Palette.Tire);
            return this;
        }

        /// <summary>Copies another builder's parts with an extra transform (to reuse sub-assemblies).</summary>
        public PropBuilder Append(PropBuilder other, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var m = Matrix4x4.TRS(pos, rot, scale);
            foreach (var p in other.parts) parts.Add(new Part { mesh = p.mesh, m = m * p.m, mat = p.mat });
            return this;
        }

        static float R(float v) => Mathf.Round(v * 100f) / 100f; // keeps the mesh cache small
        static Vector3 Round(Vector3 v) => new Vector3(R(v.x), R(v.y), R(v.z));

        /// <summary>Bakes all parts into one GameObject with a MeshFilter/MeshRenderer.</summary>
        public GameObject Build(string name, Transform parent = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            if (parts.Count == 0) return go;
            var mesh = Bake(name, out var mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public Mesh Bake(string name, out Material[] materials)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            int verts = 0;
            foreach (var p in parts)
            {
                if (p.mesh == null || p.mat == null) continue;
                if (!groups.TryGetValue(p.mat, out var list)) { list = new List<CombineInstance>(); groups[p.mat] = list; order.Add(p.mat); }
                list.Add(new CombineInstance { mesh = p.mesh, transform = p.m });
                verts += p.mesh.vertexCount;
            }
            var subMeshes = new CombineInstance[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var sub = new Mesh();
                if (verts > 65000) sub.indexFormat = IndexFormat.UInt32;
                sub.CombineMeshes(groups[order[i]].ToArray(), true, true);
                subMeshes[i] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { name = name };
            if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(subMeshes, false, false);
            foreach (var s in subMeshes) ZRUtil.SafeDestroy(s.mesh);
            mesh.RecalculateBounds();
            materials = order.ToArray();
            return mesh;
        }
    }

    /// <summary>Merges all static child renderers of a root into one renderer per material (chunk-level batching).</summary>
    public static class MeshMerge
    {
        public static GameObject MergeChildren(Transform root, string name, bool destroySources = true, bool castShadows = true)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            var toKill = new List<GameObject>();
            var w2l = root.worldToLocalMatrix;
            int verts = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<NoMerge>() != null || mf.GetComponentInParent<NoMerge>() != null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;
                    if (!groups.TryGetValue(mats[s], out var list)) { list = new List<CombineInstance>(); groups[mats[s]] = list; order.Add(mats[s]); }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = w2l * mf.transform.localToWorldMatrix });
                }
                verts += mesh.vertexCount;
                toKill.Add(mf.gameObject);
            }
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            if (order.Count == 0) return go;

            var subs = new CombineInstance[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
                sub.CombineMeshes(groups[order[i]].ToArray(), true, true);
                subs[i] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            var merged = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(subs, false, false);
            foreach (var s in subs) ZRUtil.SafeDestroy(s.mesh);
            merged.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = merged;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = order.ToArray();
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (destroySources)
                foreach (var g in toKill)
                {
                    if (g == root.gameObject) continue;
                    // Keep objects that hold other components (lights, particles, scripts); just strip their mesh.
                    if (g.GetComponents<Component>().Length > 3 || g.transform.childCount > 0)
                    {
                        ZRUtil.SafeDestroy(g.GetComponent<MeshRenderer>());
                        ZRUtil.SafeDestroy(g.GetComponent<MeshFilter>());
                    }
                    else ZRUtil.SafeDestroy(g);
                }
            return go;
        }
    }

    /// <summary>Tag component: excludes a renderer (and its children) from chunk mesh merging.</summary>
    public class NoMerge : MonoBehaviour { }
}
