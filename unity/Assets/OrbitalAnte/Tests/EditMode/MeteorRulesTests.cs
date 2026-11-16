using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Meteors;

namespace OrbitalAnte.Tests.EditMode
{
    public class MeteorRulesTests
    {
        static readonly MeteorRules Rules = new MeteorRules();

        // A world with gently rolling ground, like a small planet's, for the flights to land on.
        static PlanWorld World(double radius, double gravity, bool air)
        {
            double bump = Math.Min(6, radius * 0.04);
            Func<Vec3d, double> ground = d => radius + bump * Math.Sin(d.X * 9) * Math.Cos(d.Z * 7) + bump * 0.5 * Math.Sin(d.Y * 21);
            return new PlanWorld { GroundRadius = ground, Ceiling = radius + bump * 1.6, SurfaceRadius = radius, Gravity = gravity, Air = air, Drag = air ? Rules.airDrag : 0 };
        }

        [Test]
        public void Gaps_AreNeverShorterThanTheShortest_NorLongerThanTheLongest_AndAverageNearTheMean()
        {
            var rng = new SeededRandom(7);
            double sum = 0;
            int n = 4000;
            for (int i = 0; i < n; i++)
            {
                double gap = MeteorClock.NextGap(Rules, rng.NextDouble(), 1f);
                Assert.That(gap, Is.GreaterThanOrEqualTo(Rules.minGap - 1e-9).And.LessThanOrEqualTo(Rules.maxGap + 1e-9));
                sum += gap;
            }
            double mean = sum / n;
            Assert.That(mean, Is.GreaterThan(Rules.meanGap * 0.85).And.LessThan(Rules.meanGap * 1.05), "average gap");
            Assert.That(MeteorClock.NextGap(Rules, 0.5, 2f), Is.EqualTo(MeteorClock.NextGap(Rules, 0.5, 1f) / 2).Within(1e-9), "a busier world is struck more often");
            Assert.That(double.IsPositiveInfinity(MeteorClock.NextGap(Rules, 0.5, 0f)), "rate 0 means never");
        }

        [Test]
        public void Gaps_AreNotRegular_SomeShortSomeLong()
        {
            var rng = new SeededRandom(3);
            // Measured from the dials, so the test follows them: some gaps near the floor, some well past the mean.
            double quick = Rules.minGap + (Rules.meanGap - Rules.minGap) * 0.25;
            double slow = Rules.meanGap * 1.6;
            int fast = 0, long_ = 0;
            for (int i = 0; i < 2000; i++)
            {
                double gap = MeteorClock.NextGap(Rules, rng.NextDouble(), 1f);
                Assert.That(gap, Is.InRange(Rules.minGap, Rules.maxGap));
                if (gap < quick) fast++;
                if (gap > slow) long_++;
            }
            Assert.That(fast, Is.GreaterThan(200), "some strikes come quickly after the last");
            Assert.That(long_, Is.GreaterThan(100), "some quiet spells are long");
        }

        [Test]
        public void Strikes_LandWhereTheyShould_AroundThePlayer()
        {
            var player = new Vec3d(0.3, 0.9, 0.2).Normalized;
            double radius = 500;
            var enemies = new List<Vec3d> { MeteorAim.Along(player, 1.0, 80 / radius), MeteorAim.Along(player, 2.5, 10 / radius) };
            int atEnemies = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                var rng = new SeededRandom(seed);
                Assert.That(MeteorAim.Pick(Rules, rng, player, radius, enemies, null, out Vec3d target, out AimKind kind), Is.True);
                double d = MeteorAim.ArcDistance(player, target, radius);
                switch (kind)
                {
                    case AimKind.Wide:
                        Assert.That(d, Is.GreaterThanOrEqualTo(Rules.minDistance - 0.01).And.LessThanOrEqualTo(Rules.maxDistance + 0.01));
                        break;
                    case AimKind.NearMiss:
                        Assert.That(d, Is.GreaterThanOrEqualTo(Rules.nearMissMin - 0.01).And.LessThanOrEqualTo(Rules.nearMissMax + 0.01));
                        break;
                    case AimKind.AtEnemies:
                        atEnemies++;
                        Assert.That(d, Is.GreaterThanOrEqualTo(Rules.safeFromPlayer), "a strike at enemies never lands on the player");
                        // Only the far enemy can be aimed at: the near one is too close to the player.
                        Assert.That(MeteorAim.ArcDistance(enemies[0], target, radius), Is.LessThanOrEqualTo(Rules.enemyAimSpread + 0.01));
                        break;
                }
            }
            Assert.That(atEnemies, Is.GreaterThan(60), "enemies get struck now and then");
        }

        [Test]
        public void Strikes_NeverLandWhereTheyAreNotAllowed_AndFitOnATinyMoon()
        {
            var player = Vec3d.Up;
            Func<Vec3d, bool> notNorthEast = d => !(d.X > 0 && d.Z > 0);
            for (int seed = 0; seed < 300; seed++)
            {
                var rng = new SeededRandom(seed);
                if (MeteorAim.Pick(Rules, rng, player, 500, null, notNorthEast, out Vec3d target, out _))
                    Assert.That(notNorthEast(target), Is.True);
                // Phobos is 40 m across: a strike there stays on the near side.
                Assert.That(MeteorAim.Pick(Rules, new SeededRandom(seed), player, 40, null, null, out Vec3d small, out _), Is.True);
                Assert.That(MeteorAim.ArcDistance(player, small, 40), Is.LessThanOrEqualTo(40 * 1.2 + 0.01));
            }
            Assert.That(MeteorAim.Pick(Rules, new SeededRandom(1), player, 500, null, d => false, out _, out _), Is.False, "nowhere allowed: no strike");
        }

        [Test]
        public void AWholeRock_LandsOnItsTarget_OnTheGround()
        {
            foreach (var (radius, gravity) in new[] { (500.0, 9.8), (150.0, 1.62), (40.0, 0.3), (350.0, 3.7) })
            {
                PlanWorld world = World(radius, gravity, false);
                for (int seed = 0; seed < 25; seed++)
                {
                    var target = MeteorAim.Along(Vec3d.Up, seed, 0.3);
                    MeteorPlan plan = MeteorPlanner.Plan(Rules, new SeededRandom(seed), target, world);
                    Assert.That(plan.Pieces.Count, Is.EqualTo(1), "no air: it comes down whole");
                    MeteorPiece rock = plan.Pieces[0];
                    Assert.That(rock.Lands, Is.True, $"radius {radius} seed {seed}");
                    Assert.That((rock.End - plan.Target).Magnitude, Is.LessThan(1.5), $"aimed: radius {radius} seed {seed} miss {(rock.End - plan.Target).Magnitude:0.00}");
                    Assert.That(rock.End.Magnitude, Is.EqualTo(world.GroundRadius(rock.End.Normalized)).Within(0.05), "on the ground");
                    Assert.That(rock.Ends, Is.GreaterThan(1.5).And.LessThan(8), "a few seconds to see it coming");
                    Assert.That(rock.Crater, Is.InRange(Rules.minCrater, Rules.maxCrater));
                }
            }
        }

        [Test]
        public void InAir_RocksBurst_IntoPiecesThatAllLand_NearTheTarget()
        {
            PlanWorld world = World(500, 9.8, true);
            int bursts = 0;
            for (int seed = 0; seed < 80; seed++)
            {
                var target = MeteorAim.Along(Vec3d.Up, seed * 0.7, 0.25);
                MeteorPlan plan = MeteorPlanner.Plan(Rules, new SeededRandom(seed), target, world);
                MeteorPiece whole = plan.Pieces[0];
                if (!whole.Bursts) continue;
                bursts++;
                Assert.That(plan.Pieces.Count - 1, Is.LessThanOrEqualTo(Rules.maxTotalPieces), "no more pieces than the cap");
                Assert.That(plan.Pieces.Count, Is.GreaterThanOrEqualTo(1 + Rules.minPieces));
                double wholeVolume = Math.Pow(whole.Crater, 3), landedVolume = 0;
                for (int i = 1; i < plan.Pieces.Count; i++)
                {
                    MeteorPiece p = plan.Pieces[i];
                    MeteorPiece parent = plan.Pieces[p.Parent];
                    Assert.That(parent.Bursts, Is.True);
                    Assert.That(p.Born, Is.EqualTo(parent.Ends).Within(1e-9), "a piece appears as its parent bursts");
                    Assert.That((MeteorPlan.PositionAt(p, p.Born) - parent.End).Magnitude, Is.LessThan(0.01), "and where it burst");
                    Assert.That(p.Crater, Is.GreaterThanOrEqualTo(Rules.smallestCrater - 1e-9).And.LessThan(parent.Crater));
                    if (!p.Lands) continue;
                    landedVolume += Math.Pow(p.Crater, 3);
                    Assert.That(p.End.Magnitude, Is.EqualTo(world.GroundRadius(p.End.Normalized)).Within(0.05));
                    Assert.That(MeteorAim.ArcDistance(p.End, plan.Target, 500), Is.LessThan(80), "the pieces come down round the target");
                }
                Assert.That(landedVolume, Is.LessThan(wholeVolume), "a burst loses rock, it never makes more");
                Assert.That(plan.Pieces.Exists(p => p.Lands), Is.True);
            }
            Assert.That(bursts, Is.GreaterThan(40), "most rocks burst in air");
        }

        [Test]
        public void APiecesPath_IsSmooth_WithNoJumps()
        {
            PlanWorld world = World(500, 9.8, true);
            for (int seed = 0; seed < 30; seed++)
            {
                MeteorPlan plan = MeteorPlanner.Plan(Rules, new SeededRandom(seed), MeteorAim.Along(Vec3d.Up, 1, 0.2), world);
                foreach (MeteorPiece p in plan.Pieces)
                {
                    Vec3d last = MeteorPlan.PositionAt(p, p.Born);
                    for (double t = p.Born; t <= p.Ends + 0.02; t += 0.01)
                    {
                        Vec3d now = MeteorPlan.PositionAt(p, t);
                        Assert.That((now - last).Magnitude, Is.LessThan(Rules.entrySpeedMax * 1.3 * 0.01 + 0.05), $"seed {seed} t {t:0.00}");
                        last = now;
                    }
                    Assert.That((MeteorPlan.PositionAt(p, p.Ends) - p.End).Magnitude, Is.LessThan(1e-6));
                }
            }
        }

        [Test]
        public void TheBlast_KillsInTheCore_HurtsAtTheEdge_AndStopsAtItsReach()
        {
            double crater = 4;
            double core = crater * Rules.lethalCore, reach = BlastRules.Reach(Rules, crater);
            Assert.That(BlastRules.Strength(Rules, crater, 0), Is.EqualTo(1));
            Assert.That(BlastRules.Strength(Rules, crater, reach), Is.EqualTo(0));
            Assert.That(BlastRules.Lethal(Rules, crater, core * 0.9), Is.True);
            Assert.That(BlastRules.Lethal(Rules, crater, core * 1.1), Is.False);
            Assert.That(BlastRules.PlayerDamage(Rules, crater, core), Is.GreaterThan(100f), "standing in it is death (the suit holds 100)");
            float half = BlastRules.PlayerDamage(Rules, crater, (core + reach) / 2);
            Assert.That(half, Is.GreaterThan(5f).And.LessThan(50f), "half way out it hurts, it does not kill");
            double last = 2;
            for (double d = 0; d <= reach + 1; d += 0.25)
            {
                double s = BlastRules.Strength(Rules, crater, d);
                Assert.That(s, Is.LessThanOrEqualTo(last + 1e-12), "weaker further out");
                last = s;
            }
            Assert.That(BlastRules.Shake(Rules, crater, crater * Rules.shakePerCrater + 1), Is.EqualTo(0f));
            Assert.That(BlastRules.Shake(Rules, crater, 10), Is.GreaterThan(BlastRules.Shake(Rules, crater, 60)));
            Assert.That(BlastRules.SoundDelay(343, true), Is.EqualTo(1).Within(1e-9));
            Assert.That(BlastRules.SoundDelay(343, false), Is.LessThan(0.2));
        }

        [Test]
        public void ACrater_HasAFloorOverTheGround_ARaisedRim_AndAnEdgeSunkUnderIt()
        {
            CraterSite c = CraterSite.Make(Rules, Vec3d.Up, 4, 11, true);
            Assert.That(CraterShape.Height(c, 0, 0), Is.EqualTo(CraterShape.FloorLift).Within(1e-9));
            Assert.That(CraterShape.Height(c, c.Radius, 0, false), Is.EqualTo(c.Rim).Within(1e-9), "the rim is at the crater's radius");
            double highest = 0;
            for (double d = 0; d < c.Radius * c.Apron; d += 0.05) highest = Math.Max(highest, CraterShape.Height(c, d, 0, false));
            Assert.That(highest, Is.EqualTo(c.Rim).Within(1e-6));
            Assert.That(CraterShape.Height(c, c.Radius * c.Apron, 1.3), Is.LessThan(-0.3), "the edge goes under the ground");
            Assert.That(CraterShape.Lift(c, MeteorAim.Along(Vec3d.Up, 0, 50 / 500.0), 500), Is.EqualTo(0));
            Assert.That(CraterShape.Lift(c, MeteorAim.Along(Vec3d.Up, 0, 4 / 500.0), 500), Is.EqualTo(c.Rim).Within(0.01));
        }

        [Test]
        public void ACratersMesh_FacesUp_HidesItsEdge_AndIsNeverGarish()
        {
            foreach (bool air in new[] { true, false })
            {
                CraterSite c = CraterSite.Make(Rules, new Vec3d(0.2, 1, -0.4).Normalized, 5, 3, air);
                GroundSampler ground = (Vec3d d, out double r, out float cr, out float cg, out float cb) =>
                {
                    r = 500 + 2 * Math.Sin(d.X * 40);
                    cr = 0.55f; cg = 0.5f; cb = 0.42f;
                };
                CraterMeshData mesh = CraterMesher.Build(c, ground, 500);
                Assert.That(mesh.Triangles.Length % 3, Is.EqualTo(0));
                Assert.That(mesh.Corners, Is.LessThan(2500), "cheap enough to keep two dozen");
                Vec3d P(int i) => mesh.Origin + new Vec3d(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]);
                for (int t = 0; t < mesh.Triangles.Length; t += 3)
                {
                    Vec3d a = P(mesh.Triangles[t]), b = P(mesh.Triangles[t + 1]), d = P(mesh.Triangles[t + 2]);
                    Assert.That(Vec3d.Dot(Vec3d.Cross(b - a, d - a), c.Up), Is.GreaterThanOrEqualTo(0), "every face looks up");
                }
                int corners = mesh.Corners;
                int spokes = Math.Max(24, Math.Min(48, (int)Math.Round(c.Radius * 9)));
                for (int i = corners - spokes; i < corners; i++)
                {
                    Vec3d p = P(i);
                    ground(p.Normalized, out double gr, out _, out _, out _);
                    Assert.That(p.Magnitude, Is.LessThan(gr - 0.3), "the outer edge is under the ground");
                }
                for (int i = 0; i < corners; i++)
                {
                    for (int k = 0; k < 3; k++) Assert.That(mesh.Colours[i * 4 + k], Is.InRange(0f, 0.78f));
                    Assert.That(mesh.Colours[i * 4 + 3], Is.InRange(0.09f, 1f));
                }
                Assert.That(mesh.Colours[3], Is.LessThan(0.5f), "the middle of the floor glows when fresh");
                Assert.That(mesh.Colours[(corners - 1) * 4 + 3], Is.EqualTo(1f), "the edge does not");
            }
        }

        [Test]
        public void Craters_LiftTheGroundEnemiesStandOn()
        {
            CraterRegistry.Clear();
            try
            {
                CraterSite c = CraterSite.Make(Rules, Vec3d.Up, 4, 5, false);
                CraterRegistry.Add("Moon", c);
                Assert.That(CraterRegistry.LiftAt("Moon", MeteorAim.Along(Vec3d.Up, 0, 4 / 150.0), 150), Is.EqualTo(c.Rim).Within(0.02));
                Assert.That(CraterRegistry.LiftAt("Mars", Vec3d.Up, 150), Is.EqualTo(0));
                CraterRegistry.Remove("Moon", 5);
                Assert.That(CraterRegistry.LiftAt("Moon", MeteorAim.Along(Vec3d.Up, 0, 4 / 150.0), 150), Is.EqualTo(0));
            }
            finally { CraterRegistry.Clear(); }
        }
    }
}
