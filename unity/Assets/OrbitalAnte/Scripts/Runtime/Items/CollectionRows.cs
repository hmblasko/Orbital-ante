using System;
using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Ante;
using OrbitalAnte.Save;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The Collection page's rows (Ralph loop pass 4): every line the player owns, grouped by where it is used (in the
    /// Locker's order: the upgrade, the lander, the guns, the suit's pieces, then paintings), rarest first within a
    /// group, newest first within a rarity; six to a page. Plain C#, so the order and the words are tested.
    /// </summary>
    public static class CollectionRows
    {
        public const int PerPage = 6;

        /// <summary>The rarity a line is shown at: a line with an effect is Celestial whatever its item.</summary>
        public static ItemRarity RarityOf(OwnedItem line, ItemSpec item)
        {
            if (line != null && !string.IsNullOrEmpty(line.effectId)) return ItemRarity.Celestial;
            return item != null ? item.rarity : ItemRarity.Common;
        }

        /// <summary>The colour of a row's stripe: its rarity's, or the earned cream for a look earned by a goal.</summary>
        public static float[] StripeColour(OwnedItem line, ItemSpec item)
        {
            ItemRarity rarity = RarityOf(line, item);
            return rarity != ItemRarity.Celestial && item != null && ItemCatalog.IsEarned(item.id) ? ItemLook.EarnedColour : ItemLook.RarityColour(rarity);
        }

        /// <summary>
        /// What the page says with nothing in the collection: the next look a goal gives, then the envelope. A new player
        /// reads this long before an envelope can open (only after ante 9), so the near thing comes first.
        /// </summary>
        public static string EmptyWords(SaveData save, UnlockHints hints = null)
        {
            const string Envelope = "Every run also starts with a sealed envelope: clear Ante 9 and it opens.";
            UnlockDefinition next = UnlockBook.NextLook(save);
            if (next == null) return "Nothing yet. " + Envelope;
            return "Nothing yet. Looks are earned by goals in a run. The next: " + UnlockCatalog.NextWords(next, hints)
                   + " (Unlocks on the title screen says how).\n" + Envelope;
        }

        /// <summary>The group a line sorts into: the Locker's places in order, then anything with no place.</summary>
        static int Group(ItemSpec item)
        {
            LockerSlot slot = Locker.SlotOf(item);
            return slot == LockerSlot.None ? 99 : (int)slot;
        }

        /// <summary>The owned lines whose item is known, in the page's order.</summary>
        public static List<OwnedItem> Sorted(ItemInventory inventory, Func<string, ItemSpec> find)
        {
            var lines = new List<OwnedItem>();
            if (inventory == null) return lines;
            var items = new Dictionary<string, ItemSpec>();
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                OwnedItem line = inventory.Items[i];
                if (line == null || string.IsNullOrEmpty(line.itemId)) continue;
                if (!items.TryGetValue(line.itemId, out ItemSpec item))
                {
                    item = find != null ? find(line.itemId) : null;
                    items[line.itemId] = item;
                }
                if (item != null) lines.Add(line);
            }
            lines.Sort((a, b) =>
            {
                ItemSpec ia = items[a.itemId], ib = items[b.itemId];
                int ga = Group(ia), gb = Group(ib);
                if (ga != gb) return ga.CompareTo(gb);
                int ra = (int)RarityOf(a, ia), rb = (int)RarityOf(b, ib);
                if (ra != rb) return rb.CompareTo(ra);
                return b.firstSerial.CompareTo(a.firstSerial);
            });
            return lines;
        }

        public static int Pages(int lines) => Math.Max(1, (Math.Max(0, lines) + PerPage - 1) / PerPage);

        /// <summary>A page kept inside the pages there are.</summary>
        public static int ClampPage(int page, int lines) => Math.Max(0, Math.Min(page, Pages(lines) - 1));

        /// <summary>A row's words: "Comet Trail Fishbowl Dome · helmet x2", "Leg Day · badge · earned".</summary>
        public static string RowWords(ItemSpec item, OwnedItem line, string effectName, string gunName = null)
        {
            if (item == null) return "";
            var s = new StringBuilder();
            if (!string.IsNullOrEmpty(effectName)) s.Append(effectName).Append(' ');
            s.Append(item.displayName).Append("  · ").Append(KindWords(item, gunName));
            // Said in words as well as by the stripe's colour (design review, Ralph loop pass 5).
            if (ItemCatalog.IsEarned(item.id)) s.Append("  · earned");
            if (line != null && line.count > 1) s.Append("  x").Append(line.count);
            return s.ToString();
        }

        /// <summary>A short name for what an item is: "upgrade", "ship paint", "skin, every gun", "skin, pistol", "helmet", "painting".</summary>
        public static string KindWords(ItemSpec item, string gunName = null)
        {
            if (item == null) return "";
            switch (item.category)
            {
                case ItemCategory.Upgrade: return "upgrade";
                case ItemCategory.ShipPaint: return "ship paint";
                case ItemCategory.WeaponSkin:
                    return string.IsNullOrEmpty(item.weaponId) ? "skin, every gun"
                        : "skin, " + (string.IsNullOrEmpty(gunName) ? item.weaponId.ToLowerInvariant() : gunName.ToLowerInvariant());
                case ItemCategory.Cosmetic: return ItemLook.SlotName(item.slot);
                default: return "painting";
            }
        }

        /// <summary>How many of the owned lines are in use.</summary>
        public static int InUseCount(SaveData save, List<OwnedItem> lines, Func<string, ItemSpec> find)
        {
            int n = 0;
            if (save == null || lines == null) return 0;
            for (int i = 0; i < lines.Count; i++)
            {
                ItemSpec item = find != null ? find(lines[i].itemId) : null;
                if (item != null && Locker.InUse(save, item, lines[i].effectId)) n++;
            }
            return n;
        }

        /// <summary>
        /// The details under the rows for one line: what it is, what it does in the game, whether it is in use and
        /// what choosing it does, and (for a painting) where it will go.
        /// </summary>
        public static string Details(SaveData save, ItemSpec item, OwnedItem line, string effectName, Func<string, ItemSpec> find, string gunName = null)
        {
            if (item == null) return "";
            var s = new StringBuilder();
            s.Append(ItemLook.KindLine(item, RarityOf(line, item), gunName)).Append('\n');
            if (Locker.Usable(item)) s.Append(Locker.DoesWords(item, gunName)).Append('\n').Append(Locker.StateWords(save, item, line != null ? line.effectId : "", find));
            else s.Append(item.blurb).Append('\n').Append("Kept for the cabin wall: no frame to hang it in yet.");
            var extra = new List<string>();
            UnlockDefinition goal = ItemCatalog.IsEarned(item.id) ? UnlockCatalog.ForTarget(item.id) : null;
            if (goal != null) extra.Add("Earned for " + UnlockCatalog.ShortGoal(goal.goal));
            if (!string.IsNullOrEmpty(effectName)) extra.Add("Effect: " + effectName + " (" + Locker.EffectNotShown + ")");
            if (line != null && line.count > 1) extra.Add("You have " + line.count);
            if (line != null && line.fromTest) extra.Add("Test item");
            if (extra.Count > 0) s.Append('\n').Append(string.Join("  ·  ", extra));
            return s.ToString();
        }
    }
}
