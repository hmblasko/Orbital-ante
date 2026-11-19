using System;

namespace OrbitalAnte.Audio
{
    public enum IntensityBand
    {
        Solitude = 0,
        Exploration = 1,
        Tension = 2,
        Combat = 3,
        Chaos = 4,
    }

    /// <summary>Things that happen in the game that make the moment feel more dangerous.</summary>
    public enum IntensityEvent
    {
        PlayerShot,
        EnemyShotNearby,
        HitDealt,
        PartSevered,
        Kill,
        DamageTaken,
        Crash,
        Discovery,
    }

    /// <summary>
    /// Turns what is happening into one number from 0 to 100 that the music follows.
    ///
    /// Two things feed it. "Threat" is the steady danger the game reports (how many enemies know where you are,
    /// a storm overhead). "Heat" comes from events (shots, hits, damage) and cools off after a few quiet seconds.
    /// The number chases whichever is higher, quickly on the way up and slowly on the way down, so the music
    /// reacts to an ambush at once but does not sag the moment a gun stops firing. Each event can only push heat
    /// up to its own ceiling: firing into empty air can make the music tense, never start a battle.
    /// No Unity types, so it can be tested anywhere.
    /// </summary>
    public sealed class IntensityModel
    {
        public float RisePerSecond = 45f;
        public float FallPerSecond = 7f;
        /// <summary>Heat holds this long after the last event before it starts cooling.</summary>
        public float HoldSeconds = 3.5f;
        public float CoolPerSecond = 9f;
        /// <summary>A band must be overshot by this much before the band changes (stops flicker at an edge).</summary>
        public float BandHysteresis = 3f;
        /// <summary>The band will not drop again sooner than this after any band change.</summary>
        public float BandDropCooldown = 4f;

        float _heat;
        float _threat;
        float _sinceEvent = 100f;
        float _sinceBandChange = 100f;
        float _sinceHostile = 100f;

        /// <summary>Without anyone shooting back, the player's own hits and kills can only raise things this far: target practice is tense at most.</summary>
        public const float UnopposedCeiling = 55f;
        /// <summary>How long after the last incoming fire a fight still counts as a fight.</summary>
        public const float HostileMemory = 10f;

        /// <summary>The smoothed intensity, 0 to 100.</summary>
        public float Value { get; private set; }
        public IntensityBand Band { get; private set; }
        public float Heat => _heat;
        public float Threat => _threat;
        /// <summary>Where the intensity is heading (it gets there at RisePerSecond).</summary>
        public float Target => Math.Max(_heat, _threat);

        public static float Add(IntensityEvent e)
        {
            switch (e)
            {
                case IntensityEvent.PlayerShot: return 2.5f;
                case IntensityEvent.EnemyShotNearby: return 9f;
                case IntensityEvent.HitDealt: return 6f;
                case IntensityEvent.PartSevered: return 6f;
                case IntensityEvent.Kill: return 9f;
                case IntensityEvent.DamageTaken: return 16f;
                case IntensityEvent.Crash: return 40f;
                default: return 4f;
            }
        }

        public static float Ceiling(IntensityEvent e)
        {
            switch (e)
            {
                case IntensityEvent.PlayerShot: return 34f;
                case IntensityEvent.EnemyShotNearby: return 78f;
                case IntensityEvent.HitDealt: return 70f;
                case IntensityEvent.PartSevered: return 74f;
                case IntensityEvent.Kill: return 78f;
                case IntensityEvent.DamageTaken: return 94f;
                case IntensityEvent.Crash: return 58f; // a crash is a fright, not a fight
                default: return 30f;
            }
        }

        public void Report(IntensityEvent e, float weight = 1f)
        {
            float ceiling = Ceiling(e);
            // Fire from far off (a low weight) raises the danger a little but does not make this the player's fight.
            if (e == IntensityEvent.DamageTaken || (e == IntensityEvent.EnemyShotNearby && weight >= 0.5f)) _sinceHostile = 0f;
            if (e == IntensityEvent.EnemyShotNearby && weight < 0.5f) ceiling = Math.Min(ceiling, UnopposedCeiling);
            bool mine = e == IntensityEvent.HitDealt || e == IntensityEvent.PartSevered || e == IntensityEvent.Kill;
            if (mine && _sinceHostile > HostileMemory) ceiling = Math.Min(ceiling, UnopposedCeiling);
            if (_heat < ceiling) _heat = Math.Min(ceiling, _heat + Add(e) * Math.Max(0f, weight));
            _sinceEvent = 0f;
        }

        /// <summary>Steady danger, 0 to 100. Stays until changed.</summary>
        public void SetThreat(float threat) => _threat = Clamp(threat, 0f, 100f);

        public void Reset()
        {
            _heat = 0f;
            _threat = 0f;
            Value = 0f;
            Band = IntensityBand.Solitude;
            _sinceEvent = 100f;
            _sinceBandChange = 100f;
            _sinceHostile = 100f;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _sinceEvent += dt;
            _sinceHostile += dt;
            _sinceBandChange += dt;
            if (_sinceEvent > HoldSeconds) _heat = Math.Max(0f, _heat - CoolPerSecond * dt);

            float target = Math.Max(_heat, _threat);
            if (target > Value) Value = Math.Min(target, Value + RisePerSecond * dt);
            else Value = Math.Max(target, Value - FallPerSecond * dt);

            IntensityBand now = BandOf(Value);
            if (now == Band) return;
            if (now > Band)
            {
                if (Value >= LowerEdge(now) + BandHysteresis || now > Band + 1) SetBand(now);
            }
            else if (_sinceBandChange >= BandDropCooldown && Value <= LowerEdge(Band) - BandHysteresis)
            {
                SetBand(now);
            }
        }

        void SetBand(IntensityBand band)
        {
            Band = band;
            _sinceBandChange = 0f;
        }

        public static IntensityBand BandOf(float value)
        {
            if (value >= 80f) return IntensityBand.Chaos;
            if (value >= 60f) return IntensityBand.Combat;
            if (value >= 40f) return IntensityBand.Tension;
            if (value >= 20f) return IntensityBand.Exploration;
            return IntensityBand.Solitude;
        }

        public static float LowerEdge(IntensityBand band) => (int)band * 20f;

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
