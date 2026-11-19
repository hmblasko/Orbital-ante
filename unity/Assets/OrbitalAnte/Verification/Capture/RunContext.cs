using System;
using System.IO;
using UnityEngine;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// What the current verification run is: its build id, its level and where its evidence goes. The editor
    /// runner writes this to a small file before the tests start, because entering Play mode reloads all code
    /// and nothing held in memory survives that.
    /// </summary>
    [Serializable]
    public sealed class RunContext
    {
        public const string FileName = ".current-run.json";

        public string buildId = string.Empty;
        public string commit = string.Empty;
        /// <summary>1 = lightweight (a few targeted pictures), 2 = full visual review.</summary>
        public int level = 2;
        /// <summary>Target ids to capture; empty means all.</summary>
        public string[] targets = new string[0];
        public string startedUtc = string.Empty;

        static RunContext _current;

        /// <summary>The repository root: two folders above unity/Assets.</summary>
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string ReportsRoot => Path.Combine(RepoRoot, "Verification", "Reports");
        public static string ContextPath => Path.Combine(ReportsRoot, FileName);

        /// <summary>True only while a run started from Orbital Ante > Verification (or its command line) is going.</summary>
        /// <summary>A run file older than six hours is left over from a run that never finished, and is ignored.</summary>
        public static bool Requested
            => File.Exists(ContextPath) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(ContextPath)).TotalHours < 6;

        public static RunContext Current
        {
            get
            {
                if (_current != null) return _current;
                if (File.Exists(ContextPath)) _current = JsonUtility.FromJson<RunContext>(File.ReadAllText(ContextPath));
                if (_current == null || string.IsNullOrEmpty(_current.buildId))
                    _current = new RunContext { buildId = "manual-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), startedUtc = DateTime.UtcNow.ToString("o") };
                return _current;
            }
        }

        public static void Forget() => _current = null;

        public bool Wants(string target)
            => targets == null || targets.Length == 0 || Array.IndexOf(targets, target) >= 0;

        public string RunDir => Path.Combine(ReportsRoot, buildId);
        public string TargetDir(string target) => Path.Combine(RunDir, target);

        /// <summary>A path as written into manifests and reports: relative to the repository root, forward slashes.</summary>
        public static string Relative(string fullPath)
        {
            string root = RepoRoot.Replace('\\', '/').TrimEnd('/') + "/";
            string path = Path.GetFullPath(fullPath).Replace('\\', '/');
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }
    }
}
