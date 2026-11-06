using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    [System.Serializable]
    public class ThemeImportSet
    {
        public ThemeId theme;
        [Header("Ground PBR textures (e.g. from ambientCG / Poly Haven)")]
        public Texture2D groundAlbedo;
        public Texture2D groundNormal;
        public Texture2D vergeAlbedo;
        public Texture2D vergeNormal;
        [Header("Imported models (FBX/OBJ). Each list is optional.")]
        public List<GameObject> decor = new List<GameObject>();
        public List<GameObject> jumpObstacles = new List<GameObject>();
        public List<GameObject> slideObstacles = new List<GameObject>();
        public List<GameObject> blockObstacles = new List<GameObject>();
        [Header("Optional HDRI / cubemap skybox material")]
        public Material skyboxOverride;
    }

    [System.Serializable]
    public class AudioOverride
    {
        public string key;
        public AudioClip clip;
    }

    /// <summary>
    /// Single asset (Resources/ZRAssetRegistry) that the setup tool fills in.
    /// Every field is optional: anything missing is generated procedurally at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Runner/Asset Registry", fileName = "ZRAssetRegistry")]
    public class ZRAssetRegistry : ScriptableObject
    {
        [Header("Base materials (created by the setup tool so shaders are included in builds)")]
        public Material litOpaque;
        public Material litTransparent;
        public Material particleAlpha;
        public Material particleAdditive;
        public Material skyboxProcedural;
        [Tooltip("Procedural sky per theme, index = ThemeId (created by the setup tool).")]
        public List<Material> themeSkyboxes = new List<Material>();
        public Font uiFont;
        [Tooltip("Materials that only exist so URP keeps the shader variants the runtime enables (normal maps, emission, transparency) in builds.")]
        public List<Material> variantKeepers = new List<Material>();

        [Header("Characters (optional, humanoid rigs such as Mixamo exports)")]
        public List<GameObject> playerModels = new List<GameObject>();
        public List<GameObject> zombieModels = new List<GameObject>();
        public RuntimeAnimatorController playerController;
        public RuntimeAnimatorController zombieController;

        [Header("Themes")]
        public List<ThemeImportSet> themes = new List<ThemeImportSet>();

        [Header("Audio overrides (key -> clip). Keys are listed in AudioKeys.")]
        public List<AudioOverride> audio = new List<AudioOverride>();

        static ZRAssetRegistry cached;
        static bool looked;

        public static ZRAssetRegistry Instance
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    cached = Resources.Load<ZRAssetRegistry>("ZRAssetRegistry");
                    if (cached == null)
                        Debug.LogWarning("[ZombieRunner] Resources/ZRAssetRegistry not found. Using runtime fallbacks. Run Tools > Zombie Runner > Setup Wizard.");
                }
                return cached;
            }
        }

        public ThemeImportSet Theme(ThemeId id)
        {
            foreach (var t in themes) if (t != null && t.theme == id) return t;
            return null;
        }

        public AudioClip Audio(string key)
        {
            foreach (var a in audio) if (a != null && a.clip != null && a.key == key) return a.clip;
            return null;
        }
    }
}
