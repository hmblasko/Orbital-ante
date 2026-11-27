using System;
using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Items;
using OrbitalAnte.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The collection, from the title screen: what the player has won, envelopes waiting to be opened, and (in the
    /// editor and development builds) test envelopes for trying the reveal before the ante loop exists. A plain list
    /// for now; the notebook and trading cards of Part B section 13 come later.
    /// </summary>
    public sealed class CollectionPage : MonoBehaviour
    {
        const int ShownLines = 10;

        public Action BackRequested;

        Canvas _canvas;
        Text _summary;
        Text _list;
        Button _open;
        Button _win;
        Button _clear;
        Button _back;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("Collection", uiRoot, 55);
            var card = UiKit.Card(_canvas.transform, "CollectionCard", 760f);
            UiKit.Heading(card, "COLLECTION", 40, 52f);
            _summary = UiKit.LayoutLabel(card, "", 20, UiKit.Cream, 28f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _list = UiKit.LayoutLabel(card, "", 18, UiKit.Cream, 300f, TextAnchor.UpperLeft, FontStyle.Normal);
            _list.supportRichText = true;
            _open = UiKit.Button(card, "Open an envelope", OpenNext, 56f, 24);
            _win = UiKit.Button(card, "Win a test envelope (testing only)", () =>
            {
                EnvelopeDirector.Instance?.WinTestEnvelope();
                Refresh();
            }, 44f, 18);
            _clear = UiKit.Button(card, "Clear test envelopes and items", () =>
            {
                EnvelopeDirector.Instance?.ClearTests();
                Refresh();
            }, 44f, 18);
            _back = UiKit.Button(card, "Back", () => BackRequested?.Invoke(), 52f, 22);
            _canvas.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            _canvas.gameObject.SetActive(visible);
            if (!visible) return;
            Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_open.gameObject.activeSelf ? _open.gameObject : _back.gameObject);
        }

        void OpenNext()
        {
            List<EnvelopeTicket> waiting = SaveSystem.Data.unopenedEnvelopes;
            if (waiting == null || waiting.Count == 0 || EnvelopeDirector.Instance == null) return;
            _canvas.gameObject.SetActive(false);
            if (!EnvelopeDirector.Instance.Open(waiting[0], () => SetVisible(true))) SetVisible(true);
        }

        void Refresh()
        {
            SaveData data = SaveSystem.Data;
            int waiting = data.unopenedEnvelopes != null ? data.unopenedEnvelopes.Count : 0;
            var inventory = new ItemInventory(data.items);
            _summary.text = inventory.TotalCount + (inventory.TotalCount == 1 ? " item" : " items") + "   ·   " + waiting +
                            (waiting == 1 ? " envelope to open" : " envelopes to open") + "   ·   " + data.envelopesOpened + " opened";
            _open.gameObject.SetActive(waiting > 0);
            UiKit.ButtonText(_open).text = waiting > 1 ? "Open an envelope (" + waiting + " waiting)" : "Open the envelope";
            bool tests = EnvelopeDirector.TestsAllowed && EnvelopeConfig.Load().offerTestEnvelopes;
            _win.gameObject.SetActive(tests);
            _clear.gameObject.SetActive(tests);
            _list.text = ListText(inventory);
        }

        static string ListText(ItemInventory inventory)
        {
            if (inventory.Lines == 0)
                return "\nNothing yet. Every run starts with a sealed envelope; clear Ante 9 and it opens.";
            var catalog = new Dictionary<string, ItemSpec>();
            foreach (ItemSpec s in EnvelopeDirector.Catalog()) catalog[s.id] = s;
            var effects = new Dictionary<string, ItemEffect>();
            foreach (ItemEffect e in ItemCatalog.CreateEffects()) effects[e.id] = e;

            // Rarest first, newest first within a rarity.
            var lines = new List<OwnedItem>(inventory.Items);
            lines.Sort((a, b) =>
            {
                int ra = RarityOf(a, catalog), rb = RarityOf(b, catalog);
                return ra != rb ? rb.CompareTo(ra) : b.firstSerial.CompareTo(a.firstSerial);
            });
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count && i < ShownLines; i++)
            {
                OwnedItem line = lines[i];
                if (!catalog.TryGetValue(line.itemId, out ItemSpec item)) continue;
                var rarity = (ItemRarity)RarityOf(line, catalog);
                sb.Append("<color=").Append(ItemLook.RarityHex(rarity)).Append('>').Append(ItemLook.RarityName(rarity)).Append("</color>  ");
                if (!string.IsNullOrEmpty(line.effectId) && effects.TryGetValue(line.effectId, out ItemEffect fx))
                    sb.Append("<color=").Append(ItemLook.Hex(fx.colour)).Append('>').Append(fx.displayName).Append("</color> ");
                sb.Append("<b>").Append(item.displayName).Append("</b>");
                if (line.count > 1) sb.Append("  x").Append(line.count);
                sb.Append("  <color=#A8A096>").Append(ItemLook.CategoryName(item.category).ToLowerInvariant());
                if (line.fromTest) sb.Append(", test");
                sb.Append("</color>\n");
            }
            if (lines.Count > ShownLines) sb.Append("<color=#A8A096>...and ").Append(lines.Count - ShownLines).Append(" more</color>");
            return sb.ToString();
        }

        static int RarityOf(OwnedItem line, Dictionary<string, ItemSpec> catalog)
        {
            if (!string.IsNullOrEmpty(line.effectId)) return (int)ItemRarity.Celestial;
            return catalog.TryGetValue(line.itemId, out ItemSpec s) ? (int)s.rarity : 0;
        }
    }
}
