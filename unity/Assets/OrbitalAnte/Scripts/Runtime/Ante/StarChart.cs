using System;
using System.Collections.Generic;
using System.Globalization;

namespace OrbitalAnte.Ante
{
    public enum StarState { Empty = 0, Lit = 1, Pending = 2 }

    public struct ChartStar
    {
        /// <summary>Place on the 1280 x 800 screen, y down.</summary>
        public float x, y;
        public StarState state;
        /// <summary>The perk this star stands for, or null for an empty one.</summary>
        public string label;
    }

    public sealed class ChartConstellation
    {
        public PerkFamily family;
        public string name;
        /// <summary>"GUNNER 2/3", "GUNNER SET ON".</summary>
        public string progress;
        /// <summary>The set bonus in words.</summary>
        public string bonus;
        public bool complete;
        /// <summary>The selected card would complete it.</summary>
        public bool completesWithPick;
        public int held;
        public List<ChartStar> stars = new List<ChartStar>();
        /// <summary>Where the name goes (under the lowest star).</summary>
        public float labelX, labelY;
    }

    public sealed class ChartCard
    {
        public PerkDefinition perk;
        public PerkFamily family;
        public string familyName, rarity, name;
        public List<string> gains = new List<string>();
        public List<string> costs = new List<string>();
        /// <summary>The line at the bottom: what this pick does for the chart.</summary>
        public string chartLine;
        public bool completesSet;
        /// <summary>Unlocked since the last draft (Ante/Unlocks.cs): the card says NEW.</summary>
        public bool isNew;
    }

    /// <summary>
    /// What the upgrade screen shows (21 Nov 2026, Matthew: "interesting perk and upgrade system that looks
    /// aesthetically pleasing to upgrade during the game", option A of the mockups, the star chart). Each perk
    /// family is a constellation of <see cref="PerkFamilies.SetSize"/> stars; a held perk lights one; the card
    /// under the cursor shows, pulsing, the star it would light. Plain C#, so the screen can be checked without
    /// Unity and drawn the same way by the game and by any preview. Positions are on a 1280 x 800 screen, y down.
    /// </summary>
    public static class StarChart
    {
        public const float Width = 1280f, Height = 800f;

        /// <summary>Each family's shape: three points, in the order its stars light.</summary>
        static readonly float[][] Shapes =
        {
            new[] { 250f, 170f, 310f, 250f, 200f, 300f },   // Gunner
            new[] { 470f, 130f, 560f, 190f, 520f, 280f },   // Ghost
            new[] { 720f, 140f, 800f, 230f, 700f, 290f },   // Pilot
            new[] { 930f, 170f, 1030f, 230f, 960f, 310f },  // Brawler
            new[] { 1120f, 150f, 1180f, 250f, 1100f, 320f },// Daredevil
        };

        /// <summary>The families left to right as the chart draws them.</summary>
        public static readonly PerkFamily[] Order = { PerkFamily.Gunner, PerkFamily.Ghost, PerkFamily.Pilot, PerkFamily.Brawler, PerkFamily.Daredevil };

        /// <summary>Each family's colour (linear-ish 0..1 RGB), also used on its cards.</summary>
        public static void ColourOf(PerkFamily family, out float r, out float g, out float b)
        {
            switch (family)
            {
                case PerkFamily.Gunner: r = 0.94f; g = 0.63f; b = 0.23f; return;
                case PerkFamily.Ghost: r = 0.36f; g = 0.78f; b = 0.75f; return;
                case PerkFamily.Pilot: r = 0.50f; g = 0.69f; b = 1.00f; return;
                case PerkFamily.Brawler: r = 0.90f; g = 0.48f; b = 0.29f; return;
                case PerkFamily.Daredevil: r = 0.85f; g = 0.42f; b = 0.82f; return;
                default: r = 0.81f; g = 0.78f; b = 0.71f; return;
            }
        }

        public static string FamilyName(PerkFamily family) => family == PerkFamily.None ? "WILD" : family.ToString().ToUpperInvariant();

        public static string RarityName(PerkRarity rarity) => rarity.ToString().ToUpperInvariant();

        /// <summary>Where the stars of a family go: its shape stretched to however many stars its set needs.</summary>
        public static List<(float x, float y)> StarPlaces(PerkFamily family, int count)
        {
            int index = Array.IndexOf(Order, family);
            var places = new List<(float, float)>();
            if (index < 0 || count <= 0) return places;
            float[] s = Shapes[index];
            if (count == 1) { places.Add((s[0], s[1])); return places; }
            // Sample the shape's path evenly by length, so a set of 2 or 4 still reads as one figure.
            float l1 = Dist(s[0], s[1], s[2], s[3]), l2 = Dist(s[2], s[3], s[4], s[5]), total = l1 + l2;
            for (int i = 0; i < count; i++)
            {
                float d = total * i / (count - 1);
                if (d <= l1) places.Add((Lerp(s[0], s[2], d / l1), Lerp(s[1], s[3], d / l1)));
                else places.Add((Lerp(s[2], s[4], (d - l1) / l2), Lerp(s[3], s[5], (d - l1) / l2)));
            }
            return places;
        }

        /// <summary>The chart for a loadout, with the card under the cursor marked (or -1 for none).</summary>
        public static List<ChartConstellation> Constellations(PerkLoadout loadout, IList<PerkDefinition> offers, int selected, AnteBalance balance)
        {
            balance = balance ?? new AnteBalance();
            int size = PerkFamilies.SetSize(balance);
            PerkDefinition pick = offers != null && selected >= 0 && selected < offers.Count ? offers[selected] : null;
            var list = new List<ChartConstellation>();
            foreach (PerkFamily family in Order)
            {
                var held = new List<string>();
                if (loadout != null)
                    for (int i = 0; i < loadout.Owned.Count; i++)
                        if (loadout.Owned[i].family == family) held.Add(loadout.Owned[i].displayName);
                var c = new ChartConstellation
                {
                    family = family,
                    name = FamilyName(family),
                    held = held.Count,
                    complete = held.Count >= size,
                    bonus = PerkFamilies.Describe(family, balance),
                };
                bool pickHere = pick != null && pick.family == family && !c.complete;
                c.completesWithPick = pickHere && held.Count + 1 >= size;
                c.progress = c.complete ? c.name + " SET ON" : c.name + " " + held.Count + "/" + size;
                var places = StarPlaces(family, size);
                float lowest = 0f, sumX = 0f;
                for (int i = 0; i < places.Count; i++)
                {
                    var star = new ChartStar { x = places[i].x, y = places[i].y };
                    if (i < held.Count) { star.state = StarState.Lit; star.label = held[i]; }
                    else if (pickHere && i == held.Count) { star.state = StarState.Pending; star.label = pick.displayName; }
                    c.stars.Add(star);
                    lowest = Math.Max(lowest, star.y);
                    sumX += star.x;
                }
                c.labelX = places.Count > 0 ? sumX / places.Count : 0f;
                c.labelY = lowest + 30f;
                list.Add(c);
            }
            return list;
        }

        /// <summary>The three cards (or fewer), each with its gains and costs on their own lines.</summary>
        /// <param name="newPerkId">A perk unlocked since the last draft, whose card says NEW; null for none.</param>
        public static List<ChartCard> Cards(PerkLoadout loadout, IList<PerkDefinition> offers, AnteBalance balance, string newPerkId = null)
        {
            balance = balance ?? new AnteBalance();
            int size = PerkFamilies.SetSize(balance);
            var cards = new List<ChartCard>();
            if (offers == null) return cards;
            for (int k = 0; k < offers.Count; k++)
            {
                PerkDefinition perk = offers[k];
                if (perk == null) continue;
                var card = new ChartCard
                {
                    perk = perk,
                    family = perk.family,
                    familyName = FamilyName(perk.family),
                    rarity = RarityName(perk.rarity),
                    name = perk.displayName,
                    isNew = !string.IsNullOrEmpty(newPerkId) && perk.id == newPerkId,
                };
                for (int i = 0; i < perk.effects.Length; i++)
                {
                    string words = PerkDefinition.DescribeEffect(perk.effects[i], balance);
                    if (PerkDefinition.IsCost(perk.effects[i])) card.costs.Add(words); else card.gains.Add(words);
                }
                if (perk.family == PerkFamily.None) card.chartLine = "Wild: fits any build";
                else
                {
                    int held = loadout != null ? loadout.FamilyCount(perk.family) : 0;
                    if (held >= size) card.chartLine = card.familyName + " set already on";
                    else if (held + 1 >= size)
                    {
                        card.completesSet = true;
                        card.chartLine = "Completes " + card.familyName + ": " + PerkFamilies.Describe(perk.family, balance);
                    }
                    else card.chartLine = card.familyName + " " + (held + 1) + " of " + size;
                }
                cards.Add(card);
            }
            return cards;
        }

        /// <summary>The wild perks held, which belong to no constellation.</summary>
        public static List<string> WildHeld(PerkLoadout loadout)
        {
            var names = new List<string>();
            if (loadout == null) return names;
            for (int i = 0; i < loadout.Owned.Count; i++)
                if (loadout.Owned[i].family == PerkFamily.None) names.Add(loadout.Owned[i].displayName);
            return names;
        }

        /// <summary>Points with thousands separators, the way every number on the screen is written.</summary>
        public static string Points(int points) => points.ToString("N0", CultureInfo.InvariantCulture);

        static float Dist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
