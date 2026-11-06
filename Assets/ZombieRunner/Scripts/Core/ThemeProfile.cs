using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    public enum ThemeId { Forest = 0, Desert = 1, DarkCity = 2, AbandonedCity = 3 }

    /// <summary>How the player must deal with an obstacle.</summary>
    public enum ObstacleType
    {
        Jump,   // low: jump over it
        Slide,  // overhead: slide under it
        Block,  // full height: change lane
        Pit,    // hole in the ground: jump over it
        Zombie  // a zombie standing in the lane: change lane
    }

    /// <summary>Every procedural prop the game can build. Obstacles and decor share this list.</summary>
    public enum PropKind
    {
        // Forest
        FallenTree, LargeRock, WoodenBarricade, ForestWreck, MudPit, LowBranch,
        // Desert
        DestroyedCar, SandPit, RockFormation, MilitaryBarricade, AbandonedTruck, CollapsedArch,
        // Dark city
        AbandonedCar, PoliceRoadblock, FallenStreetlight, Scaffolding, ConstructionBarrier, BurningCar,
        // Abandoned city
        BrokenBus, RubblePile, ConcreteBarrier, OverturnedCar, RoadHole, FallenGirder
    }

    public enum RoadStyle { Mud, DesertRoad, WetAsphalt, CrackedAsphalt }
    public enum WeatherStyle { ForestFog, Sandstorm, NightRain, DustAndSmoke }
    public enum AmbienceStyle { Forest, DesertWind, CityRain, RuinedCity }

    [System.Serializable]
    public struct ObstacleDef
    {
        public PropKind kind;
        public ObstacleType type;
        public float weight;
        public ObstacleDef(PropKind k, ObstacleType t, float w) { kind = k; type = t; weight = w; }
    }

    /// <summary>
    /// Everything that makes a theme look, sound and play differently.
    /// Profiles are defined in code so the game works with zero Inspector wiring.
    /// </summary>
    public class ThemeProfile
    {
        public ThemeId id;
        public string displayName;
        public string tagline;
        public string sceneName;

        // Sky and light
        public Color skyTint, skyGround, sunColor, ambientSky, ambientEquator, ambientGround, fogColor;
        public float skyExposure, atmosphere, sunIntensity, fogDensity;
        public Vector3 sunEuler;
        public bool night;

        // Post processing
        public float postExposure, contrast, saturation, bloomIntensity, bloomThreshold, vignette, filmGrain;
        public Color colorFilter;

        // Ground
        public RoadStyle road;
        public Color roadColor, verge, vergeAlt;

        // Atmosphere
        public WeatherStyle weather;
        public AmbienceStyle ambience;
        public int musicSeed;

        // Zombies
        public Color[] zombieSkins;
        public Color[] zombieClothes;
        public float zombieDensity;

        public List<ObstacleDef> obstacles = new List<ObstacleDef>();
        public Color cardAccent;
    }

    public static class ThemeDatabase
    {
        static ThemeProfile[] profiles;

        public static ThemeProfile Get(ThemeId id)
        {
            if (profiles == null) Build();
            return profiles[(int)id];
        }

        public static int Count => 4;

        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static void Build()
        {
            profiles = new ThemeProfile[4];

            // ---------------- THEME 1: DENSE ZOMBIE FOREST ----------------
            var f = new ThemeProfile
            {
                id = ThemeId.Forest,
                displayName = "Dense Zombie Forest",
                tagline = "Mud, fog and the dead between the pines.",
                sceneName = SceneFlow.Forest,
                skyTint = C("#8FA7A0"), skyGround = C("#3B4035"), skyExposure = 1.05f, atmosphere = 1.3f,
                sunColor = C("#FFE2B0"), sunIntensity = 1.35f, sunEuler = new Vector3(28f, -35f, 0f),
                ambientSky = C("#7D8F86"), ambientEquator = C("#4B5A44"), ambientGround = C("#262A1E"),
                fogColor = C("#7E8C80"), fogDensity = 0.022f,
                postExposure = 0.1f, contrast = 12f, saturation = -18f, bloomIntensity = 0.6f, bloomThreshold = 1.0f,
                vignette = 0.32f, filmGrain = 0.15f, colorFilter = C("#F2F5E8"),
                road = RoadStyle.Mud, roadColor = C("#5A4632"), verge = C("#3D4A2A"), vergeAlt = C("#4E4430"),
                weather = WeatherStyle.ForestFog, ambience = AmbienceStyle.Forest, musicSeed = 101,
                zombieSkins = new[] { C("#8C9A7E"), C("#9AA38A"), C("#7F8B74") },
                zombieClothes = new[] { C("#4A5240"), C("#5C4B3A"), C("#3E4A52"), C("#6B5E4A") },
                zombieDensity = 1.0f, cardAccent = C("#7FAF5A")
            };
            f.obstacles.Add(new ObstacleDef(PropKind.FallenTree, ObstacleType.Jump, 3f));
            f.obstacles.Add(new ObstacleDef(PropKind.LargeRock, ObstacleType.Block, 2f));
            f.obstacles.Add(new ObstacleDef(PropKind.WoodenBarricade, ObstacleType.Jump, 2f));
            f.obstacles.Add(new ObstacleDef(PropKind.ForestWreck, ObstacleType.Block, 1.5f));
            f.obstacles.Add(new ObstacleDef(PropKind.MudPit, ObstacleType.Pit, 1.5f));
            f.obstacles.Add(new ObstacleDef(PropKind.LowBranch, ObstacleType.Slide, 2.5f));
            profiles[0] = f;

            // ---------------- THEME 2: POST-APOCALYPTIC DESERT ----------------
            var d = new ThemeProfile
            {
                id = ThemeId.Desert,
                displayName = "Post-Apocalyptic Desert",
                tagline = "Heat, sand and a horde inside the storm.",
                sceneName = SceneFlow.Desert,
                skyTint = C("#C9A27A"), skyGround = C("#8A6A48"), skyExposure = 1.35f, atmosphere = 2.2f,
                sunColor = C("#FFD7A0"), sunIntensity = 1.9f, sunEuler = new Vector3(42f, 60f, 0f),
                ambientSky = C("#C7A989"), ambientEquator = C("#A7835E"), ambientGround = C("#5E4A36"),
                fogColor = C("#C8A57E"), fogDensity = 0.018f,
                postExposure = 0.15f, contrast = 14f, saturation = -6f, bloomIntensity = 0.8f, bloomThreshold = 0.95f,
                vignette = 0.25f, filmGrain = 0.1f, colorFilter = C("#FFEBD2"),
                road = RoadStyle.DesertRoad, roadColor = C("#5C5650"), verge = C("#C9A16F"), vergeAlt = C("#B38A5C"),
                weather = WeatherStyle.Sandstorm, ambience = AmbienceStyle.DesertWind, musicSeed = 202,
                zombieSkins = new[] { C("#A08C76"), C("#9B8A73"), C("#8E7D6A") },
                zombieClothes = new[] { C("#7A6A50"), C("#5B5A44"), C("#8A7358"), C("#6E5B4A") },
                zombieDensity = 0.95f, cardAccent = C("#E0A85A")
            };
            d.obstacles.Add(new ObstacleDef(PropKind.DestroyedCar, ObstacleType.Block, 2.5f));
            d.obstacles.Add(new ObstacleDef(PropKind.SandPit, ObstacleType.Pit, 1.5f));
            d.obstacles.Add(new ObstacleDef(PropKind.RockFormation, ObstacleType.Block, 1.5f));
            d.obstacles.Add(new ObstacleDef(PropKind.MilitaryBarricade, ObstacleType.Jump, 3f));
            d.obstacles.Add(new ObstacleDef(PropKind.AbandonedTruck, ObstacleType.Block, 1.2f));
            d.obstacles.Add(new ObstacleDef(PropKind.CollapsedArch, ObstacleType.Slide, 2.5f));
            profiles[1] = d;

            // ---------------- THEME 3: SPOOKY DARK CITY ----------------
            var n = new ThemeProfile
            {
                id = ThemeId.DarkCity,
                displayName = "Spooky Dark City",
                tagline = "Wet streets, dead neon, moving shadows.",
                sceneName = SceneFlow.DarkCity,
                skyTint = C("#1C2433"), skyGround = C("#07090D"), skyExposure = 0.35f, atmosphere = 0.6f,
                sunColor = C("#8FA6D6"), sunIntensity = 0.45f, sunEuler = new Vector3(35f, 150f, 0f), night = true,
                ambientSky = C("#1E2A40"), ambientEquator = C("#141A26"), ambientGround = C("#0A0C10"),
                fogColor = C("#1A2130"), fogDensity = 0.03f,
                postExposure = 0.35f, contrast = 18f, saturation = -10f, bloomIntensity = 1.6f, bloomThreshold = 0.8f,
                vignette = 0.42f, filmGrain = 0.3f, colorFilter = C("#D8E2FF"),
                road = RoadStyle.WetAsphalt, roadColor = C("#2A2C30"), verge = C("#3A3C40"), vergeAlt = C("#303236"),
                weather = WeatherStyle.NightRain, ambience = AmbienceStyle.CityRain, musicSeed = 303,
                zombieSkins = new[] { C("#8A9488"), C("#7E8A86"), C("#949A90") },
                zombieClothes = new[] { C("#2E3440"), C("#3E3A40"), C("#4A3A3A"), C("#2F3A34") },
                zombieDensity = 1.1f, cardAccent = C("#6E8CFF")
            };
            n.obstacles.Add(new ObstacleDef(PropKind.AbandonedCar, ObstacleType.Block, 2.5f));
            n.obstacles.Add(new ObstacleDef(PropKind.PoliceRoadblock, ObstacleType.Jump, 2.5f));
            n.obstacles.Add(new ObstacleDef(PropKind.FallenStreetlight, ObstacleType.Slide, 2f));
            n.obstacles.Add(new ObstacleDef(PropKind.Scaffolding, ObstacleType.Slide, 1.5f));
            n.obstacles.Add(new ObstacleDef(PropKind.ConstructionBarrier, ObstacleType.Jump, 2f));
            n.obstacles.Add(new ObstacleDef(PropKind.BurningCar, ObstacleType.Block, 1.2f));
            profiles[2] = n;

            // ---------------- THEME 4: ABANDONED APOCALYPTIC CITY ----------------
            var a = new ThemeProfile
            {
                id = ThemeId.AbandonedCity,
                displayName = "Abandoned Apocalyptic City",
                tagline = "Daylight over the ruins of civilisation.",
                sceneName = SceneFlow.AbandonedCity,
                skyTint = C("#A9B8C2"), skyGround = C("#5B5850"), skyExposure = 1.2f, atmosphere = 1.6f,
                sunColor = C("#FFE7C4"), sunIntensity = 1.6f, sunEuler = new Vector3(32f, -120f, 0f),
                ambientSky = C("#A3AFB5"), ambientEquator = C("#7E7A70"), ambientGround = C("#3E3B35"),
                fogColor = C("#A8A596"), fogDensity = 0.014f,
                postExposure = 0.05f, contrast = 16f, saturation = -22f, bloomIntensity = 0.7f, bloomThreshold = 1.0f,
                vignette = 0.28f, filmGrain = 0.12f, colorFilter = C("#FFF4E4"),
                road = RoadStyle.CrackedAsphalt, roadColor = C("#4A4844"), verge = C("#6C6A64"), vergeAlt = C("#5E6648"),
                weather = WeatherStyle.DustAndSmoke, ambience = AmbienceStyle.RuinedCity, musicSeed = 404,
                zombieSkins = new[] { C("#9C9E8E"), C("#8E968A"), C("#A39A88") },
                zombieClothes = new[] { C("#555048"), C("#3F4650"), C("#6A5B4C"), C("#4C5244") },
                zombieDensity = 1.05f, cardAccent = C("#C9B27A")
            };
            a.obstacles.Add(new ObstacleDef(PropKind.BrokenBus, ObstacleType.Block, 1.2f));
            a.obstacles.Add(new ObstacleDef(PropKind.RubblePile, ObstacleType.Jump, 2.5f));
            a.obstacles.Add(new ObstacleDef(PropKind.ConcreteBarrier, ObstacleType.Jump, 2.5f));
            a.obstacles.Add(new ObstacleDef(PropKind.OverturnedCar, ObstacleType.Block, 2f));
            a.obstacles.Add(new ObstacleDef(PropKind.RoadHole, ObstacleType.Pit, 1.5f));
            a.obstacles.Add(new ObstacleDef(PropKind.FallenGirder, ObstacleType.Slide, 2.5f));
            profiles[3] = a;
        }
    }
}
