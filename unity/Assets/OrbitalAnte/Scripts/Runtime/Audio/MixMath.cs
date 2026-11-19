using System;
using System.Collections.Generic;

namespace OrbitalAnte.Audio
{
    /// <summary>Small bits of level arithmetic shared by the game and the offline preview. No Unity types.</summary>
    public static class MixMath
    {
        /// <summary>
        /// Room left at the top of the whole mix. Unity's output clips rather than limits, and the loudest moment
        /// measured in the showcase (gunfire over full combat music) peaks 0.4 dB under full scale with none; this
        /// puts that moment at about -4.4 dB so busier fights than the showcase still have somewhere to go.
        /// </summary>
        public const float HeadroomDb = -4f;

        public static float DbToGain(float db) => db <= -80f ? 0f : (float)Math.Pow(10.0, db / 20.0);

        /// <summary>
        /// A volume slider (0..1) to a gain. Loudness is heard in ratios, so the slider is given a curve: half
        /// way sounds about half as loud (-12 dB), not barely different. 0 is silent, 1 is full.
        /// </summary>
        public static float SliderToGain(float slider)
        {
            if (slider <= 0.0001f) return 0f;
            if (slider >= 1f) return 1f;
            return slider * slider;
        }

        // ---- slow motion: the world's sounds sink and dull, the player's own head (suit, interface) does not

        /// <summary>How slowed the world is, 0 to 1, from the game's time scale. A frozen world (pause, a hitstop
        /// frame) is not slow motion, and 1 is the deepest slow the game uses (about a third of normal speed).</summary>
        public static float SlowAmount(float timeScale)
        {
            if (timeScale <= 0.05f || timeScale >= 0.99f) return 0f;
            float a = (1f - timeScale) / 0.65f;
            return a < 0f ? 0f : a > 1f ? 1f : a;
        }

        /// <summary>World sounds drop in pitch, but less than time itself does, so a gunshot stays a gunshot.</summary>
        public static float SlowPitch(float amount) => 1f - 0.3f * Clamp01(amount);

        /// <summary>World sounds lose their top: 20 kHz at normal speed down to 3.2 kHz fully slowed.</summary>
        public static float SlowCutoff(float amount) => 20000f * (float)Math.Pow(3200.0 / 20000.0, Clamp01(amount));

        /// <summary>Music keeps its tempo (it is on the audio clock, not the game's) and steps back instead.</summary>
        public static float SlowMusicGain(float amount) => 1f - 0.35f * Clamp01(amount);

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>An equal-power fade shape: two sounds crossing over keep a steady overall loudness.</summary>
        public static float FadeCurve(float linear)
        {
            if (linear <= 0f) return 0f;
            if (linear >= 1f) return 1f;
            return (float)Math.Sin(linear * Math.PI * 0.5);
        }

        /// <summary>Which of the player's volume sliders a bus answers to (besides master).</summary>
        public static AudioVolume SliderFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return AudioVolume.Music;
                case AudioBus.Dialogue: return AudioVolume.Dialogue;
                default: return AudioVolume.Effects;
            }
        }
    }

    public enum AudioVolume
    {
        Master,
        Music,
        Effects,
        Dialogue,
    }

    /// <summary>
    /// A level that moves in scheduled straight lines: "at time T, head for G over S seconds". Used for every
    /// music fade, so a fade asked for ahead of time starts exactly when it should. Ask for values with times
    /// that never go backwards. No Unity types.
    /// </summary>
    public sealed class GainLine
    {
        struct Move
        {
            public double Time;
            public double Seconds;
            public float Target;
        }

        readonly List<Move> _moves = new List<Move>();
        float _value;
        float _from;
        float _to;
        double _start;
        double _seconds;
        bool _moving;

        public float Value => _value;
        public bool Idle => !_moving && _moves.Count == 0;

        public void Reset(float value)
        {
            _moves.Clear();
            _moving = false;
            _value = value;
        }

        public void Add(double time, double seconds, float target)
        {
            // A new instruction replaces anything that had been planned for later than it.
            for (int i = _moves.Count - 1; i >= 0; i--) if (_moves[i].Time >= time) _moves.RemoveAt(i);
            _moves.Add(new Move { Time = time, Seconds = Math.Max(0, seconds), Target = target });
        }

        public float At(double now)
        {
            while (_moves.Count > 0 && _moves[0].Time <= now)
            {
                Move m = _moves[0];
                _moves.RemoveAt(0);
                // Start from wherever the level had got to at that moment.
                _from = Evaluate(m.Time);
                _to = m.Target;
                _start = m.Time;
                _seconds = m.Seconds;
                _moving = true;
            }
            _value = Evaluate(now);
            if (_moving && now >= _start + _seconds) _moving = false;
            return _value;
        }

        float Evaluate(double t)
        {
            if (!_moving) return _value;
            if (_seconds <= 0 || t >= _start + _seconds) return _to;
            if (t <= _start) return _from;
            return _from + (_to - _from) * (float)((t - _start) / _seconds);
        }
    }
}
