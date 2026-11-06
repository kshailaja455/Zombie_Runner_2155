using UnityEngine;

namespace ZombieRunner
{
    /// <summary>A thing on the track the player can collide with. Zombies carry one too (type Zombie).</summary>
    public class Obstacle : MonoBehaviour, IPoolable
    {
        public ObstacleType type;
        public string displayName = "obstacle";
        public float length = 1f;
        public bool hit;
        public bool smashed;
        public BoxCollider box;
        public Transform visual;

        Vector3 flingVel, flingSpin;
        float flingT;
        Vector3 visualPos; Quaternion visualRot; Vector3 visualScale;

        public static Obstacle Setup(GameObject go, ObstacleType type, string displayName, Transform visual)
        {
            var o = go.GetOrAdd<Obstacle>();
            o.type = type;
            o.displayName = displayName;
            o.visual = visual;
            o.visualPos = visual.localPosition; o.visualRot = visual.localRotation; o.visualScale = visual.localScale;

            // Gameplay volume per type, independent of the art so any imported model plays the same way.
            var rends = visual.GetComponentsInChildren<Renderer>();
            float depth = 1f;
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                depth = b.size.z;
            }
            o.box = go.GetOrAdd<BoxCollider>();
            o.box.isTrigger = true;
            switch (type)
            {
                case ObstacleType.Jump: depth = Mathf.Clamp(depth, 0.6f, 2.5f); o.box.center = new Vector3(0, 0.45f, 0); o.box.size = new Vector3(2.2f, 0.9f, depth * 0.8f); break;
                case ObstacleType.Slide: depth = Mathf.Clamp(depth, 0.6f, 2f); o.box.center = new Vector3(0, 2.1f, 0); o.box.size = new Vector3(2.4f, 2.0f, depth * 0.8f); break;
                case ObstacleType.Pit: depth = Mathf.Clamp(depth, 1.5f, 3f); o.box.center = new Vector3(0, 0.1f, 0); o.box.size = new Vector3(2.0f, 0.2f, depth * 0.7f); break;
                case ObstacleType.Zombie: depth = 0.7f; o.box.center = new Vector3(0, 0.9f, 0); o.box.size = new Vector3(0.9f, 1.8f, 0.7f); break;
                default: depth = Mathf.Clamp(depth, 0.8f, 11f); o.box.center = new Vector3(0, 1.5f, 0); o.box.size = new Vector3(2.1f, 3f, depth * 0.92f); break;
            }
            o.length = depth;
            return o;
        }

        public virtual void OnSpawned()
        {
            hit = false; smashed = false; flingT = 0f;
            if (box) box.enabled = true;
            if (visual) { visual.localPosition = visualPos; visual.localRotation = visualRot; visual.localScale = visualScale; visual.gameObject.SetActive(true); }
        }

        public virtual void OnDespawned() { }

        /// <summary>Knocks the obstacle away (shield, speed boost, health protection).</summary>
        public virtual void Smash(Vector3 from)
        {
            if (smashed) return;
            smashed = true; hit = true;
            if (box) box.enabled = false;
            var dir = (transform.position - from); dir.y = 0;
            float side = Mathf.Abs(dir.x) < 0.1f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dir.x);
            flingVel = new Vector3(side * Random.Range(5f, 9f), Random.Range(6f, 10f), Random.Range(8f, 16f));
            flingSpin = Random.onUnitSphere * Random.Range(200f, 500f);
            flingT = 1.4f;
        }

        protected virtual void Update()
        {
            if (flingT <= 0f || visual == null) return;
            float dt = Time.deltaTime;
            flingT -= dt;
            flingVel += Physics.gravity * dt;
            visual.position += flingVel * dt;
            visual.Rotate(flingSpin * dt, Space.World);
            if (flingT < 0.4f) visual.localScale = visualScale * Mathf.Max(0.01f, flingT / 0.4f);
            if (flingT <= 0f) visual.gameObject.SetActive(false);
        }
    }

    public enum PowerUpType { Magnet = 0, SpeedBoost = 1, Shield = 2, DoubleCoins = 3, HealthProtection = 4 }

    public class Coin : MonoBehaviour, IPoolable
    {
        public bool collected;
        public bool attracted;
        public void OnSpawned() { collected = false; attracted = false; }
        public void OnDespawned() { }
    }

    public class PowerUpPickup : MonoBehaviour, IPoolable
    {
        public PowerUpType type;
        public bool collected;
        public void OnSpawned() { collected = false; }
        public void OnDespawned() { }
    }

    /// <summary>Builds the coin and the five power-up collectibles (each its own model, colour and glow).</summary>
    public static class PickupFactory
    {
        public static readonly string[] Names = { "Coin Magnet", "Speed Boost", "Shield", "Double Coins", "Health Protection" };
        public static readonly Color[] Colors =
        {
            new Color(0.95f, 0.25f, 0.2f), new Color(0.2f, 0.85f, 1f), new Color(0.35f, 0.6f, 1f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 1f, 0.45f)
        };

        public static GameObject Coin()
        {
            var root = new GameObject("Coin");
            var b = new PropBuilder();
            b.Cyl(Vector3.zero, 0.32f, 0.07f, Palette.Gold, new Vector3(90, 0, 0), 20);
            b.Cyl(new Vector3(0, 0, -0.04f), 0.2f, 0.02f, MaterialLibrary.Lit("goldinner", new Color(0.85f, 0.6f, 0.15f), 0.9f, 1f, null, 1f, new Color(0.3f, 0.18f, 0.02f)), new Vector3(90, 0, 0), 16);
            b.Cyl(new Vector3(0, 0, 0.04f), 0.2f, 0.02f, MaterialLibrary.Lit("goldinner", new Color(0.85f, 0.6f, 0.15f), 0.9f, 1f, null, 1f, new Color(0.3f, 0.18f, 0.02f)), new Vector3(90, 0, 0), 16);
            var vis = b.Build("CoinMesh", root.transform, false);
            vis.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 220, 0);
            root.AddComponent<Coin>();
            return root;
        }

        public static GameObject PowerUp(PowerUpType t)
        {
            var root = new GameObject("PowerUp_" + t);
            var vis = BuildModel(t, root.transform);
            var spin = vis.AddComponent<Spinner>();
            spin.degreesPerSecond = new Vector3(0, 90, 0);
            spin.bobAmplitude = 0.15f;
            // Glow halo
            var halo = new PropBuilder().Sphere(Vector3.zero, Vector3.one * 1.3f, MaterialLibrary.Transparent("halo" + t, new Color(Colors[(int)t].r, Colors[(int)t].g, Colors[(int)t].b, 0.12f), 0.9f, Colors[(int)t] * 0.6f)).Build("Halo", root.transform, false);
            halo.transform.localPosition = Vector3.zero;
            var sparkle = FX.Motes(root.transform, Colors[(int)t], 0.12f, -0.4f, 20, true);
            sparkle.transform.localPosition = Vector3.zero;
            var sh = sparkle.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f; sh.scale = Vector3.one;
            var main = sparkle.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startLifetime = 1.2f;
            var p = root.AddComponent<PowerUpPickup>();
            p.type = t;
            return root;
        }

        /// <summary>The model on its own (also used for the HUD and menu).</summary>
        public static GameObject BuildModel(PowerUpType t, Transform parent)
        {
            var b = new PropBuilder();
            var col = Colors[(int)t];
            var glow = MaterialLibrary.Emissive("pu" + t, col, 2.5f);
            switch (t)
            {
                case PowerUpType.Magnet:
                    b.Torus(new Vector3(0, 0.12f, 0), 0.32f, 0.11f, MaterialLibrary.Lit("magnetred", new Color(0.8f, 0.1f, 0.08f), 0.6f, 0.3f), new Vector3(90, 0, 0));
                    b.Box(new Vector3(-0.32f, -0.2f, 0), new Vector3(0.22f, 0.42f, 0.22f), MaterialLibrary.Lit("magnetred", new Color(0.8f, 0.1f, 0.08f), 0.6f, 0.3f));
                    b.Box(new Vector3(0.32f, -0.2f, 0), new Vector3(0.22f, 0.42f, 0.22f), MaterialLibrary.Lit("magnetred", new Color(0.8f, 0.1f, 0.08f), 0.6f, 0.3f));
                    b.Box(new Vector3(-0.32f, -0.48f, 0), new Vector3(0.23f, 0.16f, 0.23f), Palette.Steel);
                    b.Box(new Vector3(0.32f, -0.48f, 0), new Vector3(0.23f, 0.16f, 0.23f), Palette.Steel);
                    break;
                case PowerUpType.SpeedBoost:
                    // energy-drink style can with a lightning bolt
                    b.Cyl(Vector3.zero, 0.22f, 0.7f, MaterialLibrary.Lit("can", new Color(0.1f, 0.15f, 0.2f), 0.8f, 0.9f), default, 16);
                    b.Cyl(new Vector3(0, 0.36f, 0), 0.19f, 0.03f, Palette.Steel, default, 16);
                    b.Box(new Vector3(0.02f, 0.1f, 0.21f), new Vector3(0.08f, 0.26f, 0.04f), glow, new Vector3(0, 0, -25));
                    b.Box(new Vector3(-0.02f, -0.1f, 0.21f), new Vector3(0.08f, 0.26f, 0.04f), glow, new Vector3(0, 0, -25));
                    b.Box(new Vector3(0, 0, 0.21f), new Vector3(0.18f, 0.06f, 0.04f), glow);
                    break;
                case PowerUpType.Shield:
                    b.Cyl(Vector3.zero, 0.42f, 0.1f, MaterialLibrary.Lit("shieldplate", new Color(0.25f, 0.35f, 0.55f), 0.7f, 0.8f), new Vector3(90, 0, 0), 6);
                    b.Torus(Vector3.zero, 0.42f, 0.04f, glow, new Vector3(90, 0, 0));
                    b.Cyl(new Vector3(0, 0, 0.06f), 0.16f, 0.04f, glow, new Vector3(90, 0, 0), 6);
                    break;
                case PowerUpType.DoubleCoins:
                    b.Cyl(new Vector3(-0.14f, 0.05f, 0), 0.3f, 0.07f, Palette.Gold, new Vector3(90, 0, 0), 20);
                    b.Cyl(new Vector3(0.14f, -0.05f, 0.08f), 0.3f, 0.07f, Palette.Gold, new Vector3(90, 0, 0), 20);
                    b.Box(new Vector3(0.14f, -0.05f, 0.13f), new Vector3(0.05f, 0.25f, 0.02f), glow, new Vector3(0, 0, 35)); // "x" mark
                    b.Box(new Vector3(0.14f, -0.05f, 0.13f), new Vector3(0.05f, 0.25f, 0.02f), glow, new Vector3(0, 0, -35));
                    break;
                case PowerUpType.HealthProtection:
                    b.Box(Vector3.zero, new Vector3(0.6f, 0.45f, 0.28f), MaterialLibrary.Lit("medkit", new Color(0.85f, 0.85f, 0.82f), 0.5f, 0.1f));
                    b.Box(new Vector3(0, 0, 0.145f), new Vector3(0.32f, 0.1f, 0.02f), glow);
                    b.Box(new Vector3(0, 0, 0.145f), new Vector3(0.1f, 0.32f, 0.02f), glow);
                    b.Box(new Vector3(0, 0.27f, 0), new Vector3(0.24f, 0.06f, 0.08f), Palette.DarkSteel);
                    break;
            }
            var go = b.Build(t + "Model", parent, false);
            return go;
        }
    }
}
