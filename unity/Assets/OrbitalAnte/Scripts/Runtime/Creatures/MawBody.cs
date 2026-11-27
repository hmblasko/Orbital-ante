using System;

namespace OrbitalAnte.Creatures
{
    [Serializable]
    public struct MawHp
    {
        /// <summary>One front fin: what it steers with.</summary>
        public double Fin;
        public double BackFin;
        /// <summary>The tail fin: what drives it.</summary>
        public double TailFin;
        /// <summary>The lower jaw: without it its mouth cannot seal or bite.</summary>
        public double Jaw;
        /// <summary>The whole body behind the head, one number: soft, it bleeds.</summary>
        public double Trunk;
        /// <summary>The skull: it knocks.</summary>
        public double Head;

        public static MawHp Default => new MawHp { Fin = 60, BackFin = 45, TailFin = 90, Jaw = 140, Trunk = 320, Head = 420 };
    }

    public struct MawHit
    {
        public int FinLost;
        public bool BackFinLost, TailFinLost, JawLost, Killed;
        /// <summary>Damage that went down its throat with this hit (the brain counts it toward a choke).</summary>
        public double Throat;
        public bool Lost => FinLost >= 0 || BackFinLost || TailFinLost || JawLost;
        public static MawHit None => new MawHit { FinLost = -1 };
    }

    /// <summary>
    /// What a Drifting maw has left and what that lets it do. Plain C#, no Unity types.
    ///
    /// - A front fin steers: with one it turns slower and lists to the side that has none; with neither it hardly turns.
    /// - The back fin keeps it steady: without it it rolls as it swims and turns a little worse.
    /// - The tail fin drives it: without it it is slow.
    /// - The lower jaw seals its mouth: without it its pull is weak, it cannot hold or bite, and it makes off.
    /// - A shot down its open throat goes to its soft body at <see cref="ThroatTimes"/> the damage: the way to kill
    ///   it quickly is to face its pull.
    /// - It dies when its body or its skull gives way.
    /// </summary>
    public sealed class MawBody
    {
        /// <summary>How many times the damage a shot down its open throat does to its body.</summary>
        public const double ThroatTimes = 2.5;

        readonly double[] _fin = new double[MawLayout.Fins];
        double _backFin, _tailFin, _jaw;

        public double Trunk { get; private set; }
        public double Head { get; private set; }
        public bool Alive { get; private set; } = true;

        public MawBody() : this(MawHp.Default) { }

        public MawBody(MawHp hp)
        {
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

        /// <summary>How quickly it turns beside one with every fin.</summary>
        public double TurnShare => (FinCount >= 2 ? 1.0 : FinCount == 1 ? 0.6 : 0.3) * (BackFinOn ? 1.0 : 0.9);
        /// <summary>How fast it swims beside one with its tail fin.</summary>
        public double PaceShare => TailFinOn ? 1.0 : 0.45;
        /// <summary>How hard it pulls beside one with its jaw.</summary>
        public double PullShare => JawOn ? 1.0 : 0.4;
        /// <summary>It can take hold of what it pulls in, and bite.</summary>
        public bool Bites => JawOn;
        /// <summary>Which way it lists: +1 to its right, -1 to its left, 0 level.</summary>
        public double List => FinCount == 1 ? (FinOn(MawLayout.RightSide) ? -1.0 : 1.0) : 0.0;

        public MawHit HitFin(int side, double damage, bool sever = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive || !FinOn(side)) return hit;
            _fin[side] -= sever ? _fin[side] : Math.Max(0.0, damage);
            if (_fin[side] <= 0.0) hit.FinLost = side;
            return hit;
        }

        public MawHit HitBackFin(double damage, bool sever = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive || !BackFinOn) return hit;
            _backFin -= sever ? _backFin : Math.Max(0.0, damage);
            hit.BackFinLost = _backFin <= 0.0;
            return hit;
        }

        public MawHit HitTailFin(double damage, bool sever = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive || !TailFinOn) return hit;
            _tailFin -= sever ? _tailFin : Math.Max(0.0, damage);
            hit.TailFinLost = _tailFin <= 0.0;
            return hit;
        }

        public MawHit HitJaw(double damage, bool sever = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive || !JawOn) return hit;
            _jaw -= sever ? _jaw : Math.Max(0.0, damage);
            hit.JawLost = _jaw <= 0.0;
            return hit;
        }

        /// <summary>A hit on its body behind the head. <paramref name="throat"/>: it went in through its open mouth.</summary>
        public MawHit HitTrunk(double damage, bool throat = false, bool lethal = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive) return hit;
            double done = Math.Max(0.0, damage) * (throat ? ThroatTimes : 1.0);
            if (throat) hit.Throat = done;
            Trunk -= lethal ? Trunk : done;
            if (Trunk <= 0.0) Die(ref hit);
            return hit;
        }

        public MawHit HitHead(double damage, bool lethal = false)
        {
            MawHit hit = MawHit.None;
            if (!Alive) return hit;
            Head -= lethal ? Head : Math.Max(0.0, damage);
            if (Head <= 0.0) Die(ref hit);
            return hit;
        }

        void Die(ref MawHit hit)
        {
            Alive = false;
            hit.Killed = true;
        }
    }
}
