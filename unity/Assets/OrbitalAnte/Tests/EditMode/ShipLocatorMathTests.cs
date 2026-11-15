using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    public class ShipLocatorMathTests
    {
        static ShipLocatorPoint Project(Vec3d delta, double width = 1280, double height = 800)
            => ShipLocatorMath.Project(delta, 90, width, height, 104, 104, 216, 104);

        [Test]
        public void ForwardTargetIsAtItsPerspectivePosition()
        {
            ShipLocatorPoint point = Project(new Vec3d(1, 0.5, 10));
            Assert.IsTrue(point.Valid);
            Assert.IsTrue(point.AtTarget);
            Assert.AreEqual(40, point.X, 1e-9);
            Assert.AreEqual(20, point.Y, 1e-9);
        }

        [TestCase(10, 0, 1, 536, 0)]
        [TestCase(-10, 0, 1, -536, 0)]
        [TestCase(0, 10, 1, 0, 296)]
        [TestCase(0, -10, 1, 0, -184)]
        public void OffscreenTargetMeetsTheSafeEdge(double x, double y, double z, double expectedX, double expectedY)
        {
            ShipLocatorPoint point = Project(new Vec3d(x, y, z));
            Assert.IsTrue(point.Valid);
            Assert.IsFalse(point.AtTarget);
            Assert.AreEqual(expectedX, point.X, 1e-9);
            Assert.AreEqual(expectedY, point.Y, 1e-9);
        }

        [Test]
        public void BehindTargetStaysAtAnEdgeAndKeepsItsBearing()
        {
            ShipLocatorPoint point = Project(new Vec3d(1, 0, -10));
            Assert.IsTrue(point.Valid);
            Assert.IsFalse(point.AtTarget);
            Assert.AreEqual(536, point.X, 1e-9);
            Assert.AreEqual(0, point.Angle, 1e-9);
            ShipLocatorPoint aft = Project(new Vec3d(0, 0, -10));
            Assert.IsFalse(aft.AtTarget);
            Assert.AreEqual(-184, aft.Y, 1e-9);
            Assert.AreEqual(-90, aft.Angle, 1e-9);
        }

        [TestCase(1280, 800)]
        [TestCase(1920, 1080)]
        [TestCase(800, 1280)]
        public void DiagonalClampingPreservesBearingAcrossAspectRatios(double width, double height)
        {
            ShipLocatorPoint point = Project(new Vec3d(10, -8, 0), width, height);
            Assert.IsTrue(point.Valid);
            Assert.IsFalse(point.AtTarget);
            Assert.AreEqual(-0.8, point.Y / point.X, 1e-9);
            Assert.That(point.X, Is.InRange(-width * 0.5 + 104, width * 0.5 - 104));
            Assert.That(point.Y, Is.InRange(-height * 0.5 + 216, height * 0.5 - 104));
        }

        [Test]
        public void SameBearingUsesViewportAspectInsteadOfStretchingTheProjection()
        {
            ShipLocatorPoint wide = Project(new Vec3d(1, 0, 1), 1920, 800);
            ShipLocatorPoint narrow = Project(new Vec3d(1, 0, 1), 800, 800);
            Assert.IsTrue(wide.AtTarget);
            Assert.AreEqual(400, wide.X, 1e-9);
            Assert.IsFalse(narrow.AtTarget);
            Assert.AreEqual(296, narrow.X, 1e-9);
        }

        [TestCase(1e-300)]
        [TestCase(1e300)]
        public void BearingIsIndependentOfDistanceWithoutNumericOverflow(double scale)
        {
            ShipLocatorPoint ordinary = Project(new Vec3d(1, -0.5, 10));
            ShipLocatorPoint remote = Project(new Vec3d(scale, -0.5 * scale, 10 * scale));
            Assert.IsTrue(remote.Valid);
            Assert.AreEqual(ordinary.AtTarget, remote.AtTarget);
            Assert.AreEqual(ordinary.X, remote.X, 1e-9);
            Assert.AreEqual(ordinary.Y, remote.Y, 1e-9);
        }

        [Test]
        public void ExtremeSidewaysBearingStillMeetsAFiniteEdge()
        {
            ShipLocatorPoint point = Project(new Vec3d(1e308, 0, 0));
            Assert.IsTrue(point.Valid);
            Assert.IsFalse(point.AtTarget);
            Assert.AreEqual(536, point.X, 1e-9);
            Assert.AreEqual(0, point.Y, 1e-9);
        }

        [TestCase(0, "0 m")]
        [TestCase(0.5, "1 m")]
        [TestCase(999.49, "999 m")]
        [TestCase(999.5, "1.0 km")]
        [TestCase(1550, "1.6 km")]
        [TestCase(1200000, "1200.0 km")]
        public void DistanceRoundsToReadableMetresOrTenthsOfKilometres(double metres, string expected)
            => Assert.AreEqual(expected, ShipLocatorMath.Distance(metres).ToString());

        [Test]
        public void UnknownNumbersCannotBecomeATargetOrDistance()
        {
            Assert.IsFalse(Project(new Vec3d(double.NaN, 0, 1)).Valid);
            Assert.IsFalse(Project(new Vec3d(0, double.PositiveInfinity, 1)).Valid);
            Assert.IsFalse(ShipLocatorMath.Distance(double.NaN).Valid);
            Assert.IsFalse(ShipLocatorMath.Distance(double.PositiveInfinity).Valid);
            Assert.IsFalse(ShipLocatorMath.Distance(-1).Valid);
        }

        [Test]
        public void DistanceAndBearingSurviveARecentreAndAnchorChange()
        {
            var model = new SolarSystemModel(SolarSystemDefaults.Bodies(), 11);
            BodyState[] states = model.NewStateBuffer();
            model.Evaluate(321, states);
            var frame = new ReferenceFrame(model);
            int earth = model.IndexOf("Earth");
            frame.SetAnchor(earth, states);
            frame.StartRotating(states);
            Vec3d eye = states[earth].Position + new Vec3d(300, 520, -80);
            Vec3d ship = eye + new Vec3d(140, -25, 900);
            Vec3d local = frame.ToLocalDirection(ship - eye, states);
            ShipLocatorPoint before = Project(local);
            ShipLocatorDistance distance = ShipLocatorMath.Distance(Vec3d.Distance(eye, ship));

            frame.Recenter(frame.ToLocalPosition(eye, states));
            frame.SetAnchor(model.IndexOf("Moon"), states);
            Vec3d rebuiltEye = frame.ToTruePosition(frame.ToLocalPosition(eye, states), states);
            Vec3d rebuiltShip = frame.ToTruePosition(frame.ToLocalPosition(ship, states), states);
            ShipLocatorPoint after = Project(frame.ToLocalDirection(rebuiltShip - rebuiltEye, states));
            Assert.AreEqual(before.X, after.X, 1e-6);
            Assert.AreEqual(before.Y, after.Y, 1e-6);
            Assert.AreEqual(before.AtTarget, after.AtTarget);
            Assert.IsTrue(distance.Equals(ShipLocatorMath.Distance(Vec3d.Distance(rebuiltEye, rebuiltShip))));
        }
    }
}
