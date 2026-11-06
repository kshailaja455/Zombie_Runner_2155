using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    /// <summary>Particle effects built in code (fire, smoke, rain, sandstorm, fog, pickups, impacts).</summary>
    public static class FX
    {
        /// <summary>Global particle multiplier, set by QualityManager.</summary>
        public static float Density = 1f;

        static ParticleSystem Create(string name, Transform parent, Vector3 localPos, Material mat, out ParticleSystem.MainModule main)
        {
            var go = new GameObject(name);
            go.AddComponent<NoMerge>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 5f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void Grad(ParticleSystem ps, Color a, Color b, float fadeIn = 0.15f, float fadeOut = 0.7f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOut), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * Density));

        public static ParticleSystem Fire(Transform parent, Vector3 pos, float scale)
        {
            var ps = Create("Fire", parent, pos, MaterialLibrary.Particle("fire", true), out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 2.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.1f * scale);
            main.startColor = new Color(1f, 0.55f, 0.2f, 0.9f);
            main.maxParticles = N(80);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = N(45);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f * scale;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            Grad(ps, new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.2f, 0.05f), 0.05f, 0.5f);
            ps.Play();
            Smoke(parent, pos + Vector3.up * 1.2f * scale, scale * 1.2f, new Color(0.12f, 0.11f, 0.1f, 0.6f));
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = pos + Vector3.up * 0.8f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 9f * scale; l.intensity = 3f;
            l.shadows = LightShadows.None;
            var f = lightGo.AddComponent<FlickerLight>(); f.baseIntensity = 3f; f.amount = 0.45f; f.speed = 9f;
            return ps;
        }

        public static ParticleSystem Smoke(Transform parent, Vector3 pos, float scale, Color color, float rate = 10f, float life = 4f)
        {
            var ps = Create("Smoke", parent, pos, MaterialLibrary.Particle("smoke", false, ProceduralTextures.Smoke()), out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.5f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = N(60);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = N(rate);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.4f * scale;
            sh.rotation = new Vector3(-90, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Grad(ps, Color.white, Color.white, 0.1f, 0.6f);
            ps.Play();
            return ps;
        }

        /// <summary>Tall smoke column for distant burning buildings.</summary>
        public static ParticleSystem SmokeColumn(Transform parent, Vector3 pos, float scale)
        {
            var ps = Smoke(parent, pos, scale * 4f, new Color(0.18f, 0.17f, 0.16f, 0.7f), 6f, 12f);
            var main = ps.main; main.maxParticles = N(90);
            return ps;
        }

        public static ParticleSystem Rain(Transform parent)
        {
            var ps = Create("Rain", parent, new Vector3(0, 12f, 12f), MaterialLibrary.Particle("rain", false), out var main);
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 0.025f; main.startSizeY = 0.7f; main.startSizeZ = 0.025f;
            main.startColor = new Color(0.7f, 0.78f, 0.9f, 0.35f);
            main.maxParticles = N(2500);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = N(1800);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 1f, 50f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-1f, -1f); vel.y = new ParticleSystem.MinMaxCurve(-16f, -16f); vel.z = new ParticleSystem.MinMaxCurve(-2f, -2f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f; r.lengthScale = 1f;
            ps.Play();
            return ps;
        }

        public static ParticleSystem Sandstorm(Transform parent)
        {
            var ps = Create("Sandstorm", parent, new Vector3(0, 3f, 20f), MaterialLibrary.Particle("sandstorm", false, ProceduralTextures.Smoke()), out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.82f, 0.66f, 0.46f, 0.22f);
            main.maxParticles = N(220);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = N(50);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70f, 6f, 90f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-12f, -7f); vel.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.8f); vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            Grad(ps, Color.white, Color.white, 0.25f, 0.7f);
            ps.Play();
            // fine grit streaks
            var grit = Create("SandGrit", parent, new Vector3(0, 1.5f, 15f), MaterialLibrary.Particle("grit", false), out var gm);
            gm.startLifetime = 1.2f; gm.startSpeed = 0f; gm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            gm.startColor = new Color(0.9f, 0.75f, 0.5f, 0.6f); gm.maxParticles = N(900);
            gm.simulationSpace = ParticleSystemSimulationSpace.World;
            var gem = grit.emission; gem.rateOverTime = N(600);
            var gsh = grit.shape; gsh.shapeType = ParticleSystemShapeType.Box; gsh.scale = new Vector3(30f, 3f, 40f);
            var gv = grit.velocityOverLifetime; gv.enabled = true; gv.space = ParticleSystemSimulationSpace.World;
            gv.x = new ParticleSystem.MinMaxCurve(-22f, -14f); gv.y = new ParticleSystem.MinMaxCurve(-1f, 1f); gv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var gr = grit.GetComponent<ParticleSystemRenderer>(); gr.renderMode = ParticleSystemRenderMode.Stretch; gr.velocityScale = 0.03f;
            grit.Play();
            return ps;
        }

        public static ParticleSystem GroundFog(Transform parent, Color color, float height = 1.2f, float alpha = 0.18f)
        {
            var ps = Create("GroundFog", parent, new Vector3(0, height, 25f), MaterialLibrary.Particle("fog", false, ProceduralTextures.Smoke()), out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            color.a = alpha;
            main.startColor = color;
            main.maxParticles = N(120);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = N(14);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(50f, 1.5f, 90f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0.1f); vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            Grad(ps, Color.white, Color.white, 0.3f, 0.7f);
            ps.Play();
            return ps;
        }

        /// <summary>Floating motes / falling leaves / ash.</summary>
        public static ParticleSystem Motes(Transform parent, Color color, float size, float fall, int count, bool additive)
        {
            var ps = Create("Motes", parent, new Vector3(0, 4f, 15f), MaterialLibrary.Particle(additive ? "motesadd" : "motes", additive), out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.maxParticles = N(count);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = N(count / 5f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(24f, 8f, 40f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f); vel.y = new ParticleSystem.MinMaxCurve(-fall, -fall * 0.3f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f;
            Grad(ps, Color.white, Color.white, 0.2f, 0.8f);
            ps.Play();
            return ps;
        }

        /// <summary>One-shot burst (coin pickup, power-up, obstacle smash). Reusable: call Emit on the returned system.</summary>
        public static ParticleSystem Burst(Transform parent, Color color, bool additive, float speed, float size, float life)
        {
            var ps = Create("Burst", parent, Vector3.zero, MaterialLibrary.Particle(additive ? "burstadd" : "burst", additive), out var main);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.maxParticles = 200;
            main.gravityModifier = additive ? 0.2f : 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            return ps;
        }

        /// <summary>Volumetric-looking light shaft (additive cone). URP has no built-in volumetrics, so this is a cheap fake.</summary>
        public static GameObject LightShaft(Transform parent, Vector3 pos, Vector3 sunEuler, float length, float radius, Color color)
        {
            var go = new GameObject("LightShaft");
            go.AddComponent<NoMerge>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // Shaft points along the sun direction (from sky toward ground).
            go.transform.rotation = Quaternion.Euler(sunEuler) * Quaternion.Euler(90, 0, 0);
            var mesh = Object.Instantiate(MeshGen.Cylinder(radius * 0.6f, radius, length, 12, false));
            var cols = new Color[mesh.vertexCount];
            var verts = mesh.vertices;
            for (int i = 0; i < cols.Length; i++)
            {
                float t = Mathf.InverseLerp(-length * 0.5f, length * 0.5f, verts[i].y);
                cols[i] = new Color(1, 1, 1, Mathf.Sin(t * Mathf.PI));
            }
            mesh.colors = cols;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(MaterialLibrary.Particle("shaft", true));
            MaterialLibrary.SetColor(mat, color);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }

    /// <summary>Flickers a light (and optionally toggles emissive renderers) for neon, fire, broken lamps.</summary>
    public class FlickerLight : MonoBehaviour
    {
        public float baseIntensity = 2f;
        public float amount = 0.3f;
        public float speed = 6f;
        public bool hardFlicker;
        public Renderer[] toggles;
        Light l;
        float seed;
        bool on = true;
        float nextToggle;

        void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; }

        void Update()
        {
            float t = Time.time * speed + seed;
            if (hardFlicker)
            {
                if (Time.time > nextToggle)
                {
                    on = !on || Random.value < 0.3f;
                    nextToggle = Time.time + (on ? Random.Range(0.05f, 1.6f) : Random.Range(0.03f, 0.25f));
                    if (toggles != null) foreach (var r in toggles) if (r) r.enabled = on;
                }
                if (l) l.intensity = on ? baseIntensity : 0f;
            }
            else if (l)
            {
                l.intensity = baseIntensity * (1f - amount + amount * Mathf.PerlinNoise(t, seed));
            }
        }
    }

    /// <summary>Simple rotation for coins, power-ups and menu props.</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0, 120, 0);
        public float bobAmplitude;
        public float bobSpeed = 2f;
        Vector3 basePos;
        void OnEnable() { basePos = transform.localPosition; }
        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (bobAmplitude > 0f) transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        }
    }
}
