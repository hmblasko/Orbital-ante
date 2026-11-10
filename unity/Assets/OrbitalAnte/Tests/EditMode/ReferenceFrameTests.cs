using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The point-of-view sums that let the player stand on a planet that is really hurtling round the Sun.
    /// The key promise: motion comes out the same whichever point of view is used. These run without Unity objects.
    /// </summary>
    public class ReferenceFrameTests
    {
        SolarSystemModel _model;
        BodyState[] _states;

        [SetUp]
        public void SetUp()
        {
            _model = new SolarSystemModel(SolarSystemDefaults.Bodies(), 11);
            _states = _model.NewStateBuffer();
            _model.Evaluate(321.0, _states);
        }

        static void AssertClose(Vec3d expected, Vec3d actual, double tolerance, string message = null)
        {
            Assert.AreEqual(0, Vec3d.Distance(expected, actual), tolerance, (message ?? string.Empty) + " expected " + expected + " got " + actual);
        }

        ReferenceFrame FrameOn(string id, bool rotating)
        {
            var frame = new ReferenceFrame(_model);
            frame.SetAnchor(_model.IndexOf(id), _states);
            if (rotating) frame.StartRotating(_states);
            return frame;
        }

        [Test]
        public void PositionsAndVelocitiesSurviveARoundTrip()
        {
            foreach (bool rotating in new[] { false, true })
            {
                ReferenceFrame frame = FrameOn("Earth", rotating);
                frame.Recenter(new Vec3d(120, -40, 610));
                _model.Evaluate(400.0, _states);   // some time later, so a rotating frame has turned
                var position = _states[_model.IndexOf("Earth")].Position + new Vec3d(300, 520, -80);
                var velocity = new Vec3d(-12, 44, 180);
                var spin = new Vec3d(0.3, -0.2, 0.1);

                Vec3d local = frame.ToLocalPosition(position, _states);
                Vec3d localVelocity = frame.ToLocalVelocity(position, velocity, _states);
                AssertClose(position, frame.ToTruePosition(local, _states), 1e-6);
                AssertClose(velocity, frame.ToTrueVelocity(local, localVelocity, _states), 1e-6);
                AssertClose(spin, frame.ToTrueSpin(frame.ToLocalSpin(spin, _states), _states), 1e-9);
                AssertClose(velocity.Normalized, frame.ToTrueDirection(frame.ToLocalDirection(velocity.Normalized, _states), _states), 1e-9);
            }
        }

        [Test]
        public void TheAnchorIsStillInItsOwnFrame()
        {
            int earth = _model.IndexOf("Earth");
            ReferenceFrame frame = FrameOn("Earth", false);
            Vec3d where = frame.ToLocalPosition(_states[earth].Position, _states);
            AssertClose(Vec3d.Zero, frame.ToLocalVelocity(_states[earth].Position, _states[earth].Velocity, _states), 1e-9);
            // ...while the Sun appears to move the other way.
            Vec3d sunSeen = frame.ToLocalVelocity(_states[0].Position, _states[0].Velocity, _states);
            Assert.AreEqual(_model.OrbitSpeed(earth), sunSeen.Magnitude, 1e-6);
            // A minute later Earth has gone a long way round the Sun, but from here it has not moved at all.
            _model.Evaluate(381.0, _states);
            AssertClose(where, frame.ToLocalPosition(_states[earth].Position, _states), 1e-6);
        }

        [Test]
        public void GroundStandsStillWhenTurningWithThePlanet()
        {
            int earth = _model.IndexOf("Earth");
            ReferenceFrame frame = FrameOn("Earth", true);
            // A rock fixed to the equator, tracked as the planet turns.
            Vec3d bodyPoint = _model.SpinAxis(earth).AnyPerpendicular() * _model.Spec(earth).radius;
            QuatD atLock = _model.Orientation(earth, _states[earth]);
            Vec3d surfaceLocal = atLock.Inverse.Rotate(bodyPoint);
            Vec3d first = Vec3d.Zero;
            for (int step = 0; step < 4; step++)
            {
                double time = 321.0 + step * 50.0;
                _model.Evaluate(time, _states);
                Vec3d truePosition = _states[earth].Position + _model.Orientation(earth, _states[earth]).Rotate(surfaceLocal);
                Vec3d trueVelocity = _states[earth].Velocity +
                                     Vec3d.Cross(_model.SpinAxis(earth) * _model.SpinRate(earth), truePosition - _states[earth].Position);
                Vec3d local = frame.ToLocalPosition(truePosition, _states);
                if (step == 0) first = local;
                AssertClose(first, local, 1e-6, "The rock moved in the turning frame.");
                AssertClose(Vec3d.Zero, frame.ToLocalVelocity(truePosition, trueVelocity, _states), 1e-9, "The rock has speed in the turning frame.");
            }
        }

        [Test]
        public void ChangingPointOfViewNeverMovesAnything()
        {
            var frame = new ReferenceFrame(_model);
            int earth = _model.IndexOf("Earth");
            int moon = _model.IndexOf("Moon");
            Vec3d point = _states[moon].Position + new Vec3d(40, 260, -15);
            Vec3d velocity = new Vec3d(3, -8, 20);

            Vec3d seen = frame.ToLocalPosition(point, _states);
            frame.SetAnchor(earth, _states);
            AssertClose(seen, frame.ToLocalPosition(point, _states), 1e-6, "Sun to Earth");
            frame.SetAnchor(moon, _states);
            AssertClose(seen, frame.ToLocalPosition(point, _states), 1e-6, "Earth to Moon");
            frame.StartRotating(_states);
            AssertClose(seen, frame.ToLocalPosition(point, _states), 1e-6, "Start turning");

            _model.Evaluate(380.0, _states);   // turn with the Moon for a while
            point = frame.ToTruePosition(seen, _states);
            Vec3d localVelocity = frame.ToLocalVelocity(point, velocity, _states);
            frame.StopRotating(_states);
            AssertClose(seen, frame.ToLocalPosition(point, _states), 1e-6, "Stop turning");
            AssertClose(velocity, frame.ToTrueVelocity(seen, frame.ToLocalVelocity(point, velocity, _states), _states), 1e-6);
            Assert.Greater(Vec3d.Distance(localVelocity, frame.ToLocalVelocity(point, velocity, _states)), 1e-4,
                "Velocity is measured differently once the frame stops turning.");
            frame.SetAnchor(0, _states);
            AssertClose(seen, frame.ToLocalPosition(point, _states), 1e-6, "Back to the Sun");
        }

        [Test]
        public void RecenterShiftsEverythingByTheSameAmount()
        {
            ReferenceFrame frame = FrameOn("Mars", true);
            Vec3d a = _states[_model.IndexOf("Mars")].Position + new Vec3d(400, 10, 0);
            Vec3d b = _states[_model.IndexOf("Phobos")].Position;
            Vec3d la = frame.ToLocalPosition(a, _states);
            Vec3d lb = frame.ToLocalPosition(b, _states);
            Vec3d va = frame.ToLocalVelocity(a, new Vec3d(1, 2, 3), _states);
            frame.Recenter(la);
            AssertClose(Vec3d.Zero, frame.ToLocalPosition(a, _states), 1e-6);
            AssertClose(lb - la, frame.ToLocalPosition(b, _states), 1e-6);
            AssertClose(va, frame.ToLocalVelocity(a, new Vec3d(1, 2, 3), _states), 1e-9, "Velocities are untouched.");
        }

        /// <summary>
        /// The big one. A ship coasting past a planet is simulated twice: once in true coordinates, and once from
        /// the chosen point of view with the frame's extra pushes. Both must end in the same place.
        /// </summary>
        [TestCase("Earth", false)]
        [TestCase("Earth", true)]
        [TestCase("Moon", true)]
        [TestCase("Jupiter", true)]
        [TestCase("Sun", false)]
        public void MotionIsTheSameFromEveryPointOfView(string anchor, bool rotating)
        {
            const double start = 321.0;
            const double dt = 0.005;
            const int steps = 4000;   // twenty seconds
            int body = _model.IndexOf(anchor == "Sun" ? "Mars" : anchor);
            // Whose neighbourhood the ship counts as being in. Fixed for the whole test; any choice must work.
            int hood = _model.IndexOf(anchor);

            _model.Evaluate(start, _states);
            ReferenceFrame frame = FrameOn(anchor, rotating);
            frame.Recenter(new Vec3d(50, 60, 70));

            double altitude = _model.Spec(body).radius * 1.3;
            Vec3d truePosition = _states[body].Position + new Vec3d(altitude, 30, 0);
            Vec3d trueVelocity = _states[body].Velocity + new Vec3d(5, 12, _model.CircularOrbitSpeed(body, altitude) * 0.9);

            Vec3d localPosition = frame.ToLocalPosition(truePosition, _states);
            Vec3d localVelocity = frame.ToLocalVelocity(truePosition, trueVelocity, _states);

            BodyState[] mid = _model.NewStateBuffer();
            BodyState[] end = _model.NewStateBuffer();
            for (int i = 0; i < steps; i++)
            {
                double t = start + i * dt;
                _model.Evaluate(t, _states);
                _model.Evaluate(t + dt * 0.5, mid);
                _model.Evaluate(t + dt, end);

                // True coordinates, fourth-order steps.
                Vec3d TrueAcc(Vec3d p, BodyState[] s) => _model.AccelerationAt(p, s, hood);
                Vec3d k1v = TrueAcc(truePosition, _states);
                Vec3d k1p = trueVelocity;
                Vec3d k2v = TrueAcc(truePosition + k1p * (dt / 2), mid);
                Vec3d k2p = trueVelocity + k1v * (dt / 2);
                Vec3d k3v = TrueAcc(truePosition + k2p * (dt / 2), mid);
                Vec3d k3p = trueVelocity + k2v * (dt / 2);
                Vec3d k4v = TrueAcc(truePosition + k3p * dt, end);
                Vec3d k4p = trueVelocity + k3v * dt;
                truePosition += (k1p + 2 * k2p + 2 * k3p + k4p) * (dt / 6);
                trueVelocity += (k1v + 2 * k2v + 2 * k3v + k4v) * (dt / 6);

                // The same, from the frame's point of view.
                Vec3d LocalAcc(Vec3d p, Vec3d v, BodyState[] s)
                {
                    Vec3d gravity = _model.AccelerationAt(frame.ToTruePosition(p, s), s, hood);
                    return frame.ToLocalAcceleration(p, v, gravity, s);
                }
                Vec3d l1v = LocalAcc(localPosition, localVelocity, _states);
                Vec3d l1p = localVelocity;
                Vec3d l2v = LocalAcc(localPosition + l1p * (dt / 2), localVelocity + l1v * (dt / 2), mid);
                Vec3d l2p = localVelocity + l1v * (dt / 2);
                Vec3d l3v = LocalAcc(localPosition + l2p * (dt / 2), localVelocity + l2v * (dt / 2), mid);
                Vec3d l3p = localVelocity + l2v * (dt / 2);
                Vec3d l4v = LocalAcc(localPosition + l3p * dt, localVelocity + l3v * dt, end);
                Vec3d l4p = localVelocity + l3v * dt;
                localPosition += (l1p + 2 * l2p + 2 * l3p + l4p) * (dt / 6);
                localVelocity += (l1v + 2 * l2v + 2 * l3v + l4v) * (dt / 6);
            }

            _model.Evaluate(start + steps * dt, _states);
            Assert.Greater(Vec3d.Distance(truePosition, _states[body].Position), _model.Spec(body).radius, "The test ship should not have hit the ground.");
            AssertClose(truePosition, frame.ToTruePosition(localPosition, _states), 1e-3, "position");
            AssertClose(trueVelocity, frame.ToTrueVelocity(localPosition, localVelocity, _states), 1e-4, "velocity");
        }

        /// <summary>
        /// A rehearsal of what the game does every physics step, with the same simple arithmetic the physics
        /// engine uses (fifty steps a second), on a looping orbit that dips in and out of the zone where the world
        /// turns with the planet. Each time the point of view changes, velocity is re-measured the way the game
        /// does it. After five minutes the craft must still be where a careful calculation says it should be.
        /// </summary>
        [Test]
        public void AGameLikeRunThroughChangingPointsOfViewStaysOnCourse()
        {
            const double start = 50.0;
            const double dt = 0.02;
            const int steps = 15000;   // five minutes
            int earth = _model.IndexOf("Earth");
            _model.Evaluate(start, _states);

            double low = _model.Spec(earth).radius + 90;       // inside the turning zone
            double high = _model.LockRadius(earth) * 1.6;      // well outside it
            double semiMajor = (low + high) / 2;
            double speedAtLow = Math.Sqrt(_model.Mu(earth) * (2 / low - 1 / semiMajor));
            Vec3d truePosition = _states[earth].Position + new Vec3d(low, 0, 0);
            Vec3d trueVelocity = _states[earth].Velocity + new Vec3d(0, 0, speedAtLow);

            var frame = new ReferenceFrame(_model);
            frame.Follow(truePosition, _states);
            frame.Recenter(frame.ToLocalPosition(truePosition, _states));
            Vec3d local = frame.ToLocalPosition(truePosition, _states);
            Vec3d localVelocity = frame.ToLocalVelocity(truePosition, trueVelocity, _states);
            Assert.IsTrue(frame.Rotating);

            // A second copy of the craft that keeps one steady point of view (Earth's, not turning) throughout.
            var steady = new ReferenceFrame(_model);
            steady.SetAnchor(earth, _states);
            Vec3d steadyLocal = steady.ToLocalPosition(truePosition, _states);
            Vec3d steadyVelocity = steady.ToLocalVelocity(truePosition, trueVelocity, _states);

            // The careful calculation: small fourth-order steps in true coordinates, Earth's pull plus the Sun's.
            BodyState[] mid = _model.NewStateBuffer();
            BodyState[] end = _model.NewStateBuffer();
            int changes = 0;
            int recentres = 0;
            double lowest = double.MaxValue, highest = 0;
            for (int i = 0; i < steps; i++)
            {
                double t = start + i * dt;
                _model.Evaluate(t, _states);
                _model.Evaluate(t + dt * 0.5, mid);
                _model.Evaluate(t + dt, end);

                Vec3d k1v = _model.AccelerationAt(truePosition, _states, earth);
                Vec3d k1p = trueVelocity;
                Vec3d k2v = _model.AccelerationAt(truePosition + k1p * (dt / 2), mid, earth);
                Vec3d k2p = trueVelocity + k1v * (dt / 2);
                Vec3d k3v = _model.AccelerationAt(truePosition + k2p * (dt / 2), mid, earth);
                Vec3d k3p = trueVelocity + k2v * (dt / 2);
                Vec3d k4v = _model.AccelerationAt(truePosition + k3p * dt, end, earth);
                Vec3d k4p = trueVelocity + k3v * dt;

                // The game's step, exactly as SolarSystem.Step does it.
                Vec3d gameTrue = frame.ToTruePosition(local, _states);
                Vec3d gameTrueVelocity = frame.ToTrueVelocity(local, localVelocity, _states);
                bool changed = frame.Follow(gameTrue, _states);
                bool recentre = local.Magnitude > 1000;   // the game uses 2000; smaller here so it happens on this small orbit
                if (recentre) frame.Recenter(local);
                if (changed || recentre)
                {
                    if (changed) changes++;
                    if (recentre) recentres++;
                    local = frame.ToLocalPosition(gameTrue, _states);
                    localVelocity = frame.ToLocalVelocity(gameTrue, gameTrueVelocity, _states);
                }
                Vec3d push = frame.ToLocalAccelerationForStep(local, localVelocity,
                    _model.AccelerationAt(frame.ToTruePosition(local, _states), _states, frame.Anchor), _states, dt);
                localVelocity += push * dt;      // the physics engine's order: speed first,
                local += localVelocity * dt;     // then position from the new speed

                Vec3d steadyPush = steady.ToLocalAcceleration(steadyLocal, steadyVelocity,
                    _model.AccelerationAt(steady.ToTruePosition(steadyLocal, _states), _states, earth), _states);
                steadyVelocity += steadyPush * dt;
                steadyLocal += steadyVelocity * dt;

                truePosition += (k1p + 2 * k2p + 2 * k3p + k4p) * (dt / 6);
                trueVelocity += (k1v + 2 * k2v + 2 * k3v + k4v) * (dt / 6);

                double r = Vec3d.Distance(truePosition, end[earth].Position);
                lowest = Math.Min(lowest, r);
                highest = Math.Max(highest, r);
            }

            _model.Evaluate(start + steps * dt, _states);
            Assert.GreaterOrEqual(changes, 4, "The orbit should have crossed in and out of the turning zone several times.");
            Assert.GreaterOrEqual(recentres, 1, "The craft should have been brought back to zero at least once.");
            Assert.Greater(lowest, _model.Spec(earth).radius, "The orbit should clear the ground.");
            Vec3d game = frame.ToTruePosition(local, _states);
            Vec3d steadyTrue = steady.ToTruePosition(steadyLocal, _states);
            // Fifty simple steps a second drift a little along the orbit compared with the careful calculation.
            // That is the physics engine's ordinary coarseness and is the same from any point of view...
            double coarseness = Vec3d.Distance(truePosition, steadyTrue);
            Assert.Less(coarseness, 20.0, "Simple stepping drifted " + coarseness.ToString("0.0") + " m from the careful answer.");
            // ...what matters here is that changing point of view (and recentring) adds almost nothing on top.
            double added = Vec3d.Distance(game, steadyTrue);
            Assert.Less(added, 5.0, "Changing point of view " + changes + " times pushed the craft " + added.ToString("0.0") + " m off course.");
            double gameRadius = Vec3d.Distance(game, _states[earth].Position);
            Assert.Greater(gameRadius, lowest - 5);
            Assert.Less(gameRadius, highest + 5);
        }

        [Test]
        public void ParkedNextToAPlanetYouFeelOnlyThatPlanet()
        {
            // Match speed with Mars and the Sun carries you both round together: from Mars's point of view the
            // only pull left is Mars's own (and its moons'), however far out in its neighbourhood you sit.
            int mars = _model.IndexOf("Mars");
            ReferenceFrame frame = FrameOn("Mars", false);
            Vec3d parked = _states[mars].Position + new Vec3d(0, _model.InfluenceRadius(mars) * 0.9, 0);
            Vec3d felt = frame.ToLocalAcceleration(frame.ToLocalPosition(parked, _states), Vec3d.Zero,
                _model.AccelerationAt(parked, _states, mars), _states);
            Vec3d marsAndMoons = _model.GravityFrom(mars, parked, _states)
                                 + _model.GravityFrom(_model.IndexOf("Phobos"), parked, _states)
                                 + _model.GravityFrom(_model.IndexOf("Deimos"), parked, _states);
            AssertClose(frame.ToLocalDirection(marsAndMoons, _states), felt, 1e-9);
            Assert.Less(felt.Magnitude, 0.5, "This far out, Mars barely pulls.");
        }

        [Test]
        public void FollowPicksTheAnchorAndWhenToTurn()
        {
            var frame = new ReferenceFrame(_model);
            int earth = _model.IndexOf("Earth");
            double radius = _model.Spec(earth).radius;

            Vec3d far = _states[earth].Position + new Vec3d(0, _model.InfluenceRadius(earth) * 3, 0);
            Assert.IsFalse(frame.Follow(far, _states));
            Assert.AreEqual(0, frame.Anchor);

            Vec3d high = _states[earth].Position + new Vec3d(0, _model.LockRadius(earth) * 1.5, 0);
            Assert.IsTrue(frame.Follow(high, _states));
            Assert.AreEqual(earth, frame.Anchor);
            Assert.IsFalse(frame.Rotating, "High above: ride along but do not turn.");

            Vec3d low = _states[earth].Position + new Vec3d(0, radius + 20, 0);
            Assert.IsTrue(frame.Follow(low, _states));
            Assert.IsTrue(frame.Rotating, "Near the ground: turn with it.");
            Assert.IsFalse(frame.Follow(low, _states), "Nothing changes when nothing needs to.");

            Vec3d justAbove = _states[earth].Position + new Vec3d(0, _model.LockRadius(earth) * 1.05, 0);
            frame.Follow(justAbove, _states);
            Assert.IsTrue(frame.Rotating, "A little above the line still counts, so it does not flicker.");

            frame.Follow(high, _states);
            Assert.IsFalse(frame.Rotating);
            frame.Follow(far, _states);
            Assert.AreEqual(0, frame.Anchor);
        }

        [Test]
        public void TheSunNeverTurnsTheFrame()
        {
            var frame = new ReferenceFrame(_model);
            Vec3d skimming = new Vec3d(_model.Spec(0).radius + 5, 0, 0);
            frame.Follow(skimming, _states);
            Assert.AreEqual(0, frame.Anchor);
            Assert.IsFalse(frame.Rotating);
        }

        [Test]
        public void VectorAndRotationBasics()
        {
            var v = new Vec3d(1, 0, 0);
            AssertClose(new Vec3d(0, 0, -1), v.RotatedAround(Vec3d.Up, Math.PI / 2), 1e-12);
            QuatD q = QuatD.AngleAxis(Vec3d.Up, Math.PI / 2);
            AssertClose(v.RotatedAround(Vec3d.Up, Math.PI / 2), q.Rotate(v), 1e-12, "Both ways of turning agree.");
            AssertClose(v, q.Inverse.Rotate(q.Rotate(v)), 1e-12);
            QuatD twice = q * q;
            AssertClose(new Vec3d(-1, 0, 0), twice.Rotate(v), 1e-12);
            AssertClose(new Vec3d(0, 0, 1), QuatD.FromTo(Vec3d.Up, Vec3d.Forward).Rotate(Vec3d.Up), 1e-12);
            AssertClose(new Vec3d(0, -1, 0), QuatD.FromTo(Vec3d.Up, -Vec3d.Up).Rotate(Vec3d.Up), 1e-9);
            Assert.AreEqual(0, Vec3d.Dot(new Vec3d(0.3, 2, -1).AnyPerpendicular(), new Vec3d(0.3, 2, -1)), 1e-12);
        }
    }
}
