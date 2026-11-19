using System;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The scoring formula, and nothing else.
    ///
    ///   Award = Round( Base x K(ante) x (1 + min(cap, perk bonuses)) x Skill x Risk )
    ///
    /// Done in decimal arithmetic, so the same event gives the same whole number on every machine, and halves
    /// always round away from zero. Enemy tier and difficulty live in the base points only: nothing here looks
    /// at them again.
    /// </summary>
    public static class ScoreMath
    {
        /// <summary>Config numbers are typed as fractions like 0.21; this pins them to four decimal places exactly.</summary>
        public static decimal Exact(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0m;
            return Math.Round((decimal)(double)value, 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>How much more every action is worth in this ante. Ante 1 is exactly 1.</summary>
        public static decimal ValueScale(AnteBalance balance, int ante)
        {
            int n = Math.Max(0, ante - 1);
            return 1m + Exact(balance.valueScaleLinear) * n + Exact(balance.valueScaleQuadratic) * n * n;
        }

        /// <summary>
        /// Perk x skill x risk, each held inside its own limits, and the product held under the overall ceiling.
        /// </summary>
        public static decimal EventMultiplier(AnteBalance balance, float perkScoreBonus, float skillFactor, float riskFactor)
        {
            decimal perk = Clamp(Exact(perkScoreBonus), 0m, Exact(balance.perkScoreBonusCap));
            decimal skill = Clamp(Exact(skillFactor), 1m, Exact(balance.skillMax));
            decimal risk = Clamp(Exact(riskFactor), 1m, Exact(balance.riskMax));
            decimal product = (1m + perk) * skill * risk;
            return Math.Min(product, Math.Max(1m, Exact(balance.eventMultiplierCap)));
        }

        public static int Award(AnteBalance balance, int basePoints, int ante, float perkScoreBonus, float skillFactor, float riskFactor)
        {
            if (basePoints <= 0) return 0;
            decimal raw = basePoints * ValueScale(balance, ante) * EventMultiplier(balance, perkScoreBonus, skillFactor, riskFactor);
            decimal rounded = Math.Round(raw, 0, MidpointRounding.AwayFromZero);
            return rounded >= int.MaxValue ? int.MaxValue : (int)rounded;
        }

        static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (max < min) max = min;
            return value < min ? min : (value > max ? max : value);
        }
    }
}
