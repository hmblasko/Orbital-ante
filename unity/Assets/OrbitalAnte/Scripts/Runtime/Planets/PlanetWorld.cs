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
                // Wait for just the six starting tiles (a fraction of a second), then let detail stream in. Patient
                // work (cave networks) carries on in the background.
                PlanetJobs.FinishUrgent();
                TickSurface(start, eye);
                start.BeginPicture();
            }
            for (int i = 0; i < Surfaces.Length; i++)
            {
                if (Surfaces[i] != null) Surfaces[i].BeginTerrain(PriorityOf(Surfaces[i]));
            }
        }

        /// <summary>
        /// Works out a body's ground rules right now and returns them, for things that must know the ground before
        /// the player is placed (the launch base). Null for a body with no real ground.
        /// </summary>
        public PlanetTerrain TerrainNow(int body)
        {
            PlanetSurface s = SurfaceOf(body);
            if (s == null) return null;
            if (!s.Ready) s.BuildTerrainNow(PriorityOf(s));
            return s.Terrain;
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
                int groundCount = GatherGroundPoints(s, eye, out float speed);
                // Looking away must not retire the ground beneath the suit or a ship left behind.
                if (!s.Near && s.Ready && (height < near || groundCount > 0)) s.SetNear(true);
                else if (s.Near && height > near * 1.25f && groundCount == 0) s.SetNear(false);
                s.FarTick(height + s.BodyRadius, near);
                if (s.Near) TickSurface(s, eye, groundCount, speed);
            }
        }

        readonly Vec3d[] _groundPoints = new Vec3d[PlanetSurface.GroundPointLimit];

        void TickSurface(PlanetSurface s, Vector3 eye)
        {
            int count = GatherGroundPoints(s, eye, out float speed);
            TickSurface(s, eye, count, speed);
        }

        bool NeedsSurface(GravityBody mover, PlanetSurface s)
        {
            if (mover == null || !mover.NeedsGround || mover.Body == null || !mover.isActiveAndEnabled) return false;
            if (mover.Neighbourhood != s.Body.Index && (mover != System.Focus || System.Frame.Anchor != s.Body.Index)) return false;
            // A ship in high orbit needs no colliders. Use the highest possible terrain here so a raised
            // formation can never make a landed object look too far away to keep its support.
            double top = s.SurfaceRadius + (s.Ready ? s.Terrain.MaxHeight : 0);
            return Vector3.Distance(mover.Body.position, s.Body.Body.position) - top <= Settings.solidGroundLimit;
        }

        int GatherGroundPoints(PlanetSurface s, Vector3 eye, out float speed)
        {
            Transform body = s.Body.transform;
            // The first point is the focus (whatever the player is flying or wearing). After it come the other
            // things that must not lose their ground: the ship while the player is out walking, for one.
            int count = 0;
            speed = 0f;
            if (NeedsSurface(System.Focus, s))
            {
                _groundPoints[count++] = PhysicsPointOnBody(s, System.Focus.Body.position);
                speed = (System.Focus.Body.linearVelocity - System.GroundVelocityAt(s.Body.Index, System.Focus.Body.position)).magnitude;
            }
            IReadOnlyList<GravityBody> movers = System.Movers;
            for (int i = 0; i < movers.Count && count < _groundPoints.Length; i++)
            {
                GravityBody mover = movers[i];
                if (!NeedsSurface(mover, s) || mover == System.Focus) continue;
                _groundPoints[count++] = PhysicsPointOnBody(s, mover.Body.position);
            }
            if (System.Focus == null && count == 0 && Vector3.Distance(eye, body.position) < NearDistance(s) + s.BodyRadius)
                _groundPoints[count++] = SpaceConvert.ToVec3d(body.InverseTransformPoint(eye));
            return count;
        }

        // Movers are read at their physics positions. A planet's Transform can be an interpolated render
        // pose, so use its Rigidbody pose too, particularly for a parked ship on a non-anchor body.
        static Vec3d PhysicsPointOnBody(PlanetSurface surface, Vector3 point)
        {
            Rigidbody body = surface.Body.Body;
            return SpaceConvert.ToVec3d(Quaternion.Inverse(body.rotation) * (point - body.position));
        }

        void TickSurface(PlanetSurface s, Vector3 eye, int count, float speed)
        {
            Vec3d viewer = SpaceConvert.ToVec3d(s.Body.transform.InverseTransformPoint(eye));
            s.Tick(viewer, _groundPoints, count, speed);
        }

        // ------------------------------------------------------------------ every physics step

        void FixedUpdate()
        {
            if (Surfaces == null || System == null || System.Model == null) return;
            IReadOnlyList<GravityBody> movers = System.Movers;
            _reshapedNow = false;
            for (int i = 0; i < Surfaces.Length; i++) _reshapedNow |= Surfaces[i] != null && Surfaces[i].AnyReshaped;
            _step++;
            for (int i = 0; i < movers.Count; i++)
            {
                GravityBody mover = movers[i];
                Rigidbody rb = mover.Body;
                if (rb == null || rb.isKinematic) continue;
                bool loose = mover.Loose;
                if (loose && rb.IsSleeping()) continue;
                int index = mover.Neighbourhood;
                if (index <= 0 || index >= Surfaces.Length) continue;
                PlanetSurface s = Surfaces[index];
                if (s == null || !s.Ready) continue;

                Vec3d p = PhysicsPointOnBody(s, rb.position);
                double r = p.Magnitude;
                if (r < 1e-3) continue;
                Vec3d dir = p / r;

                // A loose limb or gun is checked one step in four (spread over the list): a full ground height
                // question each step for up to 52 of them costs more than a limb sinking a little longer.
                if (s.GroundShown && (!loose || ((_step + i) & 3) == 0)) GuardAgainstSinking(s, mover, rb, p, dir, r, _reshapedNow);
                if (loose && !rb.isKinematic && r <= s.SurfaceRadius + s.Terrain.MaxHeight + 5) RestIfStill(index, mover, rb);
                if (rb.isKinematic) continue;

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
                        Vector3 up = s.Body.Body.rotation * SpaceConvert.ToVector3(dir);
                        Vector3 wind = s.Body.Body.rotation * SpaceConvert.ToVector3(weather.Wind);
                        Vector3 slip = wind - Vector3.ProjectOnPlane(rb.linearVelocity - airVelocity, up);
                        rb.AddForce(slip * Settings.windPush, ForceMode.Acceleration);
                    }
                }
            }
            if (_reshapedNow)
            {
                for (int i = 0; i < Surfaces.Length; i++) if (Surfaces[i] != null) Surfaces[i].ForgetReshaped();
            }
        }

        bool _reshapedNow;

        /// <summary>
        /// Ground only becomes solid as you get near it. If something still gets below it (arriving faster than the
        /// ground could be made solid), put it back on top instead of letting it fall through the world.
        /// </summary>
        int _step;

        /// <summary>Puts a loose thing that has lain still on the ground (moving with it) for half a second to sleep.</summary>
        void RestIfStill(int body, GravityBody mover, Rigidbody rb)
        {
            Vector3 relative = rb.linearVelocity - System.GroundVelocityAt(body, rb.position);
            if (relative.sqrMagnitude > 0.01f || rb.angularVelocity.sqrMagnitude > 0.04f)
            {
                mover.StillFor = 0f;
                return;
            }
            mover.StillFor += Time.fixedDeltaTime;
            if (mover.StillFor < 0.5f) return;
            mover.StillFor = 0f;
            rb.Sleep();
        }

        void GuardAgainstSinking(PlanetSurface s, GravityBody mover, Rigidbody rb, Vec3d p, Vec3d dir, double r, bool reshaped)
        {
            // Cheap first test: nowhere near the highest ground this body has.
            float putBack = Mathf.Max(mover.PutBackAt, mover.SunkBelow + 0.5f);
            if (r > s.SurfaceRadius + s.Terrain.MaxHeight + 5 + putBack) return;
            // The ground as it is really built here: the triangles of the tile showing, which are its collider
            // too. Measured against anything else (the ideal height has fine bumps no tile draws, and is not cut
            // across by triangles), someone standing honestly on rough ground was "rescued" over and over (a
            // bounce) and someone pushed under it was left sunk, half in the ground, because the ideal ground
            // there happened to be lower. Against the real ground, below it is below it, on every world.
            double ground = s.GroundRadius(dir);
            double floor = mover.SunkBelow;
            // The tile under someone was just swapped for a finer shape, with different heights. A foot left under
            // the new surface would drop through it (a collider only holds from above), so it is put on top now.
            if (mover.NeedsGround && reshaped && s.ReshapedUnder(p) && s.SolidAt(p)) floor = global::System.Math.Max(floor, putBack - 0.4);
            if (r >= ground + floor || s.InCave(p, 4)) return;

            Rigidbody body = s.Body.Body;
            Vector3 up = body.rotation * SpaceConvert.ToVector3(dir);
            Vector3 lifted = body.position + body.rotation * SpaceConvert.ToVector3(dir * (ground + putBack));
            if (mover.Sank != null)
            {
                // A limb or a dropped gun that the ground let through where nobody is: its owner deals with it
                // (a limb goes, a gun is laid on the ground). Not a rescue: nothing anyone rides was in danger.
                mover.Sank(lifted, s.Body.transform);
                return;
            }
            Vector3 groundVelocity = System.GroundVelocityAt(s.Body.Index, lifted);
            Vector3 relative = rb.linearVelocity - groundVelocity;
            float inward = Vector3.Dot(relative, up);
            if (inward < 0f) relative -= up * inward;
            rb.position = lifted;
            rb.linearVelocity = groundVelocity + relative * 0.5f;
            // The ground it is put back on must be solid before it comes down again.
            if (mover.NeedsGround) s.MakeSolidAt(dir * ground);
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
                            (s.Features.Refused > 0 ? " (cap reached)" : string.Empty) + s.NearestNetworkForDebug();
                }
            }
            return "Ground ready " + ready + "/" + total + "  pictures " + pictures + "/" + total +
                   "  work waiting " + PlanetJobs.WaitingCount + near + (Rescues > 0 ? "   rescues " + Rescues : string.Empty) +
                   (PlanetMaterials.OwnTerrain ? string.Empty : "   SIMPLE SHADERS");
        }
    }
}
