namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// Words the run shows for a string of kills and tasks done close together (critique loop round 4, 27 Nov
    /// 2026). Plain C#, no Unity types.
    ///
    /// The rule itself (<see cref="Streak"/>) is a combo inside one run: it is built by playing well, pays one
    /// style feat, costs nothing when it lapses and ends with the run. That is not the streak constitution rule 9
    /// forbids (a count kept across days that punishes a day off). But the word on screen was "STREAK", which is
    /// that kind's name, so the shown words are "ON A ROLL": the same fact without the borrowed pressure. The
    /// code keeps the name Streak so the TaskConfig asset keeps its numbers.
    /// </summary>
    public static class RunWords
    {
        public const string OnARoll = "ON A ROLL";

        /// <summary>The readout's line: empty under two, then the count, then what it will pay once it can.</summary>
        public static string RollLine(int count, int forFeat)
        {
            if (count < 2) return string.Empty;
            return OnARoll + " x" + count + (count >= forFeat ? "  (next task +style)" : string.Empty);
        }
    }
}
