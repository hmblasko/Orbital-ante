using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    public class PopFeedbackTests
    {
        [Test]
        public void TheLineGrowsWithWhatThePlayerDid_NeverWithChance()
        {
            Assert.AreEqual(PopTier.Plain, PopFeedback.TierFor(60, 0, 170));
            Assert.AreEqual(PopTier.Skilled, PopFeedback.TierFor(90, 1, 170));
            Assert.AreEqual(PopTier.Masterful, PopFeedback.TierFor(140, 2, 170));
            Assert.AreEqual(PopTier.Masterful, PopFeedback.TierFor(140, 5, 170));
            Assert.AreEqual(PopTier.Big, PopFeedback.TierFor(170, 0, 170));
            Assert.AreEqual(PopTier.Plain, PopFeedback.TierFor(0, 3, 170), "nothing paid, nothing shown");
        }

        [Test]
        public void Big_IsTheSameDeedAtEveryAnte()
        {
            var b = new AnteBalance();
            Assert.AreEqual(170, PopFeedback.BigAt(b, 1), "a fifth of ante 1's 850");
            for (int ante = 2; ante <= 9; ante++)
                Assert.AreEqual(170f * (float)ScoreMath.ValueScale(b, ante), PopFeedback.BigAt(b, ante), 1f, "ante " + ante);
        }

        [Test]
        public void ABigAwardWonWithStyle_KeepsTheStyleColour()
        {
            var r = new ScoreResult { outcome = ScoreOutcome.Awarded, points = 400 };
            Assert.IsTrue(PopFeedback.For(new ScoreEvent { skillFeats = 1 }, r, 170).styleColour);
            Assert.IsFalse(PopFeedback.For(new ScoreEvent(), r, 170).styleColour);
        }

        [Test]
        public void EachTierIsBiggerAndLouderThanTheOneBelow()
        {
            PopLook prev = PopFeedback.LookFor(PopTier.Plain);
            Assert.IsNull(prev.sound, "a plain award has only the coins, so the chimes keep their meaning");
            foreach (PopTier t in new[] { PopTier.Skilled, PopTier.Masterful, PopTier.Big })
            {
                PopLook look = PopFeedback.LookFor(t);
                Assert.Greater(look.fontSize, prev.fontSize, t.ToString());
                Assert.GreaterOrEqual(look.rackEndVolume, prev.rackEndVolume, t.ToString());
                Assert.IsNotNull(look.sound, t.ToString());
                prev = look;
            }
        }

        [Test]
        public void VerifiedSkill_CountsAsOneFeat()
        {
            var e = new ScoreEvent { verifiedSkill = true };
            var r = new ScoreResult { outcome = ScoreOutcome.Awarded, points = 50 };
            Assert.AreEqual(PopTier.Skilled, PopFeedback.For(e, r, 850).tier);
            r.outcome = ScoreOutcome.AlreadyClaimed;
            Assert.AreEqual(PopTier.Plain, PopFeedback.For(e, r, 850).tier);
        }
    }
}
