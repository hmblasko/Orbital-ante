using System;

namespace OrbitalAnte.Creatures
{
    [Serializable]
    public struct StalkerHp
    {
        /// <summary>One tuft of gills: what it hears with.</summary>
        public double Gill;
        public double Leg;
        /// <summary>One length of tail: shot through, the tail comes off there.</summary>
        public double TailSeg;
        /// <summary>The whole trunk, one number: soft, it bleeds.</summary>
        public double Trunk;
        /// <summary>The skull: it knocks.</summary>
        public double Head;

        public static StalkerHp Default => new StalkerHp { Gill = 24, Leg = 45, TailSeg = 60, Trunk = 200, Head = 280 };
    }

    public struct StalkerHit
    {
        public int GillLost, LegLost;
        /// <summary>The tail came off at this length (everything from it back).</summary>
        public int TailCutAt;
        public bool Killed;
        public bool Lost => GillLost >= 0 || LegLost >= 0 || TailCutAt >= 0;
        public static StalkerHit None => new StalkerHit { GillLost = -1, LegLost = -1, TailCutAt = -1 };
    }

    /// <summary>
    /// What a Blind stalker has left and what that lets it do. Plain C#, no Unity types.
    ///
    /// - A gill tuft is an ear: with one it hears less far, with none hardly at all (but close up it still feels a
    ///   suit's electrics through its snout, which nothing takes away).
    /// - A leg lost slows it; with fewer than two it swims along the ground on its belly.
    /// - The tail drives its lunge: cut short, it lunges less far.
    /// - It dies when its trunk or its skull gives way.
    /// </summary>
    public sealed class StalkerBody
    {
        readonly double[] _gill = new double[StalkerLayout.Gills];
        readonly double[] _leg = new double[StalkerLayout.Legs];
        readonly double[] _tail = new double[StalkerLayout.TailSegs];

        public double Trunk { get; private set; }
        public double Head { get; private set; }
        public bool Alive { get; private set; } = true;
        /// <summary>How many lengths it still has, trunk and tail together.</summary>
        public int Kept { get; private set; } = StalkerLayout.Segs;

        public StalkerBody() : this(StalkerHp.Default) { }

        public StalkerBody(StalkerHp hp)
        {
            for (int i = 0; i < _gill.Length; i++) _gill[i] = hp.Gill;
            for (int i = 0; i < _leg.Length; i++) _leg[i] = hp.Leg;
            for (int i = 0; i < _tail.Length; i++) _tail[i] = hp.TailSeg;
            Trunk = hp.Trunk;
            Head = hp.Head;
        }

        public bool GillOn(int gill) => gill >= 0 && gill < _gill.Length && _gill[gill] > 0.0;
        public bool LegOn(int leg) => leg >= 0 && leg < _leg.Length && _leg[leg] > 0.0;
        public bool SegOn(int seg) => seg >= 0 && seg < Kept;
        public int TailKept => Kept - StalkerLayout.Trunk;

        static int Count(double[] hp)
        {
            int n = 0;
            for (int i = 0; i < hp.Length; i++) if (hp[i] > 0.0) n++;
            return n;
        }

        public int GillCount => Count(_gill);
        public int LegCount => Count(_leg);

        /// <summary>How far it hears beside one with both gills.</summary>
        public double HearShare => GillCount >= 2 ? 1.0 : GillCount == 1 ? 0.6 : 0.2;
        /// <summary>It walks on two legs or more; on fewer it swims along the ground.</summary>
        public bool Walks => LegCount >= 2;
        /// <summary>How fast it goes beside one with every leg.</summary>
        public double PaceShare => LegCount >= 4 ? 1.0 : LegCount == 3 ? 0.82 : LegCount == 2 ? 0.64 : 0.42;
        /// <summary>How far it lunges beside one with its whole tail.</summary>
        public double LungeShare => 0.55 + 0.45 * TailKept / StalkerLayout.TailSegs;

        public StalkerHit HitGill(int gill, double damage, bool sever = false)
        {
            StalkerHit hit = StalkerHit.None;
            if (!Alive || !GillOn(gill)) return hit;
            _gill[gill] -= sever ? _gill[gill] : Math.Max(0.0, damage);
            if (_gill[gill] <= 0.0) hit.GillLost = gill;
            return hit;
        }

        public StalkerHit HitLeg(int leg, double damage, bool sever = false)
        {
            StalkerHit hit = StalkerHit.None;
            if (!Alive || !LegOn(leg)) return hit;
            _leg[leg] -= sever ? _leg[leg] : Math.Max(0.0, damage);
            if (_leg[leg] <= 0.0) hit.LegLost = leg;
            return hit;
        }

        /// <summary>A hit on length <paramref name="seg"/>: the trunk's one number, or a length of tail, which comes off there.</summary>
        public StalkerHit HitSeg(int seg, double damage, bool sever = false, bool lethal = false)
        {
            StalkerHit hit = StalkerHit.None;
            if (!Alive || !SegOn(seg)) return hit;
            if (!StalkerLayout.IsTail(seg))
            {
                Trunk -= lethal ? Trunk : Math.Max(0.0, damage);
                if (Trunk <= 0.0) Die(ref hit);
                return hit;
            }
            int k = seg - StalkerLayout.Trunk;
            _tail[k] -= sever ? _tail[k] : Math.Max(0.0, damage);
            if (_tail[k] <= 0.0)
            {
                hit.TailCutAt = seg;
                Kept = seg;
            }
            return hit;
        }

        public StalkerHit HitHead(double damage, bool lethal = false)
        {
            StalkerHit hit = StalkerHit.None;
            if (!Alive) return hit;
            Head -= lethal ? Head : Math.Max(0.0, damage);
            if (Head <= 0.0) Die(ref hit);
            return hit;
        }

        void Die(ref StalkerHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }
    }
}
