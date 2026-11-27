using System;

namespace OrbitalAnte.UI
{
    /// <summary>The digits of the ante clock's flip tiles.</summary>
    public struct ClockFace
    {
        public int Minutes;
        public int Tens;
        public int Ones;
        /// <summary>Counting up past the end of the ante, not down to it.</summary>
        public bool Over;
    }

    /// <summary>
    /// The numbers behind the "Hull stencil" player screen (Matthew, 26 Nov 2026: chose option 2 of the player
    /// screen mock-ups): how many blocks of a bar are lit, what the flip clock's tiles say, how a magazine is
    /// drawn as blocks. Plain C#, no Unity types.
    /// </summary>
    public static class HullReadout
    {
        /// <summary>Blocks in the points bar.</summary>
        public const int PointBlocks = 20;
        /// <summary>The most blocks a magazine is drawn as: up to this many rounds get a block each.</summary>
        public const int RoundBlocks = 10;
        /// <summary>The most pips the "next gun" row is drawn as.</summary>
        public const int MaxPips = 8;
        /// <summary>Blocks in the jet fuel bar.</summary>
        public const int JetBlocks = 10;
        /// <summary>A magazine at or under this share is drawn in the warning colour.</summary>
        public const float LowMagazine = 0.25f;

        static readonly string[] Two = BuildTwo();

        static string[] BuildTwo()
        {
            var t = new string[100];
            for (int i = 0; i < t.Length; i++) t[i] = i.ToString("00");
            return t;
        }

        /// <summary>A count as two digits ("06"), as painted on hardware. Never allocates from 0 to 99.</summary>
        public static string TwoDigits(int n)
        {
            if (n < 0) n = 0;
            return n < Two.Length ? Two[n] : n.ToString();
        }

        /// <summary>
        /// How many of <paramref name="count"/> blocks are lit for a share (0 to 1). Anything left lights at least
        /// one, and the last block lights only when the share is whole, so a full bar always means "there".
        /// </summary>
        public static int Blocks(float share, int count)
        {
            if (count <= 0 || !(share > 0f)) return 0;
            if (share >= 1f) return count;
            int n = (int)Math.Floor(share * count + 1e-4f);
            return n < 1 ? 1 : n >= count ? count - 1 : n;
        }

        /// <summary>The flip tiles for a clock: time left, or time over in overtime.</summary>
        public static ClockFace Clock(float secondsLeft, bool overtime, float overtimeSeconds)
        {
            float raw = overtime ? overtimeSeconds : secondsLeft;
            if (float.IsNaN(raw) || raw < 0f) raw = 0f;
            // Time left rounds up (0:01 until it is really over); time over rounds down (it starts at +0:00).
            int s = overtime ? (int)Math.Floor(raw) : (int)Math.Ceiling(raw);
            int minutes = s / 60;
            if (minutes > 99) { minutes = 99; s = 99 * 60 + 59; }
            int rest = s % 60;
            return new ClockFace { Minutes = minutes, Tens = rest / 10, Ones = rest % 10, Over = overtime };
        }

        /// <summary>How many blocks a magazine of this size is drawn as.</summary>
        public static int RoundBlockCount(int magazine)
        {
            return magazine < 1 ? 1 : magazine > RoundBlocks ? RoundBlocks : magazine;
        }

        /// <summary>How many of those blocks are lit: one a round for a small magazine, a share of a large one.</summary>
        public static int RoundBlocksLit(int ammo, int magazine)
        {
            if (ammo <= 0 || magazine < 1) return 0;
            if (ammo > magazine) ammo = magazine;
            if (magazine <= RoundBlocks) return ammo;
            return Blocks(ammo / (float)magazine, RoundBlocks);
        }

        /// <summary>True when the magazine is nearly empty (and not simply a one-round gun that is loaded).</summary>
        public static bool MagazineLow(int ammo, int magazine)
        {
            if (magazine < 1) return false;
            if (ammo <= 0) return true;
            return magazine > 2 && ammo <= magazine * LowMagazine;
        }

        /// <summary>Pips in the "next gun" row for a step that takes this many kills.</summary>
        public static int PipCount(int killsNeeded)
        {
            return killsNeeded < 1 ? 1 : killsNeeded > MaxPips ? MaxPips : killsNeeded;
        }

        /// <summary>How many of those pips are lit. The last one lights only with the last kill.</summary>
        public static int PipsLit(int kills, int killsNeeded)
        {
            if (kills <= 0 || killsNeeded < 1) return 0;
            if (kills >= killsNeeded) return PipCount(killsNeeded);
            if (killsNeeded <= MaxPips) return kills;
            return Blocks(kills / (float)killsNeeded, MaxPips);
        }

        /// <summary>
        /// How wide the warning strip is for words this wide: room either side, never tiny, and never wider than
        /// the gap between the two lower plates (456 px on the 1280 canvas), so it does not lie over their edges.
        /// </summary>
        public const float NoticeMost = 440f, NoticePadding = 64f;

        public static float NoticeWidth(float wordsWidth)
        {
            float w = wordsWidth + NoticePadding;
            return w < 300f ? 300f : w > NoticeMost ? NoticeMost : w;
        }
    }
}
