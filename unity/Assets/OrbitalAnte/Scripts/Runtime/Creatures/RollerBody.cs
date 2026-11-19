using System;
using System.Collections.Generic;

namespace OrbitalAnte.Creatures
{
    /// <summary>Hit points of a Roller. Legs are low, like every limb in the game; a plate is armour and takes three rifle hits.</summary>
    [Serializable]
    public struct RollerHp
    {
        public double Leg;
        public double Plate;
        public double Tail;
        /// <summary>The armoured head: the slow way to kill it (half as much again as the soft body, like the Strider's lid).</summary>
        public double Head;
        /// <summary>The soft body under the plates: the way to kill it.</summary>
        public double Soft;

        public static RollerHp Default => new RollerHp { Leg = 30, Plate = 72, Tail = 70, Head = 300, Soft = 200 };
    }

    public enum RollerLossKind { Leg, Plate, Tail }

    /// <summary>A piece broke off.</summary>
    public readonly struct RollerLoss
    {
        public readonly RollerLossKind Kind;
        /// <summary>Which leg (0 to 13) or which segment's plate (0 to 6).</summary>
        public readonly int Index;

        public RollerLoss(RollerLossKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }
    }

    public sealed class RollerHit
    {
        public readonly List<RollerLoss> Losses = new List<RollerLoss>(2);
        public bool Killed;
    }

    /// <summary>
    /// What is left of a Roller, with no Unity types. Seven plates, fourteen legs and the tail fan can each be shot
    /// off. It dies only when the soft body under the plates or the armoured head gives way.
    ///
    /// - A plate is armour: while it is on, its segment's soft body cannot be reached from above. Shot off, it leaves
    ///   that segment bare for good.
    /// - With fewer than <see cref="PlatesToRoll"/> plates it can no longer close into a ball, so it cannot charge.
    /// - Legs only make it slower on foot; a ball needs none.
    /// - It rights itself with its legs and its tail: without the tail it lies on its back much longer, and a little
    ///   longer for every leg lost.
    /// </summary>
    public sealed class RollerBody
    {
        public const int PlatesToRoll = 5;

        readonly RollerHp _hp;
        readonly double[] _plate = new double[RollerLayout.Segs];
        readonly double[] _leg = new double[RollerLayout.Legs];
        double _tail;
        readonly RollerHit _hit = new RollerHit();

        public double Head { get; private set; }
        public double Soft { get; private set; }
        public bool Alive { get; private set; } = true;
        public int Plates { get; private set; } = RollerLayout.Segs;
        public int Legs { get; private set; } = RollerLayout.Legs;

        public RollerBody() : this(RollerHp.Default) { }

        public RollerBody(RollerHp hp)
        {
            if (hp.Soft <= 0) hp = RollerHp.Default;
            _hp = hp;
            for (int i = 0; i < _plate.Length; i++) _plate[i] = hp.Plate;
            for (int i = 0; i < _leg.Length; i++) _leg[i] = hp.Leg;
            _tail = hp.Tail;
            Head = hp.Head;
            Soft = hp.Soft;
        }

        public bool PlateOn(int seg) => _plate[seg] > 0.0;
        public bool LegOn(int leg) => _leg[leg] > 0.0;
        public bool TailOn => _tail > 0.0;
        public double PlateHp(int seg) => Math.Max(0.0, _plate[seg]);
        public double LegHp(int leg) => Math.Max(0.0, _leg[leg]);
        public double TailHp => Math.Max(0.0, _tail);

        public bool CanRoll => Alive && Plates >= PlatesToRoll;

        /// <summary>The share of its walking pace it still has.</summary>
        public double SpeedShare => 0.35 + 0.65 * Legs / RollerLayout.Legs;

        /// <summary>How much longer than usual it lies on its back before it can turn over.</summary>
        public double OnBackShare => (TailOn ? 1.0 : 1.6) * (1.0 + 0.05 * (RollerLayout.Legs - Legs));

        RollerHit Fresh()
        {
            _hit.Losses.Clear();
            _hit.Killed = false;
            return _hit;
        }

        /// <summary>The result is reused by the next hit: read it before hitting again.</summary>
        public RollerHit HitLeg(int leg, double damage, bool sever = false)
        {
            RollerHit hit = Fresh();
            if (!Alive || !LegOn(leg)) return hit;
            _leg[leg] = sever ? 0.0 : _leg[leg] - damage;
            if (!LegOn(leg))
            {
                Legs--;
                hit.Losses.Add(new RollerLoss(RollerLossKind.Leg, leg));
            }
            return hit;
        }

        public RollerHit HitPlate(int seg, double damage, bool sever = false)
        {
            RollerHit hit = Fresh();
            if (!Alive || !PlateOn(seg)) return hit;
            _plate[seg] = sever ? 0.0 : _plate[seg] - damage;
            if (!PlateOn(seg))
            {
                Plates--;
                hit.Losses.Add(new RollerLoss(RollerLossKind.Plate, seg));
            }
            return hit;
        }

        public RollerHit HitTail(double damage, bool sever = false)
        {
            RollerHit hit = Fresh();
            if (!Alive || !TailOn) return hit;
            _tail = sever ? 0.0 : _tail - damage;
            if (!TailOn) hit.Losses.Add(new RollerLoss(RollerLossKind.Tail, 0));
            return hit;
        }

        /// <summary>A hit on the soft body (<paramref name="soft"/>) or on the armoured head.</summary>
        public RollerHit HitBody(bool soft, double damage, bool lethal = false)
        {
            RollerHit hit = Fresh();
            if (!Alive) return hit;
            if (soft) Soft -= damage;
            else Head -= damage;
            if (lethal || Soft <= 0.0 || Head <= 0.0)
            {
                Soft = Math.Max(0.0, Soft);
                Head = Math.Max(0.0, Head);
                Alive = false;
                hit.Killed = true;
            }
            return hit;
        }
    }
}
