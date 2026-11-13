using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Test-arena HUD: crosshair, hit marker, ammo, health, the pick-up prompt, and a debug readout (frame time,
    /// and the part under the crosshair with its hit points). F3 toggles the debug readout.
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

        /// <summary>Set when a run starts so the HUD can show the gun, ammo and what the crosshair is on.</summary>
        public PlayerWeapons Weapons;
        public PlayerHealth Health;

        Text _ammoText;
        Text _weaponText;
        Text _promptText;
        Text _bannerText;
        Image _healthFill;
        RectTransform _healthBar;
        Image _hurtFlash;
        static readonly Color DisarmColor = new Color(0.45f, 0.85f, 1f);

        /// <summary>Extra line for the debug readout, supplied by whichever test world is running.</summary>
        public System.Func<string> ExtraDebug;

        /// <summary>Everything that only makes sense in the combat arena (crosshair, ammo, health) lives under this.</summary>
        RectTransform _arenaOnly;

        /// <summary>Shows or hides the combat parts. The frame-time readout stays either way.</summary>
        public void SetArenaMode(bool arena)
        {
            if (_arenaOnly != null) _arenaOnly.gameObject.SetActive(arena);
        }

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("HUD", uiRoot, 10);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _arenaOnly = UiKit.NewRect("ArenaOnly", _canvas.transform);
            UiKit.Stretch(_arenaOnly);

            // Crosshair: a small dot with a dark outline so it reads against any background.
            var outline = UiKit.NewRect("CrosshairOutline", _arenaOnly);
            UiKit.Pin(outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            UiKit.AddImage(outline, new Color(0f, 0f, 0f, 0.7f), UiKit.Circle);
            var dot = UiKit.NewRect("CrosshairDot", _arenaOnly);
            UiKit.Pin(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            UiKit.AddImage(dot, UiKit.Cream, UiKit.Circle);

            // Hit marker: four short diagonal ticks.
            _hitMarker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var tick = UiKit.NewRect("HitTick", _arenaOnly);
                float angle = 45f + 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = dir * 17f;
                tick.sizeDelta = new Vector2(14f, 4f);
                tick.localRotation = Quaternion.Euler(0f, 0f, angle);
                _hitMarker[i] = UiKit.AddImage(tick, Color.white);
                _hitMarker[i].enabled = false;
            }

            _aimText = UiKit.Label(_arenaOnly, string.Empty, 20, UiKit.Cream);
            UiKit.Pin((RectTransform)_aimText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(500f, 30f));
            AddShadow(_aimText);

            _frameText = UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Cream, TextAnchor.UpperLeft);
            UiKit.Pin((RectTransform)_frameText.transform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(900f, 108f));
            AddShadow(_frameText);

            _helpText = UiKit.Label(_arenaOnly, string.Empty, 17, UiKit.Cream, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_helpText.transform, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(700f, 54f));
            AddShadow(_helpText);
            _helpText.text = "Respawn dummies: T or D-Pad Up     Enemies hold fire: H or D-Pad Down     Pause: Esc or Start     Debug readout: F3";

            // Full-screen red flash when the player is hit. Drawn first so everything else sits on top of it.
            var flash = UiKit.NewRect("HurtFlash", _arenaOnly);
            UiKit.Stretch(flash);
            flash.SetAsFirstSibling();
            _hurtFlash = UiKit.AddImage(flash, new Color(0.8f, 0.05f, 0.05f, 0f));

            // Ammo, bottom right: a big number for the magazine, the gun's name above it.
            _ammoText = UiKit.Label(_arenaOnly, string.Empty, 54, UiKit.Cream, TextAnchor.LowerRight);
            UiKit.Pin((RectTransform)_ammoText.transform, new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(420f, 70f));
            AddShadow(_ammoText);
            _weaponText = UiKit.Label(_arenaOnly, string.Empty, 20, UiKit.PanelEdge, TextAnchor.LowerRight);
            UiKit.Pin((RectTransform)_weaponText.transform, new Vector2(1f, 0f), new Vector2(-26f, 84f), new Vector2(420f, 28f));
            AddShadow(_weaponText);

            // Health, bottom centre: a chunky bar.
            _healthBar = UiKit.NewRect("HealthBar", _arenaOnly);
            UiKit.Pin(_healthBar, new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(288f, 22f));
            UiKit.AddImage(_healthBar, new Color(0f, 0f, 0f, 0.6f), UiKit.Rounded);
            var fill = UiKit.NewRect("Fill", _healthBar);
            // Pivot on the left edge first, so the bar empties from right to left when it is scaled.
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            _healthFill = UiKit.AddImage(fill, UiKit.Cream);

            // "Pick up" prompt under the crosshair, and a banner at the top for arena state.
            _promptText = UiKit.Label(_arenaOnly, string.Empty, 22, UiKit.Cream);
            UiKit.Pin((RectTransform)_promptText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(760f, 30f));
            AddShadow(_promptText);
            _bannerText = UiKit.Label(_arenaOnly, string.Empty, 24, UiKit.PanelEdge);
            UiKit.Pin((RectTransform)_bannerText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 34f));
            AddShadow(_bannerText);

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
            bool big = outcome.Severed || outcome.Disarmed;
            _hitMarkerUntil = Time.unscaledTime + (big ? HitMarkerTime * 1.8f : HitMarkerTime);
            _hitMarkerColor = outcome.Killed ? UiKit.Danger
                : outcome.Severed ? UiKit.PanelEdge
                : outcome.Disarmed ? DisarmColor
                : Color.white;
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
            UpdateCombatReadouts();

            if (_debugVisible)
            {
                IHittable aimed = Weapons != null ? Weapons.AimedAt : null;
                // A destroyed target compares equal to null in Unity even though the interface reference is not.
                var asObject = aimed as Object;
                _aimText.text = aimed != null && asObject != null ? aimed.DescribeForDebug() : string.Empty;
            }
        }

        void UpdateCombatReadouts()
        {
            Weapon weapon = Weapons != null ? Weapons.Current : null;
            if (weapon != null)
            {
                FirearmState state = weapon.State;
                _weaponText.text = weapon.Data.displayName;
                _ammoText.text = state.Reloading ? "RELOADING" : state.Ammo + " / " + state.Spec.MagazineSize;
                _ammoText.color = state.Reloading || state.Ammo == 0 ? UiKit.PanelEdge : UiKit.Cream;
            }
            else
            {
                _weaponText.text = Weapons != null ? "Empty hands" : string.Empty;
                _ammoText.text = string.Empty;
            }

            Weapon pickup = Weapons != null ? Weapons.NearbyPickup : null;
            if (pickup != null && GameInput.Asset != null)
            {
                string key = GameInput.Interact.GetBindingDisplayString(0);
                string button = GameInput.Interact.GetBindingDisplayString(1);
                _promptText.text = key + " / " + button + ":  " +
                                   (weapon != null ? "swap for " : "pick up ") + pickup.Data.displayName;
            }
            else
            {
                _promptText.text = string.Empty;
            }

            if (Health != null)
            {
                _healthBar.gameObject.SetActive(true);
                float f = Health.Fraction;
                _healthFill.rectTransform.localScale = new Vector3(f, 1f, 1f);
                _healthFill.color = f > 0.35f ? UiKit.Cream : UiKit.Danger;

                // A quick red pulse on each hit, and a steady tint while knocked down.
                float sinceHit = Time.unscaledTime - Health.LastHitUnscaledTime;
                float alpha = Health.Alive ? Mathf.Clamp01(1f - sinceHit / 0.35f) * 0.28f : 0.5f;
                _hurtFlash.color = new Color(0.8f, 0.05f, 0.05f, alpha);

                if (!Health.Alive) _bannerText.text = "KNOCKED DOWN.  Back on your feet in a moment.";
                else if (CombatWorld.EnemiesHoldFire) _bannerText.text = "Enemies are holding fire.  H or D-Pad Down starts the fight.";
                else _bannerText.text = string.Empty;
            }
            else
            {
                _healthBar.gameObject.SetActive(false);
                _hurtFlash.color = new Color(0.8f, 0.05f, 0.05f, 0f);
                _bannerText.text = string.Empty;
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
                    "Seed " + GameSession.Seed + GoreReadout() + BudgetReadout() + FinishReadout() +
                    (ExtraDebug != null ? "\n" + ExtraDebug() : string.Empty);
            }
            _frameAccumulator = 0f;
            _frameCount = 0;
            _worstFrame = 0f;
        }

        /// <summary>How the last ten seconds compare with the Steam Deck frame budget.</summary>
        static string BudgetReadout()
        {
            var perf = OrbitalAnte.Perf.PerfMonitor.Instance;
            return perf != null ? "\n" + perf.Describe() : string.Empty;
        }

        /// <summary>Whether bloom, tone mapping and the colour grade are being drawn, and if not, why not.</summary>
        static string FinishReadout()
        {
            var finish = OrbitalAnte.Look.PostFx.Instance;
            return finish != null ? "\nPicture finish: " + finish.Status : string.Empty;
        }

        /// <summary>Live counts for the performance budget: loose chunks, blood droplets and splats.</summary>
        static string GoreReadout()
        {
            var gibs = OrbitalAnte.Dismemberment.GibManager.Instance;
            var gore = OrbitalAnte.Gore.GoreFx.Instance;
            if (gibs == null || gore == null) return string.Empty;
            return "   gibs " + gibs.ActiveCount + "/" + gibs.Config.maxActiveGibs +
                   "   blood " + gore.LiveDroplets + "/" + gore.Config.maxBloodParticles +
                   "   splats " + gore.Decals.LiveCount + "/" + gore.Config.maxDecals +
                   (gore.Decals.UsingProjectors ? " (projectors)" : " (flat)");
        }
    }
}
