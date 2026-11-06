using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ZombieRunner
{
    public enum MenuScreen { MainMenu, ThemeSelection }

    /// <summary>Scene entry point for MainMenu and ThemeSelection (created by the setup tool).</summary>
    [DefaultExecutionOrder(-100)]
    public class MenuBootstrap : MonoBehaviour
    {
        public MenuScreen screen = MenuScreen.MainMenu;
        public Camera mainCamera;
        public Light sun;

        void Awake()
        {
            Time.timeScale = 1f;
            QualityManager.Ensure();
            MaterialLibrary.ClearCache();
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) { var cg = new GameObject("Main Camera"); mainCamera = cg.AddComponent<Camera>(); cg.tag = "MainCamera"; cg.AddComponent<AudioListener>(); }
            LevelBootstrap.EnsureEventSystem();
            if (screen == MenuScreen.MainMenu) gameObject.AddComponent<MainMenuController>().Init(mainCamera, sun);
            else gameObject.AddComponent<ThemeSelectController>().Init(mainCamera, sun);
            AudioManager.PlayMusic(AudioKeys.MusicMenu);
            AudioManager.StopAmbience();
            AudioManager.SetChaseIntensity(0f);
        }
    }

    /// <summary>
    /// A small living scene for a theme: a few track chunks, a survivor running, zombies shambling behind.
    /// Used as the animated main-menu background and inside the theme cards.
    /// </summary>
    public class ThemeDiorama : MonoBehaviour
    {
        public ThemeProfile Theme { get; private set; }
        public Transform Runner { get; private set; }
        public Light Sun { get; private set; }
        public Camera Cam { get; private set; }
        float speed = 5.5f;
        Vector3 camOffset;
        float orbit;
        readonly List<Transform> zombies = new List<Transform>();
        readonly List<float> zombieOffsets = new List<float>();

        public static ThemeDiorama Create(ThemeProfile theme, Vector3 origin, Camera cam, bool weather, int chunks, Light sun = null)
        {
            var go = new GameObject("Diorama_" + theme.id);
            go.transform.position = origin;
            var d = go.AddComponent<ThemeDiorama>();
            d.Build(theme, cam, weather, chunks, sun);
            return d;
        }

        void Build(ThemeProfile theme, Camera cam, bool weather, int chunks, Light sun)
        {
            Theme = theme;
            Runner = ZRUtil.Child(transform, "Runner");
            Runner.localPosition = new Vector3(0, 0, 10f);
            var survivor = CharacterVisual.CreateSurvivor(Runner, GameSettings.SelectedCharacter);
            survivor.SetPose(Pose.Run, 0.75f);

            var env = new GameObject("Environment").AddComponent<EnvironmentGenerator>();
            env.transform.SetParent(transform, false);
            env.keepBehind = 55f; // the menu camera looks back at the horde
            env.Build(theme, Runner, chunks + 1, chunks, weather);

            for (int i = 0; i < 3; i++)
            {
                var z = ZRUtil.Child(transform, "Zombie" + i);
                var kind = i == 1 ? ZombieKind.Brute : ZombieKind.Runner;
                var v = CharacterVisual.CreateZombie(z, CharacterCatalog.Zombie(theme, kind, 300 + i * 11), 300 + i * 11);
                v.SetPose(Pose.ZombieRun, 0.6f);
                zombies.Add(z);
                zombieOffsets.Add(i);
            }

            if (sun == null)
            {
                var sg = new GameObject("Sun_" + theme.id);
                sg.transform.SetParent(transform, false);
                sun = sg.AddComponent<Light>();
            }
            Sun = ThemeLighting.SetupSun(sun, theme);
            Cam = cam;
            camOffset = new Vector3(2.4f, 1.9f, 4.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Runner.localPosition += new Vector3(0, 0, speed * dt);
            var rp = Runner.localPosition;
            for (int i = 0; i < zombies.Count; i++)
            {
                float lag = 4.5f + i * 1.7f + Mathf.Sin(Time.time * 0.7f + i) * 0.6f;
                zombies[i].localPosition = new Vector3((i - 1) * 1.4f, 0, rp.z - lag);
            }
            if (Cam != null)
            {
                orbit += dt * 0.08f;
                var off = Quaternion.Euler(0, Mathf.Sin(orbit) * 25f, 0) * camOffset;
                var target = transform.TransformPoint(rp + off);
                Cam.transform.position = target;
                Cam.transform.LookAt(transform.TransformPoint(rp + new Vector3(0, 1.0f, -3.5f)));
            }
        }
    }

    // =====================================================================================
    // MAIN MENU
    // =====================================================================================
    public class MainMenuController : MonoBehaviour
    {
        RectTransform mainPanel, characterPanel, settingsPanel;
        Text bestText, soundText, charName, charBlurb;
        RawImage charPreview;
        Transform studio;
        CharacterVisual studioChar;
        Camera studioCam;
        RenderTexture charRT;
        int charIndex;

        public void Init(Camera cam, Light sun)
        {
            var theme = ThemeDatabase.Get(GameSettings.SelectedTheme);
            ThemeLighting.ApplyEnvironment(theme);
            ThemeLighting.ConfigureCamera(cam, theme);
            ThemeLighting.CreateVolume(theme, transform);
            ThemeDiorama.Create(theme, Vector3.zero, cam, true, 3, sun);
            BuildStudio();
            BuildUI();
        }

        void BuildStudio()
        {
            studio = new GameObject("CharacterStudio").transform;
            studio.position = new Vector3(0, -500f, 0);
            var floor = new PropBuilder().Cyl(Vector3.zero, 1.4f, 0.1f, Palette.DarkConcrete, default, 32).Build("Floor", studio);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(studio, false); key.transform.localPosition = new Vector3(1.5f, 3f, 2.5f);
            key.type = LightType.Spot; key.spotAngle = 60f; key.range = 12f; key.intensity = 25f; key.color = new Color(1f, 0.92f, 0.8f);
            key.transform.LookAt(studio.position + Vector3.up);
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.transform.SetParent(studio, false); rim.transform.localPosition = new Vector3(-1.5f, 2.5f, -2f);
            rim.type = LightType.Point; rim.range = 6f; rim.intensity = 4f; rim.color = new Color(0.9f, 0.3f, 0.25f);
            studioCam = new GameObject("StudioCamera").AddComponent<Camera>();
            studioCam.transform.SetParent(studio, false);
            studioCam.transform.localPosition = new Vector3(0, 1.2f, 3.6f);
            studioCam.transform.LookAt(studio.position + Vector3.up * 0.95f);
            studioCam.clearFlags = CameraClearFlags.SolidColor;
            studioCam.backgroundColor = new Color(0.07f, 0.07f, 0.08f, 1f);
            studioCam.fieldOfView = 35f;
            studioCam.farClipPlane = 20f;
            charRT = new RenderTexture(640, 800, 24) { name = "CharacterPreviewRT" };
            studioCam.targetTexture = charRT;
            studioCam.enabled = false;
            charIndex = GameSettings.SelectedCharacter;
            RebuildStudioCharacter();
        }

        void RebuildStudioCharacter()
        {
            if (studioChar != null) Destroy(studioChar.gameObject);
            studioChar = CharacterVisual.CreateSurvivor(studio, charIndex);
            studioChar.SetPose(Pose.Idle);
            studioChar.gameObject.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 35, 0);
        }

        void BuildUI()
        {
            var canvas = UIFactory.Canvas("MainMenuUI");
            var safe = UIFactory.SafeArea(canvas.transform);

            // MAIN
            mainPanel = UIFactory.Rect("Main", safe); UIFactory.Stretch(mainPanel);
            var shade = UIFactory.Image("Shade", mainPanel, new Color(0, 0, 0, 0.45f));
            UIFactory.Place(shade.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(860, 2400));
            var title = UIFactory.Label("Title", mainPanel, "ZOMBIE\nAPOCALYPSE\nRUNNER", 104, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -50), new Vector2(900, 360));
            title.lineSpacing = 0.85f;
            title.gameObject.AddComponent<TitleFlicker>();
            var sub = UIFactory.Label("Subtitle", mainPanel, "Run. Survive. Don't look back.", 34, UIFactory.Accent, TextAnchor.UpperLeft, FontStyle.Italic);
            UIFactory.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -420), new Vector2(800, 50));

            var buttons = UIFactory.Rect("Buttons", mainPanel);
            UIFactory.Place(buttons, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -110), new Vector2(560, 540));
            UIFactory.VStack(buttons, 18, TextAnchor.UpperLeft);
            UIFactory.Button("Play", buttons, "PLAY", () => SceneFlow.LoadThemeSelection(), new Vector2(520, 100), true, 44);
            var last = ThemeDatabase.Get(GameSettings.SelectedTheme);
            UIFactory.Button("QuickPlay", buttons, "QUICK RUN: " + last.displayName.ToUpperInvariant(), () => SceneFlow.LoadLevel(GameSettings.SelectedTheme), new Vector2(520, 76), false, 22);
            UIFactory.Button("Themes", buttons, "THEME SELECTION", () => SceneFlow.LoadThemeSelection(), new Vector2(520, 76), false, 30);
            UIFactory.Button("Character", buttons, "CHARACTER", () => Show(characterPanel), new Vector2(520, 76), false, 30);
            UIFactory.Button("Settings", buttons, "SETTINGS", () => Show(settingsPanel), new Vector2(520, 76), false, 30);
            if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
                UIFactory.Button("Quit", buttons, "QUIT", Quit, new Vector2(520, 76), false, 30);

            bestText = UIFactory.Label("Best", mainPanel, "", 30, UIFactory.TextDim, TextAnchor.LowerLeft);
            UIFactory.Place(bestText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(74, 40), new Vector2(800, 140));
            RefreshBest();

            var sound = UIFactory.Button("Sound", mainPanel, "", () => { GameSettings.SoundOn = !GameSettings.SoundOn; RefreshSound(); }, new Vector2(260, 80), false, 28);
            UIFactory.Place((RectTransform)sound.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(260, 80));
            soundText = sound.GetComponentInChildren<Text>();
            RefreshSound();

            // CHARACTER
            characterPanel = Modal(safe, "CHOOSE YOUR SURVIVOR", out var cbox);
            charPreview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            charPreview.transform.SetParent(cbox, false);
            charPreview.texture = charRT;
            UIFactory.Place(charPreview.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-230, -130), new Vector2(400, 500));
            charName = UIFactory.Label("Name", cbox, "", 54, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(charName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(10, -150), new Vector2(420, 70));
            charBlurb = UIFactory.Label("Blurb", cbox, "", 28, UIFactory.TextDim, TextAnchor.UpperLeft);
            UIFactory.Place(charBlurb.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(10, -230), new Vector2(420, 200));
            var prev = UIFactory.Button("Prev", cbox, "<", () => Cycle(-1), new Vector2(110, 90), false, 48);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(10, -450), new Vector2(110, 90));
            var next = UIFactory.Button("Next", cbox, ">", () => Cycle(1), new Vector2(110, 90), false, 48);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(140, -450), new Vector2(110, 90));
            var select = UIFactory.Button("Select", cbox, "SELECT", () => { GameSettings.SelectedCharacter = charIndex; Show(mainPanel); }, new Vector2(320, 90), true);
            UIFactory.Place((RectTransform)select.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(320, 90));
            var back = UIFactory.Button("Back", cbox, "BACK", () => { charIndex = GameSettings.SelectedCharacter; RebuildStudioCharacter(); Show(mainPanel); }, new Vector2(320, 90));
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 40), new Vector2(320, 90));
            UpdateCharText();

            // SETTINGS
            settingsPanel = Modal(safe, "SETTINGS", out var sbox);
            var stack = UIFactory.Rect("Stack", sbox); UIFactory.Stretch(stack, 30); stack.offsetMax = new Vector2(-30, -130);
            UIFactory.VStack(stack, 14, TextAnchor.UpperCenter);
            UIFactory.SettingsControls(stack, true);
            Button reset = null;
            bool armed = false;
            reset = UIFactory.Button("Reset", stack, "RESET PROGRESS", () =>
            {
                if (!armed) { armed = true; reset.GetComponentInChildren<Text>().text = "TAP AGAIN TO CONFIRM"; return; }
                armed = false; GameSettings.ResetProgress(); RefreshBest(); reset.GetComponentInChildren<Text>().text = "PROGRESS RESET";
            }, new Vector2(520, 70), false, 26);
            UIFactory.Button("Back", stack, "BACK", () => { RefreshSound(); Show(mainPanel); }, new Vector2(520, 84), true);

            Show(mainPanel);
        }

        RectTransform Modal(Transform parent, string title, out RectTransform box)
        {
            var panel = UIFactory.Rect(title, parent); UIFactory.Stretch(panel);
            var dim = panel.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.6f);
            var b = UIFactory.Image("Box", panel, UIFactory.Panel);
            UIFactory.Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 940));
            var t = UIFactory.Label("Title", b.transform, title, 56, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(940, 90));
            box = b.rectTransform;
            return panel;
        }

        void Show(RectTransform panel)
        {
            mainPanel.gameObject.SetActive(panel == mainPanel);
            characterPanel.gameObject.SetActive(panel == characterPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel);
            studioCam.enabled = panel == characterPanel;
        }

        void Cycle(int dir)
        {
            int n = CharacterCatalog.SurvivorCount;
            charIndex = ((charIndex + dir) % n + n) % n;
            RebuildStudioCharacter();
            UpdateCharText();
        }

        void UpdateCharText()
        {
            charName.text = CharacterCatalog.SurvivorName(charIndex).ToUpperInvariant();
            charBlurb.text = CharacterCatalog.SurvivorBlurb(charIndex) + (charIndex == GameSettings.SelectedCharacter ? "\n\n(currently selected)" : "");
        }

        void RefreshBest()
        {
            bestText.text = $"BEST SCORE  {GameSettings.BestScore:N0}\nBEST DISTANCE  {GameSettings.BestDistance:N0} m\nCOINS BANKED  {GameSettings.TotalCoins:N0}   ·   RUNS  {GameSettings.TotalRuns}";
        }

        void RefreshSound() { soundText.text = GameSettings.SoundOn ? "SOUND: ON" : "SOUND: OFF"; }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy() { if (charRT != null) charRT.Release(); }
    }

    /// <summary>Slight horror flicker on the title text.</summary>
    public class TitleFlicker : MonoBehaviour
    {
        Text t; float next; Color baseColor;
        void Awake() { t = GetComponent<Text>(); baseColor = t.color; }
        void Update()
        {
            if (Time.unscaledTime < next) return;
            bool off = Random.value < 0.15f;
            t.color = off ? baseColor * 0.55f : baseColor;
            next = Time.unscaledTime + (off ? Random.Range(0.04f, 0.12f) : Random.Range(0.3f, 3f));
        }
    }

    // =====================================================================================
    // THEME SELECTION
    // =====================================================================================
    /// <summary>
    /// Four cards, each showing a live render of its theme. Every preview has its own diorama, sun and
    /// camera rendering into a RenderTexture; sky, fog and ambient light are swapped per camera just before
    /// it renders, so all four themes look right at once.
    /// </summary>
    public class ThemeSelectController : MonoBehaviour
    {
        readonly List<ThemeDiorama> dioramas = new List<ThemeDiorama>();
        readonly List<Camera> cams = new List<Camera>();
        readonly List<RenderTexture> rts = new List<RenderTexture>();
        Camera mainCam;

        public void Init(Camera cam, Light sun)
        {
            mainCam = cam;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.04f, 0.04f, 0.045f);
            mainCam.cullingMask = 0; // UI only
            if (sun != null) sun.enabled = false;

            for (int i = 0; i < 4; i++)
            {
                var theme = ThemeDatabase.Get((ThemeId)i);
                var camGo = new GameObject("PreviewCam_" + theme.id);
                var c = camGo.AddComponent<Camera>();
                c.fieldOfView = 50f;
                ThemeLighting.ConfigureCamera(c, theme);
                c.farClipPlane = 300f;
                var data = c.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = false;
                var rt = new RenderTexture(560, 700, 24) { name = "ThemePreview_" + theme.id };
                c.targetTexture = rt;
                var d = ThemeDiorama.Create(theme, new Vector3(2500f * (i + 1), 0, 0), c, true, 3);
                dioramas.Add(d); cams.Add(c); rts.Add(rt);
            }
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            BuildUI();
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            int idx = cams.IndexOf(cam);
            if (idx < 0) return;
            ThemeLighting.ApplyEnvironment(dioramas[idx].Theme);
            for (int i = 0; i < dioramas.Count; i++) dioramas[i].Sun.enabled = i == idx;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            foreach (var rt in rts) if (rt != null) rt.Release();
        }

        void BuildUI()
        {
            var canvas = UIFactory.Canvas("ThemeSelectUI");
            var safe = UIFactory.SafeArea(canvas.transform);
            var title = UIFactory.Label("Title", safe, "CHOOSE YOUR NIGHTMARE", 76, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1600, 100));
            var row = UIFactory.Rect("Cards", safe);
            UIFactory.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1840, 800));
            UIFactory.HStack(row, 24);
            for (int i = 0; i < 4; i++) Card(row, (ThemeId)i, rts[i]);
            var back = UIFactory.Button("Back", safe, "BACK", () => SceneFlow.LoadMainMenu(), new Vector2(260, 84));
            UIFactory.Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 30), new Vector2(260, 84));
        }

        void Card(Transform parent, ThemeId id, RenderTexture rt)
        {
            var theme = ThemeDatabase.Get(id);
            var card = UIFactory.Image("Card_" + id, parent, UIFactory.Panel);
            card.rectTransform.sizeDelta = new Vector2(440, 790);
            var btn = card.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => { AudioManager.UIClick(); SceneFlow.LoadLevel(id); });
            card.gameObject.AddComponent<ButtonPulse>();
            var accent = UIFactory.Image("Accent", card.transform, theme.cardAccent);
            UIFactory.Place(accent.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(420, 8));
            var img = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(card.transform, false);
            img.texture = rt;
            img.raycastTarget = false;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(416, 520));
            var name = UIFactory.Label("Name", card.transform, theme.displayName.ToUpperInvariant(), 32, UIFactory.TextMain, TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -556), new Vector2(410, 84));
            var tag = UIFactory.Label("Tagline", card.transform, theme.tagline, 23, UIFactory.TextDim, TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.Place(tag.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -636), new Vector2(400, 60));
            var best = UIFactory.Label("Best", card.transform, "BEST  " + GameSettings.BestScoreFor(id).ToString("N0"), 26, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(best.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(400, 36));
            var play = UIFactory.Button("Play", card.transform, "RUN", () => SceneFlow.LoadLevel(id), new Vector2(360, 72), true, 34);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(360, 72));
        }
    }
}
