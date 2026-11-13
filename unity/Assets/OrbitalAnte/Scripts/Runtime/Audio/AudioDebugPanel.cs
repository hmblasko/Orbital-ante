using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// A readout of what the audio systems are doing right now: the music situation, the piece and where in the
    /// bar it is, the intensity number and what is feeding it, which layers are up, how many voices are busy,
    /// and the director's last few decisions. F9 shows or hides it. It is always shown during the audio showcase.
    /// </summary>
    public sealed class AudioDebugPanel : MonoBehaviour
    {
        public static bool Visible;

        readonly StringBuilder _sb = new StringBuilder(1024);
        readonly System.Collections.Generic.List<string> _loopNames = new System.Collections.Generic.List<string>();
        string _text = "";
        float _nextBuild;
        Drawer _drawer;

        public static void ResetStatics() => Visible = false;

        void Awake()
        {
            // Drawing lives on its own component that is switched off while hidden: an OnGUI method costs a
            // little every frame just by existing on an enabled component.
            var go = new GameObject("AudioDebugDrawer");
            go.transform.SetParent(transform, false);
            _drawer = go.AddComponent<Drawer>();
            _drawer.Panel = this;
            _drawer.enabled = false;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) Visible = !Visible;
            bool show = Visible || AudioDemoSequence.Running;
            if (_drawer.enabled != show) _drawer.enabled = show;
            AudioDirector director = AudioDirector.Instance;
            if (director != null && director.Music != null) director.Music.KeepLog = show;
            if (!show) return;
            // Building the text a few times a second is plenty and keeps this off the frame budget.
            if (Time.unscaledTime < _nextBuild) return;
            _nextBuild = Time.unscaledTime + 0.1f;
            _text = Describe();
        }

        static string Bar(float value01, int width)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(value01 * width), 0, width);
            return new string('#', n) + new string('.', width - n);
        }

        string Describe()
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null) return "Audio: no director";
            _sb.Length = 0;
            _sb.Append("AUDIO  (F9 hides)\n");
            if (!d.Ready)
            {
                _sb.Append("The audio files are missing: old placeholder sounds are in use.");
                return _sb.ToString();
            }
            IntensityModel im = d.Intensity;
            _sb.Append("Intensity ").Append(im.Value.ToString("00")).Append("  ").Append(Bar(im.Value / 100f, 25)).Append("  ").Append(im.Band)
                .Append("\n  threat ").Append(im.Threat.ToString("0")).Append("   heat ").Append(im.Heat.ToString("0")).Append('\n');

            MusicBrain brain = d.Music.Brain;
            double now = AudioSettings.dspTime;
            _sb.Append("Situation: ").Append(brain.Context).Append(brain.InCombat ? "  [combat]" : "").Append(brain.Resting(now) ? "  [resting]" : "").Append('\n');
            MusicTrackDef t = brain.Track;
            if (t == null)
            {
                _sb.Append("Music: silence\n");
            }
            else
            {
                _sb.Append("Music: ").Append(t.Title).Append("  ").Append(t.Bpm.ToString("0")).Append(" bpm");
                if (brain.Position(now, out int bar, out double beat)) _sb.Append("   bar ").Append(bar + 1).Append('/').Append(t.Bars).Append("  beat ").Append((int)beat + 1);
                else _sb.Append("   starts in ").Append((brain.TrackStart - now).ToString("0.0")).Append(" s");
                // 0 means every layer is on exactly the same sample. Anything else means they started apart.
                _sb.Append("   stems apart by ").Append(d.Music.StemSpread()).Append(" samples\n");
                for (int i = 0; i < t.Stems.Length; i++)
                {
                    StemDef s = t.Stems[i];
                    _sb.Append("  ").Append(s.Id.PadRight(8)).Append(Bar(d.Music.StemLevel(i), 10)).Append("  plays ").Append(s.Lo.ToString("0")).Append('-').Append(s.Hi.ToString("0")).Append('\n');
                }
            }
            for (int i = Mathf.Max(0, d.Music.Log.Count - 4); i < d.Music.Log.Count; i++) _sb.Append("  > ").Append(d.Music.Log[i]).Append('\n');

            VoiceAllocator va = d.Allocator;
            _sb.Append("Voices ").Append(va.Active(d.VoiceClock)).Append('/').Append(va.Count)
                .Append("   taken over ").Append(va.Stolen).Append("   turned away ").Append(va.Refused).Append('\n');
            _sb.Append("Loops:");
            _loopNames.Clear();
            d.ListLoops(_loopNames);
            for (int i = 0; i < _loopNames.Count; i++) _sb.Append(' ').Append(_loopNames[i]);
            _sb.Append("\nSpace: air ").Append(d.Space.Air.ToString("0.00")).Append("  ").Append(d.Space.Enclosure).Append(d.Space.Underwater ? "  underwater" : "")
                .Append("\nLast sound: ").Append(d.LastEvent)
                .Append("\nRewards waiting ").Append(d.Rewards.WaitingCount).Append("   dropped ").Append(d.Rewards.Dropped);
            return _sb.ToString();
        }

        sealed class Drawer : MonoBehaviour
        {
            public AudioDebugPanel Panel;
            GUIStyle _style;
            Texture2D _back;

            void Awake() => useGUILayout = false;

            void OnGUI()
            {
                if (Panel == null) return;
                if (_style == null)
                {
                    _back = new Texture2D(1, 1);
                    _back.SetPixel(0, 0, new Color(0.05f, 0.04f, 0.04f, 0.8f));
                    _back.Apply();
                    _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false, alignment = TextAnchor.UpperLeft };
                    _style.normal.textColor = new Color(0.96f, 0.91f, 0.80f);
                    _style.normal.background = _back;
                    _style.padding = new RectOffset(10, 10, 8, 8);
                    Font mono = Font.CreateDynamicFontFromOSFont(new[] { "DejaVu Sans Mono", "Consolas", "Courier New", "Menlo" }, 14);
                    if (mono != null) _style.font = mono;
                }
                // Drawn at the size of the 1280x800 Deck screen and scaled to whatever the window is.
                float scale = Mathf.Max(0.5f, Screen.height / 800f);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                Vector2 size = _style.CalcSize(new GUIContent(Panel._text));
                GUI.Label(new Rect(12f, 12f, Mathf.Max(430f, size.x), size.y), Panel._text, _style);
            }
        }
    }
}
