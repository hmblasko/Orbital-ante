using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Pack biter (the Goliath tigerfish): what comes off and what kills it, the pack's turns, the warning, the dart and the way out of it, how losses change it, where packs live, and its model.</summary>
    public class BiterRulesTests
    {
        const double Dt = 1.0 / 60.0;

        sealed class Fish
        {
            public BiterBody Body;
            public BiterBrain Brain;
            public Vec3d Root;
            public double Yaw;
            public BiterOrders Last;
            public readonly List<BiterMode> Modes = new List<BiterMode>();
            public double MarkStarted = -1, MarkSeconds = -1;
        }

        /// <summary>Open water, a pack round where it lives, and a player who floats where the test says.</summary>
        sealed class Water
        {
            public readonly BiterRules Rules = new BiterRules();
            public readonly BiterPack Pack = new BiterPack();
            public readonly List<Fish> Fish = new List<Fish>();
            public Vec3d Player = new Vec3d(0, 0, 10), Home = Vec3d.Zero;
            /// <summary>Metres a second the player moves at, set by the test each step if it likes.</summary>
            public Func<Water, Vec3d> PlayerVelocity = _ => Vec3d.Zero;
            public double Time, Hurt;
            public bool InWater = true;
            public int MostStriking;
            public readonly List<int> Darters = new List<int>();

            public Water(int count, bool pack = true)
            {
                for (int i = 0; i < count; i++)
                {
                    double a = i * 2.0 * Math.PI / count;
                    Fish.Add(new Fish
                    {
                        Body = new BiterBody(Rules.hp), Brain = new BiterBrain(Rules, pack ? Pack : null, i),
                        Root = new Vec3d(Math.Cos(a) * 6, 0, Math.Sin(a) * 6), Yaw = a + Math.PI * 0.5,
                    });
                }
            }

            static Vec3d ToCreature(Fish f, Vec3d world)
            {
                Vec3d d = world - f.Root;
                double sin = Math.Sin(f.Yaw), cos = Math.Cos(f.Yaw);
                return new Vec3d(cos * d.X - sin * d.Z, d.Y, sin * d.X + cos * d.Z);
            }

            static Vec3d ToCreatureWay(Fish f, Vec3d d)
            {
                double sin = Math.Sin(f.Yaw), cos = Math.Cos(f.Yaw);
                return new Vec3d(cos * d.X - sin * d.Z, d.Y, sin * d.X + cos * d.Z);
            }

            static Vec3d ToWorldWay(Fish f, Vec3d c)
            {
                double sin = Math.Sin(f.Yaw), cos = Math.Cos(f.Yaw);
                return new Vec3d(cos * c.X + sin * c.Z, c.Y, -sin * c.X + cos * c.Z);
            }

            public void Step()
            {
                Pack.Advance(Dt);
                Vec3d playerVelocity = PlayerVelocity(this);
                int striking = 0;
                for (int i = 0; i < Fish.Count; i++)
                {
                    Fish f = Fish[i];
                    if (!f.Body.Alive) continue;
                    var senses = new BiterSenses
                    {
                        Hostile = true, PlayerInWater = InWater, Player = ToCreature(f, Player), Home = ToCreature(f, Home),
                        WaterAbove = 20, FloorBelow = 20, PlayerVelocity = ToCreatureWay(f, playerVelocity),
                    };
                    BiterMode was = f.Brain.Mode;
                    f.Last = f.Brain.Think(Dt, senses, f.Body);
                    if (f.Modes.Count == 0 || f.Modes[f.Modes.Count - 1] != f.Last.Mode) f.Modes.Add(f.Last.Mode);
                    if (f.Last.Mode == BiterMode.Mark && was != BiterMode.Mark) f.MarkStarted = Time;
                    if (f.Last.Mode == BiterMode.Dart && was != BiterMode.Dart)
                    {
                        Darters.Add(i);
                        if (f.MarkStarted >= 0) f.MarkSeconds = Time - f.MarkStarted;
                    }
                    if (f.Last.Mode == BiterMode.Mark || f.Last.Mode == BiterMode.Dart) striking++;
                    Hurt += f.Last.Bite;
                    f.Yaw += f.Last.TurnRate * Dt;
                    f.Root += ToWorldWay(f, new Vec3d(0, f.Last.Climb, f.Last.Speed)) * Dt;
                }
                MostStriking = Math.Max(MostStriking, striking);
                Player += playerVelocity * Dt;
                Time += Dt;
            }

            public void Run(double seconds)
            {
                for (int i = 0; i < seconds / Dt; i++) Step();
            }

            public bool RunUntil(Func<Water, bool> done, double seconds)
            {
                for (int i = 0; i < seconds / Dt; i++)
                {
                    Step();
                    if (done(this)) return true;
                }
                return false;
            }
        }

        // ------------------------------------------------------------------ the body

        [Test]
        public void ItsFinsComeOff_InOneRifleHit_AndOnlyItsBodyOrHeadKillsIt()
        {
            const double rifle = 34.0;
            var body = new BiterBody();
            Assert.AreEqual(BiterLayout.RightSide, body.HitFin(BiterLayout.RightSide, rifle).FinLost, "a front fin is one rifle hit");
            Assert.IsTrue(body.HitBackFin(rifle).BackFinLost, "the back fin is one");
            Assert.IsFalse(body.HitTailFin(rifle).TailFinLost, "the tail takes two, clearly: stopping its dart is a choice");
            Assert.IsTrue(body.HitTailFin(rifle).TailFinLost);
            Assert.IsFalse(body.HitJaw(rifle).JawLost, "the jaw takes two");
            Assert.IsTrue(body.HitJaw(rifle).JawLost);
            Assert.IsTrue(body.Alive, "with fins, tail and jaw gone it is still alive");
            Assert.IsFalse(body.HitTrunk(rifle * 2).Killed);
            Assert.IsTrue(body.HitTrunk(rifle).Killed, "its body is three rifle hits");
            var other = new BiterBody();
            Assert.IsFalse(other.HitHead(rifle).Killed);
            Assert.IsTrue(other.HitHead(rifle).Killed, "its head is two: the weak spot pays");
        }

        [Test]
        public void WhatItLoses_ChangesWhatItCanDo()
        {
            var body = new BiterBody();
            Assert.IsTrue(body.CanStrike);
            body.HitFin(BiterLayout.LeftSide, 1000);
            Assert.Less(body.TurnShare, 1.0, "one front fin: it turns slower");
            Assert.AreEqual(-1.0, body.List, 1e-9, "and lists to the side that has none");
            body.HitTailFin(1000);
            Assert.Less(body.PaceShare, 0.5, "no tail fin: it is slow");
            Assert.IsFalse(body.CanStrike, "and cannot dart");
            var jawless = new BiterBody();
            jawless.HitJaw(1000);
            Assert.IsFalse(jawless.CanStrike, "no jaw: nothing to bite with");
        }

        [Test]
        public void AnOldConfigWithNoNumbers_GetsTheDefaults()
        {
            var body = new BiterBody(default);
            Assert.AreEqual(BiterHp.Default.Trunk, body.Trunk, 1e-9);
            Assert.IsTrue(body.FinOn(0));
        }

        // ------------------------------------------------------------------ the pack

        [Test]
        public void APack_RingsSomeoneInTheWater_AndTakesTurns_OneAtATime_EveryFishInTurn()
        {
            var water = new Water(4);
            water.Run(40);
            Assert.AreEqual(1, water.MostStriking, "never two of the pack warning or darting at once");
            Assert.GreaterOrEqual(water.Darters.Count, 6, "the turns keep coming");
            var who = new HashSet<int>(water.Darters);
            Assert.AreEqual(4, who.Count, "every fish of the pack gets its turn");
            for (int i = 1; i < water.Darters.Count; i++)
                Assert.AreNotEqual(water.Darters[i - 1], water.Darters[i], "the same fish never goes twice running while others wait");
        }

        [Test]
        public void EveryDart_IsWarnedOf_ForAtLeastHalfASecond()
        {
            var water = new Water(3);
            water.Run(30);
            foreach (Fish f in water.Fish)
                if (f.MarkSeconds >= 0) Assert.GreaterOrEqual(f.MarkSeconds, 0.5, "the flare and flash before a dart");
            Assert.GreaterOrEqual(water.Rules.markSeconds * BiterBrain.LockAt, 0.5, "and it follows for at least half a second of that, so the warning is where it will come from");
        }

        /// <summary>Round and round a circle about the pack's home at a swimmer's pace, turning back when <paramref name="reverse"/> says.</summary>
        static Func<Water, Vec3d> Circling(Func<Water, bool> reverse)
        {
            double sign = 1.0;
            bool was = false;
            return w =>
            {
                bool now = reverse(w);
                if (now && !was) sign = -sign;
                was = now;
                double r = Math.Sqrt(w.Player.X * w.Player.X + w.Player.Z * w.Player.Z);
                Vec3d round = r > 0.1 ? new Vec3d(-w.Player.Z / r, 0, w.Player.X / r) : new Vec3d(1, 0, 0);
                Vec3d inward = r > 0.1 ? new Vec3d(-w.Player.X / r, 0, -w.Player.Z / r) : Vec3d.Zero;
                return round * (3.0 * sign) + inward * (r - 10.0) * 0.5;
            };
        }

        [Test]
        public void SomeoneWhoHangsStill_OrJustDrifts_IsBitten_ButTurningBackAsItLocks_Dodges()
        {
            var still = new Water(3);
            still.Run(30);
            Assert.Greater(still.Hurt, 0.0, "a still target is bitten");

            // Swimming steadily round: it aims where they will be, so drifting is no way out.
            var drifting = new Water(3);
            drifting.PlayerVelocity = Circling(_ => false);
            drifting.Run(30);
            Assert.Greater(drifting.Hurt, 0.0, "it leads a steady swimmer");

            // The same swim, turning back the moment a fish takes aim.
            var dodging = new Water(3);
            dodging.PlayerVelocity = Circling(w => w.Fish.Exists(f => f.Last.Locked));
            dodging.Run(30);
            Assert.Less(dodging.Hurt, drifting.Hurt, "changing way once it has locked is the way out");
        }

        [Test]
        public void ItTakesNoNotice_OfSomeoneOutOfTheWater()
        {
            var water = new Water(3) { InWater = false };
            water.Run(15);
            Assert.AreEqual(0, water.Darters.Count);
            foreach (Fish f in water.Fish) Assert.AreEqual(BiterMode.Shoal, f.Last.Mode);
        }

        [Test]
        public void WithoutItsTail_ItHangsBack_AndNeverTakesATurn()
        {
            var water = new Water(2);
            water.Fish[0].Body.HitTailFin(1000);
            water.Run(30);
            Assert.IsFalse(water.Darters.Contains(0), "no tail: no dart");
            Assert.IsTrue(water.Darters.Contains(1), "the other keeps the turns going");
            Assert.AreEqual(BiterMode.Limp, water.Fish[0].Last.Mode);
        }

        [Test]
        public void WithoutItsJaw_OrAsTheLastOfItsPack_ItMakesOff()
        {
            var water = new Water(3);
            water.Run(3);
            water.Fish[0].Body.HitJaw(1000);
            water.Run(0.5);
            Assert.AreEqual(BiterMode.Flee, water.Fish[0].Last.Mode, "no jaw: it makes off");

            var pack = new Water(3);
            pack.Run(3);
            for (int i = 1; i < 3; i++)
            {
                pack.Fish[i].Body.HitHead(1000);
                pack.Fish[i].Brain.Died();
            }
            pack.Run(0.5);
            Assert.IsTrue(pack.Pack.Broken);
            Assert.AreEqual(BiterMode.Flee, pack.Fish[0].Last.Mode, "the last of a pack of three makes off");
        }

        [Test]
        public void AStrikerShotBeforeItDarts_Scatters_AndHandsTheTurnOn()
        {
            var water = new Water(3);
            Assert.IsTrue(water.RunUntil(w => w.Fish.Exists(f => f.Last.Mode == BiterMode.Mark), 20), "someone takes a turn");
            Fish striker = water.Fish.Find(f => f.Last.Mode == BiterMode.Mark);
            striker.Brain.Hurt();
            water.Step();
            Assert.AreEqual(BiterMode.Scatter, striker.Last.Mode);
            Assert.IsNull(water.Pack.Striker, "its turn is over");
            int before = water.Darters.Count;
            water.Run(6);
            Assert.Greater(water.Darters.Count, before, "and another fish takes the next one");
        }

        [Test]
        public void AFishThatDies_GivesUpItsTurn()
        {
            var water = new Water(3);
            Assert.IsTrue(water.RunUntil(w => w.Pack.Striker != null, 20));
            BiterBrain striker = water.Pack.Striker;
            Fish f = water.Fish.Find(x => x.Brain == striker);
            f.Body.HitHead(1000);
            f.Brain.Died();
            Assert.IsNull(water.Pack.Striker);
            Assert.AreEqual(2, water.Pack.Living);
        }

        // ------------------------------------------------------------------ the animation

        [Test]
        public void TheWarning_FlaresItsJaws_AndRollsItsFlankToTheLight()
        {
            var animator = new BiterAnimator();
            var pose = new BiterPose();
            for (int i = 0; i < 60; i++)
                animator.Evaluate(Dt, new BiterAnimInput { Velocity = new Vec3d(0, 0, 4), Mode = BiterMode.Ring, BackFinOn = true }, pose);
            double ringGape = pose.Gape;
            Vec3d ringRight = pose.NodeRotation[1].Rotate(Vec3d.Right);
            double half = BiterBrain.LockAt * 0.5 * 0.9;
            for (int i = 0; i < half / Dt; i++)
                animator.Evaluate(Dt, new BiterAnimInput { Velocity = new Vec3d(0, 0, 1), Mode = BiterMode.Mark, T = i * Dt / 0.9, BackFinOn = true }, pose);
            Vec3d markRight = pose.NodeRotation[1].Rotate(Vec3d.Right);
            Assert.Greater(pose.Gape, ringGape + 0.15, "its jaws open through the warning");
            Assert.Greater(Math.Abs(markRight.Y), Math.Abs(ringRight.Y) + 0.3, "it rolls onto its side, flashing its flank");
            Assert.AreEqual(1.0, BiterAnimator.GapeOf(BiterMode.Mark, BiterBrain.LockAt + 0.01), 1e-9, "aimed, its jaws are wide");
        }

        [Test]
        public void ItsBody_FollowsItsHead()
        {
            var animator = new BiterAnimator();
            var pose = new BiterPose();
            for (int i = 0; i < 300; i++)
                animator.Evaluate(Dt, new BiterAnimInput { Velocity = new Vec3d(0, 1.0, 3.0), Mode = BiterMode.Ring, BackFinOn = true }, pose);
            Assert.Less(pose.NodePosition[BiterLayout.Nodes - 1].Y, pose.NodePosition[1].Y - 0.2, "climbing, its tail is below its shoulders");
            for (int i = 1; i < BiterLayout.Nodes; i++)
                Assert.AreEqual(BiterLayout.Spacing, (pose.NodePosition[i] - pose.NodePosition[i - 1]).Magnitude, 0.08, "its lengths stay their length");
        }

        // ------------------------------------------------------------------ where packs live

        [Test]
        public void AboutThePackShareOfHaunts_HoldPacks_OfTheRightSize()
        {
            var rules = new SeaFieldRules();
            var random = new SeededRandom(5);
            int packs = 0, all = 0;
            for (int i = 0; i < 2000; i++)
            {
                var haunt = new SeaHaunt { Seed = random.Next() };
                all++;
                if (!SeaField.IsPack(rules, haunt)) continue;
                packs++;
                int size = SeaField.PackSize(rules, haunt);
                Assert.That(size, Is.InRange(rules.packLeast, rules.packMost));
            }
            Assert.AreEqual(rules.packShare, packs / (double)all, 0.05);
            rules.packShare = 0f;
            Assert.IsFalse(SeaField.IsPack(rules, new SeaHaunt { Seed = 12345 }), "a share of none gives none");
        }

        // ------------------------------------------------------------------ the model

        [Test]
        public void BiterModelFile_MatchesTheLayout()
        {
            ModelFile model = Load(BiterLayout.Model);
            AssertSocket(model, "SegEnd", 0, 0, BiterLayout.Spacing * 0.5);
            AssertSocket(model, "HeadEnd", 0, 0, BiterLayout.HeadLength - BiterLayout.Spacing * 0.5);
            AssertSocket(model, "HeadSide", BiterLayout.HeadHalfWidth, 0, 0);
            AssertSocket(model, "HeadTop", 0, BiterLayout.HeadHalfHeight, 0);
            AssertSocket(model, "JawHinge", BiterLayout.JawHinge.X, BiterLayout.JawHinge.Y, BiterLayout.JawHinge.Z);
            AssertSocket(model, "JawEnd", 0, 0, BiterLayout.JawLength);
            AssertSocket(model, "JawSide", BiterLayout.JawHalfWidth, 0, 0);
            AssertSocket(model, "Mouth", BiterLayout.Mouth.X, BiterLayout.Mouth.Y, BiterLayout.Mouth.Z);
            AssertSocket(model, "FinR", BiterLayout.FinRoot.X, BiterLayout.FinRoot.Y, BiterLayout.FinRoot.Z);
            AssertSocket(model, "FinEnd", 0, 0, BiterLayout.FinLength);
            AssertSocket(model, "FinChord", BiterLayout.FinChord, 0, 0);
            AssertSocket(model, "BackFin", BiterLayout.BackFinRoot.X, BiterLayout.BackFinRoot.Y, BiterLayout.BackFinRoot.Z);
            AssertSocket(model, "BackFinTop", 0, BiterLayout.BackFinHeight, 0);
            AssertSocket(model, "BackFinChord", 0, 0, BiterLayout.BackFinChord);
            AssertSocket(model, "TailFinUp", 0, BiterLayout.TailFinUp, -BiterLayout.TailFinBack);
            AssertSocket(model, "TailFinDown", 0, -BiterLayout.TailFinDown, 0);
            AssertSocket(model, "StumpRim", BiterLayout.StumpRadius, 0, 0);
            for (int seg = 0; seg < BiterLayout.Segs; seg++) AssertSocket(model, "Size" + seg, BiterLayout.HalfWidth[seg], BiterLayout.HalfHeight[seg], 0);
            var pieces = new List<string> { BiterLayout.HeadPiece, BiterLayout.JawPiece, BiterLayout.FinPiece, BiterLayout.BackFinPiece, BiterLayout.TailFinPiece,
                                            BiterLayout.Stump, BiterLayout.Meat[0], BiterLayout.Meat[1] };
            for (int seg = 0; seg < BiterLayout.Segs; seg++) pieces.Add(BiterLayout.PieceOf(seg));
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Pack biter is drawn with pictures");

            void Box(string piece, out float right, out float left, out float hi, out float lo, out float fore, out float aft, float near = float.MaxValue)
            {
                ModelPart part = model.Part(piece);
                right = float.MinValue; left = float.MaxValue; hi = float.MinValue; lo = float.MaxValue; fore = float.MinValue; aft = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    if (Math.Abs(part.Positions[i * 3 + 2]) > near) continue;
                    right = Math.Max(right, part.Positions[i * 3]);
                    left = Math.Min(left, part.Positions[i * 3]);
                    hi = Math.Max(hi, part.Positions[i * 3 + 1]);
                    lo = Math.Min(lo, part.Positions[i * 3 + 1]);
                    fore = Math.Max(fore, part.Positions[i * 3 + 2]);
                    aft = Math.Min(aft, part.Positions[i * 3 + 2]);
                }
            }
            // The ring of corners nearest a piece's middle (its cross-sections need not fall exactly there).
            float MiddleRing(string piece)
            {
                ModelPart part = model.Part(piece);
                float least = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++) least = Math.Min(least, Math.Abs(part.Positions[i * 3 + 2]));
                return least + 0.01f;
            }
            float right, left, hi, lo, fore, aft;
            for (int seg = 0; seg < BiterLayout.Segs; seg++)
            {
                string piece = BiterLayout.PieceOf(seg);
                // Lengths that carry a fixed fin are measured where the body alone is, and only for width.
                bool fitted = seg == BiterLayout.BackFinSeg || seg == 1 || seg == 2;
                Box(piece, out right, out left, out hi, out lo, out fore, out aft, MiddleRing(piece));
                Assert.AreEqual(BiterLayout.HalfWidth[seg], right, 0.015, piece + ": its width at its middle");
                if (!fitted)
                {
                    Assert.AreEqual(BiterLayout.HalfHeight[seg], hi, 0.015, piece + ": its upper side");
                    Assert.AreEqual(-BiterLayout.HalfHeight[seg], lo, 0.015, piece + ": its underside");
                }
                Assert.AreEqual(-right, left, 0.005, piece + " is the same either side");
                Box(piece, out right, out left, out hi, out lo, out fore, out aft);
                Assert.Greater(fore, BiterLayout.Spacing * 0.5 + 0.01, piece + " reaches into the one in front of it, so a bend opens no gap");
                Assert.Less(fore, BiterLayout.Spacing * 0.5 + 0.10, piece + " without running far into it");
                Assert.Less(aft, -BiterLayout.Spacing * 0.5 - 0.01, piece + " reaches into the one behind");
            }
            Box(BiterLayout.HeadPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(BiterLayout.HeadLength - BiterLayout.Spacing * 0.5, fore, 0.03, "the head is as long as the layout says");
            Assert.AreEqual(BiterLayout.HeadHalfWidth, right, 0.02, "and as wide");
            Assert.AreEqual(-right, left, 0.005, "the head is the same either side");
            Assert.AreEqual(BiterLayout.HeadHalfHeight, hi, 0.02, "and as tall above its middle");
            Box(BiterLayout.JawPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(BiterLayout.JawLength, fore, 0.03, "the jaw is as long as the layout says");
            Assert.AreEqual(-right, left, 0.005, "the jaw is the same either side");
            Assert.Less(hi, 0.06, "the jaw's teeth stand up from it, but not through the roof of its mouth");
            Box(BiterLayout.FinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(BiterLayout.FinLength, fore, 0.03, "a front fin is as long as the layout says");
            Assert.AreEqual(hi, -lo, 0.002, "a front fin is the same above as below");
            Box(BiterLayout.BackFinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(BiterLayout.BackFinHeight, hi, 0.03, "the back fin is as tall as the layout says");
            Assert.AreEqual(-right, left, 0.002, "the back fin is the same either side");
            Box(BiterLayout.TailFinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(BiterLayout.TailFinUp, hi, 0.04, "the tail fin's upper lobe");
            Assert.AreEqual(-BiterLayout.TailFinDown, lo, 0.04, "and its lower lobe");
            Assert.AreEqual(-BiterLayout.TailFinBack, aft, 0.05, "and how far back they reach");
        }

        static void AssertSocket(ModelFile model, string name, double x, double y, double z)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(x, s.X, 0.002, name + " x");
            Assert.AreEqual(y, s.Y, 0.002, name + " y");
            Assert.AreEqual(z, s.Z, 0.002, name + " z");
        }

        static ModelFile Load(string id)
        {
            string folder = Environment.GetEnvironmentVariable("OA_MODELS");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = null;
                string dir = TestContext.CurrentContext.TestDirectory;
                for (int i = 0; i < 10 && dir != null && folder == null; i++)
                {
                    foreach (string candidate in new[] { Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models") })
                        if (Directory.Exists(candidate)) folder = candidate;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            if (!File.Exists(path)) Assert.Ignore("this model is not in the game yet: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }
    }
}
