using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The few words the ship needs on screen: what the button in front of you does, how a repair is going,
    /// which parts are hurt, and a short notice when something breaks, falls off or is mended.
    /// Shown only when there is something to say, so the view through the windows stays clear.
    /// Hurt parts are a small panel in the top right (17 Nov 2026 clean-up): a bar per part while it still works,
    /// a red word once it does not; the per cents are in the pause menu's Run Stats.
    /// </summary>
    public sealed class ShipHud : MonoBehaviour
    {
        const float NoticeSeconds = 4f;
        const float ListInterval = 0.25f;

        Ship _ship;
        Astronaut _astronaut;
        FlightHud _flightHud;
        SystemMap _map;
        Canvas _canvas;
        Text _prompt, _notice;

        sealed class PartRowView
        {
            public RectTransform Root, Fill;
            public Text Name, Tag;
            public Image FillImage;
            public GameObject Bar;
            public float TagWidth;
        }

        RectTransform _damagePanel;
        readonly System.Collections.Generic.List<PartRowView> _rows = new System.Collections.Generic.List<PartRowView>();
        const float RowHeight = 22f, BarWidth = 84f;
        /// <summary>How long every hurt part is listed after the damage last changed.</summary>
        const float FullSeconds = 6f;
        int _damageKey;
        float _damageChanged = -100f;
        RectTransform _barBack, _barFill, _dot;
        string _shipNotice = string.Empty;
        float _shipNoticeTime = -100f;
        bool _shipNoticeBad;
        int _shipNoticeRank;
        float _nextList;
        bool _flightShown = true;

        public void Build(Ship ship, Astronaut astronaut, FlightHud flightHud, SystemMap map)
        {
            _ship = ship;
            _astronaut = astronaut;
            _flightHud = flightHud;
            _map = map;
            _canvas = UiKit.CreateCanvas("ShipHUD", transform, 10);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            _dot = UiKit.NewRect("AimDot", _canvas.transform);
            UiKit.Pin(_dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            UiKit.AddImage(_dot, new Color(0.96f, 0.91f, 0.80f, 0.75f), UiKit.Circle);

            _prompt = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 26, UiKit.Cream));
            UiKit.Pin((RectTransform)_prompt.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(1000f, 34f));

            _barBack = UiKit.NewRect("MendBar", _canvas.transform);
            UiKit.Pin(_barBack, new Vector2(0.5f, 0.5f), new Vector2(0f, -128f), new Vector2(260f, 14f));
            UiKit.AddImage(_barBack, new Color(0f, 0f, 0f, 0.6f), UiKit.Rounded);
            _barFill = UiKit.NewRect("Fill", _barBack);
            _barFill.anchorMin = new Vector2(0f, 0f);
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.offsetMin = new Vector2(2f, 2f);
            _barFill.offsetMax = new Vector2(2f, -2f);
            UiKit.AddImage(_barFill, UiKit.PanelEdge);

            _notice = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 28, UiKit.PanelEdge));
            UiKit.Pin((RectTransform)_notice.transform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1000f, 36f));

            _damagePanel = UiKit.NewRect("ShipDamage", _canvas.transform);
            UiKit.Pin(_damagePanel, new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(320f, 440f));
            Text title = Shadowed(UiKit.Label(_damagePanel, "SHIP", 13, UiKit.Muted, TextAnchor.UpperRight));
            UiKit.Pin((RectTransform)title.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(120f, 18f));
            _damagePanel.gameObject.SetActive(false);

            ship.PartDamaged += OnPartDamaged;
            ship.PartRepaired += OnPartRepaired;
        }

        static Text Shadowed(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        void OnDestroy()
        {
            if (_ship == null) return;
            _ship.PartDamaged -= OnPartDamaged;
            _ship.PartRepaired -= OnPartRepaired;
        }

        void OnPartDamaged(int part, DamageResult result)
        {
            // Only the moments that change what you should do next; not every scratch.
            string name = _ship.Model.Parts[part].Name;
            if (result.FellOff) Raise(name + " torn off! It cannot be mended.", true);
            else if (result.Broke) Raise(name + " broken. Mend it by hand.", true);
            else if (result.Changed && result.After == PartState.Damaged) Raise(name + " damaged.", true);
        }

        void OnPartRepaired(int part)
        {
            Raise(_ship.Model.Parts[part].Name + " mended.", false);
        }

        void Raise(string words, bool bad)
        {
            // In a crash several things happen at once. The worst news stays on screen.
            int rank = !bad ? 0 : words.Contains("torn off") ? 3 : words.Contains("broken") ? 2 : 1;
            if (Time.unscaledTime - _shipNoticeTime < 1.5f && rank < _shipNoticeRank) return;
            _shipNoticeRank = rank;
            _shipNotice = words;
            _shipNoticeTime = Time.unscaledTime;
            _shipNoticeBad = bad;
        }

        void LateUpdate()
        {
            if (_ship == null || _astronaut == null) return;

            // The flying instruments belong to the seat. On your feet they would only be clutter.
            // (They mark things as seen through the windows, so they also step aside while a ship camera fills the view.)
            bool flying = !_ship.Flight.Unmanned && (_map == null || !_map.IsOpen) && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;
            if (flying != _flightShown && _flightHud != null)
            {
                _flightShown = flying;
                _flightHud.SetVisible(flying);
            }
            bool onFoot = _astronaut.Mode == AstronautMode.Aboard || _astronaut.Mode == AstronautMode.Outside;
            if (_dot.gameObject.activeSelf != onFoot) _dot.gameObject.SetActive(onFoot);

            string prompt = _astronaut.Prompt;
            if (_prompt.text != prompt) _prompt.text = prompt;
            float progress = _astronaut.PromptProgress;
            bool bar = progress >= 0f;
            if (_barBack.gameObject.activeSelf != bar) _barBack.gameObject.SetActive(bar);
            if (bar) _barFill.sizeDelta = new Vector2(256f * Mathf.Clamp01(progress), _barFill.sizeDelta.y);

            // The newest notice wins, whoever raised it.
            float now = Time.unscaledTime;
            string words = _shipNotice;
            float since = now - _shipNoticeTime;
            bool bad = _shipNoticeBad;
            if (now - _astronaut.NoticeTime < since)
            {
                words = _astronaut.Notice;
                since = now - _astronaut.NoticeTime;
                bad = false;
            }
            if (_map != null && now - _map.RefusedTime < since)
            {
                words = _map.Refused;
                since = now - _map.RefusedTime;
                bad = false;
            }
            bool showNotice = since < NoticeSeconds && words.Length > 0;
            if (showNotice)
            {
                if (_notice.text != words) _notice.text = words;
                Color colour = bad ? UiKit.Danger : UiKit.PanelEdge;
                colour.a = Mathf.Clamp01((NoticeSeconds - since) / 0.6f);
                _notice.color = colour;
            }
            else if (_notice.text.Length > 0) _notice.text = string.Empty;

            if (now < _nextList) return;
            _nextList = now + ListInterval;
            ShipModel model = _ship.Model;
            // Fresh news shows every hurt part for a while; after that only what no longer works stays, with a
            // count of the rest, so a dent you choose to live with does not hold the corner for good.
            int key = 17;
            int hurt = 0;
            for (int i = 0; i < model.Count; i++)
            {
                PartState state = model.State(i);
                if (state == PartState.Sound && !model.NeedsRepair(i)) continue;
                hurt++;
                key = key * 31 + i * 8 + (int)state * 2 + Mathf.RoundToInt(model.Fraction(i) * 10f) * 1000;
            }
            if (!model.CabinIntact) key = key * 31 + 7;
            if (key != _damageKey)
            {
                _damageKey = key;
                _damageChanged = now;
            }
            bool full = now - _damageChanged < FullSeconds;

            int shown = 0, quiet = 0;
            for (int i = 0; i < model.Count; i++)
            {
                PartState state = model.State(i);
                // Anything short of perfect is listed or counted, so there is no hidden damage.
                if (state == PartState.Sound && !model.NeedsRepair(i)) continue;
                HudRules.PartRow row = HudRules.ShipPart(state, model.Fraction(i), model.TearProgress(i));
                if (!full && row.Tag == null) { quiet++; continue; }
                ShowRow(shown++, model.Parts[i].Name, row);
            }
            if (!model.CabinIntact)
                ShowRow(shown++, "Cabin", new HudRules.PartRow { Tag = "OPEN TO SPACE", Tone = StatTone.Bad });
            if (quiet > 0)
                ShowRow(shown++, quiet + (quiet == 1 ? " part worn" : " parts worn"), new HudRules.PartRow { Tag = string.Empty, Tone = StatTone.Quiet });
            // What the damage costs in flight, while it costs anything.
            float thrust = model.ThrustFactor, turning = model.TurnFactor;
            if (hurt > 0 && (thrust < 0.95f || turning < 0.95f))
                ShowRow(shown++, "Thrust " + StatsFormat.Percent(thrust) + "   Turning " + StatsFormat.Percent(turning),
                    new HudRules.PartRow { Tag = string.Empty, Tone = StatTone.Warn });
            for (int i = shown; i < _rows.Count; i++)
                if (_rows[i].Root.gameObject.activeSelf) _rows[i].Root.gameObject.SetActive(false);
            bool any = shown > 0;
            if (_damagePanel.gameObject.activeSelf != any) _damagePanel.gameObject.SetActive(any);
        }

        void ShowRow(int index, string name, HudRules.PartRow row)
        {
            while (_rows.Count <= index) _rows.Add(NewRow(_rows.Count));
            PartRowView v = _rows[index];
            if (!v.Root.gameObject.activeSelf) v.Root.gameObject.SetActive(true);
            if (v.Name.text != name) v.Name.text = name;
            bool bar = row.Tag == null;
            if (v.Bar.activeSelf != bar) v.Bar.SetActive(bar);
            if (v.Tag.gameObject.activeSelf == bar) v.Tag.gameObject.SetActive(!bar);
            if (bar)
            {
                float width = Mathf.Round(BarWidth * row.Bar);
                if (!Mathf.Approximately(v.Fill.sizeDelta.x, width)) v.Fill.sizeDelta = new Vector2(width, 0f);
                Color c = row.Tone == StatTone.Warn ? UiKit.PanelEdge : UiKit.Cream;
                if (v.FillImage.color != c) v.FillImage.color = c;
            }
            else if (v.Tag.text != row.Tag)
            {
                v.Tag.text = row.Tag;
                v.TagWidth = v.Tag.preferredWidth;
            }
            // A row with an empty word is a line of words alone (the summary rows): muted, or amber for the cost.
            Color nameColour = !bar && row.Tag.Length == 0
                ? (row.Tone == StatTone.Warn ? UiKit.PanelEdge : UiKit.Muted)
                : UiKit.Cream;
            if (v.Name.color != nameColour) v.Name.color = nameColour;
            // The name sits left of the bar, or left of the word.
            float nameRight = bar ? -BarWidth - 12f : row.Tag.Length == 0 ? 0f : -v.TagWidth - 10f;
            ((RectTransform)v.Name.transform).anchoredPosition = new Vector2(nameRight, 0f);
        }

        PartRowView NewRow(int index)
        {
            var v = new PartRowView();
            v.Root = UiKit.NewRect("Part" + index, _damagePanel);
            UiKit.Pin(v.Root, new Vector2(1f, 1f), new Vector2(0f, -22f - index * RowHeight), new Vector2(320f, RowHeight));
            v.Name = Shadowed(UiKit.Label(v.Root, string.Empty, 15, UiKit.Cream, TextAnchor.MiddleRight, FontStyle.Normal));
            UiKit.Pin((RectTransform)v.Name.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, RowHeight));
            RectTransform back = UiKit.NewRect("Bar", v.Root);
            UiKit.Pin(back, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(BarWidth, 6f));
            UiKit.AddImage(back, new Color(0f, 0f, 0f, 0.5f), UiKit.Rounded);
            v.Bar = back.gameObject;
            v.Fill = UiKit.NewRect("Fill", back);
            v.Fill.anchorMin = new Vector2(0f, 0f);
            v.Fill.anchorMax = new Vector2(0f, 1f);
            v.Fill.pivot = new Vector2(0f, 0.5f);
            v.Fill.offsetMin = Vector2.zero;
            v.Fill.offsetMax = Vector2.zero;
            v.FillImage = UiKit.AddImage(v.Fill, UiKit.Cream);
            v.Tag = Shadowed(UiKit.Label(v.Root, string.Empty, 13, UiKit.Danger, TextAnchor.MiddleRight));
            UiKit.Pin((RectTransform)v.Tag.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(160f, RowHeight));
            return v;
        }
    }
}
