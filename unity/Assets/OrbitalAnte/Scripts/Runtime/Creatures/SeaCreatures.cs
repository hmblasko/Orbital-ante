using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// The big swimmers in the seas of the solar system: the first creatures outside the combat arena. Where they
    /// live is seeded per world (<see cref="SeaField"/>); this wakes the animal of a haunt when the player comes
    /// near it, puts it away when they leave, and remembers the ones that were killed for the rest of the run (a
    /// pack's fish one by one, so a pack woken again comes back without them).
    ///
    /// - An animal is a child of its world, so it is carried round with it; "up" and the depth of water come from
    ///   the world's own terrain (<see cref="PlanetWorld.Read"/>), which needs no colliders.
    /// - At most <see cref="SeaFieldRules.liveCap"/> at once, and one is made a think, so nobody arrives as a hitch.
    /// - Every animal is told who pays for it (<see cref="CreatureCredit"/>): its kind, its world and a claim per
    ///   haunt and place, so a kill counts toward the ante once a run.
    /// </summary>
    public sealed class SeaCreatures : MonoBehaviour
    {
        sealed class World
        {
            public List<SeaHaunt> Haunts;
            public bool[] Gone, Awake;
            /// <summary>Per haunt, the places in its pack whose fish were killed (null until one is).</summary>
            public HashSet<int>[] Dead;
            public Transform Root;
            /// <summary>The world's id, for claims ("earth").</summary>
            public string Id;
        }

        sealed class Live
        {
            /// <summary>A Drifting maw, or a pack of Pack biters (its <see cref="BiterShoal"/>).</summary>
            public Component Who;
            public int Body, Haunt;
        }

        static OrbitalAnte.Ship.Astronaut s_crew;
        static float s_lookForCrewAt = -1f;

        /// <summary>The player's suit, found at most once every two seconds for every swimmer together (never a search per animal).</summary>
        public static OrbitalAnte.Ship.Astronaut FindCrew()
        {
            if (s_crew == null && Time.unscaledTime >= s_lookForCrewAt)
            {
                s_lookForCrewAt = Time.unscaledTime + 2f;
                s_crew = FindAnyObjectByType<OrbitalAnte.Ship.Astronaut>();
            }
            return s_crew;
        }

        public static void ResetStatics()
        {
            s_crew = null;
            s_lookForCrewAt = -1f;
        }

        SolarSystemWorld _world;
        CreatureConfig _config;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly List<Live> _live = new List<Live>();
        float _nextThink;

        /// <summary>Swimmers awake right now.</summary>
        public int LiveCount => _live.Count;

        /// <summary>
        /// True while a swimmer awake has noticed the player in the water with it (a Drifting maw, or a pack of Pack
        /// biters): the music hears it as being hunted, and holds a creature's fight between its strikes.
        /// </summary>
        public bool AnyAware
        {
            get
            {
                for (int i = 0; i < _live.Count; i++)
                {
                    Component who = _live[i].Who;
                    if (who == null) continue;
                    if (who is Maw maw && maw.AwareOfPlayer) return true;
                    if (who is BiterShoal pack && pack.AwareOfPlayer) return true;
                }
                return false;
            }
        }

        public static SeaCreatures Build(SolarSystemWorld world, CreatureConfig config)
        {
            var go = new GameObject("Sea creatures");
            go.transform.SetParent(world.transform, false);
            var seas = go.AddComponent<SeaCreatures>();
            seas._world = world;
            seas._config = config;
            // Read the model while the run is being got ready, not on the frame the first one comes into range.
            if (config != null && config.seas.creaturesInTheSeas)
            {
                OrbitalAnte.Look.ModelLibrary.MaterialFor(MawLayout.Model);
                OrbitalAnte.Look.ModelLibrary.MaterialFor(BiterLayout.Model);
            }
            return seas;
        }

        /// <summary>The haunts on a body, worked out the first time they are asked for. Null if it has no sea or is not built yet.</summary>
        public IReadOnlyList<SeaHaunt> HauntsOn(int body) => WorldOf(body)?.Haunts;

        World WorldOf(int body)
        {
            if (_worlds.TryGetValue(body, out World w)) return w;
            PlanetSurface surface = _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            if (surface == null || !surface.Ready) return null;   // not built yet: ask again later
            w = null;
            PlanetTerrain terrain = surface.Terrain;
            if (terrain.HasSea)
            {
                var random = GameSession.Stream("sea.creatures." + surface.Body.Spec.id);
                double sea = terrain.SeaLevel;
                w = new World { Haunts = SeaField.Build(_config.seas, random, surface.SurfaceRadius + sea, d => sea - terrain.Height(d)) };
                w.Gone = new bool[w.Haunts.Count];
                w.Awake = new bool[w.Haunts.Count];
                w.Dead = new HashSet<int>[w.Haunts.Count];
                w.Id = surface.Body.Spec.id;
                var root = new GameObject("Sea creatures");
                root.transform.SetParent(surface.Body.transform, false);
                w.Root = root.transform;
            }
            _worlds[body] = w;
            return w;
        }

        void Update()
        {
            if (_world == null || _config == null) return;
            // A swimmer after the player is a fight for the rest of the game too (the readout, the tips, the restless
            // camps), as a sighting is: said every frame one is (Ralph loop pass 8).
            if (_live.Count > 0 && AnyAware) CombatWorld.CreatureAwareAt = Time.time;
            if (Time.time < _nextThink) return;
            _nextThink = Time.time + 0.5f;
            Think();
        }

        void Think()
        {
            SolarSystem system = _world.System;
            GravityBody focus = system != null ? system.Focus : null;
            int body = focus != null ? focus.Neighbourhood : -1;
            PlanetSurface surface = body > 0 && _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            SeaFieldRules rules = _config.seas;
            World w = rules.creaturesInTheSeas && surface != null && surface.Ready ? WorldOf(body) : null;
            Vector3 at = w != null ? surface.Body.transform.InverseTransformPoint(focus.transform.position) : Vector3.zero;

            // Put away whoever the player has left behind (or everyone, on leaving the world).
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                if (l.Who == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                // A pack is where its fish are, not where it was made.
                Vector3 where = l.Who is BiterShoal shoal && w != null ? surface.Body.transform.InverseTransformPoint(shoal.Middle) : l.Who.transform.localPosition;
                bool stay = w != null && l.Body == body && (where - at).sqrMagnitude < rules.sleepRange * rules.sleepRange;
                if (stay) continue;
                if (_worlds.TryGetValue(l.Body, out World was) && was != null) was.Awake[l.Haunt] = false;
                Destroy(l.Who.gameObject);
                _live.RemoveAt(i);
            }
            if (w == null || _live.Count >= Mathf.Max(0, rules.liveCap)) return;

            // Wake the nearest sleeping haunt in range, one a think. Only one pack is awake at a time: a pack's turns
            // keep its danger to one fish at once, and two packs would break that.
            bool packAwake = false;
            for (int i = 0; i < _live.Count; i++) if (_live[i].Who is BiterShoal) packAwake = true;
            int best = -1;
            float nearest = rules.wakeRange * rules.wakeRange;
            for (int h = 0; h < w.Haunts.Count; h++)
            {
                if (w.Awake[h] || w.Gone[h]) continue;
                if (packAwake && SeaField.IsPack(rules, w.Haunts[h])) continue;
                float d = (Place(surface, w.Haunts[h]) - at).sqrMagnitude;
                if (d < nearest) { nearest = d; best = h; }
            }
            if (best < 0) return;

            SeaHaunt haunt = w.Haunts[best];
            Transform world = surface.Body.transform;
            Vector3 up = world.TransformDirection(SpaceConvert.ToVector3(haunt.Direction)).normalized;
            // Facing some seeded way along the water.
            // Drawn in the world's own space, so it does not depend on how far the world has turned.
            var random = new SeededRandom(haunt.Seed);
            Vector3 localWay = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f);
            Vector3 along = Vector3.ProjectOnPlane(world.TransformDirection(localWay), up);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(world.right, up);
            int bodyIndex = body;
            Vector3 home = world.TransformPoint(Place(surface, haunt));
            Quaternion facing = Quaternion.LookRotation(along.normalized, up);
            System.Func<Vector3, MawWater> water = place => WaterAt(bodyIndex, place);
            Component who;
            string id = w.Id;
            int hauntIndex = best;
            if (SeaField.IsPack(rules, haunt))
            {
                int size = SeaField.PackSize(rules, haunt);
                HashSet<int> dead = w.Dead[best];
                if (dead != null && dead.Count >= size)
                {
                    w.Gone[best] = true;
                    return;
                }
                who = BiterShoal.Create(home, facing, w.Root, _config, size, "Biter pack " + best, water, dead,
                    place => new CreatureCredit(BiterLayout.Model, bodyIndex, OrbitalAnte.Tasks.TaskIds.Creature(id, hauntIndex, place)));
            }
            else
            {
                Maw maw = Maw.Create(home, facing, w.Root, _config, "Maw " + best, water);
                if (maw != null) maw.Credit = new CreatureCredit(MawLayout.Model, bodyIndex, OrbitalAnte.Tasks.TaskIds.Creature(id, hauntIndex, 0));
                who = maw;
            }
            if (who == null)
            {
                // No model in this build: do not ask again.
                w.Gone[best] = true;
                return;
            }
            w.Awake[best] = true;
            var live = new Live { Who = who, Body = body, Haunt = best };
            World there = w;
            if (who is Maw drifting) drifting.Died += _ => there.Gone[live.Haunt] = true;
            else if (who is BiterShoal pack)
            {
                pack.Gone += _ => there.Gone[live.Haunt] = true;
                pack.Lost += place => (there.Dead[live.Haunt] ??= new HashSet<int>()).Add(place);
            }
            _live.Add(live);
        }

        /// <summary>Where a haunt's animal lives, in its world's own space.</summary>
        static Vector3 Place(PlanetSurface surface, in SeaHaunt haunt)
        {
            double r = surface.SurfaceRadius + surface.Terrain.SeaLevel - haunt.Under;
            return SpaceConvert.ToVector3(haunt.Direction * r);
        }

        MawWater WaterAt(int body, Vector3 place)
        {
            // No up until the world is ready: a zero up leaves an animal as it is, where +Y would tip it over.
            var water = new MawWater { Up = Vector3.zero };
            PlanetWorld planets = _world != null ? _world.Planets : null;
            PlanetSurface surface = planets != null ? planets.SurfaceOf(body) : null;
            if (surface == null || !surface.Ready) return water;
            Vector3 from = place - surface.Body.transform.position;
            if (from.sqrMagnitude > 1e-6f) water.Up = from.normalized;
            GroundReading reading = planets.Read(body, place);
            if (reading.Known)
            {
                water.Above = (float)-reading.AboveSea;
                water.Below = (float)reading.AboveGround;
            }
            if (_world.System != null) water.Flow = _world.System.GroundVelocityAt(body, place);
            return water;
        }
    }
}
