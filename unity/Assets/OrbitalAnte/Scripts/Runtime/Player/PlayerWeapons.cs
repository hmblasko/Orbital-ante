using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Melee;
using OrbitalAnte.Weapons;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The gun in the player's hands: firing, recoil, reloading, and swapping it for one lying on the ground.
    ///
    /// One gun is in hand at a time. Guns the gun ladder has climbed past are carried (Matthew, 16 Nov 2026: "you
    /// can still revert back to your old gun, it's part of your inventory") and Switch Gun cycles through them.
    /// Picking up a gun off the ground swaps it for the one in hand, which is dropped; the carried ones stay.
    /// </summary>
    public sealed class PlayerWeapons : MonoBehaviour
    {
        PlayerConfig _config;
        Camera _eye;
        FpsController _controller;
        PlayerHealth _health;
        Transform _holder;
        Transform _dropParent;
        readonly SeededRandom _rng = new SeededRandom(12345);
        // The rare guns' blow-up rolls: one fair roll per shot, deliberately NOT from the run's seed, so restarting
        // a seed cannot teach you which shot blows up (that would be a hidden rule to learn, not a risk to weigh).
        SeededRandom _backfireRng;
        // Guns carried but not in hand, hidden under the holder. The next one to come out is first.
        readonly List<Weapon> _carried = new List<Weapon>();

        float _kick;
        bool _magazineOut;
        bool _mirrorClickPlayed;

        /// <summary>The gun in hand, or null when empty-handed.</summary>
        public Weapon Current { get; private set; }
        /// <summary>When the player last fired (Time.time), for how far off a shot is heard.</summary>
        public float LastShotTime { get; private set; } = -100f;
        /// <summary>
        /// What the crosshair is resting on, for the debug readout. Null when aiming at nothing hittable. Only the F3
        /// debug line reads it, so the ray is cast when it is asked for (at most once a frame), not every frame.
        /// </summary>
        public IHittable AimedAt
        {
            get
            {
                if (_aimedFrame != Time.frameCount)
                {
                    _aimedFrame = Time.frameCount;
                    _aimedAt = FindAimed();
                }
                return _aimedAt;
            }
        }
        IHittable _aimedAt;
        int _aimedFrame = -1;
        /// <summary>A dropped gun close enough to pick up, or null.</summary>
        public Weapon NearbyPickup { get; private set; }
        /// <summary>How good a pick-up <see cref="NearbyPickup"/> is (lower is better: degrees off the view plus ten a metre).</summary>
        public float NearbyPickupScore { get; private set; } = float.MaxValue;
        // Paired by PlayerMelee.Initialize on the player's eye.
        internal PlayerMelee MeleeSlot { get; set; }
        /// <summary>Guns carried but not in hand (the ladder's earlier guns), next out first.</summary>
        public IReadOnlyList<Weapon> Carried => _carried;
        /// <summary>
        /// A rare gun blew up in the player's hands (<see cref="WeaponData.backfireChance"/>). The gun is already
        /// gone; whoever owns the player ends the run. The argument is the cause in plain words.
        /// </summary>
        public event Action<string> Backfired;

        public void Initialize(PlayerConfig config, Camera eye, FpsController controller, PlayerHealth health, Transform dropParent)
        {
            _config = config;
            _eye = eye;
            _controller = controller;
            _health = health;
            _dropParent = dropParent;

            _backfireRng = FreshBackfireRolls();
            _holder = new GameObject("WeaponHolder").transform;
            _holder.gameObject.layer = Layers.Player;
            _holder.SetParent(eye.transform, false);
        }

        /// <summary>
        /// True while the player is inside the ship's hull, where guns take the cabin's even light. Set by the
        /// astronaut; a gun handed over later follows it.
        /// </summary>
        public bool Indoors { get; set; }

        // Subscribed for the component's whole life, not only while enabled: the weapons are switched off while the
        // player is seated or driving, and the gun in hand must still take a look earned meanwhile.
        void Awake() => OrbitalAnte.Items.LockerLook.Changed += WearAgain;
        void OnDestroy() => OrbitalAnte.Items.LockerLook.Changed -= WearAgain;

        /// <summary>
        /// A look went to work in the middle of a run (<c>LockerLook.Refresh</c>, Ralph loop pass 5): the gun in hand
        /// puts its skin on now, so it is seen as it is earned; a carried gun puts its own on as it comes into the hand.
        /// </summary>
        void WearAgain()
        {
            Weapon weapon = Current;
            if (weapon == null) return;
            OrbitalAnte.Look.ModelLibrary.Wear(weapon.gameObject, OrbitalAnte.Items.LockerLook.SkinFor(weapon.Data));
            OrbitalAnte.Look.ModelLibrary.SetIndoors(weapon.gameObject, Indoors);
        }

        /// <summary>
        /// Where the view's kick goes when there is no arena <see cref="FpsController"/> (the astronaut in the real
        /// game). Null means no kick.
        /// </summary>
        public ViewRecoil Recoil { get; set; }

        /// <summary>
        /// True while the shooter floats free with no pull to stand against (the astronaut outside, Posture.Free).
        /// Null or false: shots never push (the arena, the ground, aboard).
        /// </summary>
        public Func<bool> FloatingFree;
        /// <summary>The shooter's speed now, for the drift cap of <see cref="ZeroGKick"/>.</summary>
        public Func<Vector3> ShooterVelocity;
        /// <summary>Adds a push to the shooter's speed (metres per second, world space).</summary>
        public Action<Vector3> PushBack;

        /// <summary>
        /// The gun the run itself gave the player (the starting gun, or the gun ladder's latest), as the object.
        /// Null once it has been swapped for a gun off the ground and destroyed.
        /// </summary>
        public Weapon IssuedGun { get; private set; }

        /// <summary>Takes the run's starting gun into the player's hands and remembers it as the issued gun.</summary>
        public void EquipIssued(Weapon weapon)
        {
            Equip(weapon);
            IssuedGun = weapon;
        }

        /// <summary>Where dropped guns are parented (the arena's weapon folder).</summary>
        public void SetDropParent(Transform parent) => _dropParent = parent;
        /// <summary>The speed the player is carrying, for a gun they let go of (set by the suit; null in the arena).</summary>
        public System.Func<Vector3> CarriedVelocity;
        /// <summary>The player's "up" right now (set by the suit; null in the arena, where it is +Y).</summary>
        public System.Func<Vector3> UpNow;
        /// <summary>True while the player is inside a ship, where a gun let go of is carried instead.</summary>
        public System.Func<bool> InShip;

        static Vector3 RestPosition(WeaponData data)
        {
            return data.placeholderShape == WeaponShape.Pistol
                ? new Vector3(0.17f, -0.17f, 0.44f)
                : new Vector3(0.19f, -0.21f, 0.34f);
        }

        // ------------------------------------------------------------------ changing guns

        /// <summary>Takes a gun into the player's hands, dropping whatever was held. Carried guns stay carried.</summary>
        public void Equip(Weapon weapon)
        {
            if (weapon == null || weapon == Current) return;
            _carried.Remove(weapon);
            DropCurrent();
            PutInHand(weapon);
        }

        void PutInHand(Weapon weapon)
        {
            Current = weapon;
            weapon.gameObject.SetActive(true);
            weapon.TakeByPlayer();
            weapon.transform.SetParent(_holder, false);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = Vector3.one;
            // The Locker's skin for this kind of gun (or the one for every gun), on any gun that comes into the
            // player's hands, picked up or handed over; before the cabin light, whose twin the skin brings with it.
            OrbitalAnte.Look.ModelLibrary.Wear(weapon.gameObject, OrbitalAnte.Items.LockerLook.SkinFor(weapon.Data));
            OrbitalAnte.Look.ModelLibrary.SetIndoors(weapon.gameObject, Indoors);
            _holder.localPosition = RestPosition(weapon.Data);
            _holder.localRotation = Quaternion.identity;
            _magazineOut = false;
            _kick = 0f;
            if (NearbyPickup == weapon) NearbyPickup = null;
        }

        /// <summary>Puts the gun in hand away (hidden, still loaded as it was) to the back of the carried guns.</summary>
        void Stow(Weapon weapon)
        {
            if (weapon == null) return;
            weapon.State.CancelReload();
            weapon.SetMagazineVisible(true);
            weapon.gameObject.SetActive(false);
            _carried.Add(weapon);
        }

        /// <summary>
        /// Puts the gun in hand away and brings out the next carried one. False if nothing is carried. A gun hidden
        /// in the pilot's seat stays hidden.
        /// </summary>
        public bool SwitchGun()
        {
            if (_carried.Count == 0 || _holder == null) return false;
            Weapon next = _carried[0];
            _carried.RemoveAt(0);
            if (next == null) return _carried.Count > 0 && SwitchGun();
            Weapon held = Current;
            bool shown = held == null || held.gameObject.activeSelf;
            Current = null;
            Stow(held);
            PutInHand(next);
            next.gameObject.SetActive(shown);
            return true;
        }

        /// <summary>
        /// Hands the player a brand new gun (the gun ladder's next one), fully loaded. With
        /// <paramref name="keepOld"/> the gun in hand is put away and carried, whatever it is. Without it the very
        /// gun the run issued (<see cref="IssuedGun"/>) is gone and any other gun in hand (one picked up, even of
        /// the same kind) is dropped, as before carrying existed. Returns the new gun, or null if there is nowhere
        /// to hold it yet.
        /// </summary>
        public Weapon TakeNewGun(WeaponData data, bool keepOld = true)
        {
            if (data == null || _holder == null) return null;
            Weapon old = Current;
            bool shown = old == null || old.gameObject.activeSelf;
            if (old != null && keepOld)
            {
                Current = null;
                Stow(old);
            }
            else if (old != null && old != IssuedGun && shown) DropCurrent();
            else
            {
                Current = null;
                if (old != null) Destroy(old.gameObject);
            }

            Weapon weapon = Weapon.Create(data, _dropParent, true);
            PutInHand(weapon);
            IssuedGun = weapon;
            // In the pilot's seat the held gun is put out of sight; a gun handed over there stays out of sight too.
            weapon.gameObject.SetActive(shown);
            return weapon;
        }

        /// <summary>Tosses the held gun forward as a physics object.</summary>
        public void DropCurrent()
        {
            if (Current == null) return;
            Weapon weapon = Current;
            Current = null;
            // In the cabin nothing loose is kept (the player walks there by rule, with no furniture to rest on):
            // a gun let go of aboard is put away and carried instead, and can be dropped outside.
            if (InShip != null && InShip())
            {
                Stow(weapon);
                return;
            }

            // In the solar system the gun leaves with the suit's own speed and is tossed up from the suit's "up",
            // not Unity's +Y; it lies with the world it falls on (CombatWorld.DropParent), not in the player's space.
            Vector3 carried = CarriedVelocity != null ? CarriedVelocity() : _controller != null ? _controller.Velocity : Vector3.zero;
            Vector3 up = UpNow != null ? UpNow() : Vector3.up;
            Vector3 velocity = carried + _eye.transform.forward * _config.dropTossSpeed + up * 1.5f;
            Vector3 spin = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), Random.Range(-4f, 4f));
            // Let go from a little in front of the face, so it does not start inside the player.
            weapon.transform.position = _eye.transform.position + _eye.transform.forward * 0.5f - _eye.transform.up * 0.15f;
            Transform parent = AI.CombatWorld.DropParent != null && CarriedVelocity != null ? AI.CombatWorld.DropParent : _dropParent;
            weapon.Drop(parent, velocity, spin);
            AI.CombatWorld.RaisePlayerDroppedGun(weapon);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_config == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdatePickup();

            bool canAct = _health == null || _health.Alive;
            bool take = canAct && GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame();
            // Compare both slots from this view before equipping anything changes the set of dropped items.
            if (MeleeSlot != null && MeleeSlot.isActiveAndEnabled && MeleeSlot.UpdatePickup(NearbyPickupScore, take))
            {
                NearbyPickup = null;
                NearbyPickupScore = float.MaxValue;
            }
            else if (take && NearbyPickup != null)
            {
                Equip(NearbyPickup);
            }
            if (canAct && GameInput.SwitchGun != null && GameInput.SwitchGun.WasPressedThisFrame() && SwitchGun())
            {
                WeaponFx fx = WeaponFx.Instance;
                if (fx != null && Current != null) fx.Audio.PlayClick(Current.transform.position, 0.9f);
            }

            if (Current != null)
            {
                UpdateFiring(dt, canAct);
                // A rare gun that blew up has just left the hand.
                if (Current != null) AnimateViewModel(dt);
            }
        }

        IHittable FindAimed()
        {
            if (_eye == null) return null;
            float range = Current != null ? Current.Data.range : 100f;
            var ray = new Ray(_eye.transform.position, _eye.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<IHittable>();
            }
            return null;
        }

        void UpdatePickup()
        {
            NearbyPickup = null;
            NearbyPickupScore = float.MaxValue;
            Vector3 eye = _eye.transform.position;
            Vector3 forward = _eye.transform.forward;
            float best = float.MaxValue;

            var all = Weapon.All;
            for (int i = 0; i < all.Count; i++)
            {
                Weapon w = all[i];
                if (w == null || w.Hold != WeaponHold.Dropped) continue;
                // A gun that has only just left someone's hands cannot be snatched straight back.
                if (Time.unscaledTime - w.DroppedAt < 0.35f) continue;

                Vector3 to = w.transform.position - eye;
                float distance = to.magnitude;
                if (distance > _config.pickupRange) continue;

                // Prefer the one being looked at; anything right at the player's feet also counts.
                float angle = distance > 0.01f ? Vector3.Angle(forward, to) : 0f;
                if (angle > 40f && distance > 1.9f) continue;
                float score = angle + distance * 10f;
                if (score < best)
                {
                    best = score;
                    NearbyPickup = w;
                    NearbyPickupScore = score;
                }
            }
        }

        void UpdateFiring(float dt, bool canAct)
        {
            Weapon weapon = Current;
            FirearmState state = weapon.State;
            WeaponFx fx = WeaponFx.GetOrCreate();

            state.Tick(dt);

            if (!canAct) return;

            // The player always has both hands. (Enemies are the ones who lose them.)
            const int hands = 2;

            if (GameInput.Reload.WasPressedThisFrame() && state.StartReload(hands, OrbitalAnte.Combat.RunEffects.ReloadTime))
            {
                OnReloadStarted();
            }

            bool held = GameInput.Fire.IsPressed();
            bool pressed = GameInput.Fire.WasPressedThisFrame();
            FireResult shot = state.TryFire(hands, held, pressed);

            if (shot.DryFire)
            {
                fx.Audio.PlayClick(weapon.Muzzle.position, 1.5f);
                // Pulling the trigger on an empty gun reloads it, so nobody is stuck clicking.
                if (state.StartReload(hands, OrbitalAnte.Combat.RunEffects.ReloadTime)) OnReloadStarted();
            }

            if (shot.Fired && RollsBackfire(weapon.Data))
            {
                BlowUp(weapon);
                return;
            }

            if (shot.Fired)
            {
                LastShotTime = Time.time;
                if (Playtest.PlaytestHooks.Log != null) Playtest.PlaytestHooks.Shot(weapon.Data.Id);
                Vector3 direction = Ballistics.SpreadDirection(_eye.transform.forward, shot.SpreadDegrees, _rng);
                ShotResult result = Ballistics.FireFromPlayer(weapon.Data, _eye.transform.position, direction);
                fx.Shot(weapon.Data, weapon.Muzzle.position, result.End);

                WeaponData data = weapon.Data;
                float yaw = ((float)_rng.NextDouble() * 2f - 1f) * data.recoilYaw * shot.RecoilScale;
                if (_controller != null)
                {
                    _controller.AddRecoil(data.recoilPitch * shot.RecoilScale, yaw, data.recoilReturn, data.recoilRecovery);
                }
                else if (Recoil != null)
                {
                    Recoil.Kick(data.recoilPitch * shot.RecoilScale, yaw, data.recoilReturn, data.recoilRecovery);
                }
                PushShooter(data.recoilPitch, shot.RecoilScale);
                _kick = 1f;
            }
        }

        /// <summary>Floating free, the shot pushes the shooter back along the line of fire (<see cref="ZeroGKick"/>).</summary>
        void PushShooter(float recoilPitch, float recoilScale)
        {
            if (PushBack == null || FloatingFree == null || _config == null || !FloatingFree()) return;
            float push = ZeroGKick.ShotPush(_config.zeroGKick, recoilPitch, recoilScale);
            if (push <= 0f) return;
            Vector3 back = -_eye.transform.forward;
            float along = ShooterVelocity != null ? Vector3.Dot(ShooterVelocity(), back) : 0f;
            float given = ZeroGKick.Applied(_config.zeroGKick, push, along);
            if (given > 0f) PushBack(back * given);
        }

        static SeededRandom FreshBackfireRolls() => new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks ^ (int)(DateTime.UtcNow.Ticks >> 32)));

        bool RollsBackfire(WeaponData data)
        {
            if (data.backfireChance <= 0f) return false;
            if (_backfireRng == null) _backfireRng = FreshBackfireRolls();
            return OrbitalAnte.Combat.RareGuns.Backfires(_backfireRng.NextDouble(), data.backfireChance);
        }

        /// <summary>The gun goes off in the player's hands: a flash and a bang where it was, the gun is gone, and the
        /// owner of the player is told (it ends the run).</summary>
        void BlowUp(Weapon weapon)
        {
            LastShotTime = Time.time;
            Vector3 at = weapon.Muzzle != null ? weapon.Muzzle.position : weapon.transform.position;
            WeaponFx fx = WeaponFx.Instance;
            if (fx != null)
            {
                fx.Shot(weapon.Data, at, at);
                fx.Impact(at, -_eye.transform.forward, (Transform)null);
            }
            Audio.AudioDirector.PlayLocal("impact.metal", 1f, 0.6f);
            Gore.GoreFx gore = Gore.GoreFx.Instance;
            if (gore != null) gore.Burst(at, -_eye.transform.forward, 14, 4f);

            Current = null;
            if (IssuedGun == weapon) IssuedGun = null;
            Destroy(weapon.gameObject);
            Backfired?.Invoke("Your " + OrbitalAnte.Combat.GunLadder.ShortName(weapon.Data.DisplayName) + " blew up in your hands.");
        }

        void OnReloadStarted()
        {
            _magazineOut = false;
            _mirrorClickPlayed = false;
        }

        void AnimateViewModel(float dt)
        {
            Weapon weapon = Current;
            FirearmState state = weapon.State;
            WeaponFx fx = WeaponFx.Instance;

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 7f);
            Vector3 position = RestPosition(weapon.Data) + new Vector3(0f, 0f, -0.045f * _kick);
            Quaternion rotation = Quaternion.Euler(-3.5f * _kick, 0f, 0f);

            if (state.Reloading)
            {
                // Tip the gun over, pull the magazine, push a new one in, bring it back up.
                float p = state.ReloadProgress;
                float dip = Mathf.Sin(p * Mathf.PI);
                position += new Vector3(-0.03f * dip, -0.09f * dip, 0f);
                rotation = Quaternion.Euler(22f * dip, 0f, -28f * dip) * rotation;

                if (!_magazineOut && p >= 0.22f)
                {
                    _magazineOut = true;
                    weapon.SetMagazineVisible(false);
                    if (fx != null) fx.Audio.PlayClick(weapon.Magazine.position, 0.75f);
                }
                if (_magazineOut && !_mirrorClickPlayed && p >= 0.7f)
                {
                    _mirrorClickPlayed = true;
                    weapon.SetMagazineVisible(true);
                    if (fx != null) fx.Audio.PlayClick(weapon.Magazine.position, 1.15f);
                }
            }
            else
            {
                weapon.SetMagazineVisible(true);
            }

            _holder.localPosition = position;
            _holder.localRotation = rotation;
        }
    }
}
