using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Bases;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
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
            /// <summary>How many of each camp are still to be fought.</summary>
            public int[] Left;
            /// <summary>Which members of each camp are out of the fight for good, so a camp comes back as it was left.</summary>
            public bool[][] Gone;
            public bool[] Awake;
            public Transform Root;
        }

        sealed class Live
        {
            public Mannequin Who;
            public int Body, Camp, Member;
            public bool Counted;
            public Weapon Gun;
        }

        SolarSystemWorld _world;
        EnemyConfig _config;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly List<Live> _live = new List<Live>();
        MannequinConfig[] _typeConfigs;
        Vector3 _gravityBefore;
        bool _gravityTaken;
        float _nextThink;
        GibManager _gibs;

        void OnWorldShifted(Vector3 by)
        {
            if (_gibs != null) _gibs.Shift(by);
        }
        // The camp being woken, one enemy a frame so nobody arrives as a hitch.
        World _wakingWorld;
        PlanetSurface _wakingSurface;
        PlanetFooting _wakingFooting;
        SeededRandom _wakingRandom;
        int _wakingBody, _wakingCamp = -1, _wakingMember;
        Squad _wakingSquad;
        // Where a base's own design says its guards stand, in the body's space: on the floor, and up on its towers.
        readonly List<Vector3> _wakingSpawns = new List<Vector3>();
        readonly List<Vector3> _wakingLookouts = new List<Vector3>();
        readonly List<Vector3> _places = new List<Vector3>();
        int _lookoutsUsed;
        readonly List<Weapon> _guns = new List<Weapon>();

        /// <summary>The most guns left lying about before the oldest are cleared away.</summary>
        const int LooseGunCap = 12;
        /// <summary>Everyone standing or lying about, living or not: what the cap is measured against.</summary>
        public int PresentCount { get; private set; }

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
            // The loose chunks, the blood and the gunfire effects live and die with this world.
            GoreConfig gore = GoreConfig.Load();
            spawner._gibs = GibManager.Create(go.transform, gore);
            GoreFx.Create(go.transform, gore);
            WeaponFx.Create(go.transform);
            if (world.System != null) world.System.WorldShifted += spawner.OnWorldShifted;
            // Every kind's body and gun is read now, while the world loads, and not on the frame the first one stands up.
            OrbitalAnte.Look.ModelLibrary.Find("Enemy");
            foreach (EnemyTypeSpec spec in config.roster)
            {
                if (!string.IsNullOrEmpty(spec.model)) OrbitalAnte.Look.ModelLibrary.Find(spec.model);
                WeaponData gun = string.IsNullOrEmpty(spec.weapon) ? WeaponData.Rifle : ContentDatabase.Get<WeaponData>(spec.weapon) ?? WeaponData.Rifle;
                if (gun != null) OrbitalAnte.Look.ModelLibrary.Find(WeaponModelFactory.ModelIdFor(gun));
            }
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
                else if (surface.Body.Spec.id == "Earth") pad = OrbitalAnte.Launch.LaunchSite.Find(terrain).Up;
                var random = GameSession.Stream("enemies." + surface.Body.Spec.id);
                double radius = surface.SurfaceRadius;
                w = new World { Camps = EnemyField.Build(_config.field, _config.roster, random, radius, dry, pad) };
                // Every base has its guards, standing on its floor.
                IReadOnlyList<BaseSite> sites = _world.Bases != null ? _world.Bases.SitesOn(body) : null;
                if (sites != null)
                {
                    BaseFieldRules baseRules = _world.Bases.Rules;
                    for (int i = 0; i < sites.Count; i++)
                        EnemyField.AddGarrison(w.Camps, _config.field, _config.roster, random, radius, sites[i].Up, sites[i].FloorRadius, baseRules.Guards(sites[i].Size), (int)sites[i].Size);
                }
                w.Left = new int[w.Camps.Count];
                w.Awake = new bool[w.Camps.Count];
                w.Gone = new bool[w.Camps.Count][];
                for (int i = 0; i < w.Left.Length; i++)
                {
                    w.Left[i] = w.Camps[i].Members.Length;
                    w.Gone[i] = new bool[w.Left[i]];
                }
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
            WakeNext();
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
                // A head rolling about is beaten: it cannot shoot, so it no longer holds a place or keeps a camp alive.
                bool fighting = l.Who.Alive && l.Who.State != MovementState.HeadOnly;
                if (fighting) { alive++; continue; }
                if (!l.Counted)
                {
                    l.Counted = true;
                    Killed++;
                    World w = _worlds[l.Body];
                    if (!w.Gone[l.Camp][l.Member])
                    {
                        w.Gone[l.Camp][l.Member] = true;
                        w.Left[l.Camp]--;
                    }
                    // The body lies where it fell for a while, then is cleared away, and its gun with it
                    // unless somebody has picked the gun up.
                    Destroy(l.Who.gameObject, _config.bodySeconds);
                    if (l.Gun != null) StartCoroutine(ClearGun(l.Gun, _config.bodySeconds));
                }
            }
            LiveCount = alive;
            PresentCount = _live.Count;

            // Never more than a dozen guns lying about: the oldest nobody is holding goes first.
            int loose = 0;
            for (int i = _guns.Count - 1; i >= 0; i--)
            {
                if (_guns[i] == null) { _guns.RemoveAt(i); continue; }
                if (_guns[i].Holder == null) loose++;
            }
            for (int i = 0; i < _guns.Count && loose > LooseGunCap; i++)
            {
                if (_guns[i].Holder != null) continue;
                Destroy(_guns[i].gameObject);
                loose--;
            }
        }

        System.Collections.IEnumerator ClearGun(Weapon gun, float after)
        {
            yield return new WaitForSeconds(after);
            if (gun != null && gun.Holder == null) Destroy(gun.gameObject);
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
                if (_wakingWorld == was && _wakingCamp == l.Camp) _wakingCamp = -1;
                // Its gun goes with it, held or dropped, unless the player has taken it: left lying, it would fall
                // through the world once the ground under it is put away.
                if (l.Gun != null && (l.Gun.Holder == null || l.Gun.Holder is MannequinCombat)) Destroy(l.Gun.gameObject);
                Destroy(l.Who.gameObject);
                _live.RemoveAt(i);
            }
            if (w == null || height > _config.field.wakeRange)
            {
                ReleaseGravity();
                return;
            }

            // Wake the nearest sleeping camp that still has anyone in it, one a think, so the cost is spread.
            // A base's guards come first, and room is kept for the guards of a base the player is heading for:
            // nobody should reach an empty base and have its guards appear round them.
            int best = -1, bestGuards = -1, held = 0;
            double nearest = _config.field.wakeRange, nearestGuards = _config.field.wakeRange, nearestHeld = _config.field.sleepRange;
            for (int c = 0; c < w.Camps.Count; c++)
            {
                if (w.Awake[c] || w.Left[c] == 0) continue;
                double d = EnemyField.GroundDistance(at, w.Camps[c].Direction, radius);
                bool guards = w.Camps[c].HomeRadius > 0;
                if (guards && d < nearestGuards) { nearestGuards = d; bestGuards = c; }
                if (guards && d < nearestHeld) { nearestHeld = d; held = w.Left[c]; }
                if (!guards && d < nearest) { nearest = d; best = c; }
            }
            if (_wakingCamp >= 0) return;
            if (bestGuards >= 0)
            {
                best = bestGuards;
                held = 0;
            }
            if (best < 0) return;
            // The fallen still lying about count too: they cost as much to draw as the living.
            if (PresentCount + w.Left[best] + held > _config.field.liveCap) return;
            w.Awake[best] = true;
            _wakingWorld = w;
            _wakingSurface = surface;
            _wakingBody = body;
            _wakingCamp = best;
            _wakingMember = 0;
            _wakingRandom = new SeededRandom(w.Camps[best].Seed);
            _wakingSquad = new Squad(w.Left[best]);
            Vec3d direction = w.Camps[best].Direction;
            Vector3 home = SpaceConvert.ToVector3(direction * HomeRadius(w.Camps[best], surface));
            _wakingSpawns.Clear();
            _wakingLookouts.Clear();
            _lookoutsUsed = 0;
            _wakingFooting = new PlanetFooting(surface.Body.transform, surface, home, _config.field.roam);
            if (w.Camps[best].HomeRadius > 0)
            {
                // A base's guards: its floor is their ground. They keep to it more than a camp keeps to its patch.
                BaseSite? site = _world.Bases != null ? _world.Bases.SiteAt(body, direction) : null;
                if (site.HasValue)
                {
                    _wakingFooting.SetFloor(home, (float)site.Value.Footprint);
                    BaseSite s = site.Value;
                    _places.Clear();
                    BaseView.Places(s, "Spawn", _places);
                    for (int i = 0; i < _places.Count; i++) _wakingSpawns.Add(SpaceConvert.ToVector3(s.ToBody(_places[i].x, _places[i].y, _places[i].z)));
                    _places.Clear();
                    BaseView.Places(s, "Guard", _places);
                    for (int i = 0; i < _places.Count; i++) _wakingLookouts.Add(SpaceConvert.ToVector3(s.ToBody(_places[i].x, _places[i].y, _places[i].z)));
                }
            }
        }

        static double HomeRadius(EnemyCamp camp, PlanetSurface surface)
        {
            return camp.HomeRadius > 0 ? camp.HomeRadius : surface.GroundRadius(camp.Direction);
        }

        /// <summary>Stands up the next member of the camp being woken: one a frame.</summary>
        void WakeNext()
        {
            if (_wakingCamp < 0) return;
            World w = _wakingWorld;
            PlanetSurface surface = _wakingSurface;
            int camp = _wakingCamp, body = _wakingBody;
            EnemyCamp c = w.Camps[camp];
            int size = c.Members.Length;
            if (surface == null || w.Root == null) { _wakingCamp = -1; return; }
            // The random numbers are drawn for every member, gone or not, so each survivor is the same one as before.
            SeededRandom rng = _wakingRandom;
            PlanetFooting footing = _wakingFooting;
            Transform planet = surface.Body.transform;
            Vector3 home = SpaceConvert.ToVector3(c.Direction * HomeRadius(c, surface));
            Vector3 up = home.normalized;
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(up, side);

            bool made = false;
            while (!made && _wakingMember < size)
            {
                int m = _wakingMember++;
                float jitter = rng.NextFloat();
                int wanderSeed = rng.Next(), combatSeed = rng.Next();
                if (w.Gone[camp][m]) continue;
                made = true;
                int type = Mathf.Clamp(c.Members[m], 0, _config.roster.Count - 1);
                EnemyTypeSpec spec = _config.roster[type];
                MannequinConfig config = ConfigFor(type);
                // Spread round the camp's middle, a couple of metres apart.
                float angle = (m + jitter * 0.6f) * Mathf.PI * 2f / size, far = 2f + 1.2f * (m % 3);
                // A base's guards are spread over its floor, not bunched in the middle.
                if (footing.FloorRadius > 0f) far = footing.FloorRadius * (0.25f + 0.35f * ((m * 0.37f + jitter) % 1f));
                Vector3 position = footing.OnTheGround(home + (side * Mathf.Cos(angle) + along * Mathf.Sin(angle)) * far);
                // A base's own design says where its guards stand: the ones that hold still go up on the towers
                // as lookouts, the rest take the marked places on the floor.
                bool lookout = false;
                if (spec.holdsStill && _lookoutsUsed < _wakingLookouts.Count)
                {
                    position = planet.TransformPoint(_wakingLookouts[_lookoutsUsed++]);
                    lookout = true;
                }
                else if (_wakingSpawns.Count > 0) position = planet.TransformPoint(_wakingSpawns[m % _wakingSpawns.Count]);
                Vector3 worldUp = footing.Up(position);
                if (lookout || _wakingSpawns.Count > 0)
                {
                    // Facing outward from the middle of the base.
                    Vector3 outward = Vector3.ProjectOnPlane(position - planet.TransformPoint(home), worldUp);
                    if (outward.sqrMagnitude > 0.01f) angle = float.NaN;
                }
                Vector3 facing = float.IsNaN(angle)
                    ? Vector3.ProjectOnPlane(position - planet.TransformPoint(home), worldUp)
                    : Vector3.ProjectOnPlane(planet.TransformDirection(side * Mathf.Cos(angle + 1.3f) + along * Mathf.Sin(angle + 1.3f)), worldUp);
                Quaternion rotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.Cross(worldUp, Vector3.right), worldUp);

                Weapon gun = null;
                Mannequin who = MannequinFactory.Create(position, rotation, w.Root, config, spec.id + " " + camp + "." + m);
                who.Footing = footing;
                float tall = Mathf.Max(0.5f, spec.scale), wide = tall * Mathf.Clamp(spec.width, 0.6f, 1.8f);
                who.transform.localScale = new Vector3(wide, tall, wide);
                var locomotion = who.GetComponent<MannequinLocomotion>();
                if (locomotion != null)
                {
                    locomotion.SetWander(default, wanderSeed);
                    // A lookout keeps its post on the tower.
                    if (lookout) locomotion.Wander = false;
                }
                var combat = who.GetComponent<MannequinCombat>();
                if (combat != null)
                {
                    combat.SetSeed(combatSeed);
                    combat.Squad = _wakingSquad;
                    WeaponData data = ContentDatabase.Get<WeaponData>(spec.weapon) ?? WeaponData.Rifle;
                    Weapon weapon = Weapon.Create(data, w.Root, config.oneHandedReloadAllowed);
                    weapon.transform.position = position + worldUp * 1.2f;
                    combat.TryPickUp(weapon);
                    _guns.Add(weapon);
                    gun = weapon;
                }
                _live.Add(new Live { Who = who, Body = body, Camp = camp, Member = m, Gun = gun });
                LiveCount++;
                PresentCount++;
            }
            if (_wakingMember >= size) _wakingCamp = -1;
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
            config.headHp *= spec.headToughness > 0f ? spec.headToughness : spec.toughness;
            // Nobody fights from further off than the ground is solid: past that, shots and sight lines would
            // pass straight through hills.
            float solid = _world.Planets != null && _world.Planets.Data != null ? _world.Planets.Data.settings.solidGroundRange - 10f : 80f;
            config.engageRange = Mathf.Min(spec.engageRange, solid);
            config.reactionTime = spec.reactionTime;
            config.aimError = spec.aimError;
            config.keepFar = spec.keepFar;
            config.keepNear = spec.keepNear;
            config.rushes = spec.rushes;
            config.holdsStill = spec.holdsStill;
            config.closesIn = spec.closesIn;
            config.modelId = spec.model ?? "";
            config.aimTell = spec.aimTell;
            if (spec.red + spec.green + spec.blue > 0f) config.torsoColor = new Color(spec.red, spec.green, spec.blue);
            if (type < _typeConfigs.Length) _typeConfigs[type] = config;
            return config;
        }

        void FixedUpdate()
        {
            // Limbs and dropped guns fall the way the player feels "down" while there is anyone to shoot.
            if ((LiveCount <= 0 && !_gravityTaken) || _world == null || _world.System == null || _world.System.Focus == null) return;
            if (!_gravityTaken)
            {
                _gravityBefore = Physics.gravity;
                _gravityTaken = true;
            }
            // Only when it has really changed: setting it wakes every sleeping limb and gun.
            Vector3 felt = _world.System.Focus.FeltGravity;
            if ((felt - Physics.gravity).sqrMagnitude > 0.01f) Physics.gravity = felt;
        }

        void ReleaseGravity()
        {
            if (!_gravityTaken) return;
            Physics.gravity = _gravityBefore;
            _gravityTaken = false;
        }

        void OnDestroy()
        {
            if (_gravityTaken) Physics.gravity = _gravityBefore;
            if (_world != null && _world.System != null) _world.System.WorldShifted -= OnWorldShifted;
            if (_typeConfigs != null)
            {
                for (int i = 0; i < _typeConfigs.Length; i++) if (_typeConfigs[i] != null) Destroy(_typeConfigs[i]);
            }
            // Only if the fight is still this world's: after a restart into the arena, the arena has already set its own.
            if (_world != null && _world.Astronaut != null && CombatWorld.Player == _world.Astronaut.Armour) CombatWorld.Reset();
        }
    }
}
