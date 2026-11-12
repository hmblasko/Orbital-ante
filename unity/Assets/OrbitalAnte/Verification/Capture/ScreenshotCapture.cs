using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>Everything recorded about one picture, so it can be traced and taken again from the same place.</summary>
    [Serializable]
    public sealed class ShotRecord
    {
        public string id;
        public string file;          // .jpg review copy, relative to the repository root
        public string losslessFile;  // .png, kept on the capturing machine (not committed)
        public string target;
        public string view;
        public string distance;
        public string state;         // test state: pose, damage, weather, which body
        public string context;       // "studio" or "gameplay"
        public string scene;
        public int width;
        public int height;
        public Vector3 cameraPosition;
        public Vector3 cameraEuler;
        public Quaternion cameraRotation;
        public float fieldOfView;
        public bool orthographic;
        public float orthographicSize;
        public float nearClip;
        public float farClip;
        public string lighting;
        public string animation;
        public bool uiIncluded;
        public int frame;
        public float gameTime;
        public float meanLuma;
        public float lumaSpread;
        public float blackFraction;
        public float magentaFraction;
        public string[] flags = new string[0];
    }

    public struct ShotRequest
    {
        public string Target, View, Distance, State, Context, Lighting, Animation;
        public int Width, Height;
        /// <summary>Draw the game's on-screen UI into the picture as well.</summary>
        public bool IncludeUi;
        /// <summary>Sub-folder under the target's Screenshots folder, for frame sequences.</summary>
        public string Folder;
    }

    /// <summary>
    /// Takes pictures of the running Unity scene through a real camera. Nothing here draws, edits or invents
    /// pixels: a camera renders into a texture and the texture is saved, with a record of exactly how.
    /// </summary>
    public static class ScreenshotCapture
    {
        public const int DeckWidth = 1280, DeckHeight = 800;
        public const int DesktopWidth = 1920, DesktopHeight = 1080;

        /// <summary>Renders a camera to a texture the caller owns (and must destroy).</summary>
        public static Texture2D Render(Camera cam, int width, int height, bool includeUi)
        {
            var moved = new List<Canvas>();
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.ResetAspect();
                if (includeUi)
                {
                    // Overlay canvases are drawn straight to the screen and never reach a texture. For this one frame
                    // they are handed to the capture camera instead, then put back exactly as they were.
                    foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    {
                        if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.isActiveAndEnabled) continue;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = cam;
                        canvas.planeDistance = cam.nearClipPlane * 1.5f + 0.01f;
                        moved.Add(canvas);
                    }
                    if (moved.Count > 0) Canvas.ForceUpdateCanvases();
                }
                cam.Render();
                RenderTexture.active = rt;
                picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                picture.Apply(false);
            }
            finally
            {
                foreach (Canvas canvas in moved)
                {
                    if (canvas == null) continue;
                    canvas.worldCamera = null;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                cam.targetTexture = previousTarget;
                cam.ResetAspect();
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            return picture;
        }

        /// <summary>Takes one picture, saves it (lossless .png and a .jpg review copy) and returns its record.</summary>
        public static ShotRecord Capture(Camera cam, ShotRequest request, string screenshotsDir)
        {
            Texture2D picture = Render(cam, request.Width, request.Height, request.IncludeUi);
            try
            {
                string name = FileSafe(request.View) + "_" + FileSafe(request.Distance) +
                              (string.IsNullOrEmpty(request.State) ? string.Empty : "_" + FileSafe(request.State)) +
                              "_" + request.Width + "x" + request.Height;
                string dir = string.IsNullOrEmpty(request.Folder) ? screenshotsDir : Path.Combine(screenshotsDir, request.Folder);
                Directory.CreateDirectory(dir);
                string png = Path.Combine(dir, name + ".png");
                string jpg = Path.Combine(dir, name + ".jpg");
                File.WriteAllBytes(png, picture.EncodeToPNG());
                File.WriteAllBytes(jpg, picture.EncodeToJPG(92));

                ImageStats stats = ImageCompare.Stats(picture.GetRawTextureData(), request.Width, request.Height);
                var flags = new List<string>();
                if (stats.LooksBlank) flags.Add("blank_frame");
                if (stats.LooksPink) flags.Add("pink_shader_error");
                if (stats.BlackFraction > 0.97) flags.Add("almost_all_black");

                Transform t = cam.transform;
                return new ShotRecord
                {
                    id = request.Target + "/" + (string.IsNullOrEmpty(request.Folder) ? string.Empty : request.Folder.Replace('\\', '/') + "/") + name,
                    file = RunContext.Relative(jpg),
                    losslessFile = RunContext.Relative(png),
                    target = request.Target,
                    view = request.View,
                    distance = request.Distance,
                    state = request.State ?? string.Empty,
                    context = request.Context ?? string.Empty,
                    scene = SceneManager.GetActiveScene().name,
                    width = request.Width,
                    height = request.Height,
                    cameraPosition = t.position,
                    cameraEuler = t.eulerAngles,
                    cameraRotation = t.rotation,
                    fieldOfView = cam.fieldOfView,
                    orthographic = cam.orthographic,
                    orthographicSize = cam.orthographicSize,
                    nearClip = cam.nearClipPlane,
                    farClip = cam.farClipPlane,
                    lighting = request.Lighting ?? string.Empty,
                    animation = request.Animation ?? string.Empty,
                    uiIncluded = request.IncludeUi,
                    frame = Time.frameCount,
                    gameTime = Time.time,
                    meanLuma = (float)stats.MeanLuma,
                    lumaSpread = (float)stats.LumaSpread,
                    blackFraction = (float)stats.BlackFraction,
                    magentaFraction = (float)stats.MagentaFraction,
                    flags = flags.ToArray(),
                };
            }
            finally
            {
                UnityEngine.Object.Destroy(picture);
            }
        }

        public static string FileSafe(string text)
        {
            if (string.IsNullOrEmpty(text)) return "none";
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '.') chars[i] = '-';
            return new string(chars).ToLowerInvariant();
        }

        /// <summary>
        /// Lays frames of a sequence out in a grid, in reading order, and saves it. A contact sheet lets motion be
        /// judged from one picture: stride, weight shift, contact and transitions are visible frame to frame.
        /// </summary>
        public static string SaveContactSheet(IList<Texture2D> frames, int columns, string path)
        {
            if (frames.Count == 0) return null;
            int w = frames[0].width, h = frames[0].height;
            int rows = (frames.Count + columns - 1) / columns;
            var sheet = new Texture2D(w * columns, h * rows, TextureFormat.RGB24, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 20, 24, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < frames.Count; i++)
            {
                int col = i % columns, row = i / columns;
                // Texture rows run bottom to top, so the first row of frames goes at the top of the sheet.
                sheet.SetPixels32(col * w, (rows - 1 - row) * h, w, h, frames[i].GetPixels32());
            }
            sheet.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToJPG(90));
            UnityEngine.Object.Destroy(sheet);
            return RunContext.Relative(path);
        }
    }
}
