using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.Verification.EditorTools
{
    /// <summary>
    /// Visual regression support: compares a run's pictures with the approved baselines (or with an earlier
    /// run), and promotes a run's pictures to baselines once, and only once, its review has passed the gate.
    ///
    /// The numbers and difference pictures it writes are flags for the Critic to interpret. A large difference
    /// may be the improvement that was asked for; a small one may hide a real defect.
    /// </summary>
    public static class BaselineTool
    {
        [MenuItem("Orbital Ante/Verification/Compare Latest Run With Baselines", priority = 120)]
        static void MenuCompare()
        {
            string build = VerificationFiles.LastBuildId();
            if (string.IsNullOrEmpty(build)) { Debug.LogWarning("Orbital Ante verification: no run to compare yet."); return; }
            Compare(build, null);
        }

        /// <summary>Command line: -oaBuildId [id] and optionally -oaAgainst [earlier build id] (default: the approved baselines).</summary>
        public static void CompareFromCommandLine()
        {
            string build = VerificationFiles.Argument("-oaBuildId") ?? VerificationFiles.LastBuildId();
            int compared = Compare(build, VerificationFiles.Argument("-oaAgainst"));
            if (Application.isBatchMode) EditorApplication.Exit(compared >= 0 ? 0 : 3);
        }

        /// <summary>Command line: -oaBuildId [id] -oaTarget [target]. Refuses unless that target's review is a valid PASS.</summary>
        public static void PromoteFromCommandLine()
        {
            bool ok = Promote(VerificationFiles.Argument("-oaBuildId") ?? VerificationFiles.LastBuildId(), VerificationFiles.Argument("-oaTarget"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 2);
        }

        static byte[] LoadRgb(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                width = texture.width;
                height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                var rgb = new byte[pixels.Length * 3];
                for (int i = 0, o = 0; i < pixels.Length; i++)
                {
                    rgb[o++] = pixels[i].r;
                    rgb[o++] = pixels[i].g;
                    rgb[o++] = pixels[i].b;
                }
                return rgb;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void SaveRgb(byte[] rgb, int width, int height, string path)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.LoadRawTextureData(rgb);
                texture.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToJPG(88));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>The lossless picture's path below its target's Screenshots folder, used to match shots between runs.</summary>
        static string ShotKey(Dictionary<string, object> shot)
        {
            string file = MiniJson.Str(shot, "losslessFile") ?? string.Empty;
            int at = file.IndexOf("/Screenshots/", StringComparison.Ordinal);
            return at >= 0 ? file.Substring(at + "/Screenshots/".Length) : Path.GetFileName(file);
        }

        static bool SameCamera(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            if (a == null || b == null) return false;
            foreach (string key in new[] { "cameraPosition", "cameraEuler" })
            {
                Dictionary<string, object> va = MiniJson.Obj(MiniJson.Get(a, key)), vb = MiniJson.Obj(MiniJson.Get(b, key));
                foreach (string axis in new[] { "x", "y", "z" })
                {
                    double d = Math.Abs((MiniJson.Num(va, axis) ?? 0) - (MiniJson.Num(vb, axis) ?? 0));
                    if (key == "cameraEuler" && d > 180) d = 360 - d;
                    if (d > (key == "cameraEuler" ? 0.05 : 0.005)) return false;
                }
            }
            return Math.Abs((MiniJson.Num(a, "fieldOfView") ?? 0) - (MiniJson.Num(b, "fieldOfView") ?? 0)) < 0.01
                && (MiniJson.Flag(a, "orthographic") ?? false) == (MiniJson.Flag(b, "orthographic") ?? false)
                && Math.Abs((MiniJson.Num(a, "orthographicSize") ?? 0) - (MiniJson.Num(b, "orthographicSize") ?? 0)) < 0.001;
        }

        /// <summary>Compares every target of a run. Returns how many pictures were compared, or -1 if the run was not found.</summary>
        public static int Compare(string buildId, string againstBuildId)
        {
            List<string> targets = VerificationFiles.TargetDirs(buildId);
            if (targets.Count == 0) { Debug.LogWarning("Orbital Ante verification: run '" + buildId + "' has no captures."); return -1; }
            int compared = 0, changed = 0, missing = 0;
            foreach (string dir in targets)
            {
                string target = Path.GetFileName(dir);
                Dictionary<string, object> manifest = VerificationFiles.ReadJson(Path.Combine(dir, "capture-manifest.json"));
                string referenceShots, referenceLabel;
                Dictionary<string, object> referenceManifest;
                if (string.IsNullOrEmpty(againstBuildId))
                {
                    referenceShots = Path.Combine(VerificationFiles.BaselinesRoot, target);
                    referenceManifest = VerificationFiles.ReadJson(Path.Combine(referenceShots, "baseline-manifest.json"));
                    referenceLabel = "approved baseline";
                }
                else
                {
                    string other = Path.Combine(VerificationFiles.RunDir(againstBuildId), target);
                    referenceShots = Path.Combine(other, "Screenshots");
                    referenceManifest = VerificationFiles.ReadJson(Path.Combine(other, "capture-manifest.json"));
                    referenceLabel = "run " + againstBuildId;
                }
                var referenceByKey = new Dictionary<string, Dictionary<string, object>>();
                foreach (object o in MiniJson.Items(referenceManifest, "shots")) referenceByKey[ShotKey(MiniJson.Obj(o))] = MiniJson.Obj(o);

                var rows = new List<object>();
                foreach (object o in MiniJson.Items(manifest, "shots"))
                {
                    Dictionary<string, object> shot = MiniJson.Obj(o);
                    string key = ShotKey(shot);
                    string candidate = VerificationFiles.Absolute(MiniJson.Str(shot, "losslessFile") ?? string.Empty);
                    string reference = Path.Combine(referenceShots, key);
                    var row = new Dictionary<string, object> { ["shot"] = MiniJson.Str(shot, "id"), ["candidate"] = MiniJson.Str(shot, "file") };
                    rows.Add(row);
                    if (!File.Exists(reference)) { row["result"] = "no reference picture"; missing++; continue; }
                    if (!File.Exists(candidate)) { row["result"] = "candidate picture missing on this machine"; missing++; continue; }

                    byte[] a = LoadRgb(reference, out int wa, out int ha);
                    byte[] b = LoadRgb(candidate, out int wb, out int hb);
                    if (wa != wb || ha != hb) { row["result"] = "different size: cannot compare"; continue; }
                    // The studio backdrop colour lets the silhouette be measured for studio pictures only.
                    byte[] backdrop = MiniJson.Str(shot, "context") == "studio" ? new byte[] { 82, 82, 82 } : null;
                    ImageDifference d = ImageCompare.Compare(a, b, wa, ha, 8, backdrop);
                    referenceByKey.TryGetValue(key, out Dictionary<string, object> referenceShot);
                    bool sameCamera = SameCamera(shot, referenceShot);
                    compared++;
                    row["reference"] = VerificationFiles.Relative(reference);
                    row["result"] = ImageCompare.Describe(d);
                    row["rmse"] = Math.Round(d.Rmse, 5);
                    row["changedFraction"] = Math.Round(d.ChangedFraction, 5);
                    row["ssim"] = Math.Round(d.Ssim, 4);
                    row["silhouetteChange"] = backdrop != null ? (object)Math.Round(d.SilhouetteChange, 5) : null;
                    row["changedBox"] = d.MaxX >= 0 ? d.MinX + "," + d.MinY + " to " + d.MaxX + "," + d.MaxY + " (rows counted from the bottom)" : string.Empty;
                    row["sameCamera"] = sameCamera;
                    if (!sameCamera) row["warning"] = "The camera or lens differs from the reference, so this is not a matched before-and-after pair.";
                    if (d.ChangedFraction >= 0.0005)
                    {
                        changed++;
                        string diffPath = Path.Combine(dir, "Comparisons", Path.ChangeExtension(key, null) + "_diff.jpg");
                        SaveRgb(ImageCompare.DifferenceImage(a, b, wa, ha), wa, ha, diffPath);
                        row["differencePicture"] = VerificationFiles.Relative(diffPath);
                    }
                }
                var report = new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1,
                    ["buildId"] = buildId,
                    ["target"] = target,
                    ["comparedWith"] = referenceLabel,
                    ["generatedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["howToRead"] = "Supporting evidence only. changedFraction = share of pixels that moved by more than 8/255; ssim = structural similarity (1 = same); " +
                                    "silhouetteChange = share of pixels that switched between subject and studio backdrop. Interpret every row by looking at the pictures.",
                    ["shots"] = rows,
                };
                Directory.CreateDirectory(Path.Combine(dir, "Comparisons"));
                string name = string.IsNullOrEmpty(againstBuildId) ? "comparison-baseline.json" : "comparison-" + againstBuildId + ".json";
                File.WriteAllText(Path.Combine(dir, "Comparisons", name), MiniJson.Write(report));
            }
            Debug.Log("Orbital Ante verification: compared " + compared + " pictures of run '" + buildId + "' with " +
                      (string.IsNullOrEmpty(againstBuildId) ? "the approved baselines" : "run '" + againstBuildId + "'") + ": " + changed + " changed, " + missing +
                      " without a reference. See the Comparisons folder of each target.");
            return compared;
        }

        /// <summary>
        /// Makes a run's pictures of one target the approved baseline. Only allowed when that target's review
        /// file exists, is valid, and passes: baselines are never replaced by work that has not passed.
        /// </summary>
        public static bool Promote(string buildId, string target)
        {
            if (string.IsNullOrEmpty(buildId) || string.IsNullOrEmpty(target)) { Debug.LogError("Orbital Ante verification: promote needs a build id and a target."); return false; }
            string dir = Path.Combine(VerificationFiles.RunDir(buildId), target);
            string reviewPath = Path.Combine(dir, "issues.json");
            Dictionary<string, object> review;
            try { review = VerificationFiles.ReadJson(reviewPath); }
            catch (FormatException e) { Debug.LogError("Orbital Ante verification: " + reviewPath + " is not valid JSON: " + e.Message); return false; }
            if (review == null) { Debug.LogError("Orbital Ante verification: no review (issues.json) for '" + target + "' in run '" + buildId + "'. Nothing promoted."); return false; }
            GateResult gate = QualityGate.Check(review, p => File.Exists(VerificationFiles.Absolute(p)) || File.Exists(Path.Combine(dir, p)));
            if (!gate.Valid || gate.ComputedVerdict != QualityGate.Pass)
            {
                Debug.LogError("Orbital Ante verification: '" + target + "' has not passed (" + gate.ComputedVerdict + (gate.Valid ? string.Empty : ", review file invalid: " + string.Join(" | ", gate.Problems)) + "). Baselines left as they are.");
                return false;
            }
            string from = Path.Combine(dir, "Screenshots");
            string to = Path.Combine(VerificationFiles.BaselinesRoot, target);
            int copied = 0;
            foreach (string file in Directory.GetFiles(from, "*.png", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(to, file.Substring(from.Length).TrimStart('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
                copied++;
            }
            if (copied == 0) { Debug.LogError("Orbital Ante verification: no lossless pictures for that run on this machine. Promote from the machine that captured them."); return false; }
            File.Copy(Path.Combine(dir, "capture-manifest.json"), Path.Combine(to, "baseline-manifest.json"), true);
            File.WriteAllText(Path.Combine(to, "baseline.json"), MiniJson.Write(new Dictionary<string, object>
            {
                ["target"] = target,
                ["buildId"] = buildId,
                ["reviewId"] = MiniJson.Str(review, "reviewId"),
                ["score"] = gate.ComputedScore,
                ["promotedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            }));
            Debug.Log("Orbital Ante verification: " + copied + " pictures of '" + target + "' from run '" + buildId + "' are now the approved baseline.");
            return true;
        }
    }
}
