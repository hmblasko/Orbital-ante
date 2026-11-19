using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// Shared set-up for the capture suites. They are written as PlayMode tests because Orbital Ante builds its
    /// worlds from code at run time, so the game has to be running to be photographed, and Unity's test runner
    /// already knows how to start Play mode, wait for frames and report what happened.
    ///
    /// They do nothing in an ordinary "Run All": a capture only happens when a run was started from
    /// Orbital Ante > Verification (or the command line), which writes the run file these suites look for.
    ///
    /// Everything a suite makes lives under one root object that is destroyed afterwards. Nothing in the
    /// project's assets is changed, and the real save file is never touched.
    /// </summary>
    public abstract class CaptureSuite
    {
        public const string Category = "OACapture";
        public const string StudioLighting = "studio-v1: key 50/-35 white 1.25, fill 20/150 cool 0.45, flat ambient 0.42, backdrop rgb 82";
        public static readonly Color32 Backdrop = new Color32(82, 82, 82, 255);

        protected CaptureSession Session;
        protected GameObject Root;
        protected Camera Cam;
        AmbientMode _ambientMode;
        Color _ambientLight;
        bool _ambientSaved;

        /// <summary>Starts a target's capture, or skips the test if this run does not want it.</summary>
        protected void Begin(string target)
        {
            if (!RunContext.Requested)
                Assert.Ignore("Capture suites only run from Orbital Ante > Verification, not from an ordinary test run.");
            RunContext.Forget();
            if (!RunContext.Current.Wants(target)) Assert.Ignore("'" + target + "' is not in this run's scope.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Inconclusive("No graphics device (was Unity started with -nographics?). Nothing can be captured: this target is BLOCKED.");
            Root = new GameObject("OA_CaptureRoot_" + target);
            Session = new CaptureSession(target);
        }

        [TearDown]
        public void EndCapture()
        {
            Cleanup();
            if (_ambientSaved)
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientLight;
                _ambientSaved = false;
            }
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
            Cam = null;
            if (Session != null) Session.Dispose();
            Session = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Suite-specific clean-up, called before the root is destroyed and the manifest is written.</summary>
        protected virtual void Cleanup() { }

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>A camera that only draws when asked to, so it never appears on screen.</summary>
        protected Camera MakeCamera(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.allowHDR = true;
            return cam;
        }

        /// <summary>
        /// The neutral studio: a fixed key light, a fixed fill light, flat ambient light, a grey backdrop and a
        /// grey floor. It is the same every time, so pictures from different revisions can be laid side by side.
        /// </summary>
        protected void BuildStudio(bool floor = true)
        {
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            _ambientSaved = true;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f);

            Light key = new GameObject("StudioKey").AddComponent<Light>();
            key.transform.SetParent(Root.transform, false);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            key.color = Color.white;
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;

            Light fill = new GameObject("StudioFill").AddComponent<Light>();
            fill.transform.SetParent(Root.transform, false);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            fill.color = new Color(0.78f, 0.85f, 1f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;

            if (floor)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "StudioFloor";
                ground.transform.SetParent(Root.transform, false);
                ground.transform.position = new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(400f, 1f, 400f);
            }
            Cam = MakeCamera("StudioCamera");
        }

        /// <summary>Takes a set of standard studio views of a subject.</summary>
        protected void ShootStudio(Transform subject, IEnumerable<InspectionCameras.Shot> shots, string state, string animation = null)
        {
            Bounds bounds = InspectionCameras.BoundsOf(subject);
            float aspect = ScreenshotCapture.DeckWidth / (float)ScreenshotCapture.DeckHeight;
            foreach (InspectionCameras.Shot shot in shots)
            {
                InspectionCameras.Place(Cam, bounds, subject.rotation, shot.View, shot.Distance, aspect);
                Session.Shoot(Cam, shot.View.Name, shot.Distance.ToString(), state, "studio", StudioLighting, false, animation);
            }
        }

        /// <summary>
        /// Records motion: lets the game run and photographs every few frames with a camera that travels with the
        /// subject, then lays the frames out as one contact sheet. Game time advances at a fixed 30 steps a second
        /// while capturing, so "every 2 frames" is always 1/15 s.
        /// </summary>
        protected IEnumerator Sequence(string name, string state, Func<Vector3> focus, float radius, Quaternion facing, InspectionView view,
                                       int frames, int every, string note, Camera gameCamera = null, Action<int> onFrame = null)
        {
            const int w = 480, h = 300;
            var pictures = new List<Texture2D>();
            var bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2f / Mathf.Sqrt(3f)));
            for (int i = 0; i < frames * every; i++)
            {
                onFrame?.Invoke(i);
                yield return null;
                if (i % every != 0) continue;
                Camera cam = gameCamera;
                if (cam == null)
                {
                    cam = Cam;
                    bounds.center = focus();
                    InspectionCameras.Place(cam, bounds, facing, view, ViewDistance.Medium, w / (float)h);
                }
                pictures.Add(ScreenshotCapture.Render(cam, w, h, false));
            }
            string path = Path.Combine(Session.ScreenshotsDir, "sequences", ScreenshotCapture.FileSafe(name) + ".jpg");
            string saved = ScreenshotCapture.SaveContactSheet(pictures, 4, path);
            foreach (Texture2D picture in pictures) UnityEngine.Object.Destroy(picture);
            Session.AddSequence(new SequenceRecord
            {
                id = Session.Target + "/sequences/" + name,
                contactSheet = saved,
                state = state,
                frames = frames,
                secondsBetweenFrames = every / (float)CaptureSession.Framerate,
                note = note + " Frames read left to right, top to bottom. " + (gameCamera != null ? "Taken through the game's own camera." : "Camera: " + view.Name + ", travelling with the subject."),
            });
        }
    }
}
