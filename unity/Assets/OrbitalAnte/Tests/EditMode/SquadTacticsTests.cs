using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    public class SquadTacticsTests
    {
        static SquadMemberState Fighter(Vec3d position) => new SquadMemberState {
            Alive = true, Armed = true, ReadyToFire = true, CanMove = true, CanFlank = true, Position = position };
        static SquadTactics Squad(int count = 8, int attackers = 3)
        {
            var squad = new SquadTactics(count, attackers, 5, 0.5, 1.3, 0.35, 2.5);
            for (int i = 0; i < count; i++)
            {
                Assert.True(squad.Register(i));
                squad.UpdateMember(i, Fighter(new Vec3d((i % 8 - 3.5) * 3, 0, 14 + i / 8 * 1.5)));
            }
            return squad;
        }
        static void SeeAll(SquadTactics squad, double now, Vec3d target)
        { for (int i = 0; i < squad.Count; i++) squad.ReportSight(i, target, now); squad.Tick(now); }
        static TacticalGrid OpenGrid() => new TacticalGrid(-30, -30, 30, 30, 1.5, 0.7, null);

        [Test]
        public void OnlySightReportsUpdateSharedMemory_AndExpiryStopsPursuitAndFiring()
        {
            var squad = Squad(); Vec3d sight = new Vec3d(4, 1.3, -2);
            squad.ReportSight(2, sight, 1); squad.Tick(1);
            Assert.True(squad.TryKnownTarget(2, out Vec3d known)); Assert.AreEqual(sight, known);
            squad.ReportHidden(2);
            Assert.False(squad.SeesTarget(2, 2));
            Assert.True(squad.TryKnownTarget(5.9, out known)); Assert.AreEqual(sight, known);
            squad.Reserve(2, new Vec3d(8, 0, 8));
            squad.Tick(6.01);
            Assert.False(squad.TryKnownTarget(6.01, out _));
            for (int i = 0; i < squad.Count; i++)
            { Assert.AreEqual(TacticalRole.Hold, squad.RoleOf(i)); Assert.False(squad.HasFirePermit(i, 6.01)); }
            Assert.True(squad.ReservationFree(3, new Vec3d(8, 0, 8), 2));
        }

        [Test]
        public void DeadOrInvalidReportsCannotRevealTheTarget()
        {
            var squad = Squad(1); var state = Fighter(Vec3d.Zero); state.Alive = false;
            squad.UpdateMember(0, state); squad.ReportSight(0, Vec3d.Forward, 1);
            Assert.False(squad.TryKnownTarget(1, out _));
            state.Alive = true; squad.UpdateMember(0, state);
            squad.ReportSight(0, new Vec3d(double.NaN, 0, 0), 1);
            squad.ReportSight(0, Vec3d.Zero, double.PositiveInfinity);
            Assert.False(squad.TryKnownTarget(1, out _));
        }

        [Test]
        public void SixtyFourMembersHaveBoundedRotatingLeases_AndEveryEligibleMemberGetsATurn()
        {
            var squad = Squad(64); var served = new HashSet<int>();
            Assert.False(squad.Register(64));
            for (int tick = 0; tick < 400; tick++)
            {
                double now = tick * 0.1; SeeAll(squad, now, Vec3d.Zero);
                int active = 0;
                for (int i = 0; i < 64; i++) if (squad.HasFirePermit(i, now)) { active++; served.Add(i); }
                Assert.LessOrEqual(active, 3);
            }
            Assert.AreEqual(64, served.Count);
            squad.Remove(0); Assert.True(squad.Register(64)); Assert.AreEqual(64, squad.Count);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void LostSightReloadDisarmOrDeathReleasesTheLeaseImmediately(int change)
        {
            var squad = Squad(2, 1); SeeAll(squad, 0, Vec3d.Zero);
            Assert.True(squad.HasFirePermit(0, 0));
            if (change == 0) squad.ReportHidden(0);
            else
            {
                var state = Fighter(new Vec3d(0, 0, 10)); state.ReadyToFire = false;
                if (change == 2) state.Armed = false;
                if (change == 3) state.Alive = false;
                squad.UpdateMember(0, state);
            }
            Assert.False(squad.HasFirePermit(0, 0));
            squad.Tick(0.01); Assert.True(squad.HasFirePermit(1, 0.01));
        }

        [Test]
        public void StalePersonalSightCannotUseAnotherMembersFreshReportToKeepFiring()
        {
            var squad = Squad(2, 1); SeeAll(squad, 0, Vec3d.Zero);
            squad.ReportSight(1, new Vec3d(1, 0, 0), 0.6); squad.Tick(0.6);
            Assert.True(squad.TryKnownTarget(0.6, out _));
            Assert.False(squad.HasFirePermit(0, 0.6)); Assert.True(squad.HasFirePermit(1, 0.6));
        }

        [Test]
        public void BodyAbilityReassignsFlanking_WhileReloadKeepsStableRoles()
        {
            var squad = Squad(); SeeAll(squad, 0, Vec3d.Zero);
            Assert.AreEqual(1, Enumerable.Range(0, 8).Count(i => squad.RoleOf(i) == TacticalRole.Anchor));
            int left = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankLeft);
            int right = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankRight);
            var state = Fighter(new Vec3d(-5, 0, 15)); state.ReadyToFire = false;
            squad.UpdateMember(left, state); squad.Tick(0.1);
            Assert.AreEqual(TacticalRole.FlankLeft, squad.RoleOf(left), "a reload does not shuffle the formation");
            state.CanFlank = false; squad.UpdateMember(left, state); squad.Tick(0.2);
            Assert.AreEqual(TacticalRole.Support, squad.RoleOf(left), "a slower injured shooter supports");
            Assert.AreEqual(TacticalRole.FlankRight, squad.RoleOf(right));
            state.Armed = false; squad.UpdateMember(left, state); squad.Tick(0.3);
            Assert.AreEqual(TacticalRole.Recover, squad.RoleOf(left));
            state.CanMove = false; squad.UpdateMember(left, state); squad.Tick(0.4);
            Assert.AreEqual(TacticalRole.Hold, squad.RoleOf(left), "a head alone has no combat destination");
        }

        [Test]
        public void SmallTargetMovesKeepTheFront_LargeCrossingsUpdateItAtTheRoleInterval()
        {
            var squad = Squad(); SeeAll(squad, 0, Vec3d.Zero); Vec3d front = squad.Front;
            SeeAll(squad, 3, new Vec3d(1, 0, 0)); Assert.AreEqual(front, squad.Front);
            SeeAll(squad, 4, new Vec3d(0, 0, 35)); Assert.AreEqual(front, squad.Front, "hysteresis is not evaluated each frame");
            SeeAll(squad, 6, new Vec3d(0, 0, 35)); Assert.Less(Vec3d.Dot(front, squad.Front), -0.9);
            int left = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankLeft);
            int right = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankRight);
            Assert.Less(Vec3d.Dot(squad.PreferredDirection(left), squad.PreferredDirection(right)), -0.7);
        }

        [Test]
        public void SurvivingFlankersPromoteAnAnchor_AfterThreeToTwoToOneCasualties()
        {
            var squad = Squad(3); SeeAll(squad, 0, Vec3d.Zero);
            int anchor = Enumerable.Range(0, 3).Single(i => squad.RoleOf(i) == TacticalRole.Anchor);
            squad.Remove(anchor); squad.Tick(0.1);
            int[] alive = Enumerable.Range(0, 3).Where(i => i != anchor).ToArray();
            Assert.AreEqual(1, alive.Count(i => squad.RoleOf(i) == TacticalRole.Anchor));
            int nextAnchor = alive.Single(i => squad.RoleOf(i) == TacticalRole.Anchor);
            squad.Remove(nextAnchor); squad.Tick(0.2);
            Assert.AreEqual(TacticalRole.Anchor, squad.RoleOf(alive.Single(i => i != nextAnchor)));
        }

        [Test]
        public void ThirtyTwoPlannedMembersGetDistinctReachableReservedGoals_Deterministically()
        {
            var grid = OpenGrid(); var first = Squad(32); var second = Squad(32);
            SeeAll(first, 0, Vec3d.Zero); SeeAll(second, 0, Vec3d.Zero);
            var planner = new TacticalPlanner(); var route = new List<Vec3d>(); var goals = new List<Vec3d>();
            for (int i = 0; i < 32; i++)
            {
                Vec3d from = new Vec3d((i % 8 - 3.5) * 3, 0, 14 + i / 8 * 1.5);
                bool found = planner.Plan(grid, first, i, from, Vec3d.Zero, 14 + i % 4 * 3, 1.5,
                    Vec3d.Zero, false, false, 32, 4096, _ => true, route, out Vec3d goal);
                Assert.True(found, "member " + i);
                foreach (Vec3d other in goals) Assert.GreaterOrEqual(Vec3d.Distance(other, goal), 1.5);
                for (int n = 1; n < route.Count; n++) Assert.True(grid.SegmentClear(route[n - 1], route[n]));
                Assert.LessOrEqual(planner.LastCandidates, 32); Assert.LessOrEqual(planner.LastExpanded, 4096);
                goals.Add(goal);
                Assert.True(planner.Plan(grid, second, i, from, Vec3d.Zero, 14 + i % 4 * 3, 1.5,
                    Vec3d.Zero, false, false, 32, 4096, _ => true, route, out Vec3d repeat));
                Assert.AreEqual(goal, repeat);
            }
        }

        [Test]
        public void AFlankGoalChangesAngle_IsReachable_AndRejectsTheBlockedIdealFiringPoint()
        {
            var grid = OpenGrid(); var squad = Squad(); SeeAll(squad, 0, Vec3d.Zero);
            int left = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankLeft);
            var planner = new TacticalPlanner(); var route = new List<Vec3d>();
            Func<Vec3d, bool?> line = p => p.Z > 5 && p.X < -5;
            Assert.True(planner.Plan(grid, squad, left, new Vec3d(0, 0, 20), Vec3d.Zero, 11, 2,
                Vec3d.Zero, false, true, 24, 2048, line, route, out Vec3d goal));
            Assert.True(line(goal).Value); Assert.Less(goal.X, -5);
            Assert.Less(Vec3d.Dot(goal.Normalized, squad.Front), 0.9);
            Assert.Greater(route.Count, 1);
            for (int i = 1; i < route.Count; i++) Assert.True(grid.SegmentClear(route[i - 1], route[i]));
        }

        [Test]
        public void FlankersCannotAcceptAFrontalFiringPoint_EvenWhenItIsTheOnlyVisibleCandidate()
        {
            var squad = Squad(); SeeAll(squad, 0, Vec3d.Zero);
            int left = Enumerable.Range(0, 8).Single(i => squad.RoleOf(i) == TacticalRole.FlankLeft);
            var planner = new TacticalPlanner(); var route = new List<Vec3d>();
            Assert.False(planner.Plan(OpenGrid(), squad, left, new Vec3d(0, 0, 20), Vec3d.Zero, 11, 2,
                Vec3d.Zero, false, true, 32, 2048,
                p => Vec3d.Dot(p.Normalized, squad.Front) > Math.Cos(30 * Math.PI / 180), route, out _, false, 35));
            Assert.IsEmpty(route);
        }

        [Test]
        public void UnknownOrBlockedCandidateSightNeverCreatesAnAttackGoal_AndCoverRequiresOcclusion()
        {
            var grid = OpenGrid(); var squad = Squad(); SeeAll(squad, 0, Vec3d.Zero);
            var planner = new TacticalPlanner(); var route = new List<Vec3d>();
            foreach (bool? sight in new bool?[] { null, false })
            {
                Assert.False(planner.Plan(grid, squad, 0, new Vec3d(0, 0, 20), Vec3d.Zero, 14, 2,
                    Vec3d.Zero, false, true, 8, 32, _ => sight, route, out _));
                Assert.IsEmpty(route); Assert.LessOrEqual(planner.LastCandidates, 8); Assert.AreEqual(0, planner.LastExpanded);
            }
            Assert.False(planner.Plan(grid, squad, 0, new Vec3d(0, 0, 20), Vec3d.Zero, 14, 2,
                Vec3d.Zero, false, true, 8, 32, _ => true, route, out _, true));
            Assert.True(planner.Plan(grid, squad, 0, new Vec3d(0, 0, 20), Vec3d.Zero, 14, 2,
                Vec3d.Zero, false, true, 8, 2048, _ => false, route, out _, true));
        }

        [Test]
        public void PerFramePhysicsLanesCannotExceedHardCaps_AndResetOnTheNextFrame()
        {
            var budget = new TacticalQueryBudget(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
            int[] expected = { 16, 32, 32, 256 };
            for (int lane = 0; lane < 4; lane++)
            {
                int allowed = 0;
                for (int i = 0; i < 1000; i++) if (budget.Take((TacticalQuery)lane, 8)) allowed++;
                Assert.AreEqual(expected[lane], allowed); Assert.AreEqual(expected[lane], budget.Used((TacticalQuery)lane));
            }
            Assert.True(budget.Take(TacticalQuery.Sense, 9)); Assert.AreEqual(1, budget.Used(TacticalQuery.Sense));
            Assert.AreEqual(0, budget.Used(TacticalQuery.Plan));
        }

        [TestCase(0.533)] [TestCase(1.066)]
        public void DeferredSensorGapsDoNotRestartReaction_ButConfirmedHiddenReacquisitionDoes(double cadence)
        {
            var reaction = new SightReaction(); var squad = Squad(1, 1); bool fired = false;
            for (int i = 0; i < 4; i++)
            {
                double now = i * cadence;
                reaction.ConfirmVisible(now, 0.6);
                var member = Fighter(new Vec3d(0, 0, 10)); member.ReadyToFire = reaction.Ready(now);
                squad.UpdateMember(0, member); squad.ReportSight(0, Vec3d.Zero, now); squad.Tick(now);
                fired |= squad.HasFirePermit(0, now);
                Assert.False(squad.HasFirePermit(0, now + 0.51), "stale sight still withholds shots");
            }
            Assert.True(fired, "continuous confirmed visibility eventually finishes reaction, even with deferred samples");
            reaction.ConfirmHidden(); Assert.False(reaction.Ready(10));
            reaction.ConfirmVisible(10, 0.6);
            Assert.False(reaction.Ready(10.59)); Assert.True(reaction.Ready(10.61));
        }
    }
}
