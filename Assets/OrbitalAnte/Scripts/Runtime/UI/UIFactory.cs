using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>Builds uGUI widgets in code. Font is Unity's built-in one until we pick a display font.</summary>
    public static class UIFactory
    {
        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Label", parent);
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
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.55f);
            shadow.effectDistance = new Vector2(2, -2);
            return t;
        }

        /// <summary>Card with a chunky orange outline, laid out vertically.</summary>
        public static RectTransform Card(string name, Transform parent, Vector2 size)
        {
            var edge = Image(name, parent, UITheme.CardEdge);
            var rt = edge.rectTransform;
            rt.sizeDelta = size;
            var inner = Image("Inner", rt, UITheme.Card);
            Stretch(inner.rectTransform, 4f);
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return inner.rectTransform;
        }

        public static Text Header(Transform parent, string text, float tiltDegrees = -2f)
        {
            var t = Label(parent, text, UITheme.HeaderSize, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Layout(t.gameObject, 44);
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, tiltDegrees);
            return t;
        }

        public static LayoutElement Layout(GameObject go, float preferredHeight, float preferredWidth = -1, float flexibleWidth = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;
            if (preferredWidth > 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static Button Button(Transform parent, string label, Action onClick, float height = 50f, int fontSize = UITheme.BodySize)
        {
            var img = Image("Button_" + label, parent, Color.white);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = UITheme.Button;
            colors.highlightedColor = UITheme.ButtonHover;
            colors.selectedColor = UITheme.ButtonSelected;
            colors.pressedColor = UITheme.ButtonPressed;
            colors.disabledColor = UITheme.ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var text = Label(img.transform, label, fontSize, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 6f);
            Layout(img.gameObject, height);
            img.gameObject.AddComponent<SelectedWiggle>();
            return btn;
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();

        public static HorizontalLayoutGroup Row(Transform parent, float height, float spacing = 10f)
        {
            var rt = Rect("Row", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
            Layout(rt.gameObject, height);
            return h;
        }

        /// <summary>Vertical scroll list. Returns the content transform to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent, float height, out ScrollRect scroll)
        {
            var viewportImg = Image("Scroll", parent, new Color(0, 0, 0, 0.18f));
            Layout(viewportImg.gameObject, height);
            viewportImg.gameObject.AddComponent<RectMask2D>();
            scroll = viewportImg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewportImg.rectTransform;

            var content = Rect("Content", viewportImg.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(8, 0);
            content.offsetMax = new Vector2(-8, 0);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.padding = new RectOffset(0, 0, 6, 6);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            viewportImg.gameObject.AddComponent<ScrollToSelected>().Init(scroll);
            return content;
        }

        public static void Select(GameObject go)
        {
            if (EventSystem.current == null || go == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(go);
        }
    }

    /// <summary>Keeps the controller-selected row visible inside a scroll list.</summary>
    public class ScrollToSelected : MonoBehaviour
    {
        ScrollRect scroll;
        readonly Vector3[] itemCorners = new Vector3[4];
        readonly Vector3[] viewCorners = new Vector3[4];
        GameObject lastSelected;

        public void Init(ScrollRect s) => scroll = s;

        void LateUpdate()
        {
            if (scroll == null || EventSystem.current == null) return;
            var sel = EventSystem.current.currentSelectedGameObject;
            if (sel == null || sel == lastSelected) return;
            lastSelected = sel;
            if (!sel.transform.IsChildOf(scroll.content)) return;

            ((RectTransform)sel.transform).GetWorldCorners(itemCorners);
            scroll.viewport.GetWorldCorners(viewCorners);
            float delta = 0f;
            if (itemCorners[1].y > viewCorners[1].y) delta = itemCorners[1].y - viewCorners[1].y;
            else if (itemCorners[0].y < viewCorners[0].y) delta = itemCorners[0].y - viewCorners[0].y;
            if (Mathf.Abs(delta) > 0.01f)
            {
                scroll.content.position -= new Vector3(0, delta, 0);
                scroll.StopMovement();
            }
        }
    }

    /// <summary>A little tilt on whichever button is selected, for some TF2-ish life.</summary>
    public class SelectedWiggle : MonoBehaviour
    {
        void Update()
        {
            bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            float target = selected ? Mathf.Sin(Time.unscaledTime * 6f) * 1.2f : 0f;
            float scale = selected ? 1.04f : 1f;
            transform.localRotation = Quaternion.Euler(0, 0, target);
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(scale, scale, 1f), 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
    }
}
