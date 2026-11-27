using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// Shows the envelope: the sealed one at the start of a run, and the reveal when one is opened. Everything on
    /// it comes from <see cref="EnvelopeScene.Frame"/>; this only turns that list into images and labels each
    /// frame (keyed, so nothing is created twice) and plays the reveal's sounds. Runs on unscaled time and stops
    /// while the game is paused. Over the HUD, under the pause menu.
    /// </summary>
    public sealed class EnvelopeScreen : MonoBehaviour
    {
        const int SortingOrder = 70;

        Canvas _canvas;
        RectTransform _root;
        /// <summary>The 1280 x 800 stage the scene is laid out on, centred whatever the screen's shape.</summary>
        RectTransform _stage;
        readonly Dictionary<string, Graphic> _graphics = new Dictionary<string, Graphic>();
        readonly Dictionary<string, RectTransform> _groups = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, CanvasGroup> _groupAlpha = new Dictionary<string, CanvasGroup>();
        readonly List<DrawItem> _items = new List<DrawItem>();
        readonly HashSet<string> _seen = new HashSet<string>();
        readonly Dictionary<string, int> _childIndex = new Dictionary<string, int>();
        Action _pendingClosed;
        int _closedFrame;
        List<EnvelopeCue> _cues = new List<EnvelopeCue>();
        int _nextCue;

        EnvelopeShow _show;
        float _t;
        float _readyAt;
        Action _closed;
        bool _playWasOn;

        public bool Showing => _show != null;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("Envelope", uiRoot, SortingOrder);
            // Nothing here is clicked: presses are read straight from the input actions.
            Destroy(_canvas.GetComponent<GraphicRaycaster>());
            _root = (RectTransform)_canvas.transform;
            _stage = UiKit.NewRect("Stage", _root);
            UiKit.Pin(_stage, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(EnvelopeScene.ScreenW, EnvelopeScene.ScreenH));
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Starts showing. <paramref name="closed"/> runs once when the player closes it.</summary>
        public void Show(EnvelopeShow show, Action closed)
        {
            if (_show != null) Close();
            _show = show;
            _closed = closed;
            _t = 0f;
            _cues = EnvelopeScene.Cues(show);
            _nextCue = 0;
            // A press only counts once the envelope has had a moment to arrive, so a held button cannot skip it unseen.
            _readyAt = show.mode == EnvelopeMode.RunStart ? 0.8f : 0.6f;
            _canvas.gameObject.SetActive(true);
            // A run-start envelope is shown while the run is still being set up (its controls not on yet), so it
            // always hands the controls back and holds the world, wherever the run was started from (21 Nov 2026:
            // from the title screen the ante clock and the enemies used to run on behind it).
            bool runStart = show.mode == EnvelopeMode.RunStart;
            _playWasOn = runStart || (GameInput.Play != null && GameInput.Play.enabled);
            if (GameInput.Play != null && GameInput.Play.enabled) GameInput.Play.Disable();
            // The world waits while the envelope is on screen (enemies must not act while the controls are off).
            TimeFlow.Held = runStart || GameSession.Mode == GameMode.Playing;
            Apply();
        }

        /// <summary>Closes it now (a run being torn down, or the player done with it).</summary>
        public void Close()
        {
            if (_show == null) return;
            _show = null;
            _canvas.gameObject.SetActive(false);
            TimeFlow.Held = false;
            // Give the controls back only to a run that is still being played and not paused.
            if (_playWasOn && GameSession.Mode == GameMode.Playing && !GameSession.Paused) GameInput.Play.Enable();
            _playWasOn = false;
            // Whatever comes next starts a frame later, so the press that closed the envelope cannot also press a
            // button on the page that opens behind it.
            _pendingClosed = _closed;
            _closed = null;
            _closedFrame = Time.frameCount;
        }

        void OnDisable()
        {
            TimeFlow.Held = false;
        }

        void Update()
        {
            if (_pendingClosed != null && Time.frameCount > _closedFrame)
            {
                Action closed = _pendingClosed;
                _pendingClosed = null;
                closed();
            }
            if (_show == null) return;
            if (GameSession.Paused) return;
            // Unpausing gives the run its controls back; while the envelope shows they stay off.
            if (GameSession.Mode == GameMode.Playing && GameInput.Play.enabled)
            {
                GameInput.Play.Disable();
                _playWasOn = true;
            }
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            PlayCues();

            if (_t >= _readyAt && Pressed())
            {
                if (_show.mode == EnvelopeMode.Reveal)
                {
                    EnvelopeScene.Beats b = EnvelopeScene.BeatsFor(_show.rarity);
                    // The prize is already in the collection; a press during the show skips to the result.
                    if (_t < b.SkipTo)
                    {
                        _t = b.SkipTo;
                        SkipCuesTo(_t);
                    }
                    else if (_t >= b.Done - 0.3f) { Close(); return; }
                }
                else { Close(); return; }
            }
            if (_show.mode == EnvelopeMode.RunStart)
            {
                float auto = EnvelopeConfig.Load().runStartAutoClose;
                if (auto > 0f && _t >= auto) { Close(); return; }
            }
            Apply();
        }

        static bool Pressed()
        {
            if (GameInput.UiSubmit.WasPressedThisFrame() || GameInput.UiCancel.WasPressedThisFrame() || GameInput.UiClick.WasPressedThisFrame()) return true;
            Keyboard k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame)) return true;
            return false;
        }

        void PlayCues()
        {
            while (_nextCue < _cues.Count && _cues[_nextCue].time <= _t)
            {
                EnvelopeCue cue = _cues[_nextCue++];
                if (!Audio.AudioDirector.PlayLocal(cue.eventId, cue.volume, cue.pitch) && !string.IsNullOrEmpty(cue.fallbackId))
                    Audio.AudioDirector.PlayLocal(cue.fallbackId, cue.volume, cue.pitch);
            }
        }

        void SkipCuesTo(float t)
        {
            // Skipping still plays the prize's own sound, never the build-up.
            EnvelopeCue last = default;
            bool hasLast = false;
            while (_nextCue < _cues.Count && _cues[_nextCue].time <= t)
            {
                last = _cues[_nextCue++];
                hasLast = true;
            }
            if (hasLast && last.eventId != null && last.eventId.StartsWith("envelope.reveal"))
            {
                if (!Audio.AudioDirector.PlayLocal(last.eventId, last.volume, last.pitch) && !string.IsNullOrEmpty(last.fallbackId))
                    Audio.AudioDirector.PlayLocal(last.fallbackId, last.volume, last.pitch);
            }
        }

        // ------------------------------------------------------------------ drawing the list

        void Apply()
        {
            EnvelopeScene.Frame(_show, _t, _items);
            _seen.Clear();
            int rootIndex = 0, stageIndex = 0;
            bool stagePlaced = false;
            var childIndex = _childIndex;
            childIndex.Clear();
            // Fit the 1280 x 800 stage inside the screen whatever its shape (the canvas is only 1280 x 800 at 16:10).
            Rect area = _root.rect;
            float fit = Mathf.Min(area.width / EnvelopeScene.ScreenW, area.height / EnvelopeScene.ScreenH);
            if (fit > 0f) _stage.localScale = new Vector3(fit, fit, 1f);
            for (int i = 0; i < _items.Count; i++)
            {
                DrawItem d = _items[i];
                if (d.kind == DrawKind.Group && (d.w <= 0f || d.h <= 0f)) continue;
                _seen.Add(d.key);
                // Things the size of the whole screen (the darkening, the flash) cover the real screen, whatever its shape.
                bool fullScreen = d.kind == DrawKind.Image && string.IsNullOrEmpty(d.parent) && d.texture == EnvelopeScene.TexBackdrop;
                RectTransform parent = _stage;
                bool child = !string.IsNullOrEmpty(d.parent) && _groups.TryGetValue(d.parent, out parent);
                if (!child) parent = fullScreen ? _root : _stage;
                RectTransform rt = d.kind == DrawKind.Group ? Group(d) : Element(d, parent);
                if (rt.parent != parent) rt.SetParent(parent, false);
                if (child)
                {
                    childIndex.TryGetValue(d.parent, out int ci);
                    rt.SetSiblingIndex(ci);
                    childIndex[d.parent] = ci + 1;
                }
                else if (fullScreen) rt.SetSiblingIndex(rootIndex++);
                else
                {
                    // The stage sits among the full-screen pieces where its first item comes in the list.
                    if (!stagePlaced) { _stage.SetSiblingIndex(rootIndex++); stagePlaced = true; }
                    rt.SetSiblingIndex(stageIndex++);
                }
                if (fullScreen)
                {
                    UiKit.Stretch(rt);
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                }
                else Place(rt, d);
                if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            }
            foreach (var pair in _graphics)
                if (!_seen.Contains(pair.Key) && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
            foreach (var pair in _groups)
                if (!_seen.Contains(pair.Key) && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
        }

        RectTransform Group(DrawItem d)
        {
            if (!_groups.TryGetValue(d.key, out RectTransform rt))
            {
                rt = UiKit.NewRect(d.key, _stage);
                _groupAlpha[d.key] = rt.gameObject.AddComponent<CanvasGroup>();
                if (d.clip)
                {
                    // A stencil mask, not RectMask2D: it follows the group when it turns.
                    Image stencil = rt.gameObject.AddComponent<Image>();
                    stencil.raycastTarget = false;
                    rt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                }
                _groups[d.key] = rt;
            }
            _groupAlpha[d.key].alpha = d.a;
            return rt;
        }

        RectTransform Element(DrawItem d, RectTransform parent)
        {
            if (!_graphics.TryGetValue(d.key, out Graphic g) || g == null)
            {
                RectTransform rt = UiKit.NewRect(d.key, parent);
                if (d.kind == DrawKind.Text)
                {
                    Text label = rt.gameObject.AddComponent<Text>();
                    label.font = UiKit.Font;
                    label.supportRichText = true;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.raycastTarget = false;
                    UiKit.Halo(label);
                    g = label;
                }
                else
                {
                    RawImage image = rt.gameObject.AddComponent<RawImage>();
                    image.raycastTarget = false;
                    g = image;
                }
                _graphics[d.key] = g;
            }
            if (d.kind == DrawKind.Text)
            {
                var label = (Text)g;
                label.text = d.text;
                label.fontSize = d.fontSize;
                UiKit.SetStyle(label, d.bold ? (d.italic ? FontStyle.BoldAndItalic : FontStyle.Bold) : (d.italic ? FontStyle.Italic : FontStyle.Normal));
                label.alignment = d.align == TextAlign.Left ? TextAnchor.MiddleLeft : (d.align == TextAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
            }
            else
            {
                var image = (RawImage)g;
                Texture2D tex = d.texture == _show.cardTexture && _card != null ? _card : EnvelopeTextures.Get(d.texture);
                if (image.texture != tex) image.texture = tex;
            }
            g.color = new Color(d.r, d.g, d.b, d.a);
            return g.rectTransform;
        }

        Texture2D _card;

        /// <summary>The prize card for the reveal, painted by the caller before showing.</summary>
        public void UseCard(Texture2D card) => _card = card;

        static void Place(RectTransform rt, DrawItem d)
        {
            // Every item is placed from its parent's top left, in reference pixels with y down.
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(d.px, 1f - d.py);
            rt.sizeDelta = new Vector2(d.w, d.h);
            rt.anchoredPosition = new Vector2(d.x, -d.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, -d.rotation);
            rt.localScale = new Vector3(d.sx, d.sy, 1f);
        }
    }
}
