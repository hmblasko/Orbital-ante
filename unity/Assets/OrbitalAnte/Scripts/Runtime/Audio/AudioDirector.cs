using System.Collections;
using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// The one place the game asks for sound. Game code names an event ("weapon.rifle.shot") and a position;
    /// this class picks a variation that is not the one just played, sets level and pitch, treats the sound for
    /// the space the listener is in (vacuum, cave, cockpit), finds it a voice within the Steam Deck budget and
    /// plays it. It also owns the adaptive music, the intensity number the music follows, the reward sounds and
    /// the looping beds (engines, wind, breath).
    ///
    /// Everything it knows about the audio files comes from the manifest written by the audio build
    /// (tools/audio/build.py), so game code never names a file.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>World voices plus head voices. See docs/performance_budget.md.</summary>
        public const int Voices = VoiceAllocator.DefaultVoices;
        const int UiVoices = 6;
        /// <summary>Unity's default is 32 voices that really sound. Ours, the music stems and the beds can exceed that.</summary>
        const int RealVoices = 48;

        public static AudioDirector Instance { get; private set; }

        sealed class Voice
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public Transform Tf;
        }

        sealed class LoopVoice
        {
            public AudioEventDef Def;
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public float Level;
            public float Target;
            public float PerSecond;
            public float Pitch = 1f;
            public Transform Follow;
            public bool Playing;
        }

        readonly Voice[] _voices = new Voice[Voices];
        readonly AudioSource[] _ui = new AudioSource[UiVoices];
        readonly Dictionary<string, LoopVoice> _loops = new Dictionary<string, LoopVoice>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        readonly Dictionary<AudioBus, UnityEngine.Audio.AudioMixerGroup> _groups = new Dictionary<AudioBus, UnityEngine.Audio.AudioMixerGroup>();
        VoiceAllocator _allocator;
        VoiceAllocator _uiAllocator;
        double _voiceClock;
        readonly Dictionary<long, AnimationCurve> _rolloffs = new Dictionary<long, AnimationCurve>();
        AudioReverbZone _reverb;
        Enclosure _reverbFor = (Enclosure)(-1);
        Transform _listener;
        float _musicDuck = 1f;
        float _duckUntil;
        System.Random _rng = new System.Random(12345);

        public AudioManifest Manifest { get; private set; }
        public IntensityModel Intensity { get; } = new IntensityModel();
        public RewardRules Rewards { get; } = new RewardRules();
        public AdaptiveMusicPlayer Music { get; private set; }
        /// <summary>The space the listener is in. Game code sets this; sounds started afterwards are treated for it.</summary>
        public ListenerSpace Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
        /// <summary>False if the audio files or manifest are missing; callers then keep their old placeholder sounds.</summary>
        public bool Ready { get; private set; }
        public VoiceAllocator Allocator => _allocator;
        /// <summary>The clock world voices are counted on: it stands still while the game is paused.</summary>
        public double VoiceClock => _voiceClock;
        public string LastEvent { get; private set; } = "";
        /// <summary>Adds the ids of the beds now sounding to a list (for the debug panel).</summary>
        public void ListLoops(List<string> into)
        {
            foreach (KeyValuePair<string, LoopVoice> pair in _loops) if (pair.Value.Playing) into.Add(pair.Key);
        }

        public static void ResetStatics()
        {
            Instance = null;
            AudioPrefs.ResetStatics();
        }

        public static AudioDirector Create(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioDirector");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioDirector>();
        }

        void Awake()
        {
            Instance = this;
            _allocator = new VoiceAllocator(Voices);
            _uiAllocator = new VoiceAllocator(UiVoices);
            RaiseVoiceLimit();
            TextAsset json = Resources.Load<TextAsset>(AudioManifest.ResourcePath);
            if (json == null)
            {
                Debug.LogWarning("[Audio] No audio manifest found (Resources/" + AudioManifest.ResourcePath + ".json). Using the old placeholder sounds.");
                Manifest = new AudioManifest();
            }
            else
            {
                try
                {
                    Manifest = AudioManifest.Parse(json.text);
                    Ready = Manifest.Events.Count > 0;
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Audio] The audio manifest could not be read: " + e.Message);
                    Manifest = new AudioManifest();
                }
            }

            // An AudioMixer asset is optional (the setup tool makes one). With it, each bus shows up in Unity's
            // mixer window; without it, everything still works, mixed here in code.
            var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("Audio/OrbitalAnteMixer");
            if (mixer != null)
            {
                foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
                {
                    UnityEngine.Audio.AudioMixerGroup[] found = mixer.FindMatchingGroups(bus.ToString());
                    if (found != null && found.Length > 0) _groups[bus] = found[0];
                }
            }

            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var v = new Voice { Tf = go.transform, Source = go.AddComponent<AudioSource>(), LowPass = go.AddComponent<AudioLowPassFilter>() };
                v.Source.playOnAwake = false;
                v.Source.dopplerLevel = 0f;
                v.LowPass.enabled = false;
                _voices[i] = v;
            }
            for (int i = 0; i < UiVoices; i++)
            {
                var go = new GameObject("UiVoice" + i);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.bypassReverbZones = true;
                s.ignoreListenerPause = true;
                s.priority = 16;
                _ui[i] = s;
            }

            var zone = new GameObject("Reverb");
            zone.transform.SetParent(transform, false);
            _reverb = zone.AddComponent<AudioReverbZone>();
            _reverb.minDistance = 4000f;
            _reverb.maxDistance = 4001f;
            ApplyReverb(Enclosure.Open);

            Music = new AdaptiveMusicPlayer();
            Music.Build(transform, Manifest, Group(AudioBus.Music));
            if (Ready) StartCoroutine(WarmUp());
        }

        /// <summary>
        /// Asks Unity for more voices than its default 32, before anything has been played. If the platform
        /// refuses, nothing is lost: Unity then quietly drops the least important sounds itself.
        /// </summary>
        static void RaiseVoiceLimit()
        {
            try
            {
                AudioConfiguration config = AudioSettings.GetConfiguration();
                if (config.numRealVoices >= RealVoices) return;
                config.numRealVoices = RealVoices;
                if (config.numVirtualVoices < 256) config.numVirtualVoices = 256;
                if (!AudioSettings.Reset(config)) Debug.LogWarning("[Audio] Could not raise the voice limit; staying at Unity's default.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Audio] Could not raise the voice limit: " + e.Message);
            }
        }

        /// <summary>
        /// Loudness against distance: the natural curve (half as loud for each doubling of distance) from the
        /// event's near distance, eased down to true silence at its far distance. Unity's own "logarithmic"
        /// setting never reaches silence, so a gunshot half a kilometre away would still be heard.
        /// </summary>
        AnimationCurve Rolloff(float min, float max)
        {
            long key = ((long)Mathf.RoundToInt(min * 100f) << 32) | (uint)Mathf.RoundToInt(max * 100f);
            if (_rolloffs.TryGetValue(key, out AnimationCurve curve)) return curve;
            max = Mathf.Max(max, min * 1.5f);
            var keys = new List<Keyframe> { new Keyframe(0f, 1f), new Keyframe(min / max, 1f) };
            for (float d = min * 1.5f; d < max * 0.98f; d *= 1.5f)
            {
                float x = d / max;
                keys.Add(new Keyframe(x, min / d * (1f - x * x * x * x)));
            }
            keys.Add(new Keyframe(1f, 0f));
            curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            _rolloffs[key] = curve;
            return curve;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        UnityEngine.Audio.AudioMixerGroup Group(AudioBus bus) => _groups.TryGetValue(bus, out UnityEngine.Audio.AudioMixerGroup g) ? g : null;

        /// <summary>Loads the audio a little each frame at start-up, so the first shot of a gun never hitches.</summary>
        IEnumerator WarmUp()
        {
            int n = 0;
            foreach (AudioEventDef e in Manifest.Events.Values)
            {
                foreach (string c in e.Clips) Clip(c);
                if (++n % 4 == 0) yield return null;
            }
        }

        AudioClip Clip(string path)
        {
            if (!_clips.TryGetValue(path, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(path);
                _clips[path] = clip;
            }
            return clip;
        }

        public bool Has(string eventId) => Ready && eventId != null && Manifest.Events.ContainsKey(eventId);

        // ------------------------------------------------------------------ one-shots

        /// <summary>Plays an event at a place in the world. False if it did not play (unknown, on cooldown, out of voices).</summary>
        public static bool Play(string eventId, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            return Instance != null && Instance.PlayInternal(eventId, position, false, volume, pitch, 0f);
        }

        /// <summary>Plays an event in the listener's head (their own gear, the interface). Pan is -1 left to 1 right.</summary>
        public static bool PlayLocal(string eventId, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            return Instance != null && Instance.PlayInternal(eventId, Vector3.zero, true, volume, pitch, pan);
        }

        bool PlayInternal(string eventId, Vector3 position, bool local, float volume, float pitch, float pan)
        {
            if (!Ready || eventId == null || volume <= 0f) return false;
            if (!Manifest.Events.TryGetValue(eventId, out AudioEventDef def) || def.Clips.Length == 0) return false;
            AudioClip clip = PickClip(def);
            if (clip == null) return false;

            SoundTreatment treat = AcousticsModel.Treat(def.Vacuum, Space);
            float db = def.VolumeDb + treat.GainDb + (def.VolVarDb > 0f ? Range(-def.VolVarDb, def.VolVarDb) : 0f);
            float gain = MixMath.DbToGain(db) * volume * AudioPrefs.BusGain(def.Bus);
            float p = pitch * (def.PitchVar > 0f ? Mathf.Pow(2f, Range(-def.PitchVar, def.PitchVar) / 12f) : 1f);
            LastEvent = eventId;

            if (def.Vacuum == VacuumPolicy.Ui)
            {
                // Interface and reward sounds keep playing while the game is paused and are never treated.
                // They have voices of their own, each playing one sound, so one sound's pitch never bends another.
                int uiSlot = _uiAllocator.Request(def, Time.unscaledTimeAsDouble, clip.length / Mathf.Max(0.1f, p));
                if (uiSlot < 0) return false;
                AudioSource ui = _ui[uiSlot];
                ui.Stop();
                ui.outputAudioMixerGroup = Group(def.Bus);
                ui.clip = clip;
                ui.volume = Mathf.Clamp01(gain);
                ui.pitch = p;
                ui.panStereo = pan;
                ui.Play();
                return true;
            }

            // Slow motion pulls world sounds down with it, half as far as time itself, so it reads without mud.
            float scale = Time.timeScale;
            if (scale > 0.05f && scale < 0.99f) p *= 0.5f + 0.5f * scale;

            // World sounds freeze while the game is paused, so their clock does too.
            double now = _voiceClock;
            int slot = _allocator.Request(def, now, clip.length / Mathf.Max(0.1f, p));
            if (slot < 0) return false;
            Voice v = _voices[slot];
            AudioSource s = v.Source;
            s.Stop();
            bool flat = local || def.Spatial <= 0.001f;
            v.Tf.position = flat ? ListenerPosition() : position;
            s.clip = clip;
            s.loop = false;
            s.volume = Mathf.Clamp01(gain);
            s.pitch = p;
            s.panStereo = flat ? pan : 0f;
            s.spatialBlend = flat ? 0f : def.Spatial;
            s.minDistance = def.MinDist;
            s.maxDistance = def.MaxDist;
            if (!flat)
            {
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(def.MinDist, def.MaxDist));
            }
            s.priority = def.Priority;
            s.reverbZoneMix = Mathf.Clamp(treat.ReverbSend * 1.6f, 0f, 1.1f);
            s.outputAudioMixerGroup = Group(def.Bus);
            bool filter = treat.Cutoff < 20000f;
            v.LowPass.enabled = filter;
            if (filter) v.LowPass.cutoffFrequency = treat.Cutoff;
            s.Play();
            return true;
        }

        AudioClip PickClip(AudioEventDef def)
        {
            int n = def.Clips.Length;
            int pick = 0;
            if (n > 1)
            {
                // Never the same variation twice running: that repeat is what makes game audio sound like a machine.
                _lastVariant.TryGetValue(def.Id, out int last);
                pick = _rng.Next(n - 1);
                if (pick >= last) pick++;
                _lastVariant[def.Id] = pick;
            }
            return Clip(def.Clips[pick]);
        }

        float Range(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

        float _nextListenerSearch;

        Vector3 ListenerPosition()
        {
            if (_listener == null || !_listener.gameObject.activeInHierarchy)
            {
                _listener = null;
                // Searching the scene is not free: with no listener about, look twice a second, not every frame.
                if (Time.unscaledTime < _nextListenerSearch) return Vector3.zero;
                _nextListenerSearch = Time.unscaledTime + 0.5f;
                foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (l.enabled && l.gameObject.activeInHierarchy) { _listener = l.transform; break; }
                }
            }
            return _listener != null ? _listener.position : Vector3.zero;
        }

        // ------------------------------------------------------------------ loops

        /// <summary>
        /// Sets the level (0 to 1) of a looping event, starting it if need be, moving there over fadeSeconds.
        /// Level 0 fades it out and stops it. Give a transform to place it in the world; otherwise it is in the head.
        /// </summary>
        public void SetLoop(string eventId, float level, float fadeSeconds = 0.5f, Transform follow = null)
        {
            if (!Ready || eventId == null) return;
            if (!_loops.TryGetValue(eventId, out LoopVoice l))
            {
                if (level <= 0f) return;
                if (!Manifest.Events.TryGetValue(eventId, out AudioEventDef def) || def.Clips.Length == 0) return;
                AudioClip clip = Clip(def.Clips[0]);
                if (clip == null) return;
                var go = new GameObject("Loop " + eventId);
                go.transform.SetParent(transform, false);
                l = new LoopVoice { Def = def, Source = go.AddComponent<AudioSource>(), LowPass = go.AddComponent<AudioLowPassFilter>() };
                l.LowPass.enabled = false;
                l.Source.playOnAwake = false;
                l.Source.clip = clip;
                l.Source.loop = true;
                l.Source.dopplerLevel = 0f;
                l.Source.priority = def.Priority;
                l.Source.volume = 0f;
                l.Source.minDistance = def.MinDist;
                l.Source.maxDistance = def.MaxDist;
                l.Source.ignoreListenerPause = def.Vacuum == VacuumPolicy.Ui;
                l.Source.bypassReverbZones = def.Vacuum == VacuumPolicy.Ui || def.Vacuum == VacuumPolicy.Suit;
                l.Source.outputAudioMixerGroup = Group(def.Bus);
                _loops[eventId] = l;
            }
            if (!l.Playing)
            {
                if (level <= 0f) return;
                l.Level = 0f;
                l.Source.volume = 0f;
                l.Source.Play();
                l.Playing = true;
            }
            l.Follow = follow;
            l.Source.spatialBlend = follow != null ? l.Def.Spatial : 0f;
            l.Target = Mathf.Clamp01(level);
            l.PerSecond = fadeSeconds <= 0.001f ? 1000f : 1f / fadeSeconds;
        }

        public void SetLoopPitch(string eventId, float pitch)
        {
            if (eventId != null && _loops.TryGetValue(eventId, out LoopVoice l)) l.Pitch = Mathf.Clamp(pitch, 0.25f, 3f);
        }

        public void StopAllLoops(float fadeSeconds = 0.4f)
        {
            foreach (LoopVoice l in _loops.Values)
            {
                l.Target = 0f;
                l.PerSecond = fadeSeconds <= 0.001f ? 1000f : 1f / fadeSeconds;
            }
        }

        void TickLoops(float dt)
        {
            foreach (KeyValuePair<string, LoopVoice> pair in _loops)
            {
                LoopVoice l = pair.Value;
                if (!l.Playing) continue;
                l.Level = Mathf.MoveTowards(l.Level, l.Target, l.PerSecond * dt);
                if (l.Level <= 0f && l.Target <= 0f)
                {
                    // Kept for next time (an engine is tapped on and off all day), just not sounding.
                    l.Source.Stop();
                    l.Playing = false;
                    continue;
                }
                SoundTreatment treat = AcousticsModel.Treat(l.Def.Vacuum, Space);
                l.Source.volume = Mathf.Clamp01(MixMath.DbToGain(l.Def.VolumeDb + treat.GainDb) * l.Level * AudioPrefs.BusGain(l.Def.Bus));
                l.Source.pitch = l.Pitch;
                bool filter = treat.Cutoff < 20000f;
                if (l.LowPass.enabled != filter) l.LowPass.enabled = filter;
                if (filter) l.LowPass.cutoffFrequency = treat.Cutoff;
                if (l.Follow != null) l.Source.transform.position = l.Follow.position;
            }
        }

        // ------------------------------------------------------------------ rewards, music, frame

        /// <summary>Asks for a reward sound. The reward rules decide if and when it plays.</summary>
        public void Reward(RewardKind kind) => Rewards.Request(kind, Time.unscaledTimeAsDouble);

        public void SetMusicContext(string context)
        {
            if (Music != null) Music.Brain.SetContext(context);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!GameSession.Paused) _voiceClock += dt;
            AudioListener.volume = AudioPrefs.MasterGain * MixMath.DbToGain(MixMath.HeadroomDb);

            // The danger level only moves while the game does.
            if (!GameSession.Paused) Intensity.Tick(Time.deltaTime);

            if (Ready && Rewards.Poll(Time.unscaledTimeAsDouble, out RewardKind kind))
            {
                string id = RewardRules.EventId(kind);
                float pitch = 1f;
                if (kind == RewardKind.PointsTick && Manifest.Events.TryGetValue(id, out AudioEventDef def) && def.PitchSteps.Length > 0)
                {
                    // A score counting up climbs a scale, and holds the top note if it keeps going.
                    int step = Mathf.Min(Rewards.TickStep, def.PitchSteps.Length - 1);
                    pitch = Mathf.Pow(2f, def.PitchSteps[step] / 12f);
                }
                PlayInternal(id, Vector3.zero, true, 1f, pitch, 0f);
                if (RewardRules.DucksMusic(kind)) _duckUntil = Time.unscaledTime + RewardRules.DuckSeconds(kind);
            }

            // Music dips under a big reward and sits back while the game is paused.
            float duck = GameSession.Paused ? 0.4f : Time.unscaledTime < _duckUntil ? 0.6f : 1f;
            _musicDuck = Mathf.MoveTowards(_musicDuck, duck, dt * 1.6f);
            // Music waits until every stem has loaded, so a piece can never start out of step with itself.
            if (Ready && Music.Loaded)
            {
                Music.Duck = _musicDuck;
                Music.Tick(Intensity.Value, Intensity.Target, AudioPrefs.BusGain(AudioBus.Music));
            }

            TickLoops(dt);
            if (Space.Enclosure != _reverbFor) ApplyReverb(Space.Enclosure);
            if (_reverb != null) _reverb.transform.position = ListenerPosition();
        }

        void ApplyReverb(Enclosure e)
        {
            _reverbFor = e;
            if (_reverb == null) return;
            switch (e)
            {
                case Enclosure.Cave: _reverb.reverbPreset = AudioReverbPreset.Cave; break;
                case Enclosure.Interior: _reverb.reverbPreset = AudioReverbPreset.Hangar; break;
                case Enclosure.Cockpit: _reverb.reverbPreset = AudioReverbPreset.Livingroom; break;
                default: _reverb.reverbPreset = AudioReverbPreset.Off; break; // open ground: reflections are not worth a reverb
            }
        }
    }
}
