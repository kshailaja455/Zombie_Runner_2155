using UnityEngine;
using UnityEngine.UI;

namespace ZombieRunner
{
    /// <summary>In-game HUD, pause menu and game-over screen, built in code.</summary>
    public class HUDController : MonoBehaviour
    {
        GameManager gm;
        Text score, distance, multiplier, coins, countdown, toast, fps, hint;
        Image progressFill, danger;
        RectTransform pausePanel, gameOverPanel, hud;
        readonly Image[] puIcons = new Image[5];
        readonly Image[] puFills = new Image[5];
        readonly GameObject[] puSlots = new GameObject[5];
        float toastTimer;
        float dangerOverride = -1f;
        Text goTitle, goScore, goDistance, goCoins, goBest, goNewBest;

        public static HUDController Create(GameManager manager)
        {
            var canvas = UIFactory.Canvas("HUD", 10);
            var h = canvas.gameObject.AddComponent<HUDController>();
            h.gm = manager;
            h.Build(canvas.transform);
            return h;
        }

        void Build(Transform canvas)
        {
            // Danger vignette (behind everything else)
            danger = UIFactory.Image("Danger", canvas, new Color(0.6f, 0f, 0f, 0f), ProceduralTextures.EdgeVignette(), false);
            UIFactory.Stretch(danger.rectTransform);
            danger.raycastTarget = false;

            var safe = UIFactory.SafeArea(canvas);
            hud = UIFactory.Rect("HUD", safe);
            UIFactory.Stretch(hud);

            // Score block
            var scoreBg = UIFactory.Image("ScorePanel", hud, UIFactory.Panel);
            UIFactory.Place(scoreBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(430, 170));
            score = UIFactory.Label("Score", scoreBg.transform, "0", 64, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(score.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -14), new Vector2(390, 76));
            distance = UIFactory.Label("Distance", scoreBg.transform, "0 m", 34, UIFactory.TextDim, TextAnchor.UpperLeft);
            UIFactory.Place(distance.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -96), new Vector2(240, 50));
            multiplier = UIFactory.Label("Multiplier", scoreBg.transform, "x1.0", 34, UIFactory.Gold, TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.Place(multiplier.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -96), new Vector2(150, 50));

            // Coins + pause
            var coinBg = UIFactory.Image("CoinPanel", hud, UIFactory.Panel);
            UIFactory.Place(coinBg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -26), new Vector2(250, 90));
            var coinIcon = UIFactory.Image("CoinIcon", coinBg.transform, UIFactory.Gold, ProceduralTextures.Circle(), false);
            UIFactory.Place(coinIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(54, 54));
            coins = UIFactory.Label("Coins", coinBg.transform, "0", 48, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(coins.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(160, 70));
            var pause = UIFactory.Button("PauseButton", hud, "II", () => gm.TogglePause(), new Vector2(110, 90), false, 44);
            UIFactory.Place((RectTransform)pause.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -26), new Vector2(110, 90));

            // Running progress toward the next safe zone
            var bar = UIFactory.Image("ProgressBar", hud, UIFactory.Panel);
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(560, 34));
            progressFill = UIFactory.Image("Fill", bar.transform, UIFactory.Accent);
            progressFill.rectTransform.anchorMin = new Vector2(0, 0); progressFill.rectTransform.anchorMax = new Vector2(0, 1);
            progressFill.rectTransform.pivot = new Vector2(0, 0.5f);
            progressFill.rectTransform.offsetMin = new Vector2(4, 4); progressFill.rectTransform.offsetMax = new Vector2(4, -4);
            var barLabel = UIFactory.Label("ProgressLabel", bar.transform, "NEXT SAFE ZONE", 22, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(barLabel.rectTransform);

            // Power-up slots
            for (int i = 0; i < 5; i++)
            {
                var slot = UIFactory.Image("PowerUp" + i, hud, UIFactory.Panel);
                UIFactory.Place(slot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -214 - i * 76), new Vector2(330, 66));
                puIcons[i] = UIFactory.Image("Icon", slot.transform, PickupFactory.Colors[i], ProceduralTextures.Circle(), false);
                UIFactory.Place(puIcons[i].rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(44, 44));
                var name = UIFactory.Label("Name", slot.transform, PickupFactory.Names[i], 24, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
                UIFactory.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(68, -6), new Vector2(250, 30));
                var fillBg = UIFactory.Image("FillBg", slot.transform, new Color(1, 1, 1, 0.12f));
                UIFactory.Place(fillBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(68, 10), new Vector2(240, 12));
                puFills[i] = UIFactory.Image("Fill", fillBg.transform, PickupFactory.Colors[i]);
                puFills[i].rectTransform.anchorMin = Vector2.zero; puFills[i].rectTransform.anchorMax = new Vector2(1, 1);
                puFills[i].rectTransform.offsetMin = puFills[i].rectTransform.offsetMax = Vector2.zero;
                puSlots[i] = slot.gameObject;
                slot.gameObject.SetActive(false);
            }

            countdown = UIFactory.Label("Countdown", hud, "", 160, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(800, 220));
            toast = UIFactory.Label("Toast", hud, "", 44, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1400, 80));
            hint = UIFactory.Label("Hint", hud, ZRUtil.IsMobile ? "Swipe left / right to change lanes   ·   swipe up to jump   ·   swipe down to slide"
                : "A / D or arrow keys: change lanes   ·   W / Up / Space: jump   ·   S / Down: slide   ·   Esc: pause", 30, UIFactory.TextMain);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1600, 60));
            fps = UIFactory.Label("FPS", hud, "", 24, UIFactory.TextDim, TextAnchor.LowerRight);
            UIFactory.Place(fps.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 14), new Vector2(200, 34));

            BuildPause(safe);
            BuildGameOver(safe);
        }

        void BuildPause(Transform parent)
        {
            pausePanel = UIFactory.Rect("PausePanel", parent);
            UIFactory.Stretch(pausePanel);
            var dim = pausePanel.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.55f);
            var box = UIFactory.Image("Box", pausePanel, UIFactory.Panel);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 900));
            var stack = UIFactory.Rect("Stack", box.transform); UIFactory.Stretch(stack, 30);
            UIFactory.VStack(stack, 18, TextAnchor.UpperCenter);
            var title = UIFactory.Label("Title", stack, "PAUSED", 72, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold); title.rectTransform.sizeDelta = new Vector2(760, 100);
            UIFactory.Button("Resume", stack, "RESUME", () => gm.Resume(), new Vector2(560, 92), true);
            UIFactory.Button("Restart", stack, "RESTART RUN", () => gm.Restart(), new Vector2(560, 84));
            UIFactory.SettingsControls(stack, false);
            UIFactory.Button("Menu", stack, "MAIN MENU", () => gm.MainMenu(), new Vector2(560, 84));
            pausePanel.gameObject.SetActive(false);
        }

        void BuildGameOver(Transform parent)
        {
            gameOverPanel = UIFactory.Rect("GameOverPanel", parent);
            UIFactory.Stretch(gameOverPanel);
            var dim = gameOverPanel.gameObject.AddComponent<Image>(); dim.color = new Color(0.08f, 0, 0, 0.6f);
            var box = UIFactory.Image("Box", gameOverPanel, UIFactory.Panel);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 900));
            var stack = UIFactory.Rect("Stack", box.transform); UIFactory.Stretch(stack, 30);
            UIFactory.VStack(stack, 14, TextAnchor.UpperCenter);
            var head = UIFactory.Label("Head", stack, "YOU DIDN'T MAKE IT", 70, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold); head.rectTransform.sizeDelta = new Vector2(900, 96);
            goTitle = UIFactory.Label("Cause", stack, "", 32, UIFactory.TextDim); goTitle.rectTransform.sizeDelta = new Vector2(900, 50);
            goNewBest = UIFactory.Label("NewBest", stack, "NEW BEST SCORE!", 44, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold); goNewBest.rectTransform.sizeDelta = new Vector2(900, 60);
            goScore = Stat(stack, "FINAL SCORE");
            goDistance = Stat(stack, "DISTANCE");
            goCoins = Stat(stack, "COINS");
            goBest = Stat(stack, "BEST SCORE");
            var row = UIFactory.Rect("Buttons", stack); row.sizeDelta = new Vector2(900, 110);
            UIFactory.HStack(row, 20);
            UIFactory.Button("Restart", row, "RUN AGAIN", () => gm.Restart(), new Vector2(290, 96), true);
            UIFactory.Button("Theme", row, "CHANGE THEME", () => gm.ChangeTheme(), new Vector2(290, 96), false, 28);
            UIFactory.Button("Menu", row, "MAIN MENU", () => gm.MainMenu(), new Vector2(290, 96), false, 30);
            gameOverPanel.gameObject.SetActive(false);
        }

        Text Stat(Transform parent, string label)
        {
            var row = UIFactory.Rect(label, parent); row.sizeDelta = new Vector2(760, 64);
            var l = UIFactory.Label("Label", row, label, 32, UIFactory.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Stretch(l.rectTransform);
            var v = UIFactory.Label("Value", row, "0", 44, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Stretch(v.rectTransform);
            return v;
        }

        public void Toast(string msg) { toast.text = msg; toastTimer = 2.2f; }
        public void SetDanger(float d) { dangerOverride = d; }

        public void ShowPause(bool show) { pausePanel.gameObject.SetActive(show); }

        public void ShowGameOver()
        {
            hud.gameObject.SetActive(false);
            gameOverPanel.gameObject.SetActive(true);
            goTitle.text = gm.DeathCause;
            goScore.text = Mathf.FloorToInt(gm.Score.Score).ToString("N0");
            goDistance.text = Mathf.FloorToInt(gm.Score.Distance).ToString("N0") + " m";
            goCoins.text = gm.Score.Coins.ToString("N0");
            goBest.text = GameSettings.BestScoreFor(gm.Theme.id).ToString("N0");
            goNewBest.gameObject.SetActive(gm.NewBest);
        }

        void Update()
        {
            if (gm == null) return;
            score.text = Mathf.FloorToInt(gm.Score.Score).ToString("N0");
            distance.text = Mathf.FloorToInt(gm.Score.Distance).ToString("N0") + " m";
            multiplier.text = "x" + gm.Score.Multiplier.ToString("0.0");
            coins.text = gm.Score.Coins.ToString("N0");
            var pr = progressFill.rectTransform;
            float w = ((RectTransform)pr.parent).rect.width - 8f;
            pr.sizeDelta = new Vector2(Mathf.Max(0f, w * gm.Score.CheckpointProgress), pr.sizeDelta.y);

            for (int i = 0; i < 5; i++)
            {
                bool on = gm.PowerUps.IsActive((PowerUpType)i);
                if (puSlots[i].activeSelf != on) puSlots[i].SetActive(on);
                if (on) puFills[i].rectTransform.anchorMax = new Vector2(gm.PowerUps.Remaining01((PowerUpType)i), 1);
            }

            if (gm.CurrentState == GameManager.State.Intro)
            {
                int n = Mathf.CeilToInt(gm.IntroRemaining);
                countdown.text = n > 0 ? n.ToString() : "RUN!";
            }
            else if (gm.CurrentState == GameManager.State.Running && gm.Difficulty.Elapsed < 0.8f) countdown.text = "RUN!";
            else countdown.text = "";

            float hintA = gm.CurrentState == GameManager.State.Intro ? 1f : Mathf.Clamp01(1f - (gm.Difficulty.Elapsed - 3f) / 1.5f);
            hint.color = new Color(1, 1, 1, hintA);

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                var c = toast.color; c.a = Mathf.Clamp01(toastTimer / 0.4f); toast.color = c;
            }
            else toast.text = "";

            float d = dangerOverride >= 0f ? dangerOverride : (gm.Chase != null ? gm.Chase.Danger : 0f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
            danger.color = new Color(0.55f, 0f, 0f, Mathf.Clamp01(d * d) * 0.65f * pulse);

            fps.text = GameSettings.ShowFps ? Mathf.RoundToInt(QualityManager.CurrentFps) + " FPS" : "";
        }
    }
}
