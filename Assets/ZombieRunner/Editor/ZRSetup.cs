using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ZombieRunner.EditorTools
{
    /// <summary>All setup steps used by the ZombieRunnerSetup window. Each step is safe to run again.</summary>
    public static class ZRSetup
    {
        public const string Root = "Assets/ZombieRunner";
        public const string Imported = Root + "/Imported";
        public const string Inbox = Imported + "/_Inbox";
        public const string Unsorted = Imported + "/_Unsorted";
        public const string PlayerChars = Imported + "/Characters/Player";
        public const string ZombieChars = Imported + "/Characters/Zombies";
        public const string PlayerAnims = Imported + "/Animations/Player";
        public const string ZombieAnims = Imported + "/Animations/Zombies";
        public const string AudioDir = Imported + "/Audio";
        public const string MaterialsDir = Root + "/Materials";
        public const string AnimDir = Root + "/Animation";
        public const string PrefabDir = Root + "/Prefabs";
        public const string SceneDir = Root + "/Scenes";
        public const string ResourcesDir = Root + "/Resources";
        public const string GeneratedDir = Root + "/Generated";
        public const string RegistryPath = ResourcesDir + "/ZRAssetRegistry.asset";
        public const string PlayerControllerPath = AnimDir + "/ZR_Player.controller";
        public const string ZombieControllerPath = AnimDir + "/ZR_Zombie.controller";

        static readonly string[] ThemeFolders = { "Forest", "Desert", "DarkCity", "AbandonedCity" };
        static readonly string[] PropKinds = { "Decor", "Jump", "Slide", "Block" };
        static readonly string[] ModelExt = { ".fbx", ".obj", ".dae", ".3ds", ".blend", ".gltf", ".glb" };
        static readonly string[] TexExt = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr" };
        static readonly string[] AudioExt = { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" };

        public static string ThemeDir(int t) => Imported + "/Themes/" + ThemeFolders[t];
        public static string ScenePath(string scene) => SceneDir + "/" + scene + ".unity";

        static List<string> lastPlayerPrefabs = new List<string>(), lastZombiePrefabs = new List<string>();

        // =====================================================================================
        // 1. PROJECT CHECK
        // =====================================================================================
        public static void CheckProject(Action<LogLevel, string> log)
        {
            log(LogLevel.Info, "Unity " + Application.unityVersion);
            if (!Application.unityVersion.StartsWith("6")) log(LogLevel.Warning, "This project targets Unity 6 LTS (6000.x). Other versions may need small API fixes.");
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) rp = CreateUrpAsset(log);
            if (rp == null) log(LogLevel.Error, "No render pipeline is active and a URP asset could not be created. Check that the Universal RP package is installed (Window > Package Manager), or create the project from the 'Universal 3D' template.");
            else if (!(rp is UniversalRenderPipelineAsset)) log(LogLevel.Error, "The active render pipeline is not URP (" + rp.GetType().Name + ").");
            else
            {
                log(LogLevel.Ok, "URP is active: " + rp.name);
                CheckSsao((UniversalRenderPipelineAsset)rp, log);
            }

            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var p = so.FindProperty("activeInputHandler");
                if (p != null)
                {
                    string mode = p.intValue == 0 ? "Input Manager (legacy)" : p.intValue == 1 ? "Input System Package" : "Both";
                    log(LogLevel.Info, "Active input handling: " + mode + ". The game supports all three.");
                }
            }
#if ENABLE_INPUT_SYSTEM
            log(LogLevel.Ok, "Input System package is active.");
#else
            log(LogLevel.Info, "Input System package not active; using the legacy Input Manager. (Install com.unity.inputsystem to use the new Input System.)");
#endif
        }

        /// <summary>
        /// For projects opened without the Universal 3D template: creates a URP asset and renderer in
        /// Assets/ZombieRunner/Settings and makes it the active pipeline. Does nothing if one already exists there.
        /// </summary>
        static RenderPipelineAsset CreateUrpAsset(Action<LogLevel, string> log)
        {
            const string dir = Root + "/Settings";
            const string assetPath = dir + "/ZR_URP.asset", rendererPath = dir + "/ZR_URP_Renderer.asset";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, "Settings");
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(data, rendererPath);
                }
                // Post-processing needs the package's PostProcessData; URP normally fills it in, this makes sure.
                var so = new SerializedObject(data);
                var pp = so.FindProperty("postProcessData");
                if (pp != null && pp.objectReferenceValue == null)
                {
                    var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    if (ppd != null) { pp.objectReferenceValue = ppd; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
                EditorUtility.SetDirty(data);
                urp = UniversalRenderPipelineAsset.Create(data);
                if (urp == null) return null;
                urp.supportsHDR = true;
                AssetDatabase.CreateAsset(urp, assetPath);
            }
#if UNITY_2021_2_OR_NEWER
            GraphicsSettings.defaultRenderPipeline = urp;
#else
            GraphicsSettings.renderPipelineAsset = urp;
#endif
            // Clear per-quality-level overrides so every level uses this asset.
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qs != null && qs.Length > 0)
            {
                var qso = new SerializedObject(qs[0]);
                var levels = qso.FindProperty("m_QualitySettings");
                if (levels != null)
                    for (int i = 0; i < levels.arraySize; i++)
                    {
                        var c = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                        if (c != null) c.objectReferenceValue = null;
                    }
                qso.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            log(LogLevel.Ok, "No render pipeline was active, so a URP asset was created at " + assetPath + " and made active.");
            return urp;
        }

        static void CheckSsao(UniversalRenderPipelineAsset urp, Action<LogLevel, string> log)
        {
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            bool found = false;
            if (list != null)
                for (int i = 0; i < list.arraySize; i++)
                {
                    var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                        if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) found = true;
                }
            if (found) log(LogLevel.Ok, "Screen Space Ambient Occlusion renderer feature found.");
            else log(LogLevel.Info, "No SSAO renderer feature on the active URP renderer. For ambient occlusion: select the URP Renderer asset > Add Renderer Feature > Screen Space Ambient Occlusion.");
        }

        // =====================================================================================
        // 2. FOLDERS
        // =====================================================================================
        public static void EnsureFolders(Action<LogLevel, string> log)
        {
            var dirs = new List<string>
            {
                Inbox, Unsorted, PlayerChars, ZombieChars, PlayerAnims, ZombieAnims, AudioDir,
                MaterialsDir, AnimDir, PrefabDir + "/Characters", SceneDir, ResourcesDir, GeneratedDir
            };
            for (int t = 0; t < 4; t++)
            {
                foreach (var k in PropKinds) dirs.Add(ThemeDir(t) + "/" + (k == "Decor" ? "Decor" : "Obstacles/" + k));
                dirs.Add(ThemeDir(t) + "/Ground");
                dirs.Add(ThemeDir(t) + "/Verge");
                dirs.Add(ThemeDir(t) + "/Sky");
            }
            int made = 0;
            foreach (var d in dirs) if (MakeFolder(d)) made++;
            log(LogLevel.Ok, made > 0 ? $"Created {made} folders." : "Folder structure already in place.");
        }

        public static bool MakeFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return false;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return true;
        }

        // =====================================================================================
        // 3. ORGANISE THE INBOX
        // =====================================================================================
        static int? GuessTheme(string lower)
        {
            if (Has(lower, "forest", "wood", "pine", "tree", "mud", "moss", "bark", "grass", "leaf", "fern", "log", "stump")) return 0;
            if (Has(lower, "desert", "sand", "dune", "cactus", "canyon", "mesa", "arid")) return 1;
            if (Has(lower, "dark", "night", "neon", "wet", "rain", "alley", "streetlight")) return 2;
            if (Has(lower, "ruin", "abandon", "rubble", "debris", "concrete", "asphalt", "building", "skyscraper", "city", "urban", "checkpoint", "bus", "car", "barrier")) return 3;
            return null;
        }

        static bool Has(string s, params string[] keys) { foreach (var k in keys) if (s.Contains(k)) return true; return false; }

        public static void OrganizeInbox(Action<LogLevel, string> log)
        {
            if (!AssetDatabase.IsValidFolder(Inbox)) { EnsureFolders(log); }
            var files = AssetDatabase.FindAssets("", new[] { Inbox }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();
            if (files.Count == 0) { log(LogLevel.Info, "Inbox is empty (" + Inbox + "). Nothing to organise."); return; }
            int moved = 0;
            foreach (var path in files)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string lower = path.ToLowerInvariant();
                string dest = null;
                if (AudioExt.Contains(ext)) dest = AudioDir;
                else if (ext == ".hdr" || ext == ".exr")
                {
                    var t = GuessTheme(lower); dest = t.HasValue ? ThemeDir(t.Value) + "/Sky" : Unsorted;
                }
                else if (TexExt.Contains(ext))
                {
                    var t = GuessTheme(lower);
                    if (t.HasValue) dest = ThemeDir(t.Value) + (Has(lower, "verge", "grass", "sidewalk", "pavement", "ground_side") ? "/Verge" : "/Ground");
                    else dest = Unsorted;
                }
                else if (ModelExt.Contains(ext))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    bool skinned = go != null && go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
                    bool meshes = go != null && (go.GetComponentsInChildren<MeshFilter>(true).Length > 0 || skinned);
                    int clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__"));
                    bool zombie = Has(lower, "zombie", "undead", "walker", "infected", "mutant");
                    bool animName = Has(Path.GetFileNameWithoutExtension(lower), "run", "jump", "slide", "stumble", "trip", "death", "dying", "die", "idle", "walk", "attack", "bite", "rise", "stand up", "getup", "get up", "fall", "hit");
                    if (clips > 0 && (!meshes || animName)) dest = zombie ? ZombieAnims : PlayerAnims;
                    else if (skinned) dest = zombie ? ZombieChars : PlayerChars;
                    else if (meshes)
                    {
                        var t = GuessTheme(lower);
                        string kind = Has(lower, "barricade", "barrier", "sandbag", "log", "fence", "crate") ? "Obstacles/Jump"
                            : Has(lower, "car", "truck", "bus", "rock", "boulder", "wreck", "tank") ? "Obstacles/Block" : "Decor";
                        dest = t.HasValue ? ThemeDir(t.Value) + "/" + kind : Unsorted;
                    }
                    else dest = Unsorted;
                }
                else continue; // .meta, .mat and other support files stay

                MakeFolder(dest);
                var target = AssetDatabase.GenerateUniqueAssetPath(dest + "/" + Path.GetFileName(path));
                var err = AssetDatabase.MoveAsset(path, target);
                if (string.IsNullOrEmpty(err)) { moved++; log(LogLevel.Info, $"Moved {Path.GetFileName(path)} -> {dest}"); }
                else log(LogLevel.Warning, $"Could not move {path}: {err}");
                if (dest == Unsorted) log(LogLevel.Warning, $"Could not tell what {Path.GetFileName(path)} is. It is in {Unsorted}; move it to the right Imported folder by hand.");
            }
            log(LogLevel.Ok, $"Organised {moved} imported file(s).");
        }

        // =====================================================================================
        // 4. IMPORTERS
        // =====================================================================================
        const string Marker = "ZR_v1";

        public static IEnumerable<string> FilesIn(string folder, string[] exts)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Enumerable.Empty<string>();
            return AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => !AssetDatabase.IsValidFolder(p) && exts.Contains(Path.GetExtension(p).ToLowerInvariant()));
        }

        static bool IsLoopName(string n) => Has(n.ToLowerInvariant(), "run", "walk", "idle", "sprint", "jog", "crawl");

        public static void ConfigureImporters(Action<LogLevel, string> log)
        {
            int n = 0;
            foreach (var dir in new[] { PlayerChars, ZombieChars })
                foreach (var path in FilesIn(dir, ModelExt))
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    if (mi.userData == Marker && mi.animationType == ModelImporterAnimationType.Human) continue;
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    mi.userData = Marker;
                    mi.SaveAndReimport();
                    n++;
                }
            foreach (var dir in new[] { PlayerAnims, ZombieAnims })
                foreach (var path in FilesIn(dir, ModelExt))
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    if (mi.userData == Marker && mi.animationType == ModelImporterAnimationType.Human) continue;
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.importAnimation = true;
                    mi.SaveAndReimport(); // generate default clips first
                    var clips = mi.defaultClipAnimations;
                    string file = Path.GetFileNameWithoutExtension(path);
                    foreach (var c in clips)
                    {
                        bool loop = IsLoopName(file) || IsLoopName(c.name);
                        c.loopTime = loop;
                        c.loopPose = loop;
                        c.lockRootRotation = true; c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true; // in-place
                        if (c.name.StartsWith("mixamo") || c.name == "Take 001") c.name = file;
                    }
                    mi.clipAnimations = clips;
                    mi.userData = Marker;
                    mi.SaveAndReimport();
                    n++;
                }
            for (int t = 0; t < 4; t++)
            {
                foreach (var sub in new[] { "/Ground", "/Verge" })
                    foreach (var path in FilesIn(ThemeDir(t) + sub, TexExt))
                    {
                        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.userData == Marker) continue;
                        string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        if (IsNormalName(lower))
                        {
                            ti.textureType = TextureImporterType.NormalMap;
#if UNITY_2022_2_OR_NEWER
                            if (Has(lower, "_dx", "normaldx", "nor_dx")) ti.flipGreenChannel = true; // DirectX -> OpenGL convention
#endif
                        }
                        else
                        {
                            ti.textureType = TextureImporterType.Default;
                            ti.sRGBTexture = !Has(lower, "rough", "metal", "ao", "occlusion", "height", "disp");
                        }
                        ti.wrapMode = TextureWrapMode.Repeat;
                        ti.mipmapEnabled = true;
                        ti.anisoLevel = 4;
                        ti.maxTextureSize = 2048;
                        ti.userData = Marker;
                        ti.SaveAndReimport();
                        n++;
                    }
                foreach (var path in FilesIn(ThemeDir(t) + "/Sky", TexExt))
                {
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.userData == Marker) continue;
                    ti.textureShape = TextureImporterShape.Texture2D;
                    ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = false;
                    ti.maxTextureSize = 4096;
                    ti.userData = Marker;
                    ti.SaveAndReimport();
                    n++;
                }
            }
            foreach (var path in FilesIn(AudioDir, AudioExt))
            {
                if (!(AssetImporter.GetAtPath(path) is AudioImporter ai) || ai.userData == Marker) continue;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool longClip = name.StartsWith("music") || name.StartsWith("amb") || name.StartsWith("chase");
                var s = ai.defaultSampleSettings;
                s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = longClip ? 0.55f : 0.7f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = !longClip;
                ai.userData = Marker;
                ai.SaveAndReimport();
                n++;
            }
            log(LogLevel.Ok, n > 0 ? $"Configured {n} importer(s) (humanoid rigs, in-place looping clips, normal maps, audio compression)." : "Importers already configured (or nothing imported yet).");
        }

        static bool IsNormalName(string lower) => Has(lower, "normal", "_nrm", "_nor", "_norm", "nor_gl", "nor_dx", "_n.");

        // =====================================================================================
        // 5. MATERIALS
        // =====================================================================================
        public static void CreateMaterials(Action<LogLevel, string> log)
        {
            MakeFolder(MaterialsDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var sky = Shader.Find("Skybox/Procedural");
            var pano = Shader.Find("Skybox/Panoramic");
            if (lit == null || particles == null) { log(LogLevel.Error, "URP shaders not found. Make sure the Universal RP package is installed and active."); return; }

            MatAsset("ZR_LitOpaque", lit, m => { });
            MatAsset("ZR_LitTransparent", lit, m => MaterialLibrary.MakeTransparent(m, false));
            MatAsset("ZR_ParticleAlpha", particles, m => MaterialLibrary.MakeTransparent(m, false));
            MatAsset("ZR_ParticleAdditive", particles, m => MaterialLibrary.MakeTransparent(m, true));
            if (sky != null)
            {
                MatAsset("ZR_SkyProcedural", sky, m => { });
                for (int t = 0; t < 4; t++)
                {
                    var p = ThemeDatabase.Get((ThemeId)t);
                    MatAsset("Sky_" + ThemeFolders[t], sky, m => MaterialLibrary.ConfigureSky(m, p));
                }
            }
            else log(LogLevel.Warning, "Skybox/Procedural shader not found; skies fall back to camera colour.");

            // Shader-variant keepers: builds strip shader variants that no material asset uses, so keep the ones the runtime enables.
            MatAsset("Keep_Normal", lit, m => m.EnableKeyword("_NORMALMAP"));
            MatAsset("Keep_Emission", lit, m => m.EnableKeyword("_EMISSION"));
            MatAsset("Keep_NormalEmission", lit, m => { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_EMISSION"); });
            MatAsset("Keep_TransparentEmission", lit, m => { MaterialLibrary.MakeTransparent(m, false); m.EnableKeyword("_EMISSION"); });

            // Imported HDRI skies and ground texture previews
            for (int t = 0; t < 4; t++)
            {
                var hdri = FilesIn(ThemeDir(t) + "/Sky", TexExt).FirstOrDefault();
                if (hdri != null && pano != null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(hdri);
                    MatAsset("Sky_" + ThemeFolders[t] + "_HDRI", pano, m => { m.SetTexture("_MainTex", tex); m.SetFloat("_Exposure", 1f); });
                    log(LogLevel.Ok, $"{ThemeFolders[t]}: HDRI sky material created from {Path.GetFileName(hdri)}.");
                }
                FindPbr(ThemeDir(t) + "/Ground", out var albedo, out var normal);
                if (albedo != null)
                {
                    MatAsset("Ground_" + ThemeFolders[t], lit, m =>
                    {
                        m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", Vector2.one * 2f);
                        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                    });
                    log(LogLevel.Ok, $"{ThemeFolders[t]}: ground PBR material created ({albedo.name}{(normal != null ? " + " + normal.name : "")}).");
                }
            }
            log(LogLevel.Ok, "Base URP materials, theme skies and shader-variant keepers are in " + MaterialsDir + ".");
        }

        static Material MatAsset(string name, Shader shader, Action<Material> configure)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = name }; configure(m); AssetDatabase.CreateAsset(m, path); }
            else { m.shader = shader; configure(m); EditorUtility.SetDirty(m); }
            return m;
        }

        public static void FindPbr(string folder, out Texture2D albedo, out Texture2D normal)
        {
            albedo = null; normal = null;
            foreach (var path in FilesIn(folder, TexExt))
            {
                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                if (IsNormalName(lower)) { if (normal == null || Has(lower, "gl")) normal = tex; }
                else if (Has(lower, "color", "albedo", "basecolor", "diff", "diffuse", "col")) albedo = tex;
                else if (albedo == null && !Has(lower, "rough", "metal", "ao", "occlusion", "height", "disp", "spec")) albedo = tex;
            }
        }

        // =====================================================================================
        // 6. ANIMATOR CONTROLLERS
        // =====================================================================================
        static Dictionary<string, AnimationClip> CollectClips(params string[] folders)
        {
            var map = new Dictionary<string, AnimationClip>();
            foreach (var f in folders)
                foreach (var path in FilesIn(f, ModelExt))
                {
                    string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    {
                        if (c.name.StartsWith("__preview__")) continue;
                        string key = file + "|" + c.name.ToLowerInvariant();
                        if (!map.ContainsKey(key)) map[key] = c;
                    }
                }
            return map;
        }

        static AnimationClip Find(Dictionary<string, AnimationClip> clips, string[] include, string[] exclude = null)
        {
            foreach (var kv in clips)
            {
                if (!Has(kv.Key, include)) continue;
                if (exclude != null && Has(kv.Key, exclude)) continue;
                return kv.Value;
            }
            return null;
        }

        public static void CreateAnimators(Action<LogLevel, string> log)
        {
            MakeFolder(AnimDir);
            // Player
            var pc = CollectClips(PlayerAnims, PlayerChars);
            var run = Find(pc, new[] { "run", "sprint", "jog" }, new[] { "slide", "jump" });
            if (run == null)
            {
                if (FilesIn(PlayerChars, ModelExt).Any())
                    log(LogLevel.Warning, "Player model imported but no Run animation found in " + PlayerAnims + ". The procedural survivor will be used until a run clip is added (e.g. Mixamo 'Running', In Place).");
                else log(LogLevel.Info, "No imported player animations. The built-in procedural survivor animation is used.");
                AssetDatabase.DeleteAsset(PlayerControllerPath);
            }
            else
            {
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Idle"] = Find(pc, new[] { "idle" }),
                    ["Run"] = run,
                    ["Jump"] = Find(pc, new[] { "jump" }),
                    ["Slide"] = Find(pc, new[] { "slide" }),
                    ["Stumble"] = Find(pc, new[] { "stumble", "trip", "hit", "knock", "fall" }, new[] { "death", "dying" }),
                    ["Die"] = Find(pc, new[] { "death", "dying", "die" })
                };
                BuildController(PlayerControllerPath, clips, run, false, log);
            }

            // Zombies
            var zc = CollectClips(ZombieAnims, ZombieChars);
            var walk = Find(zc, new[] { "walk" });
            var zrun = Find(zc, new[] { "run", "sprint", "chase" });
            var baseClip = walk ?? zrun;
            if (baseClip == null)
            {
                if (FilesIn(ZombieChars, ModelExt).Any())
                    log(LogLevel.Warning, "Zombie model imported but no walk/run animation found in " + ZombieAnims + ". Procedural zombies will be used until clips are added (e.g. Mixamo 'Zombie Walk', 'Zombie Running').");
                else log(LogLevel.Info, "No imported zombie animations. The built-in procedural zombie animation is used.");
                AssetDatabase.DeleteAsset(ZombieControllerPath);
            }
            else
            {
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Walk"] = walk ?? zrun,
                    ["Run"] = zrun ?? walk,
                    ["Attack"] = Find(zc, new[] { "attack", "bite", "punch", "scream" }),
                    ["Rise"] = Find(zc, new[] { "rise", "stand up", "standup", "getup", "get up", "agony" }),
                    ["Die"] = Find(zc, new[] { "death", "dying", "die" })
                };
                BuildController(ZombieControllerPath, clips, baseClip, true, log);
            }
        }

        static void BuildController(string path, Dictionary<string, AnimationClip> clips, AnimationClip fallback, bool zombie, Action<LogLevel, string> log)
        {
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Sliding", AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Jump", "Slide", "Stumble", "Die", "Attack", "Rise" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            var missing = new List<string>();
            foreach (var kv in clips)
            {
                var s = sm.AddState(kv.Key);
                s.motion = kv.Value != null ? kv.Value : fallback;
                if (kv.Value == null) missing.Add(kv.Key);
                states[kv.Key] = s;
            }

            AnimatorStateTransition AnyTo(string to, string trigger)
            {
                var tr = sm.AddAnyStateTransition(states[to]);
                tr.AddCondition(AnimatorConditionMode.If, 0, trigger);
                tr.hasExitTime = false; tr.duration = 0.08f; tr.canTransitionToSelf = false;
                return tr;
            }
            AnimatorStateTransition Link(string from, string to, float exitTime = -1f)
            {
                var tr = states[from].AddTransition(states[to]);
                tr.hasExitTime = exitTime >= 0f;
                if (exitTime >= 0f) tr.exitTime = exitTime;
                tr.duration = 0.15f;
                return tr;
            }

            if (!zombie)
            {
                sm.defaultState = states["Run"];
                Link("Idle", "Run").AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                Link("Run", "Idle").AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                AnyTo("Jump", "Jump");
                Link("Jump", "Run").AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                AnyTo("Slide", "Slide");
                Link("Slide", "Run").AddCondition(AnimatorConditionMode.IfNot, 0, "Sliding");
                AnyTo("Stumble", "Stumble");
                Link("Stumble", "Run", 0.85f);
                AnyTo("Die", "Die");
            }
            else
            {
                sm.defaultState = states["Walk"];
                Link("Walk", "Run").AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
                Link("Run", "Walk").AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
                AnyTo("Attack", "Attack");
                AnyTo("Rise", "Rise");
                Link("Rise", "Walk", 0.9f);
                AnyTo("Die", "Die");
            }
            EditorUtility.SetDirty(ctrl);
            log(LogLevel.Ok, $"Animator controller created: {path}");
            if (missing.Count > 0) log(LogLevel.Warning, $"{Path.GetFileName(path)}: no clip found for {string.Join(", ", missing)}; using '{fallback.name}' for those states. Add matching animation files to fix.");
        }

        // =====================================================================================
        // 7. PREFABS FROM IMPORTED MODELS
        // =====================================================================================
        public static void CreateImportedPrefabs(Action<LogLevel, string> log)
        {
            lastPlayerPrefabs = CharacterPrefabs(PlayerChars, "Player", AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath), log);
            lastZombiePrefabs = CharacterPrefabs(ZombieChars, "Zombie", AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieControllerPath), log);
            int props = 0;
            for (int t = 0; t < 4; t++)
                foreach (var kind in PropKinds)
                {
                    string src = ThemeDir(t) + "/" + (kind == "Decor" ? "Decor" : "Obstacles/" + kind);
                    string dst = PrefabDir + "/Themes/" + ThemeFolders[t] + "/" + kind;
                    foreach (var path in FilesIn(src, ModelExt))
                    {
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (model == null) continue;
                        MakeFolder(dst);
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                        CheckMaterials(inst, path, log);
                        PrefabUtility.SaveAsPrefabAsset(inst, dst + "/" + model.name + ".prefab");
                        UnityEngine.Object.DestroyImmediate(inst);
                        props++;
                    }
                }
            log(LogLevel.Ok, $"Prefabs: {lastPlayerPrefabs.Count} player, {lastZombiePrefabs.Count} zombie, {props} theme prop(s) from imported models.");
        }

        static List<string> CharacterPrefabs(string folder, string prefix, RuntimeAnimatorController ctrl, Action<LogLevel, string> log)
        {
            var list = new List<string>();
            foreach (var path in FilesIn(folder, ModelExt))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var anim = inst.GetComponentInChildren<Animator>();
                if (anim == null) anim = inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                if (anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
                    log(LogLevel.Warning, $"{model.name}: avatar is missing or not a valid Humanoid. Check the rig tab of {path}.");
                CheckMaterials(inst, path, log);
                string dst = PrefabDir + "/Characters/" + prefix + "_" + model.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(inst, dst);
                UnityEngine.Object.DestroyImmediate(inst);
                list.Add(dst);
            }
            return list;
        }

        static void CheckMaterials(GameObject go, string path, Action<LogLevel, string> log)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) continue;
                    string s = m.shader.name;
                    if (s == "Hidden/InternalErrorShader" || s == "Standard" || s.StartsWith("Legacy Shaders"))
                    {
                        log(LogLevel.Warning, $"{Path.GetFileName(path)} uses a non-URP material ({m.name}: {s}). Select it and use Edit > Rendering > Materials > Convert Selected Built-in Materials to URP.");
                        return;
                    }
                }
        }

        // =====================================================================================
        // 8. REGISTRY
        // =====================================================================================
        public static ZRAssetRegistry LoadOrCreateRegistry()
        {
            MakeFolder(ResourcesDir);
            var reg = AssetDatabase.LoadAssetAtPath<ZRAssetRegistry>(RegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<ZRAssetRegistry>();
                AssetDatabase.CreateAsset(reg, RegistryPath);
            }
            return reg;
        }

        static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

        public static void BuildRegistry(Action<LogLevel, string> log)
        {
            var reg = LoadOrCreateRegistry();
            reg.litOpaque = Load<Material>(MaterialsDir + "/ZR_LitOpaque.mat");
            reg.litTransparent = Load<Material>(MaterialsDir + "/ZR_LitTransparent.mat");
            reg.particleAlpha = Load<Material>(MaterialsDir + "/ZR_ParticleAlpha.mat");
            reg.particleAdditive = Load<Material>(MaterialsDir + "/ZR_ParticleAdditive.mat");
            reg.skyboxProcedural = Load<Material>(MaterialsDir + "/ZR_SkyProcedural.mat");
            reg.themeSkyboxes = new List<Material>();
            for (int t = 0; t < 4; t++) reg.themeSkyboxes.Add(Load<Material>(MaterialsDir + "/Sky_" + ThemeFolders[t] + ".mat"));
            reg.variantKeepers = new List<Material>();
            foreach (var k in new[] { "Keep_Normal", "Keep_Emission", "Keep_NormalEmission", "Keep_TransparentEmission" })
            {
                var m = Load<Material>(MaterialsDir + "/" + k + ".mat");
                if (m != null) reg.variantKeepers.Add(m);
            }
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            reg.uiFont = font;

            if (lastPlayerPrefabs.Count == 0) lastPlayerPrefabs = PrefabsIn(PrefabDir + "/Characters", "Player_");
            if (lastZombiePrefabs.Count == 0) lastZombiePrefabs = PrefabsIn(PrefabDir + "/Characters", "Zombie_");
            reg.playerModels = lastPlayerPrefabs.Select(Load<GameObject>).Where(g => g != null).ToList();
            reg.zombieModels = lastZombiePrefabs.Select(Load<GameObject>).Where(g => g != null).ToList();
            reg.playerController = Load<RuntimeAnimatorController>(PlayerControllerPath);
            reg.zombieController = Load<RuntimeAnimatorController>(ZombieControllerPath);

            reg.themes = new List<ThemeImportSet>();
            for (int t = 0; t < 4; t++)
            {
                var set = new ThemeImportSet { theme = (ThemeId)t };
                FindPbr(ThemeDir(t) + "/Ground", out set.groundAlbedo, out set.groundNormal);
                FindPbr(ThemeDir(t) + "/Verge", out set.vergeAlbedo, out set.vergeNormal);
                string pf = PrefabDir + "/Themes/" + ThemeFolders[t] + "/";
                set.decor = PrefabsIn(pf + "Decor", "").Select(Load<GameObject>).Where(g => g != null).ToList();
                set.jumpObstacles = PrefabsIn(pf + "Jump", "").Select(Load<GameObject>).Where(g => g != null).ToList();
                set.slideObstacles = PrefabsIn(pf + "Slide", "").Select(Load<GameObject>).Where(g => g != null).ToList();
                set.blockObstacles = PrefabsIn(pf + "Block", "").Select(Load<GameObject>).Where(g => g != null).ToList();
                set.skyboxOverride = Load<Material>(MaterialsDir + "/Sky_" + ThemeFolders[t] + "_HDRI.mat");
                reg.themes.Add(set);
                int imported = set.decor.Count + set.jumpObstacles.Count + set.slideObstacles.Count + set.blockObstacles.Count;
                log(LogLevel.Info, $"{ThemeFolders[t]}: {imported} imported prop(s), ground textures {(set.groundAlbedo != null ? "imported" : "procedural")}, sky {(set.skyboxOverride != null ? "HDRI" : "procedural")}.");
            }

            reg.audio = new List<AudioOverride>();
            var known = new HashSet<string>(AudioKeys.All());
            var unknown = new List<string>();
            foreach (var path in FilesIn(AudioDir, AudioExt))
            {
                string key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var clip = Load<AudioClip>(path);
                if (clip == null) continue;
                if (known.Contains(key)) reg.audio.Add(new AudioOverride { key = key, clip = clip });
                else unknown.Add(Path.GetFileName(path));
            }
            if (unknown.Count > 0) log(LogLevel.Warning, "Audio files not matching a known key (rename them to a key from README): " + string.Join(", ", unknown));
            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssets();
            log(LogLevel.Ok, $"Registry saved: {reg.playerModels.Count} player model(s), {reg.zombieModels.Count} zombie model(s), {reg.audio.Count} audio override(s). Everything else is procedural.");
        }

        static List<string> PrefabsIn(string folder, string prefix)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new List<string>();
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith(prefix)).Distinct().ToList();
        }

        // =====================================================================================
        // 9. SCENES
        // =====================================================================================
        public static void CreateScenes(Action<LogLevel, string> log)
        {
            MakeFolder(SceneDir);
            var reg = LoadOrCreateRegistry();
            if (reg.litOpaque == null) log(LogLevel.Warning, "Registry has no base materials yet; run Materials and Registry steps first for build-safe shaders.");

            var paths = new List<string>();
            foreach (var name in SceneFlow.All)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ThemeId theme = ThemeId.Forest;
                bool isLevel = name != SceneFlow.MainMenu && name != SceneFlow.ThemeSelection;
                if (isLevel) for (int t = 0; t < 4; t++) if (ThemeDatabase.Get((ThemeId)t).sceneName == name) theme = (ThemeId)t;
                var profile = ThemeDatabase.Get(theme);
                MaterialLibrary.ClearCache();
                ThemeLighting.ApplyEnvironment(profile);
                if (reg.themeSkyboxes != null && reg.themeSkyboxes.Count > (int)theme && reg.themeSkyboxes[(int)theme] != null) RenderSettings.skybox = reg.themeSkyboxes[(int)theme];

                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0, 3.3f, -6.4f);
                camGo.transform.rotation = Quaternion.Euler(14f, 0, 0);
                var cam = camGo.GetComponent<Camera>();
                cam.farClipPlane = 450f;
                var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;

                var sunGo = new GameObject("Sun", typeof(Light));
                var sun = ThemeLighting.SetupSun(sunGo.GetComponent<Light>(), profile);

                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
                if (isLevel)
                {
                    var boot = new GameObject("LevelBootstrap").AddComponent<LevelBootstrap>();
                    boot.theme = theme; boot.mainCamera = cam; boot.sun = sun;
                }
                else
                {
                    var boot = new GameObject("MenuBootstrap").AddComponent<MenuBootstrap>();
                    boot.screen = name == SceneFlow.MainMenu ? MenuScreen.MainMenu : MenuScreen.ThemeSelection;
                    boot.mainCamera = cam; boot.sun = sun;
                }
                var note = new GameObject("_README (content is generated at Play time)");
                note.tag = "EditorOnly";

                string path = ScenePath(name);
                EditorSceneManager.SaveScene(scene, path);
                paths.Add(path);
                log(LogLevel.Ok, "Scene saved: " + path);
            }

            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in paths) list.Add(new EditorBuildSettingsScene(p, true));
            foreach (var s in EditorBuildSettings.scenes) if (!paths.Contains(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
            log(LogLevel.Ok, "Build Settings scene list updated (MainMenu is scene 0).");
            EditorSceneManager.OpenScene(ScenePath(SceneFlow.MainMenu));
        }

        // =====================================================================================
        // 10. PLAYER SETTINGS
        // =====================================================================================
        public static void ConfigurePlayerSettings(Action<LogLevel, string> log)
        {
            PlayerSettings.productName = "Zombie Apocalypse Runner";
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "ZombieRunnerStudio";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
#if UNITY_2021_2_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            if (PlayerSettings.GetApplicationIdentifier(android).Contains("DefaultCompany") || string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(android)))
                PlayerSettings.SetApplicationIdentifier(android, "com.zombierunnerstudio.zombieapocalypserunner");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
#else
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android).Contains("DefaultCompany"))
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.zombierunnerstudio.zombieapocalypserunner");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            log(LogLevel.Ok, "Player settings: product name, linear colour space, landscape orientation, Android IL2CPP + ARM64, application id.");
        }

        // =====================================================================================
        // 11. VALIDATION
        // =====================================================================================
        public static void Validate(Action<LogLevel, string> log)
        {
            int errors = 0;
            void Err(string s) { errors++; log(LogLevel.Error, s); }

            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null) Err("URP is not the active render pipeline.");

            var reg = AssetDatabase.LoadAssetAtPath<ZRAssetRegistry>(RegistryPath);
            if (reg == null) Err("Missing " + RegistryPath + ". Run the Registry step.");
            else
            {
                void CheckMat(Material m, string n)
                {
                    if (m == null) { Err($"Registry: {n} is not assigned."); return; }
                    if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported) Err($"Registry: {n} has an unsupported shader ({(m.shader != null ? m.shader.name : "none")}).");
                }
                CheckMat(reg.litOpaque, "Lit Opaque material");
                CheckMat(reg.litTransparent, "Lit Transparent material");
                CheckMat(reg.particleAlpha, "Particle Alpha material");
                CheckMat(reg.particleAdditive, "Particle Additive material");
                if (reg.uiFont == null) log(LogLevel.Warning, "Registry: no UI font assigned; the OS font fallback will be used.");
                if (reg.playerModels.Count > 0 && reg.playerController == null) Err("Player models are registered but there is no player animator controller.");
                if (reg.zombieModels.Count > 0 && reg.zombieController == null) Err("Zombie models are registered but there is no zombie animator controller.");
                foreach (var m in reg.playerModels.Concat(reg.zombieModels))
                {
                    if (m == null) { Err("Registry has an empty character slot."); continue; }
                    var a = m.GetComponentInChildren<Animator>();
                    if (a == null || a.avatar == null || !a.avatar.isHuman) log(LogLevel.Warning, $"{m.name}: not a Humanoid rig; imported animations may not play correctly.");
                }
                CheckController(reg.playerController, log);
                CheckController(reg.zombieController, log);
            }

            var buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < SceneFlow.All.Length; i++)
            {
                string name = SceneFlow.All[i];
                string path = ScenePath(name);
                if (!File.Exists(path)) { Err($"Scene missing: {path}. Run the Scenes step."); continue; }
                var entry = buildScenes.FirstOrDefault(s => s.path == path);
                if (entry == null || !entry.enabled) Err($"Scene {name} is not enabled in Build Settings.");
                if (i == 0 && (buildScenes.Length == 0 || buildScenes[0].path != path)) log(LogLevel.Warning, "MainMenu should be the first scene in Build Settings.");
            }

            // Missing scripts and bootstrap components in each scene
            foreach (var name in SceneFlow.All)
            {
                string path = ScenePath(name);
                if (!File.Exists(path)) continue;
                var existing = SceneManager.GetSceneByPath(path);
                bool wasLoaded = existing.IsValid() && existing.isLoaded;
                var scene = wasLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int missing = 0; bool hasBoot = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (root.GetComponent<LevelBootstrap>() != null || root.GetComponent<MenuBootstrap>() != null) hasBoot = true;
                }
                if (missing > 0) Err($"{name}: {missing} missing script reference(s).");
                if (!hasBoot) Err($"{name}: no LevelBootstrap/MenuBootstrap found.");
                if (missing == 0 && hasBoot) log(LogLevel.Ok, $"{name}: OK");
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }

            // Every audio key resolves (imported or procedural)
            log(LogLevel.Ok, "Audio: every sound key has a procedural fallback; " + (reg != null ? reg.audio.Count : 0) + " key(s) use imported recordings.");

            if (errors == 0) log(LogLevel.Ok, "Validation passed. Open the MainMenu scene and press Play.");
            else log(LogLevel.Error, $"Validation found {errors} problem(s).");
        }

        static void CheckController(RuntimeAnimatorController c, Action<LogLevel, string> log)
        {
            if (c == null) return;
            foreach (var clip in c.animationClips)
                if (clip == null) log(LogLevel.Warning, c.name + " has a state with no animation clip.");
        }

        // =====================================================================================
        // OPTIONAL: export procedural models as real assets
        // =====================================================================================
        public static void ExportProceduralPrefabs(Action<LogLevel, string> log)
        {
            string outDir = PrefabDir + "/Procedural";
            MakeFolder(outDir);
            MakeFolder(GeneratedDir + "/Meshes"); MakeFolder(GeneratedDir + "/Materials"); MakeFolder(GeneratedDir + "/Textures");
            MaterialLibrary.ClearCache();
            int count = 0;
            try
            {
                for (int t = 0; t < 4; t++)
                {
                    var p = ThemeDatabase.Get((ThemeId)t);
                    string dir = outDir + "/" + ThemeFolders[t];
                    MakeFolder(dir);
                    foreach (var def in p.obstacles)
                    {
                        var root = new GameObject(def.kind.ToString());
                        var vis = PropFactory.Obstacle(def.kind, 1000 + (int)def.kind);
                        vis.transform.SetParent(root.transform, false);
                        Obstacle.Setup(root, def.type, def.kind.ToString(), vis.transform);
                        Save(root, dir + "/" + def.kind + ".prefab"); count++;
                    }
                    var zombie = ZombieAgent.Build(p, ZombieKind.Walker, 5);
                    Save(zombie, dir + "/Zombie_Walker.prefab"); count++;
                    var brute = ZombieAgent.Build(p, ZombieKind.Brute, 6);
                    Save(brute, dir + "/Zombie_Brute.prefab"); count++;
                }
                string pick = outDir + "/Pickups"; MakeFolder(pick);
                Save(PickupFactory.Coin(), pick + "/Coin.prefab"); count++;
                for (int i = 0; i < 5; i++) { Save(PickupFactory.PowerUp((PowerUpType)i), pick + "/PowerUp_" + (PowerUpType)i + ".prefab"); count++; }
                string chars = outDir + "/Survivors"; MakeFolder(chars);
                for (int i = 0; i < 3; i++)
                {
                    var h = ProceduralHumanoid.Create(null, CharacterCatalog.Survivor(i), CharacterCatalog.SurvivorNames[i]);
                    Save(h.gameObject, chars + "/" + CharacterCatalog.SurvivorNames[i] + ".prefab"); count++;
                }
                string decor = outDir + "/Decor"; MakeFolder(decor);
                Save(PropFactory.PineTree(1, 16f).Build("PineTree"), decor + "/PineTree.prefab");
                Save(PropFactory.BroadTree(2, 11f).Build("BroadTree"), decor + "/BroadTree.prefab");
                Save(PropFactory.Building(3, 12f, 30f, 14f, true, 0f, false).Build("CityBuilding"), decor + "/CityBuilding.prefab");
                Save(PropFactory.Building(4, 14f, 50f, 16f, false, 0.6f, true).Build("RuinedSkyscraper"), decor + "/RuinedSkyscraper.prefab");
                Save(PropFactory.Car(5, Palette.CarPaint(1), true, false).Build("WreckedCar"), decor + "/WreckedCar.prefab");
                Save(PropFactory.Bus(6, Palette.Rust, true, true).Build("OvergrownBus"), decor + "/OvergrownBus.prefab");
                Save(PropFactory.MilitaryTruck(7, Palette.DesertMilitary).Build("MilitaryTruck"), decor + "/MilitaryTruck.prefab");
                Save(PropFactory.Tank(8, Palette.DesertMilitary).Build("Tank"), decor + "/Tank.prefab");
                Save(PropFactory.StreetLight(9, false).Build("StreetLight"), decor + "/StreetLight.prefab");
                Save(PropFactory.Watchtower(10).Build("Watchtower"), decor + "/Watchtower.prefab");
                Save(PropFactory.Shack(11).Build("Shack"), decor + "/Shack.prefab");
                count += 11;
            }
            finally
            {
                AssetDatabase.SaveAssets();
                MaterialLibrary.ClearCache();
            }
            log(LogLevel.Ok, $"Exported {count} procedural prefabs with their meshes, materials and textures to {outDir} and {GeneratedDir}.");
        }

        static void Save(GameObject go, string path)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh))
                    AssetDatabase.CreateAsset(mf.sharedMesh, AssetDatabase.GenerateUniqueAssetPath(GeneratedDir + "/Meshes/" + Clean(mf.sharedMesh.name) + ".asset"));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    foreach (var prop in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_EmissionMap" })
                    {
                        if (!m.HasProperty(prop)) continue;
                        var tex = m.GetTexture(prop);
                        if (tex != null && !AssetDatabase.Contains(tex))
                            AssetDatabase.CreateAsset(tex, AssetDatabase.GenerateUniqueAssetPath(GeneratedDir + "/Textures/" + Clean(tex.name) + ".asset"));
                    }
                    AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath(GeneratedDir + "/Materials/" + Clean(m.name) + ".mat"));
                }
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) s = "Generated";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        // =====================================================================================
        // EDITOR PREVIEW (not saved with the scene)
        // =====================================================================================
        const string PreviewName = "ZR_EditorPreview (not saved)";

        public static void PreviewEnvironmentInScene(Action<LogLevel, string> log)
        {
            ClearPreview();
            ThemeId theme = ThemeId.Forest;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var lb = root.GetComponent<LevelBootstrap>();
                if (lb != null) { theme = lb.theme; break; }
            }
            var profile = ThemeDatabase.Get(theme);
            MaterialLibrary.ClearCache();
            var go = new GameObject(PreviewName);
            var follow = new GameObject("PreviewFocus").transform;
            follow.SetParent(go.transform, false);
            follow.localPosition = new Vector3(0, 0, 40f);
            var env = new GameObject("Environment").AddComponent<EnvironmentGenerator>();
            env.transform.SetParent(go.transform, false);
            env.Build(profile, follow, 4, 4, false);
            int i = 0;
            foreach (var def in profile.obstacles)
            {
                var o = PropFactory.Obstacle(def.kind, 50 + i);
                o.transform.SetParent(go.transform, false);
                o.transform.localPosition = new Vector3(ZRUtil.LaneX(i % 3 - 1), 0, 20f + i * 9f);
                i++;
            }
            var survivor = ProceduralHumanoid.Create(go.transform, CharacterCatalog.Survivor(0), "Survivor");
            survivor.transform.localPosition = new Vector3(0, 0, 10f);
            ThemeLighting.ApplyEnvironment(profile);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.LookAt(new Vector3(0, 2f, 30f), Quaternion.Euler(12f, 0, 0), 28f);
            log(LogLevel.Ok, $"Preview of {profile.displayName} built in the Scene view. It is not saved with the scene. Lighting/fog changes made for the preview are; run Scenes again to reset them if needed.");
        }

        public static void ClearPreview()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == PreviewName) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
