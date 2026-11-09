using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>One-click Linux builds for the Steam Deck. Output goes to Builds/SteamDeck (not committed).</summary>
    public static class BuildTools
    {
        const string OutputDir = "Builds/SteamDeck";
        const string Executable = "OrbitalAnte.x86_64";

        [MenuItem("Orbital Ante/Build/Steam Deck (playtest, with profiler)", priority = 80)]
        public static void BuildDeckDevelopment() => BuildDeck(true);

        [MenuItem("Orbital Ante/Build/Steam Deck (release)", priority = 81)]
        public static void BuildDeckRelease() => BuildDeck(false);

        static void BuildDeck(bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                EditorUtility.DisplayDialog("Linux build support is missing",
                    "Unity needs the Linux module to build for the Steam Deck.\n\n" +
                    "1. Close Unity.\n" +
                    "2. Open Unity Hub and click Installs.\n" +
                    "3. Click the cog next to your Unity 6 version, then Add modules.\n" +
                    "4. Tick Linux Build Support (Mono) and click Install.\n" +
                    "5. Open the project again and run this menu item again.", "OK");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("No scenes to build",
                    "Run Orbital Ante > Run Project Setup first. It creates the scenes and adds them to the build.", "OK");
                return;
            }

            var problems = OrbitalAnte.Content.ContentValidation.FindProblems(ContentTools.LoadAll());
            if (problems.Count > 0 && !EditorUtility.DisplayDialog("Content problems",
                    $"The content check found {problems.Count} problem(s), for example:\n\n{problems[0]}\n\nBuild anyway?",
                    "Build anyway", "Cancel"))
                return;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = Path.Combine(OutputDir, Executable),
                options = development
                    ? BuildOptions.Development | BuildOptions.ConnectWithProfiler
                    : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                string full = Path.GetFullPath(OutputDir);
                Debug.Log($"[OA] Steam Deck build finished: {full} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s).");
                EditorUtility.RevealInFinder(Path.Combine(full, Executable));
                EditorUtility.DisplayDialog("Build finished",
                    "The build is in:\n" + full + "\n\nCopy the whole SteamDeck folder to the Deck. Steps are in docs/DECK_BUILD.md.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Build failed",
                    $"Result: {summary.result}, {summary.totalErrors} error(s).\n\nOpen Window > General > Console and send me the red lines.", "OK");
            }
        }
    }
}
