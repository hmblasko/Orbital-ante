using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    public enum CrewTransition { Exterior, LiftUp, Cabin, Piloting, LiftDown }

    /// <summary>One existing enemy, wherever it goes. This never creates a second body or a player controller.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EnemyShipCrew : MonoBehaviour
    {
        public Ship.Ship CurrentShip { get; private set; }
        public Ship.Ship HomeShip { get; private set; }
        public CrewTransition Transition { get; private set; }
        public bool Piloting => isActiveAndEnabled && Transition == CrewTransition.Piloting && CurrentShip != null && ReferenceEquals(CurrentShip.PilotOwner, this);
        public bool ProtectedFromCampSleep => CurrentShip != null;
        public bool FitsStarterCabin { get; private set; }
        public bool CanPilot => FitsStarterCabin && isActiveAndEnabled && _who != null && _who.Alive && _who.Model.UsableHands > 0
            && _who.Model.OnBody(BodyPartId.Head) && _who.Model.OnBody(BodyPartId.Torso);
        public bool Returning { get; private set; }

        SolarSystemWorld _world;
        EnemyConfig _config;
        Mannequin _who;
        MannequinLocomotion _loco;
        MannequinCombat _combat;
        Footing _exteriorFooting;
        readonly InteriorWalker _walker = new InteriorWalker();
        bool _leave;
        float _aboardAt, _outsideAt;
        Rigidbody _body;
        GravityBody _gravity;
        Collider[] _blockers;
        bool _falling;
        Weapon _stowedWeapon;

        public void Initialize(SolarSystemWorld world, EnemyConfig config, Mannequin who)
        {
            _world = world;
            _config = config;
            _who = who;
            _loco = who.GetComponent<MannequinLocomotion>();
            _combat = who.GetComponent<MannequinCombat>();
            _exteriorFooting = who.Footing;
            _body = who.GetComponent<Rigidbody>();
            _gravity = who.GetComponent<GravityBody>();
            _blockers = who.GetComponentsInChildren<Collider>();
            // Boarding uses the same person-sized lift/walker as the player. Oversized kinds retain their
            // ground AI rather than clipping a fixed-size lift or the curved roof after entering.
            FitsStarterCabin = false;
            for (int i = 0; i < _blockers.Length; i++)
            {
                if (!(_blockers[i] is CapsuleCollider capsule) || capsule.direction != 1
                    || capsule.gameObject.layer != Layers.CharacterBody) continue;
                Vector3 size = capsule.transform.lossyScale;
                FitsStarterCabin = EnemyShipRules.FitsCabin(capsule.height * Mathf.Abs(size.y),
                    capsule.radius * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)),
                    ShipLayout.PersonHeight, ShipLayout.PersonRadius);
                break;
            }
            who.Died += OnDied;
            who.StateChanged += OnStateChanged;
            Transition = CrewTransition.Exterior;
        }

        /// <summary>Initial encounter placement only. Subsequent transfers always go through the open lift.</summary>
        public bool StartAsPilot(Ship.Ship ship)
        {
            if (!CanPilot || CurrentShip != null || HomeShip != null || Transition != CrewTransition.Exterior
                || ship == null || !ship.TryClaimPilot(this, true)) return false;
            HomeShip = ship;
            EnterCabin(ship, new Vector3(0f, 0f, 0f));
            Transition = CrewTransition.Piloting;
            ship.Belt.StartBuckled();
            PosePilot();
            return true;
        }

        public void RequestDisembark() { _leave = true; Returning = false; }
        public void RequestReturn() { Returning = true; _leave = false; }

        void Update()
        {
            // A stowed weapon may have been disarmed while this component was not fighting.
            if (_stowedWeapon != null && (_combat == null || _stowedWeapon != _combat.Weapon)) RestoreStowedWeapon();
            if (_who == null || !_who.Alive || GameSession.Paused || Time.deltaTime <= 0f) return;
            if (CurrentShip == null && Transition != CrewTransition.Exterior) { LeaveDestroyedShip(); return; }
            if (Piloting)
            {
                if (!CanPilot || _leave)
                {
                    CurrentShip.ReleasePilot(this);
                    Transition = CrewTransition.Cabin;
                    _walker.Place(-0.8f, 1f);
                    SetArmed(true);
                }
                else { PosePilot(); return; }
            }
            switch (Transition)
            {
                case CrewTransition.Exterior: UpdateExterior(); break;
                case CrewTransition.Cabin: UpdateCabin(Time.deltaTime); break;
                case CrewTransition.LiftUp: UpdateLift(LiftRide.Up); break;
                case CrewTransition.LiftDown: UpdateLift(LiftRide.Down); break;
            }
        }

        void UpdateExterior()
        {
            if (!FitsStarterCabin) return;
            if (HomeShip != null && Time.time - _outsideAt >= Mathf.Max(2f, _config.crewOutsideSeconds)) Returning = true;
            if (Returning && HomeShip != null && !_falling && _loco != null
                && Vector3.Distance(transform.position, HomeShip.transform.position) <= _config.field.sleepRange)
            {
                HomeShip.Hatch.Set(true, HomeShip.Model.HatchWorks);
                _loco.FacePoint = null;
                _loco.Destination = HomeShip.transform.TransformPoint(new Vector3(0f, ShipLayout.GroundY, 0f));
                _loco.Hurry = true;
            }
            Ship.Ship best = null;
            float nearest = Mathf.Max(1f, _config.boardingSeekRange);
            var ships = Ship.Ship.All;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship.Ship ship = ships[i];
                if (ship == null || ship.Hatch == null) continue;
                // A crew may operate its own hatch; foreign ships, including the player's, must already be open.
                if (ship == HomeShip && Returning && !ship.Hatch.WantOpen) ship.Hatch.Set(true, ship.Model.HatchWorks);
                if (!ship.Hatch.Open || !ship.Hatch.WantOpen) continue;
                if (ship == HomeShip && !Returning && Time.time - _outsideAt < _config.crewOutsideSeconds) continue;
                Vector3 local = ship.transform.InverseTransformPoint(transform.position);
                float distance = new Vector2(local.x, local.z).magnitude;
                if (distance > nearest || local.y > ShipLayout.BellyY + 0.3f || local.y < ShipLayout.BellyY - ship.Hatch.Reach) continue;
                best = ship;
                nearest = distance;
            }
            if (best == null) return;
            Vector3 feet = best.transform.InverseTransformPoint(transform.position);
            if (best.Hatch.TryBeginUp(this, P(feet)))
            {
                EnterCabin(best, feet);
                Transition = CrewTransition.LiftUp;
                SetArmed(false);
            }
            else if (!_falling && _loco != null)
            {
                _loco.FacePoint = null;
                _loco.Destination = best.transform.TransformPoint(new Vector3(0f, feet.y, 0f));
                _loco.Hurry = true;
            }
        }

        void EnterCabin(Ship.Ship ship, Vector3 feet)
        {
            _exteriorFooting = _who.Footing;
            CurrentShip = ship;
            _falling = false;
            if (_gravity != null) _gravity.enabled = false;
            if (_body != null) { if (!_body.isKinematic) _body.linearVelocity = Vector3.zero; _body.isKinematic = true; }
            if (_loco != null) { _loco.enabled = false; _loco.Destination = _loco.FacePoint = null; }
            Blockers(false);
            transform.SetParent(ship.transform, true);
            transform.localPosition = feet;
            transform.localRotation = Quaternion.identity;
            _who.Footing = new CabinFooting(ship);
            _walker.Place(feet.x, feet.z);
            _aboardAt = Time.time;
        }

        void UpdateLift(LiftRide ride)
        {
            Ship.Ship ship = CurrentShip;
            transform.localPosition = V(ship.Hatch.Rider);
            _who.Velocity = PointVelocity(ship, transform.localPosition);
            bool finished = ship.Hatch.FinishedFor(this, ride);
            if (!finished)
            {
                // A ship being disabled or an explicit cancellation can release the reservation first.
                // Leave at the last actual lift point rather than following an unowned ride forever.
                if (!ReferenceEquals(ship.Hatch.RiderActor, this)) ExitToWorld(_who.Velocity);
                return;
            }
            if (ride == LiftRide.Up)
            {
                ship.Hatch.Release(this);
                Transition = CrewTransition.Cabin;
                _walker.Place(0f, 0f);
                _aboardAt = Time.time;
                _leave = false;
                Returning = false;
                SetArmed(true);
            }
            else ExitToWorld(PointVelocity(ship, transform.localPosition));
        }

        void UpdateCabin(float dt)
        {
            Ship.Ship ship = CurrentShip;
            // A foreign occupant can leave again through a hatch the owner has left open.
            if (ship != HomeShip && Time.time - _aboardAt > Mathf.Max(2f, _config.crewCabinSeconds)) _leave = true;
            if (_leave && ship == HomeShip && !ship.Hatch.WantOpen) ship.Hatch.Set(true, ship.Model.HatchWorks);
            Vector3 target = _leave ? Vector3.zero : new Vector3(-0.8f, 0f, 1f);
            if (!_leave && ship == HomeShip && CanPilot && ship.PilotOwner == null && !ship.Belt.Seated
                && Vector2.Distance(new Vector2(_walker.X, _walker.Z), new Vector2(0f, 1.2f)) < 1.5f
                && ship.TryClaimPilot(this, true))
            {
                Transition = CrewTransition.Piloting;
                ship.Belt.Begin();
                SetArmed(false);
                PosePilot();
                return;
            }
            if (!_leave && _world.Astronaut != null && _world.Astronaut.Ship == ship && _world.Astronaut.Mode == AstronautMode.Aboard)
            {
                Vector3 player = ship.transform.InverseTransformPoint(_world.Astronaut.transform.position);
                target = player + new Vector3(-0.9f, 0f, 0f);
            }
            Vector2 delta = new Vector2(target.x - _walker.X, target.z - _walker.Z);
            Vector2 wish = delta.magnitude > 0.15f ? delta.normalized : Vector2.zero;
            _walker.WalkSpeed = (_loco != null ? _loco.SpeedFor(_who.State, false) : _who.Config.walkSpeed) * 0.65f;
            _walker.Step(dt, wish.x, wish.y, false, _leave && ship.Hatch.Open && ship.Hatch.WantOpen);
            transform.localPosition = new Vector3(_walker.X, _walker.Y, _walker.Z);
            Vector3 facing = _loco != null && _loco.FacePoint.HasValue && !_leave
                ? ship.transform.InverseTransformDirection(_loco.FacePoint.Value - transform.position)
                : new Vector3(wish.x, 0f, wish.y);
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f) transform.localRotation = Quaternion.RotateTowards(transform.localRotation,
                Quaternion.LookRotation(facing), _who.Config.turnSpeed * dt);
            _who.Velocity = PointVelocity(ship, transform.localPosition) + ship.Body.rotation * new Vector3(_walker.VelocityX, 0f, _walker.VelocityZ);
            if (!_leave || !_walker.OverOpenWell) return;
            float bottom = ship.FindLiftBottom();
            if (float.IsNaN(bottom)) bottom = ShipLayout.BellyY - ShipLayout.PersonHeight - 0.35f;
            if (ship.Hatch.TryBeginDown(this, P(transform.localPosition), bottom)) { Transition = CrewTransition.LiftDown; SetArmed(false); }
        }

        void PosePilot()
        {
            transform.localPosition = new Vector3(0f, ShipLayout.SeatedEye.Y - ShipLayout.EyeHeight, ShipLayout.SeatedEye.Z);
            transform.localRotation = Quaternion.identity;
            _who.Velocity = PointVelocity(CurrentShip, transform.localPosition);
            SetArmed(false);
        }

        void ExitToWorld(Vector3 velocity)
        {
            Ship.Ship previous = CurrentShip;
            Vector3 position = previous != null ? previous.Body.position + previous.Body.rotation * transform.localPosition : transform.position;
            Quaternion rotation = previous != null ? previous.Body.rotation * transform.localRotation : transform.rotation;
            int neighbourhood = previous != null ? previous.Gravity.Neighbourhood : 0;
            if (previous != null) { previous.Hatch.Release(this); previous.ReleasePilot(this); }
            transform.SetParent(_world.transform, true);
            CurrentShip = null;
            Transition = CrewTransition.Exterior;
            _outsideAt = Time.time;
            _leave = false;
            _who.Footing = _exteriorFooting;
            if (_body == null)
            {
                _body = gameObject.AddComponent<Rigidbody>();
            }
            _body.mass = 100f;
            _body.useGravity = false;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (_gravity == null) _gravity = gameObject.AddComponent<GravityBody>();
            // Four solid-ground reservations belong to the player's ship/suit and the two NPC ships.
            // Exterior crew settle from the real height below, then use PlanetFooting's analytic fallback.
            _gravity.NeedsGround = false;
            _gravity.SunkBelow = -0.7f;
            _gravity.PutBackAt = 0.2f;
            _gravity.Neighbourhood = neighbourhood;
            _body.isKinematic = false;
            _body.position = position;
            _body.rotation = rotation;
            _body.linearVelocity = velocity;
            _who.Velocity = velocity;
            _gravity.enabled = true;
            _falling = true;
            Blockers(_who.Alive);
            SetArmed(true);
        }

        void FixedUpdate()
        {
            if (!_falling || _who == null || !_who.Alive || _world.System == null || _world.Planets == null) return;
            int body = _gravity.Neighbourhood;
            PlanetSurface surface = _world.Planets.SurfaceOf(body);
            if (surface == null || !surface.Ready) return;
            Rigidbody planet = surface.Body.Body;
            Vector3 local = Quaternion.Inverse(planet.rotation) * (_body.position - planet.position);
            double radius = surface.GroundRadius(SpaceConvert.ToVec3d(local).Normalized);
            Vector3 up = planet.rotation * local.normalized;
            if (local.magnitude - radius > 0.6f || Vector3.Dot(_body.linearVelocity - _world.System.GroundVelocityAt(body, _body.position), up) > 1f) return;
            _body.isKinematic = true;
            _gravity.enabled = false;
            _falling = false;
            transform.SetParent(surface.Body.transform, true);
            transform.localPosition = local.normalized * (float)radius;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
            if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward, up);
            _who.Footing = _exteriorFooting = new PlanetFooting(surface.Body.transform, surface, local.normalized * (float)radius, _config.field.roam);
            if (_loco != null) { _loco.enabled = true; _loco.Wander = true; _loco.Destination = transform.position + transform.right * 2f; }
        }

        void LeaveDestroyedShip()
        {
            CurrentShip = null;
            ExitToWorld(_who.Velocity);
        }

        void SetArmed(bool armed)
        {
            if (_combat == null) return;
            _combat.enabled = armed;
            if (armed) RestoreStowedWeapon();
            else if (_combat.Weapon != null)
            {
                _stowedWeapon = _combat.Weapon;
                _stowedWeapon.gameObject.SetActive(false);
            }
        }

        void RestoreStowedWeapon()
        {
            if (_stowedWeapon != null) _stowedWeapon.gameObject.SetActive(true);
            _stowedWeapon = null;
        }

        void Blockers(bool enabled)
        {
            if (_blockers == null) return;
            for (int i = 0; i < _blockers.Length; i++) if (_blockers[i] != null && _blockers[i].gameObject.layer == Layers.CharacterBody) _blockers[i].enabled = enabled;
        }

        void OnStateChanged(Mannequin who, MovementState state)
        {
            if (Piloting && !CanPilot) { CurrentShip.ReleasePilot(this); Transition = CrewTransition.Cabin; _walker.Place(-0.8f, 1f); SetArmed(true); }
        }

        void OnDied(Mannequin who)
        {
            if (CurrentShip != null) CurrentShip.ReleasePilot(this);
            SetArmed(true);
            if (enabled) enabled = false;
            else if (CurrentShip != null) CurrentShip.Hatch.Release(this);
        }

        void OnDisable()
        {
            if (CurrentShip != null)
            {
                Ship.Ship ship = CurrentShip;
                bool completedUp = Transition == CrewTransition.LiftUp && ship.Hatch.FinishedFor(this, LiftRide.Up);
                if ((Transition == CrewTransition.LiftUp && !completedUp) || Transition == CrewTransition.LiftDown)
                {
                    // Cancel at the real rider point. An unfinished ascent has not crossed the hatch yet.
                    transform.localPosition = V(ship.Hatch.Rider);
                    ExitToWorld(PointVelocity(ship, transform.localPosition));
                }
                else
                {
                    ship.ReleasePilot(this);
                    ship.Hatch.Release(this);
                    if (Transition == CrewTransition.Piloting || completedUp)
                    {
                        Transition = CrewTransition.Cabin;
                        _walker.Place(-0.8f, 1f);
                        transform.localPosition = new Vector3(_walker.X, 0f, _walker.Z);
                    }
                }
            }
            RestoreStowedWeapon();
        }

        void OnEnable()
        {
            if (_who != null && _who.Alive && Transition == CrewTransition.Cabin) SetArmed(true);
        }

        void OnDestroy()
        {
            RestoreStowedWeapon();
            if (_who != null) { _who.Died -= OnDied; _who.StateChanged -= OnStateChanged; }
            if (CurrentShip != null) { CurrentShip.ReleasePilot(this); CurrentShip.Hatch.Forget(this); }
            if (HomeShip != null && HomeShip != CurrentShip) HomeShip.Hatch.Forget(this);
        }

        static P3 P(Vector3 value) => new P3(value.x, value.y, value.z);
        static Vector3 V(P3 value) => new Vector3(value.X, value.Y, value.Z);
        static Vector3 PointVelocity(Ship.Ship ship, Vector3 local) => ship.Body.GetPointVelocity(ship.Body.position + ship.Body.rotation * local);

        sealed class CabinFooting : Footing
        {
            readonly Ship.Ship _ship;
            public CabinFooting(Ship.Ship ship) { _ship = ship; }
            public override bool IsFlat => false;
            public override Vector3 Up(Vector3 at) => _ship != null ? _ship.transform.up : Vector3.up;
            public override Vector3 Hold(Vector3 point) => _ship.transform.InverseTransformPoint(point);
            public override Vector3 Recall(Vector3 point) => _ship.transform.TransformPoint(point);
        }
    }
}
