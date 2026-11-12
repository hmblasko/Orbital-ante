using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>What is under (or around) a point near a body, for the flight readout.</summary>
    public struct GroundReading
    {
        /// <summary>True if the body has real ground and it has been worked out.</summary>
        public bool Known;
        /// <summary>Height above the ground straight below, in metres. Negative inside a cave.</summary>
        public double AboveGround;
        /// <summary>Height above the sea, if there is one. Negative under water.</summary>
        public double AboveSea;
        public bool OverSea;
        public bool Underwater;
        /// <summary>True in the thick of a gas giant's cloud deck, where you cannot see.</summary>
        public bool InsideClouds;
        /// <summary>True in the clear air underneath a gas giant's cloud deck.</summary>
        public bool UnderClouds;
        public bool InCave;
    }

    /// <summary>
    /// Looks after every body's ground: decides which bodies are near enough to need real terrain, keeps the
    /// background work moving, and applies what the surroundings do to loose objects (air and water slowing
    /// them, water holding them up, storm wind pushing them, and a guard that stops anything sinking through
    /// ground that was not solid yet).
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class PlanetWorld : MonoBehaviour
    {
        public static PlanetWorld Instance { get; private set; }

        public SolarSystem System { get; private set; }
        public PlanetData Data { get; private set; }
        public PlanetSettings Settings { get; private set; }
        /// <summary>One entry per body in the solar system; null for bodies with no ground (the Sun, asteroids).</summary>
        public PlanetSurface[] Surfaces { get; private set; }
        public PlanetSky Sky { get; private set; }
        public PlanetWeatherFx WeatherFx { get; private set; }
        public PlumeFx Plumes { get; private set; }
        /// <summary>How many times the guard has lifted something back above ground. For the debug readout.</summary>
        public int Rescues { get; private set; }

        public static void ResetStatics() => Instance = null;

        /// <summary>Ids of the bodies whose far-away picture is painted from their real ground.</summary>
        public static ICollection<string> PaintedLater(PlanetData data)
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < data.planets.Count; i++)
            {
                // Gas giants keep their banded cloud picture: their ground is hidden inside.
                if (!data.planets[i].cloudDeck) ids.Add(data.planets[i].bodyId);
            }
            return ids;
        }

        public void Build(SolarSystem system, PlanetData data, int seed)
        {
            Instance = this;
            System = system;
            Data = data;
            Settings = data.settings;
            PlanetJobs.ResetStatics();
            PlanetJobs.MaxRunning = Settings.backgroundWorkers;

            Surfaces = new PlanetSurface[system.Bodies.Length];
            for (int i = 0; i < system.Bodies.Length; i++)
            {
                PlanetProfile profile = data.Find(system.Bodies[i].Spec.id);
                if (profile == null || system.Bodies[i].Spec.kind == BodyKind.Star || system.Bodies[i].Spec.kind == BodyKind.Asteroid) continue;
                Surfaces[i] = PlanetSurface.Create(system.Bodies[i], profile, Settings, seed);
            }

            var skyGo = new GameObject("PlanetSky");
            skyGo.transform.SetParent(transform, false);
            Sky = skyGo.AddComponent<PlanetSky>();
            Sky.Build(this);

            var weatherGo = new GameObject("Weather");
            weatherGo.transform.SetParent(transform, false);
            WeatherFx = weatherGo.AddComponent<PlanetWeatherFx>();
            WeatherFx.Build(this);

            var plumeGo = new GameObject("Plumes");
            plumeGo.transform.SetParent(transform, false);
            Plumes = plumeGo.AddComponent<PlumeFx>();
            Plumes.Build(this);
        }

        /// <summary>
        /// Call once the player has been placed. The body they start beside gets its ground straight away, so
        /// the first thing they see is real terrain; every other body is worked out in the background, nearest first.
        /// </summary>
        public void Begin()
        {
            Vector3 eye = ViewPoint();
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Surfaces.Length; i++)
            {
                if (Surfaces[i] == null) continue;
                float d = Vector3.Distance(eye, Surfaces[i].Body.transform.position) - (float)Surfaces[i].BodyRadius;
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            if (nearest >= 0 && best < NearDistance(Surfaces[nearest]))
            {
                PlanetSurface start = Surfaces[nearest];
                start.BuildTerrainNow(PriorityOf(start));
                start.SetNear(true);
                TickSurface(start, eye);
                // Wait for just the six starting tiles (a fraction of a second), then let detail stream in.
                PlanetJobs.FinishAll();
                TickSurface(start, eye);
                start.BeginPicture();
            }
            for (int i = 0; i < Surfaces.Length; i++)
            {
                if (Surfaces[i] != null) Surfaces[i].BeginTerrain(PriorityOf(Surfaces[i]));
            }
        }

        System.Func<float> PriorityOf(PlanetSurface surface)
        {
            return () => surface == null ? float.MaxValue : Vector3.Distance(ViewPoint(), surface.Body.transform.position);
        }

        Vector3 ViewPoint()
        {
            if (System.ViewCamera != null) return System.ViewCamera.transform.position;
            return System.Focus != null ? System.Focus.transform.position : Vector3.zero;
        }

        float NearDistance(PlanetSurface surface)
        {
            return (float)surface.BodyRadius * (Settings.nearInRadiuses - 1f) + 300f;
        }

        void OnDestroy()
        {
            // On a restart the new world is built before this old one is finally removed. Its background work
            // must not be thrown away by the old world's goodbye.
            if (Instance == this || Instance == null)
            {
                PlanetJobs.ResetStatics();
                Instance = null;
            }
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (Surfaces == null) return;
            PlanetJobs.MaxRunning = Settings.backgroundWorkers;
            PlanetJobs.Pump(Settings.uploadsPerFrame);

            Vector3 eye = ViewPoint();
            for (int i = 0; i < Surfaces.Length; i++)
            {
                PlanetSurface s = Surfaces[i];
                if (s == null) continue;
                float height = Vector3.Distance(eye, s.Body.transform.position) - (float)s.BodyRadius;
                float near = NearDistance(s);
                if (!s.Near && s.Ready && height < near) s.SetNear(true);
                else if (s.Near && height > near * 1.25f) s.SetNear(false);
                if (s.Near) TickSurface(s, eye);
            }
        }

        void TickSurface(PlanetSurface s, Vector3 eye)
        {
            Transform body = s.Body.transform;
            Vec3d viewer = SpaceConvert.ToVec3d(body.InverseTransformPoint(eye));
            Vec3d focus = viewer;
            float speed = 0f;
            if (System.Focus != null)
            {
                focus = SpaceConvert.ToVec3d(body.InverseTransformPoint(System.Focus.transform.position));
                speed = (System.Focus.Body.linearVelocity - System.GroundVelocityAt(s.Body.Index, System.Focus.Body.position)).magnitude;
            }
            s.Tick(viewer, focus, speed);
        }

        // ------------------------------------------------------------------ every physics step

        void FixedUpdate()
        {
            if (Surfaces == null || System == null || System.Model == null) return;
            IReadOnlyList<GravityBody> movers = System.Movers;
            for (int i = 0; i < movers.Count; i++)
            {
                GravityBody mover = movers[i];
                Rigidbody rb = mover.Body;
                if (rb == null || rb.isKinematic) continue;
                int index = mover.Neighbourhood;
                if (index <= 0 || index >= Surfaces.Length) continue;
                PlanetSurface s = Surfaces[index];
                if (s == null || !s.Ready) continue;

                Transform body = s.Body.transform;
                Vec3d p = SpaceConvert.ToVec3d(body.InverseTransformPoint(rb.position));
                double r = p.Magnitude;
                if (r < 1e-3) continue;
                Vec3d dir = p / r;

                if (s.GroundShown) GuardAgainstSinking(s, rb, p, dir, r);

                if (r > s.AirTop) continue;
                AirSample air = s.AirAt(p);
                if (!air.InAtmosphere) continue;

                Vector3 airVelocity = System.GroundVelocityAt(index, rb.position);
                if (air.Drag > 0)
                {
                    rb.AddForce((airVelocity - rb.linearVelocity) * (float)air.Drag, ForceMode.Acceleration);
                }
                if (air.Underwater && Settings.buoyancy > 0f)
                {
                    // Water carries most of the weight. Just under the surface it carries less, so things bob.
                    float submerged = Mathf.Clamp01((float)air.Depth / 2f);
                    rb.AddForce(-mover.FeltGravity * (Settings.buoyancy * submerged), ForceMode.Acceleration);
                }
                if (Settings.windPush > 0f)
                {
                    WeatherSample weather = s.WeatherAt(p, System.SimTime, air);
                    if (weather.Intensity > 0.01)
                    {
                        // Wind is moving air: it drags you toward its own speed and no faster, and only sideways.
                        // Once you are travelling with it, it stops pushing.
                        Vector3 up = body.TransformDirection(SpaceConvert.ToVector3(dir));
                        Vector3 wind = body.TransformDirection(SpaceConvert.ToVector3(weather.Wind));
                        Vector3 slip = wind - Vector3.ProjectOnPlane(rb.linearVelocity - airVelocity, up);
                        rb.AddForce(slip * Settings.windPush, ForceMode.Acceleration);
                    }
                }
            }
        }

        /// <summary>
        /// Ground only becomes solid as you get near it. If something still gets below it (arriving faster than the
        /// ground could be made solid), put it back on top instead of letting it fall through the world.
        /// </summary>
        void GuardAgainstSinking(PlanetSurface s, Rigidbody rb, Vec3d p, Vec3d dir, double r)
        {
            // Cheap first test: nowhere near the lowest ground this body has.
            if (r > s.SurfaceRadius + s.Terrain.MaxHeight + 5) return;
            double ground = s.GroundRadius(dir);
            if (r >= ground - 2.5 || s.InCave(p, 4)) return;

            Transform body = s.Body.transform;
            Vector3 up = body.TransformDirection(SpaceConvert.ToVector3(dir));
            Vector3 lifted = body.TransformPoint(SpaceConvert.ToVector3(dir * (ground + 2.0)));
            Vector3 groundVelocity = System.GroundVelocityAt(s.Body.Index, lifted);
            Vector3 relative = rb.linearVelocity - groundVelocity;
            float inward = Vector3.Dot(relative, up);
            if (inward < 0f) relative -= up * inward;
            rb.position = lifted;
            rb.linearVelocity = groundVelocity + relative * 0.5f;
            Rescues++;
        }

        // ------------------------------------------------------------------ questions other scripts ask

        public PlanetSurface SurfaceOf(int body)
        {
            return Surfaces != null && body >= 0 && body < Surfaces.Length ? Surfaces[body] : null;
        }

        /// <summary>What is below a point given in Unity's coordinates, for a body with real ground.</summary>
        public GroundReading Read(int body, Vector3 local)
        {
            var reading = new GroundReading();
            PlanetSurface s = SurfaceOf(body);
            if (s == null || !s.Ready) return reading;
            Vec3d p = SpaceConvert.ToVec3d(s.Body.transform.InverseTransformPoint(local));
            double r = p.Magnitude;
            if (r < 1e-3) return reading;
            reading.Known = true;
            reading.AboveGround = r - s.GroundRadius(p / r);
            if (s.Terrain.HasSea)
            {
                reading.AboveSea = r - (s.SurfaceRadius + s.Terrain.SeaLevel);
                reading.OverSea = reading.AboveGround > reading.AboveSea;
                reading.Underwater = reading.AboveSea < 0;
            }
            if (s.Interior && r < s.BodyRadius)
            {
                double cloud = s.AirAt(p).Cloud;
                reading.InsideClouds = cloud >= 0.5;
                reading.UnderClouds = cloud < 0.5;
            }
            reading.InCave = reading.AboveGround < -1.5 && s.InCave(p);
            return reading;
        }

        /// <summary>One line for the debug readout (F3).</summary>
        public string DescribeForDebug()
        {
            if (Surfaces == null) return string.Empty;
            int ready = 0, total = 0, pictures = 0;
            string near = string.Empty;
            for (int i = 0; i < Surfaces.Length; i++)
            {
                PlanetSurface s = Surfaces[i];
                if (s == null) continue;
                total++;
                if (s.Ready) ready++;
                if (s.PictureReady || s.Interior) pictures++;
                if (s.Near)
                {
                    near += "   " + s.Body.Spec.displayName + ": tiles " + s.ShownTiles + " (solid " + s.SolidTiles + ")  things " + s.Features.Count +
                            (s.Features.Refused > 0 ? " (cap reached)" : string.Empty);
                }
            }
            return "Ground ready " + ready + "/" + total + "  pictures " + pictures + "/" + total +
                   "  work waiting " + PlanetJobs.WaitingCount + near + (Rescues > 0 ? "   rescues " + Rescues : string.Empty) +
                   (PlanetMaterials.OwnTerrain ? string.Empty : "   SIMPLE SHADERS");
        }
    }
}
