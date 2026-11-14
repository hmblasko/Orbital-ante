using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The solar system flight test: builds the whole moving solar system from the run seed and puts the ship,
    /// with you strapped into its seat, on the launch pad on Earth (or in orbit, for tests and by choice). The counterpart of the combat test arena.
    /// </summary>
    public enum StartPlace
    {
        /// <summary>Whatever the LaunchConfig asset says (the pad, unless that is unticked).</summary>
        FromConfig,
        /// <summary>On the launch pad on Earth.</summary>
        Pad,
        /// <summary>In orbit round the starting body, as before Milestone 8.</summary>
        Orbit,
    }

    public sealed class SolarSystemWorld : MonoBehaviour
    {
        /// <summary>The launch base on Earth. Null until Earth's ground has been worked out.</summary>
        public LaunchBase Base { get; private set; }
        /// <summary>The main engines' fire, smoke, shake and roar.</summary>
        public LaunchFx Engines { get; private set; }
        public bool StartedOnPad { get; private set; }
        int _earth = -1;
        LaunchConfig _launchConfig;

        public SolarSystem System { get; private set; }
        /// <summary>The ship's flight controls (thrusters, autopilot, target lock).</summary>
        public TestCraft Craft { get; private set; }
        public Ship.Ship Ship { get; private set; }
        public Astronaut Astronaut { get; private set; }
        public ShipHud ShipHud { get; private set; }
        public OrbitalAnte.Spacewalk.SuitHud SuitHud { get; private set; }
        public OrbitalAnte.Cockpit.CockpitDash Dash { get; private set; }
        public FlightHud Hud { get; private set; }
        public SystemMap Map { get; private set; }
        public PlanetWorld Planets { get; private set; }
        public OrbitalAnte.Enemies.EnemySpawner Enemies { get; private set; }
        public OrbitalAnte.Bases.PlanetBases Bases { get; private set; }

        public void Build(int seed, StartPlace place = StartPlace.FromConfig)
        {
            SolarSystemData data = SolarSystemData.Load();
            TestCraftConfig config = TestCraftConfig.Load();

            var systemGo = new GameObject("SolarSystem");
            systemGo.transform.SetParent(transform, false);
            System = systemGo.AddComponent<SolarSystem>();
            PlanetData planets = PlanetData.Load();
            PlanetMaterials.Configure(planets.settings.simpleShaders);
            System.Build(data, seed, PlanetWorld.PaintedLater(planets));

            var planetsGo = new GameObject("Planets");
            planetsGo.transform.SetParent(transform, false);
            Planets = planetsGo.AddComponent<PlanetWorld>();
            Planets.Build(System, planets, seed);

            ShipConfig shipConfig = ShipConfig.Load();
            Ship = OrbitalAnte.Ship.Ship.Create(transform, shipConfig, config);
            Craft = Ship.Flight;
            Craft.System = System;
            Astronaut = Astronaut.Create(transform, Ship, shipConfig, System);
            Craft.Eye = Astronaut.Eye;
            System.Focus = Craft.Gravity;
            System.ViewCamera = Astronaut.Eye;

            _launchConfig = LaunchConfig.Load();
            Engines = LaunchFx.Attach(Ship, _launchConfig);
            _earth = System.Model.IndexOf("Earth");
            bool onPad = place == StartPlace.Pad || (place == StartPlace.FromConfig && _launchConfig.startOnPad);
            // The pad is on Earth. Its place depends on the ground, so Earth's ground is worked out first.
            if (onPad && _earth > 0 && Planets.TerrainNow(_earth) != null) BuildBase();
            StartedOnPad = onPad && Base != null;

            if (StartedOnPad)
            {
                PlaceOnPad();
                System.SnapToFocus();
                // Once more now the point of view has settled on the ground, so the ship starts dead still on it.
                PlaceOnPad();
            }
            else
            {
                int start = System.Model.IndexOf(config.startBody);
                if (start <= 0) start = _earth;
                if (start <= 0) start = 1;
                // Start on the sunlit side, so the first thing you see is a lit planet below you.
                Vec3d toSun = (System.States[0].Position - System.States[start].Position).Normalized;
                double altitude = Mathf.Max(20f, config.startAltitude);
                System.PlaceInOrbit(Craft.Gravity, start, altitude, toSun);
                System.SnapToFocus();
            }
            // The planet below gets its ground now; the rest are worked out in the background.
            Planets.Begin();

            var hudGo = new GameObject("FlightHudController");
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<FlightHud>();
            Hud.Build(System, Craft);

            var mapGo = new GameObject("SystemMapController");
            mapGo.transform.SetParent(transform, false);
            Map = mapGo.AddComponent<SystemMap>();
            Map.Build(System, Craft);
            // The map is drawn from what the dish hears, and you read it from the seat.
            Map.CanOpen = () => Ship.Belt.Buckled && Ship.Model.DishWorks && (Astronaut == null || !Astronaut.Dead);
            Map.WhyNot = () => !Ship.Belt.Buckled ? "The map is at the pilot's seat." : "No map: the dish is broken. Go outside, look up at it and mend it.";

            var shipHudGo = new GameObject("ShipHudController");
            shipHudGo.transform.SetParent(transform, false);
            ShipHud = shipHudGo.AddComponent<ShipHud>();
            ShipHud.Build(Ship, Astronaut, Hud, Map);
            SuitHud = shipHudGo.AddComponent<OrbitalAnte.Spacewalk.SuitHud>();
            SuitHud.Build(Astronaut, Astronaut.SuitDials);
            Bases = OrbitalAnte.Bases.PlanetBases.Build(this, null);
            Enemies = OrbitalAnte.Enemies.EnemySpawner.Build(this, OrbitalAnte.Enemies.EnemyConfig.Load());
            Dash = OrbitalAnte.Cockpit.CockpitDash.Create(this, OrbitalAnte.Cockpit.CockpitConfig.Load());
            // Works out the cabin's soft light now if no ready-made bake came with the build.
            Ship.BakeLightIfNeeded();
        }

        void BuildBase()
        {
            PlanetSurface surface = Planets.SurfaceOf(_earth);
            if (Base != null || surface == null || !surface.Ready) return;
            Base = LaunchBase.Create(surface, LaunchSite.Find(surface.Terrain), _launchConfig);
            Base.Engines = Engines;
        }

        /// <summary>Stands the ship on the pad, nose to the north, moving exactly as the ground under it moves.</summary>
        void PlaceOnPad()
        {
            BodyState earth = System.States[_earth];
            QuatD turn = System.Model.Orientation(_earth, earth);
            Vec3d offset = turn.Rotate(Base.ShipSpot);
            Vec3d spin = System.Model.SpinAxis(_earth) * System.Model.SpinRate(_earth);
            System.Place(Craft.Gravity, earth.Position + offset, earth.Velocity + Vec3d.Cross(spin, offset));

            Vector3 forward = SpaceConvert.ToVector3(System.Frame.ToLocalDirection(turn.Rotate(Base.Spot.North), System.States));
            Vector3 up = SpaceConvert.ToVector3(System.Frame.ToLocalDirection(turn.Rotate(Base.Spot.Up), System.States));
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Craft.transform.rotation = rotation;
            Craft.Body.rotation = rotation;
            Craft.Body.angularVelocity = SpaceConvert.ToVector3(System.Frame.ToLocalSpin(spin, System.States));
            if (Engines != null) Engines.MarkResting();
        }

        void Update()
        {
            // Started somewhere else: the base appears once Earth's ground has been worked out in the background.
            if (Base == null && _earth > 0 && Planets != null && Planets.SurfaceOf(_earth) != null && Planets.SurfaceOf(_earth).Ready) BuildBase();
        }

        /// <summary>One line for the debug readout (F3): which body the physics is anchored to and how.</summary>
        public string DescribeForDebug()
        {
            if (System == null || System.Model == null) return string.Empty;
            ReferenceFrame frame = System.Frame;
            return "Anchored to " + System.Model.Spec(frame.Anchor).displayName + (frame.Rotating ? " (turning with it)" : string.Empty) +
                   "   bodies " + System.Model.Count + "   recentres " + System.RecenterCount +
                   "   time " + System.SimTime.ToString("0") + " s" +
                   (Planets != null ? "\n" + Planets.DescribeForDebug() : string.Empty) +
                   (Ship != null ? "\n" + Ship.DescribeForDebug() : string.Empty) +
                   (Engines != null ? "   engines " + Engines.Model.State + " burn " + Mathf.RoundToInt(Engines.Model.Burn * 100f) + "%" : string.Empty) +
                   (Astronaut != null ? "   " + Astronaut.DescribeForDebug() : string.Empty);
        }
    }
}
