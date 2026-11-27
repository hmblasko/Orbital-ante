using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The run's story for the death and run-over pages (21 Nov 2026, fun pass): where the points came from, the
    /// best single moment and how many feats, said as what the player did (gain framed), never as what they lost.
    /// Mastery needs to see what worked (SDT competence; the research report's "show the run back to the player").
    /// It never invents advice and never stages a near miss: "short of the target" is the plain distance. Plain C#.
    /// </summary>
    public sealed class RunRecap
    {
        readonly int[] _byCategory = new int[Enum.GetValues(typeof(ScoreCategory)).Length];
        public int Awards { get; private set; }
        public int Feats { get; private set; }
        public int BestPoints { get; private set; }
        public string BestLabel { get; private set; }
        public int BestAnte { get; private set; }

        public int PointsFrom(ScoreCategory category) => _byCategory[(int)category];

        public void Add(ScoreEvent scoreEvent, ScoreResult result, int ante, string label)
        {
            if (!result.Awarded || result.points <= 0) return;
            int c = (int)result.category;
            if (c >= 0 && c < _byCategory.Length) _byCategory[c] += result.points;
            Awards++;
            Feats += Math.Max(scoreEvent.skillFeats, scoreEvent.verifiedSkill ? 1 : 0);
            if (result.points > BestPoints)
            {
                BestPoints = result.points;
                BestLabel = string.IsNullOrEmpty(label) ? result.category.ToString() : label;
                BestAnte = ante;
            }
        }

        /// <summary>
        /// The recap's last line (critique loop round 1): what to aim at next time, as a plain fact. The page a
        /// run ends on is what is remembered of it (peak-end), so it ends looking forward, never on the shortfall.
        /// It names the ante the run fell in, or a higher score once every ante is cleared; it never says "so
        /// close", never counts runs, and promises nothing.
        /// </summary>
        public static string NextLine(int cleared, int lastAnte)
        {
            if (cleared < 0) cleared = 0;
            if (lastAnte < 1) lastAnte = 1;
            return cleared >= lastAnte
                ? "Next mark: a higher run score."
                : "Next mark: clear ante " + (cleared + 1).ToString(CultureInfo.InvariantCulture) + ".";
        }

        static string Name(ScoreCategory c)
        {
            switch (c)
            {
                case ScoreCategory.Combat: return "fighting";
                case ScoreCategory.Exploration: return "exploring";
                case ScoreCategory.Bounty: return "bounties";
                // What pays these two today (ScoreWays): a mast is fighting, a first landing is exploring.
                case ScoreCategory.Objective: return "drops and missions";
                case ScoreCategory.Salvage: return "salvage";
                case ScoreCategory.Repair: return "repairs";
                case ScoreCategory.Landing: return "hard landings";
                default: return c.ToString().ToLowerInvariant();
            }
        }

        /// <summary>"Points from fighting 62% · exploring 25% · tasks 13%": the top three, rounded so they read.</summary>
        public string SourcesLine()
        {
            int total = 0;
            for (int i = 0; i < _byCategory.Length; i++) total += _byCategory[i];
            if (total <= 0) return string.Empty;
            var order = new List<int>();
            for (int i = 0; i < _byCategory.Length; i++) if (_byCategory[i] > 0) order.Add(i);
            order.Sort((a, b) => _byCategory[b].CompareTo(_byCategory[a]));
            var parts = new List<string>();
            for (int k = 0; k < order.Count && k < 3; k++)
                parts.Add(Name((ScoreCategory)order[k]) + " " + (int)Math.Round(100.0 * _byCategory[order[k]] / total) + "%");
            return "Points from " + string.Join(" · ", parts);
        }

        public string BestLine()
        {
            if (BestPoints <= 0) return string.Empty;
            string line = "Best moment: +" + BestPoints.ToString("N0", CultureInfo.InvariantCulture) + " " + BestLabel + " (ante " + BestAnte + ")";
            if (Feats > 0) line += "   Style feats: " + Feats;
            return line;
        }

        /// <summary>The recap's own lines, empty ones left out.</summary>
        public string Lines()
        {
            var s = new StringBuilder();
            string a = SourcesLine(), b = BestLine();
            if (a.Length > 0) s.Append(a).Append('\n');
            if (b.Length > 0) s.Append(b).Append('\n');
            return s.ToString().TrimEnd('\n');
        }
    }
}
