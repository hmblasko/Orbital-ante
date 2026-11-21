using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    public class RunRecapTests
    {
        static ScoreResult Paid(int points, ScoreCategory c) => new ScoreResult { outcome = ScoreOutcome.Awarded, points = points, category = c };

        [Test]
        public void SaysWherePointsCameFrom_TopThree_LargestFirst()
        {
            var r = new RunRecap();
            r.Add(new ScoreEvent(), Paid(600, ScoreCategory.Combat), 1, "kill");
            r.Add(new ScoreEvent(), Paid(250, ScoreCategory.Exploration), 1, "new world");
            r.Add(new ScoreEvent(), Paid(100, ScoreCategory.Objective), 2, "mast");
            r.Add(new ScoreEvent(), Paid(50, ScoreCategory.Landing), 2, "landing");
            Assert.AreEqual("Points from fighting 60% · exploring 25% · tasks 10%", r.SourcesLine());
        }

        [Test]
        public void TheBestMoment_IsTheBiggestSingleAward_WithItsFeats()
        {
            var r = new RunRecap();
            r.Add(new ScoreEvent { skillFeats = 2 }, Paid(340, ScoreCategory.Combat), 3, "LONG SHOT");
            r.Add(new ScoreEvent { verifiedSkill = true }, Paid(90, ScoreCategory.Combat), 3, "kill");
            r.Add(new ScoreEvent(), new ScoreResult { outcome = ScoreOutcome.AlreadyClaimed, points = 999 }, 3, "repeat");
            Assert.AreEqual("Best moment: +340 LONG SHOT (ante 3)   Style feats: 3", r.BestLine());
            Assert.AreEqual(2, r.Awards);
        }

        [Test]
        public void ANothingRun_SaysNothing()
        {
            Assert.AreEqual(string.Empty, new RunRecap().Lines());
        }
    }
}
