using System;
using System.Collections.Generic;
using OrbitalAnte.Content;
using OrbitalAnte.Save;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The Unity side of the Locker (Ralph loop pass 4, 28 Nov 2026): what is in use, read from the save and the item
    /// assets, for the systems that show it. A world (a run, or the practice range) reads these once as its pieces are
    /// built (the lander, the suit, the run's numbers, and the skins the first time a gun comes into the player's hands),
    /// so a change made in the Collection shows from the next run. A look that goes to work in the middle of a run (one
    /// earned by a goal, or an envelope's prize at ante 9) calls <see cref="RefreshSoon"/>, and the lander's hull, the suit's
    /// display and jets, and the gun in hand put it on there and then (Ralph loop pass 5: a look is seen when it is
    /// earned). The run's numbers (an upgrade) never change mid-run. An item that is in use but whose asset has gone, or
    /// no longer fits its place, counts as nothing.
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

        /// <summary>The id of the paint in use, or "" for the lander's own: the hull is built again only when this changes.</summary>
        public static string ShipPaintId()
        {
            ItemSpec paint = Worn(LockerSlot.Paint);
            return paint != null ? paint.id : string.Empty;
        }

        /// <summary>What the suit's pieces in use change in the view and on the suit plate.</summary>
        public static SuitLook Suit() =>
            PrizeLooks.Suit(Worn(LockerSlot.Helmet), Worn(LockerSlot.Visor), Worn(LockerSlot.Suit), Worn(LockerSlot.Pack), Worn(LockerSlot.Badge));

        // The skins in use, read once a world: by gun id, "" for the skin on every gun.
        static readonly Dictionary<string, SkinLook> Skins = new Dictionary<string, SkinLook>();
        static bool _skinsRead;

        /// <summary>A new world is being built (GameBootstrap.StartRun): what is in use is read again when it is next asked for.</summary>
        public static void NewWorld()
        {
            _skinsRead = false;
            _refreshDue = false;
        }

        /// <summary>
        /// Raised by <see cref="Refresh"/>: a look went to work in the middle of a run. The hull, the suit's display, its
        /// jets and the player's guns listen (subscribed while enabled) and read what is in use again.
        /// </summary>
        public static event Action Changed;

        static bool _refreshDue;

        /// <summary>
        /// A look went to work in the middle of a run (an unlock's moment, or an envelope's prize): it is put on at the
        /// next frame that writes no save (<see cref="RefreshIfDue"/>, from AnteLoop's Update), so two looks named on one
        /// break page, or a look and the save that keeps it, cost one refresh on a frame of its own (deck-performance
        /// review, Ralph loop pass 5). The unlock moment waits out a fight for up to 20 s, so this can still fall in one.
        /// </summary>
        public static void RefreshSoon() => _refreshDue = true;

        /// <summary>Runs the refresh <see cref="RefreshSoon"/> asked for, unless this frame wrote the save. Returns whether it ran.</summary>
        public static bool RefreshIfDue(bool savedThisFrame)
        {
            if (!_refreshDue || savedThisFrame) return false;
            _refreshDue = false;
            Refresh();
            return true;
        }

        /// <summary>
        /// The skins are read again, and everything that shows a look puts it on now. Each listener changes only what
        /// changed: the hull is built again only for a new paint (a few ms on the Deck), the rest is a few colours and at
        /// most a few materials. Called through <see cref="RefreshSoon"/>.
        /// </summary>
        public static void Refresh()
        {
            _skinsRead = false;
            Changed?.Invoke();
        }

        /// <summary>The skin a gun of this kind wears in the player's hands, or null for none. A gun's own skin wins over the one for every gun.</summary>
        public static SkinLook? SkinFor(string gunId)
        {
            if (!_skinsRead) ReadSkins();
            if (!string.IsNullOrEmpty(gunId) && Skins.TryGetValue(gunId, out SkinLook own)) return own;
            if (Skins.TryGetValue("", out SkinLook every)) return every;
            return null;
        }

        static void ReadSkins()
        {
            _skinsRead = true;
            Skins.Clear();
            Loadout l = Locker.Of(SaveSystem.Data);
            for (int i = 0; i < l.skins.Count; i++)
            {
                LockerSkin skin = l.skins[i];
                ItemSpec item = skin != null ? Find(skin.itemId) : null;
                if (item == null || Locker.SlotOf(item) != LockerSlot.Skin) continue;
                // The skin's own gun, as its item says (a skin moved to another gun in the Inspector is not worn).
                string gun = string.IsNullOrEmpty(item.weaponId) ? "" : item.weaponId;
                if ((skin.gunId ?? "") != gun) continue;
                Skins[gun] = PrizeLooks.Skin(item);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Skins.Clear();
            _skinsRead = false;
            _refreshDue = false;
            Changed = null;
        }

        /// <summary>An sRGB triple (0 to 1) as a colour, or <paramref name="otherwise"/> when there is none.</summary>
        public static Color Colour(float[] c, Color otherwise, float alpha = 1f)
        {
            return c != null && c.Length == 3 ? new Color(c[0], c[1], c[2], alpha) : otherwise;
        }
    }
}
