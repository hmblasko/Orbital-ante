using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// How a character fights, decided only by what it has left:
    /// - Holding a gun with two hands: shoots normally.
    /// - Holding a gun with one hand: shoots with a big spread and recoil penalty, reloads slowly (or not at all).
    /// - Loses the gun hand or forearm, or has the gun shot: the gun drops as a physics object.
    /// - Unarmed with any hand left: goes to fetch the nearest dropped gun and picks it up.
    /// - No hands: cannot shoot. Still moves, and heads for cover.
    ///
    /// It also poses the gun and bends the arms to hold it.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class MannequinCombat : MonoBehaviour, IWeaponHolder
    {
        const float PalmLength = 0.07f;
        const float AimConeDegrees = 55f;

        Mannequin _owner;
        MannequinLocomotion _loco;
        MannequinRig _rig;
        MannequinConfig _cfg;
        SeededRandom _rng = new SeededRandom(7);

        int _gunSide = MannequinRig.Right;
        float _thinkTimer;
        bool _seesPlayer;
        readonly SightReaction _reaction = new SightReaction();
        SquadCoordinator _squad;
        int _squadId = -1;
        Vector3 _seenTarget;
        float _seenAt = float.NegativeInfinity;
        bool _triggerHeld;
        bool _triggerPressed;
        float _triggerUntil;
        float _nextTriggerAt;
        Vector3 _aimDirection = Vector3.forward;
        bool _aimedAtTarget;
        float _kick;
        bool _magazineOut;

        /// <summary>The gun in hand, or null.</summary>
        public Weapon Weapon { get; private set; }
        /// <summary>Which arm holds the gun: MannequinRig.Left or MannequinRig.Right.</summary>
        public int GunSide => _gunSide;
        /// <summary>Usable hands right now: 0, 1 or 2.</summary>
        public int Hands => _owner != null && _owner.Model != null ? _owner.Model.UsableHands : 0;
        /// <summary>True when this character is able to shoot: it has a gun and at least one hand.</summary>
        public bool CanFire => Weapon != null && Hands > 0 && _owner.Alive;
        /// <summary>Shots fired so far (for tests and tuning).</summary>
        public int ShotsFired { get; private set; }
        /// <summary>Arena members denied by the configured capacity hold safely instead of bypassing the squad.</summary>
        public bool RequireSquad;
        public Vector3 ObservationOrigin => HeadPosition;
        public float EngageRange => _cfg.engageRange;
        public float TacticalBodyHeight => _owner.State == MovementState.Intact || _owner.State == MovementState.OneLeg ? 1.8f : 0.6f;
        public SquadMemberState TacticalState
        {
            get
            {
                bool alive = _owner != null && _owner.Alive && isActiveAndEnabled;
                MovementState state = alive ? _owner.State : MovementState.Dead;
                bool armed = alive && CanFire && state != MovementState.HeadOnly;
                Vector3 p = transform.position;
                return new SquadMemberState { Alive = alive, Armed = armed,
                    ReadyToFire = armed && Weapon.State.Ammo > 0 && !Weapon.State.Reloading && _seesPlayer
                        && _reaction.Ready(Time.time) && _loco.FacePoint.HasValue && !_loco.Destination.HasValue,
                    CanMove = alive && state != MovementState.HeadOnly,
                    CanFlank = armed && state == MovementState.Intact,
                    Position = new Vec3d(p.x, p.y, p.z) };
            }
        }
        public void JoinSquad(SquadCoordinator squad, int id) { _squad = squad; _squadId = id; }
        public void ObserveTarget(Vector3 point, float now)
        {
            _reaction.ConfirmVisible(now, _cfg.reactionTime);
            _seenTarget = point; _seenAt = now; _seesPlayer = true;
        }
        public void LoseTarget() { _seesPlayer = false; _reaction.ConfirmHidden(); }

        public void Initialize(Mannequin owner, MannequinLocomotion locomotion)
        {
            _owner = owner;
            _loco = locomotion;
            _rig = owner.Rig;
            _cfg = owner.Config;
            _aimDirection = transform.forward;
            _owner.PartDetached += OnPartDetached;
            _owner.Died += OnDied;
        }

        public void SetSeed(int seed)
        {
            _rng = new SeededRandom(seed);
            _thinkTimer = (float)_rng.NextDouble() * 0.3f;
        }

        void OnDestroy()
        {
            if (_squad != null) _squad.Remove(_squadId, this);
            if (_owner != null)
            {
                _owner.PartDetached -= OnPartDetached;
                _owner.Died -= OnDied;
            }
        }
        void OnDisable()
        {
            if (_squad != null) _squad.Remove(_squadId, this, true);
            _triggerHeld = false;
            if (_loco != null) ClearGoals();
        }
        void OnEnable()
        {
            if (_squad != null && _loco != null && !_squad.Register(this, _loco, _squadId))
            {
                // A replacement may now own that ID. A stale affiliation must never borrow its lease.
                JoinSquad(null, -1); RequireSquad = true; _triggerHeld = false; ClearGoals();
            }
        }

        float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        float Rand(Vector2 range) => Rand(range.x, Mathf.Max(range.x, range.y));

        // ------------------------------------------------------------------ picking up and dropping

        /// <summary>Takes a gun that is lying in the world. Needs at least one usable hand.</summary>
        public bool TryPickUp(Weapon weapon)
        {
            if (weapon == null || Weapon != null || weapon.Hold != WeaponHold.Dropped) return false;
            if (_owner == null || !_owner.Alive || Hands == 0) return false;

            // Right hand if it works, otherwise whatever hand is left.
            _gunSide = _owner.Model.HandUsable(false) ? MannequinRig.Right : MannequinRig.Left;
            Weapon = weapon;
            if (_squad != null) _squad.ClearRecoveryDestination(_squadId);
            weapon.TakeByCharacter(this);
            weapon.transform.SetParent(transform, true);
            weapon.transform.localScale = Vector3.one;
            weapon.State.HandsChanged(Hands);
            _magazineOut = false;
            _aimedAtTarget = false;
            _seesPlayer = false;
            _reaction.ConfirmHidden();
            if (_loco != null) _loco.SuppressIdleWander = true;
            _aimDirection = LowReadyDirection();
            PoseWeapon(0f, true);
            return true;
        }

        /// <summary>Lets go of the gun. It leaves at the character's own speed plus the given extra.</summary>
        public void DropWeapon(Vector3 extraVelocity, float spin)
        {
            if (Weapon == null) return;
            Weapon weapon = Weapon;
            Weapon = null;
            _triggerHeld = false;
            _aimedAtTarget = false;
            if (_squad != null) _squad.RefreshMember(_squadId);

            Transform parent = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            Vector3 velocity = (_owner != null ? _owner.Velocity : Vector3.zero) + extraVelocity;
            weapon.Drop(parent, velocity, Random.onUnitSphere * spin);
        }

        /// <summary>The gun itself was shot: it is knocked out of the hands along the bullet.</summary>
        public void WeaponShotOutOfHands(Weapon weapon, in HitInfo hit)
        {
            if (weapon != Weapon) return;
            Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : -transform.forward;
            DropWeapon(dir * _cfg.disarmKnockSpeed + Vector3.up * (_cfg.disarmKnockSpeed * 0.55f), 14f);
        }

        void OnPartDetached(Mannequin mannequin, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (Weapon == null) return;

            // Losing the gun hand, its forearm or anything above them means the gun falls.
            if (!_owner.Model.HandUsable(_gunSide == MannequinRig.Left))
            {
                Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Vector3.zero;
                DropWeapon(dir * 2f + Vector3.up * 2f, 7f);
                return;
            }

            // Losing the other hand keeps the gun, but everything gets harder (and a reload may be abandoned).
            Weapon.State.HandsChanged(Hands);
            if (_squad != null) _squad.RefreshMember(_squadId);
        }

        void OnDied(Mannequin mannequin)
        {
            if (_squad != null) _squad.Remove(_squadId, this);
            DropWeapon(Vector3.up * 1.5f, 5f);
            if (_loco != null)
            {
                _loco.Destination = null;
                _loco.FacePoint = null;
            }
        }

        // ------------------------------------------------------------------ thinking

        void Update()
        {
            if (_owner == null || !_owner.Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Weapon != null) TickReload(dt);
            if (_squad != null) _squad.RefreshMember(_squadId);
            if (_squad != null) _seesPlayer = _squad.Sees(_squadId);

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Rand(0.12f, 0.22f);
                Think();
            }

            if (_squad != null && _owner.State != MovementState.HeadOnly) _squad.Drive(_squadId);
            if (Weapon != null) UpdateShooting();
        }

        Vector3 HeadPosition
        {
            get
            {
                BodyPart head = _owner.Part(BodyPartId.Head);
                return head != null ? head.transform.position : transform.position + Vector3.up * 1.6f;
            }
        }

        void Think()
        {
            if (RequireSquad && _squad == null) { ClearGoals(); return; }
            MovementState state = _owner.State;
            if (state == MovementState.HeadOnly)
            {
                // A head on its own has nothing to fight with. It just rolls about.
                ClearGoals();
                return;
            }

            if (Weapon != null) ThinkArmed(state);
            else ThinkUnarmed();
        }

        void ClearGoals()
        {
            _loco.Destination = null;
            _loco.FacePoint = null;
            _loco.Hurry = false;
            LoseTarget();
        }

        void ThinkArmed(MovementState state)
        {
            if (!CombatWorld.PlayerPresent || CombatWorld.EnemiesHoldFire)
            {
                ClearGoals();
                return;
            }

            if (_squad != null) { _squad.ClearRecoveryDestination(_squadId); return; }
            // A standalone mannequin (weapon fixtures / target practice) only observes and holds.
            // The arena always registers enemies with the bounded squad adapter.
            Vector3 target = CombatWorld.PlayerEye.position + Vector3.down * 0.3f;
            bool sees = (target - HeadPosition).sqrMagnitude <= _cfg.engageRange * _cfg.engageRange
                && !Physics.Linecast(HeadPosition, target, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (!sees) { ClearGoals(); return; }
            ObserveTarget(target, Time.time);
            _loco.Destination = null; _loco.FacePoint = _seenTarget;
        }

        void ThinkUnarmed()
        {
            _seesPlayer = false;
            _loco.FacePoint = null;

            if (Hands > 0)
            {
                Weapon nearest = NearestDroppedWeapon();
                if (nearest != null)
                {
                    Vector3 to = nearest.transform.position - transform.position;
                    to.y = 0f;
                    if (to.magnitude <= _cfg.pickupReach && TryPickUp(nearest)) return;
                    if (_squad != null) _squad.RouteToRecovery(_squadId, nearest.transform.position);
                    else _loco.Destination = nearest.transform.position;
                    _loco.Hurry = true;
                    return;
                }
            }

            // Nothing to shoot with: get out of the line of fire.
            if (_squad != null) { _squad.ClearRecoveryDestination(_squadId); return; }
            _loco.Destination = null;
            _loco.Hurry = false;
        }

        Weapon NearestDroppedWeapon()
        {
            Weapon best = null;
            float bestSqr = _cfg.weaponSeekRange * _cfg.weaponSeekRange;
            var all = Weapon.All;
            for (int i = 0; i < all.Count; i++)
            {
                Weapon w = all[i];
                if (w == null || w.Hold != WeaponHold.Dropped) continue;
                // Give a freshly dropped gun a moment to land (and the player a moment to enjoy the disarm).
                if (Time.unscaledTime - w.DroppedAt < 0.8f) continue;
                float sqr = (w.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = w;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ shooting

        void TickReload(float dt)
        {
            FirearmState state = Weapon.State;
            state.Tick(dt);

            if (state.Reloading)
            {
                if (!_magazineOut && state.ReloadProgress > 0.25f)
                {
                    _magazineOut = true;
                    Weapon.SetMagazineVisible(false);
                    WeaponFx fx = WeaponFx.Instance;
                    if (fx != null) fx.Audio.PlayClick(Weapon.Magazine.position, 0.75f, 0.35f);
                }
                if (_magazineOut && state.ReloadProgress > 0.7f) Weapon.SetMagazineVisible(true);
            }
            else if (_magazineOut)
            {
                _magazineOut = false;
                Weapon.SetMagazineVisible(true);
            }
            else if (state.Ammo == 0)
            {
                // Empty: reload if the hands allow it. With the one-handed reload toggle off, a one-handed
                // character is simply out of bullets.
                state.StartReload(Hands);
            }
        }

        void UpdateShooting()
        {
            int hands = Hands;
            bool wantsToShoot = hands > 0 && _seesPlayer && _aimedAtTarget && _loco.FacePoint.HasValue
                                && _reaction.Ready(Time.time) && CombatWorld.PlayerPresent && !CombatWorld.EnemiesHoldFire
                                && (!RequireSquad || _squad != null);

            float now = Time.time;
            _triggerPressed = false;
            if (!wantsToShoot)
            {
                _triggerHeld = false;
            }
            else if (_triggerHeld)
            {
                if (now >= _triggerUntil)
                {
                    _triggerHeld = false;
                    _nextTriggerAt = now + Rand(_cfg.burstPause);
                }
            }
            else if (now >= _nextTriggerAt)
            {
                _triggerHeld = true;
                _triggerPressed = true;
                // Automatic guns fire a burst; other guns fire one shot per pull.
                _triggerUntil = now + (Weapon.Data.automatic ? Rand(_cfg.burstSeconds) : 0.05f);
            }

            if (_triggerHeld && Weapon.State.Cooldown <= 0 && !Weapon.State.Reloading && Weapon.State.Ammo > 0)
            {
                bool clear = _squad != null ? _squad.MayShoot(_squadId, Weapon.Muzzle.position, _seenTarget)
                    : Time.time - _seenAt <= 0.3f && !Physics.Linecast(Weapon.Muzzle.position, _seenTarget,
                        Layers.WorldMask, QueryTriggerInteraction.Ignore);
                if (!clear) { _triggerHeld = false; _triggerPressed = false; return; }
            }
            FireResult shot = Weapon.State.TryFire(hands, _triggerHeld, _triggerPressed);
            if (!shot.Fired) return;

            Vector3 muzzle = Weapon.Muzzle.position;
            Vector3 toTarget = _seenTarget - muzzle;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = Weapon.Muzzle.forward;
            Vector3 direction = Ballistics.SpreadDirection(toTarget, shot.SpreadDegrees + _cfg.aimError, _rng);

            ShotResult result = Ballistics.FireFromEnemy(Weapon.Data, muzzle, direction);
            WeaponFx.GetOrCreate().Shot(Weapon.Data, muzzle, result.End);
            _kick = Mathf.Min(2.5f, shot.RecoilScale);
            ShotsFired++;
        }

        // ------------------------------------------------------------------ holding the gun

        void LateUpdate()
        {
            if (Weapon == null || _owner == null || !_owner.Alive) return;
            PoseWeapon(Time.deltaTime, false);
        }

        Vector3 LowReadyDirection()
        {
            bool prone = _owner.State == MovementState.Crawl || _owner.State == MovementState.Wriggle;
            Vector3 dir = transform.forward + (prone ? Vector3.up * 0.05f : Vector3.down * 0.75f);
            return dir.normalized;
        }

        /// <summary>Places the gun in front of the shoulder, pointing where it should, and bends the arms to hold it.</summary>
        void PoseWeapon(float dt, bool snap)
        {
            int side = _gunSide;
            int hands = Hands;
            Vector3 shoulder = _rig.UpperArm[side].position;

            // Aim at the player only when asked to face them and they are roughly in front.
            Vector3 want = LowReadyDirection();
            bool aiming = false;
            if (_loco != null && _loco.FacePoint.HasValue && _seesPlayer)
            {
                Vector3 toTarget = _seenTarget - shoulder;
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                if (flat.sqrMagnitude > 0.01f && Vector3.Angle(flat, transform.forward) <= AimConeDegrees)
                {
                    want = toTarget.normalized;
                    aiming = true;
                }
            }

            bool twoHandedGrip = Weapon.Data.twoHanded && hands >= 2;
            if (Weapon.Data.twoHanded && hands == 1)
            {
                // A rifle in one hand waves about. This is only for show; the real penalty is in the spread.
                float t = Time.time * 5.3f + GetInstanceID() * 0.01f;
                want = Quaternion.Euler(Mathf.Sin(t) * 4f, Mathf.Cos(t * 0.7f) * 4f, 0f) * want;
            }

            _aimDirection = snap || dt <= 0f
                ? want
                : Vector3.Slerp(_aimDirection, want, 1f - Mathf.Exp(-11f * dt)).normalized;
            if (_aimDirection.sqrMagnitude < 0.0001f) _aimDirection = want;
            _aimedAtTarget = aiming && Vector3.Angle(_aimDirection, want) < 9f;

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 7f);

            // Two hands: tucked in near the shoulder. One hand: held out at nearly arm's length.
            Vector3 inward = transform.right * (side == MannequinRig.Left ? 1f : -1f);
            Vector3 grip = twoHandedGrip
                ? shoulder + _aimDirection * 0.30f + Vector3.down * 0.10f + inward * 0.07f
                : shoulder + _aimDirection * 0.50f;

            Vector3 up = Mathf.Abs(Vector3.Dot(_aimDirection, Vector3.up)) > 0.97f ? transform.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(_aimDirection, up) * Quaternion.Euler(-5f * _kick, 0f, 0f);
            Vector3 position = grip - _aimDirection * (0.03f * _kick);
            // The weapon's origin is its grip socket.
            Weapon.transform.SetPositionAndRotation(position, rotation);

            SolveArm(side, position);
            if (twoHandedGrip) SolveArm(1 - side, Weapon.SupportHand.position);
        }

        /// <summary>Bends one arm (two bones) so that its palm reaches a point. The elbow drops down and out.</summary>
        void SolveArm(int side, Vector3 target)
        {
            Transform upper = _rig.UpperArm[side];
            Transform fore = _rig.Forearm[side];
            Transform hand = _rig.Hand[side];

            float upperLength = Vector3.Distance(upper.position, fore.position);
            float foreLength = Vector3.Distance(fore.position, hand.position) + PalmLength;
            if (upperLength < 0.001f || foreLength < 0.001f) return;

            Vector3 shoulder = upper.position;
            Vector3 to = target - shoulder;
            float distance = Mathf.Clamp(to.magnitude, Mathf.Abs(upperLength - foreLength) + 0.01f, upperLength + foreLength - 0.005f);
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;

            float cos = (upperLength * upperLength + distance * distance - foreLength * foreLength) / (2f * upperLength * distance);
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 outward = transform.right * (side == MannequinRig.Left ? -1f : 1f);
            Vector3 axis = Vector3.Cross(dir, (Vector3.down + outward * 0.6f).normalized);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(dir, transform.right);
            Vector3 upperDir = Quaternion.AngleAxis(angle, axis.normalized) * dir;

            Vector3 elbow = shoulder + upperDir * upperLength;
            Vector3 foreDir = shoulder + dir * distance - elbow;
            foreDir = foreDir.sqrMagnitude > 0.0001f ? foreDir.normalized : upperDir;

            // In the rest pose each arm bone lies along its own X axis, pointing away from the body.
            Vector3 boneAxis = side == MannequinRig.Left ? Vector3.left : Vector3.right;
            upper.rotation = Quaternion.FromToRotation(boneAxis, upperDir);
            fore.rotation = Quaternion.FromToRotation(boneAxis, foreDir);
            hand.localRotation = Quaternion.identity;
        }
    }
}
