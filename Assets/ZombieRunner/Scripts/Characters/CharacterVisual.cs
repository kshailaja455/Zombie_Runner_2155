using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// One interface over two kinds of character: an imported rigged model driven by an Animator
    /// controller (created by the setup tool), or the built-in ProceduralHumanoid.
    /// Animator parameters: Speed (float), Grounded (bool), Sliding (bool), Jump / Slide / Stumble / Die / Attack / Rise (triggers).
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        public ProceduralHumanoid procedural;
        public Animator animator;
        public float lean;
        Pose current = Pose.Idle;
        Transform modelRoot;

        static readonly int PSpeed = Animator.StringToHash("Speed");
        static readonly int PGrounded = Animator.StringToHash("Grounded");
        static readonly int PSliding = Animator.StringToHash("Sliding");
        static readonly int TJump = Animator.StringToHash("Jump");
        static readonly int TSlide = Animator.StringToHash("Slide");
        static readonly int TStumble = Animator.StringToHash("Stumble");
        static readonly int TDie = Animator.StringToHash("Die");
        static readonly int TAttack = Animator.StringToHash("Attack");
        static readonly int TRise = Animator.StringToHash("Rise");

        public Pose CurrentPose => current;

        public static CharacterVisual CreateSurvivor(Transform parent, int characterIndex)
        {
            var reg = ZRAssetRegistry.Instance;
            var go = new GameObject("SurvivorVisual");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<CharacterVisual>();
            if (reg != null && reg.playerModels.Count > 0 && reg.playerController != null)
            {
                var model = reg.playerModels[Mathf.Abs(characterIndex) % reg.playerModels.Count];
                if (model != null) { v.UseModel(model, reg.playerController, 1.8f); return v; }
            }
            v.procedural = ProceduralHumanoid.Create(go.transform, CharacterCatalog.Survivor(characterIndex), "Survivor");
            return v;
        }

        public static CharacterVisual CreateZombie(Transform parent, HumanoidStyle style, int seed)
        {
            var reg = ZRAssetRegistry.Instance;
            var go = new GameObject("ZombieVisual");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<CharacterVisual>();
            if (reg != null && reg.zombieModels.Count > 0 && reg.zombieController != null)
            {
                var model = reg.zombieModels[Mathf.Abs(seed) % reg.zombieModels.Count];
                if (model != null) { v.UseModel(model, reg.zombieController, style.height); return v; }
            }
            v.procedural = ProceduralHumanoid.Create(go.transform, style, "Zombie");
            return v;
        }

        void UseModel(GameObject model, RuntimeAnimatorController controller, float targetHeight)
        {
            var inst = Instantiate(model, transform);
            inst.name = model.name;
            modelRoot = inst.transform;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
            animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // Normalise height so any imported model fits the gameplay capsule.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (b.size.y > 0.1f) inst.transform.localScale *= targetHeight / b.size.y;
            }
        }

        public void SetPose(Pose p, float animSpeed = 1f)
        {
            if (procedural != null)
            {
                procedural.speed = animSpeed;
                procedural.lean = lean;
                procedural.SetPose(p);
                current = procedural.pose;
                return;
            }
            if (animator == null) return;
            animator.speed = Mathf.Max(0.2f, animSpeed);
            bool changed = p != current;
            current = p;
            switch (p)
            {
                case Pose.Idle: animator.SetFloat(PSpeed, 0f); animator.SetBool(PGrounded, true); animator.SetBool(PSliding, false); break;
                case Pose.Run: animator.SetFloat(PSpeed, 1f); animator.SetBool(PGrounded, true); animator.SetBool(PSliding, false); break;
                case Pose.ZombieWalk: animator.SetFloat(PSpeed, 0.3f); break;
                case Pose.ZombieRun: animator.SetFloat(PSpeed, 1f); break;
                case Pose.Jump: animator.SetBool(PGrounded, false); if (changed) animator.SetTrigger(TJump); break;
                case Pose.Slide: animator.SetBool(PSliding, true); if (changed) animator.SetTrigger(TSlide); break;
                case Pose.Stumble: if (changed) animator.SetTrigger(TStumble); break;
                case Pose.Dead: if (changed) animator.SetTrigger(TDie); break;
                case Pose.Attack: if (changed) animator.SetTrigger(TAttack); break;
                case Pose.Rise: if (changed) animator.SetTrigger(TRise); break;
            }
        }

        void LateUpdate()
        {
            if (procedural != null) { procedural.lean = lean; current = procedural.pose; }
            else if (modelRoot != null) modelRoot.localRotation = Quaternion.Euler(0, 0, -lean * 8f);
        }
    }

    /// <summary>Survivor presets for character selection, and zombie variations per theme.</summary>
    public static class CharacterCatalog
    {
        public static readonly string[] SurvivorNames = { "Ranger", "Medic", "Mechanic" };
        public static readonly string[] SurvivorBlurbs =
        {
            "Ex park ranger. Machete, cap and a pack full of trail rations.",
            "Field medic. Keeps a first-aid kit on the hip at all times.",
            "Garage mechanic. Coveralls, goggles and a heavy wrench."
        };

        public static int SurvivorCount
        {
            get
            {
                var reg = ZRAssetRegistry.Instance;
                if (reg != null && reg.playerModels.Count > 0 && reg.playerController != null) return reg.playerModels.Count;
                return SurvivorNames.Length;
            }
        }

        public static string SurvivorName(int i)
        {
            var reg = ZRAssetRegistry.Instance;
            if (reg != null && reg.playerModels.Count > 0 && reg.playerController != null)
            {
                var m = reg.playerModels[Mathf.Abs(i) % reg.playerModels.Count];
                return m != null ? m.name : "Survivor";
            }
            return SurvivorNames[Mathf.Abs(i) % SurvivorNames.Length];
        }

        public static string SurvivorBlurb(int i)
        {
            var reg = ZRAssetRegistry.Instance;
            if (reg != null && reg.playerModels.Count > 0 && reg.playerController != null) return "Imported character model.";
            return SurvivorBlurbs[Mathf.Abs(i) % SurvivorBlurbs.Length];
        }

        public static HumanoidStyle Survivor(int i)
        {
            switch (Mathf.Abs(i) % 3)
            {
                case 1:
                    return new HumanoidStyle
                    {
                        skin = new Color(0.55f, 0.38f, 0.28f), shirt = new Color(0.2f, 0.24f, 0.34f), pants = new Color(0.3f, 0.3f, 0.32f),
                        hair = new Color(0.06f, 0.05f, 0.05f), gear = new Color(0.25f, 0.27f, 0.3f), bandana = true, medBag = true, backpack = true, seed = 11
                    };
                case 2:
                    return new HumanoidStyle
                    {
                        skin = new Color(0.86f, 0.7f, 0.58f), shirt = new Color(0.55f, 0.4f, 0.22f), pants = new Color(0.5f, 0.37f, 0.2f),
                        hair = new Color(0.45f, 0.28f, 0.15f), gear = new Color(0.2f, 0.2f, 0.2f), goggles = true, wrench = true, bulk = 1.08f, seed = 12
                    };
                default:
                    return new HumanoidStyle
                    {
                        skin = new Color(0.78f, 0.6f, 0.48f), shirt = new Color(0.3f, 0.35f, 0.24f), pants = new Color(0.24f, 0.23f, 0.21f),
                        hair = new Color(0.18f, 0.12f, 0.08f), gear = new Color(0.4f, 0.32f, 0.2f), cap = true, backpack = true, machete = true, seed = 10
                    };
            }
        }

        public static HumanoidStyle Zombie(ThemeProfile theme, ZombieKind kind, int seed)
        {
            var r = new Rng(seed);
            var s = new HumanoidStyle
            {
                zombie = true,
                torn = true,
                seed = seed,
                skin = r.Vary(r.Pick(theme.zombieSkins), 0.08f),
                shirt = r.Vary(r.Pick(theme.zombieClothes), 0.15f),
                pants = r.Vary(r.Pick(theme.zombieClothes) * 0.8f, 0.1f),
                hair = r.Vary(new Color(0.12f, 0.1f, 0.08f), 0.3f),
                shoes = new Color(0.1f, 0.09f, 0.08f),
                gear = r.Vary(r.Pick(theme.zombieClothes), 0.2f),
                height = r.Range(1.7f, 1.88f),
                hood = r.Chance(0.15f),
                cap = r.Chance(0.1f)
            };
            if (theme.id == ThemeId.AbandonedCity && r.Chance(0.25f)) { s.shirt = new Color(0.32f, 0.35f, 0.24f); s.cap = true; } // soldier
            if (theme.id == ThemeId.DarkCity && r.Chance(0.2f)) { s.shirt = new Color(0.12f, 0.14f, 0.2f); } // office worker
            switch (kind)
            {
                case ZombieKind.Brute: s.height = 2.25f; s.bulk = 1.45f; s.skin *= 0.85f; s.skin.a = 1; break;
                case ZombieKind.Runner: s.bulk = 0.9f; s.height = r.Range(1.65f, 1.8f); break;
            }
            return s;
        }
    }
}
