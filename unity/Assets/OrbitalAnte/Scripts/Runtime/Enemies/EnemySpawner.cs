using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// Puts the enemies on the worlds. Where the camps are is a rule (<see cref="EnemyField"/>, the same for a
    /// seed every time); this wakes the camps near the player as real characters and puts them away again
    /// when the player leaves, so only a handful are ever alive at once. Who has been killed stays killed
    /// for the run.
    ///
    /// Enemies are children of the body they stand on, so they ride with it. Loose things (limbs, dropped
    /// guns) fall with Unity's own gravity, which this points the way the player feels "down" while there
    /// are enemies about; nothing else in the solar system uses Unity's gravity.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        sealed class World
        {
            public List<EnemyCamp> Camps;
            /// <summary>How many of each camp are still alive. -1: never met.</summary>
            public int[] Left;
            public bool[] Awake;
            public Transform Root;
        }

        sealed class Live
        {
            public Mannequin Who;
            public int Body, Camp;
            public bool Counted;
        }

        SolarSystemWorld _world;
        EnemyConfig _config;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly List<Live> _live = new List<Live>();
        MannequinConfig[] _typeConfigs;
        Vector3 _gravityBefore;
        bool _gravityTaken;
        float _nextThink;

        /// <summary>Enemies alive and awake right now.</summary>
        public int LiveCount { get; private set; }
        /// <summary>Enemies killed this run.</summary>
        public int Killed { get; private set; }
        public EnemyConfig Config => _config;

        public static EnemySpawner Build(SolarSystemWorld world, EnemyConfig config)
        {
            var go = new GameObject("Enemies");
            go.transform.SetParent(world.transform, false);
            var spawner = go.AddComponent<EnemySpawner>();
            spawner._world = world;
            spawner._config = config;
            CombatWorld.Reset();
            return spawner;
        }

        /// <summary>The camps on a body, worked out the first time they are asked for. Null if nobody lives there.</summary>
        public IReadOnlyList<EnemyCamp> CampsOn(int body) => WorldOf(body)?.Camps;

        World WorldOf(int body)
        {
            if (_worlds.TryGetValue(body, out World w)) return w;
            w = null;
            PlanetSurface surface = _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            if (surface != null && surface.Ready && surface.Terrain != null)
            {
                PlanetTerrain terrain = surface.Terrain;
                System.Func<Vec3d, bool> dry = null;
                if (terrain.HasSea) dry = d => terrain.Height(d) > terrain.SeaLevel + 1.0;
                // Nobody camps on the launch pad: the first minute of a run is the player's.
                Vec3d pad = Vec3d.Zero;
                if (_world.Base != null && _world.Base.Surface == surface) pad = _world.Base.Spot.Up;
                var random = GameSession.Stream("enemies." + surface.Body.Spec.id);
                double radius = surface.SurfaceRadius;
                w = new World { Camps = EnemyField.Build(_config.field, _config.roster, random, radius, dry, pad) };
                w.Left = new int[w.Camps.Count];
                w.Awake = new bool[w.Camps.Count];
                for (int i = 0; i < w.Left.Length; i++) w.Left[i] = -1;
                var root = new GameObject("Enemies");
                root.transform.SetParent(surface.Body.transform, false);
                w.Root = root.transform;
            }
            else if (surface != null && !surface.Ready)
            {
                return null;   // not built yet: ask again later
            }
            _worlds[body] = w;
            return w;
        }

        void Update()
        {
            if (_world == null || _config == null || _world.Astronaut == null) return;
            Astronaut crew = _world.Astronaut;
            bool outside = crew.Mode == AstronautMode.Outside && !crew.Dead;
            // Enemies only react to someone they could see: a player on foot. (The ship as a target comes later.)
            CombatWorld.PlayerEye = outside && crew.Eye != null ? crew.Eye.transform : null;
            CombatWorld.Player = crew.Armour;

            Tally();
            if (Time.time < _nextThink) return;
            _nextThink = Time.time + 0.4f;
            Think();
        }

        void Tally()
        {
            int alive = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                if (l.Who == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                if (l.Who.Alive) { alive++; continue; }
                if (!l.Counted)
                {
                    l.Counted = true;
                    Killed++;
                    World w = _worlds[l.Body];
                    if (w.Left[l.Camp] > 0) w.Left[l.Camp]--;
                    // The body lies where it fell for a while, then is cleared away.
                    Destroy(l.Who.gameObject, _config.bodySeconds);
                }
            }
            LiveCount = alive;
        }

        void Think()
        {
            SolarSystem system = _world.System;
            GravityBody focus = system != null ? system.Focus : null;
            int body = focus != null ? focus.Neighbourhood : -1;
            PlanetSurface surface = body > 0 && _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            bool here = _config.enemiesInTheWorld && surface != null && surface.Ready && surface.GroundShown;

            World w = here ? WorldOf(body) : null;
            Vec3d at = Vec3d.Zero;
            double radius = 0, height = 0;
            if (w != null)
            {
                Vec3d local = SpaceConvert.ToVec3d(surface.Body.transform.InverseTransformPoint(focus.transform.position));
                at = local.Normalized;
                radius = surface.GroundRadius(at);
                height = local.Magnitude - radius;
                CombatWorld.DropParent = w.Root;
            }

            // Put away whoever the player has left behind (or everyone, on leaving the world).
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                bool stay = w != null && l.Body == body
                            && EnemyField.GroundDistance(at, w.Camps[l.Camp].Direction, radius) < _config.field.sleepRange
                            && height < _config.field.sleepRange;
                if (stay || l.Who == null) continue;
                World was = _worlds[l.Body];
                was.Awake[l.Camp] = false;
                Destroy(l.Who.gameObject);
                _live.RemoveAt(i);
            }
            if (w == null || height > _config.field.wakeRange)
            {
                ReleaseGravity();
                return;
            }

            // Wake the nearest sleeping camp that still has anyone in it, one a think, so the cost is spread.
            int best = -1;
            double nearest = _config.field.wakeRange;
            for (int c = 0; c < w.Camps.Count; c++)
            {
                if (w.Awake[c] || w.Left[c] == 0) continue;
                double d = EnemyField.GroundDistance(at, w.Camps[c].Direction, radius);
                if (d < nearest) { nearest = d; best = c; }
            }
            if (best < 0) return;
            int size = w.Left[best] < 0 ? w.Camps[best].Members.Length : w.Left[best];
            if (LiveCount + size > _config.field.liveCap) return;
            Wake(w, body, best, size, surface);
        }

        void Wake(World w, int body, int camp, int size, PlanetSurface surface)
        {
            EnemyCamp c = w.Camps[camp];
            w.Awake[camp] = true;
            w.Left[camp] = size;
            var rng = new SeededRandom(c.Seed);
            Transform planet = surface.Body.transform;
            Vector3 home = SpaceConvert.ToVector3(c.Direction * surface.GroundRadius(c.Direction));
            var footing = new PlanetFooting(planet, surface, home, _config.field.roam);
            Vector3 up = home.normalized;
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(up, side);

            for (int m = 0; m < size; m++)
            {
                int type = Mathf.Clamp(c.Members[m], 0, _config.roster.Count - 1);
                EnemyTypeSpec spec = _config.roster[type];
                MannequinConfig config = ConfigFor(type);
                // Spread round the camp's middle, a couple of metres apart.
                float angle = (m + rng.NextFloat() * 0.6f) * Mathf.PI * 2f / size, far = 2f + 1.2f * (m % 3);
                Vector3 position = footing.OnTheGround(home + (side * Mathf.Cos(angle) + along * Mathf.Sin(angle)) * far);
                Vector3 worldUp = footing.Up(position);
                Vector3 facing = Vector3.ProjectOnPlane(planet.TransformDirection(side * Mathf.Cos(angle + 1.3f) + along * Mathf.Sin(angle + 1.3f)), worldUp);
                Quaternion rotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.Cross(worldUp, Vector3.right), worldUp);

                Mannequin who = MannequinFactory.Create(position, rotation, w.Root, config, spec.id + " " + camp + "." + m);
                who.Footing = footing;
                who.transform.localScale = Vector3.one * Mathf.Max(0.5f, spec.scale);
                var locomotion = who.GetComponent<MannequinLocomotion>();
                if (locomotion != null) locomotion.SetWander(default, rng.Next());
                var combat = who.GetComponent<MannequinCombat>();
                if (combat != null)
                {
                    combat.SetSeed(rng.Next());
                    WeaponData data = ContentDatabase.Get<WeaponData>(spec.weapon) ?? WeaponData.Rifle;
                    Weapon weapon = Weapon.Create(data, w.Root, config.oneHandedReloadAllowed);
                    weapon.transform.position = position + worldUp * 1.2f;
                    combat.TryPickUp(weapon);
                }
                _live.Add(new Live { Who = who, Body = body, Camp = camp });
            }
            LiveCount += size;
        }

        /// <summary>The ordinary enemy's settings, bent to one type. Made once per type and shared by all of that type.</summary>
        MannequinConfig ConfigFor(int type)
        {
            if (_typeConfigs == null) _typeConfigs = new MannequinConfig[_config.roster.Count];
            if (type < _typeConfigs.Length && _typeConfigs[type] != null) return _typeConfigs[type];
            EnemyTypeSpec spec = _config.roster[type];
            MannequinConfig config = Instantiate(MannequinConfig.Load());
            config.name = "MannequinConfig (" + spec.id + ")";
            config.walkSpeed *= spec.speed;
            config.sprintSpeed *= spec.speed;
            config.oneLegSpeed *= spec.speed;
            config.crawlSpeed *= spec.speed;
            // Tough in the middle only. Arms and legs come off as easily as anyone's.
            config.torsoHp *= spec.toughness;
            config.headHp *= spec.toughness;
            config.engageRange = spec.engageRange;
            config.reactionTime = spec.reactionTime;
            config.aimError = spec.aimError;
            config.keepFar = spec.keepFar;
            config.keepNear = spec.keepNear;
            config.rushes = spec.rushes;
            config.holdsStill = spec.holdsStill;
            if (type < _typeConfigs.Length) _typeConfigs[type] = config;
            return config;
        }

        void FixedUpdate()
        {
            // Limbs and dropped guns fall the way the player feels "down" while there is anyone to shoot.
            if (LiveCount <= 0 || _world == null || _world.System == null || _world.System.Focus == null) return;
            if (!_gravityTaken)
            {
                _gravityBefore = Physics.gravity;
                _gravityTaken = true;
            }
            Physics.gravity = _world.System.Focus.FeltGravity;
        }

        void ReleaseGravity()
        {
            if (!_gravityTaken || LiveCount > 0) return;
            Physics.gravity = _gravityBefore;
            _gravityTaken = false;
        }

        void OnDestroy()
        {
            if (_gravityTaken) Physics.gravity = _gravityBefore;
            if (_typeConfigs != null)
            {
                for (int i = 0; i < _typeConfigs.Length; i++) if (_typeConfigs[i] != null) Destroy(_typeConfigs[i]);
            }
            CombatWorld.Reset();
        }
    }
}
