using System;

namespace OrbitalAnte.Creatures
{
    [Serializable]
    public struct BiterHp
    {
        public double Fin;
        public double BackFin;
        /// <summary>The forked tail: what gives it its dart.</summary>
        public double TailFin;
        /// <summary>The lower jaw and its teeth: without it it cannot bite.</summary>
        public double Jaw;
        /// <summary>The whole body behind the head, one number.</summary>
        public double Trunk;
        /// <summary>The bony head.</summary>
        public double Head;

        public static BiterHp Default => new BiterHp { Fin = 20, BackFin = 20, TailFin = 60, Jaw = 45, Trunk = 80, Head = 65 };
    }

    public struct BiterHit
    {
        public int FinLost;
        public bool BackFinLost, TailFinLost, JawLost, Killed;
        public bool Lost => FinLost >= 0 || BackFinLost || TailFinLost || JawLost;
        public static BiterHit None => new BiterHit { FinLost = -1 };
    }

    /// <summary>
    /// What a Pack biter has left and what that lets it do. Plain C#, no Unity types.
    ///
    /// - A front fin steers: with one it turns slower and lists; with neither it hardly turns.
    /// - The back fin keeps it steady: without it it rolls as it swims.
    /// - The tail fin drives it: without it it is slow and cannot dart, so it leaves the pack's turns.
    /// - The lower jaw carries its teeth: without it it cannot bite and makes off.
    /// - It dies when its body or its head gives way. Each is a few rifle hits: one fish is cheap, the pack is the danger.
    /// </summary>
    public sealed class BiterBody
    {
        readonly double[] _fin = new double[BiterLayout.Fins];
        double _backFin, _tailFin, _jaw;

        public double Trunk { get; private set; }
        public double Head { get; private set; }
        public bool Alive { get; private set; } = true;

        public BiterBody() : this(BiterHp.Default) { }

        public BiterBody(BiterHp hp)
        {
            // An old config asset made before the biter has every number at zero.
            if (hp.Fin <= 0 || hp.BackFin <= 0 || hp.TailFin <= 0 || hp.Jaw <= 0 || hp.Trunk <= 0 || hp.Head <= 0) hp = BiterHp.Default;
            for (int i = 0; i < _fin.Length; i++) _fin[i] = hp.Fin;
            _backFin = hp.BackFin;
            _tailFin = hp.TailFin;
            _jaw = hp.Jaw;
            Trunk = hp.Trunk;
            Head = hp.Head;
        }

        public bool FinOn(int side) => side >= 0 && side < _fin.Length && _fin[side] > 0.0;
        public bool BackFinOn => _backFin > 0.0;
        public bool TailFinOn => _tailFin > 0.0;
        public bool JawOn => _jaw > 0.0;
        public int FinCount => (FinOn(0) ? 1 : 0) + (FinOn(1) ? 1 : 0);

        public double TurnShare => (FinCount >= 2 ? 1.0 : FinCount == 1 ? 0.6 : 0.3) * (BackFinOn ? 1.0 : 0.85);
        public double PaceShare => TailFinOn ? 1.0 : 0.4;
        /// <summary>It can take its turn at a dart: it needs its tail to dart and its jaw to bite.</summary>
        public bool CanStrike => TailFinOn && JawOn;
        public double List => FinCount == 1 ? (FinOn(BiterLayout.RightSide) ? -1.0 : 1.0) : 0.0;

        public BiterHit HitFin(int side, double damage, bool sever = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive || !FinOn(side)) return hit;
            _fin[side] -= sever ? _fin[side] : Math.Max(0.0, damage);
            if (_fin[side] <= 0.0) hit.FinLost = side;
            return hit;
        }

        public BiterHit HitBackFin(double damage, bool sever = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive || !BackFinOn) return hit;
            _backFin -= sever ? _backFin : Math.Max(0.0, damage);
            hit.BackFinLost = _backFin <= 0.0;
            return hit;
        }

        public BiterHit HitTailFin(double damage, bool sever = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive || !TailFinOn) return hit;
            _tailFin -= sever ? _tailFin : Math.Max(0.0, damage);
            hit.TailFinLost = _tailFin <= 0.0;
            return hit;
        }

        public BiterHit HitJaw(double damage, bool sever = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive || !JawOn) return hit;
            _jaw -= sever ? _jaw : Math.Max(0.0, damage);
            hit.JawLost = _jaw <= 0.0;
            return hit;
        }

        public BiterHit HitTrunk(double damage, bool lethal = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive) return hit;
            Trunk -= lethal ? Trunk : Math.Max(0.0, damage);
            if (Trunk <= 0.0) Die(ref hit);
            return hit;
        }

        public BiterHit HitHead(double damage, bool lethal = false)
        {
            BiterHit hit = BiterHit.None;
            if (!Alive) return hit;
            Head -= lethal ? Head : Math.Max(0.0, damage);
            if (Head <= 0.0) Die(ref hit);
            return hit;
        }

        void Die(ref BiterHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }
    }
}
