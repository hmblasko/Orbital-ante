using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OrbitalAnte.Verification.Cli
{
    /// <summary>
    /// Command-line front end for the quality gate rules. The same rules run inside Unity
    /// (Orbital Ante > Verification > Check Review File); this is for sessions with no editor.
    ///
    ///   validate &lt;issues.json&gt; [--previous &lt;issues.json&gt;]   check a review; exit 0 only if it is valid
    ///   record   &lt;issues.json&gt; [--previous &lt;issues.json&gt;]   validate, then add it to Verification/History/history.json
    ///   rank     &lt;issues.json&gt;                               print open issues in ranked order
    ///   status                                              latest verdict per target and the milestone roll-up
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0) { Console.Error.WriteLine("Usage: oa-verify validate|record|rank <issues.json> [--previous <issues.json>]  |  oa-verify status [milestone]"); return 2; }
            string root = FindRoot();
            try
            {
                switch (args[0])
                {
                    case "validate": return Validate(args, root, false);
                    case "record": return Validate(args, root, true);
                    case "rank": return Rank(args);
                    case "status": return Status(root, args.Length > 1 ? args[1] : null);
                    default: Console.Error.WriteLine("Unknown command '" + args[0] + "'."); return 2;
                }
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is IndexOutOfRangeException)
            {
                Console.Error.WriteLine("Could not do that: " + e.Message);
                return 2;
            }
        }

        static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Verification")) && Directory.Exists(Path.Combine(dir.FullName, "unity"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        static Dictionary<string, object> Load(string path) => MiniJson.Obj(MiniJson.Parse(File.ReadAllText(path)));

        static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static string HistoryPath(string root) => Path.Combine(root, "Verification", "History", "history.json");

        static List<object> LoadHistory(string root)
        {
            string path = HistoryPath(root);
            return File.Exists(path) ? MiniJson.List(MiniJson.Parse(File.ReadAllText(path))) ?? new List<object>() : new List<object>();
        }

        static int Validate(string[] args, string root, bool record)
        {
            string file = args[1];
            Dictionary<string, object> review = Load(file);
            string previousPath = Option(args, "--previous");
            Dictionary<string, object> previous = previousPath != null ? Load(previousPath) : null;
            string reviewDir = Path.GetDirectoryName(Path.GetFullPath(file));
            // Evidence paths may be written from the repository root or from the review's own folder.
            GateResult gate = QualityGate.Check(review, p => File.Exists(Path.Combine(root, p)) || File.Exists(Path.Combine(reviewDir, p)), previous);

            Console.WriteLine("Review   " + MiniJson.Str(review, "reviewId") + "  (" + MiniJson.Str(MiniJson.Obj(MiniJson.Get(review, "target")), "id") + ")");
            Console.WriteLine("Score    " + (gate.ComputedScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "none") +
                              (gate.ComputedScore != null ? "  " + Rubric.Classify(gate.ComputedScore.Value) : string.Empty));
            Console.WriteLine("Errors   " + gate.UnresolvedErrors + " unresolved verified, " + gate.OpenP0P1 + " open P0/P1 issues");
            Console.WriteLine("Verdict  " + gate.ComputedVerdict + " (by the rules)");
            foreach (string w in gate.Warnings) Console.WriteLine("WARNING  " + w);
            foreach (string p in gate.Problems) Console.WriteLine("INVALID  " + p);
            if (!gate.Valid)
            {
                Console.WriteLine("This review file is not accepted. Return it to the Critic; do not edit its scores.");
                return 1;
            }
            if (record)
            {
                List<object> history = LoadHistory(root);
                Dictionary<string, object> entry = ReviewHistory.Entry(review, gate, DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                if (!ReviewHistory.Append(history, entry)) { Console.WriteLine("Already in the history; nothing added."); return 0; }
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath(root)));
                File.WriteAllText(HistoryPath(root), MiniJson.Write(history));
                Console.WriteLine("Recorded in Verification/History/history.json.");
                string target = MiniJson.Str(entry, "target"), milestone = MiniJson.Str(entry, "milestone");
                if (ReviewHistory.AssessmentDue(history, target, milestone))
                    Console.WriteLine("CHECKPOINT  " + ReviewHistory.FailedCyclesInARow(history, target, milestone) +
                                      " failed cycles in a row for '" + target + "'. Stop and do a progress assessment before another refinement.");
            }
            return 0;
        }

        static int Rank(string[] args)
        {
            Dictionary<string, object> review = Load(args[1]);
            var open = new List<Dictionary<string, object>>();
            foreach (object o in MiniJson.Items(review, "issues"))
            {
                Dictionary<string, object> issue = MiniJson.Obj(o);
                string status = MiniJson.Str(issue, "status");
                if (status == "open" || status == "regressed") open.Add(issue);
            }
            foreach (Dictionary<string, object> issue in IssueRanking.Rank(open))
                Console.WriteLine(MiniJson.Str(issue, "severity") + "  " + MiniJson.Str(issue, "id") + "  " + MiniJson.Str(issue, "title"));
            return 0;
        }

        static int Status(string root, string milestone)
        {
            List<object> history = LoadHistory(root);
            var latest = new Dictionary<string, Dictionary<string, object>>();
            foreach (object o in history)
            {
                Dictionary<string, object> e = MiniJson.Obj(o);
                if (milestone != null && MiniJson.Str(e, "milestone") != milestone) continue;
                latest[MiniJson.Str(e, "target") ?? "?"] = e;
            }
            if (latest.Count == 0) { Console.WriteLine("No reviews recorded" + (milestone != null ? " for " + milestone : string.Empty) + ". Roll-up: BLOCKED (nothing verified)."); return 0; }
            var verdicts = new List<string>();
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in latest)
            {
                verdicts.Add(MiniJson.Str(pair.Value, "verdict"));
                Console.WriteLine(pair.Key.PadRight(24) + MiniJson.Str(pair.Value, "verdict").PadRight(9) + "score " + (MiniJson.Num(pair.Value, "score")?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-") +
                                  "   errors " + MiniJson.Num(pair.Value, "unresolvedErrors") + "   " + MiniJson.Str(pair.Value, "reviewId"));
            }
            Console.WriteLine("Roll-up of the targets listed: " + QualityGate.Rollup(verdicts) + " (every required target must pass by itself)");
            return 0;
        }
    }
}
