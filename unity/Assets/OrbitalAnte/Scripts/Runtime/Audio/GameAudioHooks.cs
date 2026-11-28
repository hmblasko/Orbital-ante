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
        float _lastDing = -100f;
        // What the danger is, for the music (MusicDanger): read a few times a second.
        readonly MusicDanger _danger = new MusicDanger();
        float _nextHuntRead;
        int _aware;
        bool _coming, _creatureAware;
        float _creatureAt = -1000f;
        Ship.Astronaut _crew;
        // The arena or solar world last played: a new one (a restart goes straight from one world to the next) starts calm.
        Component _world;

        void OnEnable()
        {
            CombatEvents.PlayerHitSomething += OnPlayerHit;
            AI.CombatWorld.CreatureWindup += OnCreatureWindup;
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            AI.CombatWorld.CreatureWindup -= OnCreatureWindup;
            Watch(null);
        }

        // A creature began a warning at the player: that is a fight, even though nothing has been fired.
        void OnCreatureWindup(Creatures.IWindup source) => _creatureAt = Time.time;

        /// <summary>
        /// The danger the music follows: the place's calm, and over it being hunted or a fight; and, told to the
        /// director, whether someone is after the player (no rests) and whether the last plates thin the music.
        /// <paramref name="counted"/>: who is after the player is counted here (the solar system, not the range).
        /// </summary>
        float Threat(AudioDirector d, float calm, PlayerHealth armour, bool counted)
        {
            var sense = new DangerSense
            {
                Calm = calm,
                SinceSeen = Time.time - AI.CombatWorld.PlayerLastSeenAt,
                SinceCreature = Time.time - _creatureAt,
                Aware = _aware,
                Coming = _coming,
                CreatureAware = _creatureAware,
                Counted = counted,
                Armour = armour != null && armour.Max > 0f ? armour.Fraction : 1f,
            };
            float threat = _danger.Threat(sense);
            d.SetMusicDanger(_danger.SomeoneAfter, _danger.OnTheLastPlates);
            return threat;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null || !outcome.Hit) return;
            d.Intensity.Report(IntensityEvent.HitDealt);
            if (outcome.Severed) d.Intensity.Report(IntensityEvent.PartSevered);
            if (outcome.Killed) d.Intensity.Report(IntensityEvent.Kill);
            // One ding per hit, the biggest that applies, and only for hits on living enemies (UI.HitConfirm):
            // rocks, the ship's walls and loose limbs stay quiet. Quieter in a burst, so it never nags.
            // A creature's armour that held gets a dull knock instead of the bell, its weak place the head's bell (HitZone).
            UI.HitKind kind = UI.HitConfirm.Classify(outcome.Hit, outcome.Enemy, outcome.Head, outcome.Severed,
                outcome.Disarmed, outcome.Killed, outcome.HeadshotKill, outcome.Zone);
            string ding = UI.HitConfirm.Ding(kind);
            if (ding == null) return;
            float now = Time.unscaledTime;
            AudioDirector.PlayLocal(ding, UI.HitConfirm.DingVolume(kind, now - _lastDing), UI.HitConfirm.DingPitch(kind));
            _lastDing = now;
        }

        void OnDamaged(float amount, Vector3 direction)
        {
            AudioDirector d = AudioDirector.Instance;
            if (d == null) return;
            // A crash that hurts (the rover's) is not an attack, and Part B wants no crash music: the music never hears it.
            if (_crew != null && _crew.TakingAKnock) return;
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
                d.SetMusicDanger(false, false);
                _danger.Reset();
                _aware = 0;
                _coming = false;
                _creatureAware = false;
                _creatureAt = -1000f;
                return;
            }

            NewWorld(d, game.Arena != null ? (Component)game.Arena : game.Space);
            if (game.Arena != null)
            {
                Watch(game.Arena.Health);
                _crew = null;
                d.Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
                d.SetMusicContext("surface");
                // A little steady threat keeps the walking layers in; sightings, creatures and the last plates go over
                // it. The range has no camps to hunt the player, so nobody is ever "coming".
                _aware = 0;
                _coming = false;
                _creatureAware = false;
                d.Intensity.SetThreat(Threat(d, 24f, game.Arena.Health, false));
                if (!GameSession.Paused) Footsteps(game.Arena.Player);
            }
            else if (game.Space != null)
            {
                // The suit's armour: being hurt is heard (DamageTaken), and so are its last plates.
                Ship.Astronaut crew = game.Space.Astronaut;
                _crew = crew;
                Watch(crew != null ? crew.Armour : null);
                Flight(d, game.Space);
            }
        }

        /// <summary>
        /// A world not played before starts calm. "Try again" on the death page goes straight from one run's world to
        /// the next without passing the menu, and the death page stops the game's clock, so a creature's warning just
        /// before dying would still be a fight on the new run's pad, and the old fight's heat would carry over too.
        /// </summary>
        void NewWorld(AudioDirector d, Component world)
        {
            if (_world == world) return;
            _world = world;
            _creatureAt = -1000f;
            _danger.Reset();
            _aware = 0;
            _coming = false;
            _creatureAware = false;
            _nextHuntRead = 0f;
            d.Intensity.Reset();
            d.SetMusicDanger(false, false);
        }

        void LeaveWorld(AudioDirector d)
        {
            Watch(null);
            _crew = null;
            _creatureAt = -1000f;
            _walker = null;
            _craft = null;
            if (_worldLoops)
            {
                d.StopShipThrust(0.3f);
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
                // Asked for by name: the ship keeps its other sounds (belt, hatch, lift-off) on the same object.
                AudioSource old = craft.ThrusterSound;
                if (old != null) { old.mute = true; old.Stop(); }
                // The craft's stop-gap jet hiss too: the directional thruster loops below replace it.
                AudioSource jets = craft.JetSound;
                if (jets != null) { jets.mute = true; jets.Stop(); }
                _engine = 0f;
            }
            _worldLoops = true;
            // Where the listener really is: in the cabin (pressurised, small, close), in the lift shaft, or outside
            // in whatever air the place has.
            Ship.Astronaut crew = world.Astronaut;
            Ship.AstronautMode mode = crew != null ? crew.Mode : Ship.AstronautMode.Seated;
            bool inCabin = mode == Ship.AstronautMode.Seated || mode == Ship.AstronautMode.Aboard;
            float outsideAir = world.Engines != null ? world.Engines.Air : 1f;
            if (inCabin) d.Space = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Cockpit };
            else if (mode == Ship.AstronautMode.Lift) d.Space = new ListenerSpace { Air = Mathf.Max(outsideAir, 0.5f), Enclosure = Enclosure.Interior };
            else d.Space = new ListenerSpace { Air = outsideAir, Enclosure = Enclosure.Open };
            d.SetLoop("ship.cabin.loop", inCabin ? 1f : 0f, 1.5f);

            // Each direction of push has its own thruster sound, heard from where that thruster sits.
            // Firing is what the thrusters really push (pilot, autopilot or Match Velocity), already scaled by boost.
            Vector3 push = GameSession.Paused ? Vector3.zero : craft.Firing;
            float before = d.Thrusters.Level(Thruster.Main);
            d.SetShipThrust(push, GameSession.Paused ? 0f : craft.TurnHeard, 1f);
            float main = d.Thrusters.Level(Thruster.Main);
            if (push.z > 0.3f && before < 0.05f && main >= 0.05f) AudioDirector.PlayLocal("ship.engine.ignite", 0.8f);
            // How busy the flying is, for the music: the main engine counts in full, manoeuvring jets for less.
            _engine = main;
            for (int i = 1; i < ThrusterMix.Count; i++) _engine = Mathf.Max(_engine, 0.6f * d.Thrusters.Level((Thruster)i));

            // Which music: close to the ground is exploring, caves and seas and cloud decks are the unknown,
            // everything else is flying.
            // Read a few times a second, with a gap between "low enough" and "high enough" and a three-second
            // wait, so skimming along at one height cannot make the music change its mind over and over.
            Planets.PlanetWorld planets = world.Planets;
            if (planets != null && world.System != null && Time.unscaledTime >= _nextGroundRead)
            {
                _nextGroundRead = Time.unscaledTime + 0.25f;
                string want = "flight";
                // Where the listener is: out on foot that is the player (a cave or the sea they have walked or swum
                // into), not the ship they left.
                Vector3 ears = crew != null && crew.InTheOpen && crew.Eye != null ? crew.Eye.transform.position : craft.transform.position;
                Planets.GroundReading g = planets.Read(world.System.Frame.Anchor, ears);
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
            // Who is after the player, read with the ground a few times a second.
            if (Time.unscaledTime >= _nextHuntRead)
            {
                _nextHuntRead = Time.unscaledTime + 0.25f;
                if (world.Enemies != null) world.Enemies.Hunters(out _aware, out _coming);
                else { _aware = 0; _coming = false; }
                // A sea creature that has noticed the swimmer: tension while it comes, and its fight holds between strikes.
                _creatureAware = world.Seas != null && world.Seas.AnyAware;
            }
            // Flying hard is its own excitement: thrust lifts the flight music's layers, still under the fight music.
            // A run's later antes are a little fuller (MusicDanger.Calm); danger goes over the top.
            Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
            int ante = loop != null && loop.Run != null ? loop.Run.Director.Ante : 0;
            d.Intensity.SetThreat(Threat(d, MusicDanger.Calm(context == "flight", _engine, ante), crew != null ? crew.Armour : null, true));
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
