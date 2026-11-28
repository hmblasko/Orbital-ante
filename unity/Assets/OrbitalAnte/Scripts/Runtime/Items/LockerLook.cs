using OrbitalAnte.Content;
using OrbitalAnte.Save;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The Unity side of the Locker (Ralph loop pass 4, 28 Nov 2026): what is in use, read from the save and the item
    /// assets, for the systems that show it. A run reads these once as its pieces are built (the lander, the suit, the
    /// run's numbers), so a change made in the Locker shows from the next run; a gun asks whenever it comes into the
    /// player's hands. An item that is in use but whose asset has gone, or no longer fits its place, counts as nothing.
    /// </summary>
    public static class LockerLook
    {
        /// <summary>The prize item with this id (from its asset, or the built-in one), or null.</summary>
        public static ItemSpec Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ItemData data = ContentDatabase.Get<ItemData>(id);
            return data != null && data.spec != null ? data.ToSpec() : null;
        }

        static ItemSpec Worn(LockerSlot slot)
        {
            LockerItem place = Locker.Worn(SaveSystem.Data, slot);
            if (place == null) return null;
            ItemSpec item = Find(place.itemId);
            return item != null && Locker.SlotOf(item) == slot ? item : null;
        }

        /// <summary>The upgrade in use, or null. Its gain and cost go into the run's numbers (<see cref="UpgradeEffects"/>).</summary>
        public static ItemSpec Upgrade() => Locker.UpgradeInUse(SaveSystem.Data, Find);

        /// <summary>The lander's paint, or null for its own white, grey and gold.</summary>
        public static HullPaint ShipPaint() => PrizeLooks.Paint(Worn(LockerSlot.Paint));

        /// <summary>What the suit's pieces in use change in the view and on the suit plate.</summary>
        public static SuitLook Suit() =>
            PrizeLooks.Suit(Worn(LockerSlot.Helmet), Worn(LockerSlot.Visor), Worn(LockerSlot.Suit), Worn(LockerSlot.Pack), Worn(LockerSlot.Badge));

        /// <summary>The skin a gun of this kind wears in the player's hands, or null for none.</summary>
        public static SkinLook? SkinFor(string gunId)
        {
            LockerSkin skin = Locker.SkinFor(SaveSystem.Data, gunId);
            if (skin == null) return null;
            ItemSpec item = Find(skin.itemId);
            if (item == null || Locker.SlotOf(item) != LockerSlot.Skin) return null;
            return PrizeLooks.Skin(item);
        }

        /// <summary>An sRGB triple (0 to 1) as a colour, or <paramref name="otherwise"/> when there is none.</summary>
        public static Color Colour(float[] c, Color otherwise, float alpha = 1f)
        {
            return c != null && c.Length == 3 ? new Color(c[0], c[1], c[2], alpha) : otherwise;
        }
    }
}
