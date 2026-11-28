using System;
using System.Collections.Generic;
using OrbitalAnte.Save;

namespace OrbitalAnte.Items
{
    // The Locker (Ralph loop pass 4, 28 Nov 2026): what the player's prizes do. Until now a prize was kept in the
    // collection and nothing more, so the envelope's promise was empty. Now an item in use changes the game: an
    // upgrade changes every run (its gain and its cost, Part B section 4's sidegrade rule), a ship paint goes on the
    // lander, a gun skin on the player's guns, and the suit's pieces on what the player sees of their own suit (the
    // edge of the helmet, the tint of the visor, the suit's readout plate, the jets' flames). Paintings are kept until
    // the cabin has frames for them. A Celestial item's effect is drawn where the item shows (pass 6, CelestialLooks):
    // on the gun in the player's hands, on the lander's hull, or round the suit's readout. Plain C#; the Unity side reads
    // what is in use as a run is built.

    /// <summary>Where a prize is used. One item at a time in each, except skins (one for every gun, and one of a gun's own per gun).</summary>
    public enum LockerSlot
    {
        None = 0,
        Upgrade = 1,
        Paint = 2,
        Skin = 3,
        Helmet = 4,
        Visor = 5,
        Suit = 6,
        Pack = 7,
        Badge = 8,
    }

    /// <summary>One item in use: which item, and which Celestial effect it carries ("" for none). Plain fields for Unity's JsonUtility.</summary>
    [Serializable]
    public sealed class LockerItem
    {
        public string itemId = "";
        public string effectId = "";

        public bool Empty => string.IsNullOrEmpty(itemId);

        public bool Is(string item, string effect) => !Empty && itemId == item && (effectId ?? "") == (effect ?? "");

        public void Set(string item, string effect)
        {
            itemId = item ?? "";
            effectId = effect ?? "";
        }

        public void Clear() => Set("", "");
    }

    /// <summary>A gun skin in use. <see cref="gunId"/> is empty for a skin on every gun, or the one gun it is on.</summary>
    [Serializable]
    public sealed class LockerSkin
    {
        public string gunId = "";
        public string itemId = "";
        public string effectId = "";
    }

    /// <summary>What is in use, kept in the save (layout 6). Empty ids mean nothing in that place.</summary>
    [Serializable]
    public sealed class Loadout
    {
        public LockerItem upgrade = new LockerItem();
        public LockerItem paint = new LockerItem();
        public LockerItem helmet = new LockerItem();
        public LockerItem visor = new LockerItem();
        public LockerItem suit = new LockerItem();
        public LockerItem pack = new LockerItem();
        public LockerItem badge = new LockerItem();
        /// <summary>At most one skin for every gun (gun id empty) and one per gun.</summary>
        public List<LockerSkin> skins = new List<LockerSkin>();

        /// <summary>Fills in anything a save written by Unity's JsonUtility left null.</summary>
        public void Repair()
        {
            if (upgrade == null) upgrade = new LockerItem();
            if (paint == null) paint = new LockerItem();
            if (helmet == null) helmet = new LockerItem();
            if (visor == null) visor = new LockerItem();
            if (suit == null) suit = new LockerItem();
            if (pack == null) pack = new LockerItem();
            if (badge == null) badge = new LockerItem();
            if (skins == null) skins = new List<LockerSkin>();
            skins.RemoveAll(s => s == null || string.IsNullOrEmpty(s.itemId));
            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i].gunId == null) skins[i].gunId = "";
                if (skins[i].effectId == null) skins[i].effectId = "";
            }
        }
    }

    /// <summary>
    /// Putting prizes to use, taking them off, and asking what is in use. Works on <see cref="SaveData"/> directly; the
    /// caller writes the save afterwards. Only items the player owns can be in use (the item and its effect, as one line
    /// of the collection).
    /// </summary>
    public static class Locker
    {
        /// <summary>The save's loadout, made whole first.</summary>
        public static Loadout Of(SaveData save)
        {
            if (save.locker == null) save.locker = new Loadout();
            save.locker.Repair();
            return save.locker;
        }

        /// <summary>Where an item is used. Artworks (and anything unknown) have no place yet: they are kept in the collection.</summary>
        public static LockerSlot SlotOf(ItemSpec item)
        {
            if (item == null) return LockerSlot.None;
            switch (item.category)
            {
                case ItemCategory.Upgrade: return LockerSlot.Upgrade;
                case ItemCategory.ShipPaint: return LockerSlot.Paint;
                case ItemCategory.WeaponSkin: return LockerSlot.Skin;
                case ItemCategory.Cosmetic:
                    switch (item.slot)
                    {
                        case CosmeticSlot.Helmet: return LockerSlot.Helmet;
                        case CosmeticSlot.Visor: return LockerSlot.Visor;
                        case CosmeticSlot.Suit: return LockerSlot.Suit;
                        case CosmeticSlot.Pack: return LockerSlot.Pack;
                        case CosmeticSlot.Badge: return LockerSlot.Badge;
                        default: return LockerSlot.None;
                    }
                default: return LockerSlot.None;
            }
        }

        /// <summary>True for an item that can be put to use (everything but artworks, for now).</summary>
        public static bool Usable(ItemSpec item) => SlotOf(item) != LockerSlot.None;

        /// <summary>The single place for every slot but skins (null for skins and None).</summary>
        static LockerItem Place(Loadout l, LockerSlot slot)
        {
            switch (slot)
            {
                case LockerSlot.Upgrade: return l.upgrade;
                case LockerSlot.Paint: return l.paint;
                case LockerSlot.Helmet: return l.helmet;
                case LockerSlot.Visor: return l.visor;
                case LockerSlot.Suit: return l.suit;
                case LockerSlot.Pack: return l.pack;
                case LockerSlot.Badge: return l.badge;
                default: return null;
            }
        }

        static string GunOf(ItemSpec skin) => skin != null && !string.IsNullOrEmpty(skin.weaponId) ? skin.weaponId : "";

        public static bool InUse(SaveData save, ItemSpec item, string effectId)
        {
            if (item == null) return false;
            Loadout l = Of(save);
            LockerSlot slot = SlotOf(item);
            if (slot == LockerSlot.Skin)
            {
                string gun = GunOf(item);
                for (int i = 0; i < l.skins.Count; i++)
                    if (l.skins[i].gunId == gun && l.skins[i].itemId == item.id && (l.skins[i].effectId ?? "") == (effectId ?? "")) return true;
                return false;
            }
            LockerItem place = Place(l, slot);
            return place != null && place.Is(item.id, effectId);
        }

        /// <summary>
        /// Puts an owned item to use in its place, taking off whatever was there. A skin with a gun of its own goes on
        /// that gun; one for any gun goes on every gun (a gun's own skin still shows on that gun). Returns false if the
        /// item is not owned (with that effect) or has no place.
        /// </summary>
        public static bool Use(SaveData save, ItemSpec item, string effectId)
        {
            if (item == null || !Usable(item)) return false;
            effectId = effectId ?? "";
            if (!new ItemInventory(save.items).Has(item.id, effectId)) return false;
            Loadout l = Of(save);
            LockerSlot slot = SlotOf(item);
            if (slot == LockerSlot.Skin)
            {
                string gun = GunOf(item);
                l.skins.RemoveAll(s => s.gunId == gun);
                l.skins.Add(new LockerSkin { gunId = gun, itemId = item.id, effectId = effectId });
                return true;
            }
            Place(l, slot).Set(item.id, effectId);
            return true;
        }

        /// <summary>Takes an item out of use, if it is in use.</summary>
        public static void TakeOff(SaveData save, ItemSpec item, string effectId)
        {
            if (item == null) return;
            effectId = effectId ?? "";
            Loadout l = Of(save);
            LockerSlot slot = SlotOf(item);
            if (slot == LockerSlot.Skin)
            {
                string gun = GunOf(item);
                l.skins.RemoveAll(s => s.gunId == gun && s.itemId == item.id && (s.effectId ?? "") == effectId);
                return;
            }
            LockerItem place = Place(l, slot);
            if (place != null && place.Is(item.id, effectId)) place.Clear();
        }

        /// <summary>The Locker's button: in use becomes not, and not becomes in use. Returns whether it is in use now.</summary>
        public static bool Toggle(SaveData save, ItemSpec item, string effectId)
        {
            if (InUse(save, item, effectId))
            {
                TakeOff(save, item, effectId);
                return false;
            }
            return Use(save, item, effectId);
        }

        /// <summary>
        /// A prize just won goes into use by itself when its place is empty, so it shows the next time the player
        /// plays. Never an upgrade (every upgrade has a cost, so taking one on is the player's choice) and never over
        /// something already in use, except that a look earned by a goal gives its place to an envelope's prize: the
        /// earned looks fill their places early, and the ante 9 reveal would otherwise go to work for nobody (Unity
        /// review, Ralph loop pass 5). The earned look stays in the collection. Returns whether it went into use.
        /// </summary>
        public static bool AutoUse(SaveData save, ItemSpec item, string effectId)
        {
            if (item == null || !Usable(item) || SlotOf(item) == LockerSlot.Upgrade) return false;
            bool earned = ItemCatalog.IsEarned(item.id);
            Loadout l = Of(save);
            LockerSlot slot = SlotOf(item);
            if (slot == LockerSlot.Skin)
            {
                string gun = GunOf(item);
                for (int i = 0; i < l.skins.Count; i++)
                    if (l.skins[i].gunId == gun && !GivesWay(l.skins[i].itemId, earned)) return false;
            }
            else
            {
                LockerItem place = Place(l, slot);
                if (!place.Empty && !GivesWay(place.itemId, earned)) return false;
            }
            return Use(save, item, effectId);
        }

        /// <summary>Whether what is in a place gives way to a prize going to work by itself: only an earned look, and only to an envelope's prize.</summary>
        static bool GivesWay(string inUseId, bool newIsEarned) => !newIsEarned && ItemCatalog.IsEarned(inUseId);

        /// <summary>
        /// Takes out of use anything no longer owned (test items cleared), no longer in the catalogue, or no longer
        /// fitting its place (an asset changed in the Inspector). Returns how many places were emptied.
        /// </summary>
        public static int Tidy(SaveData save, Func<string, ItemSpec> find)
        {
            Loadout l = Of(save);
            var owned = new ItemInventory(save.items);
            int gone = 0;
            LockerSlot[] singles = { LockerSlot.Upgrade, LockerSlot.Paint, LockerSlot.Helmet, LockerSlot.Visor, LockerSlot.Suit, LockerSlot.Pack, LockerSlot.Badge };
            for (int i = 0; i < singles.Length; i++)
            {
                LockerItem place = Place(l, singles[i]);
                if (place.Empty) continue;
                ItemSpec item = find != null ? find(place.itemId) : null;
                if (item == null || SlotOf(item) != singles[i] || !owned.Has(place.itemId, place.effectId))
                {
                    place.Clear();
                    gone++;
                }
            }
            gone += l.skins.RemoveAll(s =>
            {
                ItemSpec item = find != null ? find(s.itemId) : null;
                return item == null || SlotOf(item) != LockerSlot.Skin || GunOf(item) != (s.gunId ?? "") || !owned.Has(s.itemId, s.effectId);
            });
            return gone;
        }

        /// <summary>What is in one place, or null when it is empty (not for skins: see <see cref="SkinFor"/>).</summary>
        public static LockerItem Worn(SaveData save, LockerSlot slot)
        {
            LockerItem place = Place(Of(save), slot);
            return place != null && !place.Empty ? place : null;
        }

        /// <summary>The upgrade in use, or null.</summary>
        public static ItemSpec UpgradeInUse(SaveData save, Func<string, ItemSpec> find)
        {
            LockerItem place = Worn(save, LockerSlot.Upgrade);
            if (place == null || find == null) return null;
            ItemSpec item = find(place.itemId);
            return item != null && item.category == ItemCategory.Upgrade ? item : null;
        }

        /// <summary>The skin a gun wears: its own skin if one is in use, otherwise the skin for every gun, otherwise none.</summary>
        public static LockerSkin SkinFor(SaveData save, string gunId)
        {
            Loadout l = Of(save);
            LockerSkin every = null;
            for (int i = 0; i < l.skins.Count; i++)
            {
                LockerSkin s = l.skins[i];
                if (!string.IsNullOrEmpty(gunId) && s.gunId == gunId) return s;
                if (string.IsNullOrEmpty(s.gunId)) every = s;
            }
            return every;
        }

        // ------------------------------------------------------------------ words

        /// <summary>
        /// Where an item goes when it is in use, after "in use" or "now": "on your lander", "on every gun", "on the
        /// Test Pistol", "in every run". Empty for an item with no place.
        /// </summary>
        public static string WhereWords(ItemSpec item, string gunName = null)
        {
            switch (SlotOf(item))
            {
                case LockerSlot.Upgrade: return "in every run";
                case LockerSlot.Paint: return "on your lander";
                case LockerSlot.Skin:
                    return string.IsNullOrEmpty(item.weaponId) ? "on every gun" : "on the " + (string.IsNullOrEmpty(gunName) ? item.weaponId.ToLowerInvariant() : gunName);
                case LockerSlot.Helmet: return "on your helmet";
                case LockerSlot.Visor: return "on your visor";
                // Both show on the suit's readout plate, lower left: the player never sees the suit itself (design review, pass 5).
                case LockerSlot.Suit: return "on your suit's readout";
                case LockerSlot.Pack: return "on your jetpack";
                case LockerSlot.Badge: return "on your suit's readout";
                default: return "";
            }
        }

        /// <summary>An upgrade's name with its gain and cost: "Big Lungs (+12% suit air, -5% move speed)".</summary>
        public static string UpgradeWords(ItemSpec upgrade)
        {
            if (upgrade == null) return "";
            return upgrade.displayName + " (" + ItemLook.UpgradeUpLine(upgrade) + ", " + ItemLook.UpgradeDownLine(upgrade) + ")";
        }

        /// <summary>
        /// The line under a prize as it is revealed: where it went into use by itself, or how to put it to use. Empty
        /// for a painting (which has no place yet). A look put to use in the middle of a run goes on there and then
        /// (<c>LockerLook.Refresh</c>). <paramref name="inPlaceOf"/>: the name of what is in its place
        /// (<see cref="InPlaceOf"/>) when it did not go in, or of the earned look it took the place of when it did.
        /// </summary>
        public static string RevealWords(ItemSpec item, bool inUse, string gunName = null, string inPlaceOf = null)
        {
            if (!Usable(item)) return "";
            if (inUse)
                return "In use " + WhereWords(item, gunName)
                       + (string.IsNullOrEmpty(inPlaceOf) ? "." : ", in place of " + inPlaceOf + " (still in your Collection).");
            if (SlotOf(item) == LockerSlot.Upgrade)
                return "Every upgrade has a cost, so it waits for you: put it to use from Collection on the title screen.";
            if (!string.IsNullOrEmpty(inPlaceOf))
                return "Your " + inPlaceOf + " is in its place: swap this in from Collection on the title screen.";
            return "Something is already in its place: swap it in from Collection on the title screen.";
        }

        /// <summary>
        /// Where a Celestial effect on this item is seen, said in brackets after the effect's name on the reveal and in the
        /// Collection (Ralph loop pass 6): "on every gun" for a skin for every gun, "on that gun" for a gun's own skin (the
        /// line above it names the gun; the one gun that can blow up, the Wildcard, never shows an effect), "on your lander"
        /// (its painted hull), "round your suit's readout" (beads of light round the plate, for every suit piece: the
        /// player never sees the suit itself). Empty for an item that cannot carry one.
        /// </summary>
        public static string EffectWhere(ItemSpec item)
        {
            switch (SlotOf(item))
            {
                case LockerSlot.Skin: return string.IsNullOrEmpty(item.weaponId) ? "on every gun" : "on that gun";
                case LockerSlot.Paint: return "on your lander";
                case LockerSlot.Helmet:
                case LockerSlot.Visor:
                case LockerSlot.Suit:
                case LockerSlot.Pack:
                case LockerSlot.Badge: return "round your suit's readout";
                default: return "";
            }
        }

        /// <summary>A Celestial effect's line on the reveal and in the Collection: "Effect: Aurora (on every gun)".</summary>
        public static string EffectWords(ItemSpec item, string effectName)
        {
            if (string.IsNullOrEmpty(effectName)) return "";
            string where = EffectWhere(item);
            return "Effect: " + effectName + (where.Length > 0 ? " (" + where + ")" : "");
        }

        /// <summary>
        /// The Celestial effects on the items in use in these places, in the order given, each once: only items that
        /// exist and still fit their place, and only effects the game draws (<see cref="CelestialLooks.Effect"/>). Single
        /// places only (not gun skins, which go by gun). Empty when there are none.
        /// </summary>
        public static List<string> EffectsInUse(SaveData save, Func<string, ItemSpec> find, params LockerSlot[] slots)
        {
            var effects = new List<string>();
            if (find == null || slots == null) return effects;
            for (int i = 0; i < slots.Length; i++)
            {
                LockerItem place = Worn(save, slots[i]);
                if (place == null || string.IsNullOrEmpty(place.effectId) || effects.Contains(place.effectId)) continue;
                ItemSpec item = find(place.itemId);
                if (item == null || SlotOf(item) != slots[i] || CelestialLooks.Effect(place.effectId) == null) continue;
                effects.Add(place.effectId);
            }
            return effects;
        }

        /// <summary>
        /// Editor and development builds only (the Collection's test row, "Try the next effect"): puts the next Celestial
        /// effect to use on an every-gun skin, a ship paint and a suit piece, as test items, so each of the six can be seen
        /// in the game without opening some two hundred envelopes (Celestial is half a per cent of them). Each press brings
        /// the effect after the one the lander wears now, in the catalogue's order. The lines are test items, cleared with
        /// the rest (<see cref="EnvelopeBook.ClearTests"/>, then <see cref="Tidy"/>); a real line the player already owns is
        /// used as it is, never counted up. Returns the effect's id, or "" when nothing could be put to use.
        /// </summary>
        public static string TryNextEffect(SaveData save, IReadOnlyList<ItemSpec> catalog, IReadOnlyList<ItemEffect> effects)
        {
            if (save == null || catalog == null || effects == null || effects.Count == 0) return "";
            if (save.items == null) save.items = new List<OwnedItem>();
            Loadout l = Of(save);
            int at = -1;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i] != null && effects[i].id == l.paint.effectId) at = i;
            ItemEffect effect = effects[(at + 1) % effects.Count];
            if (effect == null) return "";
            var owned = new ItemInventory(save.items);
            bool any = false;
            foreach (ItemSpec item in TestBases(catalog))
            {
                if (!owned.Has(item.id, effect.id))
                    owned.Add(new Prize { itemId = item.id, effectId = effect.id, rarity = ItemRarity.Celestial }, 0, true);
                any |= Use(save, item, effect.id);
            }
            return any ? effect.id : "";
        }

        /// <summary>The items <see cref="TryNextEffect"/> puts an effect on: an every-gun skin, a ship paint and a suit piece (a badge if there is one).</summary>
        static List<ItemSpec> TestBases(IReadOnlyList<ItemSpec> catalog)
        {
            ItemSpec skin = null, paint = null, suit = null;
            for (int i = 0; i < catalog.Count; i++)
            {
                ItemSpec item = catalog[i];
                if (item == null || !item.CanCarryEffect || !PrizeRoll.InEnvelopes(item)) continue;
                LockerSlot slot = SlotOf(item);
                if (slot == LockerSlot.Skin && string.IsNullOrEmpty(item.weaponId)) { if (skin == null) skin = item; }
                else if (slot == LockerSlot.Paint) { if (paint == null) paint = item; }
                else if (slot == LockerSlot.Badge) suit = suit != null && SlotOf(suit) == LockerSlot.Badge ? suit : item;
                else if (slot >= LockerSlot.Helmet && slot <= LockerSlot.Pack && suit == null) suit = item;
            }
            var bases = new List<ItemSpec>(3);
            if (skin != null) bases.Add(skin);
            if (paint != null) bases.Add(paint);
            if (suit != null) bases.Add(suit);
            return bases;
        }

        /// <summary>
        /// The Collection's line on whether an item is in use and what pressing it does: "In use. Press to take it off."
        /// / "Press to use it instead of Sunset Visor." / "Press to put it to use." Empty for a painting.
        /// </summary>
        public static string StateWords(SaveData save, ItemSpec item, string effectId, Func<string, ItemSpec> find)
        {
            if (!Usable(item)) return "";
            if (InUse(save, item, effectId)) return "In use. Press to take it off.";
            string instead = InPlaceOf(save, item, find);
            return instead.Length > 0 ? "Press to use it instead of " + instead + "." : "Press to put it to use.";
        }

        /// <summary>The name of what an item would take the place of if it were put to use, or "" for an empty place.</summary>
        public static string InPlaceOf(SaveData save, ItemSpec item, Func<string, ItemSpec> find)
        {
            LockerSlot slot = SlotOf(item);
            string id = "";
            if (slot == LockerSlot.Skin)
            {
                string gun = GunOf(item);
                Loadout l = Of(save);
                for (int i = 0; i < l.skins.Count; i++) if (l.skins[i].gunId == gun) id = l.skins[i].itemId;
            }
            else
            {
                LockerItem place = Worn(save, slot);
                if (place != null) id = place.itemId;
            }
            if (id.Length == 0) return "";
            ItemSpec there = find != null ? find(id) : null;
            return there != null ? there.displayName : "";
        }

        /// <summary>What an item does in the game, in one or two short sentences, for the Locker's details.</summary>
        public static string DoesWords(ItemSpec item, string gunName = null)
        {
            switch (SlotOf(item))
            {
                case LockerSlot.Upgrade:
                    return ItemLook.UpgradeUpLine(item) + " and " + ItemLook.UpgradeDownLine(item) + " in every run while it is in use. One upgrade at a time.";
                case LockerSlot.Paint: return "Paints your lander's hull.";
                case LockerSlot.Skin:
                    return string.IsNullOrEmpty(item.weaponId)
                        ? "Goes on every gun you carry. A gun's own skin takes its place on that gun."
                        : "Goes on the " + (string.IsNullOrEmpty(gunName) ? item.weaponId.ToLowerInvariant() : gunName) + " whenever you carry one.";
                case LockerSlot.Helmet: return "Shades the edge of your view outside the ship.";
                case LockerSlot.Visor: return "Tints your view outside the ship.";
                case LockerSlot.Suit: return "Paints your suit's readout plate.";
                case LockerSlot.Pack: return "Colours your jetpack's flames, and the glow of the jets on your visor.";
                case LockerSlot.Badge: return "Sewn onto your suit's readout plate.";
                default: return "Kept in your collection.";
            }
        }
    }
}
