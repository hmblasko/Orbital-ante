using System;
using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>What one showing of the upgrade screen holds. Filled by the ante loop.</summary>
    public sealed class StarChartPage
    {
        public string title = "PICK A PERK";
        public string subtitle = "Take one. Each perk lights a star; a full constellation turns on its set bonus.";
        public string backLabel = "Back";
        /// <summary>A line under the chart: how the next ante will press harder, or the field note.</summary>
        public string footer;
        public PerkLoadout loadout;
        public List<PerkDefinition> offers = new List<PerkDefinition>();
        public AnteBalance balance;
        public int wallet, rerollPrice, skipCash;
        public bool canReroll;
        /// <summary>The card the cursor starts on.</summary>
        public int selected;
        /// <summary>A perk unlocked since the last draft: its card says NEW. Null for none.</summary>
        public string newPerkId;
        public Action<PerkDefinition> take;
        public Action skip, reroll, back;
    }

    /// <summary>
    /// The upgrade screen as a star chart (21 Nov 2026, Matthew: "interesting perk and upgrade system that looks
    /// aesthetically pleasing to upgrade during the game, even if that is a select menu where you apply the rewarded
    /// upgrades"; option A of the mockups in the fun report). The five perk families are constellations over a
    /// night sky; held perks are lit stars, and the card under the cursor makes the star it would light pulse, so
    /// the long-term pull of a set sits right beside the short-term pick. Every card shows its gains and its costs
    /// on separate lines. What is drawn comes from <see cref="StarChart"/> (plain C#, rule tested); this only
    /// places it. Opened at the break in place of the perk list, and on foot with Upgrades (Tab / Select) once the
    /// ante's target is met. Pauses the game while open.
    /// </summary>
    public sealed class StarChartView : MonoBehaviour
    {
        /// <summary>True while the screen is up. The pause menu leaves Esc and Start alone meanwhile.</summary>
        public static bool Showing { get; private set; }
        /// <summary>The frame it last closed, so the press that closed it does not also open the pause menu.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        public static void ResetStatics() { Showing = false; ClosedFrame = -1; }

        public const int SortingOrder = 110;
        const float HoldSeconds = 0.35f;
        const float CardW = 300f, CardH = 236f, CardTop = 490f;
        static readonly float[] CardLeft = { 150f, 490f, 830f };
        static readonly Color Night = new Color(0.035f, 0.045f, 0.09f, 0.985f);
        static readonly Color CardFill = new Color(0.08f, 0.10f, 0.17f, 0.97f);
        static readonly Color Amber = new Color(1f, 0.74f, 0.32f);
        // A cost is one side of a fair trade, not a danger: red is kept for damage, the late clock and overtime
        // (critique loop round 2). A cool tone as bright as the gains (a downside must never read as fine print), with its
        // minus sign, sets it apart.
        static readonly Color CostTone = new Color(0.80f, 0.86f, 1f);

        Canvas _canvas;
        RectTransform _board, _chart;
        Text _title, _subtitle, _wallet, _wild, _footer;
        readonly Button[] _cards = new Button[3];
        readonly RectTransform[] _cardRects = new RectTransform[3];
        readonly Image[] _cardEdges = new Image[3];
        readonly Text[] _cardTop = new Text[3], _cardName = new Text[3], _cardBody = new Text[3], _cardGain = new Text[3];
        readonly Image[] _cardGainBox = new Image[3];
        Button _skip, _reroll, _back;
        Texture2D _sky;
        RawImage _stars;
        readonly List<Image> _pulsing = new List<Image>();

        StarChartPage _page;
        List<ChartCard> _cardModels = new List<ChartCard>();
        int _index = -1;
        bool _pausedByMe;
        float _openedAt;
        int _openFrame;
        Vector2 _lastPointer;

        /// <summary>Builds the screen under the game's UI root. Call once.</summary>
        public static StarChartView Create(Transform parent)
        {
            var go = new GameObject("StarChart");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<StarChartView>();
            view.Build();
            return view;
        }

        void OnDestroy()
        {
            if (Showing && _canvas != null && _canvas.gameObject.activeSelf) Showing = false;
            if (_sky != null) Destroy(_sky);
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Shows (or refreshes) the screen with this page.</summary>
        public void Show(StarChartPage page)
        {
            if (page == null) return;
            bool fresh = !Showing;
            _page = page;
            _cardModels = StarChart.Cards(page.loadout, page.offers, page.balance, page.newPerkId);
            if (fresh)
            {
                if (!GameSession.Paused) { GameSession.SetPaused(true); _pausedByMe = true; }
                if (_sky == null) { _sky = MakeSky(); _stars.texture = _sky; }
                // Pausing turns the Gameplay map off; Upgrades alone stays on so a second press closes the chart.
                if (GameInput.Upgrades != null) GameInput.Upgrades.Enable();
                _canvas.gameObject.SetActive(true);
                Showing = true;
                _openedAt = Time.unscaledTime;
                _openFrame = Time.frameCount;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                _lastPointer = GameInput.UiPoint != null ? GameInput.UiPoint.ReadValue<Vector2>() : Vector2.zero;
            }
            Fill();
            int start = Mathf.Clamp(page.selected, 0, Mathf.Max(0, _cardModels.Count - 1));
            _index = int.MinValue;   // never equal, so the chart is always redrawn (even with no offers)
            Select(_cardModels.Count > 0 ? start : -1, false);
            if (!fresh && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_cardModels.Count > 0 ? _cards[_index].gameObject : _back.gameObject);
        }

        public void Hide()
        {
            if (!Showing) return;
            Showing = false;
            ClosedFrame = Time.frameCount;
            _canvas.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            // Leave the game paused if the pause menu was opened over the chart meanwhile.
            if (_pausedByMe && (PauseMenu.Instance == null || !PauseMenu.Instance.IsOpen)) GameSession.SetPaused(false);
            _pausedByMe = false;
            // Still paused (at the break, under the menu): the Gameplay map is off, so Upgrades goes off with it.
            if (GameSession.Paused && GameInput.Upgrades != null) GameInput.Upgrades.Disable();
            _page = null;
        }

        /// <summary>The card under the cursor, for a refresh that should keep it (a reroll).</summary>
        public int SelectedIndex => _index;

        bool Held => Time.unscaledTime - _openedAt < HoldSeconds;

        void Update()
        {
            if (!Showing || _page == null) return;
            // SetAlpha fades without rebuilding the graphic (Deck review): only a re-batch of this canvas.
            for (int i = 0; i < _pulsing.Count; i++)
                _pulsing[i].canvasRenderer.SetAlpha(0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f + i));
            if (Time.frameCount == _openFrame) return;

            if (GameInput.UiCancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame()
                || (GameInput.Upgrades != null && GameInput.Upgrades.WasPressedThisFrame()))
            {
                Press(_page.back);
                return;
            }

            EventSystem es = EventSystem.current;
            if (es == null) return;
            // A controller has nothing selected for the first moment (so a mashed button from the fight cannot pick
            // an unread card), then the card the cursor is on.
            if (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy)
            {
                if (!Held) es.SetSelectedGameObject(_index >= 0 ? _cards[_index].gameObject : _back.gameObject);
                return;
            }
            for (int i = 0; i < _cards.Length; i++)
                if (es.currentSelectedGameObject == _cards[i].gameObject && i != _index) Select(i, true);

            // The mouse: hovering a card moves the cursor to it.
            Vector2 pointer = GameInput.UiPoint.ReadValue<Vector2>();
            if ((pointer - _lastPointer).sqrMagnitude > 4f)
            {
                _lastPointer = pointer;
                for (int i = 0; i < _cardModels.Count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_cardRects[i], pointer, null) && i != _index)
                    {
                        es.SetSelectedGameObject(_cards[i].gameObject);
                        Select(i, true);
                    }
            }
        }

        void Press(Action action)
        {
            if (Held || _page == null) return;
            action?.Invoke();
        }

        void Select(int index, bool sound)
        {
            if (index == _index) return;
            _index = index;
            if (sound) Audio.AudioDirector.PlayLocal("ui.move", 0.6f);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i == _index;
                PerkFamily family = i < _cardModels.Count ? _cardModels[i].family : PerkFamily.None;
                _cardEdges[i].color = on ? Amber : WithAlpha(FamilyColour(family), 0.4f);
                _cardRects[i].anchoredPosition = new Vector2(CardLeft[i], -CardTop + (on ? 10f : 0f));
            }
            DrawChart();
        }

        // ------------------------------------------------------------------ filling

        void Fill()
        {
            StarChartPage p = _page;
            _title.text = p.title;
            _subtitle.text = p.subtitle;
            _wallet.text = "<size=26>" + StarChart.Points(p.wallet) + "</size>\nTO SPEND · NEVER LOWERS YOUR SCORE";
            List<string> wild = StarChart.WildHeld(p.loadout);
            _wild.text = wild.Count > 0 ? "WILD PERKS HELD: " + string.Join(", ", wild) : string.Empty;
            _footer.text = p.footer ?? string.Empty;

            for (int i = 0; i < _cards.Length; i++)
            {
                bool used = i < _cardModels.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;
                ChartCard card = _cardModels[i];
                Color fc = FamilyColour(card.family);
                _cardTop[i].text = "<color=#" + ColorUtility.ToHtmlStringRGB(fc) + ">" + card.familyName + "</color>   <color=#a89c8c>" + card.rarity + "</color>"
                                   + (card.isNew ? "   <color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">NEW · UNLOCKED</color>" : string.Empty);
                _cardName[i].text = card.name.ToUpperInvariant();
                var body = new StringBuilder();
                for (int g = 0; g < card.gains.Count; g++) body.Append("+ ").Append(Capital(card.gains[g])).Append('\n');
                for (int c = 0; c < card.costs.Count; c++)
                    body.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(CostTone)).Append(">- ").Append(Capital(card.costs[c])).Append("</color>\n");
                _cardBody[i].text = body.ToString().TrimEnd('\n');
                _cardGain[i].text = card.chartLine;
                _cardGain[i].color = card.completesSet ? Amber : UiKit.Cream;
                _cardGainBox[i].color = card.completesSet ? new Color(Amber.r, Amber.g, Amber.b, 0.22f) : WithAlpha(fc, 0.14f);
            }

            UiKit.ButtonText(_skip).text = "Skip all three: +" + StarChart.Points(p.skipCash) + " to spend";
            _skip.gameObject.SetActive(p.skip != null);
            _reroll.gameObject.SetActive(p.reroll != null);
            UiKit.ButtonText(_reroll).text = "New three: " + StarChart.Points(p.rerollPrice);
            _reroll.interactable = p.canReroll;
            UiKit.ButtonText(_back).text = p.backLabel;
            Wire();
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Left and right between the cards, down to the buttons, up again to the card in the middle.</summary>
        void Wire()
        {
            var row = new List<Selectable>();
            for (int i = 0; i < _cardModels.Count; i++) row.Add(_cards[i]);
            var actions = new List<Selectable>();
            if (_skip.gameObject.activeSelf) actions.Add(_skip);
            if (_reroll.gameObject.activeSelf && _reroll.interactable) actions.Add(_reroll);
            actions.Add(_back);
            Selectable upTarget = row.Count > 0 ? row[row.Count / 2] : null;
            for (int i = 0; i < row.Count; i++)
                SetNav(row[i], null, actions[Mathf.Min(i, actions.Count - 1)], i > 0 ? row[i - 1] : null, i < row.Count - 1 ? row[i + 1] : null);
            for (int i = 0; i < actions.Count; i++)
                SetNav(actions[i], upTarget, null, i > 0 ? actions[i - 1] : null, i < actions.Count - 1 ? actions[i + 1] : null);
        }

        static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            var nav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
            s.navigation = nav;
        }

        /// <summary>Redraws the constellations for the card under the cursor (only on a move, never every frame).</summary>
        void DrawChart()
        {
            _pulsing.Clear();
            // Hidden at once, destroyed at the frame's end, so the old chart is never drawn under the new one.
            for (int i = _chart.childCount - 1; i >= 0; i--)
            {
                GameObject child = _chart.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            if (_page == null) return;
            List<ChartConstellation> chart = StarChart.Constellations(_page.loadout, _page.offers, _index, _page.balance);
            foreach (ChartConstellation c in chart)
            {
                Color fc = FamilyColour(c.family);
                for (int s = 1; s < c.stars.Count; s++)
                {
                    ChartStar a = c.stars[s - 1], b = c.stars[s];
                    float alpha = a.state == StarState.Lit && b.state == StarState.Lit ? 0.85f
                        : b.state == StarState.Pending && a.state == StarState.Lit ? 0.6f : 0.16f;
                    Image line = Line(a.x, a.y, b.x, b.y, WithAlpha(fc, alpha), c.complete ? 3f : 2f);
                    if (b.state == StarState.Pending && a.state == StarState.Lit) _pulsing.Add(line);
                }
                for (int s = 0; s < c.stars.Count; s++)
                {
                    ChartStar star = c.stars[s];
                    switch (star.state)
                    {
                        case StarState.Lit:
                            Dot(star.x, star.y, c.complete ? 54f : 40f, WithAlpha(fc, c.complete ? 0.32f : 0.22f), UiKit.Circle);
                            Dot(star.x, star.y, 15f, Color.Lerp(fc, Color.white, 0.35f), UiKit.Circle);
                            break;
                        case StarState.Pending:
                            _pulsing.Add(Dot(star.x, star.y, 46f, WithAlpha(Amber, 0.5f), UiKit.Circle));
                            Dot(star.x, star.y, 20f, Amber, UiKit.Ring);
                            break;
                        default:
                            Dot(star.x, star.y, 14f, new Color(1f, 1f, 1f, 0.28f), UiKit.Ring);
                            break;
                    }
                    if (!string.IsNullOrEmpty(star.label))
                    {
                        Text name = UiKit.Label(_chart, star.label, 12, star.state == StarState.Pending ? Amber : UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                        Place((RectTransform)name.transform, star.x, star.y + 20f, 140f, 16f);
                    }
                }
                Text progress = UiKit.Label(_chart, c.completesWithPick ? c.name + " " + c.held + "/" + c.stars.Count + "  →  SET ON" : c.progress, 15,
                    c.complete || c.completesWithPick ? Amber : fc, TextAnchor.MiddleCenter);
                Place((RectTransform)progress.transform, c.labelX, c.labelY + 6f, 220f, 20f);
                Text bonus = UiKit.Label(_chart, "Set: " + c.bonus, 12, c.complete ? UiKit.Cream : UiKit.Muted, TextAnchor.UpperCenter, FontStyle.Normal);
                Place((RectTransform)bonus.transform, c.labelX, c.labelY + 44f, 210f, 48f);
            }
        }

        Image Dot(float x, float y, float size, Color colour, Sprite sprite)
        {
            RectTransform rt = UiKit.NewRect("Star", _chart);
            Place(rt, x, y, size, size);
            return UiKit.AddImage(rt, colour, sprite);
        }

        Image Line(float ax, float ay, float bx, float by, Color colour, float width)
        {
            RectTransform rt = UiKit.NewRect("Line", _chart);
            float dx = bx - ax, dy = by - ay;
            Place(rt, (ax + bx) * 0.5f, (ay + by) * 0.5f, Mathf.Sqrt(dx * dx + dy * dy), width);
            // Board y runs down, Unity's up: the angle flips.
            rt.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            return UiKit.AddImage(rt, colour);
        }

        /// <summary>Puts a rect's centre at a board point (1280 x 800, y down from the top left).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static Color FamilyColour(PerkFamily family)
        {
            StarChart.ColourOf(family, out float r, out float g, out float b);
            return new Color(r, g, b);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // ------------------------------------------------------------------ building

        void Build()
        {
            _canvas = UiKit.CreateCanvas("StarChartCanvas", transform, SortingOrder);
            // Expand, so the whole 1280 x 800 board fits on screens wider than the Deck's (the sky still fills them).
            _canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform root = (RectTransform)_canvas.transform;

            // The sky fills the whole screen (and catches the mouse, so nothing underneath is clicked).
            RectTransform skyRect = UiKit.NewRect("Sky", root);
            UiKit.Stretch(skyRect);
            UiKit.AddImage(skyRect, Night, null, true);
            RectTransform starsRect = UiKit.NewRect("Stars", root);
            UiKit.Stretch(starsRect);
            var stars = starsRect.gameObject.AddComponent<RawImage>();
            stars.raycastTarget = false;
            _stars = stars;   // the sky texture is made on the first Show, in a paused moment, not at boot

            // Everything else sits on a 1280 x 800 board in the middle, the Deck's own screen.
            _board = UiKit.NewRect("Board", root);
            UiKit.Pin(_board, new Vector2(0.5f, 0.5f), Vector2.zero, UiKit.ReferenceResolution);
            _chart = UiKit.NewRect("Chart", _board);
            UiKit.Stretch(_chart);

            _title = UiKit.Label(_board, string.Empty, 34, UiKit.Cream, TextAnchor.UpperLeft);
            PlaceTopLeft((RectTransform)_title.transform, 60f, 26f, 700f, 44f);
            _subtitle = UiKit.Label(_board, string.Empty, 15, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            PlaceTopLeft((RectTransform)_subtitle.transform, 62f, 70f, 760f, 22f);
            _wallet = UiKit.Label(_board, string.Empty, 12, Amber, TextAnchor.UpperRight);
            _wallet.supportRichText = true;
            PlaceTopLeft((RectTransform)_wallet.transform, 900f, 26f, 320f, 60f);

            _wild = UiKit.Label(_board, string.Empty, 13, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place((RectTransform)_wild.transform, 640f, 432f, 1100f, 18f);
            _footer = UiKit.Label(_board, string.Empty, 13, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place((RectTransform)_footer.transform, 640f, 456f, 1100f, 18f);

            for (int i = 0; i < _cards.Length; i++) BuildCard(i);

            _skip = ActionButton("Skip", 380f, () => Press(_page != null ? _page.skip : null));
            _reroll = ActionButton("Reroll", 640f, () => Press(_page != null ? _page.reroll : null));
            _back = ActionButton("Back", 900f, () => Press(_page != null ? _page.back : null));

            _canvas.gameObject.SetActive(false);
        }

        void BuildCard(int i)
        {
            RectTransform rt = UiKit.NewRect("Card" + i, _board);
            PlaceTopLeft(rt, CardLeft[i], CardTop, CardW, CardH);
            _cardRects[i] = rt;
            _cardEdges[i] = UiKit.AddImage(rt, Amber, UiKit.Rounded, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = _cardEdges[i];
            int slot = i;
            button.onClick.AddListener(() =>
            {
                if (Held || _page == null || slot >= _cardModels.Count) return;
                Audio.AudioDirector.PlayLocal("ui.select");
                _page.take?.Invoke(_cardModels[slot].perk);
            });
            _cards[i] = button;

            RectTransform fill = UiKit.NewRect("Fill", rt);
            UiKit.Stretch(fill);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            UiKit.AddImage(fill, CardFill, UiKit.Rounded);

            _cardTop[i] = UiKit.Label(fill, string.Empty, 12, UiKit.Muted, TextAnchor.UpperLeft);
            _cardTop[i].supportRichText = true;
            PlaceTopLeft((RectTransform)_cardTop[i].transform, 16f, 12f, CardW - 32f, 18f);
            _cardName[i] = UiKit.Label(fill, string.Empty, 22, UiKit.Cream, TextAnchor.UpperLeft);
            PlaceTopLeft((RectTransform)_cardName[i].transform, 16f, 32f, CardW - 32f, 30f);
            _cardBody[i] = UiKit.Label(fill, string.Empty, 14, UiKit.Cream, TextAnchor.UpperLeft, FontStyle.Normal);
            _cardBody[i].supportRichText = true;
            _cardBody[i].lineSpacing = 1.1f;
            // A long card (Change of Pace names its five categories) steps down a size or two rather than running
            // into the set box under it. Worked out when the cards are filled, never per frame.
            _cardBody[i].verticalOverflow = VerticalWrapMode.Truncate;   // best fit only shrinks text that would not fit
            _cardBody[i].resizeTextForBestFit = true;
            _cardBody[i].resizeTextMinSize = 12;
            _cardBody[i].resizeTextMaxSize = 14;
            PlaceTopLeft((RectTransform)_cardBody[i].transform, 16f, 68f, CardW - 32f, 100f);

            RectTransform box = UiKit.NewRect("Gain", fill);
            PlaceTopLeft(box, 10f, CardH - 6f - 58f, CardW - 26f, 52f);
            _cardGainBox[i] = UiKit.AddImage(box, new Color(1f, 1f, 1f, 0.1f), UiKit.Rounded);
            _cardGain[i] = UiKit.Label(box, string.Empty, 12, UiKit.Cream, TextAnchor.MiddleCenter);
            RectTransform gainText = (RectTransform)_cardGain[i].transform;
            UiKit.Stretch(gainText);
            gainText.offsetMin = new Vector2(8f, 2f);
            gainText.offsetMax = new Vector2(-8f, -2f);
        }

        Button ActionButton(string label, float x, UnityEngine.Events.UnityAction onClick)
        {
            Button b = UiKit.Button(_board, label, onClick, 44f, 18);
            Place((RectTransform)b.transform, x, 763f, 250f, 44f);
            return b;
        }

        /// <summary>A night sky made once: a deep blue wash, a faint band, and scattered stars (visual only, so Unity's random).</summary>
        static Texture2D MakeSky()
        {
            const int w = 640, h = 400;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "StarChartSky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    // A soft band across the sky, like the Milky Way, brighter near the chart.
                    float d = (v - 0.62f - (u - 0.5f) * 0.25f) / 0.16f;
                    float band = 1f / (1f + d * d * (1f + 0.5f * d * d));   // a cheap bell, close to exp(-d²)
                    float glow = 0.05f * band + 0.03f * v;
                    byte bl = (byte)(glow * 1.3f * 255f);
                    px[y * w + x] = new Color32((byte)(glow * 0.6f * 255f), (byte)(glow * 0.7f * 255f), bl, 0);
                }
            }
            var rng = new System.Random(2111);   // the same sky every time; visual only
            for (int i = 0; i < 900; i++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                float b = (float)Math.Pow(rng.NextDouble(), 3.0);
                Color32 c = px[y * w + x];
                byte a = (byte)Mathf.Clamp(40 + b * 215f, 0f, 255f);
                px[y * w + x] = new Color32(230, 232, 255, (byte)Mathf.Max(c.a, a));
            }
            // Alpha carries the stars; the wash shows through the colour channels only where alpha is low, so lift it.
            for (int i = 0; i < px.Length; i++)
                if (px[i].a == 0) px[i] = new Color32(px[i].r, px[i].g, px[i].b, (byte)Mathf.Clamp(px[i].b * 2, 0, 90));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
