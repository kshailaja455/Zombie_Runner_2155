using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZombieRunner
{
    /// <summary>
    /// Builds uGUI elements in code (canvas, panels, labels, buttons, sliders, toggles) with one consistent
    /// dark, gritty style. Every button is wired to a real action when it is created.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Panel = new Color(0.05f, 0.06f, 0.06f, 0.82f);
        public static readonly Color PanelLight = new Color(0.12f, 0.13f, 0.12f, 0.9f);
        public static readonly Color Accent = new Color(0.86f, 0.18f, 0.12f, 1f);
        public static readonly Color AccentDim = new Color(0.45f, 0.1f, 0.08f, 1f);
        public static readonly Color TextMain = new Color(0.93f, 0.91f, 0.86f, 1f);
        public static readonly Color TextDim = new Color(0.65f, 0.63f, 0.58f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.3f, 1f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                var reg = ZRAssetRegistry.Instance;
                if (reg != null && reg.uiFont != null) return font = reg.uiFont;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Helvetica", "DejaVu Sans" }, 32);
                return font;
            }
        }

        public static Canvas Canvas(string name, int order = 0)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            LevelBootstrap.EnsureEventSystem();
            return c;
        }

        /// <summary>A full-screen container that respects the device safe area (notches).</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? ProceduralTextures.RoundedRect();
            img.type = sliced && img.sprite == ProceduralTextures.RoundedRect() ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.75f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button Button(string name, Transform parent, string text, UnityAction onClick, Vector2 size, bool primary = false, int fontSize = 34)
        {
            var img = Image(name, parent, primary ? Accent : PanelLight);
            img.rectTransform.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? new Color(1f, 0.4f, 0.3f, 0.6f) : new Color(1f, 1f, 1f, 0.12f);
            outline.effectDistance = new Vector2(2, -2);
            var label = Label("Label", img.transform, text, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform);
            b.onClick.AddListener(() => AudioManager.UIClick());
            if (onClick != null) b.onClick.AddListener(onClick);
            img.gameObject.AddComponent<ButtonPulse>();
            return b;
        }

        public static Slider Slider(string name, Transform parent, float value, Action<float> onChange, Vector2 size)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            var bg = Image("Background", root, new Color(1, 1, 1, 0.12f));
            Stretch(bg.rectTransform, 0); bg.rectTransform.offsetMin = new Vector2(0, size.y * 0.35f); bg.rectTransform.offsetMax = new Vector2(0, -size.y * 0.35f);
            var fillArea = Rect("Fill Area", root); Stretch(fillArea); fillArea.offsetMin = new Vector2(10, size.y * 0.35f); fillArea.offsetMax = new Vector2(-10, -size.y * 0.35f);
            var fill = Image("Fill", fillArea, Accent); Stretch(fill.rectTransform);
            var handleArea = Rect("Handle Slide Area", root); Stretch(handleArea); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var handle = Image("Handle", handleArea, TextMain, ProceduralTextures.Circle(), false);
            handle.rectTransform.sizeDelta = new Vector2(size.y, 0);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1; s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static Toggle Toggle(string name, Transform parent, string text, bool value, Action<bool> onChange, Vector2 size)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            var box = Image("Box", root, PanelLight);
            Place(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(size.y, size.y));
            var check = Image("Check", box.transform, Accent);
            Stretch(check.rectTransform, 8);
            var label = Label("Label", root, text, 30, TextMain, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(size.y + 18, 0);
            var t = root.gameObject.AddComponent<Toggle>();
            t.targetGraphic = box;
            t.graphic = check;
            t.isOn = value;
            t.onValueChanged.AddListener(v => { AudioManager.UIClick(); onChange?.Invoke(v); });
            return t;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter, int pad = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.childControlHeight = false; v.childControlWidth = false; v.childForceExpandHeight = false; v.childForceExpandWidth = false;
            v.padding = new RectOffset(pad, pad, pad, pad);
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlHeight = false; h.childControlWidth = false; h.childForceExpandHeight = false; h.childForceExpandWidth = false;
            return h;
        }

        /// <summary>Settings controls shared by the main menu and the pause menu.</summary>
        public static void SettingsControls(Transform parent, bool includeQuality)
        {
            var musicRow = Rect("MusicRow", parent); musicRow.sizeDelta = new Vector2(760, 70);
            var ml = Label("MusicLabel", musicRow, "Music", 30, TextMain, TextAnchor.MiddleLeft);
            Place(ml.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(220, 60));
            var ms = Slider("MusicSlider", musicRow, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, new Vector2(500, 44));
            Place((RectTransform)ms.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(500, 44));

            var sfxRow = Rect("SfxRow", parent); sfxRow.sizeDelta = new Vector2(760, 70);
            var sl = Label("SfxLabel", sfxRow, "Sound FX", 30, TextMain, TextAnchor.MiddleLeft);
            Place(sl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(220, 60));
            var ss = Slider("SfxSlider", sfxRow, GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, new Vector2(500, 44));
            Place((RectTransform)ss.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(500, 44));

            Toggle("SoundToggle", parent, "Sound on", GameSettings.SoundOn, v => GameSettings.SoundOn = v, new Vector2(760, 56));
            Toggle("FpsToggle", parent, "Show FPS counter", GameSettings.ShowFps, v => GameSettings.ShowFps = v, new Vector2(760, 56));

            if (includeQuality)
            {
                var row = Rect("QualityRow", parent); row.sizeDelta = new Vector2(760, 80);
                var ql = Label("QualityLabel", row, "Graphics", 30, TextMain, TextAnchor.MiddleLeft);
                Place(ql.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(220, 60));
                Button qb = null;
                qb = Button("QualityButton", row, QualityLabel(GameSettings.Quality), () =>
                {
                    GameSettings.Quality = (QualityTier)(((int)GameSettings.Quality + 1) % 4);
                    qb.GetComponentInChildren<Text>().text = QualityLabel(GameSettings.Quality);
                }, new Vector2(500, 64), false, 28);
                Place((RectTransform)qb.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(500, 64));
                var note = Label("QualityNote", parent, "Graphics changes apply fully on the next level load.", 22, TextDim);
                note.rectTransform.sizeDelta = new Vector2(760, 34);
            }
        }

        public static string QualityLabel(QualityTier q)
        {
            switch (q)
            {
                case QualityTier.Low: return "LOW (fastest)";
                case QualityTier.Medium: return "MEDIUM";
                case QualityTier.High: return "HIGH (best looking)";
                default: return "AUTO (adaptive)";
            }
        }
    }

    /// <summary>Keeps UI inside the device safe area.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0 || Screen.height == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Small scale bounce when a button is pressed or hovered.</summary>
    public class ButtonPulse : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        float target = 1f, cur = 1f;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => target = 1.04f;
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => target = 1f;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => target = 0.95f;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => target = 1.04f;
        void OnDisable() { cur = target = 1f; transform.localScale = Vector3.one; }
        void Update()
        {
            cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * cur;
        }
    }
}
