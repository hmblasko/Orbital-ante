using System;
using System.Collections.Generic;

namespace OrbitalAnte.Creatures
{
    public enum WalkerStance
    {
        /// <summary>Up on its legs.</summary>
        Standing,
        /// <summary>Too few legs to stand: on its belly, dragging itself with what is left.</summary>
        Down,
        Dead,
    }

    /// <summary>Hit points of a Strider. Limbs are low, like every limb in the game (Part B, Milestone 1).</summary>
    [Serializable]
    public struct WalkerHp
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
        /// <summary>Each jaw, for a kind that has them.</summary>
        public double Jaw;

        public static WalkerHp Strider => new WalkerHp { Upper = 70, Lower = 55, Tip = 35, Arm = 70, Belly = 240, Carapace = 360 };
        /// <summary>Lighter all round: a leg in one or two rifle hits, the soft abdomen in half the Strider's.</summary>
        public static WalkerHp Sprinter => new WalkerHp { Upper = 45, Lower = 35, Tip = 25, Arm = 40, Belly = 150, Carapace = 200, Jaw = 45 };

        public double Of(int segment) => segment == 0 ? Upper : segment == 1 ? Lower : Tip;
        public double Of(int limb, int segment) => WalkerLayout.IsArm(limb) && Arm > 0 ? Arm : Of(segment);
    }

    /// <summary>A limb broke: its pieces from <see cref="FromSegment"/> outward came off as one chain.</summary>
    public readonly struct WalkerLoss
    {
        public readonly int Limb;
        public readonly int FromSegment;
        /// <summary>The body burst and threw this limb off (rather than a hit on the limb itself).</summary>
        public readonly bool Burst;

        public WalkerLoss(int limb, int fromSegment, bool burst)
        {
            Limb = limb;
            FromSegment = fromSegment;
            Burst = burst;
        }
    }

    public sealed class WalkerHit
    {
        public readonly List<WalkerLoss> Losses = new List<WalkerLoss>();
        public bool Killed;
        /// <summary>The stance changed with this hit (it fell, or died).</summary>
        public bool StanceChanged;
    }

    /// <summary>
    /// The rules of a walker's body: ten limbs of three pieces each, a soft piece and an armoured one, and for some
    /// kinds two jaws. No Unity types. ("Belly" and "Carapace" here are whichever pieces the kind's layout calls soft
    /// and armoured: the Sprinter's abdomen and head.)
    ///
    /// - A piece that runs out of hit points comes off with everything beyond it.
    /// - It dies only when the belly or the lid is destroyed; then every limb is thrown off and the body bursts.
    /// - A walking leg carries weight while it still has its lower piece (it can stand on the stump of a lost tip).
    /// - It stands while at least <see cref="LegsToStand"/> legs carry weight with at least <see cref="LegsASide"/> on
    ///   each side; every lost leg makes it slower and lower. Below that it is Down: on its belly, dragging itself.
    /// - A claw arm can strike while all three of its pieces are there; a kind that lunges can bite while it has a jaw.
    /// - A jaw is one piece (limb number <see cref="JawLimb"/>); it comes off like a limb and never kills.
    /// </summary>
    public sealed class WalkerBody
    {
        public const int LegsToStand = 5;
        public const int LegsASide = 2;

        /// <summary>The limb number a jaw's loss is reported under: 10 for the right jaw, 11 for the left.</summary>
        public static int JawLimb(int side) => WalkerLayout.Limbs + side;

        readonly WalkerLayout _l;
        readonly WalkerHp _max;
        readonly double[] _jaw = new double[2];
        readonly double[] _hp = new double[WalkerLayout.Limbs * WalkerLayout.Segments];
        readonly int[] _kept = new int[WalkerLayout.Limbs];

        public double Belly { get; private set; }
        public double Carapace { get; private set; }
        public bool Alive { get; private set; } = true;

        public WalkerBody(WalkerLayout layout) : this(layout, layout.Attack == WalkerAttack.Lunge ? WalkerHp.Sprinter : WalkerHp.Strider) { }

        public WalkerBody(WalkerLayout layout, WalkerHp hp)
        {
            _l = layout;
            _max = hp;
            if (layout.HasJaws) _jaw[0] = _jaw[1] = Math.Max(1.0, hp.Jaw);
            Belly = hp.Belly;
            Carapace = hp.Carapace;
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                _kept[limb] = WalkerLayout.Segments;
                for (int s = 0; s < WalkerLayout.Segments; s++) _hp[limb * WalkerLayout.Segments + s] = hp.Of(limb, s);
            }
        }

        /// <summary>How many of a limb's pieces are still on, counted from the body (0 to 3).</summary>
        public int Kept(int limb) => _kept[limb];
        public double Hp(int limb, int segment) => _hp[limb * WalkerLayout.Segments + segment];
        public double MaxHp(int segment) => _max.Of(segment);
        public double MaxHp(int limb, int segment) => _max.Of(limb, segment);
        public WalkerHp Max => _max;

        public bool CarriesWeight(int limb) => !WalkerLayout.IsArm(limb) && _kept[limb] >= 2;
        public WalkerLayout Layout => _l;
        public bool JawOn(int side) => _jaw[side] > 0.0;
        public double JawHp(int side) => _jaw[side];

        /// <summary>How hard it strikes, as a share of its full damage: a kind with jaws bites half as hard on one.</summary>
        public double StrikeShare => !_l.HasJaws ? 1.0 : ((JawOn(0) ? 0.5 : 0.0) + (JawOn(1) ? 0.5 : 0.0));

        /// <summary>A claw is thrown by the arm on that side; a bite needs either jaw, whichever side is asked.</summary>
        public bool CanStrike(int side) => Alive && (_l.Attack == WalkerAttack.Lunge
            ? JawOn(0) || JawOn(1)
            : _kept[WalkerLayout.Limb(side, 0)] == WalkerLayout.Segments);

        public int LegsOn(int side)
        {
            int n = 0;
            for (int place = 1; place < WalkerLayout.PerSide; place++)
                if (CarriesWeight(WalkerLayout.Limb(side, place))) n++;
            return n;
        }

        public int Legs => LegsOn(WalkerLayout.Right) + LegsOn(WalkerLayout.Left);

        public WalkerStance Stance
        {
            get
            {
                if (!Alive) return WalkerStance.Dead;
                bool stands = Legs >= LegsToStand && LegsOn(WalkerLayout.Right) >= LegsASide && LegsOn(WalkerLayout.Left) >= LegsASide;
                return stands ? WalkerStance.Standing : WalkerStance.Down;
            }
        }

        /// <summary>1 on all eight legs, falling with each one lost to 0 at the fewest it can stand on.</summary>
        double LegShare => Math.Max(0.0, Math.Min(1.0, (Legs - LegsToStand) / (double)(WalkerLayout.WalkingLegs - LegsToStand)));

        /// <summary>How fast it can go, as a share of its full pace.</summary>
        public double SpeedShare
        {
            get
            {
                switch (Stance)
                {
                    case WalkerStance.Standing: return 0.45 + 0.55 * LegShare;
                    case WalkerStance.Down:
                        // It drags itself by whatever limbs still have a second piece; with none it cannot move.
                        int pulling = 0;
                        for (int limb = 0; limb < WalkerLayout.Limbs; limb++) if (_kept[limb] >= 2) pulling++;
                        return pulling == 0 ? 0.0 : 0.10 + 0.012 * pulling;
                    default: return 0.0;
                }
            }
        }

        /// <summary>How high it carries its body, as a share of the way from its belly on the ground to full height.</summary>
        public double HeightShare => Stance == WalkerStance.Standing ? 0.55 + 0.45 * LegShare : 0.0;

        /// <summary>A hit on one piece of a limb. A sever takes the piece off whatever it has left.</summary>
        public WalkerHit HitLimb(int limb, int segment, double damage, bool sever = false)
        {
            var result = new WalkerHit();
            if (!Alive || segment >= _kept[limb] || damage <= 0 && !sever) return result;
            WalkerStance before = Stance;
            int i = limb * WalkerLayout.Segments + segment;
            _hp[i] = sever ? 0.0 : Math.Max(0.0, _hp[i] - damage);
            if (_hp[i] <= 0.0)
            {
                _kept[limb] = segment;
                result.Losses.Add(new WalkerLoss(limb, segment, false));
            }
            result.StanceChanged = Stance != before;
            return result;
        }

        /// <summary>A hit on a jaw. It comes off when it runs out; that never kills.</summary>
        public WalkerHit HitJaw(int side, double damage, bool sever = false)
        {
            var result = new WalkerHit();
            if (!Alive || !JawOn(side) || damage <= 0 && !sever) return result;
            _jaw[side] = sever ? 0.0 : Math.Max(0.0, _jaw[side] - damage);
            if (_jaw[side] <= 0.0) result.Losses.Add(new WalkerLoss(JawLimb(side), 0, false));
            return result;
        }

        /// <summary>A hit on the body: the belly (soft) or the lid (armoured). A lethal hit kills wherever it lands.</summary>
        public WalkerHit HitBody(bool belly, double damage, bool lethal = false)
        {
            var result = new WalkerHit();
            if (!Alive || damage <= 0 && !lethal) return result;
            if (belly) Belly = lethal ? 0.0 : Math.Max(0.0, Belly - damage);
            else Carapace = lethal ? 0.0 : Math.Max(0.0, Carapace - damage);
            if (Belly <= 0.0 || Carapace <= 0.0)
            {
                Alive = false;
                result.Killed = true;
                result.StanceChanged = true;
                for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
                {
                    if (_kept[limb] == 0) continue;
                    result.Losses.Add(new WalkerLoss(limb, 0, true));
                    _kept[limb] = 0;
                }
                for (int side = 0; side < 2; side++)
                {
                    if (!JawOn(side)) continue;
                    result.Losses.Add(new WalkerLoss(JawLimb(side), 0, true));
                    _jaw[side] = 0.0;
                }
            }
            return result;
        }
    }
}
