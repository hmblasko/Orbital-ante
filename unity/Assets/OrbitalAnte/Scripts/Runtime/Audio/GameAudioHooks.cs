using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OrbitalAnte.Audio
{
    /// <summary>
    /// Connects the game as it stands today to the audio systems, without the game's own classes needing to
    /// know about them: it listens for hits and damage, watches the player for footsteps and landings, drives
    /// the ship's engine sound from the thrust input, and tells the music which situation we are in.
    /// As real systems arrive (suit, ship interior, enemies with awareness) they should call
    /// <see cref="AudioDirector"/> themselves and the matching guesswork here can go.
    /// </summary>
    public sealed class GameAudioHooks : MonoBehaviour
    {
        PlayerHealth _health;
        CharacterController _walker;
        Vector3 _lastWalkerPos;
        float _stride;
        bool _wasGrounded = true;
        float _airTime;
        GameObject _selected;
        Space.TestCraft _craft;
        float _engine;
        bool _worldLoops;
        string _flightContext = "flight";
        string _wantContext = "flight";
        float _wantSince;
        float _nextGroundRead;

        void OnEnable() => CombatEvents.PlayerHitSomething += OnPlayerHit;

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            Watch(null);
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null || !outcome.Hit) return;
            d.Intensity.Report(IntensityEvent.HitDealt);
            if (outcome.Severed) d.Intensity.Report(IntensityEvent.PartSevered);
            if (outcome.Killed) d.Intensity.Report(IntensityEvent.Kill);
            // One confirmation per hit, the biggest that applies.
            if (outcome.HeadshotKill) AudioDirector.PlayLocal("hit.headshot");
            else if (outcome.Killed) AudioDirector.PlayLocal("hit.kill");
            else AudioDirector.PlayLocal("hit.marker");
        }

        void OnDamaged(float amount, Vector3 direction)
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null) return;
            d.Intensity.Report(IntensityEvent.DamageTaken, Mathf.Clamp(amount / 8f, 0.5f, 2f));
        }

        void Watch(PlayerHealth health)
        {
            if (_health == health) return;
            if (_health != null) _health.Damaged -= OnDamaged;
            _health = health;
            if (_health != null) _health.Damaged += OnDamaged;
        }

        void Update()
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null || !d.Ready) return;
            MenuSounds();
            if (AudioDemoSequence.Running) return;

            GameBootstrap game = GameBootstrap.Instance;
            if (GameSession.Mode != GameMode.Playing || game == null)
            {
                LeaveWorld(d);
                d.SetMusicContext("menu");
                d.Intensity.SetThreat(0f);
                return;
            }

            if (game.Arena != null)
            {
                Watch(game.Arena.Health);
                d.Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
                d.SetMusicContext("surface");
                // Nothing in the arena reports danger yet, so a little steady threat keeps the walking layers in.
                d.Intensity.SetThreat(24f);
                if (!GameSession.Paused) Footsteps(game.Arena.Player);
            }
            else if (game.Space != null)
            {
                Watch(null);
                Flight(d, game.Space);
            }
        }

        void LeaveWorld(AudioDirector d)
        {
            Watch(null);
            _walker = null;
            _craft = null;
            if (_worldLoops)
            {
                d.SetLoop("ship.engine.loop", 0f, 0.3f);
                d.SetLoop("ship.cabin.loop", 0f, 0.5f);
                _worldLoops = false;
            }
        }

        void Footsteps(FpsController player)
        {
            if (player == null) return;
            if (_walker == null || _walker.gameObject != player.gameObject)
            {
                _walker = player.GetComponent<CharacterController>();
                _lastWalkerPos = player.transform.position;
                _wasGrounded = true;
                _stride = 0f;
            }
            if (_walker == null) return;
            bool grounded = _walker.isGrounded;
            Vector3 pos = player.transform.position;
            Vector3 moved = pos - _lastWalkerPos;
            _lastWalkerPos = pos;
            moved.y = 0f;
            // A teleport (respawn) is not a very long step.
            if (moved.sqrMagnitude > 9f) return;

            if (grounded)
            {
                if (!_wasGrounded && _airTime > 0.25f)
                {
                    AudioDirector.PlayLocal("player.land", Mathf.Clamp01(0.5f + _airTime));
                    _stride = 0f;
                }
                _airTime = 0f;
                _stride += moved.magnitude;
                // One step about every 1.9 m: a jog at the arena's walking speed.
                if (_stride >= 1.9f)
                {
                    _stride = 0f;
                    AudioDirector.PlayLocal("player.step.regolith", 1f, 1f, Random.Range(-0.15f, 0.15f));
                }
            }
            else
            {
                _airTime += Time.deltaTime;
                // A character controller often reports "not grounded" for a frame or two while walking on flat
                // ground. Those frames still count towards the stride, or steps would go missing.
                if (_airTime < 0.15f) _stride += moved.magnitude;
            }
            // Only a real time in the air counts as leaving the ground.
            _wasGrounded = grounded || _airTime < 0.15f;
        }

        void Flight(AudioDirector d, Space.SolarSystemWorld world)
        {
            Space.TestCraft craft = world.Craft;
            if (craft == null) return;
            if (_craft != craft)
            {
                _craft = craft;
                // The craft's old code-made rumble steps aside for the recorded-quality engine loop.
                AudioSource old = craft.GetComponent<AudioSource>();
                if (old != null) { old.mute = true; old.Stop(); }
                _engine = 0f;
            }
            _worldLoops = true;
            // The player is always in the cockpit for now: pressurised, small, close.
            d.Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Cockpit };
            d.SetLoop("ship.cabin.loop", 1f, 1.5f);

            float thrust = GameSession.Paused ? 0f : Mathf.Clamp01(craft.ThrustInput.magnitude) * (craft.Boosting ? 1f : 0.7f);
            if (thrust > 0.3f && _engine < 0.05f) AudioDirector.PlayLocal("ship.engine.ignite", 0.8f);
            _engine = Mathf.MoveTowards(_engine, thrust, Time.unscaledDeltaTime * (thrust > _engine ? 3.5f : 1.8f));
            d.SetLoop("ship.engine.loop", _engine, 0.08f);
            d.SetLoopPitch("ship.engine.loop", 0.85f + 0.35f * _engine);

            // Which music: close to the ground is exploring, caves and seas and cloud decks are the unknown,
            // everything else is flying.
            // Read a few times a second, with a gap between "low enough" and "high enough" and a three-second
            // wait, so skimming along at one height cannot make the music change its mind over and over.
            Planets.PlanetWorld planets = world.Planets;
            if (planets != null && world.System != null && Time.unscaledTime >= _nextGroundRead)
            {
                _nextGroundRead = Time.unscaledTime + 0.25f;
                string want = "flight";
                Planets.GroundReading g = planets.Read(world.System.Frame.Anchor, craft.transform.position);
                if (g.Known)
                {
                    if (g.InCave || g.Underwater || g.UnderClouds) want = "mystery";
                    else if (g.AboveGround < (_flightContext == "surface" ? 500.0 : 350.0)) want = "surface";
                }
                if (want != _wantContext)
                {
                    _wantContext = want;
                    _wantSince = Time.unscaledTime;
                }
                if (_wantContext != _flightContext && Time.unscaledTime - _wantSince >= 3f) _flightContext = _wantContext;
            }
            string context = _flightContext;
            d.SetMusicContext(context);
            // Flying hard is its own excitement: thrust lifts the flight music's layers.
            // It stays under the level where combat music takes over; that is for fights.
            d.Intensity.SetThreat(context == "flight" ? 20f + 36f * _engine : 22f);
        }

        /// <summary>A tick when the highlighted button changes on any menu.</summary>
        void MenuSounds()
        {
            EventSystem es = EventSystem.current;
            GameObject now = es != null ? es.currentSelectedGameObject : null;
            bool menuUp = GameSession.Mode != GameMode.Playing || GameSession.Paused || AudioDemoSequence.Running;
            // Only when the highlight was moved with keys or a stick: a mouse click already has its own sound.
            bool steering = GameInput.Asset != null && GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f;
            if (menuUp && steering && now != null && _selected != null && now != _selected) AudioDirector.PlayLocal("ui.move");
            _selected = now;
        }
    }
}
