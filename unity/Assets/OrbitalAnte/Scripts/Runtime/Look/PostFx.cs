using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Runtime picture finish. URP is reached by name so a missing pipeline setting is reported without
    /// preventing the game from starting. Look edits reuse the same profile and effects.
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
        const string CoreAssembly = "Unity.RenderPipelines.Core.Runtime";
        const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";
        const float SettingsInterval = 0.25f;

        public static PostFx Instance { get; private set; }
        public string Status { get; private set; } = "not built";
        public readonly List<string> Missing = new List<string>();

        LookConfig _config;
        ScriptableObject _profile;
        Behaviour _volume;
        Type _cameraDataType;
        object _bloom, _tone, _grade, _balance, _split, _vignette;
        RenderPipelineAsset _pipeline, _observedPipeline;
        MemberOverride _pipelineHdr, _pipelineSamples;
        readonly Dictionary<Camera, CameraState> _prepared = new Dictionary<Camera, CameraState>();
        readonly List<Camera> _retired = new List<Camera>();
        Camera[] _cameras = new Camera[8];
        FinishSettings _lastApplied;
        bool _hasApplied, _active;
        float _nextSettingsCheck;

        public static void ResetStatics()
        {
            // Release asset settings before a replacement captures them, including with domain reload off.
            if (Instance != null) Instance.enabled = false;
            Instance = null;
        }

        public static PostFx Ensure(GameObject host, LookConfig config)
        {
            if (Instance != null && Instance.gameObject == host) return Instance;
            if (Instance != null) Instance.enabled = false;
            Instance = host.AddComponent<PostFx>();
            Instance._config = config;
            Instance.RefreshSettings();
            return Instance;
        }

        FinishSettings ReadSettings() => new FinishSettings(_config.finish, _config.bloom, _config.bloomThreshold,
            (int)_config.toneMapping, _config.exposure, _config.contrast, _config.saturation, _config.warmth,
            _config.splitTone, _config.vignette, (int)_config.edgeSmoothing);

        void RefreshSettings()
        {
            if (_config == null) return;
            _nextSettingsCheck = Time.unscaledTime + SettingsInterval;
            FinishSettings settings = ReadSettings();
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (_hasApplied && settings.Equals(_lastApplied) && pipeline == _observedPipeline) return;
            _lastApplied = settings;
            _observedPipeline = pipeline;
            _hasApplied = true;
            try { Apply(settings, pipeline); }
            catch (Exception e)
            {
                ReleaseSettings();
                Status = "failed: " + e.Message;
                Debug.LogWarning("Orbital Ante: the picture finish could not be set up, so the picture is drawn without it. " + e);
            }
        }

        bool BuildObjects()
        {
            if (_profile != null && _volume != null) return true;
            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, " + CoreAssembly);
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, " + CoreAssembly);
            _cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " + UrpAssembly);
            if (volumeType == null || profileType == null || _cameraDataType == null)
            {
                Status = "render pipeline parts not found";
                return false;
            }
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance(profileType);
                _profile.name = "OA_Look (runtime)";
                _profile.hideFlags = HideFlags.DontSave;
                // Keep one of each component, including effects that start at zero. Their active flags
                // preserve the original thresholds and let later edits update them in place.
                _bloom = Add("Bloom");
                _tone = Add("Tonemapping");
                _grade = Add("ColorAdjustments");
                _balance = Add("WhiteBalance");
                _split = Add("SplitToning");
                _vignette = Add("Vignette");
            }
            if (_volume == null)
            {
                _volume = (Behaviour)gameObject.AddComponent(volumeType);
                _volume.enabled = false;
                SetMember(_volume, "isGlobal", true);
                SetMember(_volume, "priority", 10f);
                SetMember(_volume, "sharedProfile", _profile);
            }
            return true;
        }

        void Apply(FinishSettings settings, RenderPipelineAsset pipeline)
        {
            if (!settings.Finish)
            {
                ReleaseSettings();
                Status = "switched off (Look asset)";
                return;
            }
            if (pipeline == null)
            {
                ReleaseSettings();
                Status = "no render pipeline (run Orbital Ante > Set Up Project)";
                return;
            }
            if (!BuildObjects()) { ReleaseSettings(); return; }
            if (_pipeline != pipeline)
            {
                _pipelineHdr?.Restore();
                _pipelineSamples?.Restore();
                _pipeline = pipeline;
                _pipelineHdr = new MemberOverride(this, pipeline, "supportsHDR");
                _pipelineSamples = new MemberOverride(this, pipeline, "msaaSampleCount");
            }
            _pipelineHdr.Set(true);
            int samples = settings.EdgeSmoothing == (int)EdgeSmoothing.Multisample4 ? 4
                : settings.EdgeSmoothing == (int)EdgeSmoothing.Multisample2 ? 2 : 1;
            _pipelineSamples.Set(samples);

            SetMember(_bloom, "active", settings.Bloom > 0.001f);
            SetParameter(_bloom, "threshold", settings.BloomThreshold);
            SetParameter(_bloom, "intensity", settings.Bloom);
            SetParameter(_bloom, "scatter", 0.72f);
            SetParameter(_bloom, "highQualityFiltering", false);
            SetParameter(_bloom, "downscale", "Half");
            SetParameter(_bloom, "maxIterations", 4);

            SetMember(_tone, "active", settings.ToneMapping != (int)ToneMapping.None);
            SetParameter(_tone, "mode", settings.ToneMapping == (int)ToneMapping.Filmic ? "ACES" : "Neutral");
            SetMember(_grade, "active", true);
            SetParameter(_grade, "postExposure", settings.Exposure);
            SetParameter(_grade, "contrast", settings.Contrast);
            SetParameter(_grade, "saturation", settings.Saturation);
            SetMember(_balance, "active", Mathf.Abs(settings.Warmth) > 0.01f);
            SetParameter(_balance, "temperature", settings.Warmth);
            SetMember(_split, "active", settings.SplitTone > 0.001f);
            SetParameter(_split, "shadows", Color.Lerp(Color.grey, new Color(0.38f, 0.5f, 0.62f), settings.SplitTone));
            SetParameter(_split, "highlights", Color.Lerp(Color.grey, new Color(0.6f, 0.52f, 0.4f), settings.SplitTone));
            SetParameter(_split, "balance", 0f);
            SetMember(_vignette, "active", settings.Vignette > 0.001f);
            SetParameter(_vignette, "intensity", settings.Vignette);
            SetParameter(_vignette, "smoothness", 0.45f);

            _active = true;
            _volume.enabled = true;
            foreach (var pair in _prepared) if (pair.Key != null) ApplyCamera(pair.Value);
            Status = Missing.Count == 0 ? "on" : "on, but missing: " + string.Join(", ", Missing);
            if (Missing.Count > 0)
                Debug.LogWarning("Orbital Ante: some picture finish settings were not found in this version of the render pipeline: " + string.Join(", ", Missing));
        }

        void LateUpdate()
        {
            if (_config == null) return;
            if (Time.unscaledTime >= _nextSettingsCheck) RefreshSettings();
            if (!_active) return;
            // Existing camera discovery stays before drawing; unchanged settings only compare a value
            // struct four times per second, with no reflection, boxing or collection creation.
            if (Camera.allCamerasCount > _cameras.Length) _cameras = new Camera[Camera.allCamerasCount * 2];
            int count = Camera.GetAllCameras(_cameras);
            for (int i = 0; i < count; i++)
            {
                Camera cam = _cameras[i];
                if (!_prepared.ContainsKey(cam)) Prepare(cam);
                _cameras[i] = null;
            }
            if (_prepared.Count > 32)
            {
                _retired.Clear();
                foreach (var pair in _prepared) if (pair.Key == null) _retired.Add(pair.Key);
                for (int i = 0; i < _retired.Count; i++) _prepared.Remove(_retired[i]);
                _retired.Clear();
            }
        }

        CameraState GetCamera(Camera cam)
        {
            if (!_prepared.TryGetValue(cam, out CameraState state))
            {
                state = new CameraState(this, cam);
                _prepared.Add(cam, state);
            }
            return state;
        }

        /// <summary>Tells one camera to draw the finish. Existing cockpit opt-outs remain in force.</summary>
        public void Prepare(Camera cam)
        {
            if (cam == null) return;
            CameraState state = GetCamera(cam);
            if (_active) ApplyCamera(state);
        }

        void ApplyCamera(CameraState state)
        {
            try
            {
                state.Prepare(_cameraDataType);
                state.Hdr.Set(true);
                state.Msaa.Set(true);
                state.Post.Set(state.Finish);
                state.StopNaN.Set(true);
                state.Dithering.Set(true);
                if (_lastApplied.EdgeSmoothing == (int)EdgeSmoothing.Fast)
                    state.Antialiasing.Set("FastApproximateAntialiasing");
                else state.Antialiasing.Restore();
                if (state.ControlShadows) state.Shadows.Set(state.Finish);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Orbital Ante: could not switch the picture finish on for camera '" + state.Camera.name + "': " + e.Message);
            }
        }

        /// <summary>Records the cockpit's full-screen/PIP choice even while the master finish is off.</summary>
        public void SetFinish(Camera cam, bool on)
        {
            if (cam == null) return;
            CameraState state = GetCamera(cam);
            state.Finish = on;
            state.ControlShadows = true;
            if (_active) ApplyCamera(state);
        }

        void ReleaseSettings()
        {
            _active = false;
            if (_volume != null) _volume.enabled = false;
            foreach (var pair in _prepared) pair.Value.Restore();
            _pipelineHdr?.Restore();
            _pipelineSamples?.Restore();
            _pipeline = null;
            _pipelineHdr = _pipelineSamples = null;
        }

        void OnEnable() { _hasApplied = false; _nextSettingsCheck = 0f; }
        void OnDisable() { ReleaseSettings(); _hasApplied = false; }

        void OnDestroy()
        {
            ReleaseSettings();
            if (_volume != null) Destroy(_volume);
            if (_profile != null)
            {
                FieldInfo list = _profile.GetType().GetField("components", BindingFlags.Public | BindingFlags.Instance);
                if (list != null && list.GetValue(_profile) is System.Collections.IEnumerable components)
                    foreach (object component in components)
                        if (component is UnityEngine.Object o && o != null) Destroy(o);
                Destroy(_profile);
            }
            if (Instance == this) Instance = null;
        }

        sealed class CameraState
        {
            readonly PostFx _owner;
            Component _data;
            public readonly Camera Camera;
            public readonly MemberOverride Hdr, Msaa;
            public MemberOverride Post, StopNaN, Dithering, Antialiasing, Shadows;
            public bool Finish = true, ControlShadows;
            public CameraState(PostFx owner, Camera camera)
            {
                _owner = owner; Camera = camera;
                Hdr = new MemberOverride(owner, camera, "allowHDR");
                Msaa = new MemberOverride(owner, camera, "allowMSAA");
            }
            public void Prepare(Type type)
            {
                if (_data != null) return;
                _data = Camera.GetComponent(type);
                if (_data == null) _data = Camera.gameObject.AddComponent(type);
                Post = new MemberOverride(_owner, _data, "renderPostProcessing");
                StopNaN = new MemberOverride(_owner, _data, "stopNaN");
                Dithering = new MemberOverride(_owner, _data, "dithering");
                Antialiasing = new MemberOverride(_owner, _data, "antialiasing");
                Shadows = new MemberOverride(_owner, _data, "renderShadows");
            }
            public void Restore()
            {
                Hdr.Restore(); Msaa.Restore(); Post?.Restore(); StopNaN?.Restore();
                Dithering?.Restore(); Antialiasing?.Restore(); Shadows?.Restore();
            }
        }

        /// <summary>Own only values we actually changed; do not overwrite a later edit by another system.</summary>
        sealed class MemberOverride
        {
            readonly PostFx _owner;
            readonly object _target;
            readonly PropertyInfo _property;
            readonly FieldInfo _field;
            readonly string _name;
            object _before, _written;
            bool _changed;
            public MemberOverride(PostFx owner, object target, string name)
            {
                _owner = owner; _target = target; _name = target.GetType().Name + "." + name;
                _property = FindProperty(target.GetType(), name);
                if (_property == null) _field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (_property == null && _field == null) owner.RecordMissing(_name);
            }
            bool Alive => !(_target is UnityEngine.Object o) || o != null;
            object Read() => _property != null ? _property.GetValue(_target) : _field.GetValue(_target);
            void Write(object value)
            {
                if (_property != null) _property.SetValue(_target, value);
                else _field.SetValue(_target, value);
            }
            public void Set(object value)
            {
                if (!Alive || (_property == null && _field == null)) return;
                try
                {
                    object wanted = Convert(value, _property != null ? _property.PropertyType : _field.FieldType);
                    object current = Read();
                    if (Equals(current, wanted)) return;
                    object before = _changed ? _before : current;
                    Write(wanted);
                    _before = before; _written = wanted; _changed = true;
                }
                catch (Exception) { _owner.RecordMissing(_name); }
            }
            public void Restore()
            {
                if (!_changed) return;
                try { if (Alive && Equals(Read(), _written)) Write(_before); }
                catch (Exception) { _owner.RecordMissing(_name); }
                _changed = false; _before = _written = null;
            }
        }

        void RecordMissing(string name)
        {
            if (!Missing.Contains(name)) Missing.Add(name);
        }

        // ------------------------------------------------------------------ reaching the pipeline by name

        object Add(string component)
        {
            Type type = Type.GetType("UnityEngine.Rendering.Universal." + component + ", " + UrpAssembly);
            if (type == null)
            {
                RecordMissing(component);
                return null;
            }
            MethodInfo add = _profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (add == null)
            {
                RecordMissing("VolumeProfile.Add");
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
                RecordMissing(component.GetType().Name + "." + name);
                return;
            }
            PropertyInfo valueProperty = FindProperty(parameter.GetType(), "value");
            PropertyInfo overrideProperty = FindProperty(parameter.GetType(), "overrideState");
            if (valueProperty == null || overrideProperty == null)
            {
                RecordMissing(component.GetType().Name + "." + name);
                return;
            }
            try
            {
                valueProperty.SetValue(parameter, Convert(value, valueProperty.PropertyType));
                overrideProperty.SetValue(parameter, true);
            }
            catch (Exception)
            {
                RecordMissing(component.GetType().Name + "." + name);
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
                RecordMissing(type.Name + "." + name);
            }
            catch (Exception)
            {
                RecordMissing(target.GetType().Name + "." + name);
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
