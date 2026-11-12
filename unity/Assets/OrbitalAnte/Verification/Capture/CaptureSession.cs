using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Verification.Capture
{
    [Serializable]
    public sealed class LogLine
    {
        public string type;
        public string message;
        public string stack;
        public int count = 1;
    }

    [Serializable]
    public sealed class SequenceRecord
    {
        public string id;
        public string contactSheet;
        public string state;
        public int frames;
        public float secondsBetweenFrames;
        public string note;
    }

    /// <summary>The traceable record of one target's capture: written beside its pictures as capture-manifest.json.</summary>
    [Serializable]
    public sealed class CaptureManifest
    {
        public int schemaVersion = 1;
        public string buildId;
        public string commit;
        public string target;
        public int level;
        public string capturedUtc;
        public string unityVersion;
        public string platform;
        public string graphicsDevice;
        public string graphicsApi;
        public string colorSpace;
        public string renderPipeline;
        public string qualityLevel;
        public int gameSeed;
        public float captureFramerate;
        public string hardwareNote = "Captured in the Unity editor on this machine. Not a Steam Deck: Deck presentation is checked at 1280x800 only, Deck performance is unverified.";
        public List<ShotRecord> shots = new List<ShotRecord>();
        public List<SequenceRecord> sequences = new List<SequenceRecord>();
        public List<Finding> sceneChecks = new List<Finding>();
        public List<LogLine> errors = new List<LogLine>();
        public List<LogLine> warnings = new List<LogLine>();
        public int errorCount;
        public int warningCount;
        public string[] notCaptured = new string[0];
    }

    /// <summary>
    /// One target's capture from start to finish: listens to the Unity log, collects pictures, sequences and
    /// scene checks, and writes the manifest. Errors logged while capturing are recorded, never swallowed.
    /// </summary>
    public sealed class CaptureSession : IDisposable
    {
        public const int Seed = 4242;
        /// <summary>Game time is stepped at this fixed rate while capturing, so sequences repeat frame for frame.</summary>
        public const int Framerate = 30;

        public readonly string Target;
        public readonly RunContext Run;
        public readonly CaptureManifest Manifest;
        public readonly string TargetDir;
        public readonly string ScreenshotsDir;
        readonly List<string> _notCaptured = new List<string>();
        readonly Dictionary<string, LogLine> _seen = new Dictionary<string, LogLine>();
        readonly object _lock = new object();
        readonly int _previousFramerate;
        readonly bool _previousIgnore;

        public bool Full => Run.level >= 2;

        public CaptureSession(string target)
        {
            Target = target;
            Run = RunContext.Current;
            TargetDir = Run.TargetDir(target);
            ScreenshotsDir = Path.Combine(TargetDir, "Screenshots");
            Directory.CreateDirectory(ScreenshotsDir);
            Manifest = new CaptureManifest
            {
                buildId = Run.buildId,
                commit = Run.commit,
                target = target,
                level = Run.level,
                capturedUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "Built-in (no pipeline asset set)",
                qualityLevel = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : string.Empty,
                gameSeed = Seed,
                captureFramerate = Framerate,
            };
            _previousFramerate = Time.captureFramerate;
            Time.captureFramerate = Framerate;
            // The test runner would stop a capture at the first logged error. Errors are wanted in the evidence
            // instead, where they count against the gate, so the capture carries on and records them.
            _previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            bool error = type != LogType.Warning;
            lock (_lock)
            {
                string key = type + "|" + message;
                if (_seen.TryGetValue(key, out LogLine line)) { line.count++; return; }
                line = new LogLine { type = type.ToString(), message = message, stack = stack != null && stack.Length > 1500 ? stack.Substring(0, 1500) : stack };
                _seen[key] = line;
                if (error) { if (Manifest.errors.Count < 200) Manifest.errors.Add(line); }
                else if (Manifest.warnings.Count < 200) Manifest.warnings.Add(line);
            }
        }

        public ShotRecord Shoot(Camera cam, string view, string distance, string state, string context, string lighting,
                                bool includeUi = false, string animation = null, int width = ScreenshotCapture.DeckWidth, int height = ScreenshotCapture.DeckHeight, string folder = null)
        {
            ShotRecord record = ScreenshotCapture.Capture(cam, new ShotRequest
            {
                Target = Target, View = view, Distance = distance, State = state, Context = context, Lighting = lighting,
                Animation = animation, IncludeUi = includeUi, Width = width, Height = height, Folder = folder,
            }, ScreenshotsDir);
            Manifest.shots.Add(record);
            return record;
        }

        public void AddSequence(SequenceRecord sequence) => Manifest.sequences.Add(sequence);

        /// <summary>Runs the mechanical scene checks on what is in the scene now.</summary>
        public void CheckScene(string label) => Manifest.sceneChecks.AddRange(SceneChecks.Run(label));

        /// <summary>Records something this capture was meant to cover and could not, so the gap is never silent.</summary>
        public void Skip(string what, string why) => _notCaptured.Add(what + ": " + why);

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            LogAssert.ignoreFailingMessages = _previousIgnore;
            Time.captureFramerate = _previousFramerate;
            lock (_lock)
            {
                foreach (LogLine line in Manifest.errors) Manifest.errorCount += line.count;
                foreach (LogLine line in Manifest.warnings) Manifest.warningCount += line.count;
            }
            Manifest.notCaptured = _notCaptured.ToArray();
            Directory.CreateDirectory(TargetDir);
            File.WriteAllText(Path.Combine(TargetDir, "capture-manifest.json"), JsonUtility.ToJson(Manifest, true));
        }
    }
}
