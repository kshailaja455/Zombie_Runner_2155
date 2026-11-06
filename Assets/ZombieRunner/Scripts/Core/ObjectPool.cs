using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    /// <summary>Marks a pooled instance so it can find its way home.</summary>
    public class PooledObject : MonoBehaviour
    {
        public string poolKey;
        public bool inUse;
    }

    /// <summary>
    /// Keyed object pool. Each key has a factory that builds a fresh instance on demand.
    /// Spawn/Despawn only toggle activity, so no GameObjects are created or destroyed during a run
    /// once the pools have warmed up.
    /// </summary>
    public class ObjectPool
    {
        readonly Dictionary<string, Func<GameObject>> factories = new Dictionary<string, Func<GameObject>>();
        readonly Dictionary<string, Stack<GameObject>> free = new Dictionary<string, Stack<GameObject>>();
        readonly Transform root;

        public ObjectPool(Transform root) { this.root = root; }

        public bool Has(string key) => factories.ContainsKey(key);

        public void Register(string key, Func<GameObject> factory, int prewarm = 0)
        {
            factories[key] = factory;
            if (!free.ContainsKey(key)) free[key] = new Stack<GameObject>();
            for (int i = 0; i < prewarm; i++) Despawn(Create(key));
        }

        GameObject Create(string key)
        {
            var go = factories[key]();
            var p = go.GetComponent<PooledObject>();
            if (p == null) p = go.AddComponent<PooledObject>();
            p.poolKey = key;
            go.transform.SetParent(root, false);
            return go;
        }

        public GameObject Spawn(string key, Vector3 pos, Quaternion rot)
        {
            if (!factories.ContainsKey(key)) { Debug.LogError("[ZombieRunner] No pool registered for " + key); return null; }
            var stack = free[key];
            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop();
            if (go == null) go = Create(key);
            go.transform.SetPositionAndRotation(pos, rot);
            go.GetComponent<PooledObject>().inUse = true;
            go.SetActive(true);
            foreach (var r in go.GetComponents<IPoolable>()) r.OnSpawned();
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            var p = go.GetComponent<PooledObject>();
            if (p == null) { UnityEngine.Object.Destroy(go); return; }
            foreach (var r in go.GetComponents<IPoolable>()) r.OnDespawned();
            p.inUse = false;
            go.SetActive(false);
            go.transform.SetParent(root, false);
            free[p.poolKey].Push(go);
        }
    }

    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
