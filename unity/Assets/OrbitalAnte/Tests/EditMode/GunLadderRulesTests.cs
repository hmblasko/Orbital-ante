using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Combat;

namespace OrbitalAnte.Tests
{
    /// <summary>The gun ladder's rule with no Unity objects: the order of guns, counting kills, the last gun, resets.</summary>
    public class GunLadderRulesTests
    {
        static readonly HashSet<string> RealGuns = new HashSet<string> { "Pistol", "Smg", "Rifle", "Shotgun", "Sniper" };

        static GunLadder Default() => new GunLadder(new GunLadderRules(), id => RealGuns.Contains(id));

        [Test]
        public void ARunStarts_OnTheFirstGun_WithNoKills()
        {
            var ladder = Default();
            Assert.AreEqual(0, ladder.Rung);
            Assert.AreEqual("Pistol", ladder.WeaponId);
            Assert.AreEqual(0, ladder.KillsOnRung);
            Assert.AreEqual(4, ladder.KillsLeft);
            Assert.AreEqual("Shotgun", ladder.NextWeaponId);
            Assert.IsNull(ladder.PreviousWeaponId);
        }

        [Test]
        public void TheDefaultLadder_UsesEveryGunInTheGame_Once_AndTakes22KillsToTheTop()
        {
            var ladder = Default();
            Assert.AreEqual(5, ladder.RungCount);
            var seen = new HashSet<string>();
            for (int i = 0; i < ladder.RungCount; i++) Assert.IsTrue(seen.Add(ladder.WeaponAt(i)), "a gun is listed twice");
            CollectionAssert.AreEquivalent(RealGuns, seen);
            Assert.AreEqual(22, ladder.KillsToTop);
        }

        [Test]
        public void EachStep_TakesAtLeastAsManyKillsAsTheOneBefore()
        {
            GunLadderRung[] rungs = GunLadderRules.Defaults();
            for (int i = 1; i < rungs.Length - 1; i++)
                Assert.GreaterOrEqual(rungs[i].killsToNext, rungs[i - 1].killsToNext, "step " + (i + 1));
        }

        [Test]
        public void TheNextGun_ArrivesOnExactlyTheKillThatFillsTheStep()
        {
            var ladder = Default();
            for (int i = 0; i < 3; i++) Assert.IsFalse(ladder.AddKill());
            Assert.AreEqual(1, ladder.KillsLeft);
            Assert.IsTrue(ladder.AddKill(), "the fourth kill hands over the next gun");
            Assert.AreEqual(1, ladder.Rung);
            Assert.AreEqual("Shotgun", ladder.WeaponId);
            Assert.AreEqual("Pistol", ladder.PreviousWeaponId);
            Assert.AreEqual(0, ladder.KillsOnRung, "the count starts again on the new step");
            Assert.AreEqual(5, ladder.KillsLeft);
        }

        [Test]
        public void ClimbingTheWholeLadder_EndsOnTheLastGun_AndStaysThere()
        {
            var ladder = Default();
            int climbs = 0;
            for (int i = 0; i < ladder.KillsToTop; i++) if (ladder.AddKill()) climbs++;
            Assert.AreEqual(4, climbs);
            Assert.IsTrue(ladder.AtTop);
            Assert.AreEqual("Rifle", ladder.WeaponId);
            Assert.IsNull(ladder.NextWeaponId);
            Assert.AreEqual(0, ladder.KillsLeft);

            for (int i = 0; i < 50; i++) Assert.IsFalse(ladder.AddKill(), "nothing past the last gun");
            Assert.AreEqual("Rifle", ladder.WeaponId);
            Assert.AreEqual(ladder.KillsToTop + 50, ladder.TotalKills, "kills on the last gun still count");
        }

        [Test]
        public void ANewRun_StartsAtTheBottomAgain()
        {
            var ladder = Default();
            for (int i = 0; i < 10; i++) ladder.AddKill();
            ladder.Reset();
            Assert.AreEqual(0, ladder.Rung);
            Assert.AreEqual(0, ladder.KillsOnRung);
            Assert.AreEqual(0, ladder.TotalKills);
            Assert.AreEqual("Pistol", ladder.WeaponId);
        }

        [Test]
        public void AStepNamingAGunThatDoesNotExist_IsLeftOut_AndSaidSo()
        {
            var rules = new GunLadderRules
            {
                rungs = new[] { new GunLadderRung("Pistol", 2), new GunLadderRung("Rayy", 3), new GunLadderRung("Rifle", 0) },
            };
            var ladder = new GunLadder(rules, id => RealGuns.Contains(id));
            Assert.AreEqual(2, ladder.RungCount);
            Assert.AreEqual("Rifle", ladder.WeaponAt(1));
            List<string> problems = GunLadder.Problems(rules, id => RealGuns.Contains(id));
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Rayy", problems[0]);
        }

        [Test]
        public void AStepAskingForNoKills_StillTakesOne()
        {
            var rules = new GunLadderRules { rungs = new[] { new GunLadderRung("Pistol", 0), new GunLadderRung("Rifle", 0) } };
            var ladder = new GunLadder(rules);
            Assert.AreEqual(1, ladder.KillsLeft);
            Assert.IsTrue(ladder.AddKill());
            Assert.AreEqual("Rifle", ladder.WeaponId);
            Assert.AreEqual(1, GunLadder.Problems(rules, null).Count);
        }

        [Test]
        public void AnEmptyOrAllBrokenLadder_FallsBackToTheDefaultOne()
        {
            Assert.AreEqual(5, new GunLadder(new GunLadderRules { rungs = new GunLadderRung[0] }).RungCount);
            var broken = new GunLadderRules { rungs = new[] { new GunLadderRung("Nope", 2) } };
            Assert.AreEqual("Pistol", new GunLadder(broken, id => RealGuns.Contains(id)).WeaponId);
        }

        [Test]
        public void ALadderOfOneGun_IsAlreadyAtTheTop()
        {
            var ladder = new GunLadder(new GunLadderRules { rungs = new[] { new GunLadderRung("Rifle", 5) } });
            Assert.IsTrue(ladder.AtTop);
            Assert.AreEqual(0, ladder.KillsToTop);
            Assert.IsFalse(ladder.AddKill());
        }

        [Test]
        public void ByDefault_TheGunsYouClimbPast_AreKept()
        {
            Assert.IsTrue(new GunLadderRules().keepOldGuns, "Matthew, 16 Nov 2026: the old gun stays in your inventory");
        }

        [Test]
        public void ShortName_DropsThePlaceholderTag()
        {
            Assert.AreEqual("AK-ish", GunLadder.ShortName("AK-ish (name TBD)"));
            Assert.AreEqual("Test Pistol", GunLadder.ShortName("Test Pistol"));
            Assert.AreEqual(string.Empty, GunLadder.ShortName(null));
        }
    }
}
