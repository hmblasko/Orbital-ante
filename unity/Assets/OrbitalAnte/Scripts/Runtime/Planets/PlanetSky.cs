using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Makes being inside a body's air look right: the black of space gives way to the body's sky colour as you
    /// descend, distant ground fades into haze, the stars go out by day, a gas giant's cloud deck closes round
    /// you and opens again underneath, the sea turns everything its own colour, and night is dark. A lamp comes
    /// on by itself when it is too dark to see the ground.
    ///
    /// The rules are in <see cref="AtmosphereModel"/>; this turns their answers into camera, fog and light settings.
    /// </summary>
    public sealed class PlanetSky : MonoBehaviour
    {
        static PlanetSky _live;
        PlanetWorld _world;
        Light _lamp;
        float _lampLevel;
        bool _lampWanted;

        /// <summary>The body whose air the viewer is in, or null in open space.</summary>
        public PlanetSurface Surface { get; private set; }
        public AirSample Air { get; private set; }
        public WeatherSample Weather { get; private set; }
        /// <summary>0 at night, 1 in full daylight, where the viewer is.</summary>
        public float Day { get; private set; }
        /// <summary>How completely the sky hides what is beyond the air. 0 none, 1 all.</summary>
        public float Veil { get; private set; }
        public Color Background { get; private set; }
        public bool FogOn { get; private set; }
        /// <summary>How far you can see right now, in metres (haze and weather together).</summary>
        public float Visibility { get; private set; }
        public float LampLevel => _lampLevel;
        /// <summary>The viewer's place measured from the body's centre in the body's own frame.</summary>
        public Vec3d ViewerLocal { get; private set; }
        public GroundReading Ground { get; private set; }

        public void Build(PlanetWorld world)
        {
            _live = this;
            _world = world;
            // Only used when Unity's standard shaders are standing in: the game's own ground shader has the lamp built in.
            var lampGo = new GameObject("Lamp");
            lampGo.transform.SetParent(transform, false);
            _lamp = lampGo.AddComponent<Light>();
            _lamp.type = LightType.Point;
            _lamp.color = new Color(1f, 0.93f, 0.8f);
            _lamp.shadows = LightShadows.None;
            _lamp.enabled = false;
        }

        void LateUpdate()
        {
            if (_world == null || _world.System == null || _world.System.Model == null) return;
            SolarSystem system = _world.System;
            Camera cam = system.ViewCamera;
            if (cam == null) return;
            PlanetSettings settings = _world.Settings;
            Vector3 eye = cam.transform.position;

            PlanetSurface s = _world.SurfaceOf(system.Frame.Anchor);
            if (s != null && !s.Ready) s = null;
            Surface = s;

            Color sunlight = SkyGlobals.SunColor * SkyGlobals.SunIntensity;
            Color ambient = SkyGlobals.SpaceAmbient;
            Color background = Color.black;
            float veil = 0f, day = 1f, visibility = 1e9f, dark = 0f, sunFactor = 1f;
            Color fogColor = Color.black;
            bool nearGround = false;
            var air = new AirSample { Visibility = AtmosphereModel.ClearVisibility };
            var weather = new WeatherSample();
            var ground = new GroundReading();
            // The glow round the Sun shrinks as the Sun gets further away (Earth's distance is the yardstick).
            float sunDistance = Vector3.Distance(eye, system.Bodies[0].transform.position);
            float glowScale = Mathf.Clamp(Mathf.Sqrt(22000f / Mathf.Max(1000f, sunDistance)), 0.4f, 1.5f);
            SkyPalette palette = SkyLook.OpenSpace(glowScale);
            Vector3 skyUp = Vector3.zero;

            if (s != null)
            {
                Transform body = s.Body.transform;
                Vec3d p = SpaceConvert.ToVec3d(body.InverseTransformPoint(eye));
                ViewerLocal = p;
                double r = p.Magnitude;
                Vector3 up = (eye - body.position).normalized;
                skyUp = up;
                Vector3 toSun = (system.Bodies[0].transform.position - eye).normalized;
                day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.25f, Vector3.Dot(up, toSun)));
                air = s.AirAt(p);
                weather = s.WeatherAt(p, system.SimTime, air);
                ground = _world.Read(s.Body.Index, eye);
                float storm = (float)weather.Intensity;
                nearGround = ground.AboveGround < 260;

                PlanetProfile profile = s.Profile;
                if (air.InAtmosphere)
                {
                    Color sky = s.SkyColor;
                    Color haze = s.HazeColor;
                    bool insideDeck = profile.cloudDeck && r < s.BodyRadius;
                    // Under a cloud deck it never gets fully dark: the clouds glow faintly.
                    float light = Mathf.Lerp(insideDeck ? 0.12f : 0.035f, 1f, day) * (1f - 0.35f * (float)air.SunDimming);

                    Color skyNow;
                    if (air.Underwater)
                    {
                        skyNow = Color.Lerp(s.SeaColor, s.SeaDeepColor, Mathf.Clamp01((float)air.Depth / 45f));
                        light = Mathf.Lerp(0.05f, 1f, day) * (1f - (float)air.SunDimming);
                        haze = skyNow;
                    }
                    else
                    {
                        skyNow = Color.Lerp(sky, haze, (float)air.Cloud);
                    }
                    if (storm > 0f && weather.Spec != null)
                    {
                        Color stormColor = s.ColorOf(weather.Spec) * 0.62f;
                        skyNow = Color.Lerp(skyNow, stormColor, storm * 0.75f);
                        haze = Color.Lerp(haze, stormColor, storm * 0.75f);
                    }

                    float skyAmount = Mathf.Clamp01((float)air.Sky);
                    background = Color.Lerp(Color.black, skyNow * light, skyAmount);
                    veil = air.Underwater || insideDeck ? 1f : Mathf.Max(skyAmount * day, (float)air.Cloud);
                    veil = Mathf.Max(veil, storm * 0.9f);

                    float inverse = 1f / Mathf.Max(1f, (float)air.Visibility);
                    if (storm > 0f && weather.Spec != null) inverse += storm / Mathf.Max(5f, (float)weather.Spec.visibility);
                    visibility = 1f / inverse;
                    fogColor = haze * light;

                    sunFactor = (1f - (float)air.SunDimming) * (1f - 0.6f * storm);
                    sunlight *= sunFactor;
                    // The painted sky: deeper overhead, pale at the horizon, warm at sunrise and sunset.
                    bool uniform = air.Underwater || insideDeck || air.Cloud > 0.5;
                    palette = SkyLook.InAir(SkyGlobals.ToRgb(skyNow), SkyGlobals.ToRgb(haze), Vector3.Dot(up, toSun), light, skyAmount,
                        uniform, 1f - storm, glowScale);
                    // Low sunlight comes through more air and turns amber.
                    sunlight *= SkyGlobals.ToColor(palette.SunTint);
                    // Shadows are filled by the sky, so they take its colour rather than going grey.
                    Color skyFill = SkyGlobals.ToColor((SkyGlobals.ToRgb(skyNow) * (light * 0.55f)).Saturated(1.25f));
                    Color skyLight = skyFill + SkyGlobals.SpaceAmbient * 0.5f;
                    // At sunrise and sunset the glowing horizon fills the shadows too, so they stay readable.
                    skyLight += SkyGlobals.ToColor(palette.Sunset) * 0.3f;
                    ambient = Color.Lerp(SkyGlobals.SpaceAmbient, skyLight, skyAmount);
                    dark = 1f - day * (1f - (float)air.SunDimming) * (1f - 0.6f * storm);
                }
                else
                {
                    dark = 1f - day;
                }

                // In a cave your eyes adjust: what little daylight leaks in is all there is.
                if (ground.InCave)
                {
                    ambient *= 0.3f;
                    dark = 1f;
                }
            }

            // Lightning lights everything for an instant.
            Color flash = _world.WeatherFx != null ? _world.WeatherFx.Flash : Color.black;
            ambient += flash * 1.3f;
            background += flash * 0.45f;
            fogColor += flash * 0.6f;
            background.a = 1f;

            Air = air;
            Weather = weather;
            Ground = ground;
            Day = day;
            Veil = veil;
            Background = background;
            Visibility = visibility;
            FogOn = visibility < 60000f;

            cam.backgroundColor = background;
            RenderSettings.fog = FogOn;
            if (FogOn)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                // With this curve, something as far away as the "visibility" has all but faded out.
                RenderSettings.fogDensity = 1.7f / visibility;
                // Things drawn with Unity's standard materials (characters, weapons) fade toward the horizon
                // colour, the closest one flat colour can get to the painted sky the ground fades into.
                Color horizonFog = SkyGlobals.ToColor(palette.Horizon + palette.Sunset * 0.3f);
                RenderSettings.fogColor = air.InAtmosphere ? horizonFog + flash * 0.6f : fogColor;
            }
            RenderSettings.ambientLight = ambient;
            if (system.SunLight != null)
            {
                system.SunLight.color = SkyGlobals.SunColor * SkyGlobals.ToColor(palette.SunTint);
                system.SunLight.intensity = SkyGlobals.SunIntensity * sunFactor;
            }
            SkyGlobals.SetAir(sunlight, ambient, background, veil);
            // Lightning lights the whole sky for an instant.
            Rgb flashRgb = SkyGlobals.ToRgb(flash);
            palette.Zenith += flashRgb * 0.45f;
            palette.Horizon += flashRgb * 0.6f;
            SkyGlobals.SetSky(palette, skyUp, FogOn ? 1.7f / visibility : 0f);

            // The lamp: on when it is too dark to see ground that is close enough to matter.
            // Comes on when it is properly dark and stays on until it is clearly light again, so it never flickers at dusk.
            if (dark > 0.72f) _lampWanted = true;
            else if (dark < 0.62f) _lampWanted = false;
            float wanted = settings.lampBrightness > 0f && nearGround && _lampWanted ? 1f : 0f;
            _lampLevel = Mathf.MoveTowards(_lampLevel, wanted, Time.unscaledDeltaTime * 2.5f);
            Vector3 lampAt = eye + cam.transform.forward * 0.4f;
            var lampColor = new Color(1f, 0.93f, 0.8f) * (settings.lampBrightness * _lampLevel);
            if (SkyGlobals.FlareColor.maxColorComponent > lampColor.maxColorComponent)
                SkyGlobals.SetLamp(SkyGlobals.FlarePosition, SkyGlobals.FlareReach, SkyGlobals.FlareColor);
            else
                SkyGlobals.SetLamp(lampAt, settings.lampReach, lampColor);
            bool realLamp = !PlanetMaterials.OwnTerrain && _lampLevel > 0.01f;
            if (_lamp.enabled != realLamp) _lamp.enabled = realLamp;
            if (realLamp)
            {
                _lamp.transform.position = lampAt;
                _lamp.range = settings.lampReach;
                _lamp.intensity = settings.lampBrightness * _lampLevel * 2.2f;
            }

            // Unity's standard shaders cannot fade stars into a sky, so there they are simply switched off in haze.
            if (!PlanetMaterials.OwnBody) system.SetStarsVisible(!FogOn && veil < 0.5f);
        }

        void OnDestroy()
        {
            // Not if a newer sky has already taken over (a restart builds the new world before this one goes).
            if (_live != null && _live != this) return;
            _live = null;
            SkyGlobals.Reset();
        }
    }
}
