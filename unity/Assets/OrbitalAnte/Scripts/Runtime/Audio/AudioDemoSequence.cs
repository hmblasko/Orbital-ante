using System.Collections.Generic;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// Runs the five-minute audio showcase (<see cref="DemoTimeline"/>) from the main menu. It feeds the script's
    /// game events to the real audio systems, so what is heard is the game's own director scoring them live:
    /// nothing in it is a pre-mixed recording. A caption says which part is playing; the debug panel is shown
    /// alongside. Any of Esc, B or the button stops it.
    /// </summary>
    public sealed class AudioDemoSequence : MonoBehaviour
    {
        public static bool Running { get; private set; }

        List<DemoStep> _steps;
        int _next;
        double _start;
        Canvas _canvas;
        Text _caption;
        Text _clock;
        System.Action _onStopped;
        int _shownSecond = -1;
        readonly HashSet<string> _loops = new HashSet<string>();

        public static void ResetStatics() => Running = false;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("AudioShowcase", uiRoot, 60);
            var dim = UiKit.NewRect("Back", _canvas.transform);
            UiKit.Stretch(dim);
            UiKit.AddImage(dim, new Color(0.07f, 0.06f, 0.10f, 1f), null, true);

            var card = UiKit.Card(_canvas.transform, "ShowcaseCard", 760f);
            ((RectTransform)card).anchoredPosition = new Vector2(180f, -150f);
            UiKit.LayoutLabel(card, "AUDIO SHOWCASE", 34, UiKit.PanelEdge, 46f);
            _caption = UiKit.LayoutLabel(card, string.Empty, 22, UiKit.Cream, 64f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _clock = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Muted, 26f, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.LayoutLabel(card, "Five minutes of game events, scored live by the music director.", 16, UiKit.Muted, 24f, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Button(card, "Stop", Stop);
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Starts the showcase. False (and nothing changes) if the audio is not there to play it.</summary>
        public bool Begin(System.Action onStopped)
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null || !d.Ready || Running) return false;
            _onStopped = onStopped;
            _steps = DemoTimeline.Build();
            _next = 0;
            _start = Time.unscaledTimeAsDouble;
            _shownSecond = -1;
            Running = true;
            d.Intensity.Reset();
            d.Rewards.Clear();
            d.SetMusicContext("silence");
            _canvas.gameObject.SetActive(true);
            Button stop = _canvas.GetComponentInChildren<Button>();
            if (stop != null && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(stop.gameObject);
            return true;
        }

        public void Stop()
        {
            if (!Running) return;
            Running = false;
            AudioDirector d = AudioDirector.Instance;
            if (d != null)
            {
                foreach (string id in _loops) d.SetLoop(id, 0f, 0.6f);
                d.Intensity.Reset();
                d.Rewards.Clear();
                d.Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
            }
            _loops.Clear();
            _canvas.gameObject.SetActive(false);
            System.Action done = _onStopped;
            _onStopped = null;
            done?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            AudioDirector d = AudioDirector.Instance;
            if (d == null) { Stop(); return; }
            // Cancel as the player has it bound (Esc or B unless changed).
            if (Core.GameInput.Asset != null && Core.GameInput.UiCancel.WasPressedThisFrame())
            {
                Stop();
                return;
            }

            double t = Time.unscaledTimeAsDouble - _start;
            while (_next < _steps.Count && _steps[_next].Time <= t)
            {
                if (!Run(d, _steps[_next++])) return;
            }
            int s = (int)t;
            if (s == _shownSecond) return;
            _shownSecond = s;
            _clock.text = (s / 60) + ":" + (s % 60).ToString("00") + " / 5:00";
        }

        bool Run(AudioDirector d, DemoStep step)
        {
            switch (step.Kind)
            {
                case DemoStepKind.Caption: _caption.text = step.Text; break;
                case DemoStepKind.Context: d.SetMusicContext(step.Text); break;
                case DemoStepKind.Threat: d.Intensity.SetThreat(step.Value); break;
                case DemoStepKind.Heat: d.Intensity.Report((IntensityEvent)step.Number); break;
                case DemoStepKind.Space: d.Space = new ListenerSpace { Air = step.Value, Enclosure = (Enclosure)step.Number }; break;
                case DemoStepKind.Reward: d.Reward((RewardKind)step.Number); break;
                case DemoStepKind.Play:
                    if (step.Value2 <= 0f)
                    {
                        AudioDirector.PlayLocal(step.Text);
                    }
                    else
                    {
                        // Somewhere out in front of the listener, to the side the script asks for.
                        Transform ear = Camera.main != null ? Camera.main.transform : transform;
                        float side = Mathf.Clamp(step.Value, -1f, 1f);
                        Vector3 dir = ear.right * side + ear.forward * Mathf.Sqrt(1f - side * side);
                        AudioDirector.Play(step.Text, ear.position + dir * step.Value2);
                    }
                    break;
                case DemoStepKind.LoopOn:
                case DemoStepKind.LoopLevel:
                    d.SetLoop(step.Text, step.Value, step.Value2);
                    _loops.Add(step.Text);
                    break;
                case DemoStepKind.LoopOff:
                    d.SetLoop(step.Text, 0f, step.Value2);
                    break;
                case DemoStepKind.End:
                    Stop();
                    return false;
            }
            return true;
        }
    }
}
