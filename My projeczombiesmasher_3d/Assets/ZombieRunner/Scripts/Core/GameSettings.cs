using UnityEngine;

namespace ZombieRunner
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2, Auto = 3 }

    /// <summary>All persistent player data, stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string KMusic = "zr_music", KSfx = "zr_sfx", KMute = "zr_mute", KQuality = "zr_quality",
            KTheme = "zr_theme", KChar = "zr_char", KBest = "zr_best", KBestTheme = "zr_best_", KCoins = "zr_coins_total",
            KFps = "zr_fps", KRuns = "zr_runs", KBestDist = "zr_bestdist";

        public static float MusicVolume { get => PlayerPrefs.GetFloat(KMusic, 0.7f); set { PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); Changed(); } }
        public static float SfxVolume { get => PlayerPrefs.GetFloat(KSfx, 0.9f); set { PlayerPrefs.SetFloat(KSfx, Mathf.Clamp01(value)); Changed(); } }
        public static bool SoundOn { get => PlayerPrefs.GetInt(KMute, 1) == 1; set { PlayerPrefs.SetInt(KMute, value ? 1 : 0); Changed(); } }
        public static bool ShowFps { get => PlayerPrefs.GetInt(KFps, 0) == 1; set { PlayerPrefs.SetInt(KFps, value ? 1 : 0); Changed(); } }

        public static QualityTier Quality
        {
            get => (QualityTier)PlayerPrefs.GetInt(KQuality, (int)QualityTier.Auto);
            set { PlayerPrefs.SetInt(KQuality, (int)value); Changed(); }
        }

        public static ThemeId SelectedTheme
        {
            get => (ThemeId)Mathf.Clamp(PlayerPrefs.GetInt(KTheme, 0), 0, 3);
            set { PlayerPrefs.SetInt(KTheme, (int)value); PlayerPrefs.Save(); }
        }

        public static int SelectedCharacter
        {
            get => PlayerPrefs.GetInt(KChar, 0);
            set { PlayerPrefs.SetInt(KChar, value); PlayerPrefs.Save(); }
        }

        public static int BestScore => PlayerPrefs.GetInt(KBest, 0);
        public static int BestDistance => PlayerPrefs.GetInt(KBestDist, 0);
        public static int BestScoreFor(ThemeId t) => PlayerPrefs.GetInt(KBestTheme + (int)t, 0);
        public static int TotalCoins => PlayerPrefs.GetInt(KCoins, 0);
        public static int TotalRuns => PlayerPrefs.GetInt(KRuns, 0);

        /// <summary>Saves a finished run. Returns true when it is a new best for that theme.</summary>
        public static bool SubmitRun(ThemeId theme, int score, int distance, int coins)
        {
            bool newBest = score > BestScoreFor(theme);
            if (newBest) PlayerPrefs.SetInt(KBestTheme + (int)theme, score);
            if (score > BestScore) PlayerPrefs.SetInt(KBest, score);
            if (distance > BestDistance) PlayerPrefs.SetInt(KBestDist, distance);
            PlayerPrefs.SetInt(KCoins, TotalCoins + coins);
            PlayerPrefs.SetInt(KRuns, TotalRuns + 1);
            PlayerPrefs.Save();
            return newBest;
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(KBest);
            PlayerPrefs.DeleteKey(KBestDist);
            PlayerPrefs.DeleteKey(KCoins);
            PlayerPrefs.DeleteKey(KRuns);
            for (int i = 0; i < 4; i++) PlayerPrefs.DeleteKey(KBestTheme + i);
            PlayerPrefs.Save();
            Changed();
        }

        public static event System.Action OnChanged;
        static void Changed() { PlayerPrefs.Save(); OnChanged?.Invoke(); }
    }
}
