using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MouseGame.UI
{
    /// <summary>
    /// Small runtime builder for uGUI (no scene authoring needed, same look everywhere).
    /// Canvas scales for phones in landscape (reference 1920x1080).
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.12f, 0.10f, 0.09f, 0.88f);
        public static readonly Color Panel = new Color(0.20f, 0.17f, 0.15f, 0.95f);
        public static readonly Color Accent = new Color(0.98f, 0.78f, 0.25f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.80f, 0.40f, 1f);
        public static readonly Color Bad = new Color(0.90f, 0.35f, 0.30f, 1f);
        public static readonly Color ButtonCol = new Color(0.32f, 0.27f, 0.23f, 1f);
        public static readonly Color TextCol = new Color(0.97f, 0.94f, 0.88f, 1f);
        public static readonly Color DimText = new Color(0.75f, 0.70f, 0.62f, 1f);

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        static Sprite round;
        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                // 64x64 white circle generated once (no texture asset needed)
                const int s = 64;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[s * s];
                float r = s / 2f - 1f;
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                    px[y * s + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                round = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
                return round;
            }
        }

        // ------------------------------------------------------------------
        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.6f;
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Anchor(this RectTransform rt, float xMin, float yMin, float xMax, float yMax, Vector2? offMin = null, Vector2? offMax = null)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = offMin ?? Vector2.zero;
            rt.offsetMax = offMax ?? Vector2.zero;
            return rt;
        }

        /// <summary>Fixed-size rect anchored at a point (pivot = anchor).</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Color c, bool rounded = true, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (rounded) { img.sprite = Round; img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
            img.raycastTarget = raycast;
            return img;
        }

        public static Image PanelBox(Transform parent, string name, Color? c = null) => Img(parent, name, c ?? Panel);

        public static Text Label(Transform parent, string text, int size = 36, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextCol;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button Btn(Transform parent, string label, UnityAction onClick, Color? color = null, int fontSize = 36)
        {
            var img = Img(parent, "Button", color ?? ButtonCol);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            var t = Label(img.transform, label, fontSize, TextAnchor.MiddleCenter, null, FontStyle.Bold);
            t.rectTransform.Anchor(0, 0, 1, 1, new Vector2(10, 4), new Vector2(-10, -4));
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 14;
            t.resizeTextMaxSize = fontSize;
            return b;
        }

        public static Text ButtonText(this Button b) => b.GetComponentInChildren<Text>();

        public static InputField Field(Transform parent, string placeholder, string value = "", int size = 34, int charLimit = 24)
        {
            var img = Img(parent, "Input", new Color(0.08f, 0.07f, 0.06f, 0.95f));
            var f = img.gameObject.AddComponent<InputField>();
            var txt = Label(img.transform, "", size, TextAnchor.MiddleLeft);
            txt.rectTransform.Anchor(0, 0, 1, 1, new Vector2(18, 2), new Vector2(-18, -2));
            txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Label(img.transform, placeholder, size, TextAnchor.MiddleLeft, DimText, FontStyle.Italic);
            ph.rectTransform.Anchor(0, 0, 1, 1, new Vector2(18, 2), new Vector2(-18, -2));
            f.textComponent = txt;
            f.placeholder = ph;
            f.characterLimit = charLimit;
            f.text = value;
            f.targetGraphic = img;
            return f;
        }

        /// <summary>Vertical scroll list. Returns the content transform (add rows to it).</summary>
        public static RectTransform ScrollList(Transform parent, string name, out ScrollRect scroll, float spacing = 8f)
        {
            var bg = Img(parent, name, new Color(0, 0, 0, 0.25f));
            scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var viewport = Rect("Viewport", bg.transform).Anchor(0, 0, 1, 1, new Vector2(8, 8), new Vector2(-8, -8));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.childControlHeight = true;
            vl.childControlWidth = true;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static LayoutElement Height(this Component c, float h)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
            return le;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Horizontal row container with layout.</summary>
        public static RectTransform Row(Transform parent, float spacing = 12f)
        {
            var rt = Rect("Row", parent);
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.childControlHeight = true;
            hl.childControlWidth = true;
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth = false;
            hl.childAlignment = TextAnchor.MiddleLeft;
            return rt;
        }

        public static LayoutElement Flex(this Component c, float flexibleWidth = 1f, float minWidth = 0f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = flexibleWidth;
            le.minWidth = minWidth;
            if (flexibleWidth == 0f) le.preferredWidth = minWidth;
            return le;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
