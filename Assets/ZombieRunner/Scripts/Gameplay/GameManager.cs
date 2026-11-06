using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// Owns a run: builds the level for the theme, runs the countdown, ticks difficulty/score/power-ups,
    /// handles pause/resume/restart/game over, and keeps world coordinates small with a floating origin.
    /// Created by LevelBootstrap, so level scenes need no manual wiring.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum State { Intro, Running, Paused, Dying, GameOver }

        public static GameManager Instance { get; private set; }

        public ThemeProfile Theme { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Camera { get; private set; }
        public ChaseSystem Chase { get; private set; }
        public TrackSpawner Spawner { get; private set; }
        public EnvironmentGenerator Environment { get; private set; }
        public InputRouter InputRouter { get; private set; }
        public HUDController HUD { get; private set; }
        public DifficultyManager Difficulty { get; } = new DifficultyManager();
        public ScoreManager Score { get; } = new ScoreManager();
        public PowerUpManager PowerUps { get; } = new PowerUpManager();

        public State CurrentState { get; private set; } = State.Intro;
        public bool IsRunning => CurrentState == State.Running;
        public string DeathCause { get; private set; } = "";
        public bool NewBest { get; private set; }
        public float IntroRemaining => introTimer;

        const float FloatingOriginLimit = 2000f; // multiple of the chunk length
        float introTimer = 3.2f;
        float dyingTimer;
        State stateBeforePause;
        GameObject shieldFx, magnetFx, healthFx;
        ParticleSystem speedLines;

        public void Begin(ThemeId themeId, Camera cam, Light sun)
        {
            Instance = this;
            Time.timeScale = 1f;
            QualityManager.Ensure();
            Theme = ThemeDatabase.Get(themeId);
            MaterialLibrary.ClearCache();

            // Lighting & post
            ThemeLighting.ApplyEnvironment(Theme);
            ThemeLighting.SetupSun(sun, Theme);
            ThemeLighting.CreateVolume(Theme, transform);

            // Player
            var playerGo = new GameObject("Player");
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(this, GameSettings.SelectedCharacter);
            BuildPlayerFx(playerGo.transform);

            // Camera
            if (cam == null) { var cg = new GameObject("Main Camera"); cam = cg.AddComponent<Camera>(); cg.tag = "MainCamera"; cg.AddComponent<AudioListener>(); }
            ThemeLighting.ConfigureCamera(cam, Theme);
            Camera = cam.gameObject.GetOrAdd<CameraRig>();
            Camera.Init(playerGo.transform);
            if (QualityManager.Tier == QualityTier.High && Theme.road == RoadStyle.WetAsphalt) ThemeLighting.FollowProbe(cam.transform);
            speedLines = FX.Motes(cam.transform, new Color(1f, 1f, 1f, 0.35f), 0.05f, 0f, 120, true);
            speedLines.transform.localPosition = new Vector3(0, 0, 12f);
            var sl = speedLines.velocityOverLifetime; sl.space = ParticleSystemSimulationSpace.Local; sl.z = new ParticleSystem.MinMaxCurve(-40f, -30f); sl.x = new ParticleSystem.MinMaxCurve(0f, 0f); sl.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            var slm = speedLines.main; slm.simulationSpace = ParticleSystemSimulationSpace.Local; slm.startLifetime = 0.5f;
            var slr = speedLines.GetComponent<ParticleSystemRenderer>(); slr.renderMode = ParticleSystemRenderMode.Stretch; slr.velocityScale = 0.05f;
            speedLines.Stop();

            // Environment
            var envGo = new GameObject("Environment");
            Environment = envGo.AddComponent<EnvironmentGenerator>();
            Environment.Build(Theme, playerGo.transform, QualityManager.Tier == QualityTier.Low ? 6 : 8, 6);

            // Horde & spawner
            Chase = new GameObject("Horde").AddComponent<ChaseSystem>();
            Chase.Build(Theme, Player, QualityManager.Tier == QualityTier.Low ? 3 : 5);
            Spawner = new GameObject("TrackSpawner").AddComponent<TrackSpawner>();
            Spawner.Init(this, Theme, playerGo.transform);

            // Input & UI
            InputRouter = gameObject.AddComponent<InputRouter>();
            InputRouter.OnCommand += OnCommand;
            HUD = HUDController.Create(this);

            // Events
            Score.OnCheckpoint += cp =>
            {
                HUD.Toast($"SAFE ZONE {cp} REACHED  -  SCORE x{Score.Multiplier:0.0}");
                AudioManager.PlaySfx(AudioKeys.Checkpoint, 0.9f);
            };
            PowerUps.OnEnded += t => { HUD.Toast(PickupFactory.Names[(int)t] + " ended"); AudioManager.PlaySfx(AudioKeys.PowerDown, 0.6f); };

            // Audio
            AudioManager.PlayMusic(AudioManager.MusicFor(themeId));
            AudioManager.PlayAmbience(Theme.ambience);
            AudioManager.SetChaseIntensity(0.6f);
            CurrentState = State.Intro;
        }

        void BuildPlayerFx(Transform player)
        {
            shieldFx = new PropBuilder().Sphere(Vector3.zero, new Vector3(1.6f, 2.3f, 1.6f), MaterialLibrary.Transparent("shieldbubble", new Color(0.4f, 0.65f, 1f, 0.18f), 0.95f, new Color(0.2f, 0.4f, 1f) * 0.8f)).Build("ShieldBubble", player, false);
            shieldFx.transform.localPosition = new Vector3(0, 1f, 0);
            shieldFx.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 40, 0);
            shieldFx.SetActive(false);
            magnetFx = new PropBuilder().Torus(Vector3.zero, 0.9f, 0.03f, MaterialLibrary.Emissive("magnetring", PickupFactory.Colors[0], 3f)).Build("MagnetRing", player, false);
            magnetFx.transform.localPosition = new Vector3(0, 1.1f, 0);
            magnetFx.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 200, 0);
            magnetFx.SetActive(false);
            healthFx = new PropBuilder().Torus(Vector3.zero, 0.55f, 0.025f, MaterialLibrary.Emissive("healthring", PickupFactory.Colors[4], 2.5f)).Build("HealthRing", player, false);
            healthFx.transform.localPosition = new Vector3(0, 0.05f, 0);
            healthFx.SetActive(false);
        }

        void OnCommand(InputCommand c)
        {
            if (c == InputCommand.Pause) { TogglePause(); return; }
            if (CurrentState == State.Running) Player.Command(c);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (CurrentState)
            {
                case State.Intro:
                {
                    int before = Mathf.CeilToInt(introTimer);
                    introTimer -= dt;
                    int after = Mathf.CeilToInt(introTimer);
                    if (after != before && after > 0) AudioManager.PlaySfx(AudioKeys.Countdown, 0.8f);
                    if (introTimer <= 0f)
                    {
                        CurrentState = State.Running;
                        Player.StartRunning();
                        Chase.StartRun();
                        AudioManager.PlaySfx(AudioKeys.Go, 1f);
                    }
                    break;
                }
                case State.Running:
                    Difficulty.Tick(dt);
                    PowerUps.Tick(dt);
                    Score.AddDistance(Player.Speed * dt);
                    UpdatePlayerFx();
                    if (Player.transform.position.z > FloatingOriginLimit) ShiftOrigin(-FloatingOriginLimit);
                    break;
                case State.Dying:
                    dyingTimer -= Time.unscaledDeltaTime;
                    if (dyingTimer < 1.2f) Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 2f);
                    if (dyingTimer <= 0f) ShowGameOver();
                    break;
            }
        }

        void UpdatePlayerFx()
        {
            bool shield = PowerUps.IsActive(PowerUpType.Shield);
            bool boost = PowerUps.IsActive(PowerUpType.SpeedBoost);
            if (shieldFx.activeSelf != (shield || boost)) shieldFx.SetActive(shield || boost);
            if (magnetFx.activeSelf != PowerUps.IsActive(PowerUpType.Magnet)) magnetFx.SetActive(PowerUps.IsActive(PowerUpType.Magnet));
            if (healthFx.activeSelf != PowerUps.IsActive(PowerUpType.HealthProtection)) healthFx.SetActive(PowerUps.IsActive(PowerUpType.HealthProtection));
            Camera.Boosting = boost;
            if (boost && !speedLines.isPlaying) speedLines.Play();
            if (!boost && speedLines.isPlaying) speedLines.Stop();
        }

        void ShiftOrigin(float dz)
        {
            Player.Shift(dz);
            Camera.Shift(dz);
            Environment.Shift(dz);
            Spawner.Shift(dz);
            Chase.ShiftAll(0); // pursuers follow the player each frame
        }

        // ------------------------------------------------------------------ events from gameplay
        public void CollectCoin()
        {
            int v = PowerUps.CoinValue;
            Score.AddCoins(v);
            AudioManager.PlaySfx(AudioKeys.Coin, 0.55f, v > 1 ? 1.25f : Random.Range(0.98f, 1.06f));
        }

        public void CollectPowerUp(PowerUpType t)
        {
            PowerUps.Activate(t);
            AudioManager.PlaySfx(AudioKeys.PowerUp(t), 1f);
            HUD.Toast(PickupFactory.Names[(int)t].ToUpperInvariant() + "!");
            Camera.Shake(0.15f, 0.2f);
        }

        public void OnSmashed(Obstacle o)
        {
            AudioManager.PlaySfx(o.type == ObstacleType.Zombie ? AudioKeys.ZombieHit : AudioKeys.Smash, 1f, Random.Range(0.9f, 1.1f));
            Spawner.SmashFx(o.transform.position);
            Camera.Shake(0.35f, 0.25f);
            Score.AddBonus(25f);
        }

        public void PlayerDied(string cause)
        {
            if (CurrentState != State.Running) return;
            DeathCause = cause;
            CurrentState = State.Dying;
            dyingTimer = 2.4f;
            Player.Die();
            Chase.Attack();
            Camera.DeathView();
            PowerUps.ClearAll();
            UpdatePlayerFx();
            Time.timeScale = 0.35f;
            AudioManager.PlaySfx(AudioKeys.ZombieAttack, 1f);
            AudioManager.StopMusic();
            AudioManager.PlaySfx(AudioKeys.GameOver, 1f);
            HUD.SetDanger(1f);
        }

        void ShowGameOver()
        {
            CurrentState = State.GameOver;
            Time.timeScale = 1f;
            NewBest = GameSettings.SubmitRun(Theme.id, Mathf.FloorToInt(Score.Score), Mathf.FloorToInt(Score.Distance), Score.Coins);
            AudioManager.SetChaseIntensity(0.15f);
            HUD.ShowGameOver();
        }

        // ------------------------------------------------------------------ flow
        public void TogglePause()
        {
            if (CurrentState == State.Paused) Resume();
            else if (CurrentState == State.Running || CurrentState == State.Intro) Pause();
        }

        public void Pause()
        {
            if (CurrentState != State.Running && CurrentState != State.Intro) return;
            stateBeforePause = CurrentState;
            CurrentState = State.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            HUD.ShowPause(true);
        }

        public void Resume()
        {
            if (CurrentState != State.Paused) return;
            CurrentState = stateBeforePause;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            HUD.ShowPause(false);
        }

        public void Restart() => SceneFlow.ReloadCurrent();
        public void MainMenu() => SceneFlow.LoadMainMenu();
        public void ChangeTheme() => SceneFlow.LoadThemeSelection();

        void OnApplicationPause(bool paused)
        {
            if (paused && CurrentState == State.Running) Pause();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
