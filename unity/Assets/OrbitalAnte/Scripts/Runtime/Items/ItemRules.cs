using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Items
{
    // Prize items (Matthew, 16 Nov 2026: "an item system like tf2"). Every run hands the player a sealed envelope
    // that opens only after Ante 9 is cleared, and opening it gives one item of some rarity. Plain C#, no Unity
    // types, so every rule here is tested with `dotnet test tools/rules-tests`.
    //
    // Placeholders on purpose: the rarity names, the item names and the odds are first guesses for Matthew to
    // change. Final item art and models are Fable's (made in Blender); until then a card shows a drawn icon.

    /// <summary>How rare an item is. The order matters: higher is rarer. Names are placeholders.</summary>
    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Exotic = 3,
        Legendary = 4,
        /// <summary>The top tier, like an Unusual: an ordinary looking item that also carries a special effect.</summary>
        Celestial = 5,
    }

    /// <summary>The five kinds of prize Matthew listed.</summary>
    public enum ItemCategory
    {
        Cosmetic = 0,
        WeaponSkin = 1,
        Upgrade = 2,
        ShipPaint = 3,
        Artwork = 4,
    }

    /// <summary>Where a cosmetic is worn. None for everything that is not a cosmetic.</summary>
    public enum CosmeticSlot
    {
        None = 0,
        Helmet = 1,
        Visor = 2,
        Suit = 3,
        Pack = 4,
        Badge = 5,
    }

    /// <summary>What an in-game upgrade changes. Read by the ante loop once it is wired in (Milestone 14).</summary>
    public enum UpgradeStat
    {
        None = 0,
        GunDamage = 1,
        MoveSpeed = 2,
        SuitAir = 3,
        SuitArmour = 4,
        JetFuel = 5,
        Focus = 6,
        ReloadSpeed = 7,
        ShipFuel = 8,
        MeleeDamage = 9,
    }

    /// <summary>
    /// One prize item, as data. <see cref="ItemData"/> is the asset that holds one of these for the Inspector.
    /// Colours are plain RGB triples (0 to 1) so the rules stay free of Unity types; they paint the stand-in card
    /// art until Fable's real art exists.
    /// </summary>
    [Serializable]
    public sealed class ItemSpec
    {
        public string id = "";
        public string displayName = "";
        public ItemCategory category;
        public ItemRarity rarity;
        /// <summary>One line on the card.</summary>
        public string blurb = "";

        public CosmeticSlot slot;
        /// <summary>For a weapon skin: the gun it fits (a <c>WeaponData</c> id), or empty for every gun.</summary>
        public string weaponId = "";

        /// <summary>For an upgrade: what it improves and by how much (0.15 is 15%).</summary>
        public UpgradeStat upgradeUp;
        public float upgradeUpAmount;
        /// <summary>
        /// For an upgrade: what it costs. Part B section 4 says unlocks are sidegrades, never straight upgrades,
        /// so every upgrade carries a downside (0.10 is 10% less).
        /// </summary>
        public UpgradeStat upgradeDown;
        public float upgradeDownAmount;

        /// <summary>For an artwork: who painted it and when (public-domain works only).</summary>
        public string artist = "";
        public string year = "";

        /// <summary>Main and accent colour of the stand-in art.</summary>
        public float[] colour = { 0.6f, 0.6f, 0.6f };
        public float[] accent = { 0.9f, 0.8f, 0.5f };

        /// <summary>True for the kinds of item that can carry a Celestial effect.</summary>
        public bool CanCarryEffect => category == ItemCategory.Cosmetic || category == ItemCategory.WeaponSkin || category == ItemCategory.ShipPaint;

        public void Validate(List<string> problems)
        {
            if (string.IsNullOrEmpty(displayName)) problems.Add("it has no name to show.");
            if (rarity == ItemRarity.Celestial)
                problems.Add("no item is Celestial by itself: Celestial is an ordinary item that also carries an effect.");
            if (category == ItemCategory.Cosmetic && slot == CosmeticSlot.None) problems.Add("a cosmetic needs a slot to be worn in.");
            if (category != ItemCategory.Cosmetic && slot != CosmeticSlot.None) problems.Add("only cosmetics have a slot.");
            if (category == ItemCategory.Upgrade)
            {
                if (upgradeUp == UpgradeStat.None || upgradeUpAmount <= 0f) problems.Add("an upgrade must improve something.");
                if (upgradeDown == UpgradeStat.None || upgradeDownAmount <= 0f)
                    problems.Add("an upgrade must also cost something (unlocks are sidegrades, never straight upgrades).");
                if (upgradeUp != UpgradeStat.None && upgradeUp == upgradeDown) problems.Add("an upgrade cannot improve and cost the same thing.");
            }
            if (category == ItemCategory.Artwork && string.IsNullOrEmpty(artist)) problems.Add("an artwork needs its artist (public-domain works only).");
            if (colour == null || colour.Length != 3 || accent == null || accent.Length != 3) problems.Add("its colours must be three numbers each.");
        }

        public ItemSpec Copy()
        {
            var copy = (ItemSpec)MemberwiseClone();
            copy.colour = (float[])colour.Clone();
            copy.accent = (float[])accent.Clone();
            return copy;
        }
    }

    /// <summary>A Celestial item's special effect (the Unusual-like top tier). Names are placeholders.</summary>
    public sealed class ItemEffect
    {
        public readonly string id;
        public readonly string displayName;
        public readonly float[] colour;

        public ItemEffect(string id, string displayName, float r, float g, float b)
        {
            this.id = id;
            this.displayName = displayName;
            colour = new[] { r, g, b };
        }
    }

    /// <summary>Names and colours for each rarity and category, as shown to the player.</summary>
    public static class ItemLook
    {
        public const int RarityCount = 6;
        public const int CategoryCount = 5;

        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "Common";
                case ItemRarity.Uncommon: return "Uncommon";
                case ItemRarity.Rare: return "Rare";
                case ItemRarity.Exotic: return "Exotic";
                case ItemRarity.Legendary: return "Legendary";
                default: return "Celestial";
            }
        }

        /// <summary>
        /// The rarity's colour. A ladder that reads at a glance and stays apart for the common kinds of colour
        /// blindness by brightness as well as hue: grey, green, blue, violet, gold, and a pale cyan-white that glows.
        /// </summary>
        public static float[] RarityColour(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return new[] { 0.72f, 0.72f, 0.70f };
                case ItemRarity.Uncommon: return new[] { 0.42f, 0.80f, 0.42f };
                case ItemRarity.Rare: return new[] { 0.32f, 0.58f, 1.00f };
                case ItemRarity.Exotic: return new[] { 0.70f, 0.42f, 1.00f };
                case ItemRarity.Legendary: return new[] { 1.00f, 0.74f, 0.25f };
                default: return new[] { 0.62f, 1.00f, 0.96f };
            }
        }

        /// <summary>The same colour as a rich-text tag, "#RRGGBB".</summary>
        public static string RarityHex(ItemRarity rarity) => Hex(RarityColour(rarity));

        public static string Hex(float[] rgb)
        {
            int r = (int)Math.Round(Clamp01(rgb[0]) * 255f), g = (int)Math.Round(Clamp01(rgb[1]) * 255f), b = (int)Math.Round(Clamp01(rgb[2]) * 255f);
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static string CategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Cosmetic: return "Cosmetic";
                case ItemCategory.WeaponSkin: return "Weapon skin";
                case ItemCategory.Upgrade: return "Upgrade";
                case ItemCategory.ShipPaint: return "Ship paint";
                default: return "Artwork";
            }
        }

        public static string SlotName(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Helmet: return "helmet";
                case CosmeticSlot.Visor: return "visor";
                case CosmeticSlot.Suit: return "suit";
                case CosmeticSlot.Pack: return "jetpack";
                case CosmeticSlot.Badge: return "badge";
                default: return "";
            }
        }

        public static string StatName(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.GunDamage: return "gun damage";
                case UpgradeStat.MoveSpeed: return "walking speed";
                case UpgradeStat.SuitAir: return "suit air";
                case UpgradeStat.SuitArmour: return "max armour";
                case UpgradeStat.JetFuel: return "jet fuel";
                case UpgradeStat.Focus: return "Focus from kills and limbs";
                case UpgradeStat.ReloadSpeed: return "reload speed";
                case UpgradeStat.ShipFuel: return "ship fuel";
                case UpgradeStat.MeleeDamage: return "melee damage";
                default: return "";
            }
        }

        /// <summary>
        /// The colour of a look earned by a goal, in place of a rarity's: the green of the unlock banner and of a target
        /// met (HullKit.Good), so the collection shows at a glance which looks came from goals.
        /// </summary>
        public static readonly float[] EarnedColour = { 0.624f, 0.878f, 0.478f };

        /// <summary>What the kind line says in place of a rarity for a look earned by a goal.</summary>
        public const string EarnedName = "Earned";

        /// <summary>The line under the name: "Rare cosmetic (helmet)", "Common weapon skin (any gun)", "Earned ship paint".</summary>
        public static string KindLine(ItemSpec item, ItemRarity shownRarity, string weaponName = null)
        {
            string kind = CategoryName(item.category).ToLowerInvariant();
            string detail = "";
            if (item.category == ItemCategory.Cosmetic) detail = SlotName(item.slot);
            else if (item.category == ItemCategory.WeaponSkin) detail = string.IsNullOrEmpty(item.weaponId) ? "any gun" : (weaponName ?? item.weaponId);
            else if (item.category == ItemCategory.Artwork && !string.IsNullOrEmpty(item.artist))
                detail = item.artist + (string.IsNullOrEmpty(item.year) ? "" : ", " + item.year);
            string tier = shownRarity != ItemRarity.Celestial && ItemCatalog.IsEarned(item.id) ? EarnedName : RarityName(shownRarity);
            return tier + " " + kind + (detail.Length > 0 ? " (" + detail + ")" : "");
        }

        /// <summary>What an upgrade does, as two short lines ("+15% jet fuel", "-10% max armour").</summary>
        public static string UpgradeUpLine(ItemSpec item) => "+" + Percent(item.upgradeUpAmount) + " " + StatName(item.upgradeUp);
        public static string UpgradeDownLine(ItemSpec item) => "-" + Percent(item.upgradeDownAmount) + " " + StatName(item.upgradeDown);

        static string Percent(float amount) => Math.Round(amount * 100f).ToString("0") + "%";
    }

    /// <summary>
    /// The chance of each rarity when an envelope is opened. Public on purpose (constitution rule 9, no opaque
    /// randomness): the envelope prints these odds. First guesses for Matthew to change. An envelope is earned only
    /// by clearing all nine antes, which most players manage rarely, so the odds lean generous.
    /// </summary>
    [Serializable]
    public sealed class PrizeOdds
    {
        public float common = 40f;
        public float uncommon = 30f;
        public float rare = 18f;
        public float exotic = 8f;
        public float legendary = 3.5f;
        public float celestial = 0.5f;

        public float Weight(ItemRarity rarity)
        {
            float w;
            switch (rarity)
            {
                case ItemRarity.Common: w = common; break;
                case ItemRarity.Uncommon: w = uncommon; break;
                case ItemRarity.Rare: w = rare; break;
                case ItemRarity.Exotic: w = exotic; break;
                case ItemRarity.Legendary: w = legendary; break;
                default: w = celestial; break;
            }
            return w > 0f && !float.IsNaN(w) && !float.IsInfinity(w) ? w : 0f;
        }

        public float Total
        {
            get
            {
                float t = 0f;
                for (int i = 0; i < ItemLook.RarityCount; i++) t += Weight((ItemRarity)i);
                return t;
            }
        }

        /// <summary>The chance as printed (40 means 40%).</summary>
        public float Percent(ItemRarity rarity)
        {
            float total = Total;
            return total <= 0f ? 0f : 100f * Weight(rarity) / total;
        }

        /// <summary>A short printed form: "40%", "3.5%", "0.5%".</summary>
        public string PercentText(ItemRarity rarity)
        {
            float p = Percent(rarity);
            return (p >= 10f || Math.Abs(p - Math.Round(p)) < 0.05f ? Math.Round(p).ToString("0") : p.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) + "%";
        }

        public void Validate(List<string> problems)
        {
            if (Total <= 0f) problems.Add("Envelope odds add up to nothing.");
        }
    }

    /// <summary>What came out of an envelope.</summary>
    public struct Prize
    {
        public string itemId;
        /// <summary>The Celestial effect, or empty.</summary>
        public string effectId;
        /// <summary>The rarity shown: the item's own, or Celestial when it carries an effect.</summary>
        public ItemRarity rarity;
        /// <summary>The player already had this exact item (and effect); its count goes up.</summary>
        public bool duplicate;

        public bool IsValid => !string.IsNullOrEmpty(itemId);
    }

    /// <summary>
    /// The weighted rarity roll. First the rarity, by the public odds; then one item of that rarity, every item of
    /// it equally likely, preferring ones the player does not have yet. A rarity with no items gives way to the
    /// nearest rarity below it that has some (then above), so the odds never land on nothing.
    /// </summary>
    public static class PrizeRoll
    {
        public const string StreamName = "envelope.prize";

        /// <summary>
        /// The random stream for one envelope. It depends on the save's own salt and the envelope's number, never
        /// on the run seed: replaying a seed must not replay its prize, and reloading cannot re-roll one.
        /// </summary>
        public static SeededRandom StreamFor(int profileSalt, int serial) => SeededRandom.ForStream(profileSalt, StreamName).Fork(serial);

        public static ItemRarity RollRarity(SeededRandom rng, PrizeOdds odds)
        {
            double roll = rng.NextDouble() * odds.Total;
            double sum = 0;
            for (int i = 0; i < ItemLook.RarityCount; i++)
            {
                sum += odds.Weight((ItemRarity)i);
                if (roll < sum) return (ItemRarity)i;
            }
            return ItemRarity.Common;
        }

        public static Prize Roll(SeededRandom rng, PrizeOdds odds, IReadOnlyList<ItemSpec> catalog, IReadOnlyList<ItemEffect> effects, ItemInventory owned)
        {
            if (catalog == null || catalog.Count == 0) return default;
            ItemRarity rarity = RollRarity(rng, odds);
            if (rarity == ItemRarity.Celestial)
            {
                Prize celestial = RollCelestial(rng, catalog, effects, owned);
                if (celestial.IsValid) return celestial;
                rarity = ItemRarity.Legendary;
            }

            ItemRarity tier = NearestStocked(catalog, rarity);
            var pool = new List<ItemSpec>();
            var fresh = new List<ItemSpec>();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (!InEnvelopes(catalog[i]) || catalog[i].rarity != tier || catalog[i].rarity == ItemRarity.Celestial) continue;
                pool.Add(catalog[i]);
                if (owned == null || !owned.Has(catalog[i].id, "")) fresh.Add(catalog[i]);
            }
            // Always draw the same number of values, so the stream's later numbers do not depend on what is owned.
            double pick = rng.NextDouble();
            List<ItemSpec> from = fresh.Count > 0 ? fresh : pool;
            ItemSpec item = from[Math.Min(from.Count - 1, (int)(pick * from.Count))];
            return new Prize { itemId = item.id, effectId = "", rarity = tier, duplicate = fresh.Count == 0 };
        }

        /// <summary>A cosmetic, weapon skin or ship paint of any rarity, plus one effect. New pairs come first.</summary>
        static Prize RollCelestial(SeededRandom rng, IReadOnlyList<ItemSpec> catalog, IReadOnlyList<ItemEffect> effects, ItemInventory owned)
        {
            double pickItem = rng.NextDouble();
            double pickEffect = rng.NextDouble();
            if (effects == null || effects.Count == 0) return default;
            var bases = new List<ItemSpec>();
            for (int i = 0; i < catalog.Count; i++)
                if (InEnvelopes(catalog[i]) && catalog[i].CanCarryEffect) bases.Add(catalog[i]);
            if (bases.Count == 0) return default;

            // Every (item, effect) pair is one ticket; pairs not owned yet are preferred.
            int pairs = bases.Count * effects.Count;
            var fresh = new List<int>();
            for (int p = 0; p < pairs; p++)
                if (owned == null || !owned.Has(bases[p / effects.Count].id, effects[p % effects.Count].id)) fresh.Add(p);
            int chosen;
            if (fresh.Count > 0) chosen = fresh[Math.Min(fresh.Count - 1, (int)(pickItem * fresh.Count))];
            else chosen = Math.Min(bases.Count - 1, (int)(pickItem * bases.Count)) * effects.Count + Math.Min(effects.Count - 1, (int)(pickEffect * effects.Count));
            return new Prize
            {
                itemId = bases[chosen / effects.Count].id,
                effectId = effects[chosen % effects.Count].id,
                rarity = ItemRarity.Celestial,
                duplicate = fresh.Count == 0,
            };
        }

        /// <summary>
        /// True for an item an envelope can hold: anything but a look earned by a goal (<see cref="ItemCatalog.IsEarned"/>),
        /// so the roll and the odds printed on the envelope are the same with or without the earned looks.
        /// </summary>
        public static bool InEnvelopes(ItemSpec item) => item != null && !ItemCatalog.IsEarned(item.id);

        /// <summary>The asked rarity if any item has it, else the nearest below, else the nearest above.</summary>
        public static ItemRarity NearestStocked(IReadOnlyList<ItemSpec> catalog, ItemRarity wanted)
        {
            int w = Math.Min((int)wanted, (int)ItemRarity.Legendary);
            for (int r = w; r >= 0; r--)
                if (Stocked(catalog, (ItemRarity)r)) return (ItemRarity)r;
            for (int r = w + 1; r <= (int)ItemRarity.Legendary; r++)
                if (Stocked(catalog, (ItemRarity)r)) return (ItemRarity)r;
            return ItemRarity.Common;
        }

        static bool Stocked(IReadOnlyList<ItemSpec> catalog, ItemRarity rarity)
        {
            for (int i = 0; i < catalog.Count; i++)
                if (InEnvelopes(catalog[i]) && catalog[i].rarity == rarity) return true;
            return false;
        }
    }

    /// <summary>One line of the player's collection. Plain fields so Unity's JsonUtility can save it.</summary>
    [Serializable]
    public sealed class OwnedItem
    {
        public string itemId = "";
        public string effectId = "";
        public int count = 1;
        /// <summary>The envelope it first came out of.</summary>
        public int firstSerial;
        /// <summary>Came from a test envelope (editor and development builds only); can be cleared in one go.</summary>
        public bool fromTest;
    }

    /// <summary>
    /// The player's collection, kept across runs in the save. Wraps the save's own list so there is one copy.
    /// An item with a Celestial effect is a separate line from the same item without one.
    /// </summary>
    public sealed class ItemInventory
    {
        readonly List<OwnedItem> _items;

        public ItemInventory(List<OwnedItem> items)
        {
            _items = items ?? new List<OwnedItem>();
        }

        public IReadOnlyList<OwnedItem> Items => _items;
        public int Lines => _items.Count;

        public int TotalCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _items.Count; i++) n += Math.Max(0, _items[i].count);
                return n;
            }
        }

        public OwnedItem Find(string itemId, string effectId)
        {
            effectId = effectId ?? "";
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].itemId == itemId && (_items[i].effectId ?? "") == effectId) return _items[i];
            return null;
        }

        public bool Has(string itemId, string effectId) => Find(itemId, effectId) != null;

        public OwnedItem Add(Prize prize, int serial, bool fromTest)
        {
            if (!prize.IsValid) return null;
            OwnedItem line = Find(prize.itemId, prize.effectId);
            if (line != null)
            {
                line.count++;
                // A real copy makes the line real.
                if (!fromTest) line.fromTest = false;
                return line;
            }
            line = new OwnedItem { itemId = prize.itemId, effectId = prize.effectId ?? "", count = 1, firstSerial = serial, fromTest = fromTest };
            _items.Add(line);
            return line;
        }

        /// <summary>Takes out every line that only ever came from test envelopes. Returns how many lines went.</summary>
        public int RemoveTestItems() => _items.RemoveAll(i => i.fromTest);

        /// <summary>Drops lines whose item no longer exists, so a renamed item cannot break the collection screen.</summary>
        public int RemoveUnknown(Func<string, bool> exists) => _items.RemoveAll(i => string.IsNullOrEmpty(i.itemId) || !exists(i.itemId));
    }
}
