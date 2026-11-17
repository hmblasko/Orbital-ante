using System;
using System.Collections.Generic;
using System.Globalization;
using OrbitalAnte.Navigation;

namespace OrbitalAnte.UI
{
    /// <summary>How a value in the pause menu's Run Stats is coloured.</summary>
    public enum StatTone
    {
        Plain,
        /// <summary>Worth a look (amber): a part half gone, a gun about to change.</summary>
        Warn,
        /// <summary>Something is wrong (red): broken, torn off, out of air.</summary>
        Bad,
        /// <summary>Said quietly (muted): nothing to report.</summary>
        Quiet,
    }

    public struct StatRow
    {
        public string Label;
        public string Value;
        public StatTone Tone;

        public StatRow(string label, string value, StatTone tone = StatTone.Plain)
        {
            Label = label;
            Value = value;
            Tone = tone;
        }
    }

    public sealed class StatSection
    {
        public readonly string Title;
        public readonly List<StatRow> Rows = new List<StatRow>();

        public StatSection(string title) { Title = title; }

        public StatSection Add(string label, string value, StatTone tone = StatTone.Plain)
        {
            Rows.Add(new StatRow(label, value, tone));
            return this;
        }
    }

    /// <summary>One big number in the performance strip, with its small words underneath.</summary>
    public struct StatCell
    {
        public string Value;
        public string Words;
        public StatTone Tone;

        public StatCell(string value, string words, StatTone tone = StatTone.Plain)
        {
            Value = value;
            Words = words;
            Tone = tone;
        }
    }

    /// <summary>
    /// Everything the pause menu's Run Stats page shows: up to three columns of labelled values, a strip of big
    /// performance numbers, one line of performance counts, and the full technical readout behind a button.
    /// Plain C#, filled in by <c>RunStatsReport</c>, so the page never reaches into the game itself.
    /// </summary>
    public sealed class StatsSheet
    {
        /// <summary>The page has room for three columns side by side.</summary>
        public const int MaxColumns = 3;
        /// <summary>And for this many rows in a column at the Deck's 800 lines; the rest are left out.</summary>
        public const int MaxRows = 8;

        public readonly List<StatSection> Sections = new List<StatSection>();
        public readonly List<StatCell> Performance = new List<StatCell>();
        public string PerformanceLine = string.Empty;
        public string Technical = string.Empty;

        public StatSection Section(string title)
        {
            var s = new StatSection(title);
            Sections.Add(s);
            return s;
        }

        /// <summary>
        /// The sections as the page lays them out: at most <see cref="MaxColumns"/>, empty ones dropped, each cut to
        /// <see cref="MaxRows"/> with its last row saying how many more there were.
        /// </summary>
        public List<StatSection> Columns()
        {
            var columns = new List<StatSection>(MaxColumns);
            foreach (StatSection s in Sections)
            {
                if (s.Rows.Count == 0) continue;
                if (columns.Count == MaxColumns) break;
                if (s.Rows.Count <= MaxRows)
                {
                    columns.Add(s);
                    continue;
                }
                var cut = new StatSection(s.Title);
                for (int i = 0; i < MaxRows - 1; i++) cut.Rows.Add(s.Rows[i]);
                int more = s.Rows.Count - (MaxRows - 1);
                cut.Rows.Add(new StatRow("and " + more + " more", string.Empty, StatTone.Quiet));
                columns.Add(cut);
            }
            return columns;
        }
    }

    /// <summary>Number and time words shared by the HUD and Run Stats. Always the same, whatever the machine's language.</summary>
    public static class StatsFormat
    {
        static readonly CultureInfo Words = CultureInfo.InvariantCulture;

        /// <summary>A whole number with commas: 12,345.</summary>
        public static string Count(long n) => n.ToString("#,0", Words);

        /// <summary>A share as a whole per cent, never showing 100% for a share short of full or 0% for a share above nothing.</summary>
        public static string Percent(float share)
        {
            if (float.IsNaN(share) || share <= 0f) return "0%";
            if (share >= 1f) return "100%";
            int p = (int)Math.Round(share * 100f, MidpointRounding.AwayFromZero);
            if (p >= 100) p = 99;
            if (p <= 0) p = 1;
            return p.ToString(Words) + "%";
        }

        /// <summary>A run clock: 0:07, 12:41, 1:02:05.</summary>
        public static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            long s = (long)Math.Floor(seconds);
            long h = s / 3600, m = s / 60 % 60, sec = s % 60;
            return h > 0
                ? h.ToString(Words) + ":" + m.ToString("00", Words) + ":" + sec.ToString("00", Words)
                : m.ToString(Words) + ":" + sec.ToString("00", Words);
        }

        /// <summary>Time played over many runs: "under a minute", "12 min", "6 h 12 min".</summary>
        public static string Played(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 60) return "under a minute";
            long minutes = (long)Math.Floor(seconds / 60);
            if (minutes < 60) return minutes.ToString(Words) + " min";
            return Count(minutes / 60) + " h " + (minutes % 60).ToString(Words) + " min";
        }

        /// <summary>A speed: tenths under 100 m/s, where a tenth matters, whole numbers above.</summary>
        public static string Speed(double metresPerSecond)
        {
            double v = Math.Abs(metresPerSecond);
            return v < 100 ? v.ToString("0.0", Words) + " m/s" : Count((long)Math.Round(v)) + " m/s";
        }

        /// <summary>A distance split into its number and unit, for a big number with a small unit beside it.</summary>
        public static void Distance(double metres, out string number, out string unit)
        {
            double size = Math.Abs(metres);
            if (size < 1000) { number = Math.Round(metres).ToString("0", Words); unit = "m"; return; }
            unit = "km";
            double km = metres / 1000.0;
            number = size < 100000 ? km.ToString("0.0", Words) : Count((long)Math.Round(km));
        }

        public static string Distance(double metres)
        {
            Distance(metres, out string number, out string unit);
            return number + " " + unit;
        }
    }

    /// <summary>
    /// The rules for what the HUD shows when (Matthew, 17 Nov 2026: too many stats on screen, in plain white text).
    /// The principle: the screen carries only what you act on in the next few seconds, grouped in four places
    /// (where you are at the top, your own state lower left, the thing you aim or fight with lower right, one notice
    /// above the middle of the bottom). Everything else waits in the pause menu's Run Stats.
    /// </summary>
    public static class HudRules
    {
        /// <summary>How long a list of keys stays up before it fades, the first time in a run.</summary>
        public const float HelpSeconds = 12f;
        /// <summary>How long it takes to fade.</summary>
        public const float HelpFade = 1.5f;

        /// <summary>How much a list of keys shows after <paramref name="secondsShown"/> on screen: full, then fading, then gone.</summary>
        public static float HelpAlpha(float secondsShown)
        {
            if (secondsShown <= HelpSeconds) return 1f;
            float t = (secondsShown - HelpSeconds) / HelpFade;
            return t >= 1f ? 0f : 1f - t;
        }

        // ---------------------------------------------------------------- orbit aid

        /// <summary>Where on the orbit bar the circle's speed sits (a notch), leaving room to show going faster.</summary>
        public const float CircleMark = 0.62f;
        /// <summary>Below this share of a circle's speed across, an orbit is not what you are doing: the aid hides.</summary>
        public const double OrbitShownFrom = 0.25;

        /// <summary>
        /// Whether the orbit aid is worth its place: when you are on a loop, or going across at least a quarter of
        /// the speed a circle needs. Hovering, landing and walking pace leave it off the screen.
        /// </summary>
        public static bool ShowOrbit(OrbitKind kind, double sideSpeed, double circleSpeed)
        {
            if (kind == OrbitKind.None) return false;
            if (kind == OrbitKind.Orbit || kind == OrbitKind.Tugged) return true;
            return circleSpeed > 0 && Math.Abs(sideSpeed) >= circleSpeed * OrbitShownFrom;
        }

        /// <summary>Where your speed across sits on the orbit bar, 0 to 1, with a circle's speed at <see cref="CircleMark"/>.</summary>
        public static float OrbitBar(double sideSpeed, double circleSpeed)
        {
            if (circleSpeed <= 0) return 0f;
            double share = Math.Abs(sideSpeed) / circleSpeed * CircleMark;
            return share <= 0 ? 0f : share >= 1 ? 1f : (float)share;
        }

        /// <summary>The orbit aid's few words; <paramref name="tugger"/> names what pulls a stretched loop.</summary>
        public static string OrbitWords(OrbitKind kind, double lowHeight, double highHeight, string tugger)
        {
            switch (kind)
            {
                case OrbitKind.Orbit:
                    return "IN ORBIT   low " + StatsFormat.Distance(lowHeight) + "   high " + StatsFormat.Distance(highHeight);
                case OrbitKind.Tugged:
                    return "Orbit, but the " + tugger + " tugs at its high point";
                case OrbitKind.MeetsGround:
                    return "Comes down: go faster across";
                case OrbitKind.Leaves:
                    return "Leaving: slow down across to stay";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Amber while it brings you down or away, plain when it holds you up.</summary>
        public static StatTone OrbitTone(OrbitKind kind)
            => kind == OrbitKind.Orbit ? StatTone.Plain : kind == OrbitKind.None ? StatTone.Quiet : StatTone.Warn;

        // ---------------------------------------------------------------- landing

        /// <summary>Below this height the climb or sink rate shows under the speed.</summary>
        public const double SinkShownBelow = 2000;

        /// <summary>Whether the climb or sink rate is worth a line: near the ground and moving up or down at all.</summary>
        public static bool ShowSink(double heightAboveGround, float vertical)
            => heightAboveGround >= 0 && heightAboveGround < SinkShownBelow && Math.Abs(vertical) >= 0.3f;

        /// <summary>
        /// Coming down faster than a safe touchdown and less than <paramref name="warningSeconds"/> from the ground
        /// at this rate: the same test as the cockpit dash's warning (DashModel), so the two always agree.
        /// </summary>
        public static bool SinkTooFast(double heightAboveGround, float vertical, float safeSink, float warningSeconds)
            => safeSink > 0f && heightAboveGround >= 0 && vertical < -safeSink && heightAboveGround < -vertical * warningSeconds;

        /// <summary>"down 4.2 m/s" or "up 3.0 m/s".</summary>
        public static string SinkWords(float vertical)
            => (vertical < 0f ? "down " : "up ") + StatsFormat.Speed(Math.Abs(vertical));

        // ---------------------------------------------------------------- gun ladder pips

        /// <summary>More kills than this to the next gun are shown as a number, not pips, so the row never grows past its place.</summary>
        public const int MaxPips = 8;

        /// <summary>Whether the ladder's step fits as pips (one per kill needed).</summary>
        public static bool LadderAsPips(int killsNeeded) => killsNeeded > 0 && killsNeeded <= MaxPips;

        /// <summary>The words beside the pips: the next gun, or that this is the last.</summary>
        public static string LadderWords(bool atTop, string nextGun, int killsLeft, int killsNeeded)
        {
            if (atTop) return "LAST GUN";
            string next = string.IsNullOrEmpty(nextGun) ? "NEXT GUN" : nextGun.ToUpperInvariant();
            if (LadderAsPips(killsNeeded)) return next;
            return killsLeft + (killsLeft == 1 ? " KILL TO " : " KILLS TO ") + next;
        }

        // ---------------------------------------------------------------- ship damage panel

        /// <summary>What one hurt part's row shows: a bar (share 0 to 1) or, once it no longer works, a word.</summary>
        public struct PartRow
        {
            public float Bar;
            public string Tag;
            public StatTone Tone;
        }

        public static PartRow ShipPart(Ship.PartState state, float fraction, float tearProgress)
        {
            switch (state)
            {
                case Ship.PartState.Lost:
                    return new PartRow { Bar = 0f, Tag = "GONE", Tone = StatTone.Bad };
                case Ship.PartState.Broken:
                    return new PartRow { Bar = 0f, Tag = tearProgress > 0.5f ? "COMING LOOSE" : "BROKEN", Tone = StatTone.Bad };
                default:
                    float f = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
                    return new PartRow { Bar = f, Tag = null, Tone = f < 0.5f ? StatTone.Warn : StatTone.Plain };
            }
        }

        /// <summary>A part's state in Run Stats, where there is room for a number.</summary>
        public static StatRow ShipPartStat(string name, Ship.PartState state, float fraction, float tearProgress)
        {
            PartRow row = ShipPart(state, fraction, tearProgress);
            return new StatRow(name, row.Tag ?? StatsFormat.Percent(row.Bar), row.Tone);
        }
    }
}
