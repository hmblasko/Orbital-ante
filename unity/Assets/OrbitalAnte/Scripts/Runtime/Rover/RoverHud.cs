using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Rover
{
    /// <summary>
    /// What the driver sees on the visor: speed in large numbers low in the middle, the boost tank as a bar under
    /// it, and a word when the rover is on its back. Only while driving; everything else stays on the suit's gauges.
    /// </summary>
    public sealed class RoverHud : MonoBehaviour
    {
        MoonRover _rover;
        Canvas _canvas;
        Text _speed, _unit, _note;
        RectTransform _boostFill;
        Image _boostImage;
        int _shownKmh = -1;
        bool _shownOver;

        static readonly Color Boost = new Color(1f, 0.62f, 0.25f);

        public static RoverHud Build(MoonRover rover)
        {
            var go = new GameObject("RoverHud");
            var hud = go.AddComponent<RoverHud>();
            hud._rover = rover;
            hud._canvas = UiKit.CreateCanvas("RoverHUD", go.transform, 9);

            hud._speed = UiKit.Halo(UiKit.Label(hud._canvas.transform, "0", 44, UiKit.Cream));
            UiKit.Pin((RectTransform)hud._speed.transform, new Vector2(0.5f, 0f), new Vector2(-24f, 92f), new Vector2(160f, 52f));
            hud._speed.alignment = TextAnchor.MiddleRight;
            hud._unit = UiKit.Halo(UiKit.Label(hud._canvas.transform, "km/h", 18, UiKit.Muted, TextAnchor.MiddleLeft, FontStyle.Normal));
            UiKit.Pin((RectTransform)hud._unit.transform, new Vector2(0.5f, 0f), new Vector2(64f, 84f), new Vector2(80f, 24f));

            var back = UiKit.NewRect("BoostBack", hud._canvas.transform);
            UiKit.Pin(back, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(220f, 8f));
            UiKit.AddImage(back, new Color(0f, 0f, 0f, 0.45f), UiKit.Rounded);
            hud._boostFill = UiKit.NewRect("BoostFill", back);
            hud._boostFill.anchorMin = new Vector2(0f, 0f);
            hud._boostFill.anchorMax = new Vector2(1f, 1f);
            hud._boostFill.pivot = new Vector2(0f, 0.5f);
            hud._boostFill.offsetMin = Vector2.zero;
            hud._boostFill.offsetMax = Vector2.zero;
            hud._boostImage = UiKit.AddImage(hud._boostFill, Boost, UiKit.Rounded);

            hud._note = UiKit.Halo(UiKit.Label(hud._canvas.transform, string.Empty, 22, UiKit.Cream));
            UiKit.Pin((RectTransform)hud._note.transform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(900f, 30f));
            hud.SetShown(false);
            return hud;
        }

        public void SetShown(bool shown)
        {
            if (_canvas != null && _canvas.gameObject.activeSelf != shown) _canvas.gameObject.SetActive(shown);
        }

        void LateUpdate()
        {
            if (_rover == null) { Destroy(gameObject); return; }
            if (_rover.Driver == null || _rover.Driver.Dead || OrbitalAnte.Core.GameSession.Paused)
            {
                SetShown(false);
                return;
            }
            SetShown(true);
            int kmh = Mathf.RoundToInt(_rover.Speed * 3.6f);
            if (kmh != _shownKmh) { _shownKmh = kmh; _speed.text = kmh.ToString(); }
            _boostFill.anchorMax = new Vector2(Mathf.Clamp01(_rover.Boost.Amount), 1f);
            _boostImage.color = _rover.Boost.Lit ? Color.white : Boost;
            bool over = _rover.Overturned;
            if (over != _shownOver)
            {
                _shownOver = over;
                _note.text = over ? "On its back. " + OrbitalAnte.Core.GameInput.Describe(OrbitalAnte.Core.GameInput.Reload) + ": put it back on its wheels" : string.Empty;
            }
        }
    }
}
