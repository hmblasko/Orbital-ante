using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Items;
using OrbitalAnte.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The collection, from the title screen: what the player has won, what is in use, envelopes waiting to be opened,
    /// and (in the editor and development builds) test envelopes for trying the reveal. Since Ralph loop pass 4 every
    /// prize but a painting goes to work from here (Items/Locker.cs): choose a row to put it to use, or to take it off.
    /// The details under the rows follow the chosen row (D-pad, stick or mouse) and say what the prize does in the
    /// game. The notebook and trading cards of Part B section 13 come later.
    /// </summary>
    public sealed class CollectionPage : MonoBehaviour
    {
        public Action BackRequested;

        Canvas _canvas;
        Text _summary;
        Text _details;
        Text _pageLabel;
        RectTransform _pageRow;
        Button _open;
        Button _prev, _next;
        Button _back;
        RectTransform _tests;
        LayoutElement _rowsSpace;
        readonly Button[] _rows = new Button[CollectionRows.PerPage];
        readonly Text[] _rowWords = new Text[CollectionRows.PerPage];
        readonly Text[] _rowState = new Text[CollectionRows.PerPage];
        readonly ChosenInk[] _rowInk = new ChosenInk[CollectionRows.PerPage];
        readonly Image[] _rowRarity = new Image[CollectionRows.PerPage];

        List<OwnedItem> _lines = new List<OwnedItem>();
        readonly Dictionary<string, ItemSpec> _catalog = new Dictionary<string, ItemSpec>();
        readonly Dictionary<string, ItemEffect> _effects = new Dictionary<string, ItemEffect>();
        int _page;
        GameObject _shownFor;
        bool _detailsDirty;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("Collection", uiRoot, 55);
            var card = UiKit.Card(_canvas.transform, "CollectionCard", 760f);
            UiKit.Heading(card, "COLLECTION", 40, 52f);
            _summary = UiKit.LayoutLabel(card, "", 18, UiKit.Cream, 26f, TextAnchor.MiddleCenter, FontStyle.Normal);

            RectTransform rows = UiKit.NewRect("Rows", card);
            UiKit.VStack(rows, RowGap, 0);
            // A short last page keeps the full page's height, so the card never jumps as the pages turn.
            _rowsSpace = rows.gameObject.AddComponent<LayoutElement>();
            for (int i = 0; i < CollectionRows.PerPage; i++) BuildRow(rows, i);

            _pageRow = UiKit.NewRect("Pages", card);
            UiKit.HStack(_pageRow, 10f);
            UiKit.SetLayout(_pageRow.gameObject, 36f);
            _prev = UiKit.Button(_pageRow, "Previous page", () => TurnPage(-1), 36f, 17);
            UiKit.SetLayout(_prev.gameObject, 36f, 190f);
            _pageLabel = UiKit.Label(_pageRow, "", 16, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.SetLayout(_pageLabel.gameObject, 36f, 200f, 1f);
            _next = UiKit.Button(_pageRow, "Next page", () => TurnPage(1), 36f, 17);
            UiKit.SetLayout(_next.gameObject, 36f, 190f);

            _details = UiKit.LayoutLabel(card, "", 15, UiKit.Cream, 92f, TextAnchor.UpperLeft, FontStyle.Normal);
            _details.supportRichText = true;
            _open = UiKit.Button(card, "Open an envelope", OpenNext, 50f, 22);

            // Testing only: all on one row, so the page still fits a 16:9 screen with them showing. "Try the next effect"
            // puts the next Celestial effect on a test gun skin, ship paint and suit piece, in use (Locker.TryNextEffect),
            // so each effect can be checked in a run; "Clear tests" takes test envelopes and items away and out of use.
            _tests = UiKit.NewRect("Tests", card);
            UiKit.HStack(_tests, 10f);
            UiKit.SetLayout(_tests.gameObject, 38f);
            Button win = UiKit.Button(_tests, "Win a test envelope", () =>
            {
                EnvelopeDirector.Instance?.WinTestEnvelope();
                Refresh();
            }, 38f, 16);
            UiKit.SetLayout(win.gameObject, 38f, 220f, 1f);
            Button effect = UiKit.Button(_tests, "Try the next effect", () =>
            {
                EnvelopeDirector.Instance?.TryNextEffect();
                Refresh();
            }, 38f, 16);
            UiKit.SetLayout(effect.gameObject, 38f, 220f, 1f);
            Button clear = UiKit.Button(_tests, "Clear tests", () =>
            {
                EnvelopeDirector.Instance?.ClearTests();
                Refresh();
            }, 38f, 16);
            UiKit.SetLayout(clear.gameObject, 38f, 220f, 1f);

            _back = UiKit.Button(card, "Back", () => BackRequested?.Invoke(), 46f, 22);
            _canvas.gameObject.SetActive(false);
        }

        const float RowHeight = 36f, RowGap = 4f;

        void BuildRow(Transform parent, int i)
        {
            int slot = i;
            Button row = UiKit.Button(parent, "", () => Choose(slot), RowHeight, 17);
            row.gameObject.AddComponent<SelectOnHover>();
            Text words = UiKit.ButtonText(row);
            words.alignment = TextAnchor.MiddleLeft;
            var wordsRect = (RectTransform)words.transform;
            wordsRect.offsetMin = new Vector2(26f, 0f);
            wordsRect.offsetMax = new Vector2(-112f, 0f);
            words.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The rarity as a stripe down the left edge, like the edge of a trading card.
            RectTransform stripe = UiKit.NewRect("Rarity", row.transform);
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.sizeDelta = new Vector2(7f, -12f);
            stripe.anchoredPosition = new Vector2(9f, 0f);
            _rowRarity[i] = UiKit.AddImage(stripe, Color.white);

            // IN USE on the right, in the target-met green; dark on the chosen row's orange, like the words.
            Text state = UiKit.Label(row.transform, "", 15, HullKit.Good, TextAnchor.MiddleRight, FontStyle.Bold);
            var stateRect = (RectTransform)state.transform;
            stateRect.anchorMin = new Vector2(1f, 0f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.pivot = new Vector2(1f, 0.5f);
            stateRect.sizeDelta = new Vector2(100f, 0f);
            stateRect.anchoredPosition = new Vector2(-14f, 0f);
            var ink = row.gameObject.AddComponent<ChosenInk>();
            ink.Label = state;
            _rowInk[i] = ink;

            _rows[i] = row;
            _rowWords[i] = words;
            _rowState[i] = state;
        }

        public void SetVisible(bool visible)
        {
            _canvas.gameObject.SetActive(visible);
            if (!visible) return;
            // An earned look missing from the collection comes back (it was earned, so it is owned); then anything in use
            // that has gone from the collection (or from the game) comes out of use.
            int mended = Ante.UnlockBook.KeepEarnedLooks(SaveSystem.Data);
            int tidied = Locker.Tidy(SaveSystem.Data, LockerLook.Find);
            if (mended + tidied > 0) SaveSystem.Save();
            _page = 0;
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstChoice());
        }

        // The button a page starts on: an envelope waiting to be opened, else the first prize, else Back.
        GameObject FirstChoice()
        {
            return _open.gameObject.activeSelf ? _open.gameObject
                : _rows[0].gameObject.activeSelf ? _rows[0].gameObject : _back.gameObject;
        }

        void OpenNext()
        {
            List<EnvelopeTicket> waiting = SaveSystem.Data.unopenedEnvelopes;
            if (waiting == null || waiting.Count == 0 || EnvelopeDirector.Instance == null) return;
            _canvas.gameObject.SetActive(false);
            if (!EnvelopeDirector.Instance.Open(waiting[0], () => SetVisible(true))) SetVisible(true);
        }

        void TurnPage(int by)
        {
            int pages = CollectionRows.Pages(_lines.Count);
            _page = (_page + by + pages) % pages;
            ShowRows();
            // The choice stays on the button that turned the page, so it can be pressed again.
            _detailsDirty = true;
        }

        /// <summary>A row was chosen: its prize goes into use, or out of it, and the save is written.</summary>
        void Choose(int slot)
        {
            int index = _page * CollectionRows.PerPage + slot;
            if (index < 0 || index >= _lines.Count) return;
            OwnedItem line = _lines[index];
            ItemSpec item = Find(line.itemId);
            if (item == null || !Locker.Usable(item)) return;
            Locker.Toggle(SaveSystem.Data, item, line.effectId);
            SaveSystem.Save();
            ShowRows();
            _detailsDirty = true;
        }

        void Refresh()
        {
            SaveData data = SaveSystem.Data;
            _catalog.Clear();
            foreach (ItemSpec s in EnvelopeDirector.Catalog()) _catalog[s.id] = s;
            _effects.Clear();
            foreach (ItemEffect e in ItemCatalog.CreateEffects()) _effects[e.id] = e;
            _lines = CollectionRows.Sorted(new ItemInventory(data.items), Find);
            _page = CollectionRows.ClampPage(_page, _lines.Count);

            int waiting = data.unopenedEnvelopes != null ? data.unopenedEnvelopes.Count : 0;
            _open.gameObject.SetActive(waiting > 0);
            UiKit.ButtonText(_open).text = waiting > 1 ? "Open an envelope (" + waiting + " waiting)" : "Open the envelope";
            _tests.gameObject.SetActive(EnvelopeDirector.TestsAllowed && EnvelopeConfig.Load().offerTestEnvelopes);
            ShowRows();
            _detailsDirty = true;
        }

        void ShowRows()
        {
            SaveData data = SaveSystem.Data;
            int total = 0;
            for (int i = 0; i < _lines.Count; i++) total += Mathf.Max(1, _lines[i].count);
            int inUse = CollectionRows.InUseCount(data, _lines, Find);
            int waiting = data.unopenedEnvelopes != null ? data.unopenedEnvelopes.Count : 0;
            _summary.text = total + (total == 1 ? " item" : " items") + "   ·   " + inUse + " in use   ·   " + waiting +
                            (waiting == 1 ? " envelope to open" : " envelopes to open");

            int first = _page * CollectionRows.PerPage;
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = first + i;
                bool shown = index < _lines.Count;
                if (_rows[i].gameObject.activeSelf != shown) _rows[i].gameObject.SetActive(shown);
                if (!shown) continue;
                OwnedItem line = _lines[index];
                ItemSpec item = Find(line.itemId);
                _rowWords[i].text = CollectionRows.RowWords(item, line, EffectName(line), GunName(item));
                // IN USE in green; a painting, which has no place to go to work yet, says KEPT in the dim colour.
                bool kept = item != null && !Locker.Usable(item);
                _rowState[i].text = kept ? "KEPT" : item != null && Locker.InUse(data, item, line.effectId) ? "IN USE" : "";
                _rowInk[i].SetColour(kept ? HullKit.Dim : HullKit.Good);
                _rowRarity[i].color = ToColour(CollectionRows.StripeColour(line, item));
            }
            _rowsSpace.minHeight = _lines.Count > 0 ? CollectionRows.PerPage * RowHeight + (CollectionRows.PerPage - 1) * RowGap : 0f;
            int pages = CollectionRows.Pages(_lines.Count);
            bool paging = pages > 1;
            if (_pageRow.gameObject.activeSelf != paging) _pageRow.gameObject.SetActive(paging);
            _pageLabel.text = "Page " + (_page + 1) + " of " + pages;
            // A chosen row that has gone (the last page is shorter) hands the choice to the first row.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && !EventSystem.current.currentSelectedGameObject.activeInHierarchy && _canvas.gameObject.activeSelf)
                EventSystem.current.SetSelectedGameObject(_lines.Count > 0 ? _rows[0].gameObject : _back.gameObject);
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf || EventSystem.current == null || GameInput.Asset == null) return;
            // B / Esc / Backspace goes back to the title, as on the title's own pages. (While an envelope opens, this
            // page is hidden, so the press that ends the reveal never reaches it.)
            if (GameInput.UiCancel.WasPressedThisFrame())
            {
                BackRequested?.Invoke();
                return;
            }
            GameObject chosen = EventSystem.current.currentSelectedGameObject;
            // A click on empty space clears the choice; the next push of the D-pad or stick chooses again. Only a
            // push, not A: a press would land on the button it chose and open an envelope nobody asked to open.
            if ((chosen == null || !chosen.activeInHierarchy) && GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f)
            {
                chosen = FirstChoice();
                EventSystem.current.SetSelectedGameObject(chosen);
            }
            if (chosen == _shownFor && !_detailsDirty) return;
            _shownFor = chosen;
            _detailsDirty = false;
            _details.text = DetailsFor(chosen);
        }

        string DetailsFor(GameObject chosen)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].gameObject != chosen) continue;
                int index = _page * CollectionRows.PerPage + i;
                if (index >= _lines.Count) break;
                OwnedItem line = _lines[index];
                ItemSpec item = Find(line.itemId);
                return CollectionRows.Details(SaveSystem.Data, item, line, EffectName(line), Find, GunName(item));
            }
            if (_lines.Count == 0) return CollectionRows.EmptyWords(SaveSystem.Data);
            return "Press a prize to put it to use, or to take it off. One upgrade at a time, and every upgrade has a cost.\n" +
                   "What you change here shows from your next run.";
        }

        ItemSpec Find(string id) => id != null && _catalog.TryGetValue(id, out ItemSpec s) ? s : null;

        string EffectName(OwnedItem line)
        {
            if (line == null || string.IsNullOrEmpty(line.effectId)) return "";
            return _effects.TryGetValue(line.effectId, out ItemEffect fx) ? fx.displayName : "";
        }

        static string GunName(ItemSpec item)
        {
            if (item == null || item.category != ItemCategory.WeaponSkin || string.IsNullOrEmpty(item.weaponId)) return null;
            var gun = Content.ContentDatabase.Get<Weapons.WeaponData>(item.weaponId);
            return gun != null ? gun.DisplayName : null;
        }

        static Color ToColour(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.white;
    }
}
