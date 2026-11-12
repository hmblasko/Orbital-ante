using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The solar system flight test: builds the whole moving solar system from the run seed and puts the test
    /// craft in orbit round its starting planet. The counterpart of the combat test arena.
    /// </summary>
    public sealed class SolarSystemWorld : MonoBehaviour
    {
        public SolarSystem System { get; private set; }
        public TestCraft Craft { get; private set; }
        public FlightHud Hud { get; private set; }
        public SystemMap Map { get; private set; }
        public PlanetWorld Planets { get; private set; }

        public void Build(int seed)
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

            Craft = TestCraft.Create(transform, config);
            Craft.System = System;
            System.Focus = Craft.Gravity;
            System.ViewCamera = Craft.Eye;

            int start = System.Model.IndexOf(config.startBody);
            if (start <= 0) start = System.Model.IndexOf("Earth");
            if (start <= 0) start = 1;
            // Start on the sunlit side, so the first thing you see is a lit planet below you.
            Vec3d toSun = (System.States[0].Position - System.States[start].Position).Normalized;
            double altitude = Mathf.Max(20f, config.startAltitude);
            System.PlaceInOrbit(Craft.Gravity, start, altitude, toSun);
            System.SnapToFocus();
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
        }

        /// <summary>One line for the debug readout (F3): which body the physics is anchored to and how.</summary>
        public string DescribeForDebug()
        {
            if (System == null || System.Model == null) return string.Empty;
            ReferenceFrame frame = System.Frame;
            return "Anchored to " + System.Model.Spec(frame.Anchor).displayName + (frame.Rotating ? " (turning with it)" : string.Empty) +
                   "   bodies " + System.Model.Count + "   recentres " + System.RecenterCount +
                   "   time " + System.SimTime.ToString("0") + " s" +
                   (Planets != null ? "\n" + Planets.DescribeForDebug() : string.Empty);
        }
    }
}
