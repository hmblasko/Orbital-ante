namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The kinds of score in the player's words (design review, 27 Nov 2026): each kind is named by the things that
    /// pay it today, so "score 4 different ways" and the Change of Pace perk are never a hidden rule (constitution
    /// rule 3). A mast is a fight (its points are combat points), a first landing on a world is exploring, and only a
    /// HARD LANDING scores as a landing. When something new starts to pay a kind, its words here change with it: the
    /// rule test <c>ScoreWays_NameWhatReallyPaysEachKind</c> holds every reported event to these lists. Plain C#.
    /// </summary>
    public static class ScoreWays
    {
        /// <summary>The kinds something in the game pays today, in the order they are listed.</summary>
        public static readonly ScoreCategory[] Paid =
        {
            ScoreCategory.Combat, ScoreCategory.Exploration, ScoreCategory.Landing, ScoreCategory.Objective, ScoreCategory.Salvage,
        };

        /// <summary>"a kill or a mast": what pays a kind, as one item of a list.</summary>
        public static string Things(ScoreCategory category)
        {
            switch (category)
            {
                case ScoreCategory.Combat: return "a kill or a mast";
                case ScoreCategory.Exploration: return "a first landing on a world";
                case ScoreCategory.Landing: return "a HARD LANDING";
                case ScoreCategory.Objective: return "a supply drop or a bonus mission";
                case ScoreCategory.Salvage: return "a delivered crate";
                case ScoreCategory.Bounty: return "a bounty";
                case ScoreCategory.Repair: return "a repair";
                default: return category.ToString().ToLowerInvariant();
            }
        }

        /// <summary>"kills and masts": the same, short and plural, for a perk card or the recap.</summary>
        public static string Plural(ScoreCategory category)
        {
            switch (category)
            {
                case ScoreCategory.Combat: return "kills and masts";
                case ScoreCategory.Exploration: return "first landings";
                case ScoreCategory.Landing: return "HARD LANDINGS";
                case ScoreCategory.Objective: return "drops and missions";
                case ScoreCategory.Salvage: return "crates";
                case ScoreCategory.Bounty: return "bounties";
                case ScoreCategory.Repair: return "repairs";
                default: return category.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Every kind paid today: "a kill or a mast, a first landing on a world, ..., a delivered crate".</summary>
        public static string List() => Join(false);

        /// <summary>Every kind paid today, short: "kills and masts, first landings, ..., crates".</summary>
        public static string ShortList() => Join(true);

        static string Join(bool plural)
        {
            var parts = new string[Paid.Length];
            for (int i = 0; i < Paid.Length; i++) parts[i] = plural ? Plural(Paid[i]) : Things(Paid[i]);
            return string.Join(", ", parts);
        }
    }
}
