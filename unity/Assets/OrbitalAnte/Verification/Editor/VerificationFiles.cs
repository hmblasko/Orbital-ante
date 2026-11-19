using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OrbitalAnte.Verification.EditorTools
{
    /// <summary>Where verification files live, and small helpers for reading them.</summary>
    public static class VerificationFiles
    {
        public const string ContextFile = ".current-run.json";
        public const string LastRunFile = ".last-run";

        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string VerificationRoot => Path.Combine(RepoRoot, "Verification");
        public static string ReportsRoot => Path.Combine(VerificationRoot, "Reports");
        public static string BaselinesRoot => Path.Combine(VerificationRoot, "Baselines");
        public static string HistoryPath => Path.Combine(VerificationRoot, "History", "history.json");
        public static string ContextPath => Path.Combine(ReportsRoot, ContextFile);
        public static string RunDir(string buildId) => Path.Combine(ReportsRoot, buildId);

        public static string Relative(string fullPath)
        {
            string root = RepoRoot.Replace('\\', '/').TrimEnd('/') + "/";
            string path = Path.GetFullPath(fullPath).Replace('\\', '/');
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        public static string Absolute(string relative) => Path.GetFullPath(Path.Combine(RepoRoot, relative));

        /// <summary>The commit the working copy is on, read from the .git folder (no git program needed). Empty if unknown.</summary>
        public static string CurrentCommit()
        {
            try
            {
                string git = Path.Combine(RepoRoot, ".git");
                if (File.Exists(git))
                {
                    // A worktree: .git is a file that points at the real folder.
                    string pointer = File.ReadAllText(git).Trim();
                    if (pointer.StartsWith("gitdir:")) git = Path.GetFullPath(Path.Combine(RepoRoot, pointer.Substring(7).Trim()));
                }
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (!head.StartsWith("ref:")) return head;
                string reference = head.Substring(4).Trim();
                string loose = Path.Combine(git, reference);
                if (File.Exists(loose)) return File.ReadAllText(loose).Trim();
                string packed = Path.Combine(git, "packed-refs");
                if (File.Exists(packed))
                    foreach (string line in File.ReadAllLines(packed))
                        if (line.EndsWith(" " + reference)) return line.Substring(0, line.IndexOf(' '));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return string.Empty;
        }

        public static string NewBuildId()
        {
            string commit = CurrentCommit();
            return DateTime.Now.ToString("yyyyMMdd-HHmm") + (commit.Length >= 7 ? "-" + commit.Substring(0, 7) : string.Empty);
        }

        public static string LastBuildId()
        {
            string path = Path.Combine(ReportsRoot, LastRunFile);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }

        public static Dictionary<string, object> ReadJson(string path)
            => File.Exists(path) ? MiniJson.Obj(MiniJson.Parse(File.ReadAllText(path))) : null;

        /// <summary>The value after a command-line switch such as -oaLevel, or null.</summary>
        public static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>Each target folder of a run that holds a capture manifest.</summary>
        public static List<string> TargetDirs(string buildId)
        {
            var dirs = new List<string>();
            string run = RunDir(buildId);
            if (!Directory.Exists(run)) return dirs;
            foreach (string dir in Directory.GetDirectories(run))
                if (File.Exists(Path.Combine(dir, "capture-manifest.json"))) dirs.Add(dir);
            dirs.Sort(StringComparer.Ordinal);
            return dirs;
        }
    }
}
