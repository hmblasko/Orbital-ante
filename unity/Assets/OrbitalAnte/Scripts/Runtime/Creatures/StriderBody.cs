using System;
using System.Collections.Generic;

namespace OrbitalAnte.Creatures
{
    public enum StriderStance
    {
        /// <summary>Up on its legs.</summary>
        Standing,
        /// <summary>Too few legs to stand: on its belly, dragging itself with what is left.</summary>
        Down,
        Dead,
    }

    /// <summary>Hit points of a Strider. Limbs are low, like every limb in the game (Part B, Milestone 1).</summary>
    [Serializable]
    public struct StriderHp
    {
        public double Upper;
        public double Lower;
        public double Tip;
        /// <summary>Each piece of a claw arm: a little tougher than a leg's, so disarming it takes aim (still three rifle hits at most).</summary>
        public double Arm;
        /// <summary>The soft underside: the way to kill it.</summary>
        public double Belly;
        /// <summary>The armoured lid: slow going from above.</summary>
        public double Carapace;

        public static StriderHp Default => new StriderHp { Upper = 70, Lower = 55, Tip = 35, Arm = 70, Belly = 240, Carapace = 360 };

        public double Of(int segment) => segment == 0 ? Upper : segment == 1 ? Lower : Tip;
        public double Of(int limb, int segment) => StriderLayout.IsArm(limb) && Arm > 0 ? Arm : Of(segment);
    }

    /// <summary>A limb broke: its pieces from <see cref="FromSegment"/> outward came off as one chain.</summary>
    public readonly struct StriderLoss
    {
        public readonly int Limb;
        public readonly int FromSegment;
        /// <summary>The body burst and threw this limb off (rather than a hit on the limb itself).</summary>
        public readonly bool Burst;

        public StriderLoss(int limb, int fromSegment, bool burst)
        {
            Limb = limb;
            FromSegment = fromSegment;
            Burst = burst;
        }
    }

    public sealed class StriderHit
    {
        public readonly List<StriderLoss> Losses = new List<StriderLoss>();
        public bool Killed;
        /// <summary>The stance changed with this hit (it fell, or died).</summary>
        public bool StanceChanged;
    }

    /// <summary>
    /// The rules of a Strider's body: ten limbs of three pieces each, a soft belly and an armoured lid. No Unity types.
    ///
    /// - A piece that runs out of hit points comes off with everything beyond it.
    /// - It dies only when the belly or the lid is destroyed; then every limb is thrown off and the body bursts.
    /// - A walking leg carries weight while it still has its lower piece (it can stand on the stump of a lost tip).
    /// - It stands while at least <see cref="LegsToStand"/> legs carry weight with at least <see cref="LegsASide"/> on
    ///   each side; every lost leg makes it slower and lower. Below that it is Down: on its belly, dragging itself.
    /// - A claw arm can strike while all three of its pieces are there.
    /// </summary>
    public sealed class StriderBody
    {
        public const int LegsToStand = 5;
        public const int LegsASide = 2;

        readonly StriderHp _max;
        readonly double[] _hp = new double[StriderLayout.Limbs * StriderLayout.Segments];
        readonly int[] _kept = new int[StriderLayout.Limbs];

        public double Belly { get; private set; }
        public double Carapace { get; private set; }
        public bool Alive { get; private set; } = true;

        public StriderBody() : this(StriderHp.Default) { }

        public StriderBody(StriderHp hp)
        {
            _max = hp;
            Belly = hp.Belly;
            Carapace = hp.Carapace;
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                _kept[limb] = StriderLayout.Segments;
                for (int s = 0; s < StriderLayout.Segments; s++) _hp[limb * StriderLayout.Segments + s] = hp.Of(limb, s);
            }
        }

        /// <summary>How many of a limb's pieces are still on, counted from the body (0 to 3).</summary>
        public int Kept(int limb) => _kept[limb];
        public double Hp(int limb, int segment) => _hp[limb * StriderLayout.Segments + segment];
        public double MaxHp(int segment) => _max.Of(segment);
        public double MaxHp(int limb, int segment) => _max.Of(limb, segment);
        public StriderHp Max => _max;

        public bool CarriesWeight(int limb) => !StriderLayout.IsArm(limb) && _kept[limb] >= 2;
        public bool CanStrike(int side) => Alive && _kept[StriderLayout.Limb(side, 0)] == StriderLayout.Segments;

        public int LegsOn(int side)
        {
            int n = 0;
            for (int place = 1; place < StriderLayout.PerSide; place++)
                if (CarriesWeight(StriderLayout.Limb(side, place))) n++;
            return n;
        }

        public int Legs => LegsOn(StriderLayout.Right) + LegsOn(StriderLayout.Left);

        public StriderStance Stance
        {
            get
            {
                if (!Alive) return StriderStance.Dead;
                bool stands = Legs >= LegsToStand && LegsOn(StriderLayout.Right) >= LegsASide && LegsOn(StriderLayout.Left) >= LegsASide;
                return stands ? StriderStance.Standing : StriderStance.Down;
            }
        }

        /// <summary>1 on all eight legs, falling with each one lost to 0 at the fewest it can stand on.</summary>
        double LegShare => Math.Max(0.0, Math.Min(1.0, (Legs - LegsToStand) / (double)(StriderLayout.WalkingLegs - LegsToStand)));

        /// <summary>How fast it can go, as a share of its full pace.</summary>
        public double SpeedShare
        {
            get
            {
                switch (Stance)
                {
                    case StriderStance.Standing: return 0.45 + 0.55 * LegShare;
                    case StriderStance.Down:
                        // It drags itself by whatever limbs still have a second piece; with none it cannot move.
                        int pulling = 0;
                        for (int limb = 0; limb < StriderLayout.Limbs; limb++) if (_kept[limb] >= 2) pulling++;
                        return pulling == 0 ? 0.0 : 0.10 + 0.012 * pulling;
                    default: return 0.0;
                }
            }
        }

        /// <summary>How high it carries its body, as a share of the way from its belly on the ground to full height.</summary>
        public double HeightShare => Stance == StriderStance.Standing ? 0.55 + 0.45 * LegShare : 0.0;

        /// <summary>A hit on one piece of a limb. A sever takes the piece off whatever it has left.</summary>
        public StriderHit HitLimb(int limb, int segment, double damage, bool sever = false)
        {
            var result = new StriderHit();
            if (!Alive || segment >= _kept[limb] || damage <= 0 && !sever) return result;
            StriderStance before = Stance;
            int i = limb * StriderLayout.Segments + segment;
            _hp[i] = sever ? 0.0 : Math.Max(0.0, _hp[i] - damage);
            if (_hp[i] <= 0.0)
            {
                _kept[limb] = segment;
                result.Losses.Add(new StriderLoss(limb, segment, false));
            }
            result.StanceChanged = Stance != before;
            return result;
        }

        /// <summary>A hit on the body: the belly (soft) or the lid (armoured). A lethal hit kills wherever it lands.</summary>
        public StriderHit HitBody(bool belly, double damage, bool lethal = false)
        {
            var result = new StriderHit();
            if (!Alive || damage <= 0 && !lethal) return result;
            if (belly) Belly = lethal ? 0.0 : Math.Max(0.0, Belly - damage);
            else Carapace = lethal ? 0.0 : Math.Max(0.0, Carapace - damage);
            if (Belly <= 0.0 || Carapace <= 0.0)
            {
                Alive = false;
                result.Killed = true;
                result.StanceChanged = true;
                for (int limb = 0; limb < StriderLayout.Limbs; limb++)
                {
                    if (_kept[limb] == 0) continue;
                    result.Losses.Add(new StriderLoss(limb, 0, true));
                    _kept[limb] = 0;
                }
            }
            return result;
        }
    }
}
