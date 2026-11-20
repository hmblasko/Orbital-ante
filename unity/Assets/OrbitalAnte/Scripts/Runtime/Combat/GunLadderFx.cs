using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The gun ladder in the game: counts the player's kills (<see cref="GunLadder"/> holds the rule), hands over
    /// the next gun when a step is climbed, and draws a small readout under the Focus meter plus a banner and a
    /// reward sound when a new gun arrives.
    ///
    /// Lives on the game's root object next to Deep Breath, so it carries on across the arena, the cabin and
    /// outside, and is reset with the run.
    /// </summary>
    public sealed class GunLadderFx : MonoBehaviour
    {
        static GunLadderFx _instance;

        /// <summary>The rule itself, for readouts and tests.</summary>
        public GunLadder Ladder { get; private set; }
        /// <summary>The player's guns, found by itself when null. Tests may set it.</summary>
        public PlayerWeapons Weapons;

        GunLadderConfig _config;
        Canvas _canvas;
        Text _line, _banner;
        int _shownRung = -1, _shownKills = -1, _shownCarried = -1;
        WeaponData _shownHeld, _shownLooked;
        Text _note;
        float _bannerLeft;
        float _nextSearch;
        // A step was climbed but the gun could not be handed over yet (no player found): try again each frame.
        bool _owed;

        /// <summary>
        /// The gun a new run starts with: the ladder's first gun when the ladder is on, otherwise the rifle (as
        /// before the ladder, and in automated tests that build a world with no game root).
        /// </summary>
        public static WeaponData StartingGun()
        {
            if (_instance != null && _instance.Ladder != null)
            {
                WeaponData data = Content.ContentDatabase.Get<WeaponData>(_instance.Ladder.WeaponId);
                if (data != null) return data;
            }
            return WeaponData.Rifle;
        }

        void Awake()
        {
            _config = GunLadderConfig.Load();
            if (_config.rules.enabled)
            {
                Ladder = new GunLadder(_config.rules, id => Content.ContentDatabase.Get<WeaponData>(id) != null);
                var problems = GunLadder.Problems(_config.rules, id => Content.ContentDatabase.Get<WeaponData>(id) != null);
                if (problems.Count > 0) Debug.LogWarning("Gun ladder: " + string.Join(" ", problems));
            }
            BuildHud();
        }

        void OnEnable()
        {
            _instance = this;
            CombatEvents.PlayerHitSomething += OnPlayerHit;
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            if (_instance == this) _instance = null;
        }

        public static void ResetStatics() => _instance = null;

        /// <summary>A new run: back to the first gun. Call before the new world hands the player a gun.</summary>
        public void ResetForNewRun()
        {
            if (Ladder != null) Ladder.Reset();
            Weapons = null;
            _nextSearch = 0f;
            _owed = false;
            _bannerLeft = 0f;
            _shownRung = -1;
            _shownKills = -1;
            _shownCarried = -1;
            _shownHeld = null;
            _shownLooked = null;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            // Only enemies count: shooting bits off your own ship is not a kill.
            if (Ladder == null || !outcome.Enemy || !outcome.Killed) return;
            if (!Ladder.AddKill()) return;
            _nextSearch = 0f;
            _owed = !HandOverNextGun();
        }

        /// <summary>The next step's gun is still there to be given (the ladder is on and not at the top).</summary>
        public bool CanGiveNextGun => Ladder != null && !Ladder.AtTop;

        /// <summary>
        /// Hands over the next step's gun now, without a kill (a supply drop). False when the ladder is off or
        /// already at the top. The kill count for the new step starts from nothing.
        /// </summary>
        public bool GiveNextGunNow()
        {
            if (Ladder == null || !Ladder.SkipStep()) return false;
            _nextSearch = 0f;
            _owed = !HandOverNextGun();
            return true;
        }

        public static GunLadderFx Instance => _instance;

        /// <summary>Gives the player the gun of the step they are on. False if there was no player to give it to.</summary>
        bool HandOverNextGun()
        {
            WeaponData data = Content.ContentDatabase.Get<WeaponData>(Ladder.WeaponId);
            if (data == null) return true;
            PlayerWeapons weapons = FindWeapons();
            if (weapons == null) return false;
            // The ladder's own old gun (the very one it handed out, not just the same kind) is used up; any other
            // gun in hand was picked up by the player, so it is dropped, not lost.
            Weapon given = weapons.TakeNewGun(data, _config.rules.keepOldGuns);
            if (given == null) return false;

            // Bigger steps, bigger fanfare: the four reward tiers follow the ladder up.
            int tier = Mathf.Clamp(Ladder.Rung, 1, 4);
            Audio.AudioDirector.PlayLocal("reward.tier" + tier);
            Audio.AudioDirector.PlayLocal("weapon.bolt_rack", 0.8f);

            string name = GunLadder.ShortName(data.DisplayName);
            _banner.text = Ladder.AtTop ? "LAST GUN: " + name : "NEW GUN: " + name;
            _bannerLeft = Mathf.Max(0.5f, _config.bannerSeconds);
            return true;
        }

        PlayerWeapons FindWeapons()
        {
            // A scene search is not free: while no player is about (menus, between runs) look twice a second at most.
            if (Weapons == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                Weapons = FindAnyObjectByType<PlayerWeapons>();
            }
            return Weapons;
        }

        // ------------------------------------------------------------------ what you see

        void BuildHud()
        {
            _canvas = UiKit.CreateCanvas("GunLadderHUD", transform, 8);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // One short line just above the Focus meter (which sits 112 up), out of the visor gauges' corners.
            _line = UiKit.Label(_canvas.transform, string.Empty, 15, _config.colour, TextAnchor.MiddleCenter);
            UiKit.Pin((RectTransform)_line.transform, new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(520f, 22f));
            UiKit.Halo(_line);

            // The banner: high in the middle, clear of the crosshair, gone again in a couple of seconds.
            _banner = UiKit.Label(_canvas.transform, string.Empty, 34, _config.colour, TextAnchor.MiddleCenter);
            UiKit.SetStyle(_banner, FontStyle.Bold);
            UiKit.Pin((RectTransform)_banner.transform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 50f));
            UiKit.Halo(_banner, true);

            // A warning line for a rare gun, above the ladder line: shown while looking at one or holding one.
            _note = UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Danger, TextAnchor.MiddleCenter);
            UiKit.Pin((RectTransform)_note.transform, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(720f, 22f));
            UiKit.Halo(_note);

            _line.gameObject.SetActive(false);
            _banner.gameObject.SetActive(false);
            _note.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_owed && Ladder != null && !GameSession.Paused) _owed = !HandOverNextGun();
            if (_line == null) return;

            // Shown wherever the on-foot controls are live and the player is holding a gun.
            bool onFoot = GameInput.Gameplay != null && GameInput.Gameplay.enabled;
            PlayerWeapons weapons = Ladder != null && onFoot ? FindWeapons() : null;
            bool show = weapons != null && weapons.isActiveAndEnabled && !GameSession.Paused
                        && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;

            if (_line.gameObject.activeSelf != show) _line.gameObject.SetActive(show);
            // The pause menu is where Switch Gun is rebound: build the line again after it, so the key is current.
            if (GameSession.Paused) _shownCarried = -1;
            if (show)
            {
                // Rebuilt only when a kill or a switch changes it, so a fight makes no garbage here.
                int carried = weapons.Carried.Count;
                if (Ladder.Rung != _shownRung || Ladder.KillsOnRung != _shownKills || carried != _shownCarried)
                {
                    _shownRung = Ladder.Rung;
                    _shownKills = Ladder.KillsOnRung;
                    _shownCarried = carried;
                    _line.text = Describe(Ladder) + Carrying(carried);
                }
            }

            // A rare gun's warning: what it is looking at on the ground first, then what is in hand.
            WeaponData looked = show && weapons.NearbyPickup != null ? weapons.NearbyPickup.Data : null;
            WeaponData held = show && weapons.Current != null ? weapons.Current.Data : null;
            WeaponData warn = looked != null && !string.IsNullOrEmpty(looked.pickupNote) ? looked
                            : held != null && !string.IsNullOrEmpty(held.pickupNote) ? held : null;
            if (_note.gameObject.activeSelf != (warn != null)) _note.gameObject.SetActive(warn != null);
            if (warn != null && (warn != _shownHeld || looked != _shownLooked))
            {
                _shownHeld = warn;
                _shownLooked = looked;
                _note.text = (warn == looked ? "ON THE GROUND: " : "IN HAND: ")
                             + GunLadder.ShortName(warn.DisplayName).ToUpperInvariant() + ". " + warn.pickupNote;
            }

            if (_bannerLeft > 0f) _bannerLeft -= Time.unscaledDeltaTime;
            bool bannerOn = show && _bannerLeft > 0f;
            if (_banner.gameObject.activeSelf != bannerOn) _banner.gameObject.SetActive(bannerOn);
            if (bannerOn) _banner.canvasRenderer.SetAlpha(Mathf.Clamp01(_bannerLeft / 0.5f));
        }

        /// <summary>The carried guns and the key that brings them out, or nothing when none are carried.</summary>
        static string Carrying(int carried)
        {
            if (carried <= 0 || GameInput.SwitchGun == null) return string.Empty;
            string key = GameInput.SwitchGun.GetBindingDisplayString(0);
            string pad = GameInput.SwitchGun.GetBindingDisplayString(1);
            string keys = string.IsNullOrEmpty(pad) ? key : key + " / " + pad;
            return "   +" + carried + (carried == 1 ? " GUN" : " GUNS") + " (" + keys + ": SWITCH)";
        }

        /// <summary>The readout: which step, and how far to the next gun.</summary>
        static string Describe(GunLadder ladder)
        {
            string step = "GUN " + (ladder.Rung + 1) + " OF " + ladder.RungCount;
            if (ladder.AtTop) return step + "   LAST GUN";
            WeaponData next = Content.ContentDatabase.Get<WeaponData>(ladder.NextWeaponId);
            string nextName = next != null ? GunLadder.ShortName(next.DisplayName) : ladder.NextWeaponId;
            int left = ladder.KillsLeft;
            return step + "   " + left + (left == 1 ? " KILL" : " KILLS") + " TO " + nextName.ToUpperInvariant();
        }
    }
}
