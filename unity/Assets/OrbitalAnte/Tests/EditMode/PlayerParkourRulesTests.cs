using NUnit.Framework;
using OrbitalAnte.Player;

namespace OrbitalAnte.Tests.EditMode
{
    public sealed class PlayerParkourRulesTests
    {
        [Test]
        public void ExhaustedWallRunCannotRestartOnTheSameOrADifferentWall()
        {
            var state = new ParkourState();
            Assert.IsTrue(state.BeginWall(7, 0.5f));
            state.Tick(0.25f);
            Assert.AreEqual(0.25f, state.WallSecondsLeft);
            state.Tick(0.25f);
            Assert.IsFalse(state.WallRunning);
            Assert.AreEqual(0f, state.WallSecondsLeft);
            Assert.IsFalse(state.BeginWall(7, 0.5f));
            Assert.IsFalse(state.BeginWall(8, 0.5f));
        }

        [Test]
        public void ReleasingOrJumpingAwayDoesNotRefundAWallRun()
        {
            var state = new ParkourState();
            Assert.IsTrue(state.BeginWall(-23, 1f), "rule identifiers are opaque nonzero tokens");
            Assert.AreEqual(-23, state.ActiveWallId);
            state.StopWall();
            state.StopWall();
            state.Tick(10f);
            Assert.AreEqual(0, state.ActiveWallId);
            Assert.IsFalse(state.WallAvailable(-23));
            Assert.IsFalse(state.WallAvailable(24));
        }

        [Test]
        public void LandingResetsBothBudgetsAndStopsActiveTraversal()
        {
            var state = new ParkourState();
            Assert.IsTrue(state.BeginWall(2, 1f));
            Assert.IsTrue(state.BeginClimb());
            Assert.IsFalse(state.WallRunning);
            state.Grounded();
            Assert.IsFalse(state.Climbing);
            Assert.IsTrue(state.ClimbAvailable);
            Assert.IsTrue(state.BeginWall(2, 1f));
            Assert.IsTrue(state.BeginClimb());
        }

        [Test]
        public void BlockedOrReleasedClimbCannotBeRepeatedInTheSameAirbornePeriod()
        {
            var state = new ParkourState();
            Assert.IsTrue(state.BeginClimb());
            Assert.IsFalse(state.BeginWall(3, 1f), "a wall run cannot interrupt an active climb");
            state.StopClimb(); // A moving blocker or released jump cancels a started climb.
            Assert.IsFalse(state.ClimbAvailable);
            Assert.IsFalse(state.BeginClimb());
            Assert.IsTrue(state.BeginWall(3, 1f), "an unused wall budget remains available");
        }

        [Test]
        public void ClimbAndWallRunAreMutuallyExclusive()
        {
            var state = new ParkourState();
            Assert.IsTrue(state.BeginWall(3, 1f));
            Assert.IsFalse(state.BeginWall(4, 1f));
            Assert.IsTrue(state.BeginClimb());
            Assert.IsFalse(state.WallRunning);
            Assert.AreEqual(0f, state.WallSecondsLeft);
            Assert.AreEqual(0, state.ActiveWallId);
            Assert.IsTrue(state.Climbing);
            Assert.IsFalse(state.BeginClimb());
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidDurationNeverSpendsTheWallBudget(float duration)
        {
            var state = new ParkourState();
            Assert.IsFalse(state.BeginWall(4, duration));
            Assert.IsTrue(state.WallAvailable(4));
            Assert.IsTrue(state.BeginWall(4, 0.5f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidTimeNeverExtendsOrConsumesAWallRun(float delta)
        {
            var state = new ParkourState();
            state.BeginWall(5, 0.5f);
            state.Tick(delta);
            Assert.IsTrue(state.WallRunning);
            Assert.AreEqual(0.5f, state.WallSecondsLeft);
            state.Tick(1f);
            Assert.IsFalse(state.WallRunning);
            Assert.AreEqual(0f, state.WallSecondsLeft);
        }

        [Test]
        public void AnInvalidWallIdDoesNotSpendTheBudget()
        {
            var state = new ParkourState();
            Assert.IsFalse(state.BeginWall(0, 1f));
            Assert.IsTrue(state.BeginWall(1, 1f));
            state.StopWall();
            Assert.IsFalse(state.BeginWall(1, 1f), "the motor's presence token cannot refresh an airborne budget");
            state.Grounded();
            Assert.IsTrue(state.BeginWall(1, 1f), "the same presence token is valid again after landing");
            state.Reset();
            Assert.IsTrue(state.BeginWall(2, 1f));
        }

        static bool Intent(bool allowed = true, bool grounded = false, bool sprint = true,
            float forward = 1f, float speed = 6f, float minSpeed = 4f, float normalDot = 0f,
            float maxNormalDot = 0.3f, float gravity = 9.81f, float minGravity = 1f, bool crouched = false) =>
            ParkourRules.WallIntent(allowed, grounded, sprint, forward, speed, minSpeed, normalDot,
                maxNormalDot, gravity, minGravity, crouched);

        [Test]
        public void OnlyAnAirborneForwardSprintAlongAWallCanRun()
        {
            Assert.IsTrue(Intent());
            Assert.IsFalse(Intent(allowed: false));
            Assert.IsFalse(Intent(grounded: true));
            Assert.IsFalse(Intent(sprint: false));
            Assert.IsFalse(Intent(forward: -1f));
            Assert.IsFalse(Intent(forward: 0f));
            Assert.IsFalse(Intent(speed: 3f));
            Assert.IsFalse(Intent(normalDot: 0.9f));
            Assert.IsFalse(Intent(normalDot: -0.9f));
            Assert.IsFalse(Intent(crouched: true));
            Assert.IsTrue(Intent(normalDot: -0.3f));
        }

        [Test]
        public void FreeSpaceAndInsufficientGravityCannotStartAWallRun()
        {
            Assert.IsFalse(Intent(gravity: 0f, minGravity: 0f));
            Assert.IsFalse(Intent(gravity: 0.5f));
            Assert.IsFalse(Intent(gravity: float.NaN));
            Assert.IsTrue(Intent(gravity: 1f));
        }

        [Test]
        public void ZeroMinimumDoesNotTurnFloatingIntoClimbingGravity()
        {
            Assert.IsFalse(ParkourRules.GravityAllows(0f, 0f));
            Assert.IsFalse(ParkourRules.GravityAllows(float.NaN, 0f));
            Assert.IsFalse(ParkourRules.GravityAllows(9.81f, float.PositiveInfinity));
            Assert.IsFalse(ParkourRules.GravityAllows(9.81f, -1f));
            Assert.IsTrue(ParkourRules.GravityAllows(1.62f, 1f));
        }

        [Test]
        public void MalformedIntentAndThresholdsFailClosed()
        {
            Assert.IsFalse(Intent(forward: float.NaN));
            Assert.IsFalse(Intent(forward: float.PositiveInfinity));
            Assert.IsFalse(Intent(speed: float.PositiveInfinity));
            Assert.IsFalse(Intent(minSpeed: float.NaN));
            Assert.IsFalse(Intent(minSpeed: -1f));
            Assert.IsFalse(Intent(normalDot: float.NaN));
            Assert.IsFalse(Intent(normalDot: 2f));
            Assert.IsFalse(Intent(maxNormalDot: 1f));
            Assert.IsFalse(Intent(maxNormalDot: -0.1f));
            Assert.IsFalse(Intent(minGravity: float.NaN));
            Assert.IsFalse(Intent(minGravity: -1f));
        }
    }
}
