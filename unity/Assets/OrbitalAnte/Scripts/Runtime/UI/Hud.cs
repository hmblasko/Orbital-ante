using OrbitalAnte.AI;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The HUD shared by both worlds: the gun in the lower right (name, the gun ladder's step, the magazine), and the
    /// performance overlay. In the combat arena it also draws the crosshair, health, the pick-up prompt and the
    /// arena's banner.
    ///
    /// Clean-up of 17 Nov 2026 (Matthew: "a bunch of stats on the screen and white text"): the performance overlay
    /// is off unless asked for (F3, or Run Stats in the pause menu, remembered in
    /// <see cref="FeelSettings.PerformanceOverlay"/>), sits on a dark plate in small muted lettering, and every
    /// number it held is also in Run Stats. The arena's list of keys fades after <see cref="HudRules.HelpSeconds"/>.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        Canvas _canvas;
        Text _frameText;
        Text _aimText;
        Text _helpText;
        RectTransform _framePlate;
        bool _debugVisible;
        float _helpShown;

        float _frameAccumulator;
        int _frameCount;
        float _worstFrame;
        float _nextFrameReport;

        /// <summary>Set when a run starts so the HUD can show the gun, ammo and what the crosshair is on.</summary>
        public PlayerWeapons Weapons;
        public PlayerHealth Health;

        Text _ammoText;
        Text _magText;
        Text _weaponText;
        Text _carriedText;
        Text _ladderText;
        readonly Image[] _pips = new Image[HudRules.MaxPips];
        RectTransform _weaponGroup;
        int _shownAmmo = -2, _shownMag = -1, _shownCarried = -1, _shownRung = -1, _shownKills = -1, _shownPips = -1;
        WeaponData _shownWeapon;
        Weapon _shownPickup;
        bool _shownSwap;
        bool _shownReloading;
        Text _promptText;
        Text _bannerText;
        VisorGauge _health;
        Image _hurtFlash;
        DamageFeedback _damage;
        HitMarker _hitMarker;

        /// <summary>Extra line for the debug readout, supplied by whichever test world is running.</summary>
        public System.Func<string> ExtraDebug;

        /// <summary>Everything that only makes sense in the combat arena (crosshair, health, banner) lives under this.</summary>
        RectTransform _arenaOnly;

        /// <summary>
        /// When the gun in the lower right is shown. Null shows it whenever there is a <see cref="Weapons"/> (the
        /// arena); the solar game hides it in the seat, on the ship's screen and in the rover.
        /// </summary>
        public System.Func<bool> WeaponShown;

        /// <summary>Shows or hides the combat parts, and starts the list of keys again for a new run.</summary>
        public void SetArenaMode(bool arena)
        {
            if (_arenaOnly != null) _arenaOnly.gameObject.SetActive(arena);
            _helpShown = 0f;
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

            // Crosshair: a small dot with a dark outline so it reads against any background.
            var outline = UiKit.NewRect("CrosshairOutline", _arenaOnly);
            UiKit.Pin(outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            UiKit.AddImage(outline, new Color(0f, 0f, 0f, 0.7f), UiKit.Circle);
            var dot = UiKit.NewRect("CrosshairDot", _arenaOnly);
            UiKit.Pin(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            UiKit.AddImage(dot, UiKit.Cream, UiKit.Circle);

            _aimText = UiKit.Label(_arenaOnly, string.Empty, 20, UiKit.Cream);
            UiKit.Pin((RectTransform)_aimText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(500f, 30f));
            AddShadow(_aimText);

            // The performance overlay: small muted words on a dark plate, so it reads as an instrument and not as
            // the game talking. Sized to its words each time they change.
            _framePlate = UiKit.NewRect("PerformancePlate", _canvas.transform);
            UiKit.Pin(_framePlate, new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(620f, 40f));
            UiKit.AddImage(_framePlate, new Color(0.06f, 0.05f, 0.05f, 0.62f), UiKit.Rounded);
            _frameText = UiKit.Label(_framePlate, string.Empty, 13, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            var frameRect = (RectTransform)_frameText.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = new Vector2(12f, 6f);
            frameRect.offsetMax = new Vector2(-12f, -7f);
            _frameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _frameText.lineSpacing = 1.1f;
            _framePlate.gameObject.SetActive(false);

            // The arena's keys: up for the first seconds of a run, then faded out. All of them are in the pause
            // menu's Controls page.
            _helpText = UiKit.Label(_arenaOnly, string.Empty, 15, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_helpText.transform, new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(1100f, 24f));
            AddShadow(_helpText);
            _helpText.text = "Respawn dummies: T     Switch gun: 1 or D-Pad Up     Enemies hold fire: H or D-Pad Down     Pause: Esc or Start";

            // Full-screen red flash when the player is hit. Drawn first so everything else sits on top of it.
            var flash = UiKit.NewRect("HurtFlash", _arenaOnly);
            UiKit.Stretch(flash);
            flash.SetAsFirstSibling();
            _hurtFlash = UiKit.AddImage(flash, new Color(0.8f, 0.05f, 0.05f, 0f));

            BuildWeaponGroup();

            // Health, lower right: the same arc of suit plates as outside the ship (see VisorGauge).
            _health = VisorGauge.Build(_arenaOnly, false, "SUIT", UiKit.Cream, 380f, 110f);

            // "Pick up" prompt under the crosshair, and a banner at the top for arena state.
            _promptText = UiKit.Label(_arenaOnly, string.Empty, 22, UiKit.Cream);
            UiKit.Pin((RectTransform)_promptText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(760f, 30f));
            AddShadow(_promptText);
            _bannerText = UiKit.Label(_arenaOnly, string.Empty, 24, UiKit.PanelEdge);
            UiKit.Pin((RectTransform)_bannerText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 34f));
            AddShadow(_bannerText);
        }

        /// <summary>
        /// The gun, lower right, read from the bottom up: the rounds in the magazine (big) over the magazine's size
        /// (small), the ladder's step as pips with the next gun's name, and the gun's name with how many more guns
        /// are carried. Out past the suit plates, which sit 380 in from the right edge.
        /// </summary>
        void BuildWeaponGroup()
        {
            _weaponGroup = UiKit.NewRect("Weapon", _canvas.transform);
            UiKit.Pin(_weaponGroup, new Vector2(1f, 0f), new Vector2(-30f, 22f), new Vector2(260f, 140f));

            _ammoText = UiKit.Label(_weaponGroup, string.Empty, 46, UiKit.Cream, TextAnchor.LowerRight);
            UiKit.Pin((RectTransform)_ammoText.transform, new Vector2(1f, 0f), new Vector2(0f, -6f), new Vector2(150f, 58f));
            UiKit.Halo(_ammoText, true);
            _magText = UiKit.Label(_weaponGroup, string.Empty, 20, UiKit.Muted, TextAnchor.LowerRight);
            UiKit.Pin((RectTransform)_magText.transform, new Vector2(1f, 0f), new Vector2(-62f, 4f), new Vector2(160f, 26f));
            AddShadow(_magText);

            // The ladder: one pip a kill this step needs, lit as they are made, then the next gun's name.
            _ladderText = UiKit.Label(_weaponGroup, string.Empty, 12, UiKit.Muted, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_ladderText.transform, new Vector2(1f, 0f), new Vector2(0f, 62f), new Vector2(240f, 16f));
            AddShadow(_ladderText);
            for (int i = 0; i < _pips.Length; i++)
            {
                RectTransform pip = UiKit.NewRect("Pip" + i, _weaponGroup);
                UiKit.Pin(pip, new Vector2(1f, 0f), new Vector2(0f, 66f), new Vector2(8f, 8f));
                _pips[i] = UiKit.AddImage(pip, Color.white, UiKit.Rounded);
                _pips[i].type = Image.Type.Simple;
                pip.gameObject.SetActive(false);
            }

            _weaponText = UiKit.Label(_weaponGroup, string.Empty, 18, UiKit.Cream, TextAnchor.LowerRight);
            UiKit.Pin((RectTransform)_weaponText.transform, new Vector2(1f, 0f), new Vector2(0f, 82f), new Vector2(200f, 24f));
            // A long name grows to the left on one line, never up into the melee bar.
            _weaponText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(_weaponText);
            _carriedText = UiKit.Label(_weaponGroup, string.Empty, 13, UiKit.Muted, TextAnchor.LowerRight);
            // Its own line above the gun's name, so a long key name never reaches the suit plates.
            UiKit.Pin((RectTransform)_carriedText.transform, new Vector2(1f, 0f), new Vector2(0f, 106f), new Vector2(240f, 18f));
            _carriedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(_carriedText);
            _weaponGroup.gameObject.SetActive(false);
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

            if (GameInput.Asset != null && GameInput.ToggleDebug.WasPressedThisFrame())
            {
                FeelSettings.PerformanceOverlay = !FeelSettings.PerformanceOverlay;
            }
            bool overlay = FeelSettings.PerformanceOverlay;
            if (overlay != _debugVisible)
            {
                _debugVisible = overlay;
                _framePlate.gameObject.SetActive(overlay);
                // Shown at once, not half a second later.
                _nextFrameReport = 0f;
            }
            _aimText.enabled = _debugVisible;

            if (!GameSession.Paused) _helpShown += Time.unscaledDeltaTime;
            float help = HudRules.HelpAlpha(_helpShown);
            _helpText.enabled = help > 0f;
            if (help > 0f) _helpText.canvasRenderer.SetAlpha(help);

            UpdateFrameTime();
            UpdateCombatReadouts();

            // Both of these sit under the arena's own parts: in the solar game they are hidden, so build no words.
            if (_debugVisible && _arenaOnly.gameObject.activeSelf)
            {
                IHittable aimed = Weapons != null ? Weapons.AimedAt : null;
                // A destroyed target compares equal to null in Unity even though the interface reference is not.
                var asObject = aimed as Object;
                _aimText.text = aimed != null && asObject != null ? aimed.DescribeForDebug() : string.Empty;
            }
        }

        void UpdateCombatReadouts()
        {
            UpdateWeaponGroup();
            Weapon weapon = Weapons != null ? Weapons.Current : null;

            Weapon pickup = Weapons != null && _arenaOnly.gameObject.activeSelf ? Weapons.NearbyPickup : null;
            bool swap = weapon != null;
            if (pickup != null && GameInput.Asset != null)
            {
                // Built again only when the gun on the ground or the verb changes (or after a pause, where keys are rebound).
                if (pickup != _shownPickup || swap != _shownSwap || GameSession.Paused)
                {
                    _shownPickup = pickup;
                    _shownSwap = swap;
                    string key = GameInput.Interact.GetBindingDisplayString(0);
                    string button = GameInput.Interact.GetBindingDisplayString(1);
                    _promptText.text = key + " / " + button + ":  " + (swap ? "swap for " : "pick up ") + pickup.Data.displayName;
                }
            }
            else if (_shownPickup != null || _promptText.text.Length > 0)
            {
                _shownPickup = null;
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

                if (!Health.Alive) _bannerText.text = "KNOCKED DOWN.  Back on your feet in a moment.";
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

        /// <summary>The gun in the lower right. Words are rebuilt only when what they say changes.</summary>
        void UpdateWeaponGroup()
        {
            bool show = Weapons != null && Weapons.isActiveAndEnabled && (WeaponShown == null || WeaponShown());
            if (_weaponGroup.gameObject.activeSelf != show) _weaponGroup.gameObject.SetActive(show);
            if (!show) return;

            Weapon weapon = Weapons.Current;
            WeaponData data = weapon != null ? weapon.Data : null;
            if (data != _shownWeapon)
            {
                _shownWeapon = data;
                _weaponText.text = data != null ? GunLadder.ShortName(data.DisplayName).ToUpperInvariant() : "EMPTY HANDS";
                _shownAmmo = -2;
            }
            int carried = Weapons.Carried.Count;
            if (carried != _shownCarried || _shownAmmo == -2 || GameSession.Paused)
            {
                _shownCarried = carried;
                // How many more guns, and the key that brings them out (looked up again after a pause, where it is rebound).
                string keys = carried > 0 ? OrbitalAnte.Combat.GunLadderFx.SwitchKeys() : string.Empty;
                _carriedText.text = carried <= 0 ? string.Empty
                    : "+" + carried + (carried == 1 ? " gun" : " guns") + (keys.Length > 0 ? "  [" + keys + "]" : string.Empty);
            }

            if (weapon != null)
            {
                FirearmState state = weapon.State;
                bool reloading = state.Reloading;
                int ammo = reloading ? -1 : state.Ammo;
                int mag = state.Spec.MagazineSize;
                if (ammo != _shownAmmo || mag != _shownMag || reloading != _shownReloading)
                {
                    _shownAmmo = ammo;
                    _shownMag = mag;
                    _shownReloading = reloading;
                    // Reloading is said in the small words, so the big number never changes size (a new size
                    // would grow the font's glyph sheet mid-fight).
                    _ammoText.text = reloading ? string.Empty : ammo.ToString();
                    _ammoText.color = ammo == 0 ? UiKit.PanelEdge : UiKit.Cream;
                    _magText.text = reloading ? "RELOADING" : "/ " + mag;
                    _magText.color = reloading ? UiKit.PanelEdge : UiKit.Muted;
                    // Just left of the big number, however many digits it has.
                    ((RectTransform)_magText.transform).anchoredPosition =
                        new Vector2(reloading ? 0f : -_ammoText.preferredWidth - 8f, 4f);
                }
            }
            else if (_shownAmmo != -3)
            {
                _shownAmmo = -3;
                _ammoText.text = string.Empty;
                _magText.text = string.Empty;
            }

            UpdateLadder();
        }

        void UpdateLadder()
        {
            GunLadder ladder = OrbitalAnte.Combat.GunLadderFx.Active;
            if (ladder == null)
            {
                if (_shownRung != -2)
                {
                    _shownRung = -2;
                    _ladderText.text = string.Empty;
                    SetPips(0, 0);
                }
                return;
            }
            if (ladder.Rung == _shownRung && ladder.KillsOnRung == _shownKills) return;
            _shownRung = ladder.Rung;
            _shownKills = ladder.KillsOnRung;
            string next = null;
            if (!ladder.AtTop)
            {
                WeaponData nextGun = Content.ContentDatabase.Get<WeaponData>(ladder.NextWeaponId);
                next = nextGun != null ? GunLadder.ShortName(nextGun.DisplayName) : ladder.NextWeaponId;
            }
            int needed = ladder.KillsNeeded;
            string words = HudRules.LadderWords(ladder.AtTop, next, ladder.KillsLeft, needed);
            _ladderText.text = ladder.AtTop || !HudRules.LadderAsPips(needed) ? words : "\u203A  " + words;
            bool pips = !ladder.AtTop && HudRules.LadderAsPips(needed);
            SetPips(pips ? needed : 0, ladder.KillsOnRung);
        }

        // An unlit pip: a faint cream square, visible on the dark visor shade and on a bright sky alike.
        static readonly Color PipEmpty = new Color(0.96f, 0.91f, 0.80f, 0.28f);

        /// <summary>Lays out <paramref name="count"/> pips to the left of the ladder's words, the first <paramref name="lit"/> lit.</summary>
        void SetPips(int count, int lit)
        {
            _shownPips = count;
            float right = -_ladderText.preferredWidth - 10f;
            for (int i = 0; i < _pips.Length; i++)
            {
                bool on = i < count;
                if (_pips[i].gameObject.activeSelf != on) _pips[i].gameObject.SetActive(on);
                if (!on) continue;
                // Read left to right: the first kill is the leftmost pip.
                ((RectTransform)_pips[i].transform).anchoredPosition = new Vector2(right - (count - 1 - i) * 11f, 66f);
                _pips[i].color = i < lit ? UiKit.Cream : PipEmpty;
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

            if (_frameCount > 0)
            {
                float average = _frameAccumulator / _frameCount;
                LastAverage = average;
                LastWorst = _worstFrame;
                if (_debugVisible)
                {
                    string text = DebugText();
                    _frameText.text = text;
                    int lines = 1;
                    for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
                    _framePlate.sizeDelta = new Vector2(Mathf.Clamp(_frameText.preferredWidth + 26f, 200f, 1240f), lines * 17.5f + 14f);
                }
            }
            _frameAccumulator = 0f;
            _frameCount = 0;
            _worstFrame = 0f;
        }

        /// <summary>The average and worst frame of the last half second of play, in seconds.</summary>
        public static float LastAverage { get; private set; }
        public static float LastWorst { get; private set; }

        /// <summary>
        /// The whole technical readout: the performance overlay's words, and the pause menu's Technical details.
        /// Built only when asked for.
        /// </summary>
        public string DebugText()
        {
            float average = LastAverage;
            return (average * 1000f).ToString("0.0") + " ms avg   " +
                   (LastWorst * 1000f).ToString("0.0") + " ms worst   " +
                   Mathf.RoundToInt(1f / Mathf.Max(0.0001f, average)) + " fps\n" +
                   "Seed " + GameSession.Seed + GoreReadout() + BudgetReadout() + FinishReadout() + DetailReadout() +
                   (ExtraDebug != null ? "\n" + ExtraDebug() : string.Empty);
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
