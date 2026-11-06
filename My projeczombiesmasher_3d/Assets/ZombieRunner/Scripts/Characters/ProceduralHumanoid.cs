using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    public enum Pose { Idle, Run, Jump, Slide, Stumble, Dead, ZombieWalk, ZombieRun, Attack, Rise, Cheer }

    /// <summary>Look of a procedural humanoid (survivor or zombie).</summary>
    public class HumanoidStyle
    {
        public Color skin = new Color(0.78f, 0.6f, 0.48f);
        public Color shirt = new Color(0.3f, 0.35f, 0.25f);
        public Color pants = new Color(0.25f, 0.24f, 0.22f);
        public Color shoes = new Color(0.12f, 0.1f, 0.09f);
        public Color hair = new Color(0.15f, 0.11f, 0.08f);
        public Color gear = new Color(0.35f, 0.3f, 0.2f);
        public float height = 1.8f;
        public float bulk = 1f;
        public bool zombie;
        public bool cap, backpack, machete, medBag, goggles, wrench, bandana, hood;
        public bool torn;
        public int seed;
    }

    /// <summary>
    /// A jointed humanoid built from capsules with code-driven animation (run, jump, slide, stumble,
    /// death, zombie shamble, sprint, attack, rise from ground). It is the fallback when no rigged
    /// character model has been imported, and needs no Animator or animation clips.
    /// </summary>
    public class ProceduralHumanoid : MonoBehaviour
    {
        public Pose pose = Pose.Idle;
        public float speed = 1f;       // animation speed multiplier
        public float lean;             // -1..1 lane-change lean
        public float poseTime;         // seconds since pose changed

        Transform hips, spine, chest, neck, head;
        Transform thighL, shinL, footL, thighR, shinR, footR;
        Transform upperArmL, foreArmL, upperArmR, foreArmR;
        Transform root;
        float phase;
        float hipsHeight;
        HumanoidStyle style;
        float limp;
        Renderer[] renderers;

        public HumanoidStyle Style => style;

        public static ProceduralHumanoid Create(Transform parent, HumanoidStyle s, string name = "Humanoid")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<ProceduralHumanoid>();
            h.Build(s);
            return h;
        }

        Transform Joint(Transform parent, string n, Vector3 localPos)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        void Part(Transform joint, Mesh mesh, Vector3 localPos, Vector3 euler, Vector3 scale, Material mat)
        {
            var g = new GameObject(joint.name + "_mesh");
            g.transform.SetParent(joint, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = QualityManager.Tier == QualityTier.Low ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        void Build(HumanoidStyle s)
        {
            style = s;
            float k = s.height / 1.8f;
            float b = s.bulk;
            var rng = new Rng(s.seed);
            limp = s.zombie ? rng.Range(0.1f, 0.5f) : 0f;

            string tag = s.zombie ? "z" : "p";
            var skin = MaterialLibrary.Lit($"{tag}skin{ColorUtility.ToHtmlStringRGB(s.skin)}", s.skin, s.zombie ? 0.35f : 0.3f, 0f, TexKind.Skin, 2f);
            var shirt = MaterialLibrary.Lit($"{tag}shirt{ColorUtility.ToHtmlStringRGB(s.shirt)}{s.torn}", s.shirt, 0.15f, 0f, s.torn ? TexKind.Rust : TexKind.Fabric, 2f);
            var pants = MaterialLibrary.Lit($"{tag}pants{ColorUtility.ToHtmlStringRGB(s.pants)}", s.pants, 0.12f, 0f, TexKind.Fabric, 2f);
            var shoes = MaterialLibrary.Lit($"shoes{ColorUtility.ToHtmlStringRGB(s.shoes)}", s.shoes, 0.35f, 0f, TexKind.Fabric, 1f);
            var hair = MaterialLibrary.Lit($"hair{ColorUtility.ToHtmlStringRGB(s.hair)}", s.hair, 0.3f, 0f, TexKind.Fabric, 4f);
            var gear = MaterialLibrary.Lit($"gear{ColorUtility.ToHtmlStringRGB(s.gear)}", s.gear, 0.25f, 0.1f, TexKind.Fabric, 2f);
            var eyes = s.zombie ? MaterialLibrary.Emissive("zeyes", new Color(0.9f, 0.85f, 0.5f), 1.2f) : MaterialLibrary.Lit("eyes", new Color(0.05f, 0.05f, 0.05f), 0.8f);

            root = Joint(transform, "Root", Vector3.zero);
            hipsHeight = 0.95f * k;
            hips = Joint(root, "Hips", new Vector3(0, hipsHeight, 0));
            Part(hips, MeshGen.Capsule(0.15f, 0.36f), Vector3.zero, new Vector3(0, 0, 90), new Vector3(1f * b, 1f, 0.8f * b), pants);

            spine = Joint(hips, "Spine", new Vector3(0, 0.08f * k, 0));
            chest = Joint(spine, "Chest", new Vector3(0, 0.22f * k, 0));
            Part(spine, MeshGen.Capsule(0.15f, 0.36f), new Vector3(0, 0.12f * k, 0), Vector3.zero, new Vector3(1.05f * b, k, 0.75f * b), shirt);
            Part(chest, MeshGen.Capsule(0.19f, 0.42f), new Vector3(0, 0.1f * k, 0), new Vector3(0, 0, 90), new Vector3(0.75f * k, 1.0f * b, 0.68f * b), shirt);

            neck = Joint(chest, "Neck", new Vector3(0, 0.25f * k, 0));
            Part(neck, MeshGen.Capsule(0.05f, 0.14f), new Vector3(0, 0.03f, 0), Vector3.zero, Vector3.one * k, skin);
            head = Joint(neck, "Head", new Vector3(0, 0.1f * k, 0));
            Part(head, MeshGen.Sphere(10, 14), new Vector3(0, 0.1f * k, 0.01f), Vector3.zero, new Vector3(0.2f, 0.25f, 0.23f) * k, skin);
            Part(head, MeshGen.Box(new Vector3(0.05f, 0.08f, 0.06f)), new Vector3(0, 0.08f * k, 0.12f * k), new Vector3(-10, 0, 0), Vector3.one * k, skin); // nose
            Part(head, MeshGen.Sphere(6, 8), new Vector3(0.045f * k, 0.13f * k, 0.105f * k), Vector3.zero, Vector3.one * 0.03f * k, eyes);
            Part(head, MeshGen.Sphere(6, 8), new Vector3(-0.045f * k, 0.13f * k, 0.105f * k), Vector3.zero, Vector3.one * 0.03f * k, eyes);
            if (!s.zombie || rng.Chance(0.6f))
                Part(head, MeshGen.Sphere(8, 12, s.zombie ? 0.3f : 0.1f, s.seed % 9), new Vector3(0, 0.17f * k, -0.015f), Vector3.zero, new Vector3(0.215f, 0.17f, 0.24f) * k, hair);
            if (s.cap)
            {
                Part(head, MeshGen.Sphere(8, 12, 0, 0, 1, true), new Vector3(0, 0.18f * k, 0), Vector3.zero, new Vector3(0.23f, 0.18f, 0.25f) * k, gear);
                Part(head, MeshGen.Box(new Vector3(0.18f, 0.015f, 0.12f)), new Vector3(0, 0.19f * k, 0.15f * k), new Vector3(-8, 0, 0), Vector3.one * k, gear);
            }
            if (s.hood) Part(head, MeshGen.Sphere(8, 12), new Vector3(0, 0.13f * k, -0.03f), Vector3.zero, new Vector3(0.25f, 0.29f, 0.27f) * k, gear);
            if (s.bandana) Part(head, MeshGen.Cylinder(0.105f, 0.105f, 0.04f, 12), new Vector3(0, 0.2f * k, 0), new Vector3(-8, 0, 0), new Vector3(1.05f, 1, 1.1f) * k, MaterialLibrary.Lit("bandana", new Color(0.6f, 0.12f, 0.1f), 0.2f, 0, TexKind.Fabric));
            if (s.goggles) Part(head, MeshGen.Box(new Vector3(0.19f, 0.05f, 0.05f)), new Vector3(0, 0.2f * k, 0.09f * k), new Vector3(-30, 0, 0), Vector3.one * k, Palette.DarkSteel);

            // Arms
            float shoulderX = 0.21f * b * k;
            upperArmL = Joint(chest, "UpperArmL", new Vector3(-shoulderX, 0.2f * k, 0));
            upperArmR = Joint(chest, "UpperArmR", new Vector3(shoulderX, 0.2f * k, 0));
            foreach (var ua in new[] { upperArmL, upperArmR })
            {
                Part(ua, MeshGen.Capsule(0.06f, 0.32f), new Vector3(0, -0.14f * k, 0), Vector3.zero, new Vector3(b, k, b), s.zombie && rng.Chance(0.4f) ? skin : shirt);
                var fa = Joint(ua, "ForeArm", new Vector3(0, -0.29f * k, 0));
                Part(fa, MeshGen.Capsule(0.05f, 0.3f), new Vector3(0, -0.13f * k, 0), Vector3.zero, new Vector3(b, k, b), skin);
                Part(fa, MeshGen.Sphere(6, 8), new Vector3(0, -0.3f * k, 0.01f), Vector3.zero, new Vector3(0.07f, 0.1f, 0.05f) * k, skin);
                if (ua == upperArmL) foreArmL = fa; else foreArmR = fa;
            }

            // Legs
            float hipX = 0.1f * b * k;
            thighL = Joint(hips, "ThighL", new Vector3(-hipX, -0.05f * k, 0));
            thighR = Joint(hips, "ThighR", new Vector3(hipX, -0.05f * k, 0));
            foreach (var th in new[] { thighL, thighR })
            {
                Part(th, MeshGen.Capsule(0.085f, 0.46f), new Vector3(0, -0.21f * k, 0), Vector3.zero, new Vector3(b, k, b), pants);
                var sh = Joint(th, "Shin", new Vector3(0, -0.43f * k, 0));
                Part(sh, MeshGen.Capsule(0.065f, 0.44f), new Vector3(0, -0.2f * k, 0), Vector3.zero, new Vector3(b, k, b), pants);
                var ft = Joint(sh, "Foot", new Vector3(0, -0.42f * k, 0));
                Part(ft, MeshGen.Box(new Vector3(0.11f, 0.08f, 0.26f)), new Vector3(0, -0.02f, 0.06f), Vector3.zero, Vector3.one * k, shoes);
                if (th == thighL) { shinL = sh; footL = ft; } else { shinR = sh; footR = ft; }
            }

            // Gear
            if (s.backpack)
            {
                Part(chest, MeshGen.Box(new Vector3(0.32f, 0.42f, 0.18f)), new Vector3(0, 0.02f * k, -0.2f * k), Vector3.zero, Vector3.one * k, gear);
                Part(chest, MeshGen.Cylinder(0.07f, 0.07f, 0.34f, 8), new Vector3(0, 0.26f * k, -0.2f * k), new Vector3(0, 0, 90), Vector3.one * k, MaterialLibrary.Lit("bedroll", new Color(0.25f, 0.3f, 0.35f), 0.1f, 0, TexKind.Fabric));
            }
            if (s.machete)
            {
                Part(chest, MeshGen.Box(new Vector3(0.05f, 0.55f, 0.012f)), new Vector3(0.1f, 0.05f, -0.32f * k), new Vector3(0, 0, 35), Vector3.one * k, Palette.Steel);
                Part(chest, MeshGen.Box(new Vector3(0.04f, 0.14f, 0.03f)), new Vector3(-0.07f, 0.3f * k, -0.32f * k), new Vector3(0, 0, 35), Vector3.one * k, Palette.Tire);
            }
            if (s.medBag)
            {
                Part(hips, MeshGen.Box(new Vector3(0.22f, 0.18f, 0.12f)), new Vector3(0.2f * k, -0.05f, 0.02f), Vector3.zero, Vector3.one * k, MaterialLibrary.Lit("medbag", new Color(0.75f, 0.75f, 0.72f), 0.2f, 0, TexKind.Fabric));
                Part(hips, MeshGen.Box(new Vector3(0.1f, 0.03f, 0.005f)), new Vector3(0.2f * k, -0.03f, 0.085f), Vector3.zero, Vector3.one * k, MaterialLibrary.Lit("redcross", new Color(0.7f, 0.08f, 0.08f), 0.2f));
                Part(hips, MeshGen.Box(new Vector3(0.03f, 0.1f, 0.005f)), new Vector3(0.2f * k, -0.03f, 0.085f), Vector3.zero, Vector3.one * k, MaterialLibrary.Lit("redcross", new Color(0.7f, 0.08f, 0.08f), 0.2f));
            }
            if (s.wrench)
                Part(hips, MeshGen.Box(new Vector3(0.04f, 0.32f, 0.02f)), new Vector3(-0.2f * k, -0.1f, 0.03f), new Vector3(0, 0, 8), Vector3.one * k, Palette.Steel);
            if (!s.zombie)
                Part(hips, MeshGen.Cylinder(0.17f, 0.17f, 0.05f, 14, false), new Vector3(0, 0.06f, 0), Vector3.zero, new Vector3(b, 1, 0.82f * b) * k, MaterialLibrary.Lit("belt", new Color(0.18f, 0.12f, 0.08f), 0.4f, 0, TexKind.Fabric));
            if (s.torn)
            {
                // tattered shirt flaps
                Part(chest, MeshGen.Box(new Vector3(0.12f, 0.2f, 0.01f)), new Vector3(0.08f, -0.25f * k, 0.13f * b), new Vector3(10, 0, 15), Vector3.one * k, shirt);
                Part(chest, MeshGen.Box(new Vector3(0.1f, 0.16f, 0.01f)), new Vector3(-0.1f, -0.24f * k, 0.12f * b), new Vector3(5, 0, -20), Vector3.one * k, shirt);
            }
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void SetPose(Pose p)
        {
            if (p == pose) return;
            pose = p;
            poseTime = 0f;
            if (p == Pose.Rise && root != null) root.localPosition = new Vector3(0, -1.7f, 0); // start underground
        }

        public void SetVisible(bool v)
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r) r.enabled = v;
        }

        static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            poseTime += dt;

            // Targets
            Quaternion tHips = Quaternion.identity, tSpine = Quaternion.identity, tChest = Quaternion.identity, tNeck = Quaternion.identity, tHead = Quaternion.identity;
            Quaternion tThL = Quaternion.identity, tThR = Quaternion.identity, tShL = Quaternion.identity, tShR = Quaternion.identity, tFtL = Quaternion.identity, tFtR = Quaternion.identity;
            Quaternion tUaL = E(0, 0, -8), tUaR = E(0, 0, 8), tFaL = E(-10, 0, 0), tFaR = E(-10, 0, 0);
            Vector3 tHipsPos = new Vector3(0, hipsHeight, 0);
            Quaternion tRoot = Quaternion.identity;
            Vector3 tRootPos = Vector3.zero;
            float sharp = 14f;

            switch (pose)
            {
                case Pose.Idle:
                {
                    phase += dt * 1.6f;
                    float br = Mathf.Sin(phase) * 2f;
                    tChest = E(br, 0, 0); tHead = E(-br, Mathf.Sin(phase * 0.3f) * 15f, 0);
                    tUaL = E(0, 0, -6 - br); tUaR = E(0, 0, 6 + br);
                    break;
                }
                case Pose.Run:
                {
                    phase += dt * 9.5f * speed;
                    float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
                    tThL = E(-s * 45f - 10f, 0, 0); tThR = E(s * 45f - 10f, 0, 0);
                    tShL = E(Mathf.Max(0, c) * 80f + 15f, 0, 0); tShR = E(Mathf.Max(0, -c) * 80f + 15f, 0, 0);
                    tFtL = E(-10 + s * 10, 0, 0); tFtR = E(-10 - s * 10, 0, 0);
                    tUaL = E(s * 50f, 0, -10); tUaR = E(-s * 50f, 0, 10);
                    tFaL = E(-75f, 0, 0); tFaR = E(-75f, 0, 0);
                    tSpine = E(14f, s * 6f, -lean * 12f); tChest = E(0, -s * 8f, 0); tHead = E(-10f, s * 3f, 0);
                    tHipsPos.y = hipsHeight - 0.04f + Mathf.Abs(c) * 0.07f;
                    tRoot = E(0, 0, -lean * 10f);
                    sharp = 22f;
                    break;
                }
                case Pose.Jump:
                {
                    float t = Mathf.Clamp01(poseTime / 0.35f);
                    tThL = E(-70f * t, 0, 0); tThR = E(-25f * t, 0, 0);
                    tShL = E(100f * t, 0, 0); tShR = E(50f * t, 0, 0);
                    tUaL = E(-140f * t, 0, -20); tUaR = E(-60f * t, 0, 20);
                    tFaL = E(-30, 0, 0); tFaR = E(-60, 0, 0);
                    tSpine = E(18f * t, 0, 0); tHead = E(-15f, 0, 0);
                    sharp = 16f;
                    break;
                }
                case Pose.Slide:
                {
                    tRoot = E(-55f, 0, 0);
                    tRootPos = new Vector3(0, -0.1f, 0);
                    tHipsPos.y = 0.35f;
                    tThL = E(-70f, 0, 0); tShL = E(10f, 0, 0);
                    tThR = E(-20f, 0, 10); tShR = E(90f, 0, 0);
                    tSpine = E(25f, 0, 0); tChest = E(10f, 0, 0); tHead = E(30f, 0, 0);
                    tUaL = E(-30f, 0, -70f); tUaR = E(-30f, 0, 70f);
                    tFaL = E(-20, 0, 0); tFaR = E(-20, 0, 0);
                    sharp = 20f;
                    break;
                }
                case Pose.Stumble:
                {
                    float t = poseTime / 0.6f;
                    float w = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    phase += dt * 9f;
                    float s = Mathf.Sin(phase * 1.3f);
                    tSpine = E(40f * w, s * 10f, 0); tHead = E(-20 * w, 0, 0);
                    tUaL = E(-90f * w, 0, -40 + s * 30); tUaR = E(-60f * w, 0, 50 - s * 30);
                    tThL = E(-s * 40f - 20f * w, 0, 0); tThR = E(s * 40f - 20f * w, 0, 0);
                    tShL = E(40, 0, 0); tShR = E(40, 0, 0);
                    tHipsPos.y = hipsHeight - 0.12f * w;
                    if (t > 1f) SetPose(Pose.Run);
                    break;
                }
                case Pose.Dead:
                {
                    float t = Mathf.Clamp01(poseTime / 0.7f);
                    tRoot = E(Mathf.Lerp(0, 82f, t * t), 0, 0);
                    tRootPos = new Vector3(0, 0.12f * t, 0.5f * t);
                    tHipsPos.y = Mathf.Lerp(hipsHeight, 0.2f, t);
                    tUaL = E(-150f, 0, -30); tUaR = E(-150f, 0, 30);
                    tThL = E(10, 0, -5); tThR = E(-15, 0, 5); tShL = E(30, 0, 0); tShR = E(10, 0, 0);
                    tHead = E(-25, 25, 0);
                    sharp = 8f;
                    break;
                }
                case Pose.ZombieWalk:
                {
                    phase += dt * 3.6f * speed;
                    float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
                    float lp = 1f + limp * Mathf.Sign(s);
                    tThL = E(-s * 22f * lp, 0, -3); tThR = E(s * 22f / lp, 0, 3);
                    tShL = E(Mathf.Max(0, c) * 35f + 8f, 0, 0); tShR = E(Mathf.Max(0, -c) * 35f + 8f, 0, 0);
                    tSpine = E(22f, s * 6f, 8f * limp); tChest = E(8f, 0, s * 4f);
                    tNeck = E(15f, 0, 0); tHead = E(-25f + s * 4f, 20f * limp, 18f * limp);
                    tUaL = E(-80f + s * 10f, 0, -6); tUaR = E(-75f - s * 10f, 0, 6);
                    tFaL = E(-15f, 0, 0); tFaR = E(-25f, 0, 0);
                    tHipsPos.y = hipsHeight - 0.05f + Mathf.Abs(c) * 0.04f;
                    tRoot = E(0, 0, s * 5f);
                    sharp = 10f;
                    break;
                }
                case Pose.ZombieRun:
                {
                    phase += dt * 10f * speed;
                    float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
                    tThL = E(-s * 50f - 10f, 0, 0); tThR = E(s * 50f - 10f, 0, 0);
                    tShL = E(Mathf.Max(0, c) * 85f + 10f, 0, 0); tShR = E(Mathf.Max(0, -c) * 85f + 10f, 0, 0);
                    tSpine = E(35f, s * 10f, 0); tChest = E(10f, -s * 8f, 0);
                    tHead = E(-35f, s * 8f, s * 6f);
                    tUaL = E(-95f + s * 35f, 0, -15); tUaR = E(-95f - s * 35f, 0, 15);
                    tFaL = E(-10f, 0, 0); tFaR = E(-10f, 0, 0);
                    tHipsPos.y = hipsHeight - 0.08f + Mathf.Abs(c) * 0.09f;
                    sharp = 22f;
                    break;
                }
                case Pose.Attack:
                {
                    float t = Mathf.Repeat(poseTime, 0.8f) / 0.8f;
                    float swing = Mathf.Sin(t * Mathf.PI * 2f);
                    tSpine = E(30f + swing * 15f, swing * 15f, 0);
                    tHead = E(-30f, 0, 0);
                    tUaL = E(-140f + swing * 70f, 0, -20); tUaR = E(-140f - swing * 70f, 0, 20);
                    tFaL = E(-30f, 0, 0); tFaR = E(-30f, 0, 0);
                    tThL = E(-30f, 0, 0); tThR = E(20f, 0, 0); tShL = E(30f, 0, 0); tShR = E(30f, 0, 0);
                    sharp = 18f;
                    break;
                }
                case Pose.Rise:
                {
                    float t = Mathf.Clamp01(poseTime / 1.4f);
                    tRootPos = new Vector3(0, Mathf.Lerp(-1.7f, 0f, Mathf.SmoothStep(0, 1, t)), 0);
                    tSpine = E(Mathf.Lerp(50f, 22f, t), 0, 0);
                    tUaL = E(-150f + 70f * t, 0, -20); tUaR = E(-150f + 75f * t, 0, 20);
                    tHead = E(-40f, Mathf.Sin(poseTime * 6f) * 10f, 0);
                    sharp = 12f;
                    if (t >= 1f) SetPose(Pose.ZombieWalk);
                    break;
                }
                case Pose.Cheer:
                {
                    phase += dt * 4f;
                    tUaL = E(-160f, 0, -20 + Mathf.Sin(phase) * 15f); tUaR = E(-160f, 0, 20 - Mathf.Sin(phase) * 15f);
                    tHipsPos.y = hipsHeight + Mathf.Abs(Mathf.Sin(phase)) * 0.08f;
                    break;
                }
            }

            float a = 1f - Mathf.Exp(-sharp * dt);
            root.localRotation = Quaternion.Slerp(root.localRotation, tRoot, a);
            root.localPosition = Vector3.Lerp(root.localPosition, tRootPos, a);
            hips.localPosition = Vector3.Lerp(hips.localPosition, tHipsPos, a);
            hips.localRotation = Quaternion.Slerp(hips.localRotation, tHips, a);
            spine.localRotation = Quaternion.Slerp(spine.localRotation, tSpine, a);
            chest.localRotation = Quaternion.Slerp(chest.localRotation, tChest, a);
            neck.localRotation = Quaternion.Slerp(neck.localRotation, tNeck, a);
            head.localRotation = Quaternion.Slerp(head.localRotation, tHead, a);
            thighL.localRotation = Quaternion.Slerp(thighL.localRotation, tThL, a);
            thighR.localRotation = Quaternion.Slerp(thighR.localRotation, tThR, a);
            shinL.localRotation = Quaternion.Slerp(shinL.localRotation, tShL, a);
            shinR.localRotation = Quaternion.Slerp(shinR.localRotation, tShR, a);
            footL.localRotation = Quaternion.Slerp(footL.localRotation, tFtL, a);
            footR.localRotation = Quaternion.Slerp(footR.localRotation, tFtR, a);
            upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, tUaL, a);
            upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, tUaR, a);
            foreArmL.localRotation = Quaternion.Slerp(foreArmL.localRotation, tFaL, a);
            foreArmR.localRotation = Quaternion.Slerp(foreArmR.localRotation, tFaR, a);
        }
    }
}
