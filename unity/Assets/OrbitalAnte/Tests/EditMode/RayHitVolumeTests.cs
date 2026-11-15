using System;
using NUnit.Framework;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    public class RayHitVolumeTests
    {
        static readonly Vec3d A = new Vec3d(0, -1, 5);
        static readonly Vec3d B = new Vec3d(0, 1, 5);

        static void At(double expected, bool hit, double distance, double tolerance = 1e-9)
        {
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, distance, tolerance);
        }

        [Test]
        public void SphereUsesTheEntryDistance_AndNormalizesDirection()
        {
            bool hit = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward * 7, 10, new Vec3d(0, 0, 5), 1, out double distance);
            At(4, hit, distance);
        }

        [Test]
        public void SphereCannotHitBehindTheRay_OrOutsideItsRadius()
        {
            Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, -Vec3d.Forward, 10, new Vec3d(0, 0, 5), 1, out _));
            Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 10, new Vec3d(1.001, 0, 5), 1, out _));
        }

        [Test]
        public void DistantSmallSphere_KeepsItsRadiusAndRejectsNearMisses()
        {
            bool hit = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 1e8, new Vec3d(0, 0, 1e8), 1, out double distance);
            At(1e8 - 1, hit, distance, 1e-7);
            Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 1e8, new Vec3d(1.001, 0, 1e8), 1, out _));
        }

        [Test]
        public void CapsuleSideUsesTheFirstSurface_NotItsAxisOrExit()
        {
            bool hit = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10, A, B, 1, out double distance);
            At(4, hit, distance);
            Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, -Vec3d.Forward, 10, A, B, 1, out _));
        }

        [Test]
        public void AnEarlierWorldObstructionCanBoundBothLogicalVolumes()
        {
            foreach (double range in new[] { 0.0, 3.99 })
            {
                Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, range, new Vec3d(0, 0, 5), 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, range, A, B, 1, out _));
            }
            bool sphere = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 4, new Vec3d(0, 0, 5), 1, out double sphereDistance);
            At(4, sphere, sphereDistance);
            bool capsule = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 4, A, B, 1, out double capsuleDistance);
            At(4, capsule, capsuleDistance);
        }

        [Test]
        public void InsideAndSurfaceStartsReturnZero_EvenForZeroRangeAndOutwardRays()
        {
            foreach (Vec3d origin in new[] { new Vec3d(0, 0, 5), new Vec3d(0, 0, 4), new Vec3d(0, 1.5, 5) })
            {
                bool hit = RayHitVolume.Capsule(origin, -Vec3d.Forward, 0, A, B, 1, out double distance);
                At(0, hit, distance);
            }
            bool sphere = RayHitVolume.Sphere(new Vec3d(0, 0, 4), -Vec3d.Forward, 0, new Vec3d(0, 0, 5), 1, out double sphereDistance);
            At(0, sphere, sphereDistance);
        }

        [Test]
        public void ParallelRayHitsTheRoundedEnd_AndCanMissBesideIt()
        {
            bool hit = RayHitVolume.Capsule(new Vec3d(0, -5, 5), Vec3d.Up, 10, A, B, 1, out double distance);
            At(3, hit, distance);
            Assert.IsFalse(RayHitVolume.Capsule(new Vec3d(1.01, -5, 5), Vec3d.Up, 10, A, B, 1, out _));
        }

        [Test]
        public void EndCapAndCylinderSeamTangenciesCount_ButNearbyMissesDoNot()
        {
            bool cap = RayHitVolume.Capsule(new Vec3d(-5, 2, 5), Vec3d.Right, 10, A, B, 1, out double capDistance);
            At(5, cap, capDistance);
            bool seam = RayHitVolume.Capsule(new Vec3d(-5, 1, 6), Vec3d.Right, 10, A, B, 1, out double seamDistance);
            At(5, seam, seamDistance);
            bool sphere = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 10, new Vec3d(1, 0, 5), 1, out double sphereDistance);
            At(5, sphere, sphereDistance);
            Assert.IsFalse(RayHitVolume.Capsule(new Vec3d(-5, 2.001, 5), Vec3d.Right, 10, A, B, 1, out _));
        }

        [Test]
        public void DiagonalRayEntersTheRoundedEndBeforeTheBarrel()
        {
            Vec3d origin = new Vec3d(0, 3, 3);
            bool hit = RayHitVolume.Capsule(origin, new Vec3d(0, -1, 1), 10, A, B, 1, out double distance);
            At(Math.Sqrt(8) - 1, hit, distance);
        }

        [Test]
        public void DegenerateCapsuleIsTheSameSphere()
        {
            Vec3d center = new Vec3d(0.5, 0, 5);
            bool sphere = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 10, center, 1, out double sphereDistance);
            bool capsule = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10, center, center, 1, out double capsuleDistance);
            At(5 - Math.Sqrt(0.75), sphere, sphereDistance);
            At(sphereDistance, capsule, capsuleDistance);
        }

        [Test]
        public void ZeroRadiusCanStillHitTheSegmentOrPoint()
        {
            bool point = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 5, new Vec3d(0, 0, 5), 0, out double pointDistance);
            At(5, point, pointDistance);
            bool segment = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 5, A, B, 0, out double segmentDistance);
            At(5, segment, segmentDistance);
            Assert.IsFalse(RayHitVolume.Capsule(new Vec3d(0.001, 0, 0), Vec3d.Forward, 10, A, B, 0, out _));
        }

        [Test]
        public void NearParallelRaysStillEnterALongCapsulesSide()
        {
            Vec3d a = Vec3d.Zero;
            Vec3d b = Vec3d.Up * 1e9;
            Vec3d origin = new Vec3d(2, 1, 0);
            Vec3d direction = new Vec3d(-1e-8, 1, 0);
            bool hit = RayHitVolume.Capsule(origin, direction, 2e8, a, b, 1, out double distance);
            At(1e8, hit, distance, 1e-6);
        }

        [TestCase(1e-300)]
        [TestCase(1.0)]
        [TestCase(1e300)]
        public void DirectionMagnitudeDoesNotChangeWorldDistance(double multiplier)
        {
            bool sphere = RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward * multiplier, 10, new Vec3d(0, 0, 5), 1, out double sphereDistance);
            At(4, sphere, sphereDistance);
            bool capsule = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward * multiplier, 10, A, B, 1, out double capsuleDistance);
            At(4, capsule, capsuleDistance);
        }

        [TestCase(1e-100)]
        [TestCase(0.001)]
        [TestCase(1.0)]
        [TestCase(1000000.0)]
        [TestCase(1e100)]
        public void UniformScaleChangesOnlyTheDistance(double scale)
        {
            bool hit = RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10 * scale, A * scale, B * scale, scale, out double distance);
            At(4 * scale, hit, distance, 1e-12 * scale);
            Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 3.9 * scale, A * scale, B * scale, scale, out _));
        }

        [Test]
        public void RotationTranslationAndEndpointOrderDoNotChangeEntryDistance()
        {
            Vec3d axis = new Vec3d(1, 2, 3).Normalized;
            Vec3d offset = new Vec3d(10000, -20000, 30000);
            Vec3d direction = Vec3d.Forward.RotatedAround(axis, 1.3);
            Vec3d a = A.RotatedAround(axis, 1.3) + offset;
            Vec3d b = B.RotatedAround(axis, 1.3) + offset;
            bool first = RayHitVolume.Capsule(offset, direction, 10, a, b, 1, out double distance);
            At(4, first, distance, 1e-10);
            bool reversed = RayHitVolume.Capsule(offset, direction, 10, b, a, 1, out double reversedDistance);
            At(distance, reversed, reversedDistance, 1e-10);
        }

        [Test]
        public void InvalidInputsFailWithoutLeavingADistance()
        {
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Vec3d bad = new Vec3d(invalid, 0, 0);
                Assert.IsFalse(RayHitVolume.Sphere(bad, Vec3d.Forward, 10, A, 1, out _));
                Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, bad, 10, A, 1, out _));
                Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 10, bad, 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10, bad, B, 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10, A, bad, 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, bad, 10, A, B, 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(bad, Vec3d.Forward, 10, A, B, 1, out _));
            }
            foreach (double invalid in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, invalid, A, 1, out _));
                Assert.IsFalse(RayHitVolume.Sphere(Vec3d.Zero, Vec3d.Forward, 10, A, invalid, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, invalid, A, B, 1, out _));
                Assert.IsFalse(RayHitVolume.Capsule(Vec3d.Zero, Vec3d.Forward, 10, A, B, invalid, out _));
            }
            Assert.IsFalse(RayHitVolume.Sphere(A, Vec3d.Zero, 0, A, 1, out double distance));
            Assert.AreEqual(0, distance);
            Assert.IsFalse(RayHitVolume.Capsule(A, Vec3d.Zero, 0, A, B, 1, out distance));
            Assert.AreEqual(0, distance);
        }
    }
}
