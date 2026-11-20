using System;

namespace OrbitalAnte.Creatures
{
    /// <summary>Hit points of an Ambusher. A feeler comes off in one rifle hit, a jaw in three.</summary>
    [Serializable]
    public struct AmbusherHp
    {
        /// <summary>One of its two jaws. It needs both to hold on to anyone.</summary>
        public double Jaw;
        /// <summary>One of the five feelers it feels footsteps with.</summary>
        public double Feeler;
        /// <summary>One length of its body. Shot through, the head end comes away and it is dead.</summary>
        public double Seg;
        /// <summary>The armoured head: the only part of it above ground while it waits, and the slow way.</summary>
        public double Head;

        public static AmbusherHp Default => new AmbusherHp { Jaw = 60, Feeler = 15, Seg = 90, Head = 260 };
    }

    /// <summary>What a hit took off. Reused by the next hit: read it before hitting again.</summary>
    public sealed class AmbusherHit
    {
        public int JawLost = -1, FeelerLost = -1;
        /// <summary>-1 for none; otherwise the length of body that was shot through.</summary>
        public int CutAt = -1;
        public bool Killed;
        public bool Lost => JawLost >= 0 || FeelerLost >= 0;
    }

    /// <summary>
    /// What is left of an Ambusher, with no Unity types.
    ///
    /// - It dies when its armoured head gives way, or when any one length of its body is shot through: the head end
    ///   comes away. Damage to a length is remembered, so a cut can be finished the next time it is out of its hole.
    /// - It holds on with two jaws. With one it still bites, but cannot hold or drag; with none it is harmless.
    /// - It feels footsteps with five feelers: every one lost shortens how far off it notices anyone
    ///   (<see cref="FeelShare"/>).
    /// </summary>
    public sealed class AmbusherBody
    {
        readonly double[] _jaw = new double[AmbusherLayout.Jaws];
        readonly double[] _feeler = new double[AmbusherLayout.Feelers];
        readonly double[] _seg = new double[AmbusherLayout.Segs];
        readonly AmbusherHit _hit = new AmbusherHit();

        public double Head { get; private set; }
        public bool Alive { get; private set; } = true;
        /// <summary>-1 while whole; otherwise the length it was shot through at (it is dead).</summary>
        public int CutAt { get; private set; } = -1;

        public AmbusherBody() : this(AmbusherHp.Default) { }

        public AmbusherBody(AmbusherHp hp)
        {
            if (hp.Jaw <= 0 || hp.Feeler <= 0 || hp.Seg <= 0 || hp.Head <= 0) hp = AmbusherHp.Default;
            for (int i = 0; i < _jaw.Length; i++) _jaw[i] = hp.Jaw;
            for (int i = 0; i < _feeler.Length; i++) _feeler[i] = hp.Feeler;
            for (int i = 0; i < _seg.Length; i++) _seg[i] = hp.Seg;
            Head = hp.Head;
        }

        public bool JawOn(int jaw) => jaw >= 0 && jaw < _jaw.Length && _jaw[jaw] > 0.0;
        public bool FeelerOn(int feeler) => feeler >= 0 && feeler < _feeler.Length && _feeler[feeler] > 0.0;
        public double JawHp(int jaw) => Math.Max(0.0, _jaw[jaw]);
        public double FeelerHp(int feeler) => Math.Max(0.0, _feeler[feeler]);
        public double SegHp(int seg) => Math.Max(0.0, _seg[seg]);

        static int Count(double[] of)
        {
            int n = 0;
            for (int i = 0; i < of.Length; i++) if (of[i] > 0.0) n++;
            return n;
        }

        public int JawCount => Count(_jaw);
        public int FeelerCount => Count(_feeler);

        /// <summary>It can hurt with one jaw; it takes both to hold.</summary>
        public bool CanBite => Alive && JawCount >= 1;
        public bool CanHold => Alive && JawCount >= 2;

        /// <summary>The share of its bite it still has: all of it with both jaws, half with one.</summary>
        public double BiteShare => !Alive ? 0.0 : JawCount >= 2 ? 1.0 : JawCount == 1 ? 0.5 : 0.0;

        /// <summary>The share of the distance it feels footsteps from: all of it with five feelers, two fifths with none.</summary>
        public double FeelShare => 0.4 + 0.6 * FeelerCount / AmbusherLayout.Feelers;

        AmbusherHit Fresh()
        {
            _hit.JawLost = _hit.FeelerLost = _hit.CutAt = -1;
            _hit.Killed = false;
            return _hit;
        }

        void Die(AmbusherHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }

        public AmbusherHit HitJaw(int jaw, double damage, bool sever = false)
        {
            AmbusherHit hit = Fresh();
            if (!Alive || !JawOn(jaw)) return hit;
            _jaw[jaw] = sever ? 0.0 : _jaw[jaw] - damage;
            if (_jaw[jaw] <= 0.0) hit.JawLost = jaw;
            return hit;
        }

        public AmbusherHit HitFeeler(int feeler, double damage, bool sever = false)
        {
            AmbusherHit hit = Fresh();
            if (!Alive || !FeelerOn(feeler)) return hit;
            _feeler[feeler] = sever ? 0.0 : _feeler[feeler] - damage;
            if (_feeler[feeler] <= 0.0) hit.FeelerLost = feeler;
            return hit;
        }

        /// <summary>A hit on a length of its body. When one gives way it is cut through there, and that kills it.</summary>
        public AmbusherHit HitSeg(int seg, double damage, bool sever = false, bool lethal = false)
        {
            AmbusherHit hit = Fresh();
            if (!Alive || seg < 0 || seg >= _seg.Length) return hit;
            _seg[seg] = sever ? 0.0 : _seg[seg] - damage;
            if (_seg[seg] <= 0.0)
            {
                hit.CutAt = CutAt = seg;
                Die(hit);
            }
            else if (lethal) Die(hit);
            return hit;
        }

        public AmbusherHit HitHead(double damage, bool lethal = false)
        {
            AmbusherHit hit = Fresh();
            if (!Alive) return hit;
            Head = Math.Max(0.0, Head - damage);
            if (lethal || Head <= 0.0) Die(hit);
            return hit;
        }
    }
}
