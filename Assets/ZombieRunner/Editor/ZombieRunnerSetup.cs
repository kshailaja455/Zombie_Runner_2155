using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieRunner.EditorTools
{
    public enum LogLevel { Info, Ok, Warning, Error }

    public struct SetupMessage
    {
        public LogLevel level;
        public string text;
        public SetupMessage(LogLevel l, string t) { level = l; text = t; }
    }

    /// <summary>
    /// ZombieRunnerSetup: one window that turns the imported files into a ready-to-run project.
    /// Tools > Zombie Runner > Setup Wizard. "Run Full Setup" executes every step in order and then validates.
    /// </summary>
    public class ZombieRunnerSetup : EditorWindow
    {
        Vector2 scroll;
        static readonly List<SetupMessage> log = new List<SetupMessage>();
        bool exportProcedural;
        GUIStyle wrap;

        [MenuItem("Tools/Zombie Runner/Setup Wizard", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<ZombieRunnerSetup>("ZombieRunnerSetup");
            w.minSize = new Vector2(560, 620);
        }

        [MenuItem("Tools/Zombie Runner/Run Full Setup", priority = 1)]
        public static void RunFullSetupMenu() => RunFullSetup(false);

        [MenuItem("Tools/Zombie Runner/Validate Project", priority = 2)]
        public static void ValidateMenu() { log.Clear(); ZRSetup.Validate(Log); ShowSummary("Validation"); }

        [MenuItem("Tools/Zombie Runner/Open Main Menu Scene and Play", priority = 20)]
        public static void PlayFromMenu()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = ZRSetup.ScenePath(SceneFlow.MainMenu);
            if (!System.IO.File.Exists(path)) { EditorUtility.DisplayDialog("Zombie Runner", "MainMenu scene not found. Run the Setup Wizard first.", "OK"); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Zombie Runner/Preview Level Environment In Scene View", priority = 21)]
        public static void PreviewEnvironment() => ZRSetup.PreviewEnvironmentInScene(Log);

        [MenuItem("Tools/Zombie Runner/Clear Scene View Preview", priority = 22)]
        public static void ClearPreview() => ZRSetup.ClearPreview();

        public static void Log(LogLevel level, string text)
        {
            log.Add(new SetupMessage(level, text));
            string line = "[ZombieRunnerSetup] " + text;
            if (level == LogLevel.Error) Debug.LogError(line);
            else if (level == LogLevel.Warning) Debug.LogWarning(line);
            else Debug.Log(line);
        }

        public static void RunFullSetup(bool exportProceduralPrefabs)
        {
            log.Clear();
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Log(LogLevel.Warning, "Setup cancelled."); return; }
            try
            {
                Step(0.02f, "Checking project", () => ZRSetup.CheckProject(Log));
                Step(0.08f, "Creating folders", () => ZRSetup.EnsureFolders(Log));
                Step(0.14f, "Organising imported assets", () => ZRSetup.OrganizeInbox(Log));
                Step(0.24f, "Configuring importers", () => ZRSetup.ConfigureImporters(Log));
                Step(0.36f, "Creating materials", () => ZRSetup.CreateMaterials(Log));
                Step(0.46f, "Creating animation controllers", () => ZRSetup.CreateAnimators(Log));
                Step(0.56f, "Creating prefabs", () => ZRSetup.CreateImportedPrefabs(Log));
                Step(0.66f, "Filling the asset registry", () => ZRSetup.BuildRegistry(Log));
                if (exportProceduralPrefabs) Step(0.72f, "Exporting procedural prefabs", () => ZRSetup.ExportProceduralPrefabs(Log));
                Step(0.8f, "Creating scenes", () => ZRSetup.CreateScenes(Log));
                Step(0.9f, "Player & build settings", () => ZRSetup.ConfigurePlayerSettings(Log));
                Step(0.95f, "Validating", () => ZRSetup.Validate(Log));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            ShowSummary("Setup");
        }

        static void Step(float p, string title, System.Action a)
        {
            EditorUtility.DisplayProgressBar("Zombie Runner Setup", title + "...", p);
            try { a(); }
            catch (System.Exception e)
            {
                Log(LogLevel.Error, title + " failed: " + e.Message);
                Debug.LogException(e);
            }
        }

        static void ShowSummary(string what)
        {
            int errors = log.Count(m => m.level == LogLevel.Error);
            int warnings = log.Count(m => m.level == LogLevel.Warning);
            string msg = errors == 0
                ? $"{what} finished with {warnings} warning(s).\n\nOpen Assets/ZombieRunner/Scenes/MainMenu and press Play, or use Tools > Zombie Runner > Open Main Menu Scene and Play."
                : $"{what} finished with {errors} error(s) and {warnings} warning(s).\n\nSee the Setup Wizard log (Tools > Zombie Runner > Setup Wizard) for what to fix.";
            EditorUtility.DisplayDialog("Zombie Runner", msg, "OK");
            var windows = Resources.FindObjectsOfTypeAll<ZombieRunnerSetup>();
            foreach (var w in windows) w.Repaint();
        }

        void OnGUI()
        {
            if (wrap == null) wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ZOMBIE APOCALYPSE RUNNER · Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "1. Use a Unity 6 project created from the Universal 3D (URP) template.\n" +
                "2. Optional: drop imported models / animations / textures / audio into Assets/ZombieRunner/Imported/_Inbox (or the typed folders described in README).\n" +
                "3. Press Run Full Setup. Everything that is not imported is generated procedurally, so the game runs even with no imports.", wrap);
            EditorGUILayout.Space(8);

            exportProcedural = EditorGUILayout.ToggleLeft("Also export procedural models as prefab/mesh/material assets (optional, slower)", exportProcedural);
            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.3f);
            if (GUILayout.Button("RUN FULL SETUP", GUILayout.Height(40))) RunFullSetup(exportProcedural);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Individual steps", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Folders")) Single(() => ZRSetup.EnsureFolders(Log));
                if (GUILayout.Button("Organise Inbox")) Single(() => ZRSetup.OrganizeInbox(Log));
                if (GUILayout.Button("Importers")) Single(() => ZRSetup.ConfigureImporters(Log));
                if (GUILayout.Button("Materials")) Single(() => ZRSetup.CreateMaterials(Log));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Animators")) Single(() => ZRSetup.CreateAnimators(Log));
                if (GUILayout.Button("Prefabs")) Single(() => ZRSetup.CreateImportedPrefabs(Log));
                if (GUILayout.Button("Registry")) Single(() => ZRSetup.BuildRegistry(Log));
                if (GUILayout.Button("Scenes")) Single(() => ZRSetup.CreateScenes(Log));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Player Settings")) Single(() => ZRSetup.ConfigurePlayerSettings(Log));
                if (GUILayout.Button("Export Procedural Prefabs")) Single(() => ZRSetup.ExportProceduralPrefabs(Log));
                if (GUILayout.Button("Validate")) Single(() => ZRSetup.Validate(Log));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview level in Scene view")) PreviewEnvironment();
                if (GUILayout.Button("Clear preview")) ClearPreview();
                if (GUILayout.Button("Play from Main Menu")) PlayFromMenu();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Log ({log.Count})", EditorStyles.miniBoldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, "box");
            foreach (var m in log)
            {
                string color = m.level == LogLevel.Error ? "#ff6b5e" : m.level == LogLevel.Warning ? "#ffc857" : m.level == LogLevel.Ok ? "#7fd67f" : "#c8c8c8";
                string tag = m.level == LogLevel.Error ? "ERROR" : m.level == LogLevel.Warning ? "WARN" : m.level == LogLevel.Ok ? "OK" : "INFO";
                EditorGUILayout.LabelField($"<color={color}><b>{tag}</b></color>  {m.text}", wrap);
            }
            EditorGUILayout.EndScrollView();
        }

        void Single(System.Action a)
        {
            log.Clear();
            try { a(); }
            catch (System.Exception e) { Log(LogLevel.Error, e.Message); Debug.LogException(e); }
            finally { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
        }
    }
}
