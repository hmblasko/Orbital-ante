using OrbitalAnte.Core;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The gun in the player's hands: firing, recoil, reloading, and swapping it for one lying on the ground.
    ///
    /// The player carries one gun at a time, which is the rule for the finished game. Picking up another gun
    /// drops the one in hand.
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

        float _kick;
        bool _magazineOut;
        bool _mirrorClickPlayed;

        /// <summary>The gun in hand, or null when empty-handed.</summary>
        public Weapon Current { get; private set; }
        /// <summary>What the crosshair is resting on, for the debug readout. Null when aiming at nothing hittable.</summary>
        public IHittable AimedAt { get; private set; }
        /// <summary>A dropped gun close enough to pick up, or null.</summary>
        public Weapon NearbyPickup { get; private set; }

        public void Initialize(PlayerConfig config, Camera eye, FpsController controller, PlayerHealth health, Transform dropParent)
        {
            _config = config;
            _eye = eye;
            _controller = controller;
            _health = health;
            _dropParent = dropParent;

            _holder = new GameObject("WeaponHolder").transform;
            _holder.gameObject.layer = Layers.Player;
            _holder.SetParent(eye.transform, false);
        }

        /// <summary>Where dropped guns are parented (the arena's weapon folder).</summary>
        public void SetDropParent(Transform parent) => _dropParent = parent;

        static Vector3 RestPosition(WeaponData data)
        {
            return data.placeholderShape == WeaponShape.Pistol
                ? new Vector3(0.17f, -0.17f, 0.44f)
                : new Vector3(0.19f, -0.21f, 0.34f);
        }

        // ------------------------------------------------------------------ changing guns

        /// <summary>Takes a gun into the player's hands, dropping whatever was held.</summary>
        public void Equip(Weapon weapon)
        {
            if (weapon == null || weapon == Current) return;
            DropCurrent();

            Current = weapon;
            weapon.TakeByPlayer();
            weapon.transform.SetParent(_holder, false);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = Vector3.one;
            _holder.localPosition = RestPosition(weapon.Data);
            _holder.localRotation = Quaternion.identity;
            _magazineOut = false;
            _kick = 0f;
            if (NearbyPickup == weapon) NearbyPickup = null;
        }

        /// <summary>Tosses the held gun forward as a physics object.</summary>
        public void DropCurrent()
        {
            if (Current == null) return;
            Weapon weapon = Current;
            Current = null;

            Vector3 velocity = (_controller != null ? _controller.Velocity : Vector3.zero)
                               + _eye.transform.forward * _config.dropTossSpeed + Vector3.up * 1.5f;
            Vector3 spin = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), Random.Range(-4f, 4f));
            // Let go from a little in front of the face, so it does not start inside the player.
            weapon.transform.position = _eye.transform.position + _eye.transform.forward * 0.5f - _eye.transform.up * 0.15f;
            weapon.Drop(_dropParent, velocity, spin);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_config == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateAim();
            UpdatePickup();

            bool canAct = _health == null || _health.Alive;
            if (canAct && NearbyPickup != null && GameInput.Interact.WasPressedThisFrame())
            {
                Equip(NearbyPickup);
            }

            if (Current != null)
            {
                UpdateFiring(dt, canAct);
                AnimateViewModel(dt);
            }
        }

        void UpdateAim()
        {
            AimedAt = null;
            float range = Current != null ? Current.Data.range : 100f;
            var ray = new Ray(_eye.transform.position, _eye.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                AimedAt = hit.collider.GetComponentInParent<IHittable>();
            }
        }

        void UpdatePickup()
        {
            NearbyPickup = null;
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

            if (GameInput.Reload.WasPressedThisFrame() && state.StartReload(hands))
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
                if (state.StartReload(hands)) OnReloadStarted();
            }

            if (shot.Fired)
            {
                Vector3 direction = Ballistics.SpreadDirection(_eye.transform.forward, shot.SpreadDegrees, _rng);
                ShotResult result = Ballistics.FireFromPlayer(weapon.Data, _eye.transform.position, direction);
                fx.Shot(weapon.Data, weapon.Muzzle.position, result.End);

                WeaponData data = weapon.Data;
                float yaw = ((float)_rng.NextDouble() * 2f - 1f) * data.recoilYaw * shot.RecoilScale;
                if (_controller != null)
                {
                    _controller.AddRecoil(data.recoilPitch * shot.RecoilScale, yaw, data.recoilReturn, data.recoilRecovery);
                }
                _kick = 1f;
            }
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
