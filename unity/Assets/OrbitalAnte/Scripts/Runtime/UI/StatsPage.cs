using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The pause menu's Run Stats page (Matthew, 17 Nov 2026: move stats off the screen into "a menu on the pause
    /// screen"). Three columns of labelled values (this run, the ship or your health, the suit and flight), a switch
    /// for the performance overlay, and behind a button the strip of big performance numbers with the full
    /// technical readout. What it shows comes from <see cref="Source"/> as a <see cref="StatsSheet"/> each time it opens; the
    /// game is paused, so the numbers are the moment you paused.
    /// </summary>
    public sealed class StatsPage
    {
        const float CardWidth = 1140f, CardHeight = 700f;
        const float Inset = 40f;
        const float ColumnGap = 30f;
        const float RowStep = 34f;

        public GameObject Root { get; private set; }
        public GameObject DefaultSelection => _back != null ? _back.gameObject : null;

        /// <summary>Fills the page. Set by the game; with none, the page says there is nothing to show.</summary>
        public Func<StatsSheet> Source;

        RectTransform _columns, _perf, _technical;
        Text _technicalText, _perfLine;
        readonly Text[] _cellValue = new Text[4];
        readonly Text[] _cellWords = new Text[4];
        Button _overlay, _details, _back;
        bool _showTechnical;
        readonly List<GameObject> _built = new List<GameObject>();

        public void Build(Transform canvas, Action back)
        {
            RectTransform root = UiKit.NewRect("StatsPage", canvas);
            UiKit.Stretch(root);
            Root = root.gameObject;

            // The card: an orange edge round a dark panel, like every pause page, but a fixed size, since its
            // columns are laid out by hand.
            RectTransform edge = UiKit.NewRect("StatsCard", root);
            UiKit.Pin(edge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            UiKit.AddImage(edge, UiKit.PanelEdge, UiKit.Rounded, true);
            RectTransform body = UiKit.NewRect("Body", edge);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(5f, 5f);
            body.offsetMax = new Vector2(-5f, -5f);
            UiKit.AddImage(body, UiKit.PanelColor, UiKit.Rounded);

            Text title = UiKit.Label(body, "RUN STATS", 40, UiKit.PanelEdge);
            UiKit.Pin((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(600f, 54f));
            // The same slight tilt as the other pages' headings.
            title.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);

            _columns = UiKit.NewRect("Columns", body);
            UiKit.Pin(_columns, new Vector2(0f, 1f), new Vector2(Inset, -86f), new Vector2(CardWidth - 10f - 2f * Inset, 330f));

            BuildPerformance(body);

            _technical = UiKit.NewRect("Technical", body);
            UiKit.Pin(_technical, new Vector2(0f, 1f), new Vector2(Inset, -86f), new Vector2(CardWidth - 10f - 2f * Inset, 296f));
            UiKit.AddImage(_technical, new Color(0f, 0f, 0f, 0.35f), UiKit.Rounded);
            _technicalText = UiKit.Label(_technical, string.Empty, 14, UiKit.Cream, TextAnchor.UpperLeft, FontStyle.Normal);
            RectTransform tt = (RectTransform)_technicalText.transform;
            UiKit.Stretch(tt);
            tt.offsetMin = new Vector2(16f, 12f);
            tt.offsetMax = new Vector2(-16f, -12f);
            _technicalText.lineSpacing = 1.15f;
            _technical.gameObject.SetActive(false);

            // Buttons: two side by side, then Back across the bottom.
            float inner = CardWidth - 10f - 2f * Inset;
            float half = (inner - 20f) / 2f;
            _overlay = UiKit.Button(body, string.Empty, () =>
            {
                FeelSettings.PerformanceOverlay = !FeelSettings.PerformanceOverlay;
                ShowOverlayState();
            }, 50f, 22);
            PlaceButton(_overlay, Inset, -CardHeight + 10f + 128f, half);
            _details = UiKit.Button(body, "Technical details", () =>
            {
                _showTechnical = !_showTechnical;
                ShowTechnical();
            }, 50f, 22);
            PlaceButton(_details, Inset + half + 20f, -CardHeight + 10f + 128f, half);
            _back = UiKit.Button(body, "Back", () => back?.Invoke(), 50f, 22);
            PlaceButton(_back, Inset, -CardHeight + 10f + 66f, inner);

            Root.SetActive(false);
        }

        static void PlaceButton(Button button, float x, float y, float width)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, 50f);
        }

        void BuildPerformance(RectTransform body)
        {
            _perf = UiKit.NewRect("Performance", body);
            float inner = CardWidth - 10f - 2f * Inset;
            UiKit.Pin(_perf, new Vector2(0f, 1f), new Vector2(Inset, -394f), new Vector2(inner, 150f));
            Heading(_perf, "PERFORMANCE", inner);
            float cell = inner / 4f;
            for (int i = 0; i < 4; i++)
            {
                _cellValue[i] = UiKit.Label(_perf, string.Empty, 30, UiKit.Cream, TextAnchor.UpperLeft);
                UiKit.Pin((RectTransform)_cellValue[i].transform, new Vector2(0f, 1f), new Vector2(cell * i, -32f), new Vector2(cell - 10f, 38f));
                _cellWords[i] = UiKit.Label(_perf, string.Empty, 15, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Normal);
                UiKit.Pin((RectTransform)_cellWords[i].transform, new Vector2(0f, 1f), new Vector2(cell * i, -70f), new Vector2(cell - 10f, 20f));
            }
            _perfLine = UiKit.Label(_perf, string.Empty, 14, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_perfLine.transform, new Vector2(0f, 1f), new Vector2(0f, -100f), new Vector2(inner, 20f));
            _perfLine.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>A section's heading: small amber capitals over a thin rule.</summary>
        static void Heading(RectTransform parent, string words, float width)
        {
            Text h = UiKit.Label(parent, words, 14, UiKit.PanelEdge, TextAnchor.UpperLeft);
            UiKit.Pin((RectTransform)h.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 20f));
            RectTransform rule = UiKit.NewRect("Rule", parent);
            UiKit.Pin(rule, new Vector2(0f, 1f), new Vector2(0f, -22f), new Vector2(width, 1f));
            UiKit.AddImage(rule, new Color(UiKit.Muted.r, UiKit.Muted.g, UiKit.Muted.b, 0.35f));
        }

        static Color ToneColour(StatTone tone)
        {
            switch (tone)
            {
                case StatTone.Warn: return UiKit.PanelEdge;
                case StatTone.Bad: return UiKit.Danger;
                case StatTone.Quiet: return UiKit.Muted;
                default: return UiKit.Cream;
            }
        }

        /// <summary>Builds the page afresh from <see cref="Source"/>. Called each time the page is shown.</summary>
        public void Open()
        {
            StatsSheet sheet = null;
            try
            {
                sheet = Source != null ? Source() : null;
            }
            catch (Exception e)
            {
                // A missing piece of the world must never stop the pause menu working.
                Debug.LogException(e);
            }
            if (sheet == null)
            {
                sheet = new StatsSheet();
                sheet.Section("THIS RUN").Add("Nothing to show", string.Empty, StatTone.Quiet);
            }

            foreach (GameObject go in _built) UnityEngine.Object.Destroy(go);
            _built.Clear();

            List<StatSection> columns = sheet.Columns();
            float inner = _columns.sizeDelta.x;
            float width = (inner - ColumnGap * (StatsSheet.MaxColumns - 1)) / StatsSheet.MaxColumns;
            for (int c = 0; c < columns.Count; c++)
            {
                RectTransform col = UiKit.NewRect("Column" + c, _columns);
                UiKit.Pin(col, new Vector2(0f, 1f), new Vector2(c * (width + ColumnGap), 0f), new Vector2(width, 330f));
                _built.Add(col.gameObject);
                Heading(col, columns[c].Title, width);
                for (int r = 0; r < columns[c].Rows.Count; r++)
                {
                    StatRow row = columns[c].Rows[r];
                    float y = -36f - r * RowStep;
                    bool quiet = row.Tone == StatTone.Quiet && string.IsNullOrEmpty(row.Value);
                    Text label = UiKit.Label(col, row.Label, 17, UiKit.Muted, TextAnchor.MiddleLeft, quiet ? FontStyle.Italic : FontStyle.Normal);
                    UiKit.Pin((RectTransform)label.transform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(width * 0.45f, 28f));
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    Text value = UiKit.Label(col, row.Value, 18, ToneColour(row.Tone), TextAnchor.MiddleRight);
                    UiKit.Pin((RectTransform)value.transform, new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(width * 0.55f, 28f));
                    value.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                bool has = i < sheet.Performance.Count;
                _cellValue[i].text = has ? sheet.Performance[i].Value : string.Empty;
                _cellValue[i].color = has ? ToneColour(sheet.Performance[i].Tone) : UiKit.Cream;
                _cellWords[i].text = has ? sheet.Performance[i].Words : string.Empty;
            }
            _perfLine.text = sheet.PerformanceLine ?? string.Empty;
            _technicalText.text = string.IsNullOrEmpty(sheet.Technical) ? "No technical readout." : sheet.Technical;

            _showTechnical = false;
            ShowTechnical();
            ShowOverlayState();
        }

        bool _shownOverlay;

        /// <summary>Called every frame while the page is open: F3 still works here, so keep the button's word true.</summary>
        public void Tick()
        {
            if (FeelSettings.PerformanceOverlay != _shownOverlay) ShowOverlayState();
        }

        void ShowOverlayState()
        {
            _shownOverlay = FeelSettings.PerformanceOverlay;
            UiKit.ButtonText(_overlay).text = "Performance overlay: " + (FeelSettings.PerformanceOverlay ? "ON" : "OFF");
        }

        void ShowTechnical()
        {
            _technical.gameObject.SetActive(_showTechnical);
            _columns.gameObject.SetActive(!_showTechnical);
            // The performance numbers are for testing, not for playing: they sit with the technical readout.
            _perf.gameObject.SetActive(_showTechnical);
            UiKit.ButtonText(_details).text = _showTechnical ? "Back to the stats" : "Technical details";
        }
    }
}
