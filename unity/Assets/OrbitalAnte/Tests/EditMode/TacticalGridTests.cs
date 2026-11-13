using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    public class TacticalGridTests
    {
        [Test]
        public void AStarFindsAClearDetourAroundAnInflatedWall_WithoutCornerCutting()
        {
            var grid = new TacticalGrid(-5, -5, 5, 5, 1, 0.4, new[] { new TacticalObstacle(-1, -2, 1, 2) });
            var route = new List<Vec3d>(); var from = new Vec3d(-4, 0, 0);
            int goal = grid.ClosestCell(new Vec3d(4, 0, 0), 1, true);
            Assert.False(grid.SegmentClear(from, grid.Position(goal)));
            Assert.True(grid.FindRoute(from, goal, route, 121));
            bool wentAround = false;
            for (int i = 1; i < route.Count; i++)
            {
                Assert.True(grid.SegmentClear(route[i - 1], route[i]));
                Vec3d step = route[i] - route[i - 1];
                Assert.True(Math.Abs(step.X) < 0.001 || Math.Abs(step.Z) < 0.001, "four-way links cannot cut a corner");
                if (Math.Abs(route[i].Z) >= 3) wentAround = true;
            }
            Assert.True(wentAround); Assert.Greater(route.Count, 9);
            var repeat = new List<Vec3d>(); Assert.True(grid.FindRoute(from, goal, repeat, 121));
            CollectionAssert.AreEqual(route, repeat);
        }

        [Test]
        public void AThinWallBetweenFreeEndpointsBlocksTheLink_AndASealedRegionHasNoRoute()
        {
            var thin = new TacticalGrid(0, 0, 1, 1, 1, 0.01, new[] { new TacticalObstacle(0.4, -1, 0.6, 2) });
            var route = new List<Vec3d>();
            Assert.True(thin.IsFree(0)); Assert.True(thin.IsFree(1));
            Assert.False(thin.FindRoute(Vec3d.Zero, 1, route, 4)); Assert.IsEmpty(route);
            var sealedGrid = new TacticalGrid(-5, -5, 5, 5, 1, 0.4, new[] { new TacticalObstacle(-6, -0.1, 6, 0.1) });
            int goal = sealedGrid.ClosestCell(new Vec3d(0, 0, 4), 1, true);
            Assert.False(sealedGrid.FindRoute(new Vec3d(0, 0, -4), goal, route, 121)); Assert.IsEmpty(route);
        }

        [Test]
        public void NodeExhaustionReturnsNoPartialRoute_AndAnInvalidSourceCannotSnapThroughAWall()
        {
            var grid = new TacticalGrid(-5, -5, 5, 5, 1, 0.5, new[] { new TacticalObstacle(-1, -1, 1, 1) });
            var route = new List<Vec3d>(); int goal = grid.ClosestCell(new Vec3d(4, 0, 4), 1, true);
            Assert.False(grid.FindRoute(new Vec3d(-4, 0, -4), goal, route, 1));
            Assert.AreEqual(1, grid.LastExpanded); Assert.IsEmpty(route);
            Assert.False(grid.FindRoute(Vec3d.Zero, goal, route, 121));
            Assert.AreEqual(0, grid.LastExpanded); Assert.IsEmpty(route);
            Assert.True(grid.FindRoute(new Vec3d(-4, 0, -4), goal, route, 121));
        }

        [Test]
        public void DimensionsAndObstacleCountsEnforceConstructionCaps()
        {
            Assert.Throws<ArgumentException>(() => new TacticalGrid(0, 0, 1000, 1000, 1, 0.5, null));
            Assert.Throws<ArgumentException>(() => new TacticalGrid(0, 0, 10, 10, 1, 0.5, new TacticalObstacle[65]));
            Assert.Throws<ArgumentException>(() => new TacticalGrid(0, 0, 10, 10, 0, 0.5, null));
            Assert.Throws<ArgumentException>(() => new TacticalGrid(0, 0, 10, 10, 1, double.NaN, null));
            Assert.AreEqual(4096, new TacticalGrid(0, 0, 63, 63, 1, 0, null).Count);
        }

        [Test]
        public void ATeammateOccupancySnapshotCreatesAClearAlternateRoute_AndClearingItRestoresTheDirectRoute()
        {
            var grid = new TacticalGrid(-5, -5, 5, 5, 1, 0.5, null); var route = new List<Vec3d>();
            Vec3d from = new Vec3d(-4, 0, 0); int goal = grid.ClosestCell(new Vec3d(4, 0, 0), 1, true);
            Assert.True(grid.FindRoute(from, goal, route, 121)); int direct = route.Count;
            grid.SetOccupants(new[] { Vec3d.Zero }, 1, 1);
            Assert.True(grid.FindRoute(from, goal, route, 121)); Assert.Greater(route.Count, direct);
            foreach (Vec3d p in route) Assert.Greater(Vec3d.Distance(p, Vec3d.Zero), 1);
            grid.SetOccupants(null, 0, 1);
            Assert.True(grid.FindRoute(from, goal, route, 121)); Assert.AreEqual(direct, route.Count);
        }
    }
}
