using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Strider (the first alien creature): its body's rules, its walk, and its model against its layout.</summary>
    public class StriderRulesTests
    {
        static int Leg(int side, int place) => StriderLayout.Limb(side, place);

        [Test]
        public void APieceThatBreaks_TakesEverythingBeyondIt_AndNothingNearerTheBody()
        {
            var body = new StriderBody();
            int leg = Leg(StriderLayout.Right, 2);
            StriderHit graze = body.HitLimb(leg, 1, body.MaxHp(leg, 1) - 1);
            Assert.AreEqual(0, graze.Losses.Count);
            Assert.AreEqual(3, body.Kept(leg));
            StriderHit hit = body.HitLimb(leg, 1, 5);
            Assert.AreEqual(1, hit.Losses.Count);
            Assert.AreEqual(leg, hit.Losses[0].Limb);
            Assert.AreEqual(1, hit.Losses[0].FromSegment);
            Assert.AreEqual(1, body.Kept(leg), "the upper piece stays on the body");
            Assert.AreEqual(0, body.HitLimb(leg, 2, 999).Losses.Count, "a piece that is gone cannot be hit again");
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void LimbsAreLow_AFewRifleHitsTakeOneOff()
        {
            // Part B, Milestone 1: limbs come off in one to three rifle hits. The rifle's body hit is 24.
            StriderHp hp = StriderHp.Default;
            foreach (double piece in new[] { hp.Upper, hp.Lower, hp.Tip, hp.Arm })
                Assert.LessOrEqual(Math.Ceiling(piece / 24.0), 3, "a limb piece needs more than three rifle hits");
            Assert.Greater(hp.Carapace, hp.Belly * 1.4, "the lid is armour; the belly is the way in");
            Assert.LessOrEqual(hp.Carapace, 400, "but a slow way, never a wall (constitution rule 3: no inflated health)");
        }

        [Test]
        public void ItStandsOnFiveLegs_AndGoesDownOnFour_OrWithOneSideNearlyBare()
        {
            var body = new StriderBody();
            Assert.AreEqual(StriderStance.Standing, body.Stance);
            Assert.AreEqual(1.0, body.SpeedShare, 1e-9);
            Assert.AreEqual(1.0, body.HeightShare, 1e-9);
            double speed = body.SpeedShare, height = body.HeightShare;
            // Lose three, spread over both sides: still up, slower and lower each time.
            foreach (int leg in new[] { Leg(0, 1), Leg(1, 2), Leg(0, 4) })
            {
                body.HitLimb(leg, 0, 0, sever: true);
                Assert.AreEqual(StriderStance.Standing, body.Stance);
                Assert.Less(body.SpeedShare, speed);
                Assert.Less(body.HeightShare, height);
                speed = body.SpeedShare;
                height = body.HeightShare;
            }
            Assert.AreEqual(5, body.Legs);
            StriderHit last = body.HitLimb(Leg(1, 3), 0, 0, sever: true);
            Assert.IsTrue(last.StanceChanged);
            Assert.AreEqual(StriderStance.Down, body.Stance);
            Assert.Greater(body.SpeedShare, 0.0, "it still drags itself along");
            Assert.Less(body.SpeedShare, 0.3);

            var lopsided = new StriderBody();
            for (int place = 1; place <= 3; place++) lopsided.HitLimb(Leg(StriderLayout.Right, place), 0, 0, sever: true);
            Assert.AreEqual(5, lopsided.Legs);
            Assert.AreEqual(StriderStance.Down, lopsided.Stance, "one leg on a side cannot hold it up");
        }

        [Test]
        public void ALegStandsOnItsStump_WhenOnlyTheTipIsGone_ButNotOnItsUpperPieceAlone()
        {
            var body = new StriderBody();
            body.HitLimb(Leg(0, 1), 2, 0, sever: true);
            Assert.IsTrue(body.CarriesWeight(Leg(0, 1)));
            body.HitLimb(Leg(0, 1), 1, 0, sever: true);
            Assert.IsFalse(body.CarriesWeight(Leg(0, 1)));
            Assert.IsFalse(body.CarriesWeight(StriderLayout.Limb(0, 0)), "claw arms never carry weight");
        }

        [Test]
        public void OnlyTheBellyOrTheLid_KillsIt_AndThenEveryLimbIsThrownOff()
        {
            var body = new StriderBody();
            for (int limb = 0; limb < StriderLayout.Limbs; limb++) body.HitLimb(limb, 1, 0, sever: true);
            Assert.IsTrue(body.Alive, "losing limbs never kills it");
            Assert.IsFalse(body.CanStrike(StriderLayout.Right));
            StriderHit hit = body.HitBody(belly: true, body.Max.Belly - 1);
            Assert.IsFalse(hit.Killed);
            hit = body.HitBody(belly: true, 2);
            Assert.IsTrue(hit.Killed);
            Assert.AreEqual(StriderStance.Dead, body.Stance);
            Assert.AreEqual(StriderLayout.Limbs, hit.Losses.Count, "what was left of every limb comes off");
            foreach (StriderLoss loss in hit.Losses) Assert.IsTrue(loss.Burst);
            Assert.IsFalse(body.HitBody(belly: false, 50).Killed, "it cannot die twice");

            var armoured = new StriderBody();
            Assert.IsFalse(armoured.HitBody(belly: false, armoured.Max.Belly).Killed, "from above it takes far more");
            Assert.IsTrue(armoured.HitBody(belly: false, 0, lethal: true).Killed, "a one-hit-kill gun kills wherever it lands");
        }

        static StriderPose Run(StriderBody body, StriderAnimator animator, double seconds, Vec3d velocity, double turn = 0, Action<double, StriderPose> each = null)
        {
            var pose = new StriderPose();
            const double dt = 1.0 / 60.0;
            for (double t = 0; t < seconds; t += dt)
            {
                animator.Update(body, new StriderAnimInput { Velocity = velocity, TurnRate = turn, StrikeSide = -1 }, dt, pose);
                each?.Invoke(t, pose);
            }
            return pose;
        }

        static void AssertSound(StriderBody body, StriderPose pose)
        {
            Assert.IsFalse(double.IsNaN(pose.BodyPosition.Y) || double.IsNaN(pose.BodyRotation.W));
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                for (int s = 0; s < body.Kept(limb); s++)
                {
                    int i = limb * StriderLayout.Segments + s;
                    Assert.IsFalse(double.IsNaN(pose.Position[i].X + pose.Position[i].Y + pose.Position[i].Z), "limb " + limb + " piece " + s);
                    QuatD q = pose.Rotation[i];
                    Assert.AreEqual(1.0, q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W, 1e-6);
                }
            }
        }

        /// <summary>Where a leg's last piece ends, from the pose.</summary>
        static Vec3d EndOf(StriderBody body, StriderPose pose, int limb)
        {
            int last = body.Kept(limb) - 1;
            int i = limb * StriderLayout.Segments + last;
            Vec3d along = pose.Rotation[i].Rotate(Vec3d.Forward);
            return pose.Position[i] + along * StriderLayout.Length(limb, last);
        }

        [Test]
        public void StandingStill_EveryFootIsOnTheGround_AndEveryLegReachesItsFoot()
        {
            var body = new StriderBody();
            var animator = new StriderAnimator();
            StriderPose pose = Run(body, animator, 3.0, Vec3d.Zero);
            AssertSound(body, pose);
            Assert.AreEqual(StriderLayout.StandHeight, pose.BodyPosition.Y, 0.12);
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                if (StriderLayout.IsArm(limb)) continue;
                Assert.IsTrue(pose.Planted[limb]);
                Assert.AreEqual(0.0, pose.End[limb].Y, 1e-6);
                // The modelled tip curves a little, so its straight length lands within a hand of the foot.
                Assert.Less(Vec3d.Distance(EndOf(body, pose, limb), pose.End[limb]), 0.12, "leg " + limb + " does not reach its foot");
                // The knee is the highest part of the leg, above the body: the spider crab's stance.
                Assert.Greater(pose.Position[limb * 3 + 1].Y, pose.BodyPosition.Y);
            }
        }

        [Test]
        public void Walking_FeetOnTheGroundStayPutInTheWorld_AndNeverTooManyAreInTheAir()
        {
            var body = new StriderBody();
            var animator = new StriderAnimator();
            var velocity = new Vec3d(0, 0, 1.6);
            var last = new Vec3d[StriderLayout.Limbs];
            var wasDown = new bool[StriderLayout.Limbs];
            int steps = 0, most = 0;
            const double dt = 1.0 / 60.0;
            Run(body, animator, 8.0, velocity, 0, (t, pose) =>
            {
                most = Math.Max(most, animator.Swinging);
                for (int limb = 0; limb < StriderLayout.Limbs; limb++)
                {
                    if (StriderLayout.IsArm(limb)) continue;
                    if (pose.Planted[limb] && wasDown[limb])
                    {
                        // In strider space a foot on the ground goes backward exactly as fast as the body goes forward.
                        Vec3d moved = pose.End[limb] - last[limb];
                        Assert.AreEqual(-velocity.Z * dt, moved.Z, 1e-6);
                        Assert.AreEqual(0.0, moved.X, 1e-6);
                    }
                    if (wasDown[limb] && !pose.Planted[limb]) steps++;
                    wasDown[limb] = pose.Planted[limb];
                    last[limb] = pose.End[limb];
                    Assert.Less(Vec3d.Distance(pose.End[limb], StriderLayout.FootHome(limb)), 2.2, "a foot was left behind");
                }
                AssertSound(body, pose);
            });
            Assert.Greater(steps, 24, "it has to keep stepping to keep up");
            Assert.LessOrEqual(most, 3, "a careful walk: most of its feet stay down");
            Assert.GreaterOrEqual(most, 2);
        }

        [Test]
        public void LosingLegs_ItSettlesLower_LeansOverTheLegsItHasLeft_AndGoesDownOnItsBelly()
        {
            var body = new StriderBody();
            var animator = new StriderAnimator();
            StriderPose whole = Run(body, animator, 2.0, Vec3d.Zero);
            double full = whole.BodyPosition.Y;
            body.HitLimb(Leg(StriderLayout.Right, 1), 0, 0, sever: true);
            body.HitLimb(Leg(StriderLayout.Right, 3), 1, 0, sever: true);
            StriderPose hurt = Run(body, animator, 3.0, new Vec3d(0, 0, 0.8));
            AssertSound(body, hurt);
            Assert.Less(hurt.BodyPosition.Y, full - 0.15);
            Assert.Greater(hurt.BodyPosition.Y, StriderLayout.BellyHeight + 0.5);
            Vec3d rightSide = hurt.BodyRotation.Rotate(Vec3d.Right);
            Assert.Less(rightSide.Y, -0.02, "it sags toward the side that lost legs");
            Assert.Less(hurt.BodyPosition.X, -0.02, "and shifts over the legs it has left");

            body.HitLimb(Leg(StriderLayout.Left, 2), 0, 0, sever: true);
            body.HitLimb(Leg(StriderLayout.Left, 4), 0, 0, sever: true);
            Assert.AreEqual(StriderStance.Down, body.Stance);
            StriderPose down = Run(body, animator, 3.0, new Vec3d(0, 0, 0.2));
            AssertSound(body, down);
            Assert.AreEqual(StriderLayout.BellyHeight, down.BodyPosition.Y, 0.05);
        }

        [Test]
        public void AStrike_RaisesTheClawFirst_ThenPutsItsPointWhereItWasThrown()
        {
            var body = new StriderBody();
            var animator = new StriderAnimator();
            var pose = Run(body, animator, 1.0, Vec3d.Zero);
            int arm = StriderLayout.Limb(StriderLayout.Right, 0);
            double restHeight = pose.End[arm].Y;
            var at = new Vec3d(0.4, 1.3, 3.2);
            const double dt = 1.0 / 60.0;
            double highest = 0, nearest = double.MaxValue;
            for (double t = 0; t <= 1.0; t += dt / 1.6)
            {
                animator.Update(body, new StriderAnimInput { StrikeSide = StriderLayout.Right, StrikeT = t, StrikeAt = at }, dt, pose);
                if (t < StriderAnimator.StrikeStarts) highest = Math.Max(highest, pose.End[arm].Y);
                if (t >= StriderAnimator.StrikeLands && t < 0.76) nearest = Math.Min(nearest, Vec3d.Distance(EndOf(body, pose, arm), at));
                AssertSound(body, pose);
            }
            Assert.Greater(highest, restHeight + 1.0, "the raise is the warning: it must be seen");
            Assert.Less(nearest, 0.25, "the claw's point arrives where it was thrown");
            Assert.Greater(StriderAnimator.StrikeStarts * 1.6, 0.6, "the warning lasts long enough to step aside");
        }

        [Test]
        public void ManyLossesInAnyOrder_NeverBreakThePose()
        {
            var rng = new SeededRandom(1234);
            for (int run = 0; run < 12; run++)
            {
                var body = new StriderBody();
                var animator = new StriderAnimator();
                for (int loss = 0; loss < 14; loss++)
                {
                    int limb = rng.Range(0, StriderLayout.Limbs), segment = rng.Range(0, StriderLayout.Segments);
                    StriderHit hit = body.HitLimb(limb, segment, 0, sever: true);
                    if (hit.Losses.Count > 0) animator.Flinch(new Vec3d(1, 1, 0));
                    StriderPose pose = Run(body, animator, 0.7, new Vec3d(0.3, 0, 1.2 * body.SpeedShare), 0.4);
                    AssertSound(body, pose);
                    Assert.GreaterOrEqual(pose.BodyPosition.Y, StriderLayout.BellyHeight - 1e-6, "the body never sinks into the ground");
                    Assert.Less(pose.BodyPosition.Y, StriderLayout.StandHeight + 1.0);
                }
            }
        }

        // ---- what it decides

        static StriderSenses Sees(double x, double z) => new StriderSenses { SeesPlayer = true, Player = new Vec3d(x, 0, z), Chest = new Vec3d(x, 1.3, z) };

        [Test]
        public void SeeingThePlayer_ItTurnsToThem_WalksUp_AndStopsAtArmsLength()
        {
            var rules = new StriderRules();
            var body = new StriderBody();
            var brain = new StriderBrain(rules, 1);
            StriderOrders far = brain.Think(body, Sees(2, 20), 0.02);
            Assert.Greater(far.Velocity.Z, 2.0);
            Assert.Greater(far.TurnRate, 0.0, "the player is to its right");
            Assert.LessOrEqual(far.Velocity.Z, rules.pace);
            StriderOrders behind = brain.Think(body, Sees(-3, -20), 0.02);
            Assert.AreEqual(0.0, behind.Velocity.Z, "it turns round before it walks");
            Assert.AreEqual(-rules.turnRate, behind.TurnRate, 1e-9);
            body.HitLimb(StriderLayout.Limb(0, 1), 0, 0, sever: true);
            body.HitLimb(StriderLayout.Limb(1, 2), 0, 0, sever: true);
            Assert.Less(brain.Think(body, Sees(0, 20), 0.02).Velocity.Z, far.Velocity.Z, "fewer legs, slower");
        }

        [Test]
        public void AStrike_FollowsThePlayerOnlyAtFirst_SoAStepAsideIsAMiss_AndLandsOnce()
        {
            var rules = new StriderRules();
            var body = new StriderBody();
            var brain = new StriderBrain(rules, 1);
            StriderOrders start = brain.Think(body, Sees(0.6, 3.6), 0.02);
            Assert.AreEqual(StriderLayout.Right, start.StrikeSide, "the claw on the player's side");
            int landed = 0;
            Vec3d locked = Vec3d.Zero;
            for (double t = 0; t < rules.strikeSeconds + 0.1; t += 0.02)
            {
                // The player steps aside half way through the warning.
                bool stepped = t > rules.strikeSeconds * 0.4;
                StriderOrders o = brain.Think(body, stepped ? Sees(2.4, 3.6) : Sees(0.6, 3.6), 0.02);
                if (o.StrikeSide < 0) break;
                Assert.AreEqual(0.0, o.Velocity.Z, "it stands still to strike");
                Assert.AreEqual(0.0, o.TurnRate);
                if (o.Landed) { landed++; locked = o.StrikeAt; }
            }
            Assert.AreEqual(1, landed);
            Assert.AreEqual(0.6, locked.X, 1e-6, "it lands where the player was when the claw was committed");
            Assert.Greater(Vec3d.Distance(locked, new Vec3d(2.4, 1.3, 3.6)), rules.strikeRadius, "the step aside was far enough");
            // The warning: from the lock to the landing there is time to move.
            Assert.Greater((StriderAnimator.StrikeLands - StriderBrain.LockAt) * rules.strikeSeconds, 0.5);
            Assert.IsFalse(brain.Striking);
            Assert.Less(brain.Think(body, Sees(0.6, 3.6), 0.02).StrikeSide, 0, "it rests between strikes");
        }

        [Test]
        public void WithOneClaw_ItUsesTheOther_AndWithNone_ItBacksAway()
        {
            var rules = new StriderRules();
            var body = new StriderBody();
            body.HitLimb(StriderLayout.Limb(StriderLayout.Right, 0), 1, 0, sever: true);
            var brain = new StriderBrain(rules, 1);
            Assert.AreEqual(StriderLayout.Left, brain.Think(body, Sees(0.6, 3.6), 0.02).StrikeSide);
            body.HitLimb(StriderLayout.Limb(StriderLayout.Left, 0), 2, 0, sever: true);
            StriderOrders cut = brain.Think(body, Sees(0.6, 3.6), 0.02);
            Assert.Less(cut.StrikeSide, 0, "a claw shot off in mid swing ends the swing");
            StriderOrders flee = brain.Think(body, Sees(0.0, 6.0), 0.02);
            Assert.IsTrue(brain.Fleeing);
            Assert.AreNotEqual(0.0, flee.TurnRate);
            StriderOrders going = brain.Think(body, Sees(0.0, -6.0), 0.02);
            Assert.Greater(going.Velocity.Z, 0.0, "with the player behind it, it walks away");
            Assert.AreEqual(0.0, brain.Think(body, Sees(0.0, -(rules.fleeTo + 1)), 0.02).Velocity.Z, "far enough: it stops");
        }

        [Test]
        public void Alone_ItWandersNearHome_AndTheSameSeedWandersTheSameWay()
        {
            var rules = new StriderRules();
            double Walked(int seed, out double x, out double z)
            {
                var body = new StriderBody();
                var brain = new StriderBrain(rules, seed);
                double yaw = 0, far = 0;
                x = 0; z = 0;
                for (int i = 0; i < 60 * 240; i++)
                {
                    const double dt = 1.0 / 60.0;
                    // Home in its own space: the world's origin turned by its facing.
                    double c = Math.Cos(-yaw), s = Math.Sin(-yaw);
                    var home = new Vec3d(-(x * c + z * s), 0, -(-x * s + z * c));
                    StriderOrders o = brain.Think(body, new StriderSenses { Home = home }, dt);
                    Assert.Less(o.StrikeSide, 0);
                    yaw += o.TurnRate * dt;
                    x += Math.Sin(yaw) * o.Velocity.Z * dt;
                    z += Math.Cos(yaw) * o.Velocity.Z * dt;
                    far = Math.Max(far, Math.Sqrt(x * x + z * z));
                }
                return far;
            }
            double first = Walked(7, out double x1, out double z1);
            Assert.Greater(first, 3.0, "it does go somewhere");
            Assert.Less(first, rules.wanderRadius + 8.0, "it comes back when it strays");
            Walked(7, out double x2, out double z2);
            Assert.AreEqual(x1, x2, 1e-9);
            Assert.AreEqual(z1, z2, 1e-9);
        }

        // ---- the model

        static string ModelsFolder()
        {
            string env = Environment.GetEnvironmentVariable("OA_MODELS");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models") })
                    if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        static ModelFile Load(string id)
        {
            string folder = ModelsFolder();
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            if (!File.Exists(path)) Assert.Ignore("the Strider's model is not in the game yet: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        static void AssertSocket(ModelFile model, string name, Vec3d expected)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(expected.X, s.X, 0.002, name + " x");
            Assert.AreEqual(expected.Y, s.Y, 0.002, name + " y");
            Assert.AreEqual(expected.Z, s.Z, 0.002, name + " z");
        }

        [Test]
        public void StriderModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of StriderLayout. If either changes, this fails
            // until the script is changed to match and run again.
            ModelFile model = Load(StriderLayout.Model);
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                string tag = (StriderLayout.SideOf(limb) == StriderLayout.Right ? "R" : "L") + StriderLayout.PlaceOf(limb);
                AssertSocket(model, "Hip" + tag, StriderLayout.HipModelled(limb));
                AssertSocket(model, "Out" + tag, StriderLayout.HipModelled(limb) + StriderLayout.Out(limb));
                for (int s = 0; s < StriderLayout.Segments; s++) Assert.IsNotNull(model.Part(StriderLayout.Piece(limb, s)), "missing piece " + StriderLayout.Piece(limb, s));
            }
            Assert.IsTrue(model.TrySocket("LegUpperEnd", out ModelSocket end));
            Assert.AreEqual(StriderLayout.LegUpper, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("LegLowerEnd", out end));
            Assert.AreEqual(StriderLayout.LegLower, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("LegTipEnd", out end));
            Assert.AreEqual(StriderLayout.LegTip, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("ArmUpperEnd", out end));
            Assert.AreEqual(StriderLayout.ArmUpper, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("ArmLowerEnd", out end));
            Assert.AreEqual(StriderLayout.ArmLower, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("ClawEnd", out end));
            Assert.AreEqual(StriderLayout.Claw, end.Z, 0.002);
            Assert.IsTrue(model.TrySocket("FingerEnd", out end));
            Assert.AreEqual(StriderLayout.Finger, end.Z, 0.002);
            AssertSocket(model, "ClawHinge", StriderLayout.ClawHinge);
            Assert.IsTrue(model.TrySocket("StumpRim", out end));
            Assert.AreEqual(StriderLayout.StumpRadius, end.X, 0.0005);
            foreach (string piece in StriderLayout.BodyPieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            foreach (string piece in StriderLayout.Meat) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsNotNull(model.Part(StriderLayout.Stump));
            Assert.IsNotNull(model.Part(StriderLayout.ClawFinger));
            Assert.IsTrue(model.Textured, "the Strider is drawn with pictures");

            // As drawn: the body, eight legs of three pieces, two arms of three and their fingers.
            int Triangles(ModelFile m, string piece) => m.Part(piece).TriangleCount;
            int Drawn(ModelFile m) => Triangles(m, "Carapace") + Triangles(m, "Belly")
                                      + 8 * (Triangles(m, "LegUpper") + Triangles(m, "LegLower") + Triangles(m, "LegTip"))
                                      + 2 * (Triangles(m, "ArmUpper") + Triangles(m, "ArmLower") + Triangles(m, "Claw") + Triangles(m, "ClawFinger"));
            int full = Drawn(model);
            Assert.LessOrEqual(full, 22000, "an enemy body's allowance (the trooper's)");
            ModelFile far = Load(StriderLayout.Model + ModelFile.FarSuffix);
            Assert.AreEqual(model.TextureSet, far.TextureSet);
            Assert.AreEqual(model.Parts.Length, far.Parts.Length);
            Assert.LessOrEqual(Drawn(far), full * 0.45, "the far copy is not much lighter than the full model");
            // Every piece is modelled from its own joint, along +Z.
            foreach (string piece in new[] { "LegUpper", "LegLower", "LegTip", "ArmUpper", "ArmLower", "Claw", "ClawFinger" })
            {
                ModelPart part = model.Part(piece);
                float minZ = float.MaxValue, maxZ = float.MinValue, side = 0;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    minZ = Math.Min(minZ, part.Positions[i * 3 + 2]);
                    maxZ = Math.Max(maxZ, part.Positions[i * 3 + 2]);
                    side = Math.Max(side, Math.Abs(part.Positions[i * 3]));
                }
                Assert.Greater(minZ, -0.12f, piece + " reaches behind its joint");
                Assert.Greater(maxZ, 0.3f, piece + " does not run along +Z");
                Assert.Less(side, 0.2f, piece + " is not a limb along +Z");
            }
        }
    }
}
