using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Small helpers for building menus in code. Since 27 Nov 2026 (realistic look, stage 5) the menus are the
    /// same bolted dark plates as the on-foot readout (<see cref="HullKit"/>): square-cornered plates with a thin
    /// edge and screw heads, headings in the stencil face, buttons that fill with orange and turn their words
    /// dark when chosen. Words are B612 (see <see cref="Font"/>).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.13f, 0.11f, 0.10f);
        public static readonly Color PanelColor = new Color(0.118f, 0.122f, 0.129f, 0.97f);
        public static readonly Color PanelEdge = new Color(0.93f, 0.53f, 0.16f);
        public static readonly Color ButtonColor = new Color(0.215f, 0.215f, 0.225f);
        public static readonly Color ButtonHover = new Color(0.33f, 0.325f, 0.32f);
        public static readonly Color ButtonSelected = new Color(0.93f, 0.53f, 0.16f);
        public static readonly Color ButtonPressed = new Color(1f, 0.74f, 0.32f);
        public static readonly Color ButtonDisabled = new Color(0.17f, 0.17f, 0.175f, 0.7f);
        public static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.58f);
        public static readonly Color Danger = new Color(0.86f, 0.25f, 0.20f);

        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 800f);

        static Font _font, _bold;
        static Sprite _rounded;
        static Sprite _plate;
        static Sprite _circle;
        static Sprite _ring;

        /// <summary>
        /// The game's lettering: B612, drawn for aircraft cockpit screens to stay readable small, at a glance and
        /// in poor light (SIL Open Font Licence, <c>Resources/Fonts</c>). Matthew asked on 15 Nov 2026 for text that is
        /// "more interesting and easy to read". Falls back to Unity's own font if the file is missing.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/B612-Regular");
                if (_font == null) _font = BuiltinFont();
                return _font;
            }
        }

        /// <summary>The bold cut of <see cref="Font"/>: a real bold face, not Unity's smeared fake one.</summary>
        public static Font BoldFont
        {
            get
            {
                if (_bold != null) return _bold;
                _bold = Resources.Load<Font>("Fonts/B612-Bold");
                if (_bold == null) _bold = Font;
                return _bold;
            }
        }

        static Font BuiltinFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                font = null;
            }
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return font;
        }

        /// <summary>
        /// Gives a label the game's font in the asked-for style. Bold uses the bold file (with no fake bolding on
        /// top), so always set a label's style through this, never through <c>fontStyle</c> alone.
        /// </summary>
        public static void SetStyle(Text label, FontStyle style)
        {
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            Font font = bold ? BoldFont : Font;
            // With a real bold file, only the slant (if any) is left for Unity to add.
            FontStyle rest = bold && BoldFont != Font
                ? (style == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal)
                : style;
            if (label.font != font) label.font = font;
            if (label.fontStyle != rest) label.fontStyle = rest;
        }

        /// <summary>
        /// A dark edge under a label so it reads over a bright sky or sand. <paramref name="outline"/> adds a thin
        /// outline all round as well: kept for the big numbers, since it draws the text four more times.
        /// </summary>
        public static Text Halo(Text label, bool outline = false, float strength = 0.75f)
        {
            if (outline)
            {
                var edge = label.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, 0.45f * strength);
                edge.effectDistance = new Vector2(1.2f, -1.2f);
            }
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f * strength);
            shadow.effectDistance = new Vector2(1.5f, -2f);
            return label;
        }

        /// <summary>A white ring, 1/8 of its radius thick, for the visor gauges' arcs (cut with Image.Type.Filled).</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int size = 256;
                var tex = NewTexture(size);
                var pixels = new Color32[size * size];
                float outer = size * 0.5f - 1f;
                float inner = outer * 0.875f;
                float c = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _ring.name = "OA_Ring";
                return _ring;
            }
        }

        /// <summary>
        /// A white rounded rectangle that stretches without distorting its corners: the soft shape, kept for
        /// gauges and the star chart's cards.
        /// </summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = Corners("OA_Rounded", 48, 14f);

        /// <summary>
        /// A plate's shape: corners barely eased (3 px), as sheet metal is cut. Cards and buttons use it.
        /// </summary>
        public static Sprite PlateShape => _plate != null ? _plate : _plate = Corners("OA_Plate", 24, 3f);

        static Sprite Corners(string name, int size, float radius)
        {
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
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
        }

        /// <summary>A white filled circle.</summary>
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
            SetStyle(label, style);
            label.text = text;
            label.fontSize = size;
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

        /// <summary>
        /// A page's heading: the stencil face, orange, standing straight (lettering stencilled on a plate is not
        /// set at a tilt).
        /// </summary>
        public static Text Heading(Transform parent, string text, int size, float height)
        {
            Text label = HullKit.AsStencil(LayoutLabel(parent, text, size, HullKit.Orange, height));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            var image = AddImage(rt, Color.white, PlateShape, true);
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

            rt.gameObject.AddComponent<ChosenInk>().Label = text;

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
            // A bolted plate: a thin lighter edge round a dark face, a screw head in each corner.
            var edge = NewRect(name, parent);
            edge.anchorMin = edge.anchorMax = edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = new Vector2(width, 100f);
            AddImage(edge, HullKit.PlateEdge, PlateShape, true);
            var edgeGroup = VStack(edge, 0f, 2);
            edgeGroup.childForceExpandHeight = false;
            var fitter = edge.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var body = NewRect("Body", edge);
            AddImage(body, PanelColor, PlateShape, false);
            VStack(body, 12f, 26);
            for (int i = 0; i < 4; i++)
            {
                // Outside the stack's layout, so the card's rows are counted as before.
                RectTransform s = NewRect("Screw", body);
                s.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                bool right = (i & 1) == 1, low = i >= 2;
                s.anchorMin = s.anchorMax = new Vector2(right ? 1f : 0f, low ? 0f : 1f);
                s.pivot = new Vector2(0.5f, 0.5f);
                s.anchoredPosition = new Vector2(right ? -11f : 11f, low ? 11f : -11f);
                s.sizeDelta = new Vector2(7f, 7f);
                AddImage(s, HullKit.PlateEdge, Circle);
            }
            return body;
        }
    }

    /// <summary>
    /// A button's words go dark while it is the chosen one, because cream on the orange fill is hard to read.
    /// Whatever colour the words had is put back when the choice moves on, unless something else recoloured them
    /// meanwhile.
    /// </summary>
    public sealed class ChosenInk : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
    {
        [System.NonSerialized] public Text Label;
        Color _was;
        bool _dark;

        void OnEnable()
        {
            // A page shown again with this button still the chosen one gets no select message.
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && events.currentSelectedGameObject == gameObject) OnSelect(null);
        }

        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            if (Label == null || _dark) return;
            _was = Label.color;
            Label.color = UiKit.Ink;
            _dark = true;
        }

        public void OnDeselect(UnityEngine.EventSystems.BaseEventData eventData) => Restore();

        /// <summary>
        /// Sets the label's own colour, the one it shows when its button is not the chosen one. While it is chosen
        /// the label stays dark and takes this colour back when the choice moves on.
        /// </summary>
        public void SetColour(Color colour)
        {
            if (Label == null) return;
            if (_dark) _was = colour;
            else Label.color = colour;
        }

        void OnDisable() => Restore();

        void Restore()
        {
            if (!_dark) return;
            _dark = false;
            if (Label != null && Label.color == UiKit.Ink) Label.color = _was;
        }
    }
}

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Hovering the mouse over a button chooses it, as the D-pad does, so whatever reads the chosen button (a kit's
    /// details on the title screen) follows the mouse too.
    /// </summary>
    public sealed class SelectOnHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler
    {
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Only a mouse that moved: a page appearing under a resting cursor (left in the middle of the screen by
            // the cursor lock) must not move a controller player's choice (Unity review, 27 Nov 2026).
            if (eventData == null || eventData.delta.sqrMagnitude < 0.01f) return;
            var events = UnityEngine.EventSystems.EventSystem.current;
            var selectable = GetComponent<Selectable>();
            if (events != null && selectable != null && selectable.IsInteractable() && events.currentSelectedGameObject != gameObject)
                events.SetSelectedGameObject(gameObject);
        }
    }
}
