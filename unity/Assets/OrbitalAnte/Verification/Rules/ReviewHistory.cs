using System.Collections.Generic;

namespace OrbitalAnte.Verification
{
    /// <summary>
    /// The revision and quality history (Verification/History/history.json): one line per accepted review,
    /// added to and never rewritten. Also answers "how many failed cycles has this target had in this
    /// milestone", which is when the Orchestrator must stop and reassess instead of repeating similar fixes.
    /// </summary>
    public static class ReviewHistory
    {
        /// <summary>Failed substantial cycles after which a progress assessment is due.</summary>
        public const int CheckpointCycles = 3;

        public static Dictionary<string, object> Entry(Dictionary<string, object> review, GateResult gate, string date)
        {
            Dictionary<string, object> target = MiniJson.Obj(MiniJson.Get(review, "target"));
            return new Dictionary<string, object>
            {
                ["reviewId"] = MiniJson.Str(review, "reviewId"),
                ["date"] = date,
                ["buildId"] = MiniJson.Str(review, "buildId"),
                ["commit"] = MiniJson.Str(review, "commit") ?? string.Empty,
                ["milestone"] = MiniJson.Str(review, "milestone") ?? string.Empty,
                ["target"] = MiniJson.Str(target, "id"),
                ["level"] = MiniJson.Num(review, "reviewLevel") ?? 2,
                ["score"] = gate.ComputedScore,
                ["unresolvedErrors"] = gate.UnresolvedErrors,
                ["openP0P1"] = gate.OpenP0P1,
                ["verdict"] = gate.ComputedVerdict,
                ["previousReviewId"] = MiniJson.Str(review, "previousReviewId") ?? string.Empty,
            };
        }

        /// <summary>Adds an entry. Returns false (and changes nothing) if that review is already recorded.</summary>
        public static bool Append(List<object> history, Dictionary<string, object> entry)
        {
            string id = MiniJson.Str(entry, "reviewId");
            foreach (object o in history)
                if (MiniJson.Str(MiniJson.Obj(o), "reviewId") == id) return false;
            history.Add(entry);
            return true;
        }

        /// <summary>The newest recorded review of a target, or null.</summary>
        public static Dictionary<string, object> Latest(List<object> history, string target)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                Dictionary<string, object> e = MiniJson.Obj(history[i]);
                if (MiniJson.Str(e, "target") == target) return e;
            }
            return null;
        }

        /// <summary>How many full reviews of this target in this milestone have failed in a row, newest backwards.</summary>
        public static int FailedCyclesInARow(List<object> history, string target, string milestone)
        {
            int count = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                Dictionary<string, object> e = MiniJson.Obj(history[i]);
                if (MiniJson.Str(e, "target") != target || MiniJson.Str(e, "milestone") != milestone) continue;
                if ((MiniJson.Num(e, "level") ?? 2) < 2) continue;
                string verdict = MiniJson.Str(e, "verdict");
                if (verdict == QualityGate.Fail) count++;
                else if (verdict == QualityGate.Pass) break;
                // BLOCKED reviews are neither progress nor a failed attempt.
            }
            return count;
        }

        public static bool AssessmentDue(List<object> history, string target, string milestone)
        {
            int failed = FailedCyclesInARow(history, target, milestone);
            return failed > 0 && failed % CheckpointCycles == 0;
        }
    }
}
