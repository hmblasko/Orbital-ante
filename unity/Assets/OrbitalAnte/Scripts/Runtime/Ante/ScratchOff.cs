using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The one free scratch-off between antes. Works like a small chest. Cash from it goes to the wallet only.
    /// The odds are public (<see cref="OddsPercent"/>) and are meant to be printed on the ticket. A ticket can
    /// never give another ticket: there is no such outcome.
    /// </summary>
    public static class ScratchOff
    {
        public const string StreamName = "ante.scratch";

        static readonly ScratchOutcome[] Order =
        {
            ScratchOutcome.SmallCash, ScratchOutcome.BigCash, ScratchOutcome.RepairRefillVoucher,
            ScratchOutcome.TemporaryTacticalPerk, ScratchOutcome.CosmeticToken,
        };

        /// <summary>Depends only on the run seed and the ante, so saving and reloading cannot re-roll a ticket.</summary>
        public static SeededRandom StreamFor(int runSeed, int ante) => SeededRandom.ForStream(runSeed, StreamName).Fork(ante);

        public static float Weight(AnteBalance balance, ScratchOutcome outcome)
        {
            float weight;
            switch (outcome)
            {
                case ScratchOutcome.SmallCash: weight = balance.scratchSmallCashWeight; break;
                case ScratchOutcome.BigCash: weight = balance.scratchBigCashWeight; break;
                case ScratchOutcome.RepairRefillVoucher: weight = balance.scratchVoucherWeight; break;
                case ScratchOutcome.TemporaryTacticalPerk: weight = balance.scratchTacticalPerkWeight; break;
                default: weight = balance.scratchCosmeticWeight; break;
            }
            return Math.Max(0f, weight);
        }

        public static float TotalWeight(AnteBalance balance)
        {
            float total = 0f;
            for (int i = 0; i < Order.Length; i++) total += Weight(balance, Order[i]);
            return total;
        }

        /// <summary>The chance of an outcome, as shown to the player (50 means 50%).</summary>
        public static float OddsPercent(AnteBalance balance, ScratchOutcome outcome)
        {
            float total = TotalWeight(balance);
            return total <= 0f ? 0f : 100f * Weight(balance, outcome) / total;
        }

        public static ScratchOutcome Roll(SeededRandom rng, AnteBalance balance)
        {
            double roll = rng.NextDouble() * TotalWeight(balance);
            double sum = 0;
            for (int i = 0; i < Order.Length; i++)
            {
                sum += Weight(balance, Order[i]);
                if (roll < sum) return Order[i];
            }
            return ScratchOutcome.SmallCash;
        }

        /// <summary>Wallet points this outcome is worth. Zero for the outcomes that are not cash.</summary>
        public static int CashFor(AnteBalance balance, ScratchOutcome outcome)
        {
            if (outcome == ScratchOutcome.SmallCash) return balance.scratchSmallCash;
            return outcome == ScratchOutcome.BigCash ? balance.scratchBigCash : 0;
        }
    }
}
