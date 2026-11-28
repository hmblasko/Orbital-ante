using System;
using OrbitalAnte.Ante;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// What a Locker upgrade does to a run (Ralph loop pass 4): its gain and its cost, each a share of one of the
    /// run's numbers, on top of the perks, the shop and the hazard. Part B section 4: unlocks are sidegrades, never
    /// straight upgrades, so every upgrade's cost is applied as surely as its gain. Plain C#.
    /// </summary>
    public static class UpgradeEffects
    {
        /// <summary>The most and least an upgrade can move a number, whatever an asset says.</summary>
        public const float Least = 0.5f, Most = 2f;

        /// <summary>How much an upgrade multiplies one of the stats it names (1 for any it does not name).</summary>
        public static float Factor(ItemSpec upgrade, UpgradeStat stat)
        {
            if (upgrade == null || upgrade.category != ItemCategory.Upgrade || stat == UpgradeStat.None) return 1f;
            float f = 1f;
            if (upgrade.upgradeUp == stat) f *= 1f + Math.Max(0f, upgrade.upgradeUpAmount);
            if (upgrade.upgradeDown == stat) f *= 1f - Math.Min(0.9f, Math.Max(0f, upgrade.upgradeDownAmount));
            return f < Least ? Least : f > Most ? Most : f;
        }

        /// <summary>Puts an upgrade's gain and cost into a run's numbers. Nothing changes for null.</summary>
        public static void Apply(RunMods m, ItemSpec upgrade)
        {
            if (m == null || upgrade == null || upgrade.category != ItemCategory.Upgrade) return;
            m.GunDamage *= Factor(upgrade, UpgradeStat.GunDamage);
            m.Move *= Factor(upgrade, UpgradeStat.MoveSpeed);
            m.Oxygen *= Factor(upgrade, UpgradeStat.SuitAir);
            m.MaxHealth *= Factor(upgrade, UpgradeStat.SuitArmour);
            m.JetFuel *= Factor(upgrade, UpgradeStat.JetFuel);
            m.FocusGain *= Factor(upgrade, UpgradeStat.Focus);
            // A faster reload is a shorter one.
            m.ReloadTime /= Factor(upgrade, UpgradeStat.ReloadSpeed);
            m.ShipFuel *= Factor(upgrade, UpgradeStat.ShipFuel);
            m.MeleeDamage *= Factor(upgrade, UpgradeStat.MeleeDamage);
        }
    }

    /// <summary>The patterns a gun skin can be drawn with. The numbers are the model shader's (<c>OA_Skin</c> in OA_Model.shader).</summary>
    public enum SkinPattern
    {
        /// <summary>One colour on every painted part, the pictures' wear showing through.</summary>
        Paint = 1,
        /// <summary>Diagonal bands of the colour and the accent.</summary>
        Stripes = 2,
        /// <summary>Wood grain along the gun's length.</summary>
        Grain = 3,
        /// <summary>Pale, with a colour that moves as the gun turns.</summary>
        Pearl = 4,
        /// <summary>Deep colour with fine flake; the bare metal under it is tinted.</summary>
        Flake = 5,
        /// <summary>Night blue scattered with gold points.</summary>
        Stars = 6,
        /// <summary>Gold over every part, bare metal included.</summary>
        Gilt = 7,
    }

    /// <summary>How one gun skin is drawn: a pattern and its two colours (sRGB, 0 to 1).</summary>
    public struct SkinLook
    {
        public SkinPattern Pattern;
        public float[] Colour;
        public float[] Accent;
        /// <summary>A short name for the material cache: the same look is always the same material. Made once, by <see cref="PrizeLooks.Skin"/>.</summary>
        public string Key;

        public static string KeyFor(SkinPattern pattern, float[] colour, float[] accent) => (int)pattern + ":" + ItemLook.Hex(colour) + ItemLook.Hex(accent);
    }

    /// <summary>
    /// How each kind of prize looks in the game (Ralph loop pass 4). The colours come from the item (its asset, so
    /// Matthew can change them in the Inspector); the pattern or style comes from this table by the item's id,
    /// because the item assets on a machine are made once and would never learn a new field. An item this table
    /// does not know (one Matthew adds) gets the plain look in its own colours. Plain C#.
    /// </summary>
    public static class PrizeLooks
    {
        static float[] Safe(float[] c, float r, float g, float b) => c != null && c.Length == 3 ? c : new[] { r, g, b };

        // ------------------------------------------------------------------ gun skins

        public static SkinPattern PatternOf(string itemId)
        {
            switch (itemId)
            {
                case "SkinWoodPanel": return SkinPattern.Grain;
                case "SkinHazard": return SkinPattern.Stripes;
                case "SkinPistolPearl": return SkinPattern.Pearl;
                case "SkinShotgunCandy": return SkinPattern.Flake;
                case "SkinRifleStarChart": return SkinPattern.Stars;
                case "SkinSniperGilded": return SkinPattern.Gilt;
                // Earned looks (Ralph loop pass 5).
                case "SkinButterfingers": return SkinPattern.Stripes;
                case "SkinPistolSharkskin": return SkinPattern.Flake;
                default: return SkinPattern.Paint;
            }
        }

        public static SkinLook Skin(ItemSpec item)
        {
            var look = new SkinLook
            {
                Pattern = PatternOf(item != null ? item.id : ""),
                Colour = Safe(item?.colour, 0.6f, 0.6f, 0.6f),
                Accent = Safe(item?.accent, 0.9f, 0.8f, 0.5f),
            };
            look.Key = SkinLook.KeyFor(look.Pattern, look.Colour, look.Accent);
            return look;
        }

        // ------------------------------------------------------------------ ship paints

        public static HullPaintStyle PaintStyleOf(string itemId)
        {
            switch (itemId)
            {
                case "PaintRacingGreen": return HullPaintStyle.Band;
                case "PaintTwoTone": return HullPaintStyle.TwoTone;
                // Earned (Ralph loop pass 5): the band is the accent, black on a white hull.
                case "PaintRollPattern": return HullPaintStyle.Band;
                default: return HullPaintStyle.Plain;
            }
        }

        /// <summary>The hull paint for a ship paint item, or null for anything else.</summary>
        public static HullPaint Paint(ItemSpec item)
        {
            if (item == null || item.category != ItemCategory.ShipPaint) return null;
            float[] c = Safe(item.colour, 0.85f, 0.84f, 0.80f), a = Safe(item.accent, 0.8f, 0.6f, 0.2f);
            return new HullPaint(new P3(c[0], c[1], c[2]), new P3(a[0], a[1], a[2]), PaintStyleOf(item.id));
        }

        // ------------------------------------------------------------------ the suit

        /// <summary>The visor tint is never stronger than this share of the visor's own colour, so the world's colours still mean what they mean.</summary>
        public const float VisorTintShare = 0.12f;
        /// <summary>How far a suit's colour goes into its readout plate's face (kept dark, so the cream words stay easy to read).</summary>
        public const float PlateShare = 0.5f;
        public const float PlateDark = 0.3f;

        /// <summary>
        /// What the suit pieces in use change in the view and on the suit plate. Any piece that is null leaves its part
        /// as it was; with nothing worn, every number is the game's own.
        /// </summary>
        public static SuitLook Suit(ItemSpec helmet, ItemSpec visor, ItemSpec suit, ItemSpec pack, ItemSpec badge)
        {
            var look = SuitLook.Standard;
            if (helmet != null)
            {
                float[] c = Safe(helmet.colour, 0.5f, 0.5f, 0.5f);
                if (helmet.id == "HelmetFishbowl")
                {
                    // A clear dome: the edge of the view is barely shaded.
                    look.ShadeStrength = 0.45f;
                }
                else
                {
                    // The helmet's own colour, darkened to the depth of the glass's edge.
                    look.Shade = new[] { c[0] * 0.24f, c[1] * 0.24f, c[2] * 0.24f };
                    look.ShadeStrength = 1.1f;
                }
            }
            if (visor != null)
            {
                float[] c = Safe(visor.colour, 1f, 1f, 1f);
                look.VisorTint = new[] { Tint(c[0]), Tint(c[1]), Tint(c[2]) };
            }
            if (suit != null)
            {
                float[] c = Safe(suit.colour, 0.5f, 0.5f, 0.5f), a = Safe(suit.accent, 0.5f, 0.5f, 0.5f);
                look.PlateFace = Mix(SuitLook.StandardFace, c, PlateDark, PlateShare);
                look.PlateEdge = Mix(SuitLook.StandardEdge, a, 0.55f, 0.5f);
            }
            if (pack != null)
            {
                // White-hot toward the pack's own colour: never so deep a red or orange that a jet reads as damage.
                float[] c = Safe(pack.colour, 0.62f, 0.86f, 1f);
                look.Jet = new[] { Pale(c[0]), Pale(c[1]), Pale(c[2]) };
            }
            if (badge != null)
            {
                look.Badge = true;
                look.BadgeColour = Safe(badge.colour, 0.2f, 0.3f, 0.6f);
                look.BadgeAccent = Safe(badge.accent, 0.95f, 0.78f, 0.3f);
            }
            return look;
        }

        static float Tint(float c) => 1f - VisorTintShare * (1f - Clamp01(c));
        static float Pale(float c) => Clamp01(c) + (1f - Clamp01(c)) * 0.35f;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float[] Mix(float[] from, float[] colour, float dark, float share)
        {
            var o = new float[3];
            for (int i = 0; i < 3; i++) o[i] = from[i] + (Clamp01(colour[i]) * dark - from[i]) * share;
            return o;
        }
    }

    /// <summary>What the suit's pieces in use change in what the player sees (<see cref="PrizeLooks.Suit"/>). Colours are sRGB, 0 to 1.</summary>
    public struct SuitLook
    {
        /// <summary>The colour of the shade at the lower corners of the view (the helmet's edge), and how strong it is against the suit's own setting.</summary>
        public float[] Shade;
        public float ShadeStrength;
        /// <summary>A colour filter over the world outside the ship (the visor's tint). White is none.</summary>
        public float[] VisorTint;
        /// <summary>The suit plate's face and edge.</summary>
        public float[] PlateFace, PlateEdge;
        /// <summary>The jets' flame colour and the visor rim's glow, or null for the suit's own.</summary>
        public float[] Jet;
        /// <summary>A patch sewn on the suit plate.</summary>
        public bool Badge;
        public float[] BadgeColour, BadgeAccent;

        /// <summary>The shade colour the suit is built with (SuitHud's own).</summary>
        public static readonly float[] StandardShade = { 0.04f, 0.03f, 0.03f };
        /// <summary>HullKit.PlateFace and PlateEdge, as numbers (their alpha stays the plates' own).</summary>
        public static readonly float[] StandardFace = { 0.118f, 0.122f, 0.129f };
        public static readonly float[] StandardEdge = { 0.278f, 0.282f, 0.294f };

        public static SuitLook Standard => new SuitLook
        {
            Shade = (float[])StandardShade.Clone(),
            ShadeStrength = 1f,
            VisorTint = new[] { 1f, 1f, 1f },
            PlateFace = (float[])StandardFace.Clone(),
            PlateEdge = (float[])StandardEdge.Clone(),
            Jet = null,
            Badge = false,
        };

        public bool Tinted => VisorTint != null && (VisorTint[0] < 0.999f || VisorTint[1] < 0.999f || VisorTint[2] < 0.999f);
    }
}
