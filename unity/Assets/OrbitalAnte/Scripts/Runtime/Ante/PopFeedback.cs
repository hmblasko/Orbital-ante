using System;

namespace OrbitalAnte.Ante
{
    public enum PopTier { Plain = 0, Skilled = 1, Masterful = 2, Big = 3 }

    /// <summary>How one "+points" line looks and sounds.</summary>
    public struct PopLook
    {
        public PopTier tier;
        public int fontSize;
        /// <summary>The style colour (feats) instead of the score colour.</summary>
        public bool styleColour;
        /// <summary>Seconds on screen.</summary>
        public float seconds;
        /// <summary>The reward sound, or null for none beyond the count-up.</summary>
        public string sound;
        public float volume;
        /// <summary>How loud the count-up's last coin lands (0..1).</summary>
        public float rackEndVolume;
    }

    /// <summary>
    /// Feedback sized by how well you played (21 Nov 2026, fun pass; report "Behavioural psychology of game fun"):
    /// juice raises felt competence only when it follows the player's own performance (Kao, CHI 2024, n = 1,699), and
    /// feedback that is the same for everything stops being read. So a plain kill gets a small line and the coins,
    /// a style feat a bigger line in the style colour and a chime, two or more feats bigger again, and a haul worth a
    /// real share of the ante's target the biggest. Size only ever follows what the player did or how much it paid,
    /// never chance: no pop is staged to look like a near win (constitution rule 9). Plain C#.
    /// </summary>
    public static class PopFeedback
    {
        /// <summary>
        /// The words of one award's line: "+138  MOON LANDING", and when Change of Pace paid part of it, that part
        /// on the end of the same line ("+138  MOON LANDING  (6 CHANGE OF PACE)"). One award is always one
        /// line: the amount named is already inside the total, so it must never look like a second award.
        /// </summary>
        public static string Line(int points, string label, int chainPoints)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string line = "+" + points.ToString("N0", inv) + "  " + (label ?? string.Empty);
            if (chainPoints > 0) line += "  (" + chainPoints.ToString("N0", inv) + " CHANGE OF PACE)";
            return line;
        }

        /// <summary>A single award worth this share of ante 1's target, in ante 1 points, counts as a big one.</summary>
        public const float BigShare = 0.2f;

        /// <summary>
        /// Points at this ante that make an award big: a fifth of ante 1's target times the ante's value scale, so
        /// Big is the same deed at every ante (design review, 21 Nov 2026: a share of each ante's own target would
        /// make the same play rarer to celebrate as targets rise in real effort).
        /// </summary>
        public static int BigAt(AnteBalance balance, int ante)
        {
            balance = balance ?? new AnteBalance();
            return (int)Math.Ceiling((decimal)(balance.TargetFor(1) * BigShare) * ScoreMath.ValueScale(balance, ante));
        }

        /// <param name="bigAt">Points that make an award big (<see cref="BigAt"/>); 0 for never.</param>
        public static PopTier TierFor(int points, int skillFeats, int bigAt)
        {
            if (points <= 0) return PopTier.Plain;
            if (bigAt > 0 && points >= bigAt) return PopTier.Big;
            if (skillFeats >= 2) return PopTier.Masterful;
            if (skillFeats == 1) return PopTier.Skilled;
            return PopTier.Plain;
        }

        public static PopLook LookFor(PopTier tier)
        {
            switch (tier)
            {
                case PopTier.Big:
                    return new PopLook { tier = tier, fontSize = 24, styleColour = false, seconds = 3.6f, sound = "reward.tier3", volume = 0.9f, rackEndVolume = 1f };
                case PopTier.Masterful:
                    return new PopLook { tier = tier, fontSize = 22, styleColour = true, seconds = 3.2f, sound = "reward.tier2", volume = 0.85f, rackEndVolume = 0.9f };
                case PopTier.Skilled:
                    return new PopLook { tier = tier, fontSize = 19, styleColour = true, seconds = 2.8f, sound = "reward.tier1", volume = 0.7f, rackEndVolume = 0.8f };
                default:
                    return new PopLook { tier = PopTier.Plain, fontSize = 17, styleColour = false, seconds = 2.4f, sound = null, volume = 0f, rackEndVolume = 0.6f };
            }
        }

        public static PopLook For(ScoreEvent scoreEvent, ScoreResult result, int bigAt)
        {
            int feats = Math.Max(scoreEvent.skillFeats, scoreEvent.verifiedSkill ? 1 : 0);
            PopLook look = LookFor(TierFor(result.Awarded ? result.points : 0, feats, bigAt));
            // A big award won with style keeps the style colour.
            if (look.tier == PopTier.Big && feats > 0) look.styleColour = true;
            return look;
        }
    }
}
