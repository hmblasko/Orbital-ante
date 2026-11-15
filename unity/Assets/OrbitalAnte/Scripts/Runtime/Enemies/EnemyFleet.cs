using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>Bounded reuse of starter landers. Pilots are drawn from the spawner's existing population.</summary>
    public sealed class EnemyFleet : MonoBehaviour
    {
        readonly List<EnemyShipFlight> _ships = new List<EnemyShipFlight>();
        SolarSystemWorld _world;
        EnemyConfig _config;
        public int ShipCount => _ships.Count;
        public IReadOnlyList<EnemyShipFlight> Ships => _ships;

        public static EnemyFleet Build(SolarSystemWorld world, EnemyConfig config, Transform parent)
        {
            var go = new GameObject("Enemy starter ships");
            go.transform.SetParent(parent, false);
            var fleet = go.AddComponent<EnemyFleet>();
            fleet._world = world;
            fleet._config = config;
            return fleet;
        }

        public EnemyShipCrew Register(Mannequin who, PlanetSurface surface, int camp, int member)
        {
            var crew = who.gameObject.AddComponent<EnemyShipCrew>();
            crew.Initialize(_world, _config, who);
            // No extra actors, factions, ship models or mounted guns are introduced by the flight test.
            if (!_config.enemyShips || _ships.Count >= Mathf.Clamp(_config.shipCap, 0, 2) || !crew.CanPilot
                || who.transform.lossyScale.y > 1.05f || surface == null || !surface.Ready || !surface.GroundShown) return crew;
            Vector3 local = surface.Body.transform.InverseTransformPoint(who.transform.position);
            var random = GameSession.Stream("enemy.ships." + surface.Body.Spec.id + "." + camp + "." + member);
            Vec3d offset = EnemyShipRules.SeededOffset(random, SpaceConvert.ToVec3d(local).Normalized, Mathf.Max(18f, _config.field.roam * 1.5f));
            Vec3d direction = (SpaceConvert.ToVec3d(local) + offset).Normalized;
            double ground = surface.GroundRadius(direction);
            // Camps already exclude the launch area; also keep the complete ship's footprint clear of it.
            if (_world.Base != null && _world.Base.Surface == surface
                && EnemyField.GroundDistance(direction, _world.Base.Spot.Up, ground) < _config.field.padClear) return crew;
            Vector3 landing = SpaceConvert.ToVector3(direction * ground);
            Vector3 up = surface.Body.transform.TransformDirection(SpaceConvert.ToVector3(direction));
            Vector3 tangent = surface.Body.transform.TransformDirection(SpaceConvert.ToVector3(offset));
            tangent = Vector3.ProjectOnPlane(tangent, up).normalized;
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(up, Vector3.right).normalized;
            Ship.Ship ship = Ship.Ship.Create(_world.transform, ShipConfig.Load(), TestCraftConfig.Load());
            ship.name = "Enemy starter ship " + (_ships.Count + 1);
            ship.SetNpcShip();
            ship.Flight.System = _world.System;
            ship.Gravity.Neighbourhood = surface.Body.Index;
            Vector3 position = surface.Body.transform.TransformPoint(landing) + up * (-ShipLayout.GroundY + Mathf.Max(8f, _config.shipFlight.approachHeight)) - tangent * 20f;
            ship.Body.position = position;
            ship.transform.SetPositionAndRotation(position, Quaternion.LookRotation(tangent, up));
            ship.Body.rotation = ship.transform.rotation;
            ship.Body.linearVelocity = _world.System.GroundVelocityAt(surface.Body.Index, position);
            if (!crew.StartAsPilot(ship)) { Destroy(ship.gameObject); return crew; }
            var flight = ship.gameObject.AddComponent<EnemyShipFlight>();
            flight.Initialize(_world, _config, surface, crew, direction, random.Next());
            _ships.Add(flight);
            return crew;
        }

        void Update()
        {
            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                EnemyShipFlight flight = _ships[i];
                if (flight == null) { _ships.RemoveAt(i); continue; }
                // Never unload a player/boarder or a transfer. An unoccupied derelict can be retired far away.
                if (flight.Pilot != null && flight.Pilot.CurrentShip != null) continue;
                if (_world.Astronaut == null || Vector3.Distance(flight.transform.position, _world.Astronaut.transform.position) <= _config.field.sleepRange * 2f) continue;
                bool occupied = false;
                if (_world.Astronaut.Ship == flight.Ship && _world.Astronaut.Mode != AstronautMode.Outside) occupied = true;
                foreach (EnemyShipCrew crew in flight.GetComponentsInChildren<EnemyShipCrew>()) if (crew != null) occupied = true;
                if (occupied || flight.Ship.Hatch.Ride != LiftRide.None) continue;
                Destroy(flight.gameObject);
                _ships.RemoveAt(i);
            }
        }
    }

    /// <summary>Produces bounded physical commands; TestCraft applies the same damage/fuel limits as player flight.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class EnemyShipFlight : MonoBehaviour
    {
        public Ship.Ship Ship { get; private set; }
        public EnemyShipCrew Pilot { get; private set; }
        public EnemyShipStage Stage { get; private set; }
        SolarSystemWorld _world;
        EnemyConfig _config;
        PlanetSurface _surface;
        Vec3d _landingDirection;
        SeededRandom _random;
        float _settled, _stageAt;
        float _lastGroundTouch = float.NegativeInfinity;
        readonly RaycastHit[] _obstacles = new RaycastHit[12];

        public void Initialize(SolarSystemWorld world, EnemyConfig config, PlanetSurface surface, EnemyShipCrew pilot, Vec3d landingDirection, int seed)
        {
            _world = world; _config = config; _surface = surface; Pilot = pilot;
            Ship = GetComponent<Ship.Ship>();
            _landingDirection = landingDirection;
            _random = new SeededRandom(seed);
            ChangeStage(EnemyShipStage.Approach);
        }

        void ChangeStage(EnemyShipStage stage) { Stage = stage; _stageAt = Time.time; _settled = 0f; }

        void FixedUpdate()
        {
            if (Ship == null || _world == null || _surface == null || !_surface.Ready || GameSession.Paused) return;
            // These ships have no player-only launch effects to report contact for them.
            Ship.Flight.Grounded = Time.fixedTime - _lastGroundTouch <= Time.fixedDeltaTime * 2.5f;
            Vector3 groundRelative = Ship.Body.linearVelocity - _world.System.GroundVelocityAt(_surface.Body.Index, Ship.Body.position);
            Ship.Flight.Landed = Ship.Flight.Grounded && groundRelative.sqrMagnitude <= _config.shipFlight.settleSpeed * _config.shipFlight.settleSpeed
                && Ship.Body.angularVelocity.sqrMagnitude < 0.04f;
            // A player or another owner may take the seat. This adapter has no authority over their input.
            if (!Ship.Flight.UseExternalControl || (Ship.PilotOwner != null && !ReferenceEquals(Ship.PilotOwner, Pilot))) return;
            bool pilotPresent = Pilot != null && Pilot.Piloting && Pilot.CurrentShip == Ship && Pilot.CanPilot;
            if (!pilotPresent) Ship.Flight.ClearExternalCommand();
            if (Stage == EnemyShipStage.Disembark)
            {
                if (Pilot == null || !Pilot.CanPilot) return;
                if (Pilot.CurrentShip == null && Time.time - _stageAt >= Mathf.Max(2f, _config.crewOutsideSeconds))
                { Pilot.RequestReturn(); ChangeStage(EnemyShipStage.Reboard); }
                return;
            }
            if (Stage == EnemyShipStage.Reboard)
            {
                if (!pilotPresent) return;
                Vec3d offset = EnemyShipRules.SeededOffset(_random, _landingDirection, Mathf.Max(20f, _config.field.roam * 2f));
                _landingDirection = (_landingDirection * _surface.GroundRadius(_landingDirection) + offset).Normalized;
                Ship.Hatch.Set(false, Ship.Model.HatchWorks);
                ChangeStage(EnemyShipStage.Approach);
            }
            if (!pilotPresent || !Ship.Belt.Buckled) return;
            Rigidbody planet = _surface.Body.Body;
            Quaternion planetRotation = planet.rotation;
            Vector3 planetPosition = planet.position;
            Vector3 local = Quaternion.Inverse(planetRotation) * (Ship.Body.position - planetPosition);
            Vec3d here = SpaceConvert.ToVec3d(local).Normalized;
            double currentGround = _surface.GroundRadius(here);
            Vector3 groundPoint = planetPosition + planetRotation * SpaceConvert.ToVector3(here * currentGround);
            Vector3 up = (Ship.Body.position - planetPosition).normalized;
            Vector3 landing = planetPosition + planetRotation * SpaceConvert.ToVector3(_landingDirection * _surface.GroundRadius(_landingDirection));
            Vector3 landingUp = planetRotation * SpaceConvert.ToVector3(_landingDirection);
            EnemyShipTuning tuning = _config.shipFlight;
            float altitude = -ShipLayout.GroundY + (Stage == EnemyShipStage.Approach ? Mathf.Max(8f, tuning.approachHeight) : Stage == EnemyShipStage.Hover ? Mathf.Max(3f, tuning.hoverHeight) : 0f);
            Vector3 target = landing + landingUp * altitude;
            Vector3 toward = target - Ship.Body.position;
            if (Stage != EnemyShipStage.Land && toward.magnitude > 1f)
            {
                int hits = Physics.SphereCastNonAlloc(Ship.Body.position, ShipLayout.FootpadDistance * 0.6f, toward.normalized,
                    _obstacles, Mathf.Min(toward.magnitude, 20f), Layers.WorldMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                {
                    if (_obstacles[i].collider == null || _obstacles[i].collider.GetComponentInParent<Ship.Ship>() == Ship) continue;
                    target += up * Mathf.Max(8f, tuning.hoverHeight);
                    break;
                }
            }
            Vector3 groundVelocity = _world.System.GroundVelocityAt(_surface.Body.Index, Ship.Body.position);
            var sample = new EnemyShipSample
            {
                Position = SpaceConvert.ToVec3d(Ship.Body.position), Velocity = SpaceConvert.ToVec3d(Ship.Body.linearVelocity),
                TargetPoint = SpaceConvert.ToVec3d(target), TargetVelocity = SpaceConvert.ToVec3d(groundVelocity),
                GroundPoint = SpaceConvert.ToVec3d(groundPoint), Up = SpaceConvert.ToVec3d(up),
                Gravity = SpaceConvert.ToVec3d(Ship.Gravity.FeltGravity), Forward = SpaceConvert.ToVec3d(Ship.Body.rotation * Vector3.forward),
                Grounded = Ship.Flight.Grounded, HatchOpen = Ship.Hatch.Open, PilotPresent = pilotPresent, CrewAboard = Pilot.CurrentShip == Ship,
            };
            EnemyShipOrder order = EnemyShipRules.Guide(Stage, sample, tuning);
            if (!order.Valid) { Ship.Flight.ClearExternalCommand(); return; }
            Vector3 forward = SpaceConvert.ToVector3(order.Forward);
            Vector3 guideUp = SpaceConvert.ToVector3(order.Up);
            if (Vector3.Cross(forward, guideUp).sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(Ship.Body.rotation * Vector3.forward, guideUp);
            Ship.Flight.SetExternalCommand(SpaceConvert.ToVector3(order.Acceleration), Quaternion.LookRotation(forward, guideUp));
            _settled = order.Arrived ? _settled + Time.fixedDeltaTime : 0f;
            if (_settled < Mathf.Max(0.2f, tuning.settleSeconds)) return;
            if (Stage == EnemyShipStage.Approach) ChangeStage(EnemyShipStage.Hover);
            else if (Stage == EnemyShipStage.Hover && Time.time - _stageAt >= Mathf.Max(1f, _config.shipHoverSeconds)) ChangeStage(EnemyShipStage.Land);
            else if (Stage == EnemyShipStage.Land)
            {
                Ship.Flight.ClearExternalCommand();
                Ship.Hatch.Set(true, Ship.Model.HatchWorks);
                Pilot.RequestDisembark();
                ChangeStage(EnemyShipStage.Disembark);
            }
        }

        void OnCollisionEnter(Collision collision) => RememberGroundContact(collision);
        void OnCollisionStay(Collision collision) => RememberGroundContact(collision);

        void RememberGroundContact(Collision collision)
        {
            if (Ship == null || collision.contactCount == 0 || (collision.rigidbody != null && !collision.rigidbody.isKinematic)
                || collision.collider.GetComponentInParent<Mannequin>() != null) return;
            for (int i = 0; i < collision.contactCount; i++)
                if (Vector3.Dot(collision.GetContact(i).normal, Ship.Body.rotation * Vector3.up) >= 0.5f)
                { _lastGroundTouch = Time.fixedTime; return; }
        }

        void Update()
        {
            if (Ship == null || Ship.Supplies == null || Ship.Crew != null || GameSession.Paused) return;
            int broken = 0;
            for (int i = 0; i < Ship.Model.Count; i++)
                if (Ship.Model.Parts[i].Kind == PartKind.Window && (Ship.Model.State(i) == PartState.Broken || Ship.Model.State(i) == PartState.Lost)) broken++;
            Ship.Supplies.Tick(Time.deltaTime, Ship.Flight.ThrustInput.magnitude, false, false, broken, Ship.Flight.Landed);
            Ship.FuelGate = Ship.Supplies.PushFactor;
        }
    }
}
