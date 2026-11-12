using System;
using System.Collections.Generic;

namespace OrbitalAnte.Verification
{
    /// <summary>What the gate makes of one review file.</summary>
    public sealed class GateResult
    {
        /// <summary>Things that make the review file invalid. A review with any of these is not accepted.</summary>
        public readonly List<string> Problems = new List<string>();
        /// <summary>Things the Orchestrator should look at but that do not invalidate the review.</summary>
        public readonly List<string> Warnings = new List<string>();
        /// <summary>The score worked out again from the category scores. Null when nothing was scored.</summary>
        public double? ComputedScore;
        /// <summary>The verdict the rules give for the facts in the file: PASS, FAIL or BLOCKED.</summary>
        public string ComputedVerdict = QualityGate.Blocked;
        public int UnresolvedErrors;
        public int OpenP0P1;
        public bool Valid => Problems.Count == 0;
    }

    /// <summary>
    /// The quality gate. It never scores anything itself: it checks that a Critic's review file (issues.json) is
    /// complete, that the overall score is the weighted sum of its category scores, and that the stated verdict
    /// is the one the rules give. PASS = score at least 8.5 AND no unresolved verified errors AND every required
    /// inspection done AND tests run and passed AND no open regression. Anything that could not really be
    /// verified is BLOCKED, never PASS.
    /// </summary>
    public static class QualityGate
    {
        public const string Pass = "PASS";
        public const string Fail = "FAIL";
        public const string Blocked = "BLOCKED";

        static readonly string[] Severities = { "P0", "P1", "P2", "P3" };
        static readonly string[] IssueText = { "id", "target", "category", "severity", "title", "description", "expected", "expectedBenefit", "effort", "regressionRisk", "kind", "status" };

        /// <param name="review">The parsed issues.json.</param>
        /// <param name="fileExists">Says whether an evidence path (relative to the repository root) exists. Null skips that check.</param>
        /// <param name="previous">The previous review of the same target, if any, for the score-inflation check.</param>
        public static GateResult Check(Dictionary<string, object> review, Func<string, bool> fileExists = null, Dictionary<string, object> previous = null)
        {
            var r = new GateResult();
            if (review == null) { r.Problems.Add("The review file is not a JSON object."); return r; }

            foreach (string key in new[] { "reviewId", "buildId", "critic", "verdict" })
                if (string.IsNullOrWhiteSpace(MiniJson.Str(review, key))) r.Problems.Add("Missing '" + key + "'.");
            if (MiniJson.Num(review, "rubricVersion") != Rubric.Version)
                r.Problems.Add("'rubricVersion' must be " + Rubric.Version + ". The rubric is fixed between comparable reviews.");
            Dictionary<string, object> target = MiniJson.Obj(MiniJson.Get(review, "target"));
            if (target == null || string.IsNullOrWhiteSpace(MiniJson.Str(target, "id"))) r.Problems.Add("Missing 'target.id'.");

            string verdict = MiniJson.Str(review, "verdict") ?? string.Empty;
            if (verdict != Pass && verdict != Fail && verdict != Blocked) r.Problems.Add("'verdict' must be PASS, FAIL or BLOCKED.");
            string critic = MiniJson.Str(review, "critic") ?? string.Empty;
            string builder = MiniJson.Str(review, "builder") ?? string.Empty;
            if (critic.Length > 0 && critic == builder) r.Problems.Add("The Critic and the Builder are the same agent. A Builder may not score its own work.");

            // ---- required inspections
            bool inspectionBlocked = false;
            List<object> inspections = MiniJson.Items(review, "inspections");
            if (inspections.Count == 0) r.Problems.Add("'inspections' is empty: list every required inspection and whether it was done.");
            foreach (object o in inspections)
            {
                Dictionary<string, object> ins = MiniJson.Obj(o);
                string id = MiniJson.Str(ins, "id") ?? "?";
                string status = MiniJson.Str(ins, "status");
                bool required = MiniJson.Flag(ins, "required") ?? true;
                if (status != "done" && status != "blocked" && status != "not_applicable")
                    r.Problems.Add("Inspection '" + id + "' needs a status of done, blocked or not_applicable.");
                if (status != "done" && string.IsNullOrWhiteSpace(MiniJson.Str(ins, "note")))
                    r.Problems.Add("Inspection '" + id + "' is " + status + " and must say why in 'note'.");
                if (required && status == "blocked") inspectionBlocked = true;
            }

            // ---- technical half
            Dictionary<string, object> tech = MiniJson.Obj(MiniJson.Get(review, "technical"));
            bool testsRun = false, testsPassed = false;
            int openRegressions = 0;
            if (tech == null) r.Problems.Add("Missing 'technical'.");
            else
            {
                testsRun = MiniJson.Flag(tech, "testsRun") ?? false;
                testsPassed = MiniJson.Flag(tech, "testsPassed") ?? false;
                if (testsPassed && !testsRun) r.Problems.Add("'technical.testsPassed' is true but 'testsRun' is false. Tests that did not run did not pass.");
                foreach (object o in MiniJson.Items(tech, "verifiedErrors"))
                {
                    Dictionary<string, object> e = MiniJson.Obj(o);
                    if (string.IsNullOrWhiteSpace(MiniJson.Str(e, "id")) || string.IsNullOrWhiteSpace(MiniJson.Str(e, "description")))
                        r.Problems.Add("Every entry in 'technical.verifiedErrors' needs an id and a description.");
                    if (MiniJson.Items(e, "evidence").Count == 0) r.Problems.Add("Verified error '" + MiniJson.Str(e, "id") + "' has no evidence.");
                    if (!(MiniJson.Flag(e, "resolved") ?? false)) r.UnresolvedErrors++;
                }
                foreach (object o in MiniJson.Items(tech, "regressions"))
                    if (!(MiniJson.Flag(MiniJson.Obj(o), "resolved") ?? false)) openRegressions++;
                double? stated = MiniJson.Num(tech, "unresolvedErrorCount");
                if (stated == null || (int)stated.Value != r.UnresolvedErrors)
                    r.Problems.Add("'technical.unresolvedErrorCount' is " + (stated?.ToString() ?? "missing") + " but the list holds " + r.UnresolvedErrors + " unresolved verified errors.");
                if (testsRun) CheckPath(r, fileExists, MiniJson.Str(tech, "testResults"), "technical.testResults");
            }

            // ---- aesthetic half
            var scores = new List<KeyValuePair<string, double>>();
            var seen = new HashSet<string>();
            foreach (object o in MiniJson.Items(review, "categories"))
            {
                Dictionary<string, object> c = MiniJson.Obj(o);
                string id = MiniJson.Str(c, "id") ?? string.Empty;
                if (Rubric.Find(id) == null) { r.Problems.Add("Unknown category '" + id + "'."); continue; }
                if (!seen.Add(id)) { r.Problems.Add("Category '" + id + "' appears twice."); continue; }
                bool applicable = MiniJson.Flag(c, "applicable") ?? true;
                if (string.IsNullOrWhiteSpace(MiniJson.Str(c, "justification")))
                    r.Problems.Add("Category '" + id + "' has no justification" + (applicable ? "." : " for why it does not apply."));
                if (!applicable) continue;
                double? score = MiniJson.Num(c, "score");
                if (score == null || score < 0 || score > 10) { r.Problems.Add("Category '" + id + "' needs a score from 0 to 10."); continue; }
                if (verdict != Blocked && MiniJson.Items(c, "evidence").Count == 0)
                    r.Problems.Add("Category '" + id + "' is scored without evidence. Scores must rest on inspectable evidence.");
                scores.Add(new KeyValuePair<string, double>(id, score.Value));
            }
            foreach (Rubric.Category c in Rubric.Categories)
                if (!seen.Contains(c.Id) && verdict != Blocked) r.Problems.Add("Category '" + c.Id + "' is missing. Score it, or mark it applicable: false with a reason.");

            r.ComputedScore = Rubric.Overall(scores);
            double? statedScore = MiniJson.Num(review, "overallScore");
            if (verdict != Blocked)
            {
                if (r.ComputedScore == null) r.Problems.Add("No category was scored.");
                else if (statedScore == null || Math.Abs(statedScore.Value - r.ComputedScore.Value) > 0.0001)
                    r.Problems.Add("'overallScore' is " + (statedScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing") +
                                   " but the weighted category scores give " + r.ComputedScore.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
            else if (statedScore != null && r.ComputedScore != null && Math.Abs(statedScore.Value - r.ComputedScore.Value) > 0.0001)
                r.Problems.Add("'overallScore' does not match the category scores.");

            // ---- evidence
            Dictionary<string, object> evidence = MiniJson.Obj(MiniJson.Get(review, "evidence"));
            int shots = (int)(MiniJson.Num(evidence, "screenshotCount") ?? 0);
            if (verdict != Blocked)
            {
                if (evidence == null) r.Problems.Add("Missing 'evidence'.");
                if (shots <= 0) r.Problems.Add("A PASS or FAIL needs screenshots captured from the Unity project ('evidence.screenshotCount' is 0).");
                if (MiniJson.Items(evidence, "captureManifests").Count == 0) r.Problems.Add("'evidence.captureManifests' is empty: screenshots must be traceable to a capture run.");
                List<object> strengths = MiniJson.Items(review, "strengths");
                if (strengths.Count != 3) r.Problems.Add("'strengths' must name the three strongest visual features (found " + strengths.Count + ").");
            }
            if (evidence != null)
            {
                foreach (object p in MiniJson.Items(evidence, "captureManifests")) CheckPath(r, fileExists, p as string, "evidence.captureManifests");
                foreach (object p in MiniJson.Items(evidence, "animationEvidence")) CheckPath(r, fileExists, p as string, "evidence.animationEvidence");
            }

            // ---- issues
            var ids = new HashSet<string>();
            var open = new List<Dictionary<string, object>>();
            foreach (object o in MiniJson.Items(review, "issues"))
            {
                Dictionary<string, object> issue = MiniJson.Obj(o);
                string id = MiniJson.Str(issue, "id") ?? "?";
                if (!ids.Add(id)) r.Problems.Add("Issue id '" + id + "' is used twice.");
                foreach (string key in IssueText)
                    if (string.IsNullOrWhiteSpace(MiniJson.Str(issue, key))) r.Problems.Add("Issue '" + id + "' is missing '" + key + "'.");
                string severity = MiniJson.Str(issue, "severity");
                if (Array.IndexOf(Severities, severity) < 0) r.Problems.Add("Issue '" + id + "' severity must be P0, P1, P2 or P3.");
                string kind = MiniJson.Str(issue, "kind");
                if (kind != "verified_defect" && kind != "suspected_defect" && kind != "aesthetic_judgment")
                    r.Problems.Add("Issue '" + id + "' kind must be verified_defect, suspected_defect or aesthetic_judgment.");
                double? confidence = MiniJson.Num(issue, "confidence");
                if (confidence == null || confidence < 0 || confidence > 1) r.Problems.Add("Issue '" + id + "' needs a confidence from 0 to 1.");
                List<object> ev = MiniJson.Items(issue, "evidence");
                if (ev.Count == 0) r.Problems.Add("Issue '" + id + "' has no evidence.");
                foreach (object p in ev) CheckPath(r, fileExists, p as string, "issue " + id);
                if (MiniJson.Items(issue, "recommendedFixes").Count == 0) r.Problems.Add("Issue '" + id + "' has no recommended fix.");
                if (MiniJson.Items(issue, "acceptanceCriteria").Count == 0) r.Problems.Add("Issue '" + id + "' has no acceptance criteria.");
                string status = MiniJson.Str(issue, "status");
                if (status != "open" && status != "resolved" && status != "regressed" && status != "wont_fix")
                    r.Problems.Add("Issue '" + id + "' status must be open, resolved, regressed or wont_fix.");
                if (status == "wont_fix" && string.IsNullOrWhiteSpace(MiniJson.Str(issue, "statusNote")))
                    r.Problems.Add("Issue '" + id + "' is wont_fix and must say in 'statusNote' who decided that and why.");
                if (status == "regressed") openRegressions++;
                if (status == "open" || status == "regressed")
                {
                    open.Add(issue);
                    if (severity == "P0" || severity == "P1") r.OpenP0P1++;
                }
            }

            // Ranked ids: every open issue, most severe first.
            List<object> ranked = MiniJson.Items(review, "rankedIssueIds");
            var rankedSet = new HashSet<string>();
            int lastSeverity = 0;
            foreach (object o in ranked)
            {
                string id = o as string ?? string.Empty;
                rankedSet.Add(id);
                Dictionary<string, object> issue = open.Find(x => MiniJson.Str(x, "id") == id);
                if (issue == null) { r.Problems.Add("'rankedIssueIds' lists '" + id + "', which is not an open issue."); continue; }
                int sev = Array.IndexOf(Severities, MiniJson.Str(issue, "severity"));
                if (sev < lastSeverity) r.Problems.Add("'rankedIssueIds' is not ordered by severity at '" + id + "'.");
                lastSeverity = Math.Max(lastSeverity, sev);
            }
            foreach (Dictionary<string, object> issue in open)
                if (!rankedSet.Contains(MiniJson.Str(issue, "id") ?? "?")) r.Problems.Add("Open issue '" + MiniJson.Str(issue, "id") + "' is missing from 'rankedIssueIds'.");

            // ---- the verdict the rules give
            if (inspectionBlocked || !testsRun || r.ComputedScore == null || shots <= 0) r.ComputedVerdict = Blocked;
            else if (r.ComputedScore.Value >= Rubric.PassMark && r.UnresolvedErrors == 0 && testsPassed && openRegressions == 0) r.ComputedVerdict = Pass;
            else r.ComputedVerdict = Fail;

            if (verdict.Length > 0 && verdict != r.ComputedVerdict)
                r.Problems.Add("The stated verdict is " + verdict + " but the rules give " + r.ComputedVerdict + " (score " +
                               (r.ComputedScore?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "none") + ", unresolved errors " + r.UnresolvedErrors +
                               ", tests run " + testsRun + ", tests passed " + testsPassed + ", open regressions " + openRegressions +
                               ", required inspection blocked " + inspectionBlocked + ").");
            if (r.ComputedVerdict == Fail && open.Count == 0)
                r.Problems.Add("A FAIL must come with at least one open issue the Builder can act on.");
            if (r.ComputedVerdict == Pass && r.OpenP0P1 > 0)
                r.Warnings.Add(r.OpenP0P1 + " P0/P1 issue(s) are still open on a PASS. Either they are not really P0/P1 or the scores are too generous.");
            if (r.ComputedVerdict != Pass && MiniJson.Items(review, "acceptanceCriteria").Count == 0)
                r.Problems.Add("'acceptanceCriteria' is empty: say exactly what the next review must verify.");
            if (MiniJson.Items(review, "untested").Count == 0)
                r.Warnings.Add("'untested' is empty. List what was outside this review so nobody reads the verdict as 'the whole game is fine'.");

            // ---- against the previous review of the same target
            if (previous != null)
            {
                double? before = MiniJson.Num(previous, "overallScore");
                Dictionary<string, object> comparison = MiniJson.Obj(MiniJson.Get(review, "comparison"));
                if (comparison == null) r.Problems.Add("A previous review exists, so 'comparison' is required.");
                else if (before != null && r.ComputedScore != null && r.ComputedScore > before
                         && MiniJson.Items(comparison, "resolved").Count == 0 && string.IsNullOrWhiteSpace(MiniJson.Str(comparison, "scoreChangeJustification")))
                    r.Problems.Add("The score rose from " + before + " to " + r.ComputedScore + " with no resolved issues and no 'comparison.scoreChangeJustification'. Scores do not rise by themselves.");
            }
            return r;
        }

        static void CheckPath(GateResult r, Func<string, bool> fileExists, string path, string where)
        {
            if (string.IsNullOrWhiteSpace(path)) { r.Problems.Add("An evidence path in " + where + " is empty."); return; }
            if (fileExists != null && !fileExists(path)) r.Problems.Add("Evidence file not found (" + where + "): " + path);
        }

        /// <summary>
        /// A milestone's verdict from its targets' verdicts. Every target must pass by itself: scores are never
        /// averaged across targets, so one weak asset cannot hide behind strong ones.
        /// </summary>
        public static string Rollup(IEnumerable<string> targetVerdicts)
        {
            bool any = false, blocked = false;
            foreach (string v in targetVerdicts)
            {
                any = true;
                if (v == Fail) return Fail;
                if (v != Pass) blocked = true;
            }
            return !any || blocked ? Blocked : Pass;
        }
    }
}
