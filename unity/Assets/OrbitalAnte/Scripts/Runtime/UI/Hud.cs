using OrbitalAnte.AI;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Test-arena HUD: crosshair, ammo, health, the pick-up prompt, and a debug readout (frame time,
    /// and the part under the crosshair with its hit points). F3 toggles the debug readout.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        Canvas _canvas;
        Text _frameText;
        Text _aimText;
        Text _helpText;
        // The debug readout (frame times, seed, gore counts, the solar system's numbers) is a tool for building the
        // game, not part of it (Matthew, 21 Nov 2026: the white statistics on screen "completely gone"). It starts
        // hidden, and F3 shows it only in the editor and development builds.
        bool _debugVisible;

        float _frameAccumulator;
        int _frameCount;
        float _worstFrame;
        float _nextFrameReport;

        /// <summary>Set when a run starts so the HUD can show the gun, ammo and what the crosshair is on.</summary>
        public PlayerWeapons Weapons;
        public PlayerHealth Health;

        Text _promptText;
        Text _bannerText;
        VisorGauge _health;
        Image _hurtFlash;
        DamageFeedback _damage;
        HitMarker _hitMarker;

        /// <summary>Extra line for the debug readout, supplied by whichever test world is running.</summary>
        public System.Func<string> ExtraDebug;

        /// <summary>Everything that only makes sense in the combat arena (crosshair, ammo, health) lives under this.</summary>
        RectTransform _arenaOnly;
        bool _arenaMode;

        // The practice range's one note (PracticeNote): unlock goals count only in a run. Once a session.
        float _practiceLeft;
        static bool _practiceNoteShown;

        /// <summary>Unity can keep static values between Play sessions; start clean every time.</summary>
        public static void ResetStatics() => _practiceNoteShown = false;

        /// <summary>Shows or hides the combat parts. The frame-time readout stays either way.</summary>
        public void SetArenaMode(bool arena)
        {
            _arenaMode = arena;
            // A note cut short by a restart or a quit (its clock stands still while paused) never greets the next world.
            _practiceLeft = 0f;
            if (_arenaOnly != null) _arenaOnly.gameObject.SetActive(arena);
        }

        void OnEnable() => CombatEvents.PlayerHitSomething += OnPlayerHit;
        void OnDisable() => CombatEvents.PlayerHitSomething -= OnPlayerHit;

        void OnPlayerHit(HitOutcome outcome)
        {
            // The cheap flags first: a plain hit costs nothing (deck-performance review); the save is read only for a
            // hit that would have met a goal in a run.
            if (!_arenaMode || _practiceNoteShown || !PracticeNote.WouldCount(outcome.Disarmed, outcome.Severed, outcome.Killed, outcome.Enemy)) return;
            bool goalLeft = Save.SaveSystem.Data != null && UnlockBook.Next(Save.SaveSystem.Data) != null;
            if (!PracticeNote.Shows(outcome.Disarmed, outcome.Severed, outcome.Killed, outcome.Enemy, FeelSettings.GuideTips, _practiceNoteShown, goalLeft)) return;
            _practiceNoteShown = true;
            _practiceLeft = PracticeNote.Seconds;
        }

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("HUD", uiRoot, 10);
            // Hits on the player: direction arcs, edge glow, visor cracks, hurt sound. Its own canvas, so it
            // works in the solar system too, where the arena-only parts are hidden.
            _damage = DamageFeedback.Create(uiRoot);
            // Hits the player lands: the X round the crosshair. Also its own canvas, for the same reason.
            _hitMarker = HitMarker.Create(uiRoot);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _arenaOnly = UiKit.NewRect("ArenaOnly", _canvas.transform);
            UiKit.Stretch(_arenaOnly);

            // Crosshair: the same corner brackets and orange square as in the rest of the game (HullKit.Reticle).
            var mark = UiKit.NewRect("Crosshair", _arenaOnly);
            UiKit.Pin(mark, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            UiKit.AddImage(mark, new Color(1f, 1f, 1f, 0.9f), HullKit.Reticle);

            _aimText = UiKit.Label(_arenaOnly, string.Empty, 20, UiKit.Cream);
            UiKit.Pin((RectTransform)_aimText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(500f, 30f));
            AddShadow(_aimText);

            _frameText = UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Cream, TextAnchor.UpperLeft);
            UiKit.Pin((RectTransform)_frameText.transform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(900f, 108f));
            AddShadow(_frameText);

            _helpText = UiKit.Label(_arenaOnly, string.Empty, 17, UiKit.Cream, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_helpText.transform, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(700f, 54f));
            AddShadow(_helpText);
            _helpText.text = "Respawn dummies: T     Switch gun: 1 or D-Pad Up     Enemies hold fire: H or D-Pad Down     Pause: Esc or Start";

            // Full-screen red flash when the player is hit. Drawn first so everything else sits on top of it.
            var flash = UiKit.NewRect("HurtFlash", _arenaOnly);
            UiKit.Stretch(flash);
            flash.SetAsFirstSibling();
            _hurtFlash = UiKit.AddImage(flash, new Color(0.8f, 0.05f, 0.05f, 0f));

            // Health, lower right: the same arc of suit plates as outside the ship (see VisorGauge).
            // It stands to the left of the gun plate (GunLadderFx), which has the lower right corner and shows the
            // gun and its rounds here as it does everywhere else.
            _health = VisorGauge.Build(_arenaOnly, false, "SUIT", UiKit.Cream, 510f, 110f);

            // "Pick up" prompt under the crosshair, and a banner at the top for arena state.
            _promptText = UiKit.Label(_arenaOnly, string.Empty, 22, UiKit.Cream);
            UiKit.Pin((RectTransform)_promptText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(760f, 30f));
            AddShadow(_promptText);
            _bannerText = UiKit.Label(_arenaOnly, string.Empty, 24, UiKit.PanelEdge);
            UiKit.Pin((RectTransform)_bannerText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 34f));
            AddShadow(_bannerText);
        }

        static void AddShadow(Text text)
        {
            UiKit.Halo(text);
        }

        /// <summary>Shows hits on <paramref name="health"/> as seen from <paramref name="view"/>; null stops.</summary>
        public void WatchHurt(PlayerHealth health, Transform view, System.Func<bool> visible = null)
        {
            if (_damage != null) _damage.Watch(health, view, visible);
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            if (_hitMarker != null) _hitMarker.SetVisible(visible);
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GameInput.Asset != null && GameInput.ToggleDebug.WasPressedThisFrame())
            {
                _debugVisible = !_debugVisible;
            }
#endif

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
            // The gun and its rounds are on the gun plate (GunLadderFx), here as in the rest of the game.
            Weapon pickup = Weapons != null ? Weapons.NearbyPickup : null;
            if (pickup != null && GameInput.Asset != null)
            {
                string key = GameInput.Interact.GetBindingDisplayString(0);
                string button = GameInput.Interact.GetBindingDisplayString(1);
                _promptText.text = key + " / " + button + ":  " +
                                   (Weapons.Current != null ? "swap for " : "pick up ") + pickup.Data.displayName;
            }
            else
            {
                _promptText.text = string.Empty;
            }

            if (Health != null)
            {
                _health.SetActive(true);
                float f = Health.Fraction;
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                bool justHit = Time.unscaledTime - Health.LastHitUnscaledTime < 0.25f;
                _health.Show(f, VisorReadout.SuitLine(f), VisorReadout.SuitTone(f), !justHit || blink);

                // Hits are shown by DamageFeedback; this is only the steady tint while knocked down.
                float alpha = Health.Alive ? 0f : 0.5f;
                if (!Mathf.Approximately(_hurtFlash.color.a, alpha)) _hurtFlash.color = new Color(0.8f, 0.05f, 0.05f, alpha);

                if (_practiceLeft > 0f && !GameSession.Paused) _practiceLeft -= Time.unscaledDeltaTime;
                if (!Health.Alive) _bannerText.text = "KNOCKED DOWN.  Back on your feet in a moment.";
                else if (_practiceLeft > 0f) _bannerText.text = PracticeNote.Words;
                else if (CombatWorld.EnemiesHoldFire) _bannerText.text = "Enemies are holding fire.  H or D-Pad Down starts the fight.";
                else _bannerText.text = string.Empty;
            }
            else
            {
                _health.SetActive(false);
                if (_hurtFlash.color.a > 0f) _hurtFlash.color = new Color(0.8f, 0.05f, 0.05f, 0f);
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
                    "Seed " + GameSession.Seed + GoreReadout() + BudgetReadout() + FinishReadout() + DetailReadout() +
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
            return (finish != null ? "\nPicture finish: " + finish.Status : string.Empty) + "\nSun shadows: " + OrbitalAnte.Look.SunShadows.Status;
        }

        /// <summary>How many enemies and guns are being watched, and how many of them are drawn as their far copies.</summary>
        static string DetailReadout()
        {
            return OrbitalAnte.Look.ModelDetail.Watched > 0 ? "\n" + OrbitalAnte.Look.ModelDetail.Describe() : string.Empty;
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
