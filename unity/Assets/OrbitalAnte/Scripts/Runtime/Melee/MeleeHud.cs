using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// What the melee slot shows, on its own canvas so it works in the arena and the solar system alike:
    /// - a small mark under the crosshair when a swing now would hit a living enemy (Halo's red reticle; research
    ///   report: first-person players misjudge melee distance), from the same test the swing uses;
    /// - TAKEDOWN under it when the knife's stab would kill (the same function the stab uses);
    /// - the item in hand and its key, with a heat bar for an item that overheats;
    /// - what an item on the ground does, while looking at it, and what the held one does.
    /// Text is only rebuilt when it changes, so a fight makes no garbage here.
    /// </summary>
    public sealed class MeleeHud : MonoBehaviour
    {
        static readonly Color Mark = new Color(1f, 0.80f, 0.36f);
        static readonly Color Hot = new Color(1f, 0.45f, 0.18f);
        static readonly Color Cool = new Color(0.55f, 0.85f, 1f);

        Canvas _canvas;
        Image _mark;
        Text _takedown, _line, _note;
        RectTransform _heatBack, _heatFill;
        Image _heatImage;
        MeleeItem _shownHeld, _shownLooked;
        bool _shownLock, _shownRolls;
        /// <summary>True while the controller's swing button rolls instead (floating free): the line then names no key.</summary>
        public bool KeyRolls;

        public static MeleeHud Create(Transform parent)
        {
            var go = new GameObject("MeleeHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<MeleeHud>();
            hud.Build();
            return hud;
        }

        /// <summary>True while the in-range mark is showing (tests).</summary>
        public bool MarkShown => _mark != null && _mark.enabled;
        public bool TakedownShown => _takedown != null && _takedown.gameObject.activeSelf;

        void Build()
        {
            // Under the hit marker (13), over the HUD (10).
            _canvas = UiKit.CreateCanvas("MeleeHUD", transform, 11);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            var mark = UiKit.NewRect("InRange", _canvas.transform);
            UiKit.Pin(mark, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(10f, 10f));
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _mark = UiKit.AddImage(mark, Mark);

            _takedown = HullKit.AsStencil(UiKit.Label(_canvas.transform, "TAKEDOWN", 20, Mark, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_takedown.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(240f, 26f));
            UiKit.Halo(_takedown, true);

            // The item and its key, above the gun ladder's line (136) and below its rare-gun note (222).
            _line = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Cream, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_line.transform, new Vector2(0.5f, 0f), new Vector2(0f, 166f), new Vector2(560f, 22f));
            UiKit.Halo(_line);

            _heatBack = UiKit.NewRect("Heat", _canvas.transform);
            UiKit.Pin(_heatBack, new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(140f, 6f));
            UiKit.AddImage(_heatBack, new Color(0f, 0f, 0f, 0.55f));
            _heatFill = UiKit.NewRect("Fill", _heatBack);
            _heatFill.anchorMin = new Vector2(0f, 0f);
            _heatFill.anchorMax = new Vector2(0f, 1f);
            _heatFill.pivot = new Vector2(0f, 0.5f);
            _heatFill.anchoredPosition = Vector2.zero;
            _heatFill.sizeDelta = new Vector2(0f, 0f);
            // White here: the bar's colour is set through its renderer each frame, which multiplies with this.
            _heatImage = UiKit.AddImage(_heatFill, Color.white);
            _heatImage.canvasRenderer.SetColor(Cool);

            _note = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 15, Mark, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_note.transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(760f, 22f));
            UiKit.Halo(_note);
            Hide();
        }

        public void Hide()
        {
            if (_mark == null) return;
            _mark.enabled = false;
            _takedown.gameObject.SetActive(false);
            _line.gameObject.SetActive(false);
            _heatBack.gameObject.SetActive(false);
            _note.gameObject.SetActive(false);
            _shownHeld = _shownLooked = null;
        }

        /// <summary>Lays out this frame's state. <paramref name="heat"/> is 0 to 1.</summary>
        public void Show(MeleeItem held, MeleeItem looked, bool inRange, bool takedown, float heat, bool locked)
        {
            if (_mark == null) return;
            bool watching = OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;
            if (watching) { Hide(); return; }
            if (_mark.enabled != inRange) _mark.enabled = inRange;
            if (_takedown.gameObject.activeSelf != takedown) _takedown.gameObject.SetActive(takedown);

            bool hasHeld = held != null;
            if (_line.gameObject.activeSelf != hasHeld) _line.gameObject.SetActive(hasHeld);
            if (hasHeld && (held != _shownHeld || locked != _shownLock || KeyRolls != _shownRolls))
            {
                _line.text = MeleeWords.HeldLine(held.Spec, KeyRolls ? "" : Key(), locked);
                _shownLock = locked;
                _shownRolls = KeyRolls;
            }
            bool heats = hasHeld && held.Spec.HeatSwings > 0;
            if (_heatBack.gameObject.activeSelf != heats) _heatBack.gameObject.SetActive(heats);
            if (heats)
            {
                // Only when the bar's width changes by a whole pixel, so the canvas is not rebuilt every frame.
                float width = Mathf.Round(140f * Mathf.Clamp01(heat));
                if (!Mathf.Approximately(_heatFill.sizeDelta.x, width)) _heatFill.sizeDelta = new Vector2(width, 0f);
                // Through the renderer, so a changing colour does not rebuild the bar's mesh every frame.
                _heatImage.canvasRenderer.SetColor(locked ? Hot : Color.Lerp(Cool, Hot, heat));
            }

            MeleeItem about = looked != null ? looked : held;
            bool note = about != null && !string.IsNullOrEmpty(about.Spec.PickupNote);
            if (_note.gameObject.activeSelf != note) _note.gameObject.SetActive(note);
            if (note && (held != _shownHeld || looked != _shownLooked))
                _note.text = MeleeWords.Note(about.Spec, about == looked, InteractKey());
            _shownHeld = held;
            _shownLooked = looked;
        }

        static string Key()
        {
            if (GameInput.Melee == null) return "";
            return GameInput.Describe(GameInput.Melee);
        }

        static string InteractKey() => GameInput.Interact != null ? GameInput.Describe(GameInput.Interact) : "";
    }
}
