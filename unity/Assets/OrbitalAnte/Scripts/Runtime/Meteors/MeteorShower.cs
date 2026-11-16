using System.Collections.Generic;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Player;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Meteors
{
    /// <summary>
    /// Meteor strikes on the world the player is on (Matthew, 16 Nov 2026). While the player is on or near the
    /// ground of a world with solid ground, a clock runs (<see cref="MeteorClock"/>: never too often, never
    /// regular); when it runs out a rock is aimed (<see cref="MeteorAim"/>), its whole fall is worked out on a
    /// background thread with every burst, piece and crater (<see cref="MeteorPlanner"/>, <see cref="CraterMesher"/>),
    /// and then played back here: the rocks fall and burst (<see cref="MeteorFx"/>), each landing explodes, hurts
    /// what is near (<see cref="BlastRules"/>) and leaves a crater (<see cref="CraterField"/>).
    /// One strike at a time. Built by SolarSystemWorld; the arena has none.
    /// </summary>
    public sealed class MeteorShower : MonoBehaviour
    {
        enum Phase { Waiting, Planning, Flying }

        sealed class Result
        {
            public MeteorPlan Plan;
            public CraterMeshData[] Craters;
        }

        sealed class Strike
        {
            public MeteorPlan Plan;
            public CraterMeshData[] Craters;
            public PlanetSurface Surface;
            public Transform Body;
            public int BodyIndex;
            public bool[] Done;
            public bool Air;
        }

        struct PendingCrater
        {
            public PlanetSurface Surface;
            public CraterMeshData Data;
        }

        struct Delayed
        {
            public float At;
            public string Event;
            public Transform Body;
            public Vector3 Local;
            public float Volume;
        }

        const string RoarEvent = "meteor.roar";
        const string BurstEvent = "meteor.breakup";
        const string ImpactEvent = "meteor.impact";
        const string SplashEvent = "meteor.splash";
        const string RockEvent = "meteor.debris";

        SolarSystemWorld _world;
        MeteorConfig _config;
        MeteorFx _fx;
        CraterField _craters;
        SeededRandom _rng;
        SeededRandom _gapRng;
        readonly List<PendingCrater> _pendingCraters = new List<PendingCrater>();
        Phase _phase;
        float _untilNext;
        float _grace;
        int _clockBody = -1;
        int _planningBody = -1;
        PlanetJobs.Handle _job;
        Strike _strike;
        double _t;
        int _craterSeed;
        readonly List<Vector3> _enemyPlaces = new List<Vector3>();
        readonly List<Vec3d> _enemyDirs = new List<Vec3d>();
        readonly List<Delayed> _sounds = new List<Delayed>();
        readonly Collider[] _overlap = new Collider[256];
        readonly HashSet<IHittable> _struck = new HashSet<IHittable>();
        readonly HashSet<Rigidbody> _pushed = new HashSet<Rigidbody>();

        MeteorRules R => _config.rules;
        public int Strikes { get; private set; }
        public int CraterCount => _craters != null ? _craters.Count : 0;
        /// <summary>Seconds until the next strike while the clock is running (for the debug readout).</summary>
        public float UntilNext => _grace > 0f ? _grace + _untilNext : _untilNext;

        public static MeteorShower Build(SolarSystemWorld world, MeteorConfig config)
        {
            var go = new GameObject("Meteors");
            go.transform.SetParent(world.transform, false);
            var shower = go.AddComponent<MeteorShower>();
            shower._world = world;
            shower._config = config;
            // Its own stream: strikes do not shift anything else the seed decides. Where they fall depends on
            // where the player stands, so they are never the same twice anyway.
            shower._rng = GameSession.Stream("meteors");
            // When they come is deliberately not from the seed: a same-seed restart must not teach the moment the
            // first rock falls (the same reason RareGuns rolls its own).
            shower._gapRng = new SeededRandom(unchecked((int)System.DateTime.UtcNow.Ticks ^ (int)(System.DateTime.UtcNow.Ticks >> 32)));
            shower._fx = MeteorFx.Create(go.transform, config);
            shower._fx.OnChunkLanded = shower.ChunkLanded;
            shower._craters = CraterField.Create(go.transform, config.rules);
            shower._grace = Mathf.Max(0f, config.rules.graceSeconds);
            return shower;
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _config == null) return;
            PlayDueSounds();
            AddOnePendingCrater();
            if (!R.enabled)
            {
                if (_phase != Phase.Waiting) Abort();
                return;
            }

            switch (_phase)
            {
                case Phase.Waiting:
                    if (!Eligible(out PlanetSurface surface, out int body, out Transform eye)) return;
                    if (body != _clockBody)
                    {
                        // Arriving on a world starts its clock afresh: nobody is struck the moment they land.
                        _clockBody = body;
                        _untilNext = Mathf.Max(R.arrivalGrace, NextGap(surface));
                    }
                    if (_grace > 0f)
                    {
                        _grace -= dt;
                        return;
                    }
                    _untilNext -= dt;
                    if (_untilNext <= 0f) Begin(surface, body, eye);
                    break;

                case Phase.Planning:
                    // The answer comes back in Planned. If the player has gone, it is thrown away there.
                    if (_job != null && _job.Cancelled) _phase = Phase.Waiting;
                    break;

                case Phase.Flying:
                    if (_strike == null || _world.System == null || _world.System.Frame.Anchor != _strike.BodyIndex || !_strike.Surface.Near)
                    {
                        Abort();
                        return;
                    }
                    Advance(dt);
                    break;
            }
        }

        float NextGap(PlanetSurface surface) => (float)MeteorClock.NextGap(R, _gapRng.NextDouble(), R.RateFor(surface.Body.Spec.id));

        bool Eligible(out PlanetSurface surface, out int body, out Transform eye)
        {
            surface = null;
            body = -1;
            eye = null;
            SolarSystem system = _world.System;
            Astronaut a = _world.Astronaut;
            if (system == null || system.Frame == null || _world.Planets == null || a == null || a.Dead || a.Eye == null) return false;
            body = system.Frame.Anchor;
            surface = _world.Planets.SurfaceOf(body);
            if (surface == null || !surface.Ready || !surface.Near || surface.Interior || surface.Terrain == null) return false;
            BodyKind kind = surface.Body.Spec.kind;
            if (kind == BodyKind.Star || kind == BodyKind.GasGiant) return false;
            if (!(R.RateFor(surface.Body.Spec.id) > 0f)) return false;
            eye = a.Eye.transform;
            GroundReading g = _world.Planets.Read(body, eye.position);
            return g.Known && g.AboveGround < R.maxAltitude && !g.InCave && !g.Underwater;
        }

        // ------------------------------------------------------------------ a strike begins

        void Begin(PlanetSurface surface, int body, Transform eye)
        {
            Transform bodyTransform = surface.Body.transform;
            PlanetTerrain terrain = surface.Terrain;
            Vec3d player = SpaceConvert.ToVec3d(bodyTransform.InverseTransformPoint(eye.position)).Normalized;

            _enemyDirs.Clear();
            if (_world.Enemies != null)
            {
                _world.Enemies.LivePositions(body, _enemyPlaces);
                double reach = R.maxDistance;
                for (int i = 0; i < _enemyPlaces.Count; i++)
                {
                    // Only enemies near enough for the strike to be seen: a camp a long way off is never aimed at.
                    Vec3d dir = SpaceConvert.ToVec3d(bodyTransform.InverseTransformPoint(_enemyPlaces[i])).Normalized;
                    if (MeteorAim.ArcDistance(dir, player, surface.SurfaceRadius) <= reach) _enemyDirs.Add(dir);
                }
            }

            double radius = surface.SurfaceRadius;
            Vec3d ship = _world.Ship != null ? SpaceConvert.ToVec3d(bodyTransform.InverseTransformPoint(_world.Ship.transform.position)) : Vec3d.Zero;
            bool shipHere = _world.Ship != null && Mathf.Abs((float)(ship.Magnitude - surface.GroundRadius(ship.Normalized))) < 60f;
            Vec3d shipDir = ship.Normalized;
            var sites = _world.Bases != null ? _world.Bases.SitesOn(body) : null;
            bool padHere = _world.Base != null && _world.Base.Surface == surface;
            Vec3d pad = padHere ? _world.Base.Spot.Up : Vec3d.Zero;
            OrbitalAnte.Rover.MoonRover rover = _world.Rover;
            bool roverHere = rover != null && rover.Now != OrbitalAnte.Rover.MoonRover.Where.Stowed;
            Vec3d roverDir = roverHere ? SpaceConvert.ToVec3d(bodyTransform.InverseTransformPoint(rover.transform.position)).Normalized : Vec3d.Zero;
            // The biggest crater's apron reaches this far from where it lands: nothing it must miss may be nearer.
            double widest = R.maxCrater * R.apron;
            double holeMargin = widest / Mathf.Max(1f, (float)radius);

            bool Allowed(Vec3d d)
            {
                if (terrain.HasSea && terrain.Height(d) < terrain.SeaLevel + 0.5) return false;
                // A crater laid over a cave's way in or skylight would shut it.
                if (terrain.InHole(d, holeMargin)) return false;
                if (padHere && MeteorAim.ArcDistance(d, pad, radius) < R.padClear + widest) return false;
                if (shipHere && MeteorAim.ArcDistance(d, shipDir, radius) < R.shipClear) return false;
                if (roverHere && MeteorAim.ArcDistance(d, roverDir, radius) < R.shipClear) return false;
                if (sites != null)
                    for (int i = 0; i < sites.Count; i++)
                        if (MeteorAim.ArcDistance(d, sites[i].Up, radius) < sites[i].Footprint + R.baseClear + widest) return false;
                return true;
            }

            float rate = R.RateFor(surface.Body.Spec.id);
            if (!MeteorAim.Pick(R, _rng, player, radius, _enemyDirs, Allowed, out Vec3d target, out AimKind _))
            {
                // Nowhere to put one right now (all sea, say): try again a little later.
                _untilNext = Mathf.Max(5f, R.minGap * 0.5f) / Mathf.Max(0.1f, rate);
                return;
            }

            bool air = surface.Profile.hasAtmosphere;
            double surfaceRadius = surface.SurfaceRadius;
            var world = new PlanWorld
            {
                GroundRadius = d => surfaceRadius + terrain.Height(d),
                Water = terrain.HasSea ? (System.Func<Vec3d, bool>)(d => terrain.Height(d) < terrain.SeaLevel) : null,
                Ceiling = surfaceRadius + terrain.MaxHeight + 2,
                SurfaceRadius = surfaceRadius,
                Gravity = surface.Body.Spec.surfaceGravity,
                Air = air,
                Drag = R.airDrag,
            };
            MeteorRules rules = R;
            int seed = _rng.Next();
            int craterSeed = _craterSeed;
            _craterSeed += 32;
            GroundSampler sampler = (Vec3d d, out double r, out float cr, out float cg, out float cb) =>
            {
                TerrainPoint point = terrain.Sample(d);
                r = surfaceRadius + point.Height;
                terrain.ColorAt(d, point, 0.0, out cr, out cg, out cb);
            };

            Result Work()
            {
                // On a worker thread. A throw here would leave the shower waiting for an answer that never comes,
                // so anything that goes wrong is a strike that does not happen.
                try { return Plan(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Meteor plan failed, skipping this strike: " + e.Message);
                    return null;
                }
            }

            Result Plan()
            {
                // The pieces scatter as they fall, so every landing is checked, not only the aim point; a fall that
                // puts a piece somewhere it may not go is planned again, and after a few tries left out.
                MeteorPlan plan = null;
                for (int attempt = 0; attempt < 4 && plan == null; attempt++)
                {
                    MeteorPlan tried = MeteorPlanner.Plan(rules, new SeededRandom(seed + attempt * 7919), target, world);
                    bool fits = true;
                    for (int i = 0; i < tried.Pieces.Count && fits; i++)
                    {
                        MeteorPiece p = tried.Pieces[i];
                        if (!p.Lands || p.Splash) continue;
                        Vec3d d = p.End.Normalized;
                        // Where the player stood when it was aimed: no piece may land near enough to hurt them at all.
                        double clear = BlastRules.Reach(rules, p.Crater) + 3.0;
                        if (MeteorAim.ArcDistance(d, player, radius) < clear || !Allowed(d)) fits = false;
                    }
                    if (fits) plan = tried;
                }
                if (plan == null) return null;
                var craters = new CraterMeshData[plan.Pieces.Count];
                for (int i = 0; i < plan.Pieces.Count; i++)
                {
                    MeteorPiece p = plan.Pieces[i];
                    if (!p.Lands || p.Splash) continue;
                    CraterSite site = CraterSite.Make(rules, p.End.Normalized, p.Crater, craterSeed + i, air);
                    craters[i] = CraterMesher.Build(site, sampler, surfaceRadius);
                }
                return new Result { Plan = plan, Craters = craters };
            }

            _planningBody = body;
            _phase = Phase.Planning;
            _job = PlanetJobs.Run(Work, r => Planned(r, surface, body), () => -1e5f);
        }

        void Planned(Result result, PlanetSurface surface, int body)
        {
            if (this == null || _phase != Phase.Planning || body != _planningBody) return;
            _job = null;
            if (result == null || result.Plan == null || result.Plan.Pieces.Count == 0 || _world.System == null || _world.System.Frame.Anchor != body)
            {
                _phase = Phase.Waiting;
                _untilNext = R.minGap;
                return;
            }
            Transform bodyTransform = surface.Body.transform;
            MeteorPlan plan = result.Plan;
            _strike = new Strike
            {
                Plan = plan,
                Craters = result.Craters,
                Surface = surface,
                Body = bodyTransform,
                BodyIndex = body,
                Done = new bool[plan.Pieces.Count],
                Air = plan.Air,
            };
            _t = 0;
            _phase = Phase.Flying;
            Strikes++;

            Vec3d up = plan.Target.Normalized;
            Color dust = DustColour(surface, up);
            _fx.Begin(bodyTransform, SpaceConvert.ToVector3(plan.Target), SpaceConvert.ToVector3(up), plan.Air, (float)surface.Body.Spec.surfaceGravity, dust);
        }

        /// <summary>The colour of the ground at the target, made paler, as dust thrown off it would be.</summary>
        static Color DustColour(PlanetSurface surface, Vec3d direction)
        {
            TerrainPoint point = surface.Terrain.Sample(direction);
            surface.Terrain.ColorAt(direction, point, 0.0, out float r, out float g, out float b);
            return new Color(Mathf.Lerp(r, 0.85f, 0.35f), Mathf.Lerp(g, 0.83f, 0.35f), Mathf.Lerp(b, 0.8f, 0.35f), 1f);
        }

        // ------------------------------------------------------------------ the fall

        void Advance(float dt)
        {
            Strike s = _strike;
            _t += dt;
            MeteorPlan plan = s.Plan;
            int loudest = -1;
            double biggest = 0;
            for (int i = 0; i < plan.Pieces.Count; i++)
            {
                MeteorPiece p = plan.Pieces[i];
                if (_t < p.Born) continue;
                if (_t < p.Ends)
                {
                    Vector3 at = SpaceConvert.ToVector3(MeteorPlan.PositionAt(p, _t));
                    Vector3 v = SpaceConvert.ToVector3(MeteorPlan.VelocityAt(p, _t));
                    _fx.ShowHead(i, s.Body, at, v, (float)p.Rock);
                    if (i < MeteorFx.MaxHeads && p.Crater > biggest)
                    {
                        biggest = p.Crater;
                        loudest = i;
                    }
                    continue;
                }
                if (s.Done[i]) continue;
                s.Done[i] = true;
                _fx.HideHead(i);
                if (p.Bursts) Burst(s, p);
                else if (p.Lands) Land(s, p, s.Craters != null && i < s.Craters.Length ? s.Craters[i] : null);
            }

            // The biggest rock lights the ground as it comes down, and its roar rides it.
            if (loudest >= 0 && s.Air) _fx.HeadLight(s.Body, SpaceConvert.ToVector3(MeteorPlan.PositionAt(plan.Pieces[loudest], _t)), (float)biggest);
            AudioDirector audio = AudioDirector.Instance;
            if (audio != null)
            {
                if (loudest >= 0) audio.SetLoop(RoarEvent, _config.volume * Mathf.Lerp(0.6f, 1f, (float)(biggest / R.maxCrater)), 0.8f, _fx.HeadTransform(loudest));
                else audio.SetLoop(RoarEvent, 0f, 0.3f);
            }

            if (_t > plan.Duration + 0.1)
            {
                _strike = null;
                _phase = Phase.Waiting;
                _untilNext = NextGap(s.Surface);
            }
        }

        void AddOnePendingCrater()
        {
            if (_pendingCraters.Count == 0) return;
            PendingCrater c = _pendingCraters[0];
            _pendingCraters.RemoveAt(0);
            if (c.Surface == null) return;
            _craters.Add(c.Surface, c.Data, _world.Planets != null && _world.Planets.Settings != null ? _world.Planets.Settings.groundRelief : 1f);
        }

        void Abort()
        {
            if (_job != null) _job.Cancel();
            _job = null;
            _strike = null;
            _phase = Phase.Waiting;
            _fx.HideAllHeads();
            if (AudioDirector.Instance != null) AudioDirector.Instance.SetLoop(RoarEvent, 0f, 0.4f);
        }

        void Burst(Strike s, MeteorPiece p)
        {
            Vector3 at = SpaceConvert.ToVector3(p.End);
            _fx.Burst(s.Body, at, SpaceConvert.ToVector3(p.EndVelocity), (float)p.Crater);
            Sound(BurstEvent, s, at, Mathf.Lerp(0.6f, 1f, (float)(p.Crater / R.maxCrater)));
        }

        void Land(Strike s, MeteorPiece p, CraterMeshData crater)
        {
            Vector3 at = SpaceConvert.ToVector3(p.End);
            Vector3 up = at.normalized;
            PlanetSurface surface = s.Surface;
            string id = surface.Body.Spec.id;
            double radius = surface.SurfaceRadius;
            // Thrown rocks settle on the ground, or on a crater's rim where there is one.
            System.Func<Vector3, float> ground = d =>
            {
                Vec3d dir = SpaceConvert.ToVec3d(d);
                return (float)(surface.GroundRadius(dir) + CraterRegistry.LiftAt(id, dir, radius));
            };
            // A crater's mesh and collider are made on the main thread; several pieces can land in one frame, so they
            // are put in one a frame (the flash and dust hide the wait).
            if (crater != null) _pendingCraters.Add(new PendingCrater { Surface = surface, Data = crater });
            _fx.Impact(s.Body, at, up, (float)p.Crater, p.Splash, R, ground);

            Vector3 worldAt = s.Body.TransformPoint(at);
            Vector3 worldUp = s.Body.TransformDirection(up);
            if (!p.Splash) Blast(worldAt, worldUp, p.Crater);
            Shake(worldAt, p.Crater);
            Sound(p.Splash ? SplashEvent : ImpactEvent, s, at, Mathf.Lerp(0.55f, 1f, (float)(p.Crater / R.maxCrater)));
        }

        // ------------------------------------------------------------------ the blast

        /// <summary>
        /// Hurts and throws whatever is near a landing: every enemy body part in reach takes damage that falls off
        /// with distance (limbs come off well before the edge; in the crater itself everyone dies), the player's
        /// suit takes damage if they are out in the open, the lander's parts take a knock, and loose things (limbs,
        /// dropped guns, the rover) are thrown outward and up. Nothing counts as the player's hit.
        /// </summary>
        void Blast(Vector3 at, Vector3 up, double crater)
        {
            float reach = (float)BlastRules.Reach(R, crater);
            int n = Physics.OverlapSphereNonAlloc(at, reach, _overlap, ~0, QueryTriggerInteraction.Ignore);
            _struck.Clear();
            _pushed.Clear();
            PlayerHealth player = null;
            float playerDistance = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider col = _overlap[i];
                _overlap[i] = null;
                if (col == null) continue;
                Vector3 closest = col.bounds.ClosestPoint(at);
                float d = Vector3.Distance(at, closest);

                PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
                if (health == null)
                {
                    HealthLink link = col.GetComponent<HealthLink>();
                    if (link != null) health = link.Target;
                }
                if (health != null)
                {
                    if (d < playerDistance)
                    {
                        player = health;
                        playerDistance = d;
                    }
                    continue;
                }

                IHittable target = col.GetComponentInParent<IHittable>();
                if (target != null && _struck.Add(target))
                {
                    bool skip = target is Weapon w && w.Hold == WeaponHold.HeldByPlayer;
                    float damage = target is ShipPart ? BlastRules.ShipDamage(R, crater, d) : BlastRules.PartDamage(R, crater, d);
                    bool lethal = target is BodyPart && BlastRules.Lethal(R, crater, d);
                    if (!skip && (damage > 0.5f || lethal))
                    {
                        Vector3 away = Away(at, up, closest);
                        target.Hit(new HitInfo { Damage = damage, Point = closest, Direction = away, Normal = -away, Lethal = lethal });
                    }
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && _pushed.Add(rb) && rb.GetComponentInParent<OrbitalAnte.Ship.Ship>() == null)
                {
                    float speed = BlastRules.Knock(R, crater, d);
                    if (rb.gameObject.layer == Layers.Vehicle) speed *= 0.5f;
                    else if (rb.gameObject.layer == Layers.Player) speed *= 0.35f;
                    if (speed > 0.2f)
                    {
                        rb.AddForce(Away(at, up, rb.worldCenterOfMass) * speed, ForceMode.VelocityChange);
                        if (rb.gameObject.layer == Layers.Gib || rb.gameObject.layer == Layers.Item) rb.AddTorque(Random.onUnitSphere * speed, ForceMode.VelocityChange);
                    }
                }
            }

            Astronaut a = _world.Astronaut;
            if (player != null && a != null && !a.Dead && a.InTheOpen)
            {
                float damage = BlastRules.PlayerDamage(R, crater, playerDistance);
                if (damage > 0.5f)
                {
                    // Say why before the suit's own "shot down" can.
                    if (damage >= player.Current) a.Die("Caught in a meteor strike.");
                    player.TakeDamage(damage, Away(at, up, a.Eye != null ? a.Eye.transform.position : at + up));
                }
            }
        }

        /// <summary>Outward from the blast and upward, as the ground throws things.</summary>
        static Vector3 Away(Vector3 from, Vector3 up, Vector3 to)
        {
            Vector3 d = to - from;
            Vector3 flat = d - up * Vector3.Dot(d, up);
            Vector3 dir = (flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.zero) + up * 0.7f;
            return dir.normalized;
        }

        void Shake(Vector3 at, double crater)
        {
            Astronaut a = _world.Astronaut;
            if (a == null || a.Eye == null || !FeelSettings.CameraShake) return;
            float amount = BlastRules.Shake(R, crater, Vector3.Distance(a.Eye.transform.position, at));
            if (amount <= 0.01f) return;
            CameraShake shake = a.Eye.GetComponent<CameraShake>();
            if (shake != null) shake.Rumble(amount);
        }

        // ------------------------------------------------------------------ sound

        /// <summary>A sound from a place in the strike, heard when it would reach the player (sound in air is slow).</summary>
        void Sound(string id, Strike s, Vector3 local, float volume)
        {
            float delay = 0f;
            Astronaut a = _world.Astronaut;
            if (a != null && a.Eye != null)
                delay = (float)BlastRules.SoundDelay(Vector3.Distance(a.Eye.transform.position, s.Body.TransformPoint(local)), s.Air);
            _sounds.Add(new Delayed { At = Time.time + delay, Event = id, Body = s.Body, Local = local, Volume = volume * _config.volume });
        }

        void PlayDueSounds()
        {
            for (int i = _sounds.Count - 1; i >= 0; i--)
            {
                Delayed d = _sounds[i];
                if (Time.time < d.At) continue;
                _sounds.RemoveAt(i);
                if (d.Body != null) AudioDirector.Play(d.Event, d.Body.TransformPoint(d.Local), d.Volume);
            }
        }

        void ChunkLanded(Vector3 at, float size)
        {
            AudioDirector.Play(RockEvent, at, _config.volume * Mathf.Clamp(size * 1.2f, 0.25f, 1f));
        }

        // ------------------------------------------------------------------ readout

        /// <summary>One line for the debug readout (F3).</summary>
        public string DescribeForDebug()
        {
            string state = _phase == Phase.Flying ? "falling" : _phase == Phase.Planning ? "aiming" : (UntilNext < 1e6f ? "next in " + Mathf.CeilToInt(UntilNext) + " s" : "none here");
            return "Meteors: " + state + "  strikes " + Strikes + "  craters " + CraterCount + "  rocks " + _fx.ChunkCount + "  lumps " + _fx.ParticleCount;
        }
    }
}
