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
    /// the next gun when a step is climbed, and draws the gun plate in the lower right (the gun, its rounds, how
    /// far the next gun is) plus a banner and a reward sound when a new gun arrives.
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
        // The gun plate, lower right.
        RectTransform _plate, _ladderRows;
        Text _gunName, _ammo, _nextLine;
        Image[] _rounds, _pips;
        int _shownAmmo = int.MinValue, _shownMagazine = -1, _shownPipCount = -1;
        bool _shownReloading;
        WeaponData _shownGun;
        bool _gunKnown;

        /// <summary>True while the gun plate is on screen. The Focus meter sits in it then (see <see cref="DeepBreathFx"/>).</summary>
        public static bool PlateShown => _instance != null && _instance._plate != null && _instance._plate.gameObject.activeSelf;

        // Where things sit in the plate, measured from its top left corner. The Focus bar's place is shared with
        // DeepBreathFx, which draws the bar itself.
        public const float PlateWidth = 376f, PlateHeight = 122f;
        const float RightColumn = 188f, RightWidth = 166f;
        /// <summary>The Focus bar in the plate: its left edge, the height of its middle, its width.</summary>
        public const float FocusLeft = 250f, FocusMiddle = 104f, FocusWidth = RightColumn + RightWidth - FocusLeft;
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
            _gunKnown = false;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            // Only enemies count: shooting bits off your own ship is not a kill.
            if (Ladder == null || !outcome.Enemy || !outcome.Killed) return;
            if (!Ladder.AddKill()) return;
            _nextSearch = 0f;
            _owed = !HandOverNextGun();
            if (Ladder.AtTop) ReachedTop?.Invoke();
        }

        /// <summary>
        /// The ladder has just reached its last gun (a goal unlock, Ante/Unlocks.cs). The ante loop listens for the
        /// whole session and only counts it during a solar-system run, so the practice range never unlocks anything.
        /// </summary>
        public static event System.Action ReachedTop;

        /// <summary>The ladder is on and not at the top yet, so kills still count toward a new gun.</summary>
        public bool CanClimb => Ladder != null && !Ladder.AtTop;

        /// <summary>
        /// Counts kills that were not made in a fight (a supply drop's, 20 Nov 2026), as if they had been. Hands
        /// over the new gun if they fill the step; any left over after a climb are not carried on. False when the
        /// ladder is off or at the top.
        /// </summary>
        public bool CreditKills(int kills)
        {
            if (!CanClimb || kills <= 0) return false;
            for (int i = 0; i < kills; i++)
            {
                if (!Ladder.AddKill()) continue;
                _nextSearch = 0f;
                _owed = !HandOverNextGun();
                if (Ladder.AtTop) ReachedTop?.Invoke();
                break;
            }
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
            // Under the Focus meter's canvas (12), which draws its bar on this plate, and over the suit's corner shade (9).
            _canvas = UiKit.CreateCanvas("GunLadderHUD", transform, 10);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // The gun plate, lower right ("Hull stencil", Matthew's pick of 26 Nov 2026): the gun's name, the rounds
            // in the magazine as a big number and as blocks, how far the next gun is, and the Focus meter.
            _plate = HullKit.Plate(_canvas.transform, "GunPlate", new Vector2(1f, 0f), new Vector2(-HullKit.Margin, HullKit.Margin), new Vector2(PlateWidth, PlateHeight));
            _gunName = HullKit.At(HullKit.Mono(_plate, string.Empty, 14, Color.white), 18f, 22f, 165f, 18f);
            _ammo = HullKit.At(HullKit.Stencil(_plate, string.Empty, 62, Color.white), 16f, 74f, 170f, 70f);
            _ladderRows = UiKit.NewRect("Ladder", _plate);
            UiKit.Stretch(_ladderRows);
            _nextLine = HullKit.At(HullKit.Mono(_ladderRows, string.Empty, 14, HullKit.Dim), RightColumn, 60f, RightWidth, 18f);
            _nextLine.supportRichText = true;
            HullKit.At(HullKit.Mono(_plate, "FOCUS", 14, HullKit.Dim), RightColumn, FocusMiddle, 60f, 18f);
            _plate.gameObject.SetActive(false);

            // Above the plate: the guns you carry and the key that brings them out.
            _line = HullKit.Mono(_canvas.transform, string.Empty, 14, HullKit.Paint, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_line.transform, new Vector2(1f, 0f), new Vector2(-HullKit.Margin - 2f, 152f), new Vector2(520f, 20f));
            UiKit.Halo(_line, true);

            // The banner: high in the middle, clear of the crosshair, gone again in a couple of seconds.
            _banner = HullKit.Stencil(_canvas.transform, string.Empty, 36, _config.colour, TextAnchor.MiddleCenter);
            UiKit.Pin((RectTransform)_banner.transform, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(900f, 50f));   // under the ante's note line (-146)
            UiKit.Halo(_banner, true);

            // A warning line for a rare gun, above the ladder line: shown while looking at one or holding one.
            _note = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Danger, TextAnchor.MiddleCenter));
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

            // Shown wherever the on-foot controls are live and the player has hands for a gun.
            bool onFoot = GameInput.Gameplay != null && GameInput.Gameplay.enabled;
            PlayerWeapons weapons = onFoot ? FindWeapons() : null;
            bool show = weapons != null && weapons.isActiveAndEnabled && !GameSession.Paused
                        && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;

            if (_plate.gameObject.activeSelf != show) _plate.gameObject.SetActive(show);
            // The pause menu is where Switch Gun is rebound: build the line again after it, so the key is current.
            if (GameSession.Paused) _shownCarried = -1;
            if (show)
            {
                DrawGun(weapons.Current);
                DrawLadder();
                // Rebuilt only when a switch changes it, so a fight makes no garbage here.
                int carried = weapons.Carried.Count;
                if (carried != _shownCarried)
                {
                    _shownCarried = carried;
                    _line.text = Carrying(carried);
                }
            }
            bool lineOn = show && _shownCarried > 0;
            if (_line.gameObject.activeSelf != lineOn) _line.gameObject.SetActive(lineOn);

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

        /// <summary>The gun's name, the rounds left as a number, and the magazine as blocks.</summary>
        void DrawGun(Weapon gun)
        {
            WeaponData data = gun != null ? gun.Data : null;
            int magazine = gun != null ? gun.State.Spec.MagazineSize : 0;
            int ammo = gun != null ? gun.State.Ammo : -1;
            bool reloading = gun != null && gun.State.Reloading;

            if (!_gunKnown || data != _shownGun || reloading != _shownReloading)
            {
                _shownGun = data;
                _shownReloading = reloading;
                _gunName.text = data == null ? "NO GUN" : reloading ? "RELOADING" : GunLadder.ShortName(data.DisplayName).ToUpperInvariant();
            }
            if (!_gunKnown || magazine != _shownMagazine)
            {
                _shownMagazine = magazine;
                _shownAmmo = int.MinValue;
                // A magazine of a hundred or more gets smaller digits, so they stay clear of the blocks.
                _ammo.fontSize = magazine > 99 ? 42 : 62;
                // One block a round for a small magazine, ten for a large one.
                if (_rounds != null) for (int i = 0; i < _rounds.Length; i++) Destroy(_rounds[i].gameObject);
                int count = HullReadout.RoundBlockCount(magazine);
                const float gap = 4f;
                _rounds = HullKit.Blocks(_plate, "Round", RightColumn, 16f, count, (RightWidth - gap * (count - 1)) / count, 28f, gap);
            }
            if (ammo != _shownAmmo)
            {
                _shownAmmo = ammo;
                _ammo.text = gun == null ? "--"
                    : HullReadout.TwoDigits(ammo) + "<size=26> /" + HullReadout.TwoDigits(magazine) + "</size>";
            }
            _gunKnown = true;

            // Orange when the magazine is nearly empty, and while a new one goes in.
            bool low = gun != null && HullReadout.MagazineLow(ammo, magazine);
            Color colour = low || reloading ? HullKit.Orange : HullKit.Paint;
            HullKit.Tint(_ammo, gun == null ? HullKit.Dim : colour);
            HullKit.Tint(_gunName, reloading ? HullKit.Orange : HullKit.Dim);
            HullKit.Light(_rounds, gun == null ? 0 : HullReadout.RoundBlocksLit(ammo, magazine), colour);
        }

        /// <summary>The next gun and the kills it takes, as pips. Nothing when the ladder is switched off.</summary>
        void DrawLadder()
        {
            bool on = Ladder != null;
            if (_ladderRows.gameObject.activeSelf != on) _ladderRows.gameObject.SetActive(on);
            if (!on) return;
            if (Ladder.Rung != _shownRung || Ladder.KillsOnRung != _shownKills)
            {
                _shownRung = Ladder.Rung;
                _shownKills = Ladder.KillsOnRung;
                _nextLine.text = Describe(Ladder);
                int count = Ladder.AtTop ? 0 : HullReadout.PipCount(Ladder.KillsNeeded);
                if (count != _shownPipCount)
                {
                    _shownPipCount = count;
                    if (_pips != null) for (int i = 0; i < _pips.Length; i++) Destroy(_pips[i].gameObject);
                    const float gap = 4f;
                    _pips = count > 0
                        ? HullKit.Blocks(_ladderRows, "Kill", RightColumn, 74f, count, (RightWidth - gap * (count - 1)) / count, 10f, gap)
                        : new Image[0];
                }
            }
            if (_pips != null) HullKit.Light(_pips, Ladder.AtTop ? 0 : HullReadout.PipsLit(Ladder.KillsOnRung, Ladder.KillsNeeded), HullKit.Paint);
        }

        /// <summary>The carried guns and the key that brings them out, or nothing when none are carried.</summary>
        static string Carrying(int carried)
        {
            if (carried <= 0 || GameInput.SwitchGun == null) return string.Empty;
            string key = GameInput.SwitchGun.GetBindingDisplayString(0);
            string pad = GameInput.SwitchGun.GetBindingDisplayString(1);
            string keys = string.IsNullOrEmpty(pad) ? key : key + " / " + pad;
            return "+" + carried + (carried == 1 ? " GUN" : " GUNS") + "  " + keys.ToUpperInvariant() + ": SWITCH";
        }

        /// <summary>The line over the pips: the next gun's name, or that this is the last one.</summary>
        static string Describe(GunLadder ladder)
        {
            if (ladder.AtTop) return "LAST GUN";
            WeaponData next = Content.ContentDatabase.Get<WeaponData>(ladder.NextWeaponId);
            string nextName = next != null ? GunLadder.ShortName(next.DisplayName) : ladder.NextWeaponId;
            return "NEXT <color=#EFE6D2>" + nextName.ToUpperInvariant() + "</color>";
        }
    }
}
