using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieRunner
{
    /// <summary>Scene names and transitions. The setup tool adds these scenes to Build Settings in this order.</summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string ThemeSelection = "ThemeSelection";
        public const string Forest = "ForestLevel";
        public const string Desert = "DesertLevel";
        public const string DarkCity = "DarkCityLevel";
        public const string AbandonedCity = "AbandonedCityLevel";

        public static readonly string[] All = { MainMenu, ThemeSelection, Forest, Desert, DarkCity, AbandonedCity };

        public static string LevelScene(ThemeId id) => ThemeDatabase.Get(id).sceneName;

        public static void LoadMainMenu() => Load(MainMenu);
        public static void LoadThemeSelection() => Load(ThemeSelection);

        public static void LoadLevel(ThemeId id)
        {
            GameSettings.SelectedTheme = id;
            Load(LevelScene(id));
        }

        public static void ReloadCurrent() => Load(SceneManager.GetActiveScene().name);

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[ZombieRunner] Scene '{scene}' is not in Build Settings. Run Tools > Zombie Runner > Setup Wizard > Run Full Setup.");
                return;
            }
            SceneManager.LoadScene(scene);
        }
    }
}
