using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// Plays what the music director (<see cref="MusicBrain"/>) decides. Every stem of a piece is its own audio
    /// source; all of them are started together on the audio clock (sample-accurate, independent of frame rate)
    /// and loop for ever in step, and layering is done purely by moving their volumes. Cues are scheduled the
    /// same way so a riser's hit lands exactly on the downbeat it was aimed at.
    /// Music keeps playing while the game is paused (it ducks instead), because the audio clock does not stop.
    /// </summary>
    public sealed class AdaptiveMusicPlayer
    {
        sealed class TrackVoice
        {
            public MusicTrackDef Def;
            public AudioSource[] Sources;
            public GainLine[] Stems;
            public readonly GainLine Master = new GainLine();
            public bool Active;
            public double StopAt = double.MaxValue;
        }

        const int CueVoices = 3;

        readonly Dictionary<string, TrackVoice> _tracks = new Dictionary<string, TrackVoice>();
        readonly AudioSource[] _cues = new AudioSource[CueVoices];
        readonly List<MusicCommand> _commands = new List<MusicCommand>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioManifest _manifest;
        Transform _root;
        int _nextCue;
        UnityEngine.Audio.AudioMixerGroup _group;

        public MusicBrain Brain { get; private set; }
        /// <summary>Extra gain on all music (pause duck, reward duck), smoothed by the caller.</summary>
        public float Duck = 1f;
        /// <summary>The most recent commands, newest last, for the debug panel.</summary>
        public readonly List<string> Log = new List<string>();

        public void Build(Transform parent, AudioManifest manifest, UnityEngine.Audio.AudioMixerGroup group)
        {
            _manifest = manifest;
            _group = group;
            Brain = new MusicBrain(manifest);
            _root = new GameObject("Music").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < CueVoices; i++) _cues[i] = NewSource("Cue" + i);
            // Every piece gets its sources now, so starting one later creates nothing and loads nothing.
            foreach (MusicTrackDef def in manifest.Tracks.Values) Voice(def.Id);
            WarmCues();
        }

        bool _loaded;

        /// <summary>
        /// True once every stem and cue has finished loading. Music does not start before this: a clip that is
        /// still loading starts late, and one late stem would put a whole piece out of step with itself.
        /// </summary>
        public bool Loaded
        {
            get
            {
                if (_loaded) return true;
                foreach (AudioClip clip in _clips.Values)
                {
                    if (clip == null) continue;
                    if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                    if (clip.loadState == AudioDataLoadState.Loading || clip.loadState == AudioDataLoadState.Unloaded) return false;
                }
                _loaded = true;
                return true;
            }
        }

        /// <summary>Largest difference in play position between the stems of the playing piece, in samples. 0 means locked.</summary>
        public int StemSpread()
        {
            MusicTrackDef t = Brain.Track;
            if (t == null || !_tracks.TryGetValue(t.Id, out TrackVoice v)) return 0;
            int lo = int.MaxValue, hi = int.MinValue;
            for (int i = 0; i < v.Sources.Length; i++)
            {
                if (!v.Sources[i].isPlaying) continue;
                int s = v.Sources[i].timeSamples;
                if (s < lo) lo = s;
                if (s > hi) hi = s;
            }
            return hi >= lo ? hi - lo : 0;
        }

        /// <summary>Set by the debug panel while it is showing; the command log is only kept then.</summary>
        public bool KeepLog;

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.bypassReverbZones = true;
            s.bypassEffects = true;
            s.bypassListenerEffects = true;
            s.dopplerLevel = 0f;
            s.priority = 0;
            s.ignoreListenerPause = true;
            s.outputAudioMixerGroup = _group;
            return s;
        }

        AudioClip Clip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!_clips.TryGetValue(path, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(path);
                _clips[path] = clip;
            }
            return clip;
        }

        /// <summary>Loads a piece's audio ahead of need, so starting it later cannot hitch.</summary>
        public void Warm(string trackId)
        {
            if (trackId == null || !_manifest.Tracks.TryGetValue(trackId, out MusicTrackDef def)) return;
            foreach (StemDef s in def.Stems)
            {
                AudioClip clip = Clip(s.Clip);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        public void WarmCues()
        {
            foreach (CueDef c in _manifest.Cues.Values)
            {
                AudioClip clip = Clip(c.Clip);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        /// <summary>Volumes are set a frame ahead of the audio clock, so a piece that starts between two frames starts at its level, not from a frame of silence.</summary>
        const double LookAhead = 0.03;

        public void Tick(float intensity, float heading, float busGain)
        {
            double now = AudioSettings.dspTime;
            _commands.Clear();
            Brain.Update(now, intensity, _commands, heading);
            for (int i = 0; i < _commands.Count; i++) Run(_commands[i], now);

            foreach (TrackVoice v in _tracks.Values)
            {
                if (!v.Active) continue;
                if (now >= v.StopAt)
                {
                    for (int i = 0; i < v.Sources.Length; i++) v.Sources[i].Stop();
                    v.Active = false;
                    continue;
                }
                float master = MixMath.FadeCurve(v.Master.At(now + LookAhead)) * busGain * Duck;
                for (int i = 0; i < v.Sources.Length; i++)
                {
                    v.Sources[i].volume = master * MixMath.FadeCurve(v.Stems[i].At(now + LookAhead));
                }
            }
            for (int i = 0; i < CueVoices; i++) _cues[i].volume = busGain * Duck;
        }

        void Run(MusicCommand c, double now)
        {
            if (KeepLog)
            {
                Log.Add(c.ToString());
                if (Log.Count > 8) Log.RemoveAt(0);
            }
            switch (c.Kind)
            {
                case MusicCommandKind.StartTrack:
                {
                    TrackVoice v = Voice(c.Track);
                    if (v == null) return;
                    // One start time for every stem, even if a long frame has made us late: late together is in step.
                    double startAt = System.Math.Max(c.Time, AudioSettings.dspTime + 0.05);
                    for (int i = 0; i < v.Sources.Length; i++)
                    {
                        AudioSource s = v.Sources[i];
                        s.Stop();
                        v.Stems[i].Reset(0f);
                        if (s.clip == null) continue;
                        s.volume = 0f;
                        s.timeSamples = Mathf.Clamp((int)System.Math.Round(c.Offset * s.clip.frequency), 0, s.clip.samples - 1);
                        s.PlayScheduled(startAt);
                    }
                    v.Master.Reset(c.Seconds > 0 ? 0f : 1f);
                    if (c.Seconds > 0) v.Master.Add(c.Time, c.Seconds, 1f);
                    v.Active = true;
                    v.StopAt = double.MaxValue;
                    break;
                }
                case MusicCommandKind.StopTrack:
                {
                    if (!_tracks.TryGetValue(c.Track, out TrackVoice v) || !v.Active) return;
                    v.Master.Add(c.Time, c.Seconds, 0f);
                    v.StopAt = c.Time + c.Seconds + 0.05;
                    break;
                }
                case MusicCommandKind.StemGain:
                {
                    if (!_tracks.TryGetValue(c.Track, out TrackVoice v)) return;
                    for (int i = 0; i < v.Def.Stems.Length; i++)
                    {
                        if (v.Def.Stems[i].Id == c.Stem) v.Stems[i].Add(c.Time, c.Seconds, c.Gain);
                    }
                    break;
                }
                case MusicCommandKind.PlayCue:
                {
                    if (!_manifest.Cues.TryGetValue(c.Cue, out CueDef cue)) return;
                    AudioClip clip = Clip(cue.Clip);
                    if (clip == null) return;
                    AudioSource s = _cues[_nextCue];
                    _nextCue = (_nextCue + 1) % CueVoices;
                    s.Stop();
                    s.clip = clip;
                    s.PlayScheduled(System.Math.Max(c.Time, now + 0.05));
                    break;
                }
            }
        }

        TrackVoice Voice(string id)
        {
            if (_tracks.TryGetValue(id, out TrackVoice v)) return v;
            if (!_manifest.Tracks.TryGetValue(id, out MusicTrackDef def)) return null;
            v = new TrackVoice { Def = def, Sources = new AudioSource[def.Stems.Length], Stems = new GainLine[def.Stems.Length] };
            for (int i = 0; i < def.Stems.Length; i++)
            {
                AudioSource s = NewSource(def.Id + "." + def.Stems[i].Id);
                s.clip = Clip(def.Stems[i].Clip);
                s.loop = true;
                v.Sources[i] = s;
                v.Stems[i] = new GainLine();
            }
            _tracks[id] = v;
            return v;
        }

        /// <summary>Current level of each stem of the playing piece, for the debug panel.</summary>
        public float StemLevel(int index)
        {
            MusicTrackDef t = Brain.Track;
            if (t == null || !_tracks.TryGetValue(t.Id, out TrackVoice v) || index >= v.Stems.Length) return 0f;
            return v.Stems[index].Value * v.Master.Value;
        }
    }
}
