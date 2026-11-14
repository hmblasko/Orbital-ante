using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// Turns the engine rules (<see cref="LaunchModel"/>) into what you see, hear and feel from the pilot's seat:
    /// the build-up before the ship leaves the ground, flame under the engine bells, smoke and grit thrown across
    /// the ground, the ground lit orange, the view shaking, and the roar. Lives on the ship.
    ///
    /// Everything scales with three things: how hard the engines push, how near the ground is and how much air
    /// there is. So a lift-off from Earth is violent, one from the Moon throws grit but no billowing smoke and no
    /// crackle, and a burn in open space is a steady hum with a faint tremor.
    /// </summary>
    // After the ship (-300) and the flight controls (0): this has the last word on how much push gets through this frame.
    [DefaultExecutionOrder(100)]
    public sealed class LaunchFx : MonoBehaviour
    {
        public LaunchModel Model { get; private set; }
        public LaunchConfig Config { get; private set; }
        public Ship.Ship Ship { get; private set; }
        public LaunchAudio Sounds { get; private set; }

        /// <summary>Metres from the engine bells to whatever is below them, or a large number when nothing is within reach.</summary>
        /// <summary>From the engine bells straight down to the ground, in metres.</summary>
        public float HeightAboveGround { get; private set; } = 1e6f;
        /// <summary>How far the bells are above the footpads: the ship's height off the ground is measured from its feet.</summary>
        const float BellsAboveFeet = OrbitalAnte.Ship.ShipLayout.EngineExitY - OrbitalAnte.Ship.ShipLayout.GroundY;
        public bool OnGround { get; private set; }
        /// <summary>True when the ship has been standing still on something for a moment: pressing up now starts the build-up.</summary>
        public bool Settled { get; private set; }
        float _touchingFor, _sliding;
        Mesh _gritMesh;
        bool _slidingKnown;
        /// <summary>How thick the air is here, 0 (space) to 1 (Earth).</summary>
        public float Air { get; private set; }
        public int SmokeAlive => _smoke != null ? _smoke.particleCount : 0;
        public int DebrisAlive => _debris != null ? _debris.particleCount : 0;
        /// <summary>The extra turn given to the pilot's view this frame.</summary>
        public Quaternion ViewShake { get; private set; } = Quaternion.identity;

        /// <summary>For tests: stands in for the pilot's "up" control (0 to 1). Negative means read the real controls.</summary>
        public float UpOverride { get; set; } = -1f;

        ParticleSystem _smoke, _debris;
        ParticleSystemRenderer _smokeRenderer;
        Transform _groundFx;
        Transform[] _flames;
        Material _flameMaterial;
        Mesh _flameMesh;
        AudioSource _roar, _crackle, _oneShot;
        readonly RaycastHit[] _hits = new RaycastHit[8];
        float _lastTouch = -10f;
        float _smokeOwed, _debrisOwed;
        Vector3 _groundPoint, _groundNormal = Vector3.up;
        int _dustBody = -2;
        float _nextDustLook;
        bool _groundKnown;

        /// <summary>Say the ship has just been set down at rest (the start of a run on the pad), so nothing waits for physics to notice.</summary>
        public void MarkResting()
        {
            _lastTouch = Time.fixedTime;
            _touchingFor = 1f;
            _sliding = 0f;
            OnGround = true;
        }

        /// <summary>The player's "engines" volume setting, so these sounds obey the same slider as the rest.</summary>
        static float BusGain => OrbitalAnte.Audio.AudioPrefs.BusGain(OrbitalAnte.Audio.AudioBus.Engines);

        public static LaunchFx Attach(Ship.Ship ship, LaunchConfig config)
        {
            var fx = ship.gameObject.AddComponent<LaunchFx>();
            fx.Ship = ship;
            ship.Launch = fx;
            fx.Config = config;
            fx.Model = new LaunchModel(config.rules);
            fx.Build();
            return fx;
        }

        void Build()
        {
            Sounds = LaunchAudio.Build();
            _roar = NewLoop(Sounds.Roar);
            _crackle = NewLoop(Sounds.Crackle);
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.spatialBlend = 0f;
            _oneShot.playOnAwake = false;

            // Flames: one bright cone under each engine bell, stretched by how hard it is burning.
            _flameMesh = ConeMesh(10);
            _flameMaterial = new Material(PlanetMaterials.VertexColour(new Color(1f, 0.62f, 0.2f), 0f, 2f, false));
            _flameMaterial.name = "OA_EngineFlame";
            _flames = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                OrbitalAnte.Ship.P3 at = OrbitalAnte.Ship.ShipLayout.Engine(i);
                var go = new GameObject("Flame" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(at.X, OrbitalAnte.Ship.ShipLayout.EngineExitY, at.Z);
                go.layer = Layers.Default;
                go.AddComponent<MeshFilter>().sharedMesh = _flameMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _flameMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _flames[i] = go.transform;
            }

            // Smoke and grit are thrown out along the ground, so they belong to the ground, not the ship.
            var fxGo = new GameObject("LaunchGroundFx");
            _groundFx = fxGo.transform;
            _smoke = NewParticles("Smoke", Mathf.Clamp(Config.maxSmoke, 8, 400), out _smokeRenderer, 0);
            _debris = NewParticles("Debris", Mathf.Clamp(Config.maxDebris, 4, 200), out ParticleSystemRenderer debrisRenderer, 3);
            // Plain colour, no painted detail: these are many, small, overlapping and gone in seconds.
            debrisRenderer.sharedMaterial = PlanetMaterials.VertexColour(new Color(0.2f, 0.18f, 0.17f), 1f, 0f, false);
            _gritMesh = GritMesh();
            debrisRenderer.mesh = _gritMesh;
            // Until the ground's own colour is known (or where there is no worked-out ground at all, like an asteroid).
            _smokeRenderer.sharedMaterial = PlanetMaterials.VertexColour(new Color(0.75f, 0.75f, 0.75f), 1f, 0f, false);

            ParticleSystem.MainModule smoke = _smoke.main;
            smoke.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
            smoke.startSpeed = new ParticleSystem.MinMaxCurve(7f, 16f);
            smoke.startSize = new ParticleSystem.MinMaxCurve(1.3f, 3.4f);
            ParticleSystem.ShapeModule smokeShape = _smoke.shape;
            smokeShape.angle = 84f;          // nearly flat: it rolls outward across the ground
            smokeShape.radius = 1.4f;
            ParticleSystem.LimitVelocityOverLifetimeModule slow = _smoke.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = 1.2f;
            slow.dampen = 0.06f;
            ParticleSystem.ForceOverLifetimeModule lift = _smoke.forceOverLifetime;
            lift.z = 1.6f;                   // "forward" is up: hot smoke rises as it slows

            ParticleSystem.MainModule grit = _debris.main;
            grit.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            grit.startSpeed = new ParticleSystem.MinMaxCurve(8f, 21f);
            grit.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.34f);
            ParticleSystem.ShapeModule gritShape = _debris.shape;
            gritShape.angle = 68f;
            gritShape.radius = 1.1f;
            ParticleSystem.SizeOverLifetimeModule gritSize = _debris.sizeOverLifetime;
            gritSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
            ParticleSystem.RotationOverLifetimeModule spin = _debris.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
        }

        AudioSource NewLoop(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.playOnAwake = false;
            return source;
        }

        /// <summary>A bit of grit: eight flat faces. Small things do not need a boulder's worth of triangles.</summary>
        static Mesh GritMesh()
        {
            Vector3[] tips = { Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.right * 0.6f, Vector3.left * 0.45f, Vector3.forward * 0.5f, Vector3.back * 0.55f };
            int[,] faces = { { 0, 4, 2 }, { 0, 3, 4 }, { 0, 5, 3 }, { 0, 2, 5 }, { 1, 2, 4 }, { 1, 4, 3 }, { 1, 3, 5 }, { 1, 5, 2 } };
            var positions = new Vector3[24];
            var colours = new Color[24];
            var triangles = new int[24];
            for (int f = 0; f < 8; f++)
                for (int k = 0; k < 3; k++)
                {
                    int i = f * 3 + k;
                    positions[i] = tips[faces[f, k]];
                    colours[i] = Color.white;
                    triangles[i] = i;
                }
            var mesh = new Mesh { name = "LaunchGrit" };
            mesh.vertices = positions;
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        ParticleSystem NewParticles(string name, int max, out ParticleSystemRenderer renderer, int meshVariant)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_groundFx, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            // The plume is under the ship, out of the pilot's sight at first. It must keep moving anyway.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;        // puffs are let out by hand, so the count follows the rules exactly
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            // Swell quickly, then shrink away to nothing: solid lumps, in keeping with the rest of the game's smoke.
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(0.65f, 0.85f), new Keyframe(1f, 0f)));
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = FeatureMeshes.Get(FeatureKind.Boulder, meshVariant);
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play(true);
            return system;
        }

        /// <summary>A cone with its wide end at the origin and its point one metre down (-Y), bright at the top and dimmer at the tip.</summary>
        static Mesh ConeMesh(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var colors = new Color[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                colors[i] = new Color(1f, 1f, 1f, 1f);
                triangles[i * 3] = i;
                triangles[i * 3 + 1] = sides;
                triangles[i * 3 + 2] = (i + 1) % sides;
            }
            vertices[sides] = new Vector3(0f, -1f, 0f);
            colors[sides] = new Color(1f, 0.55f, 0.35f, 1f);
            var mesh = new Mesh { name = "EngineFlame" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Called by the physics engine on the ship's own object for as long as it rests on anything.
        void OnCollisionStay(Collision collision)
        {
            // Only ground and things fixed to it count. Wreckage or a crew member brushing the hull in space is not "landed".
            if (collision.rigidbody != null && !collision.rigidbody.isKinematic) return;
            _lastTouch = Time.fixedTime;
            // How fast the ship is moving against whatever it touches (the ground turns with its planet, so not plain speed).
            _sliding = collision.relativeVelocity.magnitude;
            _slidingKnown = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Ship == null || Ship.Model == null || Ship.Flight == null) return;
            if (dt <= 0f)
            {
                // Paused: the engines hold their breath rather than roaring over the menu.
                ViewShake = Quaternion.identity;
                return;
            }

            TestCraft flight = Ship.Flight;
            float up = UpOverride >= 0f ? UpOverride : Mathf.Max(0f, flight.ThrustInput.y);
            bool boost = flight.Boosting;
            float health = Ship.Model.ThrustFactor;
            OnGround = Time.fixedTime - _lastTouch < 0.12f;
            LookBelow();

            // The build-up is for a ship standing still. Bouncing off a bad landing or scraping a cliff, the engines answer at once.
            if (!OnGround) _touchingFor = 0f; else _touchingFor += dt;
            bool settled = OnGround && _touchingFor >= 0.5f && flight.Body.angularVelocity.sqrMagnitude < 0.04f
                && (_slidingKnown ? _sliding < 0.5f : true);
            Settled = settled;
            Model.Tick(dt, up, boost, Model.State == EngineState.Off ? settled : OnGround, Mathf.Max(0f, HeightAboveGround - BellsAboveFeet), Air, health);
            Ship.LiftGate = Model.LiftGate;
            // The ship copied last frame's gate into the controls before this ran. Set it again now, so not one
            // physics step of full push slips out at the start of a build-up.
            flight.LiftScale = Ship.Model.ThrustFactor * Mathf.Clamp01(Model.LiftGate);
            flight.Landed = Settled;
            flight.Grounded = OnGround;

            if (Model.JustIgnited)
            {
                _oneShot.PlayOneShot(Sounds.Ignite, Config.ignitionVolume * BusGain);
                _debrisOwed += Config.rules.debrisAtIgnition;
            }
            if (Model.JustCancelled) _oneShot.Stop();
            if (Model.JustReleased) _oneShot.PlayOneShot(Sounds.Release, Config.ignitionVolume * BusGain);

            ShowFlames();
            ShowGround(dt);
            ShowSound(dt);
            ShowShake();
        }

        /// <summary>Finds the ground under the engines and how much air is about.</summary>
        void LookBelow()
        {
            Vector3 from = transform.TransformPoint(0f, OrbitalAnte.Ship.ShipLayout.EngineExitY, 0f);
            Vector3 down = -transform.up;
            float reach = Mathf.Max(1f, Config.rules.groundEffectHeight) + 5f;
            int count = Physics.RaycastNonAlloc(from, down, _hits, reach, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            _groundKnown = false;
            for (int i = 0; i < count; i++)
            {
                // Not the ship's own legs and bells.
                if (_hits[i].rigidbody == Ship.Flight.Body) continue;
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                _groundPoint = _hits[i].point;
                _groundNormal = _hits[i].normal;
                _groundKnown = true;
            }
            HeightAboveGround = _groundKnown ? best : 1e6f;

            Air = 0f;
            SolarSystem system = Ship.Flight.System;
            PlanetWorld planets = PlanetWorld.Instance;
            if (system == null || system.Frame == null || planets == null) return;
            PlanetSurface surface = planets.SurfaceOf(system.Frame.Anchor);
            if (surface == null || !surface.Ready) return;
            Vec3d p = SpaceConvert.ToVec3d(surface.Body.transform.InverseTransformPoint(transform.position));
            Air = Mathf.Clamp01((float)surface.AirAt(p).Sky);

            // The dust is the colour of the ground it is blown off. Looked up now and then, not every frame.
            if (_groundKnown && (system.Frame.Anchor != _dustBody || Time.unscaledTime >= _nextDustLook))
            {
                _dustBody = system.Frame.Anchor;
                _nextDustLook = Time.unscaledTime + 2f;
                Vec3d direction = SpaceConvert.ToVec3d(surface.Body.transform.InverseTransformPoint(_groundPoint)).Normalized;
                TerrainPoint point = surface.Terrain.Sample(direction);
                surface.Terrain.ColorAt(direction, point, 0.0, out float r, out float g, out float b);
                // Blown dust is paler than the ground it came from. Rounded off so a handful of materials cover every world.
                var dust = new Color(Step(Mathf.Lerp(r, 0.92f, 0.5f)), Step(Mathf.Lerp(g, 0.9f, 0.5f)), Step(Mathf.Lerp(b, 0.86f, 0.5f)), 1f);
                _smokeRenderer.sharedMaterial = PlanetMaterials.VertexColour(dust, 1f, 0f, false);
            }
        }

        static float Step(float v) => Mathf.Round(Mathf.Clamp01(v) * 8f) / 8f;

        void ShowFlames()
        {
            float burn = Model.Burn;
            bool lost = Ship.Model.State(Ship.Model.Find(OrbitalAnte.Ship.PartKind.Engine)) == OrbitalAnte.Ship.PartState.Lost;
            bool on = burn > 0.01f && !lost;
            float flicker = 0.88f + 0.24f * Mathf.PerlinNoise(Time.time * 23f, 0.3f);
            // Thin air or none lets the flame spread wide and faint; thick air squeezes it long and narrow.
            float width = OrbitalAnte.Ship.ShipLayout.EngineExitRadius * Mathf.Lerp(1.5f, 0.85f, Air);
            float length = Mathf.Max(0.05f, Config.flameLength * burn * flicker);
            // A flame cannot be longer than the gap to the ground: there it flattens out instead.
            if (_groundKnown) length = Mathf.Min(length, Mathf.Max(0.3f, HeightAboveGround));
            for (int i = 0; i < _flames.Length; i++)
            {
                if (_flames[i].gameObject.activeSelf != on) _flames[i].gameObject.SetActive(on);
                if (on) _flames[i].localScale = new Vector3(width, length, width);
            }

            if (on && _groundKnown && Model.GroundGlow > 0.01f)
            {
                SkyGlobals.FlarePosition = _groundPoint + _groundNormal * 1.5f;
                SkyGlobals.FlareReach = Config.groundLightReach;
                SkyGlobals.FlareColor = new Color(1f, 0.62f, 0.3f) * (Config.groundLight * Model.GroundGlow * flicker);
            }
            else SkyGlobals.FlareColor = Color.black;
        }

        void ShowGround(float dt)
        {
            if (!_groundKnown || (Model.SmokeRate <= 0f && _debrisOwed < 1f && Model.DebrisRate <= 0f))
            {
                _smokeOwed = 0f;
                if (!_groundKnown) _debrisOwed = 0f;
                return;
            }
            SolarSystem system = Ship.Flight.System;
            Transform ground = system != null && system.Bodies != null && system.Frame != null ? system.Bodies[system.Frame.Anchor].transform : null;
            if (ground != null && _groundFx.parent != ground)
            {
                // Smoke stays with the ground it was blown across, however the planet is turning.
                _groundFx.SetParent(ground, true);
                ParticleSystem.MainModule a = _smoke.main, b = _debris.main;
                a.customSimulationSpace = ground;
                b.customSimulationSpace = ground;
            }
            // The emitters point "forward", so forward is aimed straight out of the ground.
            _groundFx.SetPositionAndRotation(_groundPoint + _groundNormal * 0.3f,
                Quaternion.LookRotation(_groundNormal, Mathf.Abs(_groundNormal.y) < 0.9f ? Vector3.up : Vector3.right));

            float gravity = Ship.Flight.Gravity != null ? Ship.Flight.Gravity.FeltGravity.magnitude : 9.8f;
            ParticleSystem.ForceOverLifetimeModule fall = _debris.forceOverLifetime;
            fall.z = -gravity;

            _smokeOwed += Model.SmokeRate * dt;
            _debrisOwed += Model.DebrisRate * dt;
            int smoke = Mathf.Min(12, Mathf.FloorToInt(_smokeOwed));
            if (smoke > 0)
            {
                _smokeOwed -= smoke;
                _smoke.Emit(smoke);
            }
            int grit = Mathf.Min(40, Mathf.FloorToInt(_debrisOwed));
            if (grit > 0)
            {
                _debrisOwed -= grit;
                _debris.Emit(grit);
            }
        }

        void ShowSound(float dt)
        {
            float bus = BusGain;
            float roar = Model.Roar * Config.roarVolume * bus;
            float crackle = Model.Crackle * Config.crackleVolume * bus;
            _roar.volume = Mathf.MoveTowards(_roar.volume, roar, dt * 6f);
            _crackle.volume = Mathf.MoveTowards(_crackle.volume, crackle, dt * 6f);
            _roar.pitch = 0.82f + 0.3f * Model.Burn;
            _crackle.pitch = 0.9f + 0.2f * Model.Burn;
            Keep(_roar);
            Keep(_crackle);
        }

        static void Keep(AudioSource source)
        {
            if (source.volume > 0.001f && !source.isPlaying) source.Play();
            else if (source.volume <= 0.001f && source.isPlaying) source.Stop();
        }

        void ShowShake()
        {
            float degrees = FeelSettings.CameraShake ? Model.Shake : 0f;
            if (degrees <= 0.001f)
            {
                ViewShake = Quaternion.identity;
                return;
            }
            float t = Time.time * Mathf.Max(1f, Config.shakeSpeed);
            float pitch = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * degrees;
            float yaw = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * degrees * 0.7f;
            float roll = (Mathf.PerlinNoise(t, t + 5.3f) - 0.5f) * 2f * degrees * 0.6f;
            ViewShake = Quaternion.Euler(pitch, yaw, roll);
        }

        // After the astronaut has aimed the eye for this frame: the shake turns the view, never moves the head.
        void LateUpdate()
        {
            if (Ship == null || Ship.Flight == null || Ship.Flight.Eye == null) return;
            if (ViewShake == Quaternion.identity) return;
            // Only someone aboard is shaken by the ship.
            if (!Ship.Flight.Eye.transform.IsChildOf(transform)) return;
            Ship.Flight.Eye.transform.localRotation *= ViewShake;
        }

        void OnDisable()
        {
            SkyGlobals.FlareColor = Color.black;
            if (Ship != null) Ship.LiftGate = 1f;
        }

        void OnDestroy()
        {
            if (Sounds != null) Sounds.Dispose();
            if (_flameMaterial != null) Destroy(_flameMaterial);
            if (_flameMesh != null) Destroy(_flameMesh);
            if (_gritMesh != null) Destroy(_gritMesh);
            if (_groundFx != null) Destroy(_groundFx.gameObject);
        }
    }
}
