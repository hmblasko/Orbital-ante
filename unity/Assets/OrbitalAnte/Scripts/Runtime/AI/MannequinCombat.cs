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
    public sealed partial class MannequinCombat : MonoBehaviour, IWeaponHolder
    {
        const float PalmLength = 0.07f;
        const float AimConeDegrees = 55f;

        Mannequin _owner;
        MannequinLocomotion _loco;
        MannequinRig _rig;
        MannequinConfig _cfg;

        /// <summary>How much sooner this enemy reacts and how much truer it aims than its kind's settings (1 = as set;
        /// set by the spawner from the ante's pressure, and changed on enemies already awake when an ante starts).</summary>
        [System.NonSerialized] public float Sharpness = 1f;
        float ReactionTime => _cfg.reactionTime * Sharpness;
        float AimError => _cfg.aimError * Sharpness;
        SeededRandom _rng = new SeededRandom(7);

        int _gunSide = MannequinRig.Right;
        float _thinkTimer;
        bool _seesPlayer;
        float _reactAt;
        float _lastSaw = -1000f;
        /// <summary>Where the player was last heard, and when; null when nothing has been heard lately.</summary>
        Vector3? _heardAt;
        float _heardTime = -1000f, _heardPauseUntil = -1000f;
        bool _heardHolding;
        /// <summary>A sound gives a place, not a track: a new place is taken at most this often.</summary>
        const float HeardEvery = 1.2f;
        /// <summary>The beat an enemy who heard something stands and turns to it before going to look.</summary>
        const float HeardPause = 0.7f;
        /// <summary>The camp this one belongs to, if any: who may shoot now, and what the others have seen. Null in the arena.</summary>
        [System.NonSerialized] public OrbitalAnte.Enemies.Squad Squad;
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

        // Losing the gun: a startled beat looking after it, then a scramble to get it back, then a quick crouch
        // to snatch it off the ground (about half the time a calm pick-up takes: it is in a hurry).
        const float StartleSeconds = 0.55f;
        // Long enough to read as the player's last chance to deny the gun (shoot it away, or the reaching arm).
        const float GrabSeconds = 0.4f;
        const float ProneGrabSeconds = 0.35f;
        Weapon _lostGun;
        float _startledUntil = -1f;
        float _grabAt = -1f;
        Weapon _grabbing;
        float _glanceUntil = -1f;
        Weapon _scrambleFor;
        // Giving up on a gun it cannot get to (on a ledge, down a pit, behind a wall).
        Weapon _chasing;
        float _chaseStart, _chaseBest;
        Weapon _givenUp;
        // Running away: one place, kept until reached.
        Vector3? _fleePoint;
        float _fleeUntil;

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

        // Knocked down by a melee hit: cannot shoot or move by itself until this time (Time.time).
        float _downUntil = -1f;
        bool _down;

        /// <summary>True while knocked down by a melee hit (the bat, the mailbox): no shooting, no walking.</summary>
        public bool KnockedDown => _owner != null && _owner.Alive && Time.time < _downUntil;

        /// <summary>
        /// True when it (or its squad) has seen the player lately: the same memory that keeps a spotted player
        /// hunted. The butterfly knife's takedown and its HUD cue both ask this.
        /// </summary>
        public bool AwareOfPlayer
        {
            get
            {
                float now = Time.time;
                return now - _lastSaw <= _cfg.chaseMemory || (Squad != null && Squad.SawWithin(now, _cfg.chaseMemory));
            }
        }

        /// <summary>It was just hit up close by the player: it knows where they are now.</summary>
        public void Noticed()
        {
            _lastSaw = Time.time;
            CombatWorld.PlayerLastSeenAt = _lastSaw;
            if (Squad != null) Squad.Saw(_lastSaw);
        }

        /// <summary>
        /// Knocked off its feet by a melee hit: it slides along the ground at <paramref name="shove"/> (m/s),
        /// cannot shoot for <paramref name="seconds"/>, then gets up and carries on. Keeps its gun if it still has
        /// a hand for it (a launched enemy that keeps its gun gets up and shoots: the bat's weakness).
        /// </summary>
        public void KnockDown(Vector3 shove, float seconds, float friction)
        {
            if (_owner == null || !_owner.Alive) return;
            DropCover();
            // Knocked flat: whatever it was reaching for is let go.
            _grabbing = null;
            _grabAt = -1f;
            _scrambleFor = null;
            _downUntil = Mathf.Max(_downUntil, Time.time + Mathf.Max(0.1f, seconds));
            _down = true;
            _triggerHeld = false;
            _telling = false;
            ShowTell(false);
            Noticed();
            if (_loco != null)
            {
                _loco.Shove(shove, friction);
                _loco.Hold = true;
                _loco.Destination = null;
                _loco.FacePoint = null;
                _loco.Hurry = false;
                _loco.Reach = 0f;
            }
            // Think again as soon as it is back up.
            _thinkTimer = Mathf.Max(_thinkTimer, seconds + 0.02f);
        }

        public void Initialize(Mannequin owner, MannequinLocomotion locomotion)
        {
            _owner = owner;
            _loco = locomotion;
            _rig = owner.Rig;
            _cfg = owner.Config;
            _aimDirection = transform.forward;
            _owner.PartDetached += OnPartDetached;
            _owner.Died += OnDied;
            SubscribeFaction();
        }

        public void SetSeed(int seed)
        {
            _rng = new SeededRandom(seed);
            _thinkTimer = (float)_rng.NextDouble() * 0.3f;
        }

        void OnDestroy()
        {
            UnsubscribeFaction();
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
            _lostGun = null;
            _grabbing = null;
            _grabAt = -1f;
            if (_loco != null)
            {
                _loco.Scramble = false;
                _loco.Reach = 0f;
                _loco.LookAt = null;
                _loco.Hold = false;
            }
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
            LetGoOfGunAbilities();
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
            KnockedOutOfHands(dir * _cfg.disarmKnockSpeed + transform.up * (_cfg.disarmKnockSpeed * 0.55f), 14f);
        }

        /// <summary>
        /// The frame the gun was last knocked out of its living hands (shot out, or the hand or arm holding it shot off),
        /// or -1. A player's hit is raised the same frame it lands, so the task director reads a disarm from it for the
        /// goal unlock (Ralph loop pass 5) without the hit having to say so.
        /// </summary>
        public int DisarmedFrame { get; private set; } = -1;

        /// <summary>The gun is gone from its hands while it lives: it drops, and the character is startled.</summary>
        void KnockedOutOfHands(Vector3 extraVelocity, float spin)
        {
            DropCover();
            Weapon weapon = Weapon;
            DropWeapon(extraVelocity, spin);
            if (weapon == null || _owner == null || !_owner.Alive) return;
            DisarmedFrame = Time.frameCount;
            _lostGun = weapon;
            _startledUntil = Time.time + StartleSeconds;
            _grabbing = null;
            _grabAt = -1f;
            if (_loco != null)
            {
                // Toward where the gun is going, not where it still is (in its hand).
                _loco.Startle(weapon.transform.position + extraVelocity * 0.5f);
                _loco.Hold = true;
                _loco.LookAt = weapon.transform.position;
                _loco.Destination = null;
                _loco.FacePoint = null;
                _loco.Hurry = false;
                _loco.Scramble = false;
                _loco.Reach = 0f;
            }
            // Think again as soon as the startle is over, not on the next round of thinking.
            _thinkTimer = StartleSeconds + 0.02f;
        }

        void OnPartDetached(Mannequin mannequin, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (Weapon == null) return;

            // Losing the gun hand, its forearm or anything above them means the gun falls.
            if (!_owner.Model.HandUsable(_gunSide == MannequinRig.Left))
            {
                Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Vector3.zero;
                KnockedOutOfHands(dir * 2f + transform.up * 2f, 7f);
                return;
            }

            // Losing the other hand keeps the gun, but everything gets harder (and a reload may be abandoned).
            Weapon.State.HandsChanged(Hands);
        }

        void OnDied(Mannequin mannequin)
        {
            EndAbilities();
            DropCover();
            LeaveSquad();
            DropWeapon(transform.up * 1.5f, 5f);
            if (_loco != null)
            {
                _loco.Destination = null;
                _loco.FacePoint = null;
                _loco.Scramble = false;
                _loco.Reach = 0f;
                _loco.LookAt = null;
                _loco.Hold = false;
            }
        }

        // ------------------------------------------------------------------ thinking

        void Update()
        {
            if (_owner == null || !_owner.Alive) { ShowTell(false); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Weapon != null) TickReload(dt);
            else if (_grabbing != null) TickGrab();
            else if (_scrambleFor != null && _loco.Scramble && _scrambleFor.Hold == WeaponHold.Dropped && Reachable(_scrambleFor)
                     && Vector3.ProjectOnPlane(_scrambleFor.transform.position - transform.position, transform.up).magnitude <= _cfg.pickupReach)
            {
                // Arrived: stop now rather than at the next round of thinking, or a sprint carries it past the gun.
                _thinkTimer = 0f;
            }

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Rand(0.12f, 0.22f);
                Think();
            }

            TickAbilities(Time.time);
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

        Vector3 PlayerAimPoint => CombatWorld.PlayerEye.position - transform.up * CombatWorld.PlayerAimBelowEye;

        void Think()
        {
            MovementState state = _owner.State;
            // Only an armed body in a fight hunches; ThinkArmed says when.
            _loco.Tactical = 0f;
            if (state == MovementState.HeadOnly)
            {
                // A head on its own has nothing to fight with. It just rolls about.
                LeaveSquad();
                ClearGoals();
                return;
            }

            if (KnockedDown)
            {
                // Down on the ground after a melee hit: nothing until it is back up.
                _loco.Hold = true;
                _loco.Destination = null;
                _loco.FacePoint = null;
                return;
            }
            if (_down)
            {
                _down = false;
                _loco.Hold = false;
            }

            TickSquad(state, Time.time);
            if (Weapon != null) ThinkArmed(state);
            else if (Time.time < _startledUntil)
            {
                // The startled beat: rooted to the spot, eyes on where the gun went.
                _loco.Hold = true;
                _loco.Destination = null;
                _loco.FacePoint = null;
                _loco.Hurry = false;
                _loco.Scramble = false;
                if (_lostGun != null) _loco.LookAt = _lostGun.transform.position;
            }
            else if (_grabbing == null) ThinkUnarmed();
        }

        void ClearGoals()
        {
            DropCover();
            _loco.Destination = null;
            _loco.FacePoint = null;
            _loco.Hurry = false;
            _loco.Scramble = false;
            _loco.Reach = 0f;
            _loco.LookAt = null;
            _loco.Hold = false;
            _grabbing = null;
            _seesPlayer = false;
            _heardAt = null; _sentUntil = -1000f;
            _heardHolding = false;
        }

        /// <summary>The "heard something" beat: stand still with the head turned to the sound, so the player can
        /// see that they were heard. Only ever clears the hold and look it set itself.</summary>
        void SetHeardBeat(bool on, Vector3? toward)
        {
            if (on)
            {
                _loco.Hold = true;
                _loco.LookAt = toward;
                _heardHolding = true;
            }
            else if (_heardHolding)
            {
                _loco.Hold = false;
                _loco.LookAt = null;
                _heardHolding = false;
            }
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
            float now = Time.time;
            bool sees = distance <= SightRange(now) && ClearSight(head, target);
            if (sees)
            {
                // Faction A's Scout fires its flare on a fresh sighting, unless its squad is already on to the player.
                // "Already" means the squad had the player before this moment, not a squadmate who looked a
                // fraction of a second earlier in the same sighting.
                if (now - _lastSaw > _cfg.chaseMemory)
                {
                    FirstSight(now, Squad != null && Squad.SawWithin(now, _cfg.chaseMemory) && !Squad.SawWithin(now, 1f));
                    // The visor's amber warning: this one has just picked the player out and fires after its beat.
                    CombatWorld.RaisePlayerSpotted(head, FirstShotBeat(), now - CombatWorld.PlayerLastSeenAt >= _cfg.chaseMemory);
                }
                _lastSaw = now;
                CombatWorld.PlayerLastSeenAt = now;
                if (Squad != null) Squad.Saw(now);
            }
            if (_flare.Raising && CombatWorld.PlayerEye != null)
            {
                // Rooted, arm up, facing the player: the window to stop it.
                _loco.FacePoint = CombatWorld.PlayerEye.position;
                _loco.LookAt = CombatWorld.PlayerEye.position;
                return;
            }
            // Cover and flanking (MannequinCombat.Tactics.cs); when they do not apply, the fight below goes on as before.
            if (ThinkTactics(state, sees, distance, now)) return;

            if (!sees)
            {
                // Lost sight. It only comes looking if it saw the player a moment ago or can hear them close by:
                // nobody knows where you are from across the valley. Otherwise it goes back to wandering.
                _seesPlayer = false;
                _loco.FacePoint = null;
                bool chasing = now - _lastSaw <= _cfg.chaseMemory || (Squad != null && Squad.SawWithin(now, _cfg.chaseMemory));
                // A sneaking, crouched or prone player is heard from less far, a shot from much further. A sound only
                // says where the player was when it was made: the enemy turns to it, then goes to look there. It
                // never follows the player live through a hill by ear, and a player who goes quiet is lost.
                bool hears = distance <= _cfg.hearRange * CombatWorld.PlayerHeardFrom;
                if (hears && (_heardAt == null || now - _heardTime >= HeardEvery))
                {
                    if (_heardAt == null && !chasing) _heardPauseUntil = now + HeardPause;
                    _heardAt = CombatWorld.PlayerEye.position; _sentUntil = -1000f;
                    _heardTime = now;
                }
                if (_heardAt != null && now - _heardTime > Mathf.Max(_cfg.chaseMemory, _alarmMemoryUntil - _heardTime)) { _heardAt = null; _sentUntil = -1000f; }

                bool pausing = !chasing && _heardAt != null && now < _heardPauseUntil;
                SetHeardBeat(pausing, _heardAt);
                if (chasing) _loco.Destination = CombatWorld.PlayerEye.position;
                else if (_heardAt != null && !pausing) _loco.Destination = _heardAt;
                else _loco.Destination = null;
                bool hunting = chasing || _heardAt != null;
                // Sent by a long quiet (SendToLook): it runs until it is near, then comes on low and careful.
                _loco.Hurry = (chasing && _cfg.rushes)
                              || (!chasing && _heardAt != null && !pausing && now < _sentUntil
                                  && (transform.position - _heardAt.Value).sqrMagnitude > _sentWalkIn * _sentWalkIn);
                // Coming to look for the player: low and careful, gun up.
                _loco.Tactical = hunting ? _cfg.tacticalHunch : 0f;
                return;
            }
            _heardAt = null;
            _sentUntil = -1000f;
            SetHeardBeat(false, null);
            _loco.Tactical = _cfg.tacticalHunch;
            _lastSaw = now;
            if (Squad != null) Squad.Saw(now);
            if (!_seesPlayer)
            {
                // Never an instant shot: there is always a beat between spotting the player and firing.
                _seesPlayer = true;
                _reactAt = now + ReactionTime;
                _shootPhase = true;
                _phaseEnd = now + Rand(1.5f, 2.6f);
            }

            bool onFeet = state == MovementState.Intact || state == MovementState.OneLeg;
            // A braced Heavy stays planted until its burst is done (MannequinCombat.Faction.cs).
            bool braced = _spin.Braced;
            if (onFeet && now >= _phaseEnd && !_cfg.holdsStill && !braced)
            {
                // Alternate between standing to shoot and shifting position, so nobody is a statue.
                _shootPhase = !_shootPhase;
                _phaseEnd = now + (_shootPhase ? Rand(1.4f, 2.6f) : Rand(0.9f, 1.8f));
                if (!_shootPhase) _movePoint = PickMovePoint(distance);
            }

            if (onFeet && _cfg.closesIn && distance > _cfg.keepFar && !braced)
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
            _loco.Reach = 0f;
            _loco.Hold = false;

            if (Hands > 0)
            {
                // Its own gun first, if it is still lying there; otherwise the nearest one.
                Weapon gun = _lostGun != null && _lostGun != _givenUp && _lostGun.Hold == WeaponHold.Dropped && Reachable(_lostGun)
                             && (_lostGun.transform.position - transform.position).sqrMagnitude <= _cfg.weaponSeekRange * _cfg.weaponSeekRange
                    ? _lostGun
                    : NearestDroppedWeapon();
                if (gun != null && !_loco.Wander)
                {
                    // A lookout does not leave its post: it stares at the gun it cannot go and get.
                    _loco.Scramble = false;
                    _loco.LookAt = gun.transform.position;
                    return;
                }
                if (gun != null && GivingUp(gun)) gun = null;
                if (gun != null)
                {
                    Vector3 to = Vector3.ProjectOnPlane(gun.transform.position - transform.position, transform.up);
                    if (to.magnitude <= _cfg.pickupReach)
                    {
                        // Close enough: stop and snatch it up.
                        _grabbing = gun;
                        bool prone = _owner.State == MovementState.Crawl || _owner.State == MovementState.Wriggle;
                        _grabAt = Time.time + (prone ? ProneGrabSeconds : GrabSeconds);
                        _loco.Destination = null;
                        _loco.Hurry = false;
                        _loco.Scramble = false;
                        _loco.LookAt = gun.transform.position;
                        _loco.ReachSide = _owner.Model.HandUsable(false) ? MannequinRig.Right : MannequinRig.Left;
                        _loco.Reach = 1f;
                        _loco.Hold = true;
                        return;
                    }
                    _loco.Destination = gun.transform.position;
                    _loco.Hurry = true;
                    _loco.Scramble = true;
                    _scrambleFor = gun;
                    // Eyes on the gun, with a nervous glance back at the player now and then.
                    float now = Time.time;
                    if (now >= _glanceUntil + 0.9f && CombatWorld.PlayerPresent && _rng.NextDouble() < 0.25) _glanceUntil = now + 0.35f;
                    _loco.LookAt = now < _glanceUntil && CombatWorld.PlayerPresent ? CombatWorld.PlayerEye.position : gun.transform.position;
                    return;
                }
            }

            _loco.Scramble = false;
            // Nothing to shoot with: get out of the line of fire, looking back over its shoulder while it can see
            // the player. It never knows where the player is through a hill.
            bool seen = CombatWorld.PlayerPresent && SeesPlayer();
            if (seen) { _lastSaw = Time.time; CombatWorld.PlayerLastSeenAt = _lastSaw; }
            bool threatened = CombatWorld.PlayerPresent && (seen || Time.time - _lastSaw <= _cfg.chaseMemory);
            _loco.LookAt = seen ? CombatWorld.PlayerEye.position : (Vector3?)null;
            if (CombatWorld.PlayerPresent && (CombatWorld.FindCover != null || threatened))
            {
                // The arena has its own list of walls; everywhere else cover is found by looking (CoverFinder).
                Vector3? cover = CombatWorld.FindCover != null
                    ? CombatWorld.FindCover(transform.position, CombatWorld.PlayerEye.position)
                    : HideFromPlayer();
                if (cover.HasValue)
                {
                    _loco.Destination = cover.Value;
                    _loco.Hurry = true;
                    return;
                }
            }
            if (threatened)
            {
                // No cover anywhere: run away from the player rather than wander about in front of them. One
                // place, eight metres off, kept until it gets there; then it thinks again.
                bool arrived = _fleePoint.HasValue && Vector3.ProjectOnPlane(_fleePoint.Value - transform.position, transform.up).sqrMagnitude < 1f;
                if (!_fleePoint.HasValue || arrived || Time.time > _fleeUntil)
                {
                    Vector3 away = Vector3.ProjectOnPlane(transform.position - CombatWorld.PlayerEye.position, transform.up);
                    away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
                    _fleePoint = arrived && !seen ? (Vector3?)null : transform.position + away * 8f;
                    _fleeUntil = Time.time + 4f;
                }
                if (_fleePoint.HasValue)
                {
                    _loco.Destination = _fleePoint;
                    _loco.Hurry = true;
                    return;
                }
            }
            _fleePoint = null;
            _loco.Destination = null;
            _loco.Hurry = false;
        }

        bool SeesPlayer()
        {
            Vector3 head = HeadPosition, eye = CombatWorld.PlayerEye.position;
            return Vector3.Distance(head, eye) <= SightRange(Time.time) && ClearSight(head, eye);
        }

        /// <summary>
        /// How far off it can see the player now. One that has seen them lately (or whose squad has) keeps them in
        /// view to its full range whatever they do: once spotted you are hunted. One that has not picks out a
        /// crouched, prone or still player only from closer (<see cref="CombatWorld.PlayerSeenFrom"/>).
        /// </summary>
        float SightRange(float now)
        {
            bool aware = now - _lastSaw <= _cfg.chaseMemory || (Squad != null && Squad.SawWithin(now, _cfg.chaseMemory));
            if (aware) return _cfg.engageRange;
            float range = _cfg.engageRange * Mathf.Clamp(CombatWorld.PlayerSeenFrom, 0.05f, 1f);
            // An enemy that has not seen you lately sees much less far behind its back, so sneaking up for a
            // takedown is possible. Once it has seen you, it sees all round again (you are hunted).
            if (CombatWorld.PlayerEye != null)
            {
                Vector3 to = Vector3.ProjectOnPlane(CombatWorld.PlayerEye.position - transform.position, transform.up);
                if (to.sqrMagnitude > 0.01f) range *= SightBehind(Vector3.Angle(transform.forward, to), Dials.behindSight);
            }
            return range;
        }

        /// <summary>
        /// How much of its sight range an unaware enemy has toward something this many degrees off its facing:
        /// all of it ahead, <paramref name="behind"/> in the 120 degrees behind its back, blended over 20 degrees.
        /// </summary>
        public static float SightBehind(float degreesOffFacing, float behind)
        {
            float t = Mathf.Clamp01((degreesOffFacing - 110f) / 20f);
            return Mathf.Lerp(1f, Mathf.Clamp01(behind), t);
        }

        /// <summary>
        /// Nothing solid between two points, looking both ways. Ground and cave walls are solid from one side only
        /// (the side you can stand on or see), so one look from inside a cave would go straight up through the
        /// ground to someone standing on it.
        /// </summary>
        internal static bool ClearSight(Vector3 a, Vector3 b)
        {
            if (Physics.Linecast(a, b, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            // A cave's walls face inward, and a line only hits a face from its front: from inside a cave to the
            // ground outside, the way back is the one that finds the rock. Only needed while a cave network is built.
            return !OrbitalAnte.Planets.CaveLight.AnyBuilt
                   || !Physics.Linecast(b, a, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Not too far above or below to bend down to (a gun on a ledge or down a pit is out of reach).</summary>
        bool Reachable(Weapon gun)
        {
            float up = Vector3.Dot(gun.transform.position - transform.position, transform.up);
            return Mathf.Abs(up) <= 1.2f * transform.lossyScale.y;
        }

        /// <summary>
        /// True once it has chased this gun for six seconds without getting any closer (it is behind something
        /// it cannot get round): it gives up on that gun and does something else.
        /// </summary>
        bool GivingUp(Weapon gun)
        {
            float distance = Vector3.ProjectOnPlane(gun.transform.position - transform.position, transform.up).magnitude;
            if (gun != _chasing)
            {
                _chasing = gun;
                _chaseStart = Time.time;
                _chaseBest = distance;
                return false;
            }
            if (distance < _chaseBest - 0.5f)
            {
                _chaseBest = distance;
                _chaseStart = Time.time;
            }
            if (Time.time - _chaseStart < 6f) return false;
            _givenUp = gun;
            _chasing = null;
            return true;
        }

        /// <summary>Bent down over a gun: takes it at the bottom of the reach, or gives up if it has been knocked away.</summary>
        void TickGrab()
        {
            Weapon gun = _grabbing;
            if (gun == null || gun.Hold != WeaponHold.Dropped || Hands == 0
                || Vector3.ProjectOnPlane(gun.transform.position - transform.position, transform.up).magnitude > _cfg.pickupReach * 1.6f
                || !Reachable(gun))
            {
                _grabbing = null;
                _grabAt = -1f;
                _loco.Reach = 0f;
                _loco.Hold = false;
                _thinkTimer = 0f;
                return;
            }
            _loco.LookAt = gun.transform.position;
            if (Time.time < _grabAt) return;
            _grabbing = null;
            _grabAt = -1f;
            // TryPickUp clears the reach, so the body straightens up with the gun at low ready.
            if (!TryPickUp(gun)) { _loco.Reach = 0f; _loco.Hold = false; _thinkTimer = 0f; }
        }

        Weapon NearestDroppedWeapon()
        {
            Weapon best = null;
            float bestSqr = _cfg.weaponSeekRange * _cfg.weaponSeekRange;
            var all = Weapon.All;
            for (int i = 0; i < all.Count; i++)
            {
                Weapon w = all[i];
                if (w == null || w.Hold != WeaponHold.Dropped || w == _givenUp || !Reachable(w)) continue;
                // Rare guns (one-hit, may blow up) are the player's find: enemies never pick them up.
                if (w.Data != null && (w.Data.oneHitKill || w.Data.backfireChance > 0f)) continue;
                // Give a freshly dropped gun a moment to land (and the player a moment to enjoy the disarm).
                if (Time.unscaledTime - w.DroppedAt < 0.4f) continue;
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
            bool wantsToShoot = hands > 0 && !KnockedDown && _seesPlayer && _aimedAtTarget && _loco.FacePoint.HasValue
                                && Time.time >= _reactAt && CombatWorld.PlayerPresent && !CombatWorld.EnemiesHoldFire;

            float now = Time.time;
            if (_flare.Raising) wantsToShoot = false;
            _triggerPressed = false;
            bool spinReady = SpinGate(wantsToShoot, now);
            bool covering = wantsToShoot && Covering(now);
            if (Ability == OrbitalAnte.Enemies.FactionAbility.SpinUp && wantsToShoot)
            {
                // The Heavy: nothing while it spins up, then the trigger held down through the whole burst.
                _telling = false;
                _triggerPressed = spinReady && !_triggerHeld;
                _triggerHeld = spinReady;
            }
            else if (!wantsToShoot)
            {
                _triggerHeld = false;
                _telling = false;
            }
            else if (_triggerHeld)
            {
                if (now >= _triggerUntil)
                {
                    _triggerHeld = false;
                    _nextTriggerAt = now + (covering ? Dials.suppress.pauseSeconds : Rand(_cfg.burstPause));
                }
            }
            else if (now >= _nextTriggerAt && !_telling && Squad != null
                     && !TakeSquadTurn(now, covering ? Dials.suppress.burstSeconds + 0.2f : _cfg.aimTell > 0f ? _cfg.aimTell + 0.4f : (Weapon.Data.automatic ? _cfg.burstSeconds.y : 0.5f)))
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
                _triggerUntil = now + (Weapon.Data.automatic ? (covering ? Dials.suppress.burstSeconds : Rand(_cfg.burstSeconds)) : 0.05f);
            }

            ShowTell(_telling);
            FireResult shot = Weapon.State.TryFire(hands, _triggerHeld, _triggerPressed);
            if (!shot.Fired) return;

            Vector3 muzzle = Weapon.Muzzle.position;
            Vector3 toTarget = PlayerAimPoint - muzzle;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = Weapon.Muzzle.forward;
            Vector3 direction = Ballistics.SpreadDirection(toTarget, (shot.SpreadDegrees + AimError) * (covering ? Dials.suppress.spreadScale : 1f), _rng);

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
            PoseFlareArm();
        }

        Vector3 LowReadyDirection()
        {
            bool prone = _owner.State == MovementState.Crawl || _owner.State == MovementState.Wriggle;
            // In the tactical hunch the gun is carried higher, just under the line of sight.
            float down = Mathf.Lerp(0.75f, 0.25f, _loco != null ? _loco.TacticalShown : 0f);
            Vector3 dir = transform.forward + (prone ? transform.up * 0.05f : (-transform.up) * down);
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
