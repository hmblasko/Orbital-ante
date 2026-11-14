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
        float _reactAt;
        float _lastSaw = -1000f;
        /// <summary>The camp this one belongs to, if any: who may shoot now, and what the others have seen. Null in the arena.</summary>
        public OrbitalAnte.Enemies.Squad Squad;
        float _tellUntil;
        bool _telling;
        LineRenderer _tellLine;
        bool _shootPhase = true;
        float _phaseEnd;
        Vector3 _movePoint;
        bool _triggerHeld;
        bool _triggerPressed;
        float _triggerUntil;
        float _nextTriggerAt;
        Vector3 _aimDirection = Vector3.forward;
        // Gives each one-handed wobble its own rhythm (set on first use: Unity forbids Random in constructors). Visual only.
        float _wobblePhase = -1f;
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
            if (_owner != null)
            {
                _owner.PartDetached -= OnPartDetached;
                _owner.Died -= OnDied;
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
            weapon.TakeByCharacter(this);
            weapon.transform.SetParent(transform, true);
            weapon.transform.localScale = Vector3.one;
            weapon.State.HandsChanged(Hands);
            _magazineOut = false;
            _aimedAtTarget = false;
            _seesPlayer = false;
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
            _telling = false;
            ShowTell(false);
            _aimedAtTarget = false;

            Transform parent = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            Vector3 velocity = (_owner != null ? _owner.Velocity : Vector3.zero) + extraVelocity;
            weapon.Drop(parent, velocity, Random.onUnitSphere * spin);
        }

        /// <summary>The gun itself was shot: it is knocked out of the hands along the bullet.</summary>
        public void WeaponShotOutOfHands(Weapon weapon, in HitInfo hit)
        {
            if (weapon != Weapon) return;
            Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : -transform.forward;
            DropWeapon(dir * _cfg.disarmKnockSpeed + transform.up * (_cfg.disarmKnockSpeed * 0.55f), 14f);
        }

        void OnPartDetached(Mannequin mannequin, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (Weapon == null) return;

            // Losing the gun hand, its forearm or anything above them means the gun falls.
            if (!_owner.Model.HandUsable(_gunSide == MannequinRig.Left))
            {
                Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Vector3.zero;
                DropWeapon(dir * 2f + transform.up * 2f, 7f);
                return;
            }

            // Losing the other hand keeps the gun, but everything gets harder (and a reload may be abandoned).
            Weapon.State.HandsChanged(Hands);
        }

        void OnDied(Mannequin mannequin)
        {
            DropWeapon(transform.up * 1.5f, 5f);
            if (_loco != null)
            {
                _loco.Destination = null;
                _loco.FacePoint = null;
            }
        }

        // ------------------------------------------------------------------ thinking

        void Update()
        {
            if (_owner == null || !_owner.Alive) { ShowTell(false); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Weapon != null) TickReload(dt);

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Rand(0.12f, 0.22f);
                Think();
            }

            if (Weapon != null) UpdateShooting();
        }

        Vector3 HeadPosition
        {
            get
            {
                BodyPart head = _owner.Part(BodyPartId.Head);
                return head != null ? head.transform.position : transform.position + transform.up * 1.6f;
            }
        }

        Vector3 PlayerAimPoint => CombatWorld.PlayerEye.position - transform.up * 0.3f;

        void Think()
        {
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
            _seesPlayer = false;
        }

        void ThinkArmed(MovementState state)
        {
            if (!CombatWorld.PlayerPresent || CombatWorld.EnemiesHoldFire)
            {
                ClearGoals();
                return;
            }

            Vector3 target = PlayerAimPoint;
            Vector3 head = HeadPosition;
            float distance = Vector3.Distance(head, target);
            bool sees = distance <= _cfg.engageRange
                        && !Physics.Linecast(head, target, Layers.WorldMask, QueryTriggerInteraction.Ignore);

            float now = Time.time;
            if (!sees)
            {
                // Lost sight. It only comes looking if it saw the player a moment ago or can hear them close by:
                // nobody knows where you are from across the valley. Otherwise it goes back to wandering.
                _seesPlayer = false;
                _loco.FacePoint = null;
                bool hunting = now - _lastSaw <= _cfg.chaseMemory || distance <= _cfg.hearRange
                               || (Squad != null && Squad.SawWithin(now, _cfg.chaseMemory));
                _loco.Destination = hunting ? CombatWorld.PlayerEye.position : (Vector3?)null;
                _loco.Hurry = hunting && _cfg.rushes;
                return;
            }
            _lastSaw = now;
            if (Squad != null) Squad.Saw(now);
            if (!_seesPlayer)
            {
                // Never an instant shot: there is always a beat between spotting the player and firing.
                _seesPlayer = true;
                _reactAt = now + _cfg.reactionTime;
                _shootPhase = true;
                _phaseEnd = now + Rand(1.5f, 2.6f);
            }

            bool onFeet = state == MovementState.Intact || state == MovementState.OneLeg;
            if (onFeet && now >= _phaseEnd && !_cfg.holdsStill)
            {
                // Alternate between standing to shoot and shifting position, so nobody is a statue.
                _shootPhase = !_shootPhase;
                _phaseEnd = now + (_shootPhase ? Rand(1.4f, 2.6f) : Rand(0.9f, 1.8f));
                if (!_shootPhase) _movePoint = PickMovePoint(distance);
            }

            if (onFeet && _cfg.closesIn && distance > _cfg.keepFar)
            {
                // The ones that fight close do not dance at range: they come straight at you, and you can see it.
                _loco.FacePoint = null;
                _loco.Destination = CombatWorld.PlayerEye.position;
                _loco.Hurry = _cfg.rushes;
            }
            else if (_shootPhase || !onFeet)
            {
                _loco.FacePoint = CombatWorld.PlayerEye.position;
                _loco.Destination = null;
            }
            else
            {
                _loco.FacePoint = null;
                _loco.Destination = _movePoint;
                _loco.Hurry = _cfg.rushes;
            }
        }

        /// <summary>A few steps sideways, drifting toward a comfortable fighting distance.</summary>
        Vector3 PickMovePoint(float distance)
        {
            Vector3 toPlayer = Vector3.ProjectOnPlane(CombatWorld.PlayerEye.position - transform.position, transform.up);
            toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            Vector3 side = Vector3.Cross(transform.up, toPlayer) * (_rng.NextDouble() < 0.5 ? -1f : 1f);
            float along = distance > _cfg.keepFar ? 3f : distance < _cfg.keepNear ? -3f : 0f;
            return transform.position + side * Rand(2.5f, 5f) + toPlayer * along;
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
                    Vector3 to = Vector3.ProjectOnPlane(nearest.transform.position - transform.position, transform.up);
                    if (to.magnitude <= _cfg.pickupReach && TryPickUp(nearest)) return;
                    _loco.Destination = nearest.transform.position;
                    _loco.Hurry = true;
                    return;
                }
            }

            // Nothing to shoot with: get out of the line of fire.
            if (CombatWorld.PlayerPresent && CombatWorld.FindCover != null)
            {
                Vector3? cover = CombatWorld.FindCover(transform.position, CombatWorld.PlayerEye.position);
                if (cover.HasValue)
                {
                    _loco.Destination = cover.Value;
                    _loco.Hurry = true;
                    return;
                }
            }
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
                                && Time.time >= _reactAt && CombatWorld.PlayerPresent && !CombatWorld.EnemiesHoldFire;

            float now = Time.time;
            _triggerPressed = false;
            if (!wantsToShoot)
            {
                _triggerHeld = false;
                _telling = false;
            }
            else if (_triggerHeld)
            {
                if (now >= _triggerUntil)
                {
                    _triggerHeld = false;
                    _nextTriggerAt = now + Rand(_cfg.burstPause);
                }
            }
            else if (now >= _nextTriggerAt && !_telling && Squad != null
                     && !Squad.TakeTurn(now, _cfg.aimTell > 0f ? _cfg.aimTell + 0.4f : (Weapon.Data.automatic ? _cfg.burstSeconds.y : 0.5f)))
            {
                // Somebody else's turn to shoot: ask again in a moment.
                _nextTriggerAt = now + 0.25f;
            }
            else if (now >= _nextTriggerAt && _cfg.aimTell > 0f && !_telling)
            {
                // A heavy single shot is always announced: the line shows for a moment first.
                _telling = true;
                _tellUntil = now + _cfg.aimTell;
            }
            else if (now >= _nextTriggerAt && (!_telling || now >= _tellUntil))
            {
                _telling = false;
                _triggerHeld = true;
                _triggerPressed = true;
                // Automatic guns fire a burst; other guns fire one shot per pull.
                _triggerUntil = now + (Weapon.Data.automatic ? Rand(_cfg.burstSeconds) : 0.05f);
            }

            ShowTell(_telling);
            FireResult shot = Weapon.State.TryFire(hands, _triggerHeld, _triggerPressed);
            if (!shot.Fired) return;

            Vector3 muzzle = Weapon.Muzzle.position;
            Vector3 toTarget = PlayerAimPoint - muzzle;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = Weapon.Muzzle.forward;
            Vector3 direction = Ballistics.SpreadDirection(toTarget, shot.SpreadDegrees + _cfg.aimError, _rng);

            ShotResult result = Ballistics.FireFromEnemy(Weapon.Data, muzzle, direction);
            WeaponFx.GetOrCreate().Shot(Weapon.Data, muzzle, result.End);
            _kick = Mathf.Min(2.5f, shot.RecoilScale);
            if (_loco != null) _loco.Kick(_kick, _gunSide);
            ShotsFired++;
        }

        /// <summary>The thin bright line from the muzzle to where a heavy shot is about to go.</summary>
        void ShowTell(bool on)
        {
            SetLens(on);
            if (!on || Weapon == null)
            {
                if (_tellLine != null && _tellLine.enabled) _tellLine.enabled = false;
                return;
            }
            if (_tellLine == null)
            {
                var go = new GameObject("AimLine");
                go.transform.SetParent(transform, false);
                _tellLine = go.AddComponent<LineRenderer>();
                _tellLine.useWorldSpace = true;
                _tellLine.positionCount = 2;
                _tellLine.startWidth = 0.03f;
                _tellLine.endWidth = 0.03f;
                _tellLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _tellLine.receiveShadows = false;
                _tellLine.sharedMaterial = TellMaterial();
            }
            _tellLine.enabled = true;
            _tellLine.SetPosition(0, Weapon.Muzzle.position);
            _tellLine.SetPosition(1, PlayerAimPoint);
        }

        Weapon _lensOf;
        GameObject _lens;

        /// <summary>The scope's front glass (the gun model's LensGlow piece, if it has one) shines only while a shot is being lined up.</summary>
        void SetLens(bool on)
        {
            if (Weapon != _lensOf)
            {
                if (_lens != null) _lens.SetActive(true);
                _lensOf = Weapon;
                _lens = null;
                if (Weapon != null)
                    foreach (Transform t in Weapon.GetComponentsInChildren<Transform>(true))
                        if (t.name == "LensGlow") { _lens = t.gameObject; break; }
            }
            if (_lens != null && _lens.activeSelf != on) _lens.SetActive(on);
        }

        static Material _tellMaterial;
        static Material TellMaterial()
        {
            if (_tellMaterial == null) _tellMaterial = Mat.Unlit(new Color(1f, 0.25f, 0.2f));
            return _tellMaterial;
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
            Vector3 dir = transform.forward + (prone ? transform.up * 0.05f : (-transform.up) * 0.75f);
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
            if (_loco != null && _loco.FacePoint.HasValue && CombatWorld.PlayerEye != null)
            {
                Vector3 toTarget = PlayerAimPoint - shoulder;
                Vector3 flat = Vector3.ProjectOnPlane(toTarget, transform.up);
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
                if (_wobblePhase < 0f) _wobblePhase = UnityEngine.Random.Range(0f, 100f);
                float t = Time.time * 5.3f + _wobblePhase;
                want = Quaternion.AngleAxis(Mathf.Sin(t) * 4f, transform.right) * Quaternion.AngleAxis(Mathf.Cos(t * 0.7f) * 4f, transform.up) * want;
            }

            _aimDirection = snap || dt <= 0f
                ? want
                : Vector3.Slerp(_aimDirection, want, 1f - Mathf.Exp(-11f * dt)).normalized;
            if (_aimDirection.sqrMagnitude < 0.0001f) _aimDirection = want;
            _aimedAtTarget = aiming && Vector3.Angle(_aimDirection, want) < 9f;

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 7f);

            // Two hands: tucked in near the shoulder. One hand: held out at nearly arm's length.
            Vector3 inward = transform.right * (side == MannequinRig.Left ? 1f : -1f);
            // The body may be bigger or smaller than the ordinary one, and its arms with it.
            float size = transform.lossyScale.y;
            Vector3 grip = twoHandedGrip
                ? shoulder + _aimDirection * (0.30f * size) + (-transform.up) * (0.10f * size) + inward * (0.07f * size)
                : shoulder + _aimDirection * (0.50f * size);

            Vector3 up = Mathf.Abs(Vector3.Dot(_aimDirection, transform.up)) > 0.97f ? transform.forward : transform.up;
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
            float foreLength = Vector3.Distance(fore.position, hand.position) + PalmLength * transform.lossyScale.y;
            if (upperLength < 0.001f || foreLength < 0.001f) return;

            Vector3 shoulder = upper.position;
            Vector3 to = target - shoulder;
            float distance = Mathf.Clamp(to.magnitude, Mathf.Abs(upperLength - foreLength) + 0.01f, upperLength + foreLength - 0.005f);
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;

            float cos = (upperLength * upperLength + distance * distance - foreLength * foreLength) / (2f * upperLength * distance);
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 outward = transform.right * (side == MannequinRig.Left ? -1f : 1f);
            Vector3 axis = Vector3.Cross(dir, ((-transform.up) + outward * 0.6f).normalized);
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
