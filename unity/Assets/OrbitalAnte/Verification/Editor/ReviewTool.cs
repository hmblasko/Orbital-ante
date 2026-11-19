using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.Verification.EditorTools
{
    /// <summary>
    /// Checks the Critic's review files of a run against the gate rules, from inside Unity. The same rules are
    /// available without Unity through Verification/Tools/oa-verify.
    /// </summary>
    public static class ReviewTool
    {
        [MenuItem("Orbital Ante/Verification/Check Review Files Of Latest Run", priority = 121)]
        static void MenuCheck()
        {
            string build = VerificationFiles.LastBuildId();
            if (string.IsNullOrEmpty(build)) { Debug.LogWarning("Orbital Ante verification: no run yet."); return; }
            Check(build);
        }

        /// <summary>Returns the roll-up verdict for the targets reviewed in that run.</summary>
        public static string Check(string buildId)
        {
            var verdicts = new List<string>();
            string run = VerificationFiles.RunDir(buildId);
            if (!Directory.Exists(run)) { Debug.LogWarning("Orbital Ante verification: run '" + buildId + "' not found."); return QualityGate.Blocked; }
            foreach (string dir in Directory.GetDirectories(run))
            {
                string target = Path.GetFileName(dir);
                string path = Path.Combine(dir, "issues.json");
                if (!File.Exists(path))
                {
                    if (File.Exists(Path.Combine(dir, "capture-manifest.json")))
                    {
                        verdicts.Add(QualityGate.Blocked);
                        Debug.LogWarning("Orbital Ante verification: '" + target + "' was captured but has no Critic review yet. It counts as BLOCKED, not as passed.");
                    }
                    continue;
                }
                try
                {
                    Dictionary<string, object> review = VerificationFiles.ReadJson(path);
                    GateResult gate = QualityGate.Check(review, p => File.Exists(VerificationFiles.Absolute(p)) || File.Exists(Path.Combine(dir, p)));
                    string line = "'" + target + "': " + gate.ComputedVerdict + ", score " + (gate.ComputedScore?.ToString("0.0") ?? "none") + ", " + gate.UnresolvedErrors + " unresolved errors.";
                    if (gate.Valid) Debug.Log("Orbital Ante verification: " + line);
                    else Debug.LogError("Orbital Ante verification: " + line + " The review file is NOT accepted:\n - " + string.Join("\n - ", gate.Problems));
                    foreach (string warning in gate.Warnings) Debug.LogWarning("Orbital Ante verification: '" + target + "': " + warning);
                    verdicts.Add(gate.Valid ? gate.ComputedVerdict : QualityGate.Blocked);
                }
                catch (FormatException e)
                {
                    verdicts.Add(QualityGate.Blocked);
                    Debug.LogError("Orbital Ante verification: " + path + " is not valid JSON: " + e.Message);
                }
            }
            string rollup = QualityGate.Rollup(verdicts);
            Debug.Log("Orbital Ante verification: run '" + buildId + "' overall: " + rollup + " (every target must pass by itself).");
            return rollup;
        }
    }
}
