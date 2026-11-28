using System.Collections.Generic;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The built-in prize items and Celestial effects. Every name, line, colour and rarity here is a placeholder
    /// for Matthew (and later Fable's art) to replace. Each item is also an <see cref="ItemData"/> asset in
    /// Resources/Items (made by the setup tool from this list); an asset that is missing falls back to this.
    ///
    /// Artworks are public-domain paintings only (Part B section 12). Upgrades are sidegrades: each one gives
    /// something and costs something (Part B section 4).
    /// </summary>
    public static class ItemCatalog
    {
        /// <summary>
        /// Which set of built-in upgrade numbers this catalogue holds. The setup tool makes each item's asset once and
        /// then leaves it alone, so an asset made from an older set takes its upgrade's gain, cost and blurb from here
        /// when it loads (<see cref="ItemData"/>; an asset whose Catalog Version is 9999 keeps its own). Raise this
        /// whenever an upgrade's numbers change. 2: Ralph loop pass 4's design review (28 Nov 2026) retuned Big Lungs,
        /// Light Frame, Deep Focus and Heavy Hands so each is a real choice.
        /// </summary>
        public const int Version = 2;

        static Dictionary<string, ItemSpec> _byId;

        /// <summary>The built-in item with this id, or null. A shared copy: read it, never change it.</summary>
        public static ItemSpec Default(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                var all = new Dictionary<string, ItemSpec>();
                foreach (ItemSpec item in CreateDefaults()) all[item.id] = item;
                _byId = all;
            }
            return _byId.TryGetValue(id, out ItemSpec found) ? found : null;
        }

        /// <summary>
        /// Brings an older asset's upgrade up to the catalogue's numbers (<see cref="Version"/>): the gain, the cost and
        /// the blurb, nothing else. True if anything was taken from the catalogue.
        /// </summary>
        public static bool FollowUpgrade(ItemSpec asset, string id, int assetVersion)
        {
            if (asset == null || assetVersion >= Version || asset.category != ItemCategory.Upgrade) return false;
            ItemSpec built = Default(id);
            if (built == null || built.category != ItemCategory.Upgrade) return false;
            asset.upgradeUp = built.upgradeUp;
            asset.upgradeUpAmount = built.upgradeUpAmount;
            asset.upgradeDown = built.upgradeDown;
            asset.upgradeDownAmount = built.upgradeDownAmount;
            asset.blurb = built.blurb;
            return true;
        }

        public static List<ItemSpec> CreateDefaults()
        {
            return new List<ItemSpec>
            {
                // ---- Cosmetics ----
                Cosmetic("HelmetTapeRepair", "Duct Tape Halo", ItemRarity.Common, CosmeticSlot.Helmet, "A helmet held together with optimism.", 0.78f, 0.78f, 0.74f, 0.55f, 0.55f, 0.52f),
                Cosmetic("BadgeMissionPatch", "Mission Patch: Ante Nine", ItemRarity.Common, CosmeticSlot.Badge, "Sewn on crooked, worn with pride.", 0.20f, 0.30f, 0.62f, 0.95f, 0.78f, 0.30f),
                Cosmetic("SuitCoffeeStain", "Coffee-Stained Coveralls", ItemRarity.Uncommon, CosmeticSlot.Suit, "Zero-G coffee goes everywhere. Everywhere.", 0.86f, 0.80f, 0.68f, 0.45f, 0.30f, 0.18f),
                Cosmetic("VisorSunset", "Sunset Visor", ItemRarity.Uncommon, CosmeticSlot.Visor, "Gold tint, warm on the inside.", 0.95f, 0.55f, 0.25f, 0.98f, 0.82f, 0.45f),
                Cosmetic("PackRacingStripes", "Racing Stripe Jetpack", ItemRarity.Rare, CosmeticSlot.Pack, "Adds no speed. Feels like it does.", 0.92f, 0.92f, 0.90f, 0.85f, 0.20f, 0.18f),
                Cosmetic("HelmetFishbowl", "Fishbowl Dome", ItemRarity.Rare, CosmeticSlot.Helmet, "A clear dome, a clear view, a clear lack of fish.", 0.70f, 0.88f, 0.95f, 0.95f, 0.95f, 0.98f),
                Cosmetic("SuitVelvetDisco", "Velvet Moonwalker", ItemRarity.Exotic, CosmeticSlot.Suit, "Crushed velvet, rated for vacuum and the dance floor.", 0.40f, 0.12f, 0.45f, 0.95f, 0.75f, 0.95f),
                Cosmetic("HelmetCrown", "Orbital Crown", ItemRarity.Legendary, CosmeticSlot.Helmet, "Nine points, one for every ante.", 0.95f, 0.75f, 0.28f, 1.00f, 0.95f, 0.70f),

                // ---- Weapon skins ----
                Skin("SkinWoodPanel", "Station Wagon Wood", ItemRarity.Common, "", "Faux wood panelling for any gun.", 0.55f, 0.36f, 0.20f, 0.85f, 0.70f, 0.45f),
                Skin("SkinHazard", "Hazard Stripes", ItemRarity.Common, "", "Yellow and black: everyone knows what it means.", 0.95f, 0.80f, 0.15f, 0.12f, 0.12f, 0.12f),
                Skin("SkinPistolPearl", "Mother of Pearl", ItemRarity.Uncommon, "Pistol", "Iridescent grips for the pistol.", 0.92f, 0.90f, 0.95f, 0.70f, 0.85f, 0.95f),
                Skin("SkinShotgunCandy", "Candy Apple", ItemRarity.Rare, "Shotgun", "Deep red flake over chrome.", 0.75f, 0.08f, 0.12f, 0.95f, 0.60f, 0.60f),
                Skin("SkinRifleStarChart", "Star Chart", ItemRarity.Exotic, "Rifle", "Engraved with the sky the night you cleared Ante 9.", 0.10f, 0.14f, 0.32f, 0.95f, 0.82f, 0.45f),
                Skin("SkinSniperGilded", "Gilded Long Shot", ItemRarity.Legendary, "Sniper", "Gold leaf on the sniper, hand laid.", 0.90f, 0.70f, 0.25f, 1.00f, 0.92f, 0.65f),

                // ---- In-game upgrades (sidegrades) ----
                Upgrade("UpgradeBigLungs", "Big Lungs", ItemRarity.Common, "Bigger air tank, heavier suit.", UpgradeStat.SuitAir, 0.25f, UpgradeStat.MoveSpeed, 0.04f),
                Upgrade("UpgradeLightFrame", "Light Frame", ItemRarity.Common, "Faster on foot, thinner armour.", UpgradeStat.MoveSpeed, 0.12f, UpgradeStat.SuitArmour, 0.10f),
                Upgrade("UpgradeHotLoads", "Hot Loads", ItemRarity.Uncommon, "Harder hits, slower reloads.", UpgradeStat.GunDamage, 0.10f, UpgradeStat.ReloadSpeed, 0.12f),
                Upgrade("UpgradeSpareTank", "Spare Jet Tank", ItemRarity.Uncommon, "More jet fuel, less ship fuel.", UpgradeStat.JetFuel, 0.20f, UpgradeStat.ShipFuel, 0.10f),
                Upgrade("UpgradeDeepFocus", "Deep Focus", ItemRarity.Rare, "More Focus, slower on your feet.", UpgradeStat.Focus, 0.20f, UpgradeStat.MoveSpeed, 0.05f),
                Upgrade("UpgradeHeavyHands", "Heavy Hands", ItemRarity.Exotic, "Hits like a truck. Thinks like one too.", UpgradeStat.MeleeDamage, 0.45f, UpgradeStat.Focus, 0.15f),

                // ---- Ship paints ----
                Paint("PaintMoonDust", "Moon Dust Grey", ItemRarity.Common, "The colour of everything on the Moon.", 0.62f, 0.62f, 0.60f, 0.80f, 0.80f, 0.78f),
                Paint("PaintSafetyOrange", "Safety Orange", ItemRarity.Common, "Easy to find after a crash.", 0.95f, 0.45f, 0.10f, 0.95f, 0.95f, 0.92f),
                Paint("PaintRacingGreen", "Racing Green", ItemRarity.Uncommon, "With a cream stripe down the hull.", 0.08f, 0.30f, 0.18f, 0.92f, 0.88f, 0.75f),
                Paint("PaintTwoTone", "Two-Tone Cruiser", ItemRarity.Rare, "Teal over cream, like a diner on legs.", 0.25f, 0.62f, 0.62f, 0.96f, 0.92f, 0.82f),
                Paint("PaintMidnightFoil", "Midnight Foil", ItemRarity.Exotic, "Deep blue with gold foil seams.", 0.08f, 0.10f, 0.25f, 0.92f, 0.75f, 0.35f),
                Paint("PaintChromeDream", "Chrome Dream", ItemRarity.Legendary, "Mirror chrome. Sunglasses advised.", 0.85f, 0.88f, 0.92f, 1.00f, 1.00f, 1.00f),

                // ---- Artwork (public-domain paintings) ----
                Art("ArtHaywain", "The Hay Wain", ItemRarity.Common, "John Constable", "1821", "A cart in a stream, a long way from here.", 0.45f, 0.55f, 0.35f, 0.80f, 0.85f, 0.90f),
                Art("ArtSunflowers", "Sunflowers", ItemRarity.Uncommon, "Vincent van Gogh", "1888", "Something yellow for the cabin wall.", 0.95f, 0.75f, 0.20f, 0.55f, 0.65f, 0.40f),
                Art("ArtGreatWave", "The Great Wave off Kanagawa", ItemRarity.Rare, "Katsushika Hokusai", "c. 1831", "The sea, rising, with a mountain behind.", 0.18f, 0.30f, 0.50f, 0.92f, 0.90f, 0.82f),
                Art("ArtWanderer", "Wanderer above the Sea of Fog", ItemRarity.Exotic, "Caspar David Friedrich", "c. 1818", "Someone else who likes a view.", 0.45f, 0.48f, 0.55f, 0.85f, 0.82f, 0.75f),
                Art("ArtStarryNight", "The Starry Night", ItemRarity.Legendary, "Vincent van Gogh", "1889", "The sky as it ought to look.", 0.12f, 0.22f, 0.50f, 0.98f, 0.88f, 0.35f),
            };
        }

        /// <summary>The Celestial tier's effects (placeholder names, our own).</summary>
        public static List<ItemEffect> CreateEffects()
        {
            return new List<ItemEffect>
            {
                new ItemEffect("SolarFlare", "Solar Flare", 1.00f, 0.55f, 0.18f),
                new ItemEffect("Aurora", "Aurora", 0.35f, 1.00f, 0.65f),
                new ItemEffect("CometTail", "Comet Tail", 0.70f, 0.88f, 1.00f),
                new ItemEffect("NebulaDrift", "Nebula Drift", 0.85f, 0.45f, 1.00f),
                new ItemEffect("Starfall", "Starfall", 1.00f, 0.95f, 0.70f),
                new ItemEffect("EventHorizon", "Event Horizon", 0.60f, 0.35f, 0.95f),
            };
        }

        static ItemSpec Base(string id, string name, ItemCategory category, ItemRarity rarity, string blurb, float r, float g, float b, float ar, float ag, float ab)
        {
            return new ItemSpec
            {
                id = id, displayName = name, category = category, rarity = rarity, blurb = blurb,
                colour = new[] { r, g, b }, accent = new[] { ar, ag, ab },
            };
        }

        static ItemSpec Cosmetic(string id, string name, ItemRarity rarity, CosmeticSlot slot, string blurb, float r, float g, float b, float ar, float ag, float ab)
        {
            ItemSpec s = Base(id, name, ItemCategory.Cosmetic, rarity, blurb, r, g, b, ar, ag, ab);
            s.slot = slot;
            return s;
        }

        static ItemSpec Skin(string id, string name, ItemRarity rarity, string weaponId, string blurb, float r, float g, float b, float ar, float ag, float ab)
        {
            ItemSpec s = Base(id, name, ItemCategory.WeaponSkin, rarity, blurb, r, g, b, ar, ag, ab);
            s.weaponId = weaponId;
            return s;
        }

        static ItemSpec Upgrade(string id, string name, ItemRarity rarity, string blurb, UpgradeStat up, float upAmount, UpgradeStat down, float downAmount)
        {
            ItemSpec s = Base(id, name, ItemCategory.Upgrade, rarity, blurb, 0.30f, 0.80f, 0.55f, 0.95f, 0.95f, 0.85f);
            s.upgradeUp = up;
            s.upgradeUpAmount = upAmount;
            s.upgradeDown = down;
            s.upgradeDownAmount = downAmount;
            return s;
        }

        static ItemSpec Paint(string id, string name, ItemRarity rarity, string blurb, float r, float g, float b, float ar, float ag, float ab)
            => Base(id, name, ItemCategory.ShipPaint, rarity, blurb, r, g, b, ar, ag, ab);

        static ItemSpec Art(string id, string name, ItemRarity rarity, string artist, string year, string blurb, float r, float g, float b, float ar, float ag, float ab)
        {
            ItemSpec s = Base(id, name, ItemCategory.Artwork, rarity, blurb, r, g, b, ar, ag, ab);
            s.artist = artist;
            s.year = year;
            return s;
        }
    }
}
