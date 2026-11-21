using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Clear goals (21 Nov 2026): ante 1's clock waiting for liftoff, the one-time tips and their order,
    /// and the "left to score here" line. Plain C#.
    /// </summary>
    public class RunGuideTests
    {
        static readonly RunGuide.Keys K = new RunGuide.Keys
        {
            Map = "M", Lock = "T", Autopilot = "Y", Pause = "Esc", LiftOff = "Space", Interact = "E", Upgrades = "Tab",
        };

        [Test]
        public void TheClock_WaitsOnThePad_AndStartsAtLiftoff()
        {
            var hold = new RunStartHold();
            hold.Step(5f, 0.2f, false, false);
            Assert.IsTrue(hold.Holding, "sitting on the pad");
            hold.Step(1f, RunStartHold.LiftedAt + 1f, false, false);
            Assert.IsFalse(hold.Holding);
            Assert.AreEqual("LIFTOFF", hold.Released);
        }

        [Test]
        public void TheClock_StartsAtTheFirstPoint_SoTheHoldCannotBeFarmed()
        {
            var hold = new RunStartHold();
            hold.Step(1f, 0f, false, true);
            Assert.IsFalse(hold.Holding);
            Assert.AreEqual("FIRST POINTS", hold.Released);
        }

        [Test]
        public void TheClock_StartsWhenYouStepOutside_OrAfterAMinute()
        {
            var outside = new RunStartHold();
            outside.Step(1f, 0f, true, false);
            Assert.IsFalse(outside.Holding);

            var lingering = new RunStartHold();
            for (int i = 0; i < 59; i++) lingering.Step(1f, 0f, false, false);
            Assert.IsTrue(lingering.Holding);
            lingering.Step(1.5f, 0f, false, false);
            Assert.IsFalse(lingering.Holding, "never waits longer than " + RunStartHold.MaxHoldSeconds + " s");
        }

        [Test]
        public void ResetHoldsTheClockAgain()
        {
            var hold = new RunStartHold();
            hold.Step(1f, 100f, false, false);
            hold.Reset();
            Assert.IsTrue(hold.Holding);
            Assert.AreEqual(string.Empty, hold.Released);
        }

        [Test]
        public void Tips_ComeInTheOrderTheyAreNeeded_EachOnlyOnce()
        {
            var seen = new HashSet<GuideTip>();
            var s = new GuideState { Running = true, SecondsLeft = 300f };
            Assert.AreEqual(GuideTip.None, RunGuide.Next(s, seen), "nothing on the pad: the goal card covers it");

            s.NearAWorld = true;
            s.WorldHasTasks = true;
            Assert.AreEqual(GuideTip.None, RunGuide.Next(s, seen), "on the pad, nothing but the goal card");

            s.Lifted = true;
            s.AtTheControls = true;
            Assert.AreEqual(GuideTip.OpenMap, RunGuide.Next(s, seen));
            seen.Add(GuideTip.OpenMap);

            s.MapOpenedThisRun = true;
            Assert.AreEqual(GuideTip.LockAndFly, RunGuide.Next(s, seen));
            seen.Add(GuideTip.LockAndFly);

            Assert.AreEqual(GuideTip.WhatScores, RunGuide.Next(s, seen));
            seen.Add(GuideTip.WhatScores);

            s.Scored = true;
            Assert.AreEqual(GuideTip.FirstPoints, RunGuide.Next(s, seen));
            seen.Add(GuideTip.FirstPoints);

            s.TargetMet = true;
            Assert.AreEqual(GuideTip.Bank, RunGuide.Next(s, seen));
            seen.Add(GuideTip.Bank);
            Assert.AreEqual(GuideTip.None, RunGuide.Next(s, seen));
        }

        [Test]
        public void NeverOpeningTheMap_DoesNotHoldBackTheOtherTips()
        {
            var seen = new HashSet<GuideTip> { GuideTip.OpenMap };
            var s = new GuideState { Running = true, SecondsLeft = 300f, Lifted = true, NearAWorld = true, WorldHasTasks = true };
            Assert.AreEqual(GuideTip.WhatScores, RunGuide.Next(s, seen));
        }

        [Test]
        public void MapTips_OnlyAtThePilotsSeat()
        {
            var s = new GuideState { Running = true, SecondsLeft = 300f, Lifted = true, AtTheControls = false };
            Assert.AreEqual(GuideTip.None, RunGuide.Next(s, new HashSet<GuideTip>()));
            s.AtTheControls = true;
            Assert.AreEqual(GuideTip.OpenMap, RunGuide.Next(s, new HashSet<GuideTip>()));
        }

        [Test]
        public void ARunOffThePad_StartsItsClockAtOnce()
        {
            var hold = new RunStartHold();
            hold.ReleaseNow("IN SPACE");
            Assert.IsFalse(hold.Holding);
            Assert.AreEqual("IN SPACE", hold.Released);
        }

        [Test]
        public void ALowClock_ComesBeforeOtherTips_ButNotOnceTheTargetIsMet()
        {
            var seen = new HashSet<GuideTip>();
            var s = new GuideState { Running = true, Lifted = true, Scored = true, SecondsLeft = 45f };
            Assert.AreEqual(GuideTip.HurryUp, RunGuide.Next(s, seen));
            s.TargetMet = true;
            Assert.AreEqual(GuideTip.Bank, RunGuide.Next(s, seen));
        }

        [Test]
        public void NoTips_BetweenAntes()
        {
            var s = new GuideState { Running = false, Lifted = true, Scored = true, TargetMet = true };
            Assert.AreEqual(GuideTip.None, RunGuide.Next(s, new HashSet<GuideTip>()));
        }

        [Test]
        public void EveryTip_HasWords_AndNamesItsKeys()
        {
            foreach (GuideTip tip in new[] { GuideTip.OpenMap, GuideTip.LockAndFly, GuideTip.WhatScores, GuideTip.FirstPoints, GuideTip.Bank, GuideTip.HurryUp })
                Assert.IsNotEmpty(RunGuide.Text(tip, K, 850), tip.ToString());
            StringAssert.Contains("M", RunGuide.Text(GuideTip.OpenMap, K, 850));
            StringAssert.Contains("850", RunGuide.Text(GuideTip.FirstPoints, K, 850));
            StringAssert.Contains("Esc", RunGuide.Text(GuideTip.Bank, K, 850));
        }

        [Test]
        public void TheGoalCard_SaysTheTargetTheClockAndHowTheRunEnds()
        {
            string card = RunGuide.GoalCard(850, 300, 9, K);
            StringAssert.Contains("850", card);
            StringAssert.Contains("5 minute", card);
            StringAssert.Contains("9 antes", card);
            StringAssert.Contains("die", card);
            StringAssert.Contains("Space", card);
            StringAssert.Contains("Tab opens the shop", card);
        }

        [Test]
        public void SeenTips_SurviveBeingStored_AndJunkIsDropped()
        {
            var seen = new HashSet<GuideTip> { GuideTip.Goal, GuideTip.Bank, GuideTip.OpenMap };
            string stored = RunGuide.StoreSeen(seen);
            Assert.AreEqual("1,2,6", stored);
            CollectionAssert.AreEquivalent(seen, RunGuide.ParseSeen(stored));
            CollectionAssert.AreEquivalent(new[] { GuideTip.OpenMap }, RunGuide.ParseSeen("2,,x,99,0"));
            Assert.IsEmpty(RunGuide.ParseSeen(null));
        }

        [Test]
        public void TheWorldLine_ListsWhatIsLeft_OrSaysToMoveOn()
        {
            Assert.AreEqual("MARS: 2 BASES · 1 MAST", WorldTaskLine.Compose("Mars", 2, 1, 0));
            Assert.AreEqual("MOON: 1 BASE · 3 CRATES", WorldTaskLine.Compose("Moon", 1, 0, 3));
            Assert.AreEqual("EUROPA: ALL DONE. FLY TO ANOTHER WORLD", WorldTaskLine.Compose("Europa", 0, 0, 0));
            Assert.AreEqual(string.Empty, WorldTaskLine.Compose("Mars", -1, 0, 0), "not known yet");
        }
    }
}
