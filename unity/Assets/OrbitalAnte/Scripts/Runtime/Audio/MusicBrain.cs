using System;
using System.Collections.Generic;

namespace OrbitalAnte.Audio
{
    public enum MusicCommandKind
    {
        /// <summary>Start every stem of Track together at Time, all silent except as the StemGain commands say. Seconds is a fade-in for the whole piece.</summary>
        StartTrack,
        /// <summary>Fade the whole of Track out, starting at Time and taking Seconds, then stop it.</summary>
        StopTrack,
        /// <summary>Move one stem of Track to Gain (0 or 1), starting at Time and taking Seconds.</summary>
        StemGain,
        /// <summary>Play the transition cue Cue, starting at Time.</summary>
        PlayCue,
        /// <summary>Fade the cue Cue out, starting at Time and taking Seconds, wherever it has got to.</summary>
        StopCue,
    }

    /// <summary>One instruction from the music director to whatever plays the audio. Times are on the audio clock.</summary>
    public struct MusicCommand
    {
        public MusicCommandKind Kind;
        public string Track;
        public string Stem;
        public string Cue;
        public double Time;
        public double Seconds;
        public float Gain;
        /// <summary>StartTrack only: how far into the piece to start, in seconds (always a bar line).</summary>
        public double Offset;

        public override string ToString()
        {
            return Kind + " " + (Track ?? Cue) + (Stem != null ? "/" + Stem : "") + " @" + Time.ToString("0.000") + " over " + Seconds.ToString("0.00") + " -> " + Gain;
        }
    }

    /// <summary>
    /// The music director's decisions, with no Unity types. Each update it is told the audio clock time, the
    /// situation ("surface", "flight", ...) and the intensity, and it answers with commands: start this piece on
    /// that beat, fade that layer in over two bars, play the riser now so it lands on the downbeat.
    ///
    /// Rules it keeps:
    /// - Nothing changes off the grid. Layers come and go on bar lines; pieces hand over on the beat, bar or
    ///   phrase their transition rule asks for, with a cue bridging the two where one is written.
    /// - Combat music takes over above one intensity and lets go only below a lower one, and never before it has
    ///   played for a minimum time, so the music cannot flap between two pieces.
    /// - After a piece has been round a set number of times it rests, and comes back later. Silence is part of
    ///   the score.
    /// Because it only produces commands, the same class drives the game, the tests and the offline preview.
    /// </summary>
    public sealed class MusicBrain
    {
        /// <summary>Scheduling a sound takes a moment; nothing is ever asked to start sooner than this.</summary>
        public const double Lead = 0.3;
        /// <summary>A layer that has just changed stays put for at least this many bars.</summary>
        public const int StemHoldBars = 2;
        const float StemHysteresis = 5f;

        readonly AudioManifest _m;
        string _context = "silence";
        MusicTrackDef _track;
        double _trackStart;
        double _trackEnter;
        readonly Dictionary<string, int> _plays = new Dictionary<string, int>();
        bool[] _stemOn = new bool[0];
        double[] _stemChanged = new double[0];
        bool _combat;
        double _combatSince;
        double _busyUntil;
        double _restUntil = -1;
        bool _afterRest;
        bool _started;
        float _heading;
        double _changeAt;
        double _calmSince = -1;
        string _lastCue = "";
        double _lastCueEnd;

        /// <summary>How long the danger must stay low before a fight counts as over. Without it, danger that comes and goes every few seconds would end and restart the combat music each time.</summary>
        public const double CalmSeconds = 4.0;

        public MusicBrain(AudioManifest manifest)
        {
            _m = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        public string Context => _context;
        /// <summary>The piece playing (or about to), or null for silence.</summary>
        public MusicTrackDef Track => _track;
        /// <summary>Audio clock time at which bar 1 of the current piece falls. It may be in the future, or, for a piece picked up part-way through, earlier than the moment it was started.</summary>
        public double TrackStart => _trackStart;
        public bool InCombat => _combat;
        public bool Resting(double now) => now < _restUntil;

        public void SetContext(string context)
        {
            if (string.IsNullOrEmpty(context) || context == _context) return;
            _context = context;
            // A new situation is a fresh start: no leftover rest from the old one.
            _restUntil = -1;
            _afterRest = false;
        }

        /// <summary>Which bar (from 0) and beat the current piece is on at this time. False if nothing is playing yet.</summary>
        public bool Position(double now, out int bar, out double beat)
        {
            bar = 0;
            beat = 0;
            if (_track == null || now < _trackEnter) return false;
            double t = (now - _trackStart) % _track.Seconds;
            bar = (int)(t / _track.BarSeconds);
            beat = (t - bar * _track.BarSeconds) / _track.BeatSeconds;
            return true;
        }

        public bool StemOn(int index) => index >= 0 && index < _stemOn.Length && _stemOn[index];

        /// <param name="heading">Where the intensity is about to arrive (IntensityModel.Target), if known. Used only to tell an ambush from a slow build.</param>
        public void Update(double now, float intensity, List<MusicCommand> commands, float heading = -1f)
        {
            _heading = Math.Max(intensity, heading);
            ContextDef ctx = _m.Contexts.TryGetValue(_context, out ContextDef c) ? c : null;
            bool allowCombat = ctx == null || ctx.Combat;

            if (!allowCombat || _m.CombatTrack.Length == 0)
            {
                _combat = false;
            }
            else if (!_combat && intensity >= _m.CombatEnter)
            {
                _combat = true;
                _combatSince = now;
            }
            else if (_combat)
            {
                if (intensity >= _m.CombatExit) _calmSince = -1;
                else if (_calmSince < 0) _calmSince = now;
                if (_calmSince >= 0 && now - _calmSince >= CalmSeconds && now - _combatSince >= _m.CombatMinSeconds)
                {
                    _combat = false;
                    _calmSince = -1;
                }
            }

            string home = ctx != null ? ctx.Track : "";
            string want = _combat ? _m.CombatTrack : home;
            if (!_combat && now < _restUntil) want = "";
            if (want.Length > 0 && !_m.Tracks.ContainsKey(want)) want = "";
            string have = _track != null ? _track.Id : "";

            // A fight does not wait its turn: combat may interrupt a hand-over that is still under way (the hush
            // after the last fight, a piece fading in). Flapping is still impossible, because leaving combat has
            // its own minimum time and lower threshold.
            // It does wait for one thing: a hand-over already counted in (the ending on its bar line) happens first,
            // so the old piece is never cut off in the middle of leaving.
            bool urgent = want.Length > 0 && want == _m.CombatTrack && have != want && now >= _changeAt;
            if (now >= _busyUntil || urgent)
            {
                if (want != have)
                {
                    Change(now, want, intensity, commands);
                }
                else if (_track != null && !_combat && ctx != null && ctx.MaxLoops > 0 && ctx.RestSeconds > 0 && _restUntil < now)
                {
                    // Time to rest? Plan it one bar before the end of the last time round, so the piece fades
                    // across its final bars and stops exactly where it would have started over.
                    double end = _trackStart + ctx.MaxLoops * _track.Seconds;
                    double fade = Math.Min(2 * _track.BarSeconds, 6.0);
                    if (now >= end - fade - Lead)
                    {
                        double start = Math.Max(now + Lead, end - fade);
                        commands.Add(new MusicCommand { Kind = MusicCommandKind.StopTrack, Track = _track.Id, Time = start, Seconds = Math.Max(0.05, end - start) });
                        _restUntil = end + ctx.RestSeconds;
                        _afterRest = true;
                        _track = null;
                        _busyUntil = end;
                    }
                }
            }

            UpdateStems(now, intensity, commands);
        }

        void Change(double now, string to, float intensity, List<MusicCommand> commands)
        {
            string from = _track != null ? _track.Id : "";
            TransitionDef rule = _m.FindTransition(from, to) ?? new TransitionDef();
            bool cameFromRest = _afterRest && from.Length == 0;
            _afterRest = false;

            // The cue, if this hand-over has one. A piece returning from a rest slips back in without ceremony,
            // and the very first music after a silent start does not get a riser out of nowhere unless it is combat.
            CueDef cue = null;
            bool ambush = false;
            if (!cameFromRest && to.Length > 0)
            {
                if (rule.AmbushCue.Length > 0 && _heading >= _m.AmbushIntensity && _m.Cues.TryGetValue(rule.AmbushCue, out CueDef a))
                {
                    cue = a;
                    ambush = true;
                }
                else if (rule.Cue.Length > 0) _m.Cues.TryGetValue(rule.Cue, out cue);
            }
            else if (to.Length == 0 && rule.Cue.Length > 0)
            {
                _m.Cues.TryGetValue(rule.Cue, out cue);
            }
            if (!_started && to != _m.CombatTrack) cue = null;
            _started = true;

            double earliest = now + Lead;
            double change;
            double cueStart = 0;
            bool leadIn = cue != null && cue.PreSeconds > 0;
            if (_track == null || now < _trackEnter)
            {
                cueStart = earliest;
                change = leadIn ? cueStart + cue.PreSeconds : earliest;
            }
            else if (leadIn)
            {
                // The riser starts on the next beat of the old piece and lands where it lands: by then the old
                // piece has faded underneath it and the new one starts on the hit.
                cueStart = NextBoundary(earliest, "beat");
                change = cueStart + cue.PreSeconds;
            }
            else
            {
                // No riser to cover the join, so the old piece gets its whole fade and finishes on its own grid.
                double room = ambush ? 0.0 : Math.Min(rule.FadeOut, 8.0);
                change = NextBoundary(Math.Max(earliest, now + room), ambush ? "beat" : rule.Quantize);
                cueStart = change;
            }

            if (_track != null)
            {
                double fade = Math.Max(0.03, Math.Min(rule.FadeOut, change - now));
                double start = Math.Max(now, change - fade);
                commands.Add(new MusicCommand { Kind = MusicCommandKind.StopTrack, Track = _track.Id, Time = start, Seconds = change - start });
            }
            // A cue still ringing from the last hand-over (the ending of a fight that has just flared up again)
            // bows out as the new one begins.
            if (_lastCue.Length > 0 && _lastCueEnd > now && (cue == null || cue.Id != _lastCue || cueStart < _lastCueEnd))
                commands.Add(new MusicCommand { Kind = MusicCommandKind.StopCue, Cue = _lastCue, Time = earliest, Seconds = 0.6 });
            _lastCue = "";
            if (cue != null)
            {
                commands.Add(new MusicCommand { Kind = MusicCommandKind.PlayCue, Cue = cue.Id, Time = cueStart });
                _lastCue = cue.Id;
                _lastCueEnd = cueStart + cue.Seconds;
            }
            _changeAt = change;

            if (to.Length == 0)
            {
                _track = null;
                _stemOn = new bool[0];
                _busyUntil = change + 1.0;
                return;
            }

            MusicTrackDef next = _m.Tracks[to];
            // A hush is a breath between the two; a negative one overlaps them instead.
            double enter = Math.Max(earliest, change + (from.Length > 0 || cue != null ? rule.Hush : 0.0));
            double fadeIn = cameFromRest ? Math.Max(rule.FadeIn, 6.0) : rule.FadeIn;
            // Combat always starts from its top (the riser is written to land there). Other pieces rotate through
            // their entry points, so the fifth return from a fight does not open like the first.
            double offset = 0;
            _plays.TryGetValue(next.Id, out int plays);
            _plays[next.Id] = plays + 1;
            // A piece that arrives on a riser also starts from its top: the riser is written to land on bar 1.
            if (next.Id != _m.CombatTrack && !leadIn && next.EntryBars.Length > 0)
            {
                int bar = next.EntryBars[plays % next.EntryBars.Length];
                if (bar >= 1 && bar <= next.Bars) offset = (bar - 1) * next.BarSeconds;
            }
            commands.Add(new MusicCommand { Kind = MusicCommandKind.StartTrack, Track = next.Id, Time = enter, Seconds = fadeIn, Gain = 1f, Offset = offset });
            _track = next;
            _trackStart = enter - offset;
            _trackEnter = enter;
            _stemOn = new bool[next.Stems.Length];
            _stemChanged = new double[next.Stems.Length];
            // Coming out of a fight the next piece starts from its barest layers and builds again from there,
            // whatever the danger number still reads: the quiet is the reward.
            bool bare = from == _m.CombatTrack && from.Length > 0;
            for (int i = 0; i < next.Stems.Length; i++)
            {
                StemDef s = next.Stems[i];
                _stemOn[i] = intensity >= s.Lo && intensity <= s.Hi + StemHysteresis && (!bare || s.Lo <= 0f);
                _stemChanged[i] = enter;
                commands.Add(new MusicCommand { Kind = MusicCommandKind.StemGain, Track = next.Id, Stem = s.Id, Time = enter, Seconds = 0, Gain = _stemOn[i] ? 1f : 0f });
            }
            // Once a hand-over is planned it is carried through; the next decision waits until the new piece has
            // had a couple of bars, which also keeps two quick changes from tripping over each other.
            _busyUntil = enter + Math.Min(4.0, 2 * next.BarSeconds);
        }

        void UpdateStems(double now, float intensity, List<MusicCommand> commands)
        {
            if (_track == null || now < _trackEnter) return;
            double bar = _track.BarSeconds;
            for (int i = 0; i < _track.Stems.Length; i++)
            {
                StemDef s = _track.Stems[i];
                bool on = _stemOn[i];
                bool want = on
                    ? intensity >= s.Lo - StemHysteresis && intensity <= s.Hi + StemHysteresis
                    : intensity >= s.Lo && intensity <= s.Hi;
                if (want == on) continue;
                if (now - _stemChanged[i] < StemHoldBars * bar) continue;
                double at = NextBoundary(now + Lead, "bar");
                double fade = Math.Max(0.05, (want ? s.FadeInBars : s.FadeOutBars) * bar);
                // A layer with no fade-in enters on the bar line; one with a fade swells towards it instead.
                commands.Add(new MusicCommand { Kind = MusicCommandKind.StemGain, Track = _track.Id, Stem = s.Id, Time = at, Seconds = fade, Gain = want ? 1f : 0f });
                _stemOn[i] = want;
                _stemChanged[i] = at;
            }
        }

        /// <summary>The first grid line of the current piece at or after <paramref name="t"/>.</summary>
        public double NextBoundary(double t, string quantize)
        {
            if (_track == null || quantize == "now") return t;
            double unit = quantize == "beat" ? _track.BeatSeconds : quantize == "phrase" ? _track.BarSeconds * 4 : quantize == "2bars" ? _track.BarSeconds * 2 : _track.BarSeconds;
            if (t <= _trackEnter) return _trackEnter;
            // Count grid lines inside the current time round the loop, so the answer stays on the loop's own grid
            // however long the piece has been playing.
            double into = (t - _trackStart) % _track.Seconds;
            double loopStart = t - into;
            double k = Math.Ceiling(into / unit - 1e-9);
            double at = loopStart + k * unit;
            double loopEnd = loopStart + _track.Seconds;
            return at > loopEnd ? loopEnd : at;
        }
    }
}
