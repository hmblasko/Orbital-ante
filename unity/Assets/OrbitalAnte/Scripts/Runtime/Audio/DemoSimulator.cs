using System.Collections.Generic;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// Runs the audio showcase script through the real rules (intensity, music director, reward rules,
    /// acoustics) on a pretend clock, and writes down everything that would be played and when.
    /// The rule tests use it to check the whole five minutes behaves, and the offline preview renderer
    /// (tools/audio/render_demo.py) turns its record into an audio file. No Unity types.
    /// </summary>
    public sealed class DemoSimulator
    {
        public struct Sound
        {
            public double Time;
            public string Event;
            public float GainDb;
            public float Cutoff;
            public float Pitch;
            public float Side;
            public float Metres;
            public float Reverb;
        }

        public struct LoopMove
        {
            public double Time;
            public string Event;
            public float Level;
            public float Seconds;
            public float GainDb;
            public float Cutoff;
        }

        public struct Trace
        {
            public double Time;
            public float Intensity;
            public IntensityBand Band;
            public string Track;
        }

        public readonly List<MusicCommand> Music = new List<MusicCommand>();
        public readonly List<Sound> Sounds = new List<Sound>();
        public readonly List<LoopMove> Loops = new List<LoopMove>();
        public readonly List<Trace> Traces = new List<Trace>();
        public readonly List<KeyValuePair<double, string>> Captions = new List<KeyValuePair<double, string>>();
        public readonly List<KeyValuePair<double, bool>> Ducks = new List<KeyValuePair<double, bool>>();
        public int Refused;

        public void Run(AudioManifest manifest, List<DemoStep> steps, double step = 0.02, double startClock = 1.0)
        {
            var intensity = new IntensityModel();
            var brain = new MusicBrain(manifest);
            var rewards = new RewardRules();
            var voices = new VoiceAllocator(VoiceAllocator.DefaultVoices);
            var space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
            var commands = new List<MusicCommand>();
            var loopOn = new Dictionary<string, float>();
            brain.SetContext("silence");
            int next = 0;
            double duckUntil = -1;
            bool ducking = false;

            for (double t = 0; t <= DemoTimeline.Length + 1e-9; t += step)
            {
                while (next < steps.Count && steps[next].Time <= t)
                {
                    DemoStep s = steps[next++];
                    switch (s.Kind)
                    {
                        case DemoStepKind.Caption: Captions.Add(new KeyValuePair<double, string>(s.Time, s.Text)); break;
                        case DemoStepKind.Context: brain.SetContext(s.Text); break;
                        case DemoStepKind.Threat: intensity.SetThreat(s.Value); break;
                        case DemoStepKind.Heat: intensity.Report((IntensityEvent)s.Number); break;
                        case DemoStepKind.Space:
                            space = new ListenerSpace { Air = s.Value, Enclosure = (Enclosure)s.Number };
                            // Beds already running take on the new space.
                            foreach (KeyValuePair<string, float> on in new List<KeyValuePair<string, float>>(loopOn)) Loop(manifest, space, t, on.Key, on.Value, 0.5f);
                            break;
                        case DemoStepKind.Reward: rewards.Request((RewardKind)s.Number, t); break;
                        case DemoStepKind.Play: Play(manifest, voices, space, t, s.Text, 1f, s.Value, s.Value2); break;
                        case DemoStepKind.LoopOn:
                        case DemoStepKind.LoopLevel:
                            loopOn[s.Text] = s.Value;
                            Loop(manifest, space, t, s.Text, s.Value, s.Value2);
                            break;
                        case DemoStepKind.LoopOff:
                            loopOn.Remove(s.Text);
                            Loop(manifest, space, t, s.Text, 0f, s.Value2);
                            break;
                    }
                }

                intensity.Tick((float)step);
                if (rewards.Poll(t, out RewardKind kind))
                {
                    string id = RewardRules.EventId(kind);
                    float pitch = 1f;
                    if (kind == RewardKind.PointsTick && manifest.Events.TryGetValue(id, out AudioEventDef def) && def.PitchSteps.Length > 0)
                    {
                        int k = rewards.TickStep < def.PitchSteps.Length ? rewards.TickStep : def.PitchSteps.Length - 1;
                        pitch = (float)System.Math.Pow(2.0, def.PitchSteps[k] / 12.0);
                    }
                    Play(manifest, voices, space, t, id, pitch, 0f, 0f);
                    if (RewardRules.DucksMusic(kind)) duckUntil = t + RewardRules.DuckSeconds(kind);
                }
                bool duck = t < duckUntil;
                if (duck != ducking)
                {
                    ducking = duck;
                    Ducks.Add(new KeyValuePair<double, bool>(t, duck));
                }

                commands.Clear();
                brain.Update(startClock + t, intensity.Value, commands, intensity.Target);
                foreach (MusicCommand c in commands)
                {
                    MusicCommand shifted = c;
                    shifted.Time = c.Time - startClock;
                    Music.Add(shifted);
                }
                if (Traces.Count == 0 || t - Traces[Traces.Count - 1].Time >= 0.25 - 1e-9)
                {
                    Traces.Add(new Trace { Time = t, Intensity = intensity.Value, Band = intensity.Band, Track = brain.Track != null ? brain.Track.Id : "" });
                }
            }
            Refused = voices.Refused;
        }

        void Play(AudioManifest m, VoiceAllocator voices, ListenerSpace space, double t, string id, float pitch, float side, float metres)
        {
            if (!m.Events.TryGetValue(id, out AudioEventDef def)) return;
            if (def.Vacuum != VacuumPolicy.Ui && voices.Request(def, t, 1.0) < 0) return;
            SoundTreatment treat = AcousticsModel.Treat(def.Vacuum, space);
            Sounds.Add(new Sound
            {
                Time = t, Event = id, GainDb = def.VolumeDb + treat.GainDb, Cutoff = treat.Cutoff, Pitch = pitch,
                Side = side, Metres = metres * def.Spatial, Reverb = treat.ReverbSend,
            });
        }

        void Loop(AudioManifest m, ListenerSpace space, double t, string id, float level, float seconds)
        {
            if (!m.Events.TryGetValue(id, out AudioEventDef def)) return;
            SoundTreatment treat = AcousticsModel.Treat(def.Vacuum, space);
            Loops.Add(new LoopMove { Time = t, Event = id, Level = level, Seconds = seconds, GainDb = def.VolumeDb + treat.GainDb, Cutoff = treat.Cutoff });
        }
    }
}
