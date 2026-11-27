using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Drifting maw (the megamouth shark): what comes off and what kills it, who it notices, the warning, the pull and the way out of it, the hold, the choke, and how it moves.</summary>
    public class MawRulesTests
    {
        const double Dt = 1.0 / 60.0;

        /// <summary>Open water: the Maw's head and heading, a player who floats where the test says (and is moved by its pull), and the senses that follow.</summary>
        sealed class World
        {
            public readonly MawRules Rules = new MawRules();
            public readonly MawBody Body;
            public readonly MawBrain Brain;
            public readonly MawAnimator Animator = new MawAnimator();
            public readonly MawPose Pose = new MawPose();
            public Vec3d Root = Vec3d.Zero, Player = new Vec3d(0, 0, 20), PlayerVelocity = Vec3d.Zero, Home = Vec3d.Zero;
            /// <summary>What the player's own jets add, in world space, metres a second squared.</summary>
            public Vec3d Jets = Vec3d.Zero;
            public double Yaw, Time, Hurt, WaterAbove = 30, FloorBelow = 30, MostTurn, Climbed;
            public bool Hostile = true, InWater = true, Blocked, Held, WasHeld, Spat;
            public MawOrders Last;
            public readonly List<MawMode> Modes = new List<MawMode> { MawMode.Drift };
            public Action<World> Before;

            public World()
            {
                Body = new MawBody(Rules.hp);
                Brain = new MawBrain(Rules);
            }

            public Vec3d ToCreature(Vec3d world)
            {
                Vec3d d = world - Root;
                double sin = Math.Sin(Yaw), cos = Math.Cos(Yaw);
                return new Vec3d(cos * d.X - sin * d.Z, d.Y, sin * d.X + cos * d.Z);
            }

            public Vec3d ToWorldWay(Vec3d creature)
            {
                double sin = Math.Sin(Yaw), cos = Math.Cos(Yaw);
                return new Vec3d(cos * creature.X + sin * creature.Z, creature.Y, -sin * creature.X + cos * creature.Z);
            }

            public Vec3d MouthWorld => Root + ToWorldWay(MawLayout.Mouth);
            public double FromMouth => (Player - MouthWorld).Magnitude;

            public MawSenses Senses() => new MawSenses
            {
                Hostile = Hostile, PlayerInWater = InWater, Player = ToCreature(Player), Home = ToCreature(Home),
                WaterAbove = WaterAbove - Root.Y, FloorBelow = FloorBelow + Root.Y, Blocked = Blocked,
            };

            public void Step()
            {
                Before?.Invoke(this);
                MawOrders o = Brain.Think(Dt, Senses(), Body);
                Last = o;
                if (o.Mode != Modes[Modes.Count - 1]) Modes.Add(o.Mode);
                Hurt += o.Bite;
                MostTurn = Math.Max(MostTurn, Math.Abs(o.TurnRate));
                Yaw += o.TurnRate * Dt;
                Vec3d moved = new Vec3d(0, o.Climb, o.Speed);
                Root += ToWorldWay(moved) * Dt;
                Climbed += o.Climb * Dt;
                Animator.Evaluate(Dt, new MawAnimInput { Velocity = moved, TurnRate = o.TurnRate, Mode = o.Mode, T = o.T, List = Body.List, BackFinOn = Body.BackFinOn }, Pose);
                Held = o.Holding;
                if (Held)
                {
                    WasHeld = true;
                    Player = MouthWorld;
                    PlayerVelocity = Vec3d.Zero;
                }
                else
                {
                    if (o.Spit.SqrMagnitude > 0) { PlayerVelocity = ToWorldWay(o.Spit); Spat = true; }
                    PlayerVelocity += (ToWorldWay(o.Pull) + Jets) * Dt;
                    // Water takes speed away.
                    PlayerVelocity -= PlayerVelocity * Math.Min(1.0, 1.2 * Dt);
                    Player += PlayerVelocity * Dt;
                }
                Time += Dt;
            }

            public bool RunUntil(Func<World, bool> done, double seconds)
            {
                for (int i = 0; i < seconds / Dt; i++)
                {
                    Step();
                    if (done(this)) return true;
                }
                return false;
            }

            public void Run(double seconds) => RunUntil(_ => false, seconds);
        }

        // ------------------------------------------------------------------------------------------------ the body

        [Test]
        public void OnlyItsBodyOrItsSkull_KillsIt()
        {
            var body = new MawBody();
            for (int side = 0; side < MawLayout.Fins; side++) Assert.IsFalse(body.HitFin(side, 1000).Killed);
            Assert.IsFalse(body.HitBackFin(1000).Killed);
            Assert.IsFalse(body.HitTailFin(1000).Killed);
            Assert.IsFalse(body.HitJaw(1000).Killed);
            Assert.IsTrue(body.Alive, "with every fin and its jaw gone it is still alive");
            Assert.IsTrue(body.HitTrunk(1000).Killed);
            Assert.IsTrue(new MawBody().HitHead(1000).Killed);
        }

        [Test]
        public void WhatItLoses_ChangesWhatItCanDo()
        {
            var body = new MawBody();
            Assert.AreEqual(1.0, body.TurnShare, 1e-9);
            Assert.AreEqual(0.0, body.List, 1e-9);
            Assert.IsTrue(body.HitFin(MawLayout.RightSide, 1000).FinLost == MawLayout.RightSide);
            Assert.Less(body.TurnShare, 1.0, "one front fin: it turns slower");
            Assert.AreEqual(1.0, body.List, 1e-9, "and lists to the side that has none");
            double one = body.TurnShare;
            body.HitFin(MawLayout.LeftSide, 1000);
            Assert.Less(body.TurnShare, one);
            Assert.AreEqual(0.0, body.List, 1e-9);
            Assert.IsTrue(body.HitTailFin(1000).TailFinLost);
            Assert.Less(body.PaceShare, 0.6, "no tail fin: it is slow");
            Assert.IsTrue(body.Bites);
            Assert.IsTrue(body.HitJaw(1000).JawLost);
            Assert.IsFalse(body.Bites, "no jaw: it cannot hold or bite");
            Assert.Less(body.PullShare, 0.5, "and its pull is weak");
        }

        [Test]
        public void AFin_ComesOffInOneToThreeRifleHits_AndAShotDownTheThroat_DoesMore()
        {
            MawHp hp = MawHp.Default;
            const double rifle = 28;
            foreach (double limb in new[] { hp.Fin, hp.BackFin })
                Assert.LessOrEqual(Math.Ceiling(limb / rifle), 3, "limbs stay low");
            var body = new MawBody();
            MawHit plain = body.HitTrunk(20);
            Assert.AreEqual(0.0, plain.Throat, 1e-9);
            Assert.AreEqual(hp.Trunk - 20, body.Trunk, 1e-9);
            MawHit throat = body.HitTrunk(20, throat: true);
            Assert.AreEqual(20 * MawBody.ThroatTimes, throat.Throat, 1e-9);
            Assert.AreEqual(hp.Trunk - 20 - 20 * MawBody.ThroatTimes, body.Trunk, 1e-9);
            // Killing it through its throat takes well under half the shots.
            Assert.Less(hp.Trunk / (rifle * MawBody.ThroatTimes), 0.5 * hp.Trunk / rifle);
        }

        // ------------------------------------------------------------------------------------------------ the brain

        [Test]
        public void WithNobodyInTheWater_ItCruisesACircleRoundHome()
        {
            var w = new World { InWater = false, Player = new Vec3d(0, 0, 8), Root = new Vec3d(0, 0, 10) };
            double furthest = 0, nearest = double.MaxValue;
            for (int i = 0; i < 90 / Dt; i++)
            {
                w.Step();
                if (w.Time < 30) continue;
                double r = Math.Sqrt(w.Root.X * w.Root.X + w.Root.Z * w.Root.Z);
                furthest = Math.Max(furthest, r);
                nearest = Math.Min(nearest, r);
            }
            Assert.AreEqual(1, w.Modes.Count, "someone on the shore, however near, is nothing to it");
            Assert.Less(furthest, w.Rules.driftRadius * 1.5);
            Assert.Greater(nearest, w.Rules.driftRadius * 0.5);
            Assert.AreEqual(0.0, w.Hurt, 1e-9);
        }

        [Test]
        public void SomeoneInTheWater_IsApproached_Warned_AndDrawnIn()
        {
            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Gape, 30), "it comes and opens its mouth");
            double began = w.Time;
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Inhale, 5));
            Assert.GreaterOrEqual(w.Time - began, 1.0, "the warning lasts at least a second");
            Assert.IsTrue(w.RunUntil(x => x.Held, w.Rules.inhaleSeconds + 0.5), "someone who only floats there ends up in its mouth");
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Spent, 5));
            Assert.IsTrue(w.Spat, "and is spat out");
            Assert.AreEqual(w.Rules.chewDamage * w.Rules.chewBeats, w.Hurt, 1e-6, "bitten once a beat while it holds");
            w.Run(0.5);
            Assert.Greater(w.FromMouth, 1.5, "thrown clear of its mouth");
            CollectionAssert.AreEqual(new[] { MawMode.Drift, MawMode.Approach, MawMode.Gape, MawMode.Inhale, MawMode.Chew, MawMode.Spent }, w.Modes);
        }

        [Test]
        public void ItTakesAimPartWayThroughTheWarning_AndThenHardlyTurns()
        {
            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Gape, 30));
            Assert.IsTrue(w.RunUntil(x => x.Last.Locked, 5));
            Assert.AreEqual(MawBrain.LockAt, w.Last.T, 0.03);
            w.MostTurn = 0;
            w.RunUntil(x => x.Last.Mode != MawMode.Gape, 5);
            Assert.AreEqual(0.0, w.MostTurn, 1e-9, "aimed: it does not follow any more");
            w.MostTurn = 0;
            // The player swims hard to one side while it draws in.
            w.Jets = new Vec3d(14, 0, 0);
            w.RunUntil(x => x.Last.Mode != MawMode.Inhale, 5);
            Assert.LessOrEqual(w.MostTurn, w.Rules.turn * MawBrain.InhaleTurn + 1e-9, "drawing in, it can only swing round slowly");
            Assert.IsFalse(w.WasHeld, "so going sideways gets you out of it");
            Assert.AreEqual(0.0, w.Hurt, 1e-9);
        }

        [Test]
        public void ThePull_IsOnlyInTheConeInFrontOfItsMouth_AndStrongerNearer()
        {
            var rules = new MawRules();
            Vec3d m = MawLayout.Mouth;
            Vec3d ahead = MawBrain.PullAt(m + new Vec3d(0, 0, 6), rules, 1.0);
            Assert.Less(ahead.Z, 0, "toward its mouth");
            Assert.AreEqual(0.0, ahead.X, 1e-9);
            Vec3d nearer = MawBrain.PullAt(m + new Vec3d(0, 0, 2), rules, 1.0);
            Assert.Greater(nearer.Magnitude, ahead.Magnitude);
            Assert.LessOrEqual(nearer.Magnitude, rules.pullAccel);
            Vec3d edge = MawBrain.PullAt(m + new Vec3d(0, 0, rules.pullReach - 0.01), rules, 1.0);
            Assert.AreEqual(rules.pullAccel * MawBrain.EdgeShare, edge.Magnitude, 0.05);
            Assert.AreEqual(0.0, MawBrain.PullAt(m + new Vec3d(0, 0, rules.pullReach + 0.5), rules, 1.0).Magnitude, 1e-9, "beyond its reach");
            Assert.AreEqual(0.0, MawBrain.PullAt(m + new Vec3d(6, 0, 3), rules, 1.0).Magnitude, 1e-9, "off to the side");
            Assert.AreEqual(0.0, MawBrain.PullAt(m + new Vec3d(0, 0, -6), rules, 1.0).Magnitude, 1e-9, "behind it");
            Assert.AreEqual(0.0, MawBrain.PullAt(m + new Vec3d(0, 5, 3), rules, 1.0).Magnitude, 1e-9, "above it");
            Assert.AreEqual(ahead.Magnitude * 0.4, MawBrain.PullAt(m + new Vec3d(0, 0, 6), rules, 0.4).Magnitude, 1e-9);
        }

        [Test]
        public void LeavingTheWaterBeforeItTakesAim_CallsItOff()
        {
            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Gape, 30));
            w.Run(0.2);
            w.InWater = false;
            w.Step();
            w.Step();
            Assert.AreEqual(MawMode.Drift, w.Last.Mode);
            w.Run(10);
            Assert.AreEqual(0.0, w.Hurt, 1e-9);
            Assert.AreEqual(0.0, w.Last.Pull.Magnitude, 1e-9);
        }

        [Test]
        public void EnoughDownItsThroat_MakesItChoke_AndLetGo_ButNotTwiceRunning()
        {
            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Held, 40));
            Assert.IsFalse(w.Brain.Throat(w.Rules.chokeDamage * 0.5));
            Assert.IsTrue(w.Brain.Throat(w.Rules.chokeDamage * 0.5), "the rest of it tips it over");
            w.Step();
            Assert.AreEqual(MawMode.Reel, w.Last.Mode);
            Assert.IsFalse(w.Held, "it lets go");
            double hurt = w.Hurt;
            w.Run(w.Rules.reelSeconds - 0.1);
            Assert.AreEqual(hurt, w.Hurt, 1e-9);
            // Straight back into its mouth: it cannot be choked again yet.
            Assert.IsFalse(w.Brain.Throat(1000), "it is not warning, pulling or holding");
            w.Player = w.Root + w.ToWorldWay(new Vec3d(0, 0, 8));
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Gape, 40));
            if (w.Time < w.Rules.steadySeconds) Assert.IsFalse(w.Brain.Throat(1000));
        }

        [Test]
        public void WithoutItsJaw_ItCannotHold_AndMakesOffForGood()
        {
            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Held, 40));
            double hurt = w.Hurt;
            Assert.IsTrue(w.Body.HitJaw(1000).JawLost);
            w.Step();
            Assert.IsFalse(w.Held, "the jaw shot off frees whoever it holds at once");
            Assert.AreEqual(MawMode.Flee, w.Last.Mode);
            double from = (w.Player - w.Root).Magnitude;
            w.Run(w.Rules.fleeSeconds - 0.5);
            Assert.Greater((w.Player - w.Root).Magnitude, from + 10, "it makes off");
            w.Run(2);
            Assert.IsTrue(w.Brain.Harmless);
            w.Player = w.Root + w.ToWorldWay(new Vec3d(0, 0, 6));
            w.Run(20);
            Assert.AreEqual(MawMode.Drift, w.Last.Mode, "after that it only drifts");
            Assert.AreEqual(hurt, w.Hurt, 1e-9);
        }

        [Test]
        public void HurtFromTheShore_ItSwimsAwayAndDown_HurtInTheWater_ItComes()
        {
            var w = new World { InWater = false, Player = new Vec3d(0, 12, 20) };
            w.Run(1);
            w.Brain.Hurt(reachable: false);
            w.Climbed = 0;
            w.Run(3);
            Assert.AreEqual(MawMode.Shy, w.Last.Mode);
            Assert.Less(w.Climbed, -1.0, "down");
            Assert.AreEqual(0.0, w.Hurt, 1e-9);

            var near = new World { Player = new Vec3d(0, 0, 45) };
            near.Run(1);
            Assert.AreEqual(MawMode.Drift, near.Last.Mode, "too far to notice");
            near.Brain.Hurt(reachable: true);
            near.Step();
            Assert.AreEqual(MawMode.Approach, near.Last.Mode);
        }

        [Test]
        public void ItKeepsWaterOverItsBackAndUnderItsBelly()
        {
            // The player floats at the surface, above a Maw in a shallow sea.
            var w = new World { WaterAbove = 6, FloorBelow = 4, Player = new Vec3d(0, 5.8, 18) };
            double highest = double.MinValue, lowest = double.MaxValue;
            for (int i = 0; i < 40 / Dt; i++)
            {
                w.Step();
                highest = Math.Max(highest, w.Root.Y);
                lowest = Math.Min(lowest, w.Root.Y);
            }
            Assert.Less(highest, 6 - w.Rules.keepUnder + 0.6, "it does not break the surface");
            Assert.Greater(lowest, -4 + w.Rules.keepOff - 0.6, "or plough the bottom");
        }

        [Test]
        public void ItNeverGoesAfterAnyoneFarFromWhereItLives()
        {
            var w = new World { Player = new Vec3d(0, 0, 20) };
            // The player backs away for ever, just ahead of it.
            w.Before = x => x.Player = new Vec3d(0, 0, Math.Max(x.Player.Z, x.Root.Z + 16));
            double furthest = 0;
            for (int i = 0; i < 120 / Dt; i++)
            {
                w.Step();
                furthest = Math.Max(furthest, w.Root.Magnitude);
            }
            Assert.Less(furthest, w.Rules.roamRadius + 8);
            Assert.AreEqual(MawMode.Drift, w.Last.Mode);
        }

        // ------------------------------------------------------------------------------------------------ the animator

        [Test]
        public void ItsLengths_MeetAtTheirJoints_HoweverItBendsOrThrashes()
        {
            var w = new World { Player = new Vec3d(14, 3, 14) };
            double worst = 0;
            Action check = () =>
            {
                for (int i = 0; i + 1 < MawLayout.Nodes; i++)
                {
                    Vec3d backOfThis = w.Pose.NodePosition[i] + w.Pose.NodeRotation[i].Rotate(new Vec3d(0, 0, -MawLayout.Spacing * 0.5));
                    Vec3d frontOfNext = w.Pose.NodePosition[i + 1] + w.Pose.NodeRotation[i + 1].Rotate(new Vec3d(0, 0, MawLayout.Spacing * 0.5));
                    worst = Math.Max(worst, (backOfThis - frontOfNext).Magnitude);
                }
            };
            for (int i = 0; i < 20 / Dt; i++) { w.Step(); check(); }
            Assert.IsTrue(w.RunUntil(x => x.Held, 40));
            w.Brain.Throat(1000);
            for (int i = 0; i < 2 / Dt; i++) { w.Step(); check(); }
            Assert.Less(worst, 0.06, "the back of each length is where the front of the next is");
        }

        [Test]
        public void ItsMouth_IsAjarCruising_FallsWideWhenItTakesAim_AndIsShutWhenSpent()
        {
            Assert.Less(MawAnimator.GapeOf(MawMode.Drift, 0), 0.3);
            Assert.Greater(MawAnimator.GapeOf(MawMode.Drift, 0), 0.05);
            Assert.Less(MawAnimator.GapeOf(MawMode.Gape, MawBrain.LockAt - 0.01), 0.6, "before it takes aim it is only part open");
            Assert.AreEqual(1.0, MawAnimator.GapeOf(MawMode.Gape, MawBrain.LockAt + 0.11), 1e-6, "then it falls wide");
            Assert.AreEqual(1.0, MawAnimator.GapeOf(MawMode.Inhale, 0.5), 1e-9);
            Assert.Less(MawAnimator.GapeOf(MawMode.Spent, 0.5), 0.1);

            var w = new World { Player = new Vec3d(0, 0, 22) };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == MawMode.Inhale, 40));
            w.Run(0.5);
            Assert.Greater(w.Pose.Gape, 0.95);
            // The jaw's tip is well below the head's line, and the mouth is in front of the head.
            Vec3d tip = w.Pose.JawPosition + w.Pose.JawRotation.Rotate(new Vec3d(0, 0, MawLayout.JawLength));
            Assert.Less(tip.Y, w.Pose.NodePosition[0].Y - 0.8);
            Assert.Greater(w.Pose.Mouth.Z, w.Pose.NodePosition[0].Z + 1.0);
        }

        [Test]
        public void ItsFrontFins_PointOutEitherSide_LeadingEdgeAhead()
        {
            var w = new World { InWater = false };
            w.Run(5);
            int node = MawLayout.FinSeg + 1;
            QuatD body = w.Pose.NodeRotation[node];
            for (int s = 0; s < MawLayout.Fins; s++)
            {
                Vec3d along = body.Inverse.Rotate(w.Pose.FinRotation[s].Rotate(Vec3d.Forward));
                Vec3d edge = body.Inverse.Rotate(w.Pose.FinRotation[s].Rotate(Vec3d.Right));
                Assert.Greater(along.X * MawLayout.Sign(s), 0.5, "out to its own side");
                Assert.Less(along.Y, 0.0, "and a little down");
                Assert.Greater(edge.Z, 0.6, "the same edge leads on both sides");
            }
        }

        [Test]
        public void ItLeansIntoATurn_AndListsToTheSideOfALostFin()
        {
            var animator = new MawAnimator();
            var pose = new MawPose();
            for (int i = 0; i < 240; i++)
                animator.Evaluate(Dt, new MawAnimInput { Velocity = new Vec3d(0, 0, 3), TurnRate = 0.7, Mode = MawMode.Circle, BackFinOn = true }, pose);
            Vec3d right = pose.NodeRotation[2].Rotate(Vec3d.Right);
            Assert.Less(right.Y, -0.05, "turning right, its right side is down");

            animator = new MawAnimator();
            for (int i = 0; i < 240; i++)
                animator.Evaluate(Dt, new MawAnimInput { Velocity = new Vec3d(0, 0, 2), Mode = MawMode.Drift, List = -1, BackFinOn = true }, pose);
            right = pose.NodeRotation[2].Rotate(Vec3d.Right);
            Assert.Greater(right.Y, 0.05, "with its left fin gone its left side hangs");
        }

        [Test]
        public void ItsBody_FollowsItsHead_UpAndDown()
        {
            var animator = new MawAnimator();
            var pose = new MawPose();
            for (int i = 0; i < 300; i++)
                animator.Evaluate(Dt, new MawAnimInput { Velocity = new Vec3d(0, 1.5, 2.5), Mode = MawMode.Approach, BackFinOn = true }, pose);
            Assert.Less(pose.NodePosition[MawLayout.Nodes - 1].Y, pose.NodePosition[1].Y - 1.0, "climbing, its tail is below its shoulders");
            Vec3d nose = pose.NodeRotation[0].Rotate(Vec3d.Forward);
            Assert.Greater(nose.Y, 0.2, "and its nose points up");
        }

        // ------------------------------------------------------------------ the model

        [Test]
        public void MawModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of MawLayout (MAW). If either changes, this
            // fails until the script is changed to match and run again.
            ModelFile model = Load(MawLayout.Model);
            AssertSocket(model, "SegEnd", 0, 0, MawLayout.Spacing * 0.5);
            AssertSocket(model, "HeadEnd", 0, 0, MawLayout.HeadLength - MawLayout.Spacing * 0.5);
            AssertSocket(model, "HeadSide", MawLayout.HeadHalfWidth, 0, 0);
            AssertSocket(model, "HeadTop", 0, MawLayout.HeadHalfHeight, 0);
            AssertSocket(model, "JawHinge", MawLayout.JawHinge.X, MawLayout.JawHinge.Y, MawLayout.JawHinge.Z);
            AssertSocket(model, "JawEnd", 0, 0, MawLayout.JawLength);
            AssertSocket(model, "JawSide", MawLayout.JawHalfWidth, 0, 0);
            AssertSocket(model, "Mouth", MawLayout.Mouth.X, MawLayout.Mouth.Y, MawLayout.Mouth.Z);
            AssertSocket(model, "FinR", MawLayout.FinRoot.X, MawLayout.FinRoot.Y, MawLayout.FinRoot.Z);
            AssertSocket(model, "FinEnd", 0, 0, MawLayout.FinLength);
            AssertSocket(model, "FinChord", MawLayout.FinChord, 0, 0);
            AssertSocket(model, "BackFin", MawLayout.BackFinRoot.X, MawLayout.BackFinRoot.Y, MawLayout.BackFinRoot.Z);
            AssertSocket(model, "BackFinTop", 0, MawLayout.BackFinHeight, 0);
            AssertSocket(model, "BackFinChord", 0, 0, MawLayout.BackFinChord);
            AssertSocket(model, "TailFinUp", 0, MawLayout.TailFinUp, -MawLayout.TailFinBack);
            AssertSocket(model, "TailFinDown", 0, -MawLayout.TailFinDown, 0);
            AssertSocket(model, "StumpRim", MawLayout.StumpRadius, 0, 0);
            for (int seg = 0; seg < MawLayout.Segs; seg++) AssertSocket(model, "Size" + seg, MawLayout.HalfWidth[seg], MawLayout.HalfHeight[seg], 0);
            var pieces = new List<string> { MawLayout.HeadPiece, MawLayout.JawPiece, MawLayout.FinPiece, MawLayout.BackFinPiece, MawLayout.TailFinPiece,
                                            MawLayout.Stump, MawLayout.Meat[0], MawLayout.Meat[1] };
            for (int seg = 0; seg < MawLayout.Segs; seg++) pieces.Add(MawLayout.PieceOf(seg));
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Drifting maw is drawn with pictures");

            // `near` keeps only the corners within that distance (along) of the piece's own middle.
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
            float right, left, hi, lo, fore, aft;
            // The body is one outline cut at its joints: every length has its own piece, as wide and tall at its
            // middle as the layout (and its hitbox) says, the same either side, reaching a little into its neighbours.
            for (int seg = 0; seg < MawLayout.Segs; seg++)
            {
                string piece = MawLayout.PieceOf(seg);
                // (Lengths that carry a fixed fin or the shoulders are measured where the body alone is.)
                bool fitted = seg == MawLayout.FinSeg || seg == MawLayout.BackFinSeg || seg == 4 || seg == 6;
                Box(piece, out right, out left, out hi, out lo, out fore, out aft, 0.03f);
                if (!fitted)
                {
                    Assert.AreEqual(MawLayout.HalfWidth[seg], right, 0.03, piece + ": its width at its middle");
                    Assert.AreEqual(MawLayout.HalfHeight[seg], hi, 0.03, piece + ": its upper side");
                    Assert.AreEqual(-MawLayout.HalfHeight[seg], lo, 0.03, piece + ": its underside");
                }
                Assert.AreEqual(-right, left, 0.01, piece + " is the same either side");
                Box(piece, out right, out left, out hi, out lo, out fore, out aft);
                Assert.Greater(fore, MawLayout.Spacing * 0.5 + 0.03, piece + " reaches into the one in front of it, so a bend opens no gap");
                Assert.Less(fore, MawLayout.Spacing * 0.5 + 0.20, piece + " without running far into it");
                Assert.Less(aft, -MawLayout.Spacing * 0.5 - 0.03, piece + " reaches into the one behind");
            }
            Box(MawLayout.HeadPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(MawLayout.HeadLength - MawLayout.Spacing * 0.5, fore, 0.05, "the head is as long as the layout says");
            Assert.AreEqual(MawLayout.HeadHalfWidth, right, 0.05, "and as wide");
            Assert.AreEqual(-right, left, 0.01, "the head is the same either side");
            Assert.AreEqual(MawLayout.HeadHalfHeight, hi, 0.05, "and as tall above its middle");
            Box(MawLayout.JawPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(MawLayout.JawLength, fore, 0.05, "the jaw is as long as the layout says");
            Assert.AreEqual(-right, left, 0.01, "the jaw is the same either side");
            Assert.Less(hi, 0.06, "the jaw lies below its hinge, so shut it does not stand through the roof of its mouth");
            // A fin is flat and the same above as below: the left one is this piece turned over.
            Box(MawLayout.FinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(MawLayout.FinLength, fore, 0.06, "a front fin is as long as the layout says");
            Assert.AreEqual(hi, -lo, 0.004, "a front fin is the same above as below");
            Assert.Less(hi, 0.08, "and flat");
            Box(MawLayout.BackFinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(MawLayout.BackFinHeight, hi, 0.06, "the back fin is as tall as the layout says");
            Assert.AreEqual(-right, left, 0.004, "the back fin is the same either side");
            Box(MawLayout.TailFinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(MawLayout.TailFinUp, hi, 0.08, "the tail fin's upper blade");
            Assert.AreEqual(-MawLayout.TailFinDown, lo, 0.08, "and its lower one");
            Assert.AreEqual(-MawLayout.TailFinBack, aft, 0.10, "and how far back it sweeps");

            // The far copy is the same model in a fraction of the triangles.
            ModelFile farCopy = Load(MawLayout.Model + "Far");
            int full = 0, far = 0;
            foreach (string piece in pieces)
            {
                Assert.IsNotNull(farCopy.Part(piece), "the far copy is missing " + piece);
                full += model.Part(piece).TriangleCount;
                far += farCopy.Part(piece).TriangleCount;
            }
            Assert.LessOrEqual(full, 16000, "the full model's triangles");
            Assert.LessOrEqual(far, 4500, "the far copy's triangles");
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
