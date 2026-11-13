using System;
using System.Collections.Generic;

namespace OrbitalAnte.Audio
{
    /// <summary>The mix buses. Every sound belongs to exactly one.</summary>
    public enum AudioBus
    {
        Music,
        Weapons,
        CombatImpacts,
        Engines,
        Environment,
        PlayerEquipment,
        UI,
        Dialogue,
    }

    /// <summary>What a sound does where there is no air to carry it.</summary>
    public enum VacuumPolicy
    {
        /// <summary>Travels through air: in vacuum only a dull, quiet trace arrives through the ground and suit.</summary>
        Air,
        /// <summary>Made by something the player is holding or touching: reaches them through the suit, duller.</summary>
        Self,
        /// <summary>Inside the suit (breath, alarms, radio): never changes.</summary>
        Suit,
        /// <summary>Interface and reward sounds: never changes.</summary>
        Ui,
    }

    /// <summary>One named sound the game can ask for. Read from the audio manifest.</summary>
    public sealed class AudioEventDef
    {
        public string Id;
        public AudioBus Bus;
        /// <summary>Resources paths of the variations (no file extension).</summary>
        public string[] Clips = new string[0];
        public float VolumeDb;
        public float VolVarDb;
        public float PitchVar;
        /// <summary>0 = plays in the head (2D), 1 = fully placed in the world.</summary>
        public float Spatial = 1f;
        public float MinDist = 2f;
        public float MaxDist = 60f;
        /// <summary>0 is most important, 255 least (Unity's convention).</summary>
        public int Priority = 128;
        public int CooldownMs;
        public int MaxVoices = 4;
        public bool Loop;
        public VacuumPolicy Vacuum;
        /// <summary>Semitone steps for sounds that climb as they repeat (a score counting up).</summary>
        public float[] PitchSteps = new float[0];
        public string Note = "";
    }

    public sealed class StemDef
    {
        public string Id;
        public string Clip;
        /// <summary>The stem plays while intensity is inside lo..hi.</summary>
        public float Lo;
        public float Hi = 100f;
        public float FadeInBars;
        public float FadeOutBars = 2f;
    }

    public sealed class MusicTrackDef
    {
        public string Id;
        public string Title = "";
        public double Bpm = 120;
        public int BeatsPerBar = 4;
        public int Bars;
        public double Seconds;
        public StemDef[] Stems = new StemDef[0];
        /// <summary>Bars (counted from 1) where the piece may be picked up, so it does not always return with the same opening.</summary>
        public int[] EntryBars = { 1 };

        public double BeatSeconds => 60.0 / Bpm;
        public double BarSeconds => BeatSeconds * BeatsPerBar;
    }

    public sealed class CueDef
    {
        public string Id;
        public string Clip;
        /// <summary>How much of the cue plays before the moment it lands on.</summary>
        public double PreSeconds;
        public double Seconds;
    }

    public sealed class ContextDef
    {
        public string Id;
        /// <summary>Empty means this situation has no music.</summary>
        public string Track = "";
        /// <summary>After this many times round, the music rests. 0 = never rests.</summary>
        public int MaxLoops;
        public double RestSeconds;
        /// <summary>Whether a fight may take the music over here.</summary>
        public bool Combat = true;
    }

    public sealed class TransitionDef
    {
        public string From = "*";
        public string To = "*";
        /// <summary>"now", "beat", "bar", "2bars" or "phrase" (four bars).</summary>
        public string Quantize = "bar";
        public string Cue = "";
        public string AmbushCue = "";
        public double FadeOut = 4;
        public double FadeIn = 4;
        public double Hush;

        public bool Matches(string from, string to)
        {
            return (From == "*" || From == from) && (To == "*" && to.Length > 0 || To == to);
        }
    }

    /// <summary>Everything the game knows about its audio files, read from Resources/Audio/audio_manifest.json.</summary>
    public sealed class AudioManifest
    {
        public const string ResourcePath = "Audio/audio_manifest";

        public readonly Dictionary<string, AudioEventDef> Events = new Dictionary<string, AudioEventDef>();
        public readonly Dictionary<string, MusicTrackDef> Tracks = new Dictionary<string, MusicTrackDef>();
        public readonly Dictionary<string, CueDef> Cues = new Dictionary<string, CueDef>();
        public readonly Dictionary<string, ContextDef> Contexts = new Dictionary<string, ContextDef>();
        public readonly List<TransitionDef> Transitions = new List<TransitionDef>();
        public string CombatTrack = "";
        public float CombatEnter = 60f;
        public float CombatExit = 38f;
        public double CombatMinSeconds = 14;
        public float AmbushIntensity = 85f;

        public static AudioManifest Parse(string json)
        {
            var m = new AudioManifest();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (object o in MiniJson.List(root, "events"))
            {
                var e = MiniJson.Obj(o);
                var def = new AudioEventDef
                {
                    Id = MiniJson.Str(e, "id"),
                    Bus = ParseEnum(MiniJson.Str(e, "bus"), AudioBus.Environment),
                    Clips = Strings(MiniJson.List(e, "clips")),
                    VolumeDb = (float)MiniJson.Num(e, "volumeDb"),
                    VolVarDb = (float)MiniJson.Num(e, "volVarDb"),
                    PitchVar = (float)MiniJson.Num(e, "pitchVar"),
                    Spatial = (float)MiniJson.Num(e, "spatial", 1),
                    MinDist = (float)MiniJson.Num(e, "minDist", 2),
                    MaxDist = (float)MiniJson.Num(e, "maxDist", 60),
                    Priority = (int)MiniJson.Num(e, "priority", 128),
                    CooldownMs = (int)MiniJson.Num(e, "cooldownMs"),
                    MaxVoices = (int)MiniJson.Num(e, "maxVoices", 4),
                    Loop = MiniJson.Bool(e, "loop"),
                    Vacuum = ParseEnum(MiniJson.Str(e, "vacuum", "air"), VacuumPolicy.Air),
                    PitchSteps = Floats(MiniJson.List(e, "pitchSteps")),
                    Note = MiniJson.Str(e, "note"),
                };
                if (!string.IsNullOrEmpty(def.Id)) m.Events[def.Id] = def;
            }

            var music = MiniJson.Obj(root.TryGetValue("music", out object mo) ? mo : null);
            foreach (object o in MiniJson.List(music, "tracks"))
            {
                var t = MiniJson.Obj(o);
                var def = new MusicTrackDef
                {
                    Id = MiniJson.Str(t, "id"),
                    Title = MiniJson.Str(t, "title"),
                    Bpm = MiniJson.Num(t, "bpm", 120),
                    BeatsPerBar = (int)MiniJson.Num(t, "beatsPerBar", 4),
                    Bars = (int)MiniJson.Num(t, "bars"),
                    Seconds = MiniJson.Num(t, "seconds"),
                };
                var stems = new List<StemDef>();
                foreach (object so in MiniJson.List(t, "stems"))
                {
                    var s = MiniJson.Obj(so);
                    stems.Add(new StemDef
                    {
                        Id = MiniJson.Str(s, "id"),
                        Clip = MiniJson.Str(s, "clip"),
                        Lo = (float)MiniJson.Num(s, "lo"),
                        Hi = (float)MiniJson.Num(s, "hi", 100),
                        FadeInBars = (float)MiniJson.Num(s, "fadeInBars"),
                        FadeOutBars = (float)MiniJson.Num(s, "fadeOutBars", 2),
                    });
                }
                def.Stems = stems.ToArray();
                List<object> entries = MiniJson.List(t, "entryBars");
                if (entries.Count > 0)
                {
                    def.EntryBars = new int[entries.Count];
                    for (int i = 0; i < entries.Count; i++) def.EntryBars[i] = entries[i] is double d ? (int)d : 1;
                }
                if (!string.IsNullOrEmpty(def.Id)) m.Tracks[def.Id] = def;
            }
            foreach (object o in MiniJson.List(music, "cues"))
            {
                var c = MiniJson.Obj(o);
                var def = new CueDef
                {
                    Id = MiniJson.Str(c, "id"),
                    Clip = MiniJson.Str(c, "clip"),
                    PreSeconds = MiniJson.Num(c, "preSeconds"),
                    Seconds = MiniJson.Num(c, "seconds"),
                };
                if (!string.IsNullOrEmpty(def.Id)) m.Cues[def.Id] = def;
            }
            foreach (object o in MiniJson.List(music, "contexts"))
            {
                var c = MiniJson.Obj(o);
                var def = new ContextDef
                {
                    Id = MiniJson.Str(c, "id"),
                    Track = MiniJson.Str(c, "track"),
                    MaxLoops = (int)MiniJson.Num(c, "maxLoops"),
                    RestSeconds = MiniJson.Num(c, "restSeconds"),
                    Combat = MiniJson.Bool(c, "combat", true),
                };
                if (!string.IsNullOrEmpty(def.Id)) m.Contexts[def.Id] = def;
            }
            foreach (object o in MiniJson.List(music, "transitions"))
            {
                var t = MiniJson.Obj(o);
                m.Transitions.Add(new TransitionDef
                {
                    From = MiniJson.Str(t, "from", "*"),
                    To = MiniJson.Str(t, "to", "*"),
                    Quantize = MiniJson.Str(t, "quantize", "bar"),
                    Cue = MiniJson.Str(t, "cue"),
                    AmbushCue = MiniJson.Str(t, "ambushCue"),
                    FadeOut = MiniJson.Num(t, "fadeOut", 4),
                    FadeIn = MiniJson.Num(t, "fadeIn", 4),
                    Hush = MiniJson.Num(t, "hush"),
                });
            }
            m.CombatTrack = MiniJson.Str(music, "combatTrack");
            m.CombatEnter = (float)MiniJson.Num(music, "combatEnter", 60);
            m.CombatExit = (float)MiniJson.Num(music, "combatExit", 38);
            m.CombatMinSeconds = MiniJson.Num(music, "combatMinSeconds", 14);
            m.AmbushIntensity = (float)MiniJson.Num(music, "ambushIntensity", 85);
            return m;
        }

        /// <summary>
        /// Lists everything wrong with the manifest. <paramref name="clipExists"/> answers whether a Resources
        /// path has an audio file behind it; pass null to skip that check.
        /// </summary>
        public void Validate(List<string> problems, Func<string, bool> clipExists = null)
        {
            foreach (AudioEventDef e in Events.Values)
            {
                if (e.Clips.Length == 0) problems.Add("event " + e.Id + " has no clips.");
                if (e.MaxVoices < 1) problems.Add("event " + e.Id + " allows no voices.");
                if (e.VolumeDb > 0.01f) problems.Add("event " + e.Id + " is boosted above full level (" + e.VolumeDb + " dB).");
                if (clipExists != null)
                {
                    foreach (string c in e.Clips) if (!clipExists(c)) problems.Add("event " + e.Id + " points at a missing file: " + c);
                }
            }
            foreach (MusicTrackDef t in Tracks.Values)
            {
                if (t.Stems.Length == 0) problems.Add("track " + t.Id + " has no stems.");
                if (t.Bpm <= 0 || t.Bars <= 0) problems.Add("track " + t.Id + " has no tempo or length.");
                double expect = t.Bars * t.BarSeconds;
                if (Math.Abs(expect - t.Seconds) > 0.002) problems.Add("track " + t.Id + " is " + t.Seconds + " s long but its bars add up to " + expect + " s, so it would drift off the beat.");
                bool floor = false;
                foreach (StemDef s in t.Stems)
                {
                    if (s.Lo <= 0f) floor = true;
                    if (s.Hi < s.Lo) problems.Add("track " + t.Id + " stem " + s.Id + " can never play.");
                    if (clipExists != null && !clipExists(s.Clip)) problems.Add("track " + t.Id + " stem " + s.Id + " points at a missing file: " + s.Clip);
                }
                if (!floor) problems.Add("track " + t.Id + " is silent at intensity 0.");
                foreach (int bar in t.EntryBars) if (bar < 1 || bar > t.Bars) problems.Add("track " + t.Id + " has an entry point outside itself: bar " + bar);
            }
            foreach (CueDef c in Cues.Values)
            {
                if (clipExists != null && !clipExists(c.Clip)) problems.Add("cue " + c.Id + " points at a missing file: " + c.Clip);
                if (c.PreSeconds < 0 || c.PreSeconds >= c.Seconds) problems.Add("cue " + c.Id + " has a lead-in longer than itself.");
            }
            foreach (ContextDef c in Contexts.Values)
            {
                if (c.Track.Length > 0 && !Tracks.ContainsKey(c.Track)) problems.Add("context " + c.Id + " names an unknown track: " + c.Track);
            }
            if (CombatTrack.Length > 0 && !Tracks.ContainsKey(CombatTrack)) problems.Add("the combat track is unknown: " + CombatTrack);
            if (CombatExit >= CombatEnter) problems.Add("combat music would flicker: its exit level is not below its entry level.");
            bool catchAll = false;
            foreach (TransitionDef t in Transitions)
            {
                if (t.Cue.Length > 0 && !Cues.ContainsKey(t.Cue)) problems.Add("a transition names an unknown cue: " + t.Cue);
                if (t.AmbushCue.Length > 0 && !Cues.ContainsKey(t.AmbushCue)) problems.Add("a transition names an unknown cue: " + t.AmbushCue);
                if (t.From != "*" && t.From.Length > 0 && !Tracks.ContainsKey(t.From)) problems.Add("a transition starts from an unknown track: " + t.From);
                if (t.To != "*" && t.To.Length > 0 && !Tracks.ContainsKey(t.To)) problems.Add("a transition leads to an unknown track: " + t.To);
                if (t.From == "*" && t.To == "*") catchAll = true;
            }
            if (Tracks.Count > 0 && !catchAll) problems.Add("there is no catch-all transition (from * to *).");
        }

        public TransitionDef FindTransition(string from, string to)
        {
            foreach (TransitionDef t in Transitions) if (t.Matches(from ?? "", to ?? "")) return t;
            return null;
        }

        static T ParseEnum<T>(string text, T fallback) where T : struct
        {
            return Enum.TryParse(text, true, out T value) ? value : fallback;
        }

        static string[] Strings(List<object> list)
        {
            var a = new string[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i] as string ?? "";
            return a;
        }

        static float[] Floats(List<object> list)
        {
            var a = new float[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i] is double d ? (float)d : 0f;
            return a;
        }
    }
}
