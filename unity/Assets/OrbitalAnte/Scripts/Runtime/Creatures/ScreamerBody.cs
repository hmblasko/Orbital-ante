using System;

namespace OrbitalAnte.Creatures
{
    /// <summary>Hit points of a Screamer. A wing or a leg comes off in two rifle hits, a drum in three.</summary>
    [Serializable]
    public struct ScreamerHp
    {
        public double ForeWing, HindWing, Leg;
        /// <summary>One of the two drums on its flanks that make the scream.</summary>
        public double Drum;
        /// <summary>The hollow abdomen, its sounding box. Soft: the quick way to kill it.</summary>
        public double Abdomen;
        /// <summary>The armoured fore body and head: the slow way.</summary>
        public double Body;

        public static ScreamerHp Default => new ScreamerHp { ForeWing = 40, HindWing = 30, Leg = 30, Drum = 70, Abdomen = 150, Body = 300 };
    }

    /// <summary>What a hit took off. Reused by the next hit: read it before hitting again.</summary>
    public sealed class ScreamerHit
    {
        public int WingLost = -1, LegLost = -1, DrumLost = -1;
        public bool Killed;
        public bool Lost => WingLost >= 0 || LegLost >= 0 || DrumLost >= 0;
    }

    /// <summary>
    /// What is left of a Screamer, with no Unity types.
    ///
    /// - It dies only when its abdomen (soft) or its armoured fore body gives way.
    /// - Its scream is made by two drums. With one it screams weaker and less far (<see cref="Voice"/>); with none it
    ///   is silent for good.
    /// - It flies on three wings or four (slower on three); with fewer it is on the ground for good.
    /// - It walks on three legs or more, slower for each one lost; with fewer it lies on its belly.
    /// </summary>
    public sealed class ScreamerBody
    {
        public const int WingsToFly = 3, LegsToWalk = 3;

        readonly double[] _wing = new double[ScreamerLayout.Wings];
        readonly double[] _leg = new double[ScreamerLayout.Legs];
        readonly double[] _drum = new double[ScreamerLayout.Drums];
        readonly ScreamerHit _hit = new ScreamerHit();

        public double Abdomen { get; private set; }
        public double Body { get; private set; }
        public bool Alive { get; private set; } = true;

        public ScreamerBody() : this(ScreamerHp.Default) { }

        public ScreamerBody(ScreamerHp hp)
        {
            if (hp.ForeWing <= 0 || hp.HindWing <= 0 || hp.Leg <= 0 || hp.Drum <= 0 || hp.Abdomen <= 0 || hp.Body <= 0) hp = ScreamerHp.Default;
            for (int i = 0; i < _wing.Length; i++) _wing[i] = ScreamerLayout.IsHind(i) ? hp.HindWing : hp.ForeWing;
            for (int i = 0; i < _leg.Length; i++) _leg[i] = hp.Leg;
            for (int i = 0; i < _drum.Length; i++) _drum[i] = hp.Drum;
            Abdomen = hp.Abdomen;
            Body = hp.Body;
        }

        public bool WingOn(int wing) => wing >= 0 && wing < _wing.Length && _wing[wing] > 0.0;
        public bool LegOn(int leg) => leg >= 0 && leg < _leg.Length && _leg[leg] > 0.0;
        public bool DrumOn(int drum) => drum >= 0 && drum < _drum.Length && _drum[drum] > 0.0;
        public double WingHp(int wing) => Math.Max(0.0, _wing[wing]);
        public double LegHp(int leg) => Math.Max(0.0, _leg[leg]);
        public double DrumHp(int drum) => Math.Max(0.0, _drum[drum]);

        static int Count(double[] of)
        {
            int n = 0;
            for (int i = 0; i < of.Length; i++) if (of[i] > 0.0) n++;
            return n;
        }

        public int WingCount => Count(_wing);
        public int LegCount => Count(_leg);
        public int DrumCount => Count(_drum);

        public bool CanFly => Alive && WingCount >= WingsToFly;
        public bool CanWalk => Alive && LegCount >= LegsToWalk;
        public bool CanScream => Alive && DrumCount > 0;

        /// <summary>How strong its scream is: 1 with both drums, 0.6 with one, 0 with none.</summary>
        public double Voice => !Alive ? 0.0 : DrumCount >= 2 ? 1.0 : DrumCount == 1 ? 0.6 : 0.0;

        /// <summary>The share of its flying pace it still has: all of it on four wings, three quarters on three.</summary>
        public double FlightShare => !CanFly ? 0.0 : WingCount >= 4 ? 1.0 : 0.75;

        /// <summary>The share of its walking pace it still has: by the legs left.</summary>
        public double WalkShare => !CanWalk ? 0.0 : LegCount / (double)ScreamerLayout.Legs;

        ScreamerHit Fresh()
        {
            _hit.WingLost = _hit.LegLost = _hit.DrumLost = -1;
            _hit.Killed = false;
            return _hit;
        }

        void Die(ScreamerHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }

        public ScreamerHit HitWing(int wing, double damage, bool sever = false)
        {
            ScreamerHit hit = Fresh();
            if (!Alive || !WingOn(wing)) return hit;
            _wing[wing] = sever ? 0.0 : _wing[wing] - damage;
            if (_wing[wing] <= 0.0) hit.WingLost = wing;
            return hit;
        }

        public ScreamerHit HitLeg(int leg, double damage, bool sever = false)
        {
            ScreamerHit hit = Fresh();
            if (!Alive || !LegOn(leg)) return hit;
            _leg[leg] = sever ? 0.0 : _leg[leg] - damage;
            if (_leg[leg] <= 0.0) hit.LegLost = leg;
            return hit;
        }

        public ScreamerHit HitDrum(int drum, double damage, bool sever = false)
        {
            ScreamerHit hit = Fresh();
            if (!Alive || !DrumOn(drum)) return hit;
            _drum[drum] = sever ? 0.0 : _drum[drum] - damage;
            if (_drum[drum] <= 0.0) hit.DrumLost = drum;
            return hit;
        }

        public ScreamerHit HitAbdomen(double damage, bool lethal = false)
        {
            ScreamerHit hit = Fresh();
            if (!Alive) return hit;
            Abdomen = Math.Max(0.0, Abdomen - damage);
            if (lethal || Abdomen <= 0.0) Die(hit);
            return hit;
        }

        public ScreamerHit HitBody(double damage, bool lethal = false)
        {
            ScreamerHit hit = Fresh();
            if (!Alive) return hit;
            Body = Math.Max(0.0, Body - damage);
            if (lethal || Body <= 0.0) Die(hit);
            return hit;
        }
    }
}
