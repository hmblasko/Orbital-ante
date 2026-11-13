using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// What a storm looks and sounds like from inside it: rain, snow, blowing dust, acid drizzle, methane rain
    /// or falling diamonds round the viewer; lightning in the body's own colour with a flash and thunder that
    /// arrives late if the strike was far off; and the sound of the wind.
    ///
    /// Where the storms are and how strong is decided by <see cref="WeatherModel"/> (seeded, the same for a
    /// seed). This only draws them. The streaks, flashes and sounds themselves are visual scatter and use
    /// Unity's own random numbers, as the repository's rules allow.
    /// </summary>
    public sealed class PlanetWeatherFx : MonoBehaviour
    {
        const int MaxParticles = 700;

        PlanetWorld _world;
        ParticleSystem _fall;
        ParticleSystemRenderer _fallRenderer;
        Material _fallMaterial;
        WeatherSpec _shownSpec;
        LineRenderer _bolt;
        Material _boltMaterial;
        AudioSource _thunder;
        AudioSource _wind;
        AudioClip[] _thunderClips;
        float _nextStrike;
        float _boltLeft;
        float _flashLevel;
        Color _flashColor;
        float _thunderDue = -1f;
        float _thunderVolume;
        readonly Vector3[] _boltPoints = new Vector3[10];

        /// <summary>The light a lightning flash is adding right now. Black when there is none.</summary>
        public Color Flash => _flashColor * _flashLevel;
        public int Strikes { get; private set; }
        public float Intensity { get; private set; }

        public void Build(PlanetWorld world)
        {
            _world = world;

            var fallGo = new GameObject("Falling");
            fallGo.transform.SetParent(transform, false);
            _fall = fallGo.AddComponent<ParticleSystem>();
            _fall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _fall.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.startLifetime = 1.6f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = _fall.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = _fall.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 2f, 70f);
            ParticleSystem.VelocityOverLifetimeModule velocity = _fall.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            _fallRenderer = fallGo.GetComponent<ParticleSystemRenderer>();
            _fallMaterial = new Material(Mat.Unlit(Color.white)) { name = "Weather" };
            _fallRenderer.sharedMaterial = _fallMaterial;
            _fallRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fallRenderer.receiveShadows = false;

            var boltGo = new GameObject("Lightning");
            boltGo.transform.SetParent(transform, false);
            _bolt = boltGo.AddComponent<LineRenderer>();
            _bolt.useWorldSpace = true;
            _bolt.positionCount = _boltPoints.Length;
            _bolt.numCornerVertices = 0;
            _bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bolt.receiveShadows = false;
            // The game's own shader ignores haze, so a bolt shows through the murk of the storm it belongs to.
            _boltMaterial = PlanetMaterials.OwnBody
                ? PlanetMaterials.Body("Lightning", null, Color.white, 1f, 0f)
                : new Material(Mat.Unlit(Color.white)) { name = "Lightning" };
            _bolt.sharedMaterial = _boltMaterial;
            _bolt.enabled = false;

            _thunder = gameObject.AddComponent<AudioSource>();
            _thunder.playOnAwake = false;
            _thunder.spatialBlend = 0f;
            _wind = gameObject.AddComponent<AudioSource>();
            _wind.playOnAwake = false;
            _wind.loop = true;
            _wind.spatialBlend = 0f;
            _wind.volume = 0f;
            _wind.clip = MakeWind();
            _thunderClips = new[] { MakeThunder(1), MakeThunder(2), MakeThunder(3) };
            _nextStrike = 3f;
        }

        void OnDestroy()
        {
            if (_fallMaterial != null) Destroy(_fallMaterial);
            if (_boltMaterial != null) Destroy(_boltMaterial);
        }

        void LateUpdate()
        {
            if (_world == null || _world.Sky == null || _world.System == null) return;
            Camera cam = _world.System.ViewCamera;
            if (cam == null) return;
            PlanetSky sky = _world.Sky;
            PlanetSurface s = sky.Surface;
            WeatherSample weather = sky.Weather;
            float intensity = s != null && weather.Spec != null ? (float)weather.Intensity : 0f;
            // No weather indoors: not in caves, and (for now) no rain reaching the viewer under water.
            if (sky.Ground.InCave || sky.Air.Underwater) intensity = 0f;
            Intensity = intensity;
            float dt = Time.deltaTime;

            UpdateFalling(cam, s, weather, intensity);
            UpdateLightning(cam, s, weather, intensity, dt);

            // The player's effects volume setting applies to these older code-made sounds too.
            float volume = _world.Settings.thunderVolume * Audio.AudioPrefs.BusGain(Audio.AudioBus.Environment);
            float windTarget = Mathf.Clamp01(intensity * 1.2f) * 0.55f * volume;
            _wind.volume = Mathf.MoveTowards(_wind.volume, windTarget, dt * 0.4f);
            if (_wind.volume > 0.002f && !_wind.isPlaying) _wind.Play();
            else if (_wind.volume <= 0.002f && _wind.isPlaying) _wind.Stop();
            if (intensity > 0f && weather.Spec != null) _wind.pitch = Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01((float)weather.Spec.windSpeed / 40f));
        }

        // ------------------------------------------------------------------ rain, snow, dust

        void UpdateFalling(Camera cam, PlanetSurface s, WeatherSample weather, float intensity)
        {
            float amount = _world.Settings.weatherParticles;
            ParticleSystem.EmissionModule emission = _fall.emission;
            if (intensity <= 0.02f || amount <= 0f || s == null || weather.Spec.kind == WeatherKind.Thunder)
            {
                emission.rateOverTime = 0f;
                if (_fall.isPlaying && _fall.particleCount == 0) _fall.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            Transform body = s.Body.transform;
            Vector3 eye = cam.transform.position;
            Vector3 up = (eye - body.position).normalized;
            Vector3 wind = body.TransformDirection(SpaceConvert.ToVector3(weather.Wind));
            Vector3 along = Vector3.ProjectOnPlane(wind, up);
            Vector3 forward = along.sqrMagnitude > 0.01f ? along.normalized : Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;

            WeatherSpec spec = weather.Spec;
            if (spec != _shownSpec)
            {
                _shownSpec = spec;
                Configure(spec, s);
            }

            float fallSpeed, life, height, rate;
            switch (spec.kind)
            {
                case WeatherKind.Snow: fallSpeed = 2.6f; life = 6f; height = 14f; rate = 110f; break;
                case WeatherKind.Dust: fallSpeed = 0.4f; life = 3f; height = 6f; rate = 200f; break;
                case WeatherKind.Methane: fallSpeed = 5f; life = 4.5f; height = 20f; rate = 90f; break;
                case WeatherKind.Diamond: fallSpeed = 26f; life = 1.3f; height = 30f; rate = 60f; break;
                case WeatherKind.Acid: fallSpeed = 12f; life = 2.2f; height = 24f; rate = 150f; break;
                default: fallSpeed = 21f; life = 1.5f; height = 28f; rate = 230f; break;
            }

            // The emitter is a flat box above (and a little upwind of) the viewer, lying along the ground.
            Transform t = _fall.transform;
            float windSpeed = along.magnitude;
            Vector3 upwind = -forward * Mathf.Min(40f, windSpeed * life * 0.5f);
            t.SetPositionAndRotation(eye + up * height + upwind, Quaternion.LookRotation(forward, up));

            ParticleSystem.MainModule main = _fall.main;
            main.customSimulationSpace = body;
            main.startLifetime = life;
            ParticleSystem.VelocityOverLifetimeModule velocity = _fall.velocityOverLifetime;
            velocity.x = 0f;
            velocity.y = -fallSpeed;
            velocity.z = windSpeed;
            emission.rateOverTime = Mathf.Min(rate * intensity * amount, MaxParticles / life);
            // Unlit specks would glow at night, so dim them with the daylight.
            Color c = s.ColorOf(spec) * Mathf.Lerp(0.25f, 1f, _world.Sky.Day);
            c.a = 1f;
            PlanetMaterials.SetColor(_fallMaterial, c);
            if (!_fall.isPlaying) _fall.Play(true);
        }

        void Configure(WeatherSpec spec, PlanetSurface s)
        {
            ParticleSystem.MainModule main = _fall.main;
            bool streak = spec.kind == WeatherKind.Rain || spec.kind == WeatherKind.Acid || spec.kind == WeatherKind.Methane || spec.kind == WeatherKind.Diamond;
            if (streak)
            {
                _fallRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                _fallRenderer.velocityScale = spec.kind == WeatherKind.Methane ? 0.07f : 0.035f;
                _fallRenderer.lengthScale = 1.5f;
                main.startSize = spec.kind == WeatherKind.Methane ? 0.07f : spec.kind == WeatherKind.Diamond ? 0.05f : 0.035f;
            }
            else
            {
                _fallRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                main.startSize = spec.kind == WeatherKind.Snow
                    ? new ParticleSystem.MinMaxCurve(0.04f, 0.09f)
                    : new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            }
            _fall.Clear();
        }

        // ------------------------------------------------------------------ lightning

        void UpdateLightning(Camera cam, PlanetSurface s, WeatherSample weather, float intensity, float dt)
        {
            _flashLevel = Mathf.MoveTowards(_flashLevel, 0f, dt * 5f);
            if (_boltLeft > 0f)
            {
                _boltLeft -= dt;
                // A bolt flickers rather than glowing steadily.
                _bolt.enabled = _boltLeft > 0f && Mathf.Repeat(_boltLeft, 0.07f) > 0.02f;
            }
            if (_thunderDue >= 0f)
            {
                _thunderDue -= dt;
                if (_thunderDue < 0f)
                {
                    _thunder.pitch = Random.Range(0.85f, 1.1f);
                    _thunder.PlayOneShot(_thunderClips[Random.Range(0, _thunderClips.Length)], _thunderVolume);
                }
            }

            if (s == null || weather.Spec == null || intensity < 0.2f) return;
            Color color = s.LightningOf(weather.Spec);
            if (color.a <= 0f) return;

            _nextStrike -= dt * Mathf.Lerp(0.4f, 1.6f, intensity);
            if (_nextStrike > 0f) return;
            _nextStrike = Random.Range(2.2f, 7.5f);
            Strike(cam, s, color, intensity);
        }

        void Strike(Camera cam, PlanetSurface s, Color color, float intensity)
        {
            Strikes++;
            Vector3 eye = cam.transform.position;
            Vector3 up = (eye - s.Body.transform.position).normalized;
            // Somewhere round about, more often in front than behind, so strikes get seen.
            Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, up);
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.Cross(up, cam.transform.right);
            flatForward.Normalize();
            float angle = Random.Range(-110f, 110f);
            Vector3 toward = Quaternion.AngleAxis(angle, up) * flatForward;
            float distance = Random.Range(70f, 520f);
            float groundBelow = Mathf.Clamp((float)_world.Sky.Ground.AboveGround, 0f, 400f);
            Vector3 foot = eye + toward * distance - up * groundBelow;
            Vector3 top = foot + up * Random.Range(160f, 260f) + toward * Random.Range(-40f, 40f);
            Vector3 side = Vector3.Cross(up, toward);

            int last = _boltPoints.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                float k = i / (float)last;
                Vector3 p = Vector3.Lerp(top, foot, k);
                if (i > 0 && i < last) p += (side * Random.Range(-1f, 1f) + toward * Random.Range(-0.6f, 0.6f)) * (14f * (1f - k * 0.5f));
                _boltPoints[i] = p;
            }
            _bolt.SetPositions(_boltPoints);
            // Thick enough to read as a line from this far away.
            float width = Mathf.Max(0.5f, distance * 0.012f);
            _bolt.startWidth = width * 1.4f;
            _bolt.endWidth = width * 0.5f;
            Color bright = Color.Lerp(color, Color.white, 0.45f);
            PlanetMaterials.SetColor(_boltMaterial, bright);
            _bolt.enabled = true;
            _boltLeft = Random.Range(0.14f, 0.26f);

            _flashColor = color;
            _flashLevel = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(520f, 70f, distance)) * Mathf.Lerp(0.6f, 1f, intensity);
            // Sound travels about 340 metres a second: far strikes are heard late and quieter.
            _thunderDue = distance / 340f;
            _thunderVolume = _world.Settings.thunderVolume * Audio.AudioPrefs.BusGain(Audio.AudioBus.Environment) * Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(70f, 520f, distance));
        }

        // ------------------------------------------------------------------ placeholder sounds, made from noise

        static AudioClip MakeThunder(int variant)
        {
            const int rate = 22050;
            var data = new float[(int)(rate * 3.2f)];
            var rng = new SeededRandom(900 + variant);
            float low = 0f, lower = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                // A sharp crack that falls away into a long, rolling rumble.
                float crack = Mathf.Exp(-t * 9f);
                float roll = Mathf.Exp(-t * 1.1f) * (0.6f + 0.4f * Mathf.Sin(t * (5f + variant) + variant));
                low += (rng.Range(-1f, 1f) - low) * Mathf.Lerp(0.035f, 0.3f, crack);
                lower += (low - lower) * 0.12f;
                data[i] = Mathf.Clamp((low * crack * 2.2f + lower * roll * 5.5f), -1f, 1f);
            }
            for (int i = 0; i < 200; i++) data[i] *= i / 200f;
            var clip = AudioClip.Create("Thunder" + variant, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeWind()
        {
            const int rate = 22050;
            const int length = rate * 4;
            const int blend = 3000;
            // A little extra is made past the end, then folded into the start, so the loop has no click.
            var all = new float[length + blend];
            var rng = new SeededRandom(951);
            float low = 0f, band = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                float t = i / (float)length;
                // Gusts: the hiss swells and dies away, in a pattern that joins up at the loop point.
                float gust = 0.55f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f) + 0.15f * Mathf.Sin(t * Mathf.PI * 6f + 1f);
                low += (rng.Range(-1f, 1f) - low) * 0.11f;
                band += (low - band) * 0.25f;
                all[i] = (low - band * 0.5f) * gust * 1.6f;
            }
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = i < blend ? Mathf.Lerp(all[length + i], all[i], i / (float)blend) : all[i];
            }
            var clip = AudioClip.Create("StormWind", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
