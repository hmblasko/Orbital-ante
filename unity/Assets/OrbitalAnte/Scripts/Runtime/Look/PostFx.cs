using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// The finish laid over the whole picture: bloom, tone mapping, a warm and teal colour grade, a light vignette
    /// and smoothed edges. Built from code when the game starts, so there is nothing to set up by hand.
    ///
    /// Like the rest of the game, the render pipeline (URP) is reached by name rather than by a compile-time
    /// reference. If a future Unity version renames something here, the game still runs: the picture is simply
    /// drawn without the finish, and <see cref="Status"/> (shown on the frame-time readout) says what was missing.
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
        const string CoreAssembly = "Unity.RenderPipelines.Core.Runtime";
        const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";

        public static PostFx Instance { get; private set; }
        /// <summary>"on", or the reason the finish is not being drawn.</summary>
        public string Status { get; private set; } = "not built";
        /// <summary>Names of settings that could not be found in this version of the render pipeline.</summary>
        public readonly List<string> Missing = new List<string>();

        LookConfig _config;
        ScriptableObject _profile;
        Type _cameraDataType;
        readonly HashSet<Camera> _prepared = new HashSet<Camera>();
        Camera[] _cameras = new Camera[8];

        public static void ResetStatics() => Instance = null;

        public static PostFx Ensure(GameObject host, LookConfig config)
        {
            // Not an old one that is on its way out with a bootstrap being rebuilt.
            if (Instance != null && Instance.gameObject == host) return Instance;
            Instance = host.AddComponent<PostFx>();
            Instance._config = config;
            try
            {
                Instance.Build();
            }
            catch (Exception e)
            {
                Instance.Status = "failed: " + e.Message;
                Debug.LogWarning("Orbital Ante: the picture finish (bloom, tone mapping, colour grade) could not be set up, so the picture is drawn without it. " + e);
            }
            return Instance;
        }

        void Build()
        {
            if (!_config.finish)
            {
                Status = "switched off (Look asset)";
                return;
            }
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                Status = "no render pipeline (run Orbital Ante > Set Up Project)";
                return;
            }

            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, " + CoreAssembly);
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, " + CoreAssembly);
            _cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " + UrpAssembly);
            if (volumeType == null || profileType == null || _cameraDataType == null)
            {
                Status = "render pipeline parts not found";
                return;
            }

            // The picture must be drawn with headroom above white for bloom and tone mapping to have anything to work on.
            SetMember(pipeline, "supportsHDR", true);
            int samples = _config.edgeSmoothing == EdgeSmoothing.Multisample4 ? 4 : _config.edgeSmoothing == EdgeSmoothing.Multisample2 ? 2 : 1;
            SetMember(pipeline, "msaaSampleCount", samples);

            _profile = ScriptableObject.CreateInstance(profileType);
            _profile.name = "OA_Look (runtime)";
            _profile.hideFlags = HideFlags.DontSave;

            if (_config.bloom > 0.001f)
            {
                object bloom = Add("Bloom");
                SetParameter(bloom, "threshold", _config.bloomThreshold);
                SetParameter(bloom, "intensity", _config.bloom);
                SetParameter(bloom, "scatter", 0.72f);
                SetParameter(bloom, "highQualityFiltering", false);
                // Deck budget: start from a half-size picture and stop after four halvings.
                SetParameter(bloom, "downscale", "Half");
                SetParameter(bloom, "maxIterations", 4);
            }

            if (_config.toneMapping != ToneMapping.None)
            {
                object tone = Add("Tonemapping");
                SetParameter(tone, "mode", _config.toneMapping == ToneMapping.Filmic ? "ACES" : "Neutral");
            }

            object grade = Add("ColorAdjustments");
            SetParameter(grade, "postExposure", _config.exposure);
            SetParameter(grade, "contrast", _config.contrast);
            SetParameter(grade, "saturation", _config.saturation);

            if (Mathf.Abs(_config.warmth) > 0.01f)
            {
                object balance = Add("WhiteBalance");
                SetParameter(balance, "temperature", _config.warmth);
            }

            if (_config.splitTone > 0.001f)
            {
                // Mid grey leaves the picture alone; the further from grey, the stronger the tint.
                object split = Add("SplitToning");
                SetParameter(split, "shadows", Color.Lerp(Color.grey, new Color(0.38f, 0.5f, 0.62f), _config.splitTone));
                SetParameter(split, "highlights", Color.Lerp(Color.grey, new Color(0.6f, 0.52f, 0.4f), _config.splitTone));
                SetParameter(split, "balance", 0f);
            }

            if (_config.vignette > 0.001f)
            {
                object vignette = Add("Vignette");
                SetParameter(vignette, "intensity", _config.vignette);
                SetParameter(vignette, "smoothness", 0.45f);
            }

            Component volume = gameObject.AddComponent(volumeType);
            SetMember(volume, "isGlobal", true);
            SetMember(volume, "priority", 10f);
            SetMember(volume, "sharedProfile", _profile);

            Status = Missing.Count == 0 ? "on" : "on, but missing: " + string.Join(", ", Missing);
            if (Missing.Count > 0)
            {
                Debug.LogWarning("Orbital Ante: some picture finish settings were not found in this version of the render pipeline: " + string.Join(", ", Missing));
            }
        }

        void LateUpdate()
        {
            if (_profile == null || _cameraDataType == null) return;
            // The game makes its cameras from code, in several places. Rather than ask each of them to remember,
            // look every frame, just before drawing, for any camera that has not been told to draw the finish
            // yet, so a new camera never shows a raw frame first. Makes no garbage.
            if (Camera.allCamerasCount > _cameras.Length) _cameras = new Camera[Camera.allCamerasCount * 2];
            int count = Camera.GetAllCameras(_cameras);
            for (int i = 0; i < count; i++)
            {
                if (_prepared.Add(_cameras[i])) Prepare(_cameras[i]);
                _cameras[i] = null;
            }
            if (_prepared.Count > 32) _prepared.RemoveWhere(c => c == null);
        }

        /// <summary>Tells one camera to draw the finish. Safe to call for a camera that has already been done.</summary>
        public void Prepare(Camera cam)
        {
            if (cam == null || _profile == null || _cameraDataType == null) return;
            _prepared.Add(cam);
            try
            {
                cam.allowHDR = true;
                cam.allowMSAA = true;
                Component data = cam.GetComponent(_cameraDataType);
                if (data == null) data = cam.gameObject.AddComponent(_cameraDataType);
                SetMember(data, "renderPostProcessing", true);
                // One bad dot must not become a black block once it is blurred into the bloom, and dark sky
                // gradients need a sprinkle of noise or they show as stripes.
                SetMember(data, "stopNaN", true);
                SetMember(data, "dithering", true);
                if (_config.edgeSmoothing == EdgeSmoothing.Fast) SetMember(data, "antialiasing", "FastApproximateAntialiasing");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Orbital Ante: could not switch the picture finish on for camera '" + cam.name + "': " + e.Message);
            }
        }

        /// <summary>
        /// Switches the finish (and shadows) on or off for one camera. A small picture-in-picture camera has no
        /// use for bloom or shadows and should not pay for them; the same camera shown full screen should.
        /// </summary>
        public void SetFinish(Camera cam, bool on)
        {
            if (cam == null || _profile == null || _cameraDataType == null) return;
            if (!_prepared.Contains(cam)) Prepare(cam);
            try
            {
                Component data = cam.GetComponent(_cameraDataType);
                if (data == null) return;
                SetMember(data, "renderPostProcessing", on);
                SetMember(data, "renderShadows", on);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Orbital Ante: could not change the picture finish for camera '" + cam.name + "': " + e.Message);
            }
        }

        void OnDestroy()
        {
            if (_profile != null)
            {
                // The effects live inside the profile as objects of their own.
                FieldInfo list = _profile.GetType().GetField("components", BindingFlags.Public | BindingFlags.Instance);
                if (list != null && list.GetValue(_profile) is System.Collections.IEnumerable components)
                {
                    foreach (object component in components)
                    {
                        if (component is UnityEngine.Object o && o != null) Destroy(o);
                    }
                }
                Destroy(_profile);
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ reaching the pipeline by name

        object Add(string component)
        {
            Type type = Type.GetType("UnityEngine.Rendering.Universal." + component + ", " + UrpAssembly);
            if (type == null)
            {
                Missing.Add(component);
                return null;
            }
            MethodInfo add = _profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (add == null)
            {
                Missing.Add("VolumeProfile.Add");
                return null;
            }
            return add.Invoke(_profile, new object[] { type, false });
        }

        /// <summary>Sets one dial of an effect and marks it as "use my value, not the default".</summary>
        void SetParameter(object component, string name, object value)
        {
            if (component == null) return;
            FieldInfo field = component.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            object parameter = field != null ? field.GetValue(component) : null;
            if (parameter == null)
            {
                Missing.Add(component.GetType().Name + "." + name);
                return;
            }
            PropertyInfo valueProperty = FindProperty(parameter.GetType(), "value");
            PropertyInfo overrideProperty = FindProperty(parameter.GetType(), "overrideState");
            if (valueProperty == null || overrideProperty == null)
            {
                Missing.Add(component.GetType().Name + "." + name);
                return;
            }
            try
            {
                valueProperty.SetValue(parameter, Convert(value, valueProperty.PropertyType));
                overrideProperty.SetValue(parameter, true);
            }
            catch (Exception)
            {
                Missing.Add(component.GetType().Name + "." + name);
            }
        }

        /// <summary>Sets a plain setting (a field or a property) on a pipeline object.</summary>
        void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            try
            {
                Type type = target.GetType();
                PropertyInfo property = FindProperty(type, name);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, Convert(value, property.PropertyType));
                    return;
                }
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(target, Convert(value, field.FieldType));
                    return;
                }
                Missing.Add(type.Name + "." + name);
            }
            catch (Exception)
            {
                Missing.Add(target.GetType().Name + "." + name);
            }
        }

        // A property redefined in a more specific class would otherwise be reported as "found twice".
        static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null) return p;
            }
            return null;
        }

        static object Convert(object value, Type wanted)
        {
            if (value == null || wanted.IsInstanceOfType(value)) return value;
            if (wanted.IsEnum) return value is string text ? Enum.Parse(wanted, text) : Enum.ToObject(wanted, value);
            return System.Convert.ChangeType(value, wanted);
        }
    }
}
