using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The rules of the solar system: orbits, spin, gravity, seeded starting positions, and which body's
    /// neighbourhood a point is in. These run without Unity objects.
    /// </summary>
    public class SolarSystemModelTests
    {
        static SolarSystemModel Build(int seed = 1)
        {
            return new SolarSystemModel(SolarSystemDefaults.Bodies(), seed);
        }

        static BodyState[] At(SolarSystemModel model, double time)
        {
            BodyState[] states = model.NewStateBuffer();
            model.Evaluate(time, states);
            return states;
        }

        [Test]
        public void TheSunIsFirstAndStaysPut()
        {
            SolarSystemModel model = Build();
            Assert.AreEqual("Sun", model.Spec(0).id);
            Assert.AreEqual(-1, model.ParentOf(0));
            BodyState[] later = At(model, 5000);
            Assert.AreEqual(0, later[0].Position.Magnitude, 1e-9);
        }

        [Test]
        public void HasEightPlanetsInTheRealOrder()
        {
            SolarSystemModel model = Build();
            string[] order = { "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };
            double last = 0;
            foreach (string id in order)
            {
                int i = model.IndexOf(id);
                Assert.GreaterOrEqual(i, 0, id + " is missing.");
                Assert.AreEqual(0, model.ParentOf(i), id + " should orbit the Sun.");
                Assert.Greater(model.Spec(i).orbitRadius, last, id + " is out of order.");
                last = model.Spec(i).orbitRadius;
            }
            Assert.AreEqual(model.IndexOf("Earth"), model.ParentOf(model.IndexOf("Moon")));
            Assert.AreEqual(-1, model.IndexOf("Planet X"));
        }

        [Test]
        public void BodiesStayOnTheirOrbits()
        {
            SolarSystemModel model = Build();
            foreach (double time in new[] { 0.0, 37.5, 900.0, 12345.6 })
            {
                BodyState[] states = At(model, time);
                for (int i = 1; i < model.Count; i++)
                {
                    double distance = Vec3d.Distance(states[i].Position, states[model.ParentOf(i)].Position);
                    Assert.AreEqual(model.Spec(i).orbitRadius, distance, 1e-6, model.Spec(i).id);
                }
            }
        }

        [Test]
        public void EverythingKeepsMoving()
        {
            SolarSystemModel model = Build();
            BodyState[] a = At(model, 0);
            BodyState[] b = At(model, 10);
            for (int i = 1; i < model.Count; i++)
            {
                Assert.Greater(Vec3d.Distance(a[i].Position, b[i].Position), 1.0, model.Spec(i).id + " did not move.");
                Assert.AreNotEqual(a[i].SpinAngle, b[i].SpinAngle, model.Spec(i).id + " did not turn.");
            }
        }

        [Test]
        public void AFullOrbitReturnsToTheStart()
        {
            SolarSystemModel model = Build();
            int earth = model.IndexOf("Earth");
            double period = model.OrbitPeriod(earth);
            Assert.Greater(period, 300, "An Earth year should take minutes, not seconds.");
            Assert.Less(period, 1800);
            BodyState[] start = At(model, 100);
            BodyState[] half = At(model, 100 + period / 2);
            BodyState[] full = At(model, 100 + period);
            Assert.AreEqual(0, Vec3d.Distance(start[earth].Position, full[earth].Position), 1e-4);
            Assert.AreEqual(2 * model.Spec(earth).orbitRadius, Vec3d.Distance(start[earth].Position, half[earth].Position), 1e-4);
        }

        [Test]
        public void VelocityAndAccelerationMatchTheMotion()
        {
            SolarSystemModel model = Build(7);
            const double t = 432.1;
            const double h = 1e-3;
            BodyState[] before = At(model, t - h);
            BodyState[] now = At(model, t);
            BodyState[] after = At(model, t + h);
            for (int i = 0; i < model.Count; i++)
            {
                Vec3d velocity = (after[i].Position - before[i].Position) / (2 * h);
                Vec3d acceleration = (after[i].Velocity - before[i].Velocity) / (2 * h);
                Assert.AreEqual(0, Vec3d.Distance(velocity, now[i].Velocity), 1e-4, model.Spec(i).id + " velocity");
                Assert.AreEqual(0, Vec3d.Distance(acceleration, now[i].Acceleration), 1e-4, model.Spec(i).id + " acceleration");
            }
        }

        [Test]
        public void OrbitSpeedComesFromTheParentsGravity()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 250);
            for (int i = 1; i < model.Count; i++)
            {
                int parent = model.ParentOf(i);
                // The parent's pull at the body is exactly the turn-in it needs to stay on its circle.
                Vec3d pull = model.GravityFrom(parent, states[i].Position, states);
                Vec3d turnIn = states[i].Acceleration - states[parent].Acceleration;
                Assert.AreEqual(0, Vec3d.Distance(pull, turnIn), 1e-6 * Math.Max(1, pull.Magnitude), model.Spec(i).id);
            }
        }

        [Test]
        public void InnerPlanetsOrbitFaster()
        {
            SolarSystemModel model = Build();
            Assert.Less(model.OrbitPeriod(model.IndexOf("Mercury")), model.OrbitPeriod(model.IndexOf("Earth")));
            Assert.Less(model.OrbitPeriod(model.IndexOf("Earth")), model.OrbitPeriod(model.IndexOf("Neptune")));
            Assert.Greater(model.OrbitSpeed(model.IndexOf("Mercury")), model.OrbitSpeed(model.IndexOf("Neptune")));
        }

        [Test]
        public void TritonGoesRoundBackwards()
        {
            SolarSystemModel model = Build();
            Assert.AreEqual(-SolarSystemModel.Prograde, Math.Sign(model.OrbitRate(model.IndexOf("Triton"))));
            Assert.AreEqual(SolarSystemModel.Prograde, Math.Sign(model.OrbitRate(model.IndexOf("Moon"))));
        }

        [Test]
        public void PlanetsGoRoundAnticlockwiseSeenFromTheNorth()
        {
            // As in the real solar system. On the map (X across, Z up, looking down from the north) that means the
            // bearing of a planet from the Sun keeps increasing.
            SolarSystemModel model = Build();
            int earth = model.IndexOf("Earth");
            BodyState[] a = At(model, 0);
            BodyState[] b = At(model, 5);
            double before = Math.Atan2(a[earth].Position.Z, a[earth].Position.X);
            double after = Math.Atan2(b[earth].Position.Z, b[earth].Position.X);
            Assert.Greater(Math.IEEERemainder(after - before, 2 * Math.PI), 0);

            // Earth also spins that way: a point on the equator moves toward increasing bearing.
            Vec3d spot = model.Orientation(earth, a[earth]).Rotate(Vec3d.Right);
            Vec3d later = model.Orientation(earth, b[earth]).Rotate(Vec3d.Right);
            Assert.Greater(Vec3d.Dot(Vec3d.Cross(spot, later), model.SpinAxis(earth)) * SolarSystemModel.Prograde, 0);
        }

        [Test]
        public void TheSeedOnlyChangesStartingPositions()
        {
            SolarSystemModel a = Build(1);
            SolarSystemModel again = Build(1);
            SolarSystemModel b = Build(2);
            BodyState[] sa = At(a, 60);
            BodyState[] sAgain = At(again, 60);
            BodyState[] sb = At(b, 60);
            int moved = 0;
            for (int i = 1; i < a.Count; i++)
            {
                Assert.AreEqual(0, Vec3d.Distance(sa[i].Position, sAgain[i].Position), 1e-9, "Same seed, same place.");
                Assert.AreEqual(a.Spec(i).orbitRadius, b.Spec(i).orbitRadius, "Orbits are the same every run.");
                Assert.AreEqual(a.OrbitPeriod(i), b.OrbitPeriod(i), 1e-9);
                if (Vec3d.Distance(sa[i].Position, sb[i].Position) > 10) moved++;
            }
            Assert.GreaterOrEqual(moved, a.Count - 3, "A different seed starts bodies somewhere else on their orbits.");
        }

        [Test]
        public void SurfaceGravityMatchesTheData()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 0);
            foreach (string id in new[] { "Earth", "Moon", "Mars", "Jupiter" })
            {
                int i = model.IndexOf(id);
                Vec3d surface = states[i].Position + new Vec3d(0, model.Spec(i).radius, 0);
                Vec3d pull = model.GravityFrom(i, surface, states);
                Assert.AreEqual(model.Spec(i).surfaceGravity, pull.Magnitude, 1e-9, id);
                Assert.Less(pull.Y, 0, "Gravity points at the centre.");
            }
        }

        [Test]
        public void GravityFallsOffWithTheSquareOfDistance()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 0);
            int earth = model.IndexOf("Earth");
            double r = model.Spec(earth).radius;
            double atTwo = model.GravityFrom(earth, states[earth].Position + new Vec3d(2 * r, 0, 0), states).Magnitude;
            Assert.AreEqual(model.Spec(earth).surfaceGravity / 4, atTwo, 1e-9);
            // Inside the body it fades to nothing at the centre instead of shooting to infinity.
            double halfway = model.GravityFrom(earth, states[earth].Position + new Vec3d(0.5 * r, 0, 0), states).Magnitude;
            Assert.AreEqual(model.Spec(earth).surfaceGravity / 2, halfway, 1e-9);
            Assert.AreEqual(0, model.GravityFrom(earth, states[earth].Position, states).Magnitude, 1e-9);
        }

        [Test]
        public void RealGravityOrderIsKept()
        {
            SolarSystemModel model = Build();
            double G(string id) => model.Spec(model.IndexOf(id)).surfaceGravity;
            Assert.Greater(G("Jupiter"), G("Earth"));
            Assert.Greater(G("Earth"), G("Mars"));
            Assert.Greater(G("Mars"), G("Moon"));
            Assert.AreEqual(9.8, G("Earth"), 0.01);
        }

        [Test]
        public void WhatYouFeelOnTheGroundIsThatBodysGravity()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 77);
            foreach (string id in new[] { "Earth", "Moon", "Phobos", "Titan", "Neptune", "Mercury" })
            {
                int i = model.IndexOf(id);
                var frame = new ReferenceFrame(model);
                frame.SetAnchor(i, states);
                frame.StartRotating(states);
                // Standing still on the equator.
                Vec3d surface = states[i].Position + model.SpinAxis(i).AnyPerpendicular() * model.Spec(i).radius;
                Vec3d local = frame.ToLocalPosition(surface, states);
                Vec3d felt = frame.ToLocalAcceleration(local, Vec3d.Zero, model.AccelerationAt(surface, states, i), states);
                Vec3d down = frame.ToLocalDirection((states[i].Position - surface).Normalized, states);
                double g = model.Spec(i).surfaceGravity;
                // The only difference is the planet's spin trying to fling you off, which is small.
                Assert.AreEqual(g, felt.Magnitude, g * 0.13, id + ": what you feel should be close to the listed gravity.");
                Assert.LessOrEqual(felt.Magnitude, g + 1e-9, id + ": spin can only make you lighter.");
                Assert.Greater(Vec3d.Dot(felt.Normalized, down), 0.99, id + ": down should be toward the centre.");
            }
        }

        [Test]
        public void InsideANeighbourhoodOnlyThatBodyAndItsMoonsPull()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 77);
            int earth = model.IndexOf("Earth");
            int moon = model.IndexOf("Moon");
            Vec3d point = states[earth].Position + new Vec3d(900, 100, -300);

            // At Earth: carried along with Earth, pulled by Earth and the Moon. Nothing from the Sun or Mars.
            Vec3d expected = states[earth].Acceleration + model.GravityFrom(earth, point, states) + model.GravityFrom(moon, point, states);
            Assert.AreEqual(0, Vec3d.Distance(expected, model.AccelerationAt(point, states, earth)), 1e-12);

            // At the Moon: carried along with the Moon, pulled by the Moon alone.
            Vec3d nearMoon = states[moon].Position + new Vec3d(0, 200, 0);
            Vec3d moonOnly = states[moon].Acceleration + model.GravityFrom(moon, nearMoon, states);
            Assert.AreEqual(0, Vec3d.Distance(moonOnly, model.AccelerationAt(nearMoon, states, moon)), 1e-12);

            // In open space: the Sun and every planet, but no moons.
            Vec3d open = new Vec3d(5000, 800, 27000);
            Vec3d sunAndPlanets = model.GravityFrom(0, open, states);
            for (int i = 1; i < model.Count; i++) if (model.ParentOf(i) == 0) sunAndPlanets += model.GravityFrom(i, open, states);
            Assert.AreEqual(0, Vec3d.Distance(sunAndPlanets, model.AccelerationAt(open, states, 0)), 1e-12);
        }

        [Test]
        public void NeighbourhoodsNestMoonInsidePlanetInsideSun()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 10);
            int earth = model.IndexOf("Earth");
            int moon = model.IndexOf("Moon");
            Vec3d nearEarth = states[earth].Position + new Vec3d(0, model.Spec(earth).radius + 100, 0);
            Vec3d nearMoon = states[moon].Position + new Vec3d(0, model.Spec(moon).radius + 20, 0);
            Vec3d deepSpace = (states[earth].Position + states[model.IndexOf("Mars")].Position) * 0.5 + new Vec3d(0, 9000, 0);
            Assert.AreEqual(earth, model.NeighbourhoodOf(nearEarth, states, 0));
            Assert.AreEqual(moon, model.NeighbourhoodOf(nearMoon, states, 0));
            Assert.AreEqual(0, model.NeighbourhoodOf(deepSpace, states, earth));
        }

        [Test]
        public void EveryMoonSitsInsideItsPlanetsNeighbourhoodAndOutsideItsLockRadius()
        {
            SolarSystemModel model = Build();
            for (int i = 1; i < model.Count; i++)
            {
                int parent = model.ParentOf(i);
                if (parent == 0) continue;
                BodySpec moon = model.Spec(i);
                Assert.Less(moon.orbitRadius + model.InfluenceRadius(i), model.InfluenceRadius(parent),
                    moon.id + " would poke out of its planet's neighbourhood.");
                Assert.Greater(moon.orbitRadius - model.InfluenceRadius(i), model.LockRadius(parent),
                    moon.id + " is too close to its planet.");
                Assert.Greater(model.InfluenceRadius(i), moon.radius * 2, moon.id);
                Assert.Greater(model.LockRadius(i), moon.radius);
            }
        }

        [Test]
        public void LeavingANeighbourhoodTakesALittleExtraDistance()
        {
            SolarSystemModel model = Build();
            BodyState[] states = At(model, 10);
            int mars = model.IndexOf("Mars");
            double edge = model.InfluenceRadius(mars);
            Vec3d justOutside = states[mars].Position + new Vec3d(0, edge * 1.05, 0);
            Assert.AreEqual(0, model.NeighbourhoodOf(justOutside, states, 0), "Arriving: not there yet.");
            Assert.AreEqual(mars, model.NeighbourhoodOf(justOutside, states, mars), "Leaving: still counts as Mars.");
            Assert.AreEqual(mars, model.NeighbourhoodOf(justOutside, states, model.IndexOf("Phobos")), "Coming off a moon of Mars also sticks.");
        }

        [Test]
        public void TidallyLockedMoonsKeepOneFaceToTheirPlanet()
        {
            SolarSystemModel model = Build();
            int moon = model.IndexOf("Moon");
            int earth = model.IndexOf("Earth");
            double first = double.NaN;
            foreach (double time in new[] { 0.0, 90.0, 333.0, 1000.0 })
            {
                BodyState[] states = At(model, time);
                Vec3d toEarth = (states[earth].Position - states[moon].Position).Normalized;
                // The direction to Earth, seen from the Moon's own surface, should not change.
                Vec3d seenFromSurface = model.Orientation(moon, states[moon]).Inverse.Rotate(toEarth);
                double bearing = Math.Atan2(seenFromSurface.Z, seenFromSurface.X);
                if (double.IsNaN(first)) first = bearing;
                Assert.AreEqual(0, Math.IEEERemainder(bearing - first, 2 * Math.PI), 1e-6);
            }
        }

        [Test]
        public void EarthsDayAndTilt()
        {
            SolarSystemModel model = Build();
            int earth = model.IndexOf("Earth");
            Assert.AreEqual(2 * Math.PI / 600, Math.Abs(model.SpinRate(earth)), 1e-12);
            Assert.AreEqual(SolarSystemModel.Prograde, Math.Sign(model.SpinRate(earth)));
            double tilt = Math.Acos(Vec3d.Dot(model.SpinAxis(earth), model.OrbitNormal(earth))) * 180 / Math.PI;
            Assert.AreEqual(23.4, tilt, 1e-6);
            Assert.AreEqual(-SolarSystemModel.Prograde, Math.Sign(model.SpinRate(model.IndexOf("Venus"))), "Venus spins backwards.");
            // North pole stays pointing along the spin axis as the planet turns.
            BodyState[] states = At(model, 1234);
            Vec3d pole = model.Orientation(earth, states[earth]).Rotate(Vec3d.Up);
            Assert.AreEqual(0, Vec3d.Distance(pole, model.SpinAxis(earth)), 1e-9);
        }

        [Test]
        public void AsteroidsAreSeededAndSitInTheBelt()
        {
            var belt = new AsteroidBeltSpec();
            List<BodySpec> a = AsteroidBelt.Generate(5, belt, "Sun");
            List<BodySpec> again = AsteroidBelt.Generate(5, belt, "Sun");
            List<BodySpec> b = AsteroidBelt.Generate(6, belt, "Sun");
            Assert.AreEqual(belt.count, a.Count);
            int different = 0;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].orbitRadius, again[i].orbitRadius);
                Assert.AreEqual(a[i].radius, again[i].radius);
                Assert.GreaterOrEqual(a[i].orbitRadius, belt.innerRadius);
                Assert.LessOrEqual(a[i].orbitRadius, belt.outerRadius);
                Assert.GreaterOrEqual(a[i].radius, belt.minSize);
                Assert.LessOrEqual(a[i].radius, belt.maxSize);
                Assert.AreEqual(BodyKind.Asteroid, a[i].kind);
                if (Math.Abs(a[i].orbitRadius - b[i].orbitRadius) > 1) different++;
            }
            Assert.Greater(different, a.Count / 2);

            // Asking for more rocks leaves the first ones exactly where they were.
            var bigger = new AsteroidBeltSpec { count = belt.count + 20 };
            List<BodySpec> more = AsteroidBelt.Generate(5, bigger, "Sun");
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].orbitRadius, more[i].orbitRadius);
        }

        [Test]
        public void TheWholeSystemWithAsteroidsBuilds()
        {
            List<BodySpec> bodies = SolarSystemDefaults.Bodies();
            bodies.AddRange(AsteroidBelt.Generate(3, new AsteroidBeltSpec(), "Sun"));
            var model = new SolarSystemModel(bodies, 3);
            Assert.AreEqual(20 + new AsteroidBeltSpec().count, model.Count);
            int mars = model.IndexOf("Mars");
            int jupiter = model.IndexOf("Jupiter");
            int rock = model.IndexOf("Asteroid001");
            Assert.Greater(model.Spec(rock).orbitRadius, model.Spec(mars).orbitRadius + model.InfluenceRadius(mars));
            Assert.Less(model.Spec(rock).orbitRadius, model.Spec(jupiter).orbitRadius - model.InfluenceRadius(jupiter));
        }

        [Test]
        public void BadDataIsRefusedWithAReason()
        {
            var twoSuns = new List<BodySpec> { new BodySpec { id = "A" }, new BodySpec { id = "B" } };
            Assert.Throws<ArgumentException>(() => new SolarSystemModel(twoSuns, 1));
            var orphan = new List<BodySpec> { new BodySpec { id = "Sun" }, new BodySpec { id = "X", parentId = "Nowhere" } };
            Assert.Throws<ArgumentException>(() => new SolarSystemModel(orphan, 1));
            var twins = new List<BodySpec> { new BodySpec { id = "Sun" }, new BodySpec { id = "Sun" } };
            Assert.Throws<ArgumentException>(() => new SolarSystemModel(twins, 1));
            var loop = new List<BodySpec>
            {
                new BodySpec { id = "Sun" }, new BodySpec { id = "A", parentId = "B" }, new BodySpec { id = "B", parentId = "A" },
            };
            Assert.Throws<ArgumentException>(() => new SolarSystemModel(loop, 1));
        }

        [Test]
        public void ChildrenListedBeforeParentsStillWork()
        {
            var bodies = new List<BodySpec>
            {
                new BodySpec { id = "Moon", parentId = "Earth", orbitRadius = 1000, radius = 50 },
                new BodySpec { id = "Earth", parentId = "Sun", orbitRadius = 20000, radius = 500 },
                new BodySpec { id = "Sun", radius = 3000, surfaceGravity = 75 },
            };
            var model = new SolarSystemModel(bodies, 1);
            Assert.AreEqual("Sun", model.Spec(0).id);
            Assert.Less(model.IndexOf("Earth"), model.IndexOf("Moon"));
        }
    }
}
