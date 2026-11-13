using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Small helpers for building menus in code, with one shared chunky, warm, slightly goofy look.
    /// (Placeholder styling: proper fonts and art come with the UI pass.)
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.13f, 0.11f, 0.10f);
        public static readonly Color PanelColor = new Color(0.19f, 0.16f, 0.14f, 0.97f);
        public static readonly Color PanelEdge = new Color(0.93f, 0.53f, 0.16f);
        public static readonly Color ButtonColor = new Color(0.33f, 0.28f, 0.24f);
        public static readonly Color ButtonHover = new Color(0.47f, 0.39f, 0.31f);
        public static readonly Color ButtonSelected = new Color(0.93f, 0.53f, 0.16f);
        public static readonly Color ButtonPressed = new Color(1f, 0.74f, 0.32f);
        public static readonly Color ButtonDisabled = new Color(0.25f, 0.23f, 0.22f, 0.7f);
        public static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.58f);
        public static readonly Color Danger = new Color(0.86f, 0.25f, 0.20f);

        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 800f);

        static Font _font;
        static Sprite _rounded;
        static Sprite _circle;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    _font = null;
                }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                return _font;
            }
        }

        /// <summary>A white rounded rectangle that stretches without distorting its corners.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 48;
                const float radius = 14f;
                var tex = NewTexture(size);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float cx = Mathf.Clamp(px, radius, size - radius);
                        float cy = Mathf.Clamp(py, radius, size - radius);
                        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                        float a = Mathf.Clamp01(radius - d + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                float border = radius + 2f;
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                _rounded.name = "OA_Rounded";
                return _rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 64;
                var tex = NewTexture(size);
                var pixels = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                _circle.name = "OA_Circle";
                return _circle;
            }
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // ------------------------------------------------------------------ building blocks

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Pins a rect to a point of its parent. Anchor and pivot share the same normalised position.</summary>
        public static void Pin(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        public static Image AddImage(RectTransform rt, Color color, Sprite sprite = null, bool raycastTarget = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            return image;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = NewRect("Label", parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A label that takes a fixed height inside a layout group.</summary>
        public static Text LayoutLabel(Transform parent, string text, int size, Color color, float height,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var label = Label(parent, text, size, color, anchor, style);
            SetLayout(label.gameObject, height);
            return label;
        }

        public static LayoutElement SetLayout(GameObject go, float height, float width = -1f, float flexibleWidth = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (height >= 0f)
            {
                le.minHeight = height;
                le.preferredHeight = height;
            }
            if (width >= 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static Button Button(Transform parent, string label, UnityAction onClick, float height = 56f, int fontSize = 26)
        {
            var rt = NewRect("Button " + label, parent);
            var image = AddImage(rt, Color.white, Rounded, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonSelected;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var text = Label(rt, label, fontSize, Cream);
            Stretch((RectTransform)text.transform);
            SetLayout(rt.gameObject, height);

            button.onClick.AddListener(ClickSound);
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        static void ClickSound() => Audio.AudioDirector.PlayLocal("ui.select");

        public static Text ButtonText(Button button)
        {
            return button.GetComponentInChildren<Text>(true);
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding)
        {
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing)
        {
            var group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return group;
        }

        /// <summary>
        /// A centred card that grows to fit its contents: dark rounded panel with an orange edge.
        /// Returns the content rect to add children to.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, float width)
        {
            var edge = NewRect(name, parent);
            edge.anchorMin = edge.anchorMax = edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = new Vector2(width, 100f);
            AddImage(edge, PanelEdge, Rounded, true);
            var edgeGroup = VStack(edge, 0f, 5);
            edgeGroup.childForceExpandHeight = false;
            var fitter = edge.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var body = NewRect("Body", edge);
            AddImage(body, PanelColor, Rounded, false);
            VStack(body, 12f, 26);
            return body;
        }
    }
}
