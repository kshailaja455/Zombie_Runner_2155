using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// Spawns rows of obstacles, zombies, coins and power-ups ahead of the player from object pools,
    /// and recycles them behind. Every row leaves at least one lane the player can pass, and each row
    /// is spaced so the player can always reach it from the previous row's free lane.
    /// </summary>
    public class TrackSpawner : MonoBehaviour
    {
        public float spawnAhead = 170f;
        public float firstRowZ = 55f;

        GameManager gm;
        ThemeProfile theme;
        ObjectPool pool;
        Transform player;
        float nextRowZ;
        float lastPowerUpZ = -999f;
        readonly List<GameObject> live = new List<GameObject>();
        readonly List<Coin> coins = new List<Coin>();
        readonly List<PowerUpPickup> powerUps = new List<PowerUpPickup>();
        readonly List<string> obstacleKeys = new List<string>();
        readonly List<ObstacleType> obstacleTypes = new List<ObstacleType>();
        readonly List<float> obstacleWeights = new List<float>();
        bool[] prevBlocked = new bool[3];
        float prevRowZ;
        ParticleSystem coinBurst, smashBurst, powerBurst;
        int zombieVariants = 6;

        public void Init(GameManager manager, ThemeProfile profile, Transform playerT)
        {
            gm = manager; theme = profile; player = playerT;
            var poolRoot = ZRUtil.Child(transform, "Pool");
            pool = new ObjectPool(poolRoot);
            nextRowZ = firstRowZ;
            prevRowZ = 0f;

            // Theme obstacles (procedural)
            int seed = profile.musicSeed * 77;
            foreach (var def in profile.obstacles)
            {
                for (int v = 0; v < 2; v++)
                {
                    string key = def.kind + "_" + v;
                    int s = seed + v * 13 + (int)def.kind * 101;
                    var d = def;
                    pool.Register(key, () => BuildObstacle(d.kind, d.type, s), 1);
                    obstacleKeys.Add(key); obstacleTypes.Add(def.type); obstacleWeights.Add(def.weight * 0.5f);
                }
            }
            // Imported obstacles (optional)
            var reg = ZRAssetRegistry.Instance;
            var set = reg != null ? reg.Theme(profile.id) : null;
            if (set != null)
            {
                AddImported(set.jumpObstacles, ObstacleType.Jump);
                AddImported(set.slideObstacles, ObstacleType.Slide);
                AddImported(set.blockObstacles, ObstacleType.Block);
            }
            // Zombies
            foreach (ZombieKind k in new[] { ZombieKind.Walker, ZombieKind.Runner, ZombieKind.Brute, ZombieKind.Emerger })
                for (int v = 0; v < zombieVariants; v++)
                {
                    var kk = k; int s = seed + v * 31 + (int)k * 7;
                    pool.Register("Z_" + k + "_" + v, () => { var z = ZombieAgent.Build(theme, kk, s); z.GetComponent<ZombieAgent>().Init(player); return z; }, v < 2 ? 1 : 0);
                }
            pool.Register("Coin", PickupFactory.Coin, 40);
            for (int i = 0; i < 5; i++)
            {
                var t = (PowerUpType)i;
                pool.Register("PU_" + t, () => PickupFactory.PowerUp(t), 1);
            }
            coinBurst = FX.Burst(transform, new Color(1f, 0.85f, 0.3f), true, 3f, 0.15f, 0.4f);
            smashBurst = FX.Burst(transform, new Color(0.45f, 0.42f, 0.38f), false, 7f, 0.35f, 1f);
            powerBurst = FX.Burst(transform, Color.white, true, 5f, 0.25f, 0.7f);
        }

        void AddImported(List<GameObject> prefabs, ObstacleType type)
        {
            if (prefabs == null) return;
            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;
                string key = "IMP_" + type + "_" + i;
                pool.Register(key, () => WrapImported(prefab, type), 0);
                obstacleKeys.Add(key); obstacleTypes.Add(type); obstacleWeights.Add(1.5f);
            }
        }

        GameObject BuildObstacle(PropKind kind, ObstacleType type, int seed)
        {
            var root = new GameObject("Obstacle_" + kind);
            var vis = PropFactory.Obstacle(kind, seed);
            vis.transform.SetParent(root.transform, false);
            if (kind == PropKind.BurningCar) FX.Fire(root.transform, new Vector3(0, 1.0f, 0.8f), 0.8f);
            if (kind == PropKind.SandPit)
            {
                var swirl = FX.Smoke(root.transform, new Vector3(0, 0.05f, 0), 0.6f, new Color(0.78f, 0.62f, 0.42f, 0.35f), 6f, 2f);
                var vel = swirl.velocityOverLifetime; vel.orbitalY = new ParticleSystem.MinMaxCurve(2f);
            }
            if (kind == PropKind.PoliceRoadblock && QualityManager.AllowExtraLights)
            {
                var lg = new GameObject("Beacon"); lg.transform.SetParent(root.transform, false); lg.transform.localPosition = new Vector3(0, 1.4f, -0.5f);
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 7f; l.color = new Color(1f, 0.2f, 0.2f); l.shadows = LightShadows.None;
                var f = lg.AddComponent<FlickerLight>(); f.baseIntensity = 3f; f.hardFlicker = true;
            }
            Obstacle.Setup(root, type, Pretty(kind), vis.transform);
            return root;
        }

        GameObject WrapImported(GameObject prefab, ObstacleType type)
        {
            var root = new GameObject("Imported_" + prefab.name);
            var inst = Instantiate(prefab, root.transform);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
            // Fit the model into the lane and the height for its type.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                float targetH = type == ObstacleType.Jump ? 0.9f : type == ObstacleType.Block ? 2.4f : 0.6f;
                float s = Mathf.Min(2.3f / Mathf.Max(0.01f, b.size.x), targetH / Mathf.Max(0.01f, b.size.y));
                if (type == ObstacleType.Block) s = Mathf.Min(2.3f / Mathf.Max(0.01f, b.size.x), 3.2f / Mathf.Max(0.01f, b.size.y));
                inst.transform.localScale *= s;
                b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                inst.transform.localPosition -= new Vector3(b.center.x, b.min.y - (type == ObstacleType.Slide ? 1.3f : 0f), b.center.z);
            }
            Obstacle.Setup(root, type, prefab.name, inst.transform);
            return root;
        }

        static string Pretty(PropKind k)
        {
            var s = k.ToString();
            var sb = new System.Text.StringBuilder("a ");
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(char.ToLowerInvariant(s[i]));
            }
            return sb.ToString();
        }

        public void Shift(float dz)
        {
            foreach (var go in live) if (go) go.transform.position += new Vector3(0, 0, dz);
            nextRowZ += dz; prevRowZ += dz; lastPowerUpZ += dz;
            foreach (var go in live)
            {
                var z = go ? go.GetComponent<ZombieAgent>() : null;
                if (z != null && z.minZ > float.MinValue / 2) z.minZ += dz;
            }
        }

        void Update()
        {
            if (player == null || !gm.IsRunning) return;
            float pz = player.position.z;
            while (nextRowZ < pz + spawnAhead) SpawnRow();
            Recycle(pz);
            Pickups();
        }

        // ------------------------------------------------------------------ ROWS
        void SpawnRow()
        {
            float diff = gm.Difficulty.Level01;
            float z = nextRowZ;
            var blocked = new bool[3];     // lane cannot be passed at all
            var obstacleIn = new ObstacleType?[3];
            float rowLength = 1f;

            float roll = Random.value;
            float zombieChance = Mathf.Lerp(0.16f, 0.3f, diff) * theme.zombieDensity;
            if (roll < zombieChance) SpawnZombies(z, diff, blocked, obstacleIn);
            else if (roll < zombieChance + 0.08f) { /* breather row: coins only */ }
            else
            {
                int count = Random.value < Mathf.Lerp(0.25f, 0.6f, diff) ? 2 : 1;
                if (diff > 0.35f && Random.value < (diff - 0.35f) * 0.6f) count = 3;
                var lanes = Shuffled();
                for (int i = 0; i < count; i++)
                {
                    int lane = lanes[i];
                    bool mustBePassable = (i == 2) || (count == 3 && i == 1 && Random.value < 0.5f);
                    int idx = PickObstacle(mustBePassable);
                    if (idx < 0) continue;
                    var go = pool.Spawn(obstacleKeys[idx], new Vector3(ZRUtil.LaneX(lane), 0, z), Quaternion.identity);
                    if (go == null) continue;
                    live.Add(go);
                    var type = obstacleTypes[idx];
                    obstacleIn[lane + 1] = type;
                    if (type == ObstacleType.Block) blocked[lane + 1] = true;
                    rowLength = Mathf.Max(rowLength, go.GetComponent<Obstacle>().length);
                }
                // never block all three lanes
                if (blocked[0] && blocked[1] && blocked[2]) Debug.LogWarning("[ZombieRunner] Row fully blocked, this should not happen.");
            }

            SpawnCoins(z, blocked, obstacleIn);
            TrySpawnPowerUp(z, blocked, obstacleIn);

            prevBlocked = blocked;
            prevRowZ = z;
            float spacing = Mathf.Lerp(30f, 15f, diff) * Random.Range(0.85f, 1.15f);
            nextRowZ = z + rowLength + spacing;
        }

        int[] Shuffled()
        {
            var a = new[] { -1, 0, 1 };
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }
            return a;
        }

        int PickObstacle(bool passableOnly)
        {
            float total = 0f;
            for (int i = 0; i < obstacleKeys.Count; i++) if (!passableOnly || obstacleTypes[i] != ObstacleType.Block) total += obstacleWeights[i];
            if (total <= 0f) return -1;
            float r = Random.value * total;
            for (int i = 0; i < obstacleKeys.Count; i++)
            {
                if (passableOnly && obstacleTypes[i] == ObstacleType.Block) continue;
                r -= obstacleWeights[i];
                if (r <= 0f) return i;
            }
            return obstacleKeys.Count - 1;
        }

        void SpawnZombies(float z, float diff, bool[] blocked, ObstacleType?[] obstacleIn)
        {
            var lanes = Shuffled();
            float pattern = Random.value;
            if (pattern < 0.3f)
            {
                // a group: 2 walkers (3rd lane stays free)
                for (int i = 0; i < 2; i++) Zombie(ZombieKind.Walker, lanes[i], z + Random.Range(-1f, 1.5f), Random.Range(0.6f, 1.3f), blocked, obstacleIn);
            }
            else if (pattern < 0.5f + diff * 0.1f)
            {
                // runner charging down a lane that was open in the previous row
                int lane = lanes[0];
                for (int i = 0; i < 3; i++) if (!prevBlocked[lanes[i] + 1]) { lane = lanes[i]; break; }
                Zombie(ZombieKind.Runner, lane, z + 25f, Mathf.Lerp(3.5f, 6.5f, diff), blocked, obstacleIn);
            }
            else if (pattern < 0.65f && diff > 0.2f)
            {
                Zombie(ZombieKind.Brute, lanes[0], z, 0.6f, blocked, obstacleIn);
            }
            else
            {
                // emerges from the ground (nature) or steps out from the roadside (city)
                Zombie(ZombieKind.Emerger, lanes[0], z, 0.9f, blocked, obstacleIn);
                if (diff > 0.4f) Zombie(ZombieKind.Walker, lanes[1], z + 2f, 1f, blocked, obstacleIn);
            }
        }

        void Zombie(ZombieKind kind, int lane, float z, float speed, bool[] blocked, ObstacleType?[] obstacleIn)
        {
            string key = "Z_" + kind + "_" + Random.Range(0, zombieVariants);
            var go = pool.Spawn(key, new Vector3(ZRUtil.LaneX(lane), 0, z), Quaternion.identity);
            if (go == null) return;
            live.Add(go);
            var agent = go.GetComponent<ZombieAgent>();
            bool city = theme.id == ThemeId.DarkCity || theme.id == ThemeId.AbandonedCity;
            agent.Activate(kind, lane, speed, prevRowZ + 6f, kind == ZombieKind.Emerger && (city || Random.value < 0.3f));
            blocked[lane + 1] = true;
            obstacleIn[lane + 1] = ObstacleType.Zombie;
        }

        void SpawnCoins(float z, bool[] blocked, ObstacleType?[] obstacleIn)
        {
            if (Random.value < 0.35f) return;
            var free = new List<int>();
            for (int l = -1; l <= 1; l++) if (!blocked[l + 1]) free.Add(l);
            if (free.Count == 0) return;
            int lane = free[Random.Range(0, free.Count)];
            var type = obstacleIn[lane + 1];
            int n = Random.Range(5, 10);
            float start = z - n * 2f + 1f;
            for (int i = 0; i < n; i++)
            {
                float cz = start + i * 2f;
                float cy = 0.9f;
                if (type == ObstacleType.Jump || type == ObstacleType.Pit)
                {
                    float t = Mathf.InverseLerp(start, z + 6f, cz);
                    cy = 0.9f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.4f;
                }
                else if (type == ObstacleType.Slide) cy = 0.5f;
                var go = pool.Spawn("Coin", new Vector3(ZRUtil.LaneX(lane), cy, cz), Quaternion.identity);
                if (go == null) continue;
                live.Add(go);
                coins.Add(go.GetComponent<Coin>());
            }
        }

        void TrySpawnPowerUp(float z, bool[] blocked, ObstacleType?[] obstacleIn)
        {
            if (z - lastPowerUpZ < 180f || Random.value > 0.22f) return;
            var free = new List<int>();
            for (int l = -1; l <= 1; l++) if (!blocked[l + 1] && obstacleIn[l + 1] == null) free.Add(l);
            if (free.Count == 0) return;
            int lane = free[Random.Range(0, free.Count)];
            var t = (PowerUpType)Random.Range(0, 5);
            if (t == PowerUpType.HealthProtection && gm.PowerUps.IsActive(PowerUpType.HealthProtection)) t = PowerUpType.Magnet;
            var go = pool.Spawn("PU_" + t, new Vector3(ZRUtil.LaneX(lane), 1.1f, z - 6f), Quaternion.identity);
            if (go == null) return;
            live.Add(go);
            powerUps.Add(go.GetComponent<PowerUpPickup>());
            lastPowerUpZ = z;
        }

        // ------------------------------------------------------------------ PICKUPS
        void Pickups()
        {
            var pc = gm.Player;
            if (!pc.Alive) return;
            Vector3 center = pc.transform.position + Vector3.up * (pc.Sliding ? 0.5f : 1.0f);
            float magnet = gm.PowerUps.MagnetRadius;
            float dt = Time.deltaTime;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                if (c == null || !c.gameObject.activeSelf || c.collected) { coins.RemoveAt(i); continue; }
                var d = center - c.transform.position;
                float dist = d.magnitude;
                if (magnet > 0f && dist < magnet) c.attracted = true;
                if (c.attracted) c.transform.position += d.normalized * Mathf.Min(dist, (pc.Speed + 18f) * dt);
                if (dist < 1.15f)
                {
                    c.collected = true;
                    coinBurst.transform.position = c.transform.position;
                    coinBurst.Emit(10);
                    gm.CollectCoin();
                    pool.Despawn(c.gameObject);
                    live.Remove(c.gameObject);
                    coins.RemoveAt(i);
                }
            }
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                var p = powerUps[i];
                if (p == null || !p.gameObject.activeSelf || p.collected) { powerUps.RemoveAt(i); continue; }
                if ((center - p.transform.position).magnitude < 1.4f)
                {
                    p.collected = true;
                    var main = powerBurst.main; main.startColor = PickupFactory.Colors[(int)p.type];
                    powerBurst.transform.position = p.transform.position;
                    powerBurst.Emit(40);
                    gm.CollectPowerUp(p.type);
                    pool.Despawn(p.gameObject);
                    live.Remove(p.gameObject);
                    powerUps.RemoveAt(i);
                }
            }
        }

        public void SmashFx(Vector3 pos)
        {
            smashBurst.transform.position = pos + Vector3.up;
            smashBurst.Emit(35);
        }

        void Recycle(float pz)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var go = live[i];
                if (go == null) { live.RemoveAt(i); continue; }
                var o = go.GetComponent<Obstacle>();
                float len = o != null ? o.length : 1f;
                if (go.transform.position.z + len < pz - 14f)
                {
                    pool.Despawn(go);
                    live.RemoveAt(i);
                }
            }
        }
    }
}
