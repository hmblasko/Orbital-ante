using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Additional pure-rule cases for deterministic rail evaluation and reference-frame boundaries.
    /// Written against main 96264e13187659d90dba198703bba188721f4e23; not run in Unity.
    /// </summary>
    public class SpaceBoundaryTests
    {
        static List<BodySpec> Bodies()
        {
            // Small isolated fixtures avoid depending on the shipped world's tuning values.
            return new List<BodySpec>
            {
                new BodySpec { id = "Sun", radius = 1000, surfaceGravity = 20, dayLength = 0 },
                new BodySpec
                {
                    id = "Planet", parentId = "Sun", radius = 100, surfaceGravity = 5,
                    orbitRadius = 10000, orbitInclination = 7, axialTilt = 20, dayLength = 16,
                },
            };
        }

        static void Close(Vec3d expected, Vec3d actual, double tolerance = 1e-8)
        {
            Assert.LessOrEqual(Vec3d.Distance(expected, actual), tolerance);
        }

        static void SameState(in BodyState expected, in BodyState actual)
        {
            Close(expected.Position, actual.Position);
            Close(expected.Velocity, actual.Velocity);
            Close(expected.Acceleration, actual.Acceleration);
            Assert.AreEqual(expected.SpinAngle, actual.SpinAngle, 1e-10);
        }

        [TestCase(-64.0)]
        [TestCase(0.0)]
        [TestCase(1024.0)]
        public void EvaluatingOutOfOrderAndReusingABufferDoesNotChangeTheAnswer(double time)
        {
            var model = new SolarSystemModel(Bodies(), 123);
            BodyState[] expected = model.NewStateBuffer();
            BodyState[] reused = model.NewStateBuffer();
            model.Evaluate(time, expected);
            model.Evaluate(time + 900, reused);
            model.Evaluate(time - 600, reused);
            model.Evaluate(time, reused);

            for (int i = 0; i < model.Count; i++) SameState(expected[i], reused[i]);
            model.Evaluate(time, reused);
            for (int i = 0; i < model.Count; i++) SameState(expected[i], reused[i]);
        }

        [Test]
        public void AddingAndReorderingAnUnrelatedSiblingDoesNotReseedExistingBodies()
        {
            List<BodySpec> original = Bodies();
            var baseline = new SolarSystemModel(original, 42);
            var sibling = new BodySpec
            {
                id = "OtherPlanet", parentId = "Sun", radius = 40, surfaceGravity = 2,
                orbitRadius = 20000, orbitInclination = 11, dayLength = 32,
            };
            var changed = new SolarSystemModel(new List<BodySpec>
            {
                sibling, original[1].Clone(), original[0].Clone(),
            }, 42);
            BodyState[] a = baseline.NewStateBuffer();
            BodyState[] b = changed.NewStateBuffer();
            foreach (double time in new[] { 0.0, 128.0, 4096.0 })
            {
                baseline.Evaluate(time, a);
                changed.Evaluate(time, b);
                for (int i = 0; i < baseline.Count; i++)
                    SameState(a[i], b[changed.IndexOf(baseline.Spec(i).id)]);
            }
        }

        [Test]
        public void RepeatingFrameModeRequestsDoesNotRelockOrMoveTheFrame()
        {
            var model = new SolarSystemModel(Bodies(), 7);
            BodyState[] states = model.NewStateBuffer();
            model.Evaluate(0, states);
            int planet = model.IndexOf("Planet");
            var once = new ReferenceFrame(model);
            var repeated = new ReferenceFrame(model);
            once.SetAnchor(planet, states);
            repeated.SetAnchor(planet, states);
            once.StartRotating(states);
            repeated.StartRotating(states);

            model.Evaluate(3, states);
            repeated.SetAnchor(planet, states);
            repeated.StartRotating(states);
            model.Evaluate(7, states);
            var probe = new Vec3d(100, -20, 30);
            Close(once.ToTruePosition(probe, states), repeated.ToTruePosition(probe, states));
            Close(once.AngularVelocityLocal, repeated.AngularVelocityLocal);

            once.StopRotating(states);
            repeated.StopRotating(states);
            model.Evaluate(11, states);
            repeated.StopRotating(states);
            Close(once.ToTruePosition(probe, states), repeated.ToTruePosition(probe, states));
            Close(Vec3d.Zero, repeated.AngularVelocityLocal);
        }

        [Test]
        public void RepeatedFollowAtASteadySurfacePointReportsNoFurtherTransition()
        {
            var model = new SolarSystemModel(Bodies(), 5);
            BodyState[] states = model.NewStateBuffer();
            model.Evaluate(8, states);
            int planet = model.IndexOf("Planet");
            Vec3d point = states[planet].Position + new Vec3d(0, model.Spec(planet).radius + 1, 0);
            var frame = new ReferenceFrame(model);
            Assert.IsTrue(frame.Follow(point, states));
            Vec3d local = frame.ToLocalPosition(point, states);

            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(frame.Follow(point, states));
                Assert.AreEqual(planet, frame.Anchor);
                Assert.IsTrue(frame.Rotating);
                Close(local, frame.ToLocalPosition(point, states));
            }
        }

        [Test]
        public void ZeroDurationStepHasTheInstantaneousAccelerationAndDoesNotChangeTheFrame()
        {
            var model = new SolarSystemModel(Bodies(), 8);
            BodyState[] states = model.NewStateBuffer();
            model.Evaluate(10, states);
            var frame = new ReferenceFrame(model);
            frame.SetAnchor(model.IndexOf("Planet"), states);
            frame.StartRotating(states);
            frame.Recenter(new Vec3d(100, -20, 30));
            var position = new Vec3d(40, 50, -30);
            var velocity = new Vec3d(2, -3, 4);
            var acceleration = new Vec3d(-5, 1, 3);
            Vec3d before = frame.ToTruePosition(position, states);

            Close(frame.ToLocalAcceleration(position, velocity, acceleration, states),
                frame.ToLocalAccelerationForStep(position, velocity, acceleration, states, 0));
            Close(before, frame.ToTruePosition(position, states));
        }

        [TestCase(1)]
        [TestCase(9)]
        public void OrbitOutlineClosesAndResamplingDoesNotChangePreviouslyReturnedPoints(int segments)
        {
            var model = new SolarSystemModel(Bodies(), 1);
            int planet = model.IndexOf("Planet");
            Vec3d[] outline = model.OrbitOutline(planet, segments);
            var saved = (Vec3d[])outline.Clone();
            Assert.AreEqual(segments + 1, outline.Length);
            Close(outline[0], outline[segments]);
            foreach (Vec3d point in outline)
            {
                Assert.IsFalse(double.IsNaN(point.Magnitude));
                Assert.IsFalse(double.IsInfinity(point.Magnitude));
                Assert.AreEqual(model.Spec(planet).orbitRadius, point.Magnitude, 1e-8);
            }

            model.OrbitOutline(planet, segments + 3);
            for (int i = 0; i < saved.Length; i++) Close(saved[i], outline[i]);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveAsteroidCountProducesNoRocks(int count)
        {
            Assert.AreEqual(0, AsteroidBelt.Generate(5, new AsteroidBeltSpec { count = count }, "Sun").Count);
        }

        [Test]
        public void MissingAsteroidBeltProducesNoRocks()
        {
            Assert.AreEqual(0, AsteroidBelt.Generate(5, null, "Sun").Count);
        }

        [Test]
        public void IncreasingAsteroidCountPreservesEveryExistingRocksAuthoredProperties()
        {
            List<BodySpec> small = AsteroidBelt.Generate(123, new AsteroidBeltSpec { count = 2 }, "Sun");
            List<BodySpec> large = AsteroidBelt.Generate(123, new AsteroidBeltSpec { count = 3 }, "Sun");
            for (int i = 0; i < small.Count; i++)
            {
                BodySpec a = small[i], b = large[i];
                Assert.AreEqual(a.id, b.id);
                Assert.AreEqual(a.parentId, b.parentId);
                Assert.AreEqual(a.radius, b.radius);
                Assert.AreEqual(a.surfaceGravity, b.surfaceGravity);
                Assert.AreEqual(a.orbitRadius, b.orbitRadius);
                Assert.AreEqual(a.orbitInclination, b.orbitInclination);
                Assert.AreEqual(a.orbitNode, b.orbitNode);
                Assert.AreEqual(a.dayLength, b.dayLength);
                Assert.AreEqual(a.axialTilt, b.axialTilt);
                Assert.AreEqual(a.retrogradeSpin, b.retrogradeSpin);
                Assert.AreEqual(a.colorA, b.colorA);
                Assert.AreEqual(a.colorB, b.colorB);
            }
        }
    }
}
