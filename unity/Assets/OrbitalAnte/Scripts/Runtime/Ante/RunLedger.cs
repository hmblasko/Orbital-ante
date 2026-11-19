using System;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The three separate counts of points.
    ///
    ///  - AnteEarned: score earned this ante. Compared with the ante's target. Starts again at zero each ante.
    ///  - LifetimeRunScore: every legitimate scoring event of the run. This is the leaderboard number and it never goes down.
    ///  - SpendablePoints: the wallet. Goes up with score and with bank-only rewards (scratch-off cash, casino
    ///    winnings); goes down when the player buys or wagers.
    ///
    /// Spending touches only the wallet. Bank-only rewards touch only the wallet. There is no second currency.
    /// </summary>
    public sealed class RunLedger
    {
        public int AnteEarned { get; private set; }
        public int LifetimeRunScore { get; private set; }
        public int SpendablePoints { get; private set; }

        /// <summary>Raised after any of the three numbers changes.</summary>
        public event Action Changed;

        /// <summary>A real scoring event: counts toward the ante, the leaderboard and the wallet.</summary>
        public void AddScore(int points)
        {
            if (points <= 0) return;
            AnteEarned = SafeAdd(AnteEarned, points);
            LifetimeRunScore = SafeAdd(LifetimeRunScore, points);
            SpendablePoints = SafeAdd(SpendablePoints, points);
            Changed?.Invoke();
        }

        /// <summary>Scratch-off cash, casino winnings and the like: wallet only, never the ante or the leaderboard.</summary>
        public void AddBankOnly(int points)
        {
            if (points <= 0) return;
            SpendablePoints = SafeAdd(SpendablePoints, points);
            Changed?.Invoke();
        }

        public bool CanAfford(int cost) => cost >= 0 && SpendablePoints >= cost;

        /// <summary>A purchase or a wager. Fails, changing nothing, if the wallet is short.</summary>
        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == 0) return true;
            SpendablePoints -= cost;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A new ante starts from zero. Score beyond the old target does not carry over; the wallet does.</summary>
        public void StartNewAnte()
        {
            AnteEarned = 0;
            Changed?.Invoke();
        }

        public void Restore(int anteEarned, int lifetimeRunScore, int spendablePoints)
        {
            AnteEarned = Math.Max(0, anteEarned);
            LifetimeRunScore = Math.Max(0, lifetimeRunScore);
            SpendablePoints = Math.Max(0, spendablePoints);
            Changed?.Invoke();
        }

        static int SafeAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
