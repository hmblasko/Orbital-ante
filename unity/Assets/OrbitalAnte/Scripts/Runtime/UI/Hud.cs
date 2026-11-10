using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Test-arena HUD: crosshair, a hit marker, and a debug readout (frame time, and the part under the crosshair
    /// with its hit points). F3 toggles the debug readout.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float HitMarkerTime = 0.14f;

        Canvas _canvas;
        Image[] _hitMarker;
        Text _frameText;
        Text _aimText;
        Text _helpText;
        bool _debugVisible = true;

        float _hitMarkerUntil;
        Color _hitMarkerColor = Color.white;

        float _frameAccumulator;
        int _frameCount;
        float _worstFrame;
        float _nextFrameReport;

        /// <summary>Set by the arena so the HUD can show what the crosshair is on.</summary>
        public TestGun Gun;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("HUD", uiRoot, 10);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // Crosshair: a small dot with a dark outline so it reads against any background.
            var outline = UiKit.NewRect("CrosshairOutline", _canvas.transform);
            UiKit.Pin(outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            UiKit.AddImage(outline, new Color(0f, 0f, 0f, 0.7f), UiKit.Circle);
            var dot = UiKit.NewRect("CrosshairDot", _canvas.transform);
            UiKit.Pin(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            UiKit.AddImage(dot, UiKit.Cream, UiKit.Circle);

            // Hit marker: four short diagonal ticks.
            _hitMarker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var tick = UiKit.NewRect("HitTick", _canvas.transform);
                float angle = 45f + 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = dir * 17f;
                tick.sizeDelta = new Vector2(14f, 4f);
                tick.localRotation = Quaternion.Euler(0f, 0f, angle);
                _hitMarker[i] = UiKit.AddImage(tick, Color.white);
                _hitMarker[i].enabled = false;
            }

            _aimText = UiKit.Label(_canvas.transform, string.Empty, 20, UiKit.Cream);
            UiKit.Pin((RectTransform)_aimText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(500f, 30f));
            AddShadow(_aimText);

            _frameText = UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Cream, TextAnchor.UpperLeft);
            UiKit.Pin((RectTransform)_frameText.transform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(520f, 60f));
            AddShadow(_frameText);

            _helpText = UiKit.Label(_canvas.transform, string.Empty, 17, UiKit.Cream, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_helpText.transform, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(900f, 54f));
            AddShadow(_helpText);
            _helpText.text = "Respawn dummies: T or D-Pad Up     Pause: Esc or Start     Debug readout: F3";

            CombatEvents.PlayerHitSomething += OnPlayerHit;
        }

        static void AddShadow(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void OnDestroy()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            _hitMarkerUntil = Time.unscaledTime + (outcome.Severed ? HitMarkerTime * 1.8f : HitMarkerTime);
            _hitMarkerColor = outcome.Killed ? UiKit.Danger : outcome.Severed ? UiKit.PanelEdge : Color.white;
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            if (GameInput.Asset != null && GameInput.ToggleDebug.WasPressedThisFrame())
            {
                _debugVisible = !_debugVisible;
            }

            bool showMarker = Time.unscaledTime < _hitMarkerUntil;
            for (int i = 0; i < _hitMarker.Length; i++)
            {
                _hitMarker[i].enabled = showMarker;
                _hitMarker[i].color = _hitMarkerColor;
            }

            _frameText.enabled = _debugVisible;
            _aimText.enabled = _debugVisible;
            _helpText.enabled = _debugVisible;

            UpdateFrameTime();

            if (_debugVisible)
            {
                IHittable aimed = Gun != null ? Gun.AimedAt : null;
                // A destroyed target compares equal to null in Unity even though the interface reference is not.
                var asObject = aimed as Object;
                _aimText.text = aimed != null && asObject != null ? aimed.DescribeForDebug() : string.Empty;
            }
        }

        void UpdateFrameTime()
        {
            float dt = Time.unscaledDeltaTime;
            _frameAccumulator += dt;
            _frameCount++;
            if (dt > _worstFrame) _worstFrame = dt;

            if (Time.unscaledTime < _nextFrameReport) return;
            _nextFrameReport = Time.unscaledTime + 0.5f;

            if (_frameCount > 0 && _debugVisible)
            {
                float average = _frameAccumulator / _frameCount;
                _frameText.text =
                    (average * 1000f).ToString("0.0") + " ms avg   " +
                    (_worstFrame * 1000f).ToString("0.0") + " ms worst   " +
                    Mathf.RoundToInt(1f / Mathf.Max(0.0001f, average)) + " fps\n" +
                    "Seed " + GameSession.Seed;
            }
            _frameAccumulator = 0f;
            _frameCount = 0;
            _worstFrame = 0f;
        }
    }
}
