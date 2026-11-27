using System;

namespace OrbitalAnte.Creatures
{
    /// <summary>Hit points of a Cave hunter. A leg comes off in one rifle hit; a segment is cut through in one near the tail, three near the front.</summary>
    [Serializable]
    public struct HunterHp
    {
        public double Leg;
        /// <summary>
        /// A segment in the middle of the body: when one gives way the body is cut in two there. The nearer the head
        /// the tougher (<see cref="HunterBody.SegShare"/>), so a short cut near the tail is cheap and a deep one dear.
        /// </summary>
        public double Seg;
        /// <summary>The armoured head: small, and the slow way to kill it.</summary>
        public double Head;
        /// <summary>The fore body: the three segments behind the head, as one. Soft: the quick way to kill it.</summary>
        public double Fore;

        public static HunterHp Default => new HunterHp { Leg = 20, Seg = 60, Head = 220, Fore = 150 };
    }

    /// <summary>What a hit took off. Reused by the next hit: read it before hitting again.</summary>
    public sealed class HunterHit
    {
        public int LegLost = -1;
        /// <summary>-1 for none; otherwise the first segment of the length that came away.</summary>
        public int FromSeg = -1;
        /// <summary>How many segments it had before the hit.</summary>
        public int HadSegs;
        public bool Killed;
    }

    /// <summary>
    /// What is left of a Cave hunter, with no Unity types: a head and a row of segments.
    ///
    /// - It dies only when its head or its fore body (the first <see cref="ForeSegs"/> segments, which share one
    ///   number) gives way.
    /// - Any segment further back that gives way cuts it in two there: the head end lives on, shorter, and everything
    ///   from the cut back comes away. Damage to a segment is remembered, so a cut can be finished later.
    /// - Shorter, it rears lower and reaches less far, a little for every segment lost (<see cref="LengthShare"/>),
    ///   but turns quicker. A cut never kills.
    /// - Legs come off one at a time; fewer on what is left make it slower (<see cref="SpeedShare"/>).
    /// </summary>
    public sealed class HunterBody
    {
        public const int ForeSegs = 3;

        readonly double[] _seg = new double[HunterLayout.Segs];
        readonly double[] _leg = new double[HunterLayout.Legs];
        readonly HunterHit _hit = new HunterHit();

        public double Head { get; private set; }
        public double Fore { get; private set; }
        public bool Alive { get; private set; } = true;
        /// <summary>How many segments are still joined to the head.</summary>
        public int Kept { get; private set; } = HunterLayout.Segs;

        public HunterBody() : this(HunterHp.Default) { }

        public HunterBody(HunterHp hp)
        {
            if (hp.Head <= 0 || hp.Fore <= 0 || hp.Seg <= 0 || hp.Leg <= 0) hp = HunterHp.Default;
            for (int i = 0; i < _seg.Length; i++) _seg[i] = hp.Seg * SegShare(i);
            for (int i = 0; i < _leg.Length; i++) _leg[i] = hp.Leg;
            Head = hp.Head;
            Fore = hp.Fore;
        }

        /// <summary>How tough a segment is beside the middle one: 1.2 just behind the fore body, 0.4 at the tail.</summary>
        public static double SegShare(int seg) => 0.4 + 0.8 * (HunterLayout.Segs - 1 - Math.Max(ForeSegs, seg)) / (HunterLayout.Segs - 1.0 - ForeSegs);

        public bool SegOn(int seg) => seg >= 0 && seg < Kept;
        public bool LegOn(int leg) => leg >= 0 && leg < _leg.Length && HunterLayout.SegOf(leg) < Kept && _leg[leg] > 0.0;
        public double SegHp(int seg) => seg < ForeSegs ? Fore : Math.Max(0.0, _seg[seg]);
        public double LegHp(int leg) => Math.Max(0.0, _leg[leg]);
        /// <summary>The two long trailing legs go with the last segment.</summary>
        public bool HasTail => Kept == HunterLayout.Segs;

        /// <summary>Legs still on the part that is joined to the head.</summary>
        public int Legs
        {
            get
            {
                int n = 0;
                for (int leg = 0; leg < Kept * 2; leg++) if (_leg[leg] > 0.0) n++;
                return n;
            }
        }

        /// <summary>The share of its pace it still has: by the legs left on what is left of it. With none it still wriggles.</summary>
        public double SpeedShare => 0.5 + 0.5 * Legs / (Kept * 2.0);

        /// <summary>How many segments it lifts off the ground to strike: a third of what it has, five at most, never fewer than two (the warning must still show).</summary>
        public int RearSegs => Math.Min(5, Math.Max(2, (int)Math.Round(Kept / 3.0)));

        /// <summary>How much of it is left behind the fore body: 1 whole, 0 cut right down.</summary>
        public double LengthShare => (Kept - ForeSegs) / (double)(HunterLayout.Segs - ForeSegs);

        HunterHit Fresh()
        {
            _hit.LegLost = -1;
            _hit.FromSeg = -1;
            _hit.HadSegs = Kept;
            _hit.Killed = false;
            return _hit;
        }

        void Die(HunterHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }

        public HunterHit HitLeg(int leg, double damage, bool sever = false)
        {
            HunterHit hit = Fresh();
            if (!Alive || !LegOn(leg)) return hit;
            _leg[leg] = sever ? 0.0 : _leg[leg] - damage;
            if (_leg[leg] <= 0.0) hit.LegLost = leg;
            return hit;
        }

        /// <summary>A hit on a segment. On the fore body it only ever hurts; behind it, it can cut (a cut never kills).</summary>
        public HunterHit HitSeg(int seg, double damage, bool sever = false, bool lethal = false)
        {
            HunterHit hit = Fresh();
            if (!Alive || !SegOn(seg)) return hit;
            if (seg < ForeSegs)
            {
                Fore = Math.Max(0.0, Fore - damage);
                if (Fore <= 0.0) Die(hit);
            }
            else
            {
                _seg[seg] = sever ? 0.0 : _seg[seg] - damage;
                if (_seg[seg] <= 0.0)
                {
                    hit.FromSeg = seg;
                    Kept = seg;
                }
            }
            if (lethal && Alive) Die(hit);
            return hit;
        }

        public HunterHit HitHead(double damage, bool lethal = false)
        {
            HunterHit hit = Fresh();
            if (!Alive) return hit;
            Head = Math.Max(0.0, Head - damage);
            if (lethal || Head <= 0.0) Die(hit);
            return hit;
        }
    }
}
