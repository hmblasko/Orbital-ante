using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Navigation;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>The orbit readout: what a circle needs here, and where the path you are coasting on goes.</summary>
    public class OrbitPathTests
    {
        static SolarSystemModel Build() => new SolarSystemModel(SolarSystemDefaults.Bodies(), 1);

        [Test]
        public void AtCircleSpeed_ItIsAnOrbit_WithLowAndHighPointsAtYourHeight()
        {
            SolarSystemModel model = Build();
            foreach (string id in new[] { "Earth", "Moon", "Mars", "Jupiter", "Phobos" })
            {
                int body = model.IndexOf(id);
                double r = model.Spec(body).radius;
                double d = r * 1.6;
                double v = model.CircularOrbitSpeed(body, d);
                OrbitReading reading = OrbitPath.Read(model, body, new Vec3d(d, 0, 0), new Vec3d(0, 0, v));
                Assert.AreEqual(OrbitKind.Orbit, reading.Kind, id);
                Assert.AreEqual(v, reading.CircleSpeed, 1e-9, id);
                Assert.AreEqual(v, reading.SideSpeed, 1e-9, id);
                Assert.AreEqual(d - r, reading.LowHeight, (d - r) * 0.02, id + " low point");
                Assert.AreEqual(d - r, reading.HighHeight, (d - r) * 0.02, id + " high point");
            }
        }

        [Test]
        public void TooSlowSideways_ThePathMeetsTheGround_AndFarTooFast_ItLeaves()
        {
            SolarSystemModel model = Build();
            int earth = model.IndexOf("Earth");
            double d = 900;
            double v = model.CircularOrbitSpeed(earth, d);
            Assert.AreEqual(OrbitKind.MeetsGround, OrbitPath.Read(model, earth, new Vec3d(d, 0, 0), new Vec3d(0, 0, v * 0.4)).Kind);
            Assert.AreEqual(OrbitKind.Leaves, OrbitPath.Read(model, earth, new Vec3d(d, 0, 0), new Vec3d(0, 0, v * 2.5)).Kind);
            Assert.AreEqual(OrbitKind.MeetsGround, OrbitPath.Read(model, earth, new Vec3d(d, 0, 0), new Vec3d(-20, 0, 0)).Kind, "falling straight down");
            Assert.AreEqual(OrbitKind.None, OrbitPath.Read(model, 0, new Vec3d(d, 0, 0), new Vec3d(0, 0, v)).Kind, "nothing to say about the Sun");
        }

        [Test]
        public void TheLowAndHighPoints_AreWhereTheShipReallyGoes()
        {
            // Let go a little faster than a circle, and climbing a little, and fly it the way the game does.
            SolarSystemModel model = Build();
            BodyState[] states = model.NewStateBuffer();
            foreach (string id in new[] { "Earth", "Mars", "Saturn", "Titan" })
            {
                int body = model.IndexOf(id);
                double r = model.Spec(body).radius;
                double d0 = r * 1.5;
                double v = model.CircularOrbitSpeed(body, d0);
                Vec3d offset = new Vec3d(0, 0, d0);
                Vec3d velocity = new Vec3d(v * 1.08, v * 0.05, 0);
                OrbitReading reading = OrbitPath.Read(model, body, offset, velocity);
                Assert.AreEqual(OrbitKind.Orbit, reading.Kind, id);

                double t = 0, dt = 0.02;
                model.Evaluate(t, states);
                Vec3d p = states[body].Position + offset;
                Vec3d u = states[body].Velocity + velocity;
                double low = double.MaxValue, high = 0;
                for (int k = 0; k < (int)(2.5 * 2 * Math.PI * d0 / v / dt); k++)
                {
                    u += model.AccelerationAt(p, states, body) * dt;
                    p += u * dt;
                    t += dt;
                    model.Evaluate(t, states);
                    double d = Vec3d.Distance(p, states[body].Position);
                    low = Math.Min(low, d);
                    high = Math.Max(high, d);
                }
                // The world's moons tug the real path a little, which the readout leaves out.
                Assert.AreEqual(low - r, reading.LowHeight, r * 0.03, id + " low point");
                Assert.AreEqual(high - r, reading.HighHeight, r * 0.03, id + " high point");
                Assert.Greater(reading.HighHeight, d0 - r, id + ": faster than a circle climbs");
            }
        }
    }
}
