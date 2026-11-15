using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    public class TerrainCollisionTests
    {
        [Test]
        public void SupportingTileIsTheSameAtEveryElevationOnAllSixFaces()
        {
            const int level = 3;
            for (int face = 0; face < CubeSphere.Faces; face++)
            {
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        Vec3d direction = TerrainMesher.TileCentre(face, level, x, y);
                        foreach (double radius in new[] { 1.0, 250.0, 500.0, 850.0, 10000.0 })
                        {
                            Assert.IsTrue(TerrainCollision.ContainsDirection(face, level, x, y, direction * radius),
                                "Elevation must not remove the supporting tile: face " + face + " tile " + x + "," + y);
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryFaceSeamCornerAndInternalTileEdgeHasExactlyOneOwner()
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;
                        Vec3d point = new Vec3d(x, y, z) * 500;
                        Assert.AreEqual(1, Owners(point, 2), "No gap or duplicate owner at " + point);
                    }
                }
            }
            for (int face = 0; face < CubeSphere.Faces; face++)
            {
                foreach (double u in new[] { -0.5, 0.0, 0.5 })
                {
                    foreach (double v in new[] { -0.5, 0.0, 0.5 })
                    {
                        Assert.AreEqual(1, Owners(CubeSphere.Direction(face, u, v) * 500, 2));
                    }
                }
            }
            Assert.IsTrue(TerrainCollision.ContainsDirection(0, 0, 0, 0, new Vec3d(1, 1, 1)),
                "The existing cube mapping owns equal-axis corners on the X face.");
            Assert.IsTrue(TerrainCollision.ContainsDirection(2, 0, 0, 0, new Vec3d(0, 1, 1)));
        }

        [Test]
        public void ChildrenPartitionTheirParentForInteriorPoints()
        {
            for (int face = 0; face < CubeSphere.Faces; face++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        for (int child = 0; child < 4; child++)
                        {
                            Vec3d point = TerrainMesher.TileCentre(face, 3, x * 2 + (child & 1), y * 2 + (child >> 1)) * 700;
                            Assert.IsTrue(TerrainCollision.ContainsDirection(face, 2, x, y, point));
                            int owners = 0;
                            for (int other = 0; other < 4; other++)
                            {
                                if (TerrainCollision.ContainsDirection(face, 3, x * 2 + (other & 1), y * 2 + (other >> 1), point)) owners++;
                            }
                            Assert.AreEqual(1, owners);
                        }
                    }
                }
            }
        }

        [Test]
        public void FootprintDistanceIgnoresOmittedElevationAndStillSeparatesDistantTiles()
        {
            Vec3d point = CubeSphere.Direction(0, 0.3, -0.2);
            double ground = TerrainCollision.FootprintDistance(500, 0, 4, 10, 6, point * 500);
            foreach (double radius in new[] { 50.0, 400.0, 680.0, 2500.0 })
            {
                Assert.AreEqual(ground, TerrainCollision.FootprintDistance(500, 0, 4, 10, 6, point * radius), 1e-9);
            }
            Vec3d centre = TerrainMesher.TileCentre(0, 4, 10, 6);
            Assert.Less(TerrainCollision.FootprintDistance(500, 0, 4, 10, 6, centre * 900), 0);
            Assert.Greater(TerrainCollision.FootprintDistance(500, 1, 4, 10, 6, centre * 900), 500);
        }

        [Test]
        public void APointDeepBelowAMouthIsNotInsideTheCave()
        {
            Cave cave = Tunnel();
            Assert.IsTrue(TerrainCollision.InsideCave(cave, new Vec3d(499, 0, 0), 4), "The actual mouth remains enterable.");
            Assert.IsTrue(TerrainCollision.InsideCave(cave, new Vec3d(482, 20, 0), 4), "The actual tube remains usable.");
            Assert.IsFalse(TerrainCollision.InsideCave(cave, new Vec3d(100, 0, 0), 4),
                "A mouth must not exempt the entire radial line from sinking recovery.");
            Assert.IsFalse(TerrainCollision.InsideCave(cave, Vec3d.Zero, 4));
        }

        [Test]
        public void TheTubeIncludesGapsBetweenCirclesAndUsesTheWiderAdjacentRadius()
        {
            var cave = new Cave
            {
                Centres = new[] { new Vec3d(480, 0, 0), new Vec3d(480, 40, 0) },
                Radii = new[] { 2.0, 5.0 },
                Mouths = new Vec3d[0],
                MouthRadius = 2,
            };
            Assert.IsTrue(TerrainCollision.InsideCave(cave, new Vec3d(480, 20, 5), 0));
            Assert.IsFalse(TerrainCollision.InsideCave(cave, new Vec3d(480, 20, 7), 0));
            Assert.IsFalse(TerrainCollision.InsideCave(cave, new Vec3d(480, 60, 0), 0), "A segment must have finite ends.");
        }

        [Test]
        public void BothMouthsHaveFiniteFunnelsAtTheirActualEndpoints()
        {
            Cave cave = Tunnel();
            cave.Mouths = new[] { Vec3d.Right, cave.Centres[2].Normalized };
            Vec3d nearExit = cave.Centres[2] + new Vec3d(0, 0, 10);
            Assert.IsTrue(TerrainCollision.InsideCave(cave, nearExit, 1));
            Assert.IsFalse(TerrainCollision.InsideCave(cave, cave.Centres[2].Normalized * 100, 1));
            cave.Mouths = new[] { Vec3d.Right };
            Assert.IsFalse(TerrainCollision.InsideCave(cave, nearExit, 1), "The closed end has no extra mouth funnel.");
        }

        [Test]
        public void ZeroLengthSegmentsSingleCirclesAndMissingDataAreSafe()
        {
            var cave = new Cave
            {
                Centres = new[] { new Vec3d(470, 10, 0), new Vec3d(470, 10, 0) },
                Radii = new[] { 2.0, 3.0 },
                Mouths = new Vec3d[0],
            };
            Assert.IsTrue(TerrainCollision.InsideCave(cave, new Vec3d(470, 10, 3), 0));
            Assert.IsFalse(TerrainCollision.InsideCave(cave, new Vec3d(470, 10, 5), 0));
            cave.Centres = new[] { new Vec3d(470, 10, 0) };
            Assert.IsTrue(TerrainCollision.InsideCave(cave, cave.Centres[0], 0));
            Assert.IsFalse(TerrainCollision.InsideCave(null, Vec3d.Right, 0));
            Assert.IsFalse(TerrainCollision.InsideCave(new Cave(), Vec3d.Right, 0));
            Assert.IsFalse(TerrainCollision.ContainsDirection(0, 2, 0, 0, Vec3d.Zero));
            Assert.AreEqual(double.PositiveInfinity, TerrainCollision.FootprintDistance(500, 0, 2, 0, 0, Vec3d.Zero));
        }

        static Cave Tunnel()
        {
            return new Cave
            {
                Centres = new[] { new Vec3d(500, 0, 0), new Vec3d(482, 20, 0), new Vec3d(480, 50, 0) },
                Radii = new[] { 3.0, 4.0, 3.0 },
                Mouths = new[] { Vec3d.Right },
                MouthRadius = 4,
            };
        }

        static int Owners(Vec3d point, int level)
        {
            int owners = 0, side = 1 << level;
            for (int face = 0; face < CubeSphere.Faces; face++)
            {
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        if (TerrainCollision.ContainsDirection(face, level, x, y, point)) owners++;
                    }
                }
            }
            return owners;
        }
    }
}
