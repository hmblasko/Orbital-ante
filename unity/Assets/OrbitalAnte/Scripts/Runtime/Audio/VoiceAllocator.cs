using System;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// Decides whether a one-shot sound may play and which voice plays it, with no Unity types.
    /// A fixed number of voices is the Steam Deck budget: when they are all busy, the least important and
    /// oldest sound gives its voice up; a sound less important than everything playing is simply not played.
    /// Each event also has its own limit and cooldown, so thirty shell-less ricochets cannot eat the mix.
    /// </summary>
    public sealed class VoiceAllocator
    {
        /// <summary>The game's voice budget for one-shot sounds. See docs/performance_budget.md.</summary>
        public const int DefaultVoices = 24;

        struct Voice
        {
            public string Event;
            public int Priority;
            public double Start;
            public double End;
        }

        readonly Voice[] _voices;
        readonly System.Collections.Generic.Dictionary<string, double> _last = new System.Collections.Generic.Dictionary<string, double>();

        public VoiceAllocator(int voices)
        {
            _voices = new Voice[Math.Max(1, voices)];
        }

        public int Count => _voices.Length;
        public int Refused { get; private set; }
        public int Stolen { get; private set; }

        public int Active(double now)
        {
            int n = 0;
            for (int i = 0; i < _voices.Length; i++) if (_voices[i].Event != null && _voices[i].End > now) n++;
            return n;
        }

        /// <summary>Returns the voice to use, or -1 if the sound should not play.</summary>
        public int Request(AudioEventDef def, double now, double seconds)
        {
            if (def == null) return -1;
            if (def.CooldownMs > 0 && _last.TryGetValue(def.Id, out double last) && now - last < def.CooldownMs / 1000.0)
            {
                Refused++;
                return -1;
            }

            int free = -1, same = 0, oldestSame = -1, victim = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                Voice v = _voices[i];
                bool busy = v.Event != null && v.End > now;
                if (!busy)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (v.Event == def.Id)
                {
                    same++;
                    if (oldestSame < 0 || v.Start < _voices[oldestSame].Start) oldestSame = i;
                }
                // Unity's convention: a bigger priority number is less important.
                if (victim < 0 || v.Priority > _voices[victim].Priority || (v.Priority == _voices[victim].Priority && v.Start < _voices[victim].Start)) victim = i;
            }

            int slot;
            if (same >= Math.Max(1, def.MaxVoices))
            {
                slot = oldestSame; // the newest shot replaces the oldest tail of the same sound
                Stolen++;
            }
            else if (free >= 0)
            {
                slot = free;
            }
            else if (victim >= 0 && _voices[victim].Priority >= def.Priority)
            {
                slot = victim;
                Stolen++;
            }
            else
            {
                Refused++;
                return -1;
            }

            _voices[slot] = new Voice { Event = def.Id, Priority = def.Priority, Start = now, End = now + Math.Max(0.02, seconds) };
            _last[def.Id] = now;
            return slot;
        }

        public void Release(int slot)
        {
            if (slot >= 0 && slot < _voices.Length) _voices[slot].Event = null;
        }

        public void Clear()
        {
            for (int i = 0; i < _voices.Length; i++) _voices[i].Event = null;
            _last.Clear();
        }
    }
}
