using UnityEngine;

namespace ZombieRunner
{
    public static class ZRUtil
    {
        public const float LaneWidth = 2.6f;
        public static float LaneX(int lane) => lane * LaneWidth;

        /// <summary>Destroy that also works in Edit Mode (used when the setup tool exports procedural prefabs).</summary>
        public static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        public static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static float Damp(float current, float target, float lambda, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt) => Vector3.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));

        public static bool IsMobile => Application.isMobilePlatform;
    }
}
