using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace OrbitalAnte.Verification.EditorTools
{
    /// <summary>
    /// Runs a verification pass and writes its evidence under Verification/Reports/[Build-ID]/.
    ///
    ///   Level 1 (lightweight): the rule tests, then a few targeted pictures of each target.
    ///   Level 2 (full):        the rule tests, every PlayMode test, then the full capture of each target.
    ///
    /// From the editor: Orbital Ante > Verification. From a command line (no window needed, but a graphics
    /// device is, so do not pass -nographics):
    ///
    ///   Unity -batchmode -projectPath unity -executeMethod OrbitalAnte.Verification.EditorTools.VerificationRunner.RunFromCommandLine
    ///         -oaLevel 2 [-oaTargets mannequin,weapons] [-oaBuildId my-id] -logFile -
    ///
    /// The process exits 0 when every test ran and passed, 2 when something failed, 3 when it could not run.
    /// This tool gathers evidence only. It never scores anything and never decides PASS.
    /// </summary>
    [InitializeOnLoad]
    public static class VerificationRunner
    {
        const string ActiveKey = "OA.Verify.Active";
        const string StageKey = "OA.Verify.Stage";
        const string BatchKey = "OA.Verify.Batch";
        const string BuildKey = "OA.Verify.BuildId";
        const string LevelKey = "OA.Verify.Level";
        const string EditXmlKey = "OA.Verify.EditXml";
        const string StartedKey = "OA.Verify.Started";
        public const string CaptureCategory = "OACapture";

        static TestRunnerApi _api;
        static Callbacks _callbacks;

        // Entering Play mode reloads all code, so anything still needed afterwards is kept in SessionState and
        // the callbacks are attached again here.
        static VerificationRunner()
        {
            if (SessionState.GetBool(ActiveKey, false)) Attach();
        }

        static void Attach()
        {
            if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            if (_callbacks == null)
            {
                _callbacks = new Callbacks();
                _api.RegisterCallbacks(_callbacks);
            }
        }

        [MenuItem("Orbital Ante/Verification/Level 1 - Quick Check", priority = 100)]
        static void MenuLevel1() => Start(1, null, null, false);

        [MenuItem("Orbital Ante/Verification/Level 2 - Full Capture", priority = 101)]
        static void MenuLevel2() => Start(2, null, null, false);

        [MenuItem("Orbital Ante/Verification/Open Reports Folder", priority = 140)]
        static void OpenReports()
        {
            Directory.CreateDirectory(VerificationFiles.ReportsRoot);
            EditorUtility.RevealInFinder(VerificationFiles.ReportsRoot);
        }

        [MenuItem("Orbital Ante/Verification/Cancel Stuck Run", priority = 141)]
        static void CancelStuckRun()
        {
            SessionState.SetBool(ActiveKey, false);
            if (File.Exists(VerificationFiles.ContextPath)) File.Delete(VerificationFiles.ContextPath);
            Debug.Log("Orbital Ante verification: cleared. You can start a new run.");
        }

        public static void RunFromCommandLine()
        {
            int level = int.TryParse(VerificationFiles.Argument("-oaLevel"), out int parsed) ? Mathf.Clamp(parsed, 1, 2) : 2;
            string targets = VerificationFiles.Argument("-oaTargets");
            Start(level, string.IsNullOrEmpty(targets) ? null : targets.Split(','), VerificationFiles.Argument("-oaBuildId"), true);
        }

        public static void Start(int level, string[] targets, string buildId, bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Orbital Ante verification: stop Play mode first, then run it again.");
                return;
            }
            if (SessionState.GetBool(ActiveKey, false))
            {
                Debug.LogWarning("Orbital Ante verification: a run is already going.");
                return;
            }
            if (string.IsNullOrEmpty(buildId)) buildId = VerificationFiles.NewBuildId();
            string runDir = VerificationFiles.RunDir(buildId);
            if (File.Exists(Path.Combine(runDir, "test-results.json")))
            {
                // Evidence of an earlier review is never overwritten.
                Debug.LogError("Orbital Ante verification: a run called '" + buildId + "' already exists. Choose another build id.");
                if (batch) EditorApplication.Exit(3);
                return;
            }
            Directory.CreateDirectory(runDir);
            string started = DateTime.UtcNow.ToString("o");
            var context = new Dictionary<string, object>
            {
                ["buildId"] = buildId,
                ["commit"] = VerificationFiles.CurrentCommit(),
                ["level"] = level,
                ["targets"] = targets != null ? new List<object>(targets) : new List<object>(),
                ["startedUtc"] = started,
            };
            File.WriteAllText(VerificationFiles.ContextPath, MiniJson.Write(context));

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetInt(StageKey, 0);
            SessionState.SetInt(LevelKey, level);
            SessionState.SetString(BuildKey, buildId);
            SessionState.SetString(StartedKey, started);
            SessionState.SetString(EditXmlKey, string.Empty);
            Attach();
            Debug.Log("Orbital Ante verification: level " + level + " run '" + buildId + "' started. Rule tests first.");
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(ActiveKey, false)) return;
                string xml = result.ToXml().OuterXml;
                if (SessionState.GetInt(StageKey, 0) == 0)
                {
                    SessionState.SetString(EditXmlKey, xml);
                    SessionState.SetInt(StageKey, 1);
                    // Start the second half on the next editor tick, once this run has fully wound down.
                    EditorApplication.delayCall += StartPlayMode;
                }
                else Finish(xml);
            }
        }

        static void StartPlayMode()
        {
            Attach();
            var filter = new Filter { testMode = TestMode.PlayMode };
            // Level 1 takes pictures only; level 2 also runs every PlayMode test of the game.
            if (SessionState.GetInt(LevelKey, 2) < 2) filter.categoryNames = new[] { CaptureCategory };
            Debug.Log("Orbital Ante verification: starting Play mode for tests and capture. The editor will flicker for a while.");
            _api.Execute(new ExecutionSettings(filter));
        }

        static void Finish(string playXml)
        {
            string buildId = SessionState.GetString(BuildKey, string.Empty);
            bool batch = SessionState.GetBool(BatchKey, false);
            int level = SessionState.GetInt(LevelKey, 2);
            SessionState.SetBool(ActiveKey, false);
            int exit = 3;
            try
            {
                string runDir = VerificationFiles.RunDir(buildId);
                Directory.CreateDirectory(runDir);
                string editXml = SessionState.GetString(EditXmlKey, string.Empty);
                File.WriteAllText(Path.Combine(runDir, "editmode-results.xml"), editXml);
                File.WriteAllText(Path.Combine(runDir, "playmode-results.xml"), playXml);
                Dictionary<string, object> edit = TestResultsSummary.FromNUnitXml(editXml, "EditMode");
                Dictionary<string, object> play = TestResultsSummary.FromNUnitXml(playXml, level < 2 ? "PlayMode (capture suites only)" : "PlayMode (all tests and capture suites)");

                var captures = new List<object>();
                int shots = 0, runtimeErrors = 0, sceneErrors = 0, flagged = 0;
                foreach (string dir in VerificationFiles.TargetDirs(buildId))
                {
                    Dictionary<string, object> manifest = VerificationFiles.ReadJson(Path.Combine(dir, "capture-manifest.json"));
                    if (manifest == null) continue;
                    int targetSceneErrors = 0, targetFlagged = 0;
                    foreach (object o in MiniJson.Items(manifest, "sceneChecks"))
                        if (MiniJson.Str(MiniJson.Obj(o), "severity") == "error") targetSceneErrors++;
                    foreach (object o in MiniJson.Items(manifest, "shots"))
                        if (MiniJson.Items(MiniJson.Obj(o), "flags").Count > 0) targetFlagged++;
                    int targetShots = MiniJson.Items(manifest, "shots").Count;
                    int targetErrors = (int)(MiniJson.Num(manifest, "errorCount") ?? 0);
                    shots += targetShots;
                    runtimeErrors += targetErrors;
                    sceneErrors += targetSceneErrors;
                    flagged += targetFlagged;
                    captures.Add(new Dictionary<string, object>
                    {
                        ["target"] = MiniJson.Str(manifest, "target"),
                        ["manifest"] = VerificationFiles.Relative(Path.Combine(dir, "capture-manifest.json")),
                        ["screenshots"] = targetShots,
                        ["sequences"] = MiniJson.Items(manifest, "sequences").Count,
                        ["runtimeErrorsLogged"] = targetErrors,
                        ["runtimeWarningsLogged"] = (int)(MiniJson.Num(manifest, "warningCount") ?? 0),
                        ["sceneCheckErrors"] = targetSceneErrors,
                        ["flaggedScreenshots"] = targetFlagged,
                        ["notCaptured"] = MiniJson.Items(manifest, "notCaptured"),
                    });
                }

                bool testsRan = (bool)edit["ran"] && (bool)play["ran"];
                bool testsPassed = (bool)edit["allPassed"] && (bool)play["allPassed"];
                var results = new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1,
                    ["buildId"] = buildId,
                    ["commit"] = VerificationFiles.CurrentCommit(),
                    ["level"] = level,
                    ["unityVersion"] = Application.unityVersion,
                    ["platform"] = Application.platform.ToString(),
                    ["startedUtc"] = SessionState.GetString(StartedKey, string.Empty),
                    ["finishedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["compilation"] = new Dictionary<string, object>
                    {
                        ["ok"] = true,
                        ["note"] = "This runner is part of the project's code, so it can only start if every script compiled.",
                    },
                    ["testsRan"] = testsRan,
                    ["testsPassed"] = testsPassed,
                    ["suites"] = new List<object> { edit, play },
                    ["captures"] = captures,
                    ["totals"] = new Dictionary<string, object>
                    {
                        ["screenshots"] = shots,
                        ["runtimeErrorsLoggedDuringCapture"] = runtimeErrors,
                        ["sceneCheckErrors"] = sceneErrors,
                        ["flaggedScreenshots"] = flagged,
                    },
                    ["notes"] = new List<object>
                    {
                        "Gathered by the capture tool, not by the Builder. It records; it does not judge.",
                        "Logged errors and scene check errors are candidates for 'verified errors'. The Critic classifies each one.",
                        "Steam Deck hardware and frame times are not measured by this run.",
                    },
                };
                File.WriteAllText(Path.Combine(runDir, "test-results.json"), MiniJson.Write(results));
                File.WriteAllText(Path.Combine(VerificationFiles.ReportsRoot, VerificationFiles.LastRunFile), buildId);

                string summary = "Orbital Ante verification '" + buildId + "' finished. Rule tests " + edit["passed"] + "/" + edit["total"] +
                                 ", PlayMode " + play["passed"] + "/" + play["total"] + " (" + play["failed"] + " failed, " + play["skipped"] + " skipped), " +
                                 shots + " screenshots, " + runtimeErrors + " errors logged while capturing, " + sceneErrors + " scene check errors.\n" +
                                 "Evidence: " + VerificationFiles.Relative(runDir) + "\nNext: an independent Critic review. This run is not a verdict.";
                if (testsRan && testsPassed) Debug.Log(summary); else Debug.LogWarning(summary);
                exit = !testsRan ? 3 : testsPassed ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogError("Orbital Ante verification could not write its results: " + e);
            }
            finally
            {
                if (File.Exists(VerificationFiles.ContextPath)) File.Delete(VerificationFiles.ContextPath);
            }
            if (batch) EditorApplication.Exit(exit);
        }
    }
}
