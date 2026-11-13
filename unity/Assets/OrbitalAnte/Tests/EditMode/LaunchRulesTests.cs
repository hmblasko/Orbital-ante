using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The rules of Milestone 8 that need no engine: choosing the launch site, the lift-off build-up, and how violent each moment is.</summary>
    public sealed class LaunchRulesTests
    {
        static PlanetTerrain Earth(int seed)
        {
            PlanetProfile profile = PlanetProfiles.Defaults().Find(p => p.bodyId == "Earth");
            BodySpec body = SolarSystemDefaults.Bodies().Find(b => b.id == "Earth");
            return new PlanetTerrain(profile, "Earth", body.radius, seed);
        }

        // ------------------------------------------------------------------ the site

        [TestCase(4242)]
        [TestCase(1)]
        [TestCase(77)]
        [TestCase(31337)]
        public void TheSite_IsDryLevelAndTheSameEveryTime(int seed)
        {
            PlanetTerrain earth = Earth(seed);
            LaunchSpot a = LaunchSite.Find(earth);
            LaunchSpot b = LaunchSite.Find(Earth(seed));
            Assert.AreEqual(a.Up.X, b.Up.X, 1e-12);
            Assert.AreEqual(a.Up.Z, b.Up.Z, 1e-12);

            // Dry: the pad stands above the sea, with the margin.
            Assert.Greater(a.PadRadius - earth.Radius, earth.SeaLevel + LaunchSite.DryMargin);
            // Level enough that the mound hides the difference without looking like a hill.
            Assert.Less(a.Roughness, 12.0, "the flattest ground found is still rough");
            Assert.Less(a.MoundHeight, 16.0);
            Assert.Greater(a.MoundHeight, LaunchSite.Clearance - 1e-9);
            // Not in a cave mouth.
            Assert.IsFalse(earth.InHole(a.Up, 10.0));
        }

        [Test]
        public void TheSite_IsNearTheRealCape()
        {
            LaunchSpot spot = LaunchSite.Find(Earth(4242));
            Vec3d cape = LaunchSite.Direction(LaunchSite.PreferredLatitude, LaunchSite.PreferredLongitude);
            double metres = Math.Acos(Math.Min(1.0, Vec3d.Dot(cape, spot.Up))) * 500.0;
            Assert.Less(metres, 1100.0);
        }

        [Test]
        public void ThePadsAxes_PointNorthEastAndUp()
        {
            LaunchSpot spot = LaunchSite.Find(Earth(4242));
            Assert.AreEqual(0.0, Vec3d.Dot(spot.Up, spot.North), 1e-9);
            Assert.AreEqual(0.0, Vec3d.Dot(spot.Up, spot.East), 1e-9);
            Assert.AreEqual(0.0, Vec3d.Dot(spot.North, spot.East), 1e-9);
            Assert.Greater(spot.North.Y, 0.0, "north points toward the north pole");
            // An ordinary set of axes, the same handedness as Unity's (x = y cross z).
            Vec3d cross = Vec3d.Cross(spot.Up, spot.North);
            Assert.AreEqual(1.0, Vec3d.Dot(cross, spot.East), 1e-9);
            Vec3d top = spot.ToBody(0, 0, 0);
            Assert.AreEqual(spot.PadRadius, top.Magnitude, 1e-6);
        }

        [Test]
        public void TheMoundReachesBelowTheGroundAllRound()
        {
            PlanetTerrain earth = Earth(4242);
            LaunchSpot spot = LaunchSite.Find(earth);
            float depth = (float)spot.MoundHeight + 1f;
            // Walk round the edge of the skirt: wherever it ends, it is under the ground, so there is no gap.
            for (int k = 0; k < 36; k++)
            {
                double a = k * Math.PI / 18;
                double r = LaunchShapes.PadRadius + depth * 1.15;
                Vec3d foot = spot.ToBody(Math.Sin(a) * r, -depth, Math.Cos(a) * r);
                double ground = earth.Radius + earth.Height(foot.Normalized);
                Assert.Less(foot.Magnitude, ground + 0.01, "the mound's skirt stops short of the ground at " + k * 10 + " degrees");
            }
        }

        // ------------------------------------------------------------------ the build-up

        static LaunchModel Run(LaunchModel m, float seconds, float up, bool onGround, float height = 0f, float air = 1f, float dt = 0.02f)
        {
            for (float t = 0f; t < seconds; t += dt) m.Tick(dt, up, false, onGround, height, air, 1f);
            return m;
        }

        [Test]
        public void OnTheGround_TheEnginesBuildUpBeforeTheShipLeaves()
        {
            var m = new LaunchModel();
            m.Tick(0.02f, 1f, false, true, 0f, 1f, 1f);
            Assert.IsTrue(m.JustIgnited);
            Assert.AreEqual(EngineState.Igniting, m.State);
            Assert.AreEqual(m.Rules.ignitionPush, m.LiftGate, 1e-6, "held down while building up");

            int released = 0;
            float t = 0.02f;
            while (m.State == EngineState.Igniting && t < 5f)
            {
                m.Tick(0.02f, 1f, false, true, 0f, 1f, 1f);
                t += 0.02f;
                if (m.JustReleased) released++;
            }
            Assert.AreEqual(1, released);
            Assert.AreEqual(m.Rules.ignitionSeconds, t, 0.05f);
            Assert.AreEqual(1f, m.LiftGate);
        }

        [Test]
        public void TheBuildUpIsShort_AndNeverStopsYouFlying()
        {
            var rules = new LaunchRules();
            Assert.LessOrEqual(rules.ignitionSeconds, 1.5f, "a build-up, not a wait");
            Assert.Less(rules.ignitionPush, 0.3f, "enough to rock the ship, not to lift it");

            // In the air the engines answer at once: no build-up.
            var m = new LaunchModel();
            m.Tick(0.02f, 1f, false, false, 200f, 0f, 1f);
            Assert.AreEqual(EngineState.Running, m.State);
            Assert.IsFalse(m.JustIgnited);
            Assert.AreEqual(1f, m.LiftGate);

            // Letting go during the build-up winds down harmlessly.
            var g = new LaunchModel();
            Run(g, 0.4f, 1f, true);
            Assert.AreEqual(EngineState.Igniting, g.State);
            g.Tick(0.02f, 0f, false, true, 0f, 1f, 1f);
            Assert.AreEqual(EngineState.Off, g.State);
            Assert.AreEqual(1f, g.LiftGate);

            // Leaving the ground during the build-up (a bump, a slope) lets go at once.
            var h = new LaunchModel();
            Run(h, 0.3f, 1f, true);
            h.Tick(0.02f, 1f, false, false, 1f, 1f, 1f);
            Assert.AreEqual(EngineState.Running, h.State);
            Assert.IsTrue(h.JustReleased);
        }

        [Test]
        public void TheGroundMakesItViolent_SpaceMakesItCalm()
        {
            var pad = Run(new LaunchModel(), 2f, 1f, false, 0f, 1f);
            var high = Run(new LaunchModel(), 2f, 1f, false, 1000f, 1f);
            var moon = Run(new LaunchModel(), 2f, 1f, false, 0f, 0f);

            Assert.Greater(pad.Shake, high.Shake * 4f);
            Assert.Greater(pad.SmokeRate, 10f);
            Assert.AreEqual(0f, high.SmokeRate, 1e-6, "no smoke high above the ground");
            Assert.AreEqual(0f, high.DebrisRate, 1e-6);
            Assert.Greater(pad.Crackle, 0.5f);
            Assert.AreEqual(0f, high.Crackle, 1e-6);
            // The steady engine note in flight belongs to the audio system; this layer is the weight the ground adds, so it thins out.
            Assert.Greater(high.Roar, 0.05f, "a trace of it still comes through the ship's frame");
            Assert.Less(high.Roar, 0.25f, "the ground roar should not double up with the flying sound in open space");

            // The Moon: grit flies, but nothing billows or shimmers, and there is no echo off the ground.
            Assert.Greater(moon.DebrisRate, 1f);
            Assert.Less(moon.SmokeRate, pad.SmokeRate * 0.3f);
            Assert.AreEqual(0f, moon.Shimmer, 1e-6);
            Assert.AreEqual(0f, moon.Crackle, 1e-6);
        }

        [Test]
        public void EverythingRisesAndFallsSmoothly_AndStopsWhenTheEnginesStop()
        {
            var m = new LaunchModel();
            float last = 0f;
            for (int i = 0; i < 200; i++)
            {
                m.Tick(0.02f, 1f, false, i < 60, i < 60 ? 0f : i * 0.5f, 1f, 1f);
                Assert.LessOrEqual(Math.Abs(m.Burn - last), 0.26f, "the burn jumped at step " + i);
                last = m.Burn;
            }
            Run(m, 2f, 0f, false, 100f);
            Assert.AreEqual(0f, m.Burn, 1e-6);
            Assert.AreEqual(0f, m.Shake, 1e-6);
            Assert.AreEqual(0f, m.Roar, 1e-6);
            Assert.AreEqual(EngineState.Off, m.State);
        }

        [Test]
        public void BrokenEnginesBurnWeaker_ButStillBurn()
        {
            var good = new LaunchModel();
            var bad = new LaunchModel();
            for (int i = 0; i < 100; i++)
            {
                good.Tick(0.02f, 1f, false, false, 0f, 1f, 1f);
                bad.Tick(0.02f, 1f, false, false, 0f, 1f, 0.3f);
            }
            Assert.Less(bad.Burn, good.Burn);
            Assert.Greater(bad.Burn, 0.3f);
        }

        // ------------------------------------------------------------------ the shapes

        [Test]
        public void LettingGo_IsAStepUpNeverAStepDown_AndKicks()
        {
            var m = new LaunchModel();
            float peak = 0f, shakeBefore = 0f;
            bool released = false;
            for (int i = 0; i < 200 && !released; i++)
            {
                shakeBefore = m.Shake;
                peak = Math.Max(peak, m.Burn);
                m.Tick(0.02f, 1f, false, true, 0f, 1f, 1f);
                released = m.JustReleased;
            }
            Assert.IsTrue(released);
            Assert.AreEqual(1f, m.Kick, 1e-6);
            Assert.Greater(m.Shake, shakeBefore * 1.3f, "no kick on release");
            Run(m, 0.3f, 1f, false, 2f);
            Assert.GreaterOrEqual(m.Burn, peak - 1e-4f, "the burn dropped as the ship left the ground");
            Run(m, 0.3f, 1f, false, 4f);
            Assert.AreEqual(0f, m.Kick, 1e-6);
        }

        [Test]
        public void LettingGo_IsNeverAStepDown_ForAPartPush([Values(0.1f, 0.35f, 0.707f, 1f)] float up, [Values(false, true)] bool boost)
        {
            // Up and forward together reach the engines as about 0.707 of "up" (the push is capped at length one).
            var m = new LaunchModel();
            float before = 0f;
            bool released = false;
            for (int i = 0; i < 200 && !released; i++)
            {
                before = m.Burn;
                m.Tick(0.02f, up, boost, true, 0f, 1f, 1f);
                released = m.JustReleased;
            }
            Assert.IsTrue(released);
            Assert.GreaterOrEqual(m.Burn, before - 1e-5f, "the burn dropped in the step the ship was let go");
            for (int i = 0; i < 25; i++)
            {
                float last = m.Burn;
                m.Tick(0.02f, up, boost, false, 1f, 1f, 1f);
                Assert.GreaterOrEqual(m.Burn, last - 1e-5f, "the burn dropped just after the ship was let go");
            }
        }

        [Test]
        public void HotEnginesRelight_AlmostAtOnce_ColdOnesTakeTheFullBuildUp()
        {
            var m = new LaunchModel();
            Run(m, 2f, 1f, true);
            Assert.AreEqual(EngineState.Running, m.State);
            Run(m, 1f, 0f, true);
            Assert.AreEqual(EngineState.Off, m.State);
            Run(m, m.Rules.hotIgnitionSeconds + 0.06f, 1f, true);
            Assert.AreEqual(EngineState.Running, m.State, "a hop should not wait for the full build-up");

            Run(m, m.Rules.hotSeconds + 0.5f, 0f, true);
            Run(m, m.Rules.hotIgnitionSeconds + 0.06f, 1f, true);
            Assert.AreEqual(EngineState.Igniting, m.State, "cold engines skipped their build-up");
        }

        [Test]
        public void LettingGoEarly_IsReportedOnce_AndCostsNothing()
        {
            var m = new LaunchModel();
            Run(m, 0.4f, 1f, true);
            Assert.AreEqual(EngineState.Igniting, m.State);
            m.Tick(0.02f, 0f, false, true, 0f, 1f, 1f);
            Assert.IsTrue(m.JustCancelled);
            Assert.AreEqual(1f, m.LiftGate, 1e-6);
            m.Tick(0.02f, 0f, false, true, 0f, 1f, 1f);
            Assert.IsFalse(m.JustCancelled);
        }

        [Test]
        public void SmokeAtFullBoost_FitsUnderTheCap()
        {
            // About three seconds of life per puff (LaunchFx); the cap is LaunchConfig.maxSmoke = 110.
            var m = new LaunchModel();
            Run(m, 3f, 1f, true);
            for (int i = 0; i < 50; i++) m.Tick(0.02f, 1f, true, false, 0f, 1f, 1f);
            Assert.AreEqual(1f, m.Burn, 1e-4);
            Assert.LessOrEqual(m.SmokeRate * 3f, 110f);
        }

        static double Volume(ShipMesh m)
        {
            double v = 0;
            for (int t = 0; t < m.Triangles.Length; t += 3)
            {
                P3 a = m.Position(m.Triangles[t]), b = m.Position(m.Triangles[t + 1]), c = m.Position(m.Triangles[t + 2]);
                v += (a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y) - a.Y * ((double)b.X * c.Z - (double)b.Z * c.X) + a.Z * ((double)b.X * c.Y - (double)b.Y * c.X)) / 6.0;
            }
            return v;
        }

        [Test]
        public void Shapes_AreWellFormed_AndWithinBudget()
        {
            var all = new List<KeyValuePair<string, ShipMesh>>(LaunchShapes.Fixed(6f));
            all.Add(new KeyValuePair<string, ShipMesh>("Arm", LaunchShapes.Arm()));
            int total = 0;
            foreach (var kv in all)
            {
                ShipMesh m = kv.Value;
                Assert.Greater(m.TriangleCount, 0, kv.Key);
                Assert.AreEqual(m.VertexCount * 3, m.Colors.Length, kv.Key);
                foreach (int i in m.Triangles) Assert.IsTrue(i >= 0 && i < m.VertexCount, kv.Key);
                foreach (float p in m.Positions) Assert.IsFalse(float.IsNaN(p), kv.Key);
                total += m.TriangleCount;
            }
            Assert.Less(total, 9000, "the base's whole budget");
        }

        [Test]
        public void TheMoundIsASolidSeenFromOutside_AndItsTopFacesUp()
        {
            Assert.Greater(Volume(LaunchShapes.MoundCollision(6f)), 1000.0);
            ShipMesh mound = LaunchShapes.Mound(6f);
            double up = 0, down = 0;
            for (int t = 0; t < mound.Triangles.Length; t += 3)
            {
                P3 a = mound.Position(mound.Triangles[t]), b = mound.Position(mound.Triangles[t + 1]), c = mound.Position(mound.Triangles[t + 2]);
                if (a.Y < -0.4f || b.Y < -0.4f || c.Y < -0.4f) continue;
                P3 n = P3.Cross(b - a, c - a);
                if (Math.Abs(n.Y) < 0.5f * n.Length) continue;
                if (n.Y > 0) up += n.Length; else down += n.Length;
            }
            Assert.Greater(up, down * 50, "the pad's top is facing the wrong way");
            // The skirt faces out: a ray from far outside, level with the slope, hits its outside.
            ShipMesh skirt = mound;
            var bake = new LightBake();
            bake.Add(skirt);
            bake.PrepareForRays();
            int hit = bake.Trace(new P3(60, -3f, 0), new P3(-1, 0, 0), 100, out float _);
            Assert.GreaterOrEqual(hit, 0);
            Assert.Less(P3.Dot(bake.TriangleNormal(hit), new P3(-1, 0, 0)), 0f, "the skirt faces inward");
        }

        [Test]
        public void TheShipFitsThePad_AndTheArmMeetsItsSide()
        {
            // Every footpad lands on the pad's top, inside the keep-clear line.
            for (int i = 0; i < ShipLayout.Legs; i++)
            {
                P3 pad = ShipLayout.Footpad(i);
                float r = (float)Math.Sqrt(pad.X * pad.X + pad.Z * pad.Z) + ShipLayout.FootpadRadius;
                Assert.Less(r, 12.6f);
                Assert.Greater(r - ShipLayout.FootpadRadius, LaunchShapes.DishRadius, "a footpad would sit in the blast dish");
            }
            // The arm's tip reaches the cabin's outer skin, and nothing it passes through on the way.
            P3 tip = LaunchShapes.ArmPivot + new P3(LaunchShapes.ArmLength + 0.05f, 0.55f - LaunchShapes.ShipHeightOnPad, 0);
            float reach = (float)Math.Sqrt(tip.X * tip.X + tip.Z * tip.Z);
            float outer = ShipLayout.CabinRadius + ShipLayout.HullThickness;
            Assert.AreEqual(outer, reach, 0.35f, "the arm stops " + (reach - outer) + " m from the hull");
            // The tower is clear of the ship's widest point, the thrusters.
            P3 thruster = ShipLayout.Thruster(3);
            float thrusterReach = (float)Math.Sqrt(thruster.X * thruster.X + thruster.Z * thruster.Z);
            Assert.Greater(-LaunchShapes.TowerFoot.X - LaunchShapes.TowerHalf, thrusterReach + 2f);
        }

        [Test]
        public void SkyShade_IsOpenOnTopAndShadedUnderneath()
        {
            var b = new MeshBuilder();
            // The floor, cut into a grid so it has corners under the table.
            b.Sheet(11, 11, (i, j) => new P3(-5f + i, 0.1f, -5f + j), flip: true);
            b.Box(new P3(0, 0.6f, 0), new P3(1f, 0.1f, 1f));       // a low table over it
            ShipMesh m = b.Finish();
            var bake = new LightBake();
            bake.Add(m);
            List<float[]> shade = bake.SkyShade(64, 12f);
            float under = 1f, open = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                if (p.Y > 0.09f && p.Y < 0.11f && Math.Abs(p.X) < 1.1f && Math.Abs(p.Z) < 1.1f) under = Math.Min(under, shade[0][i]);
                if (p.Y > 0.65f) open = Math.Max(open, shade[0][i]);
            }
            Assert.Less(under, 0.5f);
            Assert.Greater(open, 0.95f);
            CollectionAssert.AreEqual(shade[0], bake.SkyShade(64, 12f)[0]);
        }

        [Test]
        public void EveryNamedPieceOfTheBase_HasAStandInShape()
        {
            var names = new List<string>();
            foreach (KeyValuePair<string, ShipMesh> piece in LaunchShapes.Fixed(6f)) names.Add(piece.Key);
            names.Add(LaunchLayout.Arm);
            CollectionAssert.AreEquivalent(LaunchLayout.Pieces, names, "the layout's piece names and the stand-in shapes have drifted apart");
            // The solid things you can bump into sit inside the base's footprint, clear of the ship.
            float clear = ShipLayout.FootpadDistance + ShipLayout.FootpadRadius + 1f;
            foreach (P3 foot in new[] { LaunchLayout.TowerFoot, LaunchLayout.WaterTowerFoot, LaunchLayout.HutCentre, LaunchLayout.Mast(0), LaunchLayout.Mast(1), LaunchLayout.Mast(2), LaunchLayout.Mast(3) })
            {
                double across = Math.Sqrt(foot.X * foot.X + foot.Z * foot.Z);
                Assert.Less(across, LaunchSite.Footprint, "outside the levelled ground");
                Assert.Greater(across - LaunchLayout.WaterTowerRadius, clear, "too close to the ship's legs");
            }
        }
    }
}
