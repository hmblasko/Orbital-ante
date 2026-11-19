using System.Collections.Generic;
using NUnit.Framework;

namespace OrbitalAnte.Verification.Tests
{
    /// <summary>
    /// The gate's rules, with no Unity objects: the weighted score, PASS / FAIL / BLOCKED, and the ways a
    /// review file can try to say more than its evidence supports.
    /// </summary>
    public class QualityGateTests
    {
        static Dictionary<string, object> Category(string id, double score) => new Dictionary<string, object>
        {
            ["id"] = id, ["score"] = score, ["applicable"] = true, ["justification"] = "seen in shots", ["evidence"] = new List<object> { "shots/a.jpg" },
        };

        static Dictionary<string, object> Issue(string id, string severity, string status = "open") => new Dictionary<string, object>
        {
            ["id"] = id, ["target"] = "mannequin", ["category"] = "modeling", ["severity"] = severity, ["title"] = "t",
            ["description"] = "d", ["expected"] = "e", ["expectedBenefit"] = "high", ["effort"] = "small", ["regressionRisk"] = "low",
            ["kind"] = "aesthetic_judgment", ["status"] = status, ["confidence"] = 0.7,
            ["evidence"] = new List<object> { "shots/a.jpg" }, ["recommendedFixes"] = new List<object> { "fix" }, ["acceptanceCriteria"] = new List<object> { "looks right" },
        };

        /// <summary>A complete review in which every category scores the same.</summary>
        static Dictionary<string, object> Review(double everyScore, string verdict)
        {
            var categories = new List<object>();
            foreach (Rubric.Category c in Rubric.Categories) categories.Add(Category(c.Id, everyScore));
            return new Dictionary<string, object>
            {
                ["schemaVersion"] = 1.0, ["rubricVersion"] = (double)Rubric.Version,
                ["reviewId"] = "R1", ["buildId"] = "B1", ["critic"] = "orbital-critic", ["builder"] = "orbital-builder", ["milestone"] = "M7",
                ["reviewLevel"] = 2.0,
                ["target"] = new Dictionary<string, object> { ["id"] = "mannequin" },
                ["inspections"] = new List<object> { new Dictionary<string, object> { ["id"] = "views.standard", ["required"] = true, ["status"] = "done" } },
                ["technical"] = new Dictionary<string, object>
                {
                    ["testsRun"] = true, ["testsPassed"] = true, ["testResults"] = "test-results.json",
                    ["verifiedErrors"] = new List<object>(), ["regressions"] = new List<object>(), ["unresolvedErrorCount"] = 0.0,
                },
                ["evidence"] = new Dictionary<string, object>
                {
                    ["screenshotCount"] = 12.0, ["captureManifests"] = new List<object> { "capture-manifest.json" }, ["animationEvidence"] = new List<object>(),
                },
                ["categories"] = categories,
                ["overallScore"] = everyScore,
                ["strengths"] = new List<object> { "a", "b", "c" },
                ["issues"] = new List<object>(), ["rankedIssueIds"] = new List<object>(),
                ["acceptanceCriteria"] = new List<object> { "next review checks x" },
                ["untested"] = new List<object> { "Steam Deck hardware" },
                ["verdict"] = verdict,
            };
        }

        static Dictionary<string, object> FailingReview()
        {
            Dictionary<string, object> review = Review(7.0, "FAIL");
            ((List<object>)review["issues"]).Add(Issue("I-1", "P1"));
            ((List<object>)review["rankedIssueIds"]).Add("I-1");
            return review;
        }

        static Dictionary<string, object> Tech(Dictionary<string, object> review) => (Dictionary<string, object>)review["technical"];

        [Test]
        public void TheRubricWeightsAddUpToOneHundred()
        {
            double sum = 0;
            foreach (Rubric.Category c in Rubric.Categories) sum += c.Weight;
            Assert.AreEqual(100, sum, 1e-9);
        }

        [Test]
        public void TheOverallScoreIsTheWeightedSum()
        {
            var scores = new Dictionary<string, double>
            {
                ["cohesion"] = 8, ["modeling"] = 6, ["environment"] = 7, ["materials"] = 9, ["animation"] = 5, ["readability"] = 8, ["polish"] = 6,
            };
            // 8*20 + 6*20 + 7*15 + 9*15 + 5*15 + 8*10 + 6*5 = 705 -> 7.05 -> 7.1
            Assert.AreEqual(7.1, Rubric.Overall(scores).Value, 1e-9);
        }

        [Test]
        public void ACategoryThatDoesNotApplyIsLeftOutNotGivenATen()
        {
            var scores = new Dictionary<string, double> { ["cohesion"] = 6, ["modeling"] = 6 };
            Assert.AreEqual(6.0, Rubric.Overall(scores).Value, 1e-9);
            var uneven = new Dictionary<string, double> { ["cohesion"] = 9, ["polish"] = 4 };
            // (9*20 + 4*5) / 25 = 8.0
            Assert.AreEqual(8.0, Rubric.Overall(uneven).Value, 1e-9);
            Assert.IsNull(Rubric.Overall(new Dictionary<string, double>()));
        }

        [Test]
        public void RoundingHappensOnlyAfterTheWeightedSum()
        {
            // 8.44 everywhere stays 8.4 (a fail); it must not be rounded up per category first.
            var scores = new Dictionary<string, double>();
            foreach (Rubric.Category c in Rubric.Categories) scores[c.Id] = 8.44;
            Assert.AreEqual(8.4, Rubric.Overall(scores).Value, 1e-9);
        }

        [Test]
        public void AStrongCleanReviewPasses()
        {
            GateResult g = QualityGate.Check(Review(8.5, "PASS"));
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
            Assert.AreEqual("PASS", g.ComputedVerdict);
        }

        [Test]
        public void JustUnderThePassMarkFails()
        {
            Dictionary<string, object> review = Review(8.4, "FAIL");
            ((List<object>)review["issues"]).Add(Issue("I-1", "P2"));
            ((List<object>)review["rankedIssueIds"]).Add("I-1");
            GateResult g = QualityGate.Check(review);
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
            Assert.AreEqual("FAIL", g.ComputedVerdict);
        }

        [Test]
        public void AHighScoreWithAVerifiedErrorFails()
        {
            Dictionary<string, object> review = Review(9.3, "PASS");
            ((List<object>)Tech(review)["verifiedErrors"]).Add(new Dictionary<string, object>
            {
                ["id"] = "E-1", ["description"] = "NullReferenceException in Hud.Update", ["evidence"] = new List<object> { "test-results.json" }, ["resolved"] = false,
            });
            Tech(review)["unresolvedErrorCount"] = 1.0;
            GateResult g = QualityGate.Check(review);
            Assert.AreEqual("FAIL", g.ComputedVerdict);
            Assert.IsFalse(g.Valid, "Saying PASS with an unresolved error must be rejected.");
        }

        [Test]
        public void HidingAnErrorByMiscountingIsRejected()
        {
            Dictionary<string, object> review = Review(9.0, "PASS");
            ((List<object>)Tech(review)["verifiedErrors"]).Add(new Dictionary<string, object>
            {
                ["id"] = "E-1", ["description"] = "x", ["evidence"] = new List<object> { "log" }, ["resolved"] = false,
            });
            Assert.IsFalse(QualityGate.Check(review).Valid);
        }

        [Test]
        public void NoErrorsButALowScoreFails()
        {
            GateResult g = QualityGate.Check(FailingReview());
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
            Assert.AreEqual("FAIL", g.ComputedVerdict);
            Assert.AreEqual(1, g.OpenP0P1);
        }

        [Test]
        public void TestsThatDidNotRunBlockTheReview()
        {
            Dictionary<string, object> review = Review(9.0, "PASS");
            Tech(review)["testsRun"] = false;
            Tech(review)["testsPassed"] = false;
            GateResult g = QualityGate.Check(review);
            Assert.AreEqual("BLOCKED", g.ComputedVerdict);
            Assert.IsFalse(g.Valid);
        }

        [Test]
        public void ABlockedRequiredInspectionBlocksTheReview()
        {
            Dictionary<string, object> review = Review(9.0, "BLOCKED");
            ((List<object>)review["inspections"]).Add(new Dictionary<string, object>
            {
                ["id"] = "animation.playback", ["required"] = true, ["status"] = "blocked", ["note"] = "Unity could not be launched",
            });
            GateResult g = QualityGate.Check(review);
            Assert.AreEqual("BLOCKED", g.ComputedVerdict);
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
        }

        [Test]
        public void NoScreenshotsMeansBlockedNeverPass()
        {
            Dictionary<string, object> review = Review(9.0, "PASS");
            ((Dictionary<string, object>)review["evidence"])["screenshotCount"] = 0.0;
            GateResult g = QualityGate.Check(review);
            Assert.AreEqual("BLOCKED", g.ComputedVerdict);
            Assert.IsFalse(g.Valid);
        }

        [Test]
        public void AnOverallScoreThatDoesNotMatchItsCategoriesIsRejected()
        {
            Dictionary<string, object> review = FailingReview();
            review["overallScore"] = 8.6;
            review["verdict"] = "PASS";
            GateResult g = QualityGate.Check(review);
            Assert.IsFalse(g.Valid);
            Assert.AreEqual(7.0, g.ComputedScore.Value, 1e-9);
            Assert.AreEqual("FAIL", g.ComputedVerdict);
        }

        [Test]
        public void AnOpenRegressionFailsEvenAStrongReview()
        {
            Dictionary<string, object> review = Review(9.0, "FAIL");
            ((List<object>)review["issues"]).Add(Issue("I-1", "P2", "regressed"));
            ((List<object>)review["rankedIssueIds"]).Add("I-1");
            GateResult g = QualityGate.Check(review);
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
            Assert.AreEqual("FAIL", g.ComputedVerdict);
        }

        [Test]
        public void ABuilderMayNotBeItsOwnCritic()
        {
            Dictionary<string, object> review = Review(9.0, "PASS");
            review["critic"] = "orbital-builder";
            Assert.IsFalse(QualityGate.Check(review).Valid);
        }

        [Test]
        public void AFailNeedsSomethingTheBuilderCanActOn()
        {
            Assert.IsFalse(QualityGate.Check(Review(7.0, "FAIL")).Valid);
        }

        [Test]
        public void AnIssueWithoutEvidenceOrAcceptanceCriteriaIsRejected()
        {
            Dictionary<string, object> review = FailingReview();
            var issue = (Dictionary<string, object>)((List<object>)review["issues"])[0];
            issue["evidence"] = new List<object>();
            issue["acceptanceCriteria"] = new List<object>();
            GateResult g = QualityGate.Check(review);
            Assert.AreEqual(2, g.Problems.Count, string.Join(" | ", g.Problems));
        }

        [Test]
        public void MissingEvidenceFilesAreReported()
        {
            GateResult g = QualityGate.Check(Review(8.5, "PASS"), path => path != "capture-manifest.json");
            Assert.IsFalse(g.Valid);
            StringAssert.Contains("capture-manifest.json", string.Join(" | ", g.Problems));
        }

        [Test]
        public void RankedIssuesMustPutSeverityFirst()
        {
            Dictionary<string, object> review = FailingReview();
            ((List<object>)review["issues"]).Add(Issue("I-0", "P0"));
            ((List<object>)review["rankedIssueIds"]).Add("I-0"); // P1 then P0: wrong order
            Assert.IsFalse(QualityGate.Check(review).Valid);
            review["rankedIssueIds"] = new List<object> { "I-0", "I-1" };
            Assert.IsTrue(QualityGate.Check(review).Valid);
        }

        [Test]
        public void AScoreMayNotRiseBetweenReviewsWithoutAReason()
        {
            Dictionary<string, object> before = FailingReview();
            Dictionary<string, object> after = Review(8.6, "PASS");
            after["comparison"] = new Dictionary<string, object> { ["resolved"] = new List<object>() };
            Assert.IsFalse(QualityGate.Check(after, null, before).Valid);
            ((Dictionary<string, object>)after["comparison"])["resolved"] = new List<object> { "I-1" };
            Assert.IsTrue(QualityGate.Check(after, null, before).Valid);
        }

        [Test]
        public void TheRubricVersionCannotBeChangedToMakeABuildPass()
        {
            Dictionary<string, object> review = Review(8.5, "PASS");
            review["rubricVersion"] = 2.0;
            Assert.IsFalse(QualityGate.Check(review).Valid);
        }

        [Test]
        public void OneWeakTargetFailsTheMilestoneHoweverGoodTheOthersAre()
        {
            Assert.AreEqual("FAIL", QualityGate.Rollup(new[] { "PASS", "PASS", "FAIL" }));
            Assert.AreEqual("BLOCKED", QualityGate.Rollup(new[] { "PASS", "BLOCKED" }));
            Assert.AreEqual("PASS", QualityGate.Rollup(new[] { "PASS", "PASS" }));
            Assert.AreEqual("BLOCKED", QualityGate.Rollup(new string[0]));
        }

        [Test]
        public void IssuesRankBySeverityThenBenefitConfidenceEffortAndRisk()
        {
            Dictionary<string, object> a = Issue("minor", "P3");
            Dictionary<string, object> b = Issue("major-low-benefit", "P1"); b["expectedBenefit"] = "low";
            Dictionary<string, object> c = Issue("major-high-benefit", "P1");
            Dictionary<string, object> d = Issue("major-high-benefit-surer", "P1"); d["confidence"] = 0.9;
            Dictionary<string, object> e = Issue("critical", "P0"); e["effort"] = "large";
            List<Dictionary<string, object>> ranked = IssueRanking.Rank(new[] { a, b, c, d, e });
            var ids = ranked.ConvertAll(x => (string)x["id"]);
            CollectionAssert.AreEqual(new[] { "critical", "major-high-benefit-surer", "major-high-benefit", "major-low-benefit", "minor" }, ids);
        }

        [Test]
        public void ThreeFailedCyclesInARowCallForAnAssessment()
        {
            var history = new List<object>();
            for (int i = 1; i <= 3; i++)
            {
                Dictionary<string, object> review = FailingReview();
                review["reviewId"] = "R" + i;
                Assert.IsTrue(ReviewHistory.Append(history, ReviewHistory.Entry(review, QualityGate.Check(review), "2026-11-12")));
                Assert.AreEqual(i == 3, ReviewHistory.AssessmentDue(history, "mannequin", "M7"));
            }
            Assert.IsFalse(ReviewHistory.Append(history, MiniJson.Obj(history[0])), "The same review is never recorded twice.");
            Dictionary<string, object> pass = Review(8.7, "PASS");
            pass["reviewId"] = "R4";
            ReviewHistory.Append(history, ReviewHistory.Entry(pass, QualityGate.Check(pass), "2026-11-13"));
            Assert.AreEqual(0, ReviewHistory.FailedCyclesInARow(history, "mannequin", "M7"));
            Assert.AreEqual("R4", (string)ReviewHistory.Latest(history, "mannequin")["reviewId"]);
        }

        [Test]
        public void JsonSurvivesAWriteAndARead()
        {
            Dictionary<string, object> review = FailingReview();
            review["note"] = "quotes \" and\nnew lines \\ and tabs\t";
            var back = MiniJson.Obj(MiniJson.Parse(MiniJson.Write(review)));
            Assert.AreEqual(review["note"], back["note"]);
            GateResult g = QualityGate.Check(back);
            Assert.IsTrue(g.Valid, string.Join(" | ", g.Problems));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("{\"a\": 1,}"));
        }

        [Test]
        public void TestResultsAreCountedFromTheRunnersFile()
        {
            const string xml = "<test-run duration=\"2.5\"><test-suite><test-case fullname=\"A.One\" result=\"Passed\"/>" +
                               "<test-case fullname=\"A.Two\" result=\"Failed\"><failure><message>boom</message></failure></test-case>" +
                               "<test-case fullname=\"A.Three\" result=\"Skipped\"/></test-suite></test-run>";
            Dictionary<string, object> s = TestResultsSummary.FromNUnitXml(xml, "PlayMode");
            Assert.AreEqual(3, s["total"]);
            Assert.AreEqual(1, s["passed"]);
            Assert.AreEqual(1, s["failed"]);
            Assert.AreEqual(false, s["allPassed"]);
            Assert.AreEqual("boom", ((Dictionary<string, object>)((List<object>)s["failures"])[0])["message"]);
            Assert.AreEqual(false, TestResultsSummary.FromNUnitXml("<test-run/>", "x")["allPassed"], "No tests is not a pass.");
        }
    }
}
