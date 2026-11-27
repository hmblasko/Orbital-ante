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
        Text _prompt, _notice, _damage;
        RectTransform _barBack, _barFill, _dot;
        string _shipNotice = string.Empty;
        float _shipNoticeTime = -100f;
        bool _shipNoticeBad;
        int _shipNoticeRank;
        float _nextList;
        bool _flightShown = true;
        bool _listOutside, _listShown = true;

        /// <summary>
        /// Out on foot or driving, the ante readout and its "+points" lines fill the top right corner down to about
        /// -226 (since 25 Nov 2026 the bonus mission line pushes them to about -260), so the ship's list of hurt parts moves under them (24 Nov 2026 playtest pass: the two overlapped).
        /// </summary>
        const float ListTopAboard = -110f, ListTopOutside = -134f;
        /// <summary>Away from the ship the list is only clutter over a fight; it comes back within this many metres.</summary>
        const float ListNearShip = 45f;
        readonly StringBuilder _text = new StringBuilder(256);

        public void Build(Ship ship, Astronaut astronaut, FlightHud flightHud, SystemMap map)
        {
            _ship = ship;
            _astronaut = astronaut;
            _flightHud = flightHud;
            _map = map;
            _canvas = UiKit.CreateCanvas("ShipHUD", transform, 10);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            _dot = UiKit.NewRect("AimDot", _canvas.transform);
            // The aiming mark: corner brackets round a small orange square (the "Hull stencil" screen, 26 Nov 2026).
            UiKit.Pin(_dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            UiKit.AddImage(_dot, new Color(1f, 1f, 1f, 0.9f), HullKit.Reticle);

            _prompt = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 22, HullKit.Paint));
            UiKit.Halo(_prompt, true);
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

            _notice = Shadowed(HullKit.AsStencil(UiKit.Label(_canvas.transform, string.Empty, 26, UiKit.PanelEdge)));
            UiKit.Pin((RectTransform)_notice.transform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1000f, 36f));

            _damage = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Cream, TextAnchor.UpperRight));
            UiKit.Pin((RectTransform)_damage.transform, new Vector2(1f, 1f), new Vector2(-22f, -110f), new Vector2(420f, 440f));

            ship.PartDamaged += OnPartDamaged;
            ship.PartRepaired += OnPartRepaired;
        }

        /// <summary>Where the list of hurt parts goes, and whether it shows, for where the player is.</summary>
        void PlaceList()
        {
            AstronautMode mode = _astronaut.Mode;
            bool outside = mode == AstronautMode.Outside || mode == AstronautMode.Driving;
            if (outside != _listOutside)
            {
                _listOutside = outside;
                ((RectTransform)_damage.transform).anchoredPosition = new Vector2(-22f, outside ? ListTopOutside : ListTopAboard);
            }
            bool shown = !outside || _astronaut.Eye == null
                         || (_astronaut.Eye.transform.position - _ship.transform.position).sqrMagnitude <= ListNearShip * ListNearShip;
            if (shown != _listShown)
            {
                _listShown = shown;
                _damage.gameObject.SetActive(shown);
            }
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
            PlaceList();
            _text.Length = 0;
            ShipModel model = _ship.Model;
            for (int i = 0; i < model.Count; i++)
            {
                PartState state = model.State(i);
                // Anything short of perfect is listed, so there is no hidden damage.
                if (state == PartState.Sound && !model.NeedsRepair(i)) continue;
                _text.Append(model.Parts[i].Name).Append(": ");
                if (state == PartState.Lost) _text.Append("GONE");
                else if (state == PartState.Broken) _text.Append(model.TearProgress(i) > 0.5f ? "BROKEN, COMING LOOSE" : "BROKEN");
                else _text.Append(Mathf.RoundToInt(model.Fraction(i) * 100f)).Append('%');
                _text.Append('\n');
            }
            if (!model.CabinIntact) _text.Append("Cabin open to the outside\n");
            string list = _text.ToString();
            if (_damage.text != list) _damage.text = list;
        }
    }
}
