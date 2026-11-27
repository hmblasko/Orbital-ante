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
    /// near it, puts it away when they leave, and remembers the ones that were killed for the rest of the run.
    ///
    /// - An animal is a child of its world, so it is carried round with it; "up" and the depth of water come from
    ///   the world's own terrain (<see cref="PlanetWorld.Read"/>), which needs no colliders.
    /// - At most <see cref="SeaFieldRules.liveCap"/> at once, and one is made a think, so nobody arrives as a hitch.
    /// </summary>
    public sealed class SeaCreatures : MonoBehaviour
    {
        sealed class World
        {
            public List<SeaHaunt> Haunts;
            public bool[] Gone, Awake;
            public Transform Root;
        }

        sealed class Live
        {
            public Maw Who;
            public int Body, Haunt;
        }

        SolarSystemWorld _world;
        CreatureConfig _config;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly List<Live> _live = new List<Live>();
        float _nextThink;

        /// <summary>Swimmers awake right now.</summary>
        public int LiveCount => _live.Count;

        public static SeaCreatures Build(SolarSystemWorld world, CreatureConfig config)
        {
            var go = new GameObject("Sea creatures");
            go.transform.SetParent(world.transform, false);
            var seas = go.AddComponent<SeaCreatures>();
            seas._world = world;
            seas._config = config;
            // Read the model while the run is being got ready, not on the frame the first one comes into range.
            if (config != null && config.seas.creaturesInTheSeas) OrbitalAnte.Look.ModelLibrary.MaterialFor(MawLayout.Model);
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
                var root = new GameObject("Sea creatures");
                root.transform.SetParent(surface.Body.transform, false);
                w.Root = root.transform;
            }
            _worlds[body] = w;
            return w;
        }

        void Update()
        {
            if (_world == null || _config == null || Time.time < _nextThink) return;
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
                bool stay = w != null && l.Body == body && (l.Who.transform.localPosition - at).sqrMagnitude < rules.sleepRange * rules.sleepRange;
                if (stay) continue;
                if (_worlds.TryGetValue(l.Body, out World was) && was != null) was.Awake[l.Haunt] = false;
                Destroy(l.Who.gameObject);
                _live.RemoveAt(i);
            }
            if (w == null || _live.Count >= Mathf.Max(0, rules.liveCap)) return;

            // Wake the nearest sleeping haunt in range, one a think.
            int best = -1;
            float nearest = rules.wakeRange * rules.wakeRange;
            for (int h = 0; h < w.Haunts.Count; h++)
            {
                if (w.Awake[h] || w.Gone[h]) continue;
                float d = (Place(surface, w.Haunts[h]) - at).sqrMagnitude;
                if (d < nearest) { nearest = d; best = h; }
            }
            if (best < 0) return;

            SeaHaunt haunt = w.Haunts[best];
            Transform world = surface.Body.transform;
            Vector3 up = world.TransformDirection(SpaceConvert.ToVector3(haunt.Direction)).normalized;
            // Facing some seeded way along the water.
            var random = new System.Random(haunt.Seed);
            Vector3 along = Vector3.ProjectOnPlane(new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f), up);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(world.right, up);
            int bodyIndex = body;
            Maw maw = Maw.Create(world.TransformPoint(Place(surface, haunt)), Quaternion.LookRotation(along.normalized, up), w.Root, _config, "Maw " + best,
                                 place => WaterAt(bodyIndex, place));
            if (maw == null)
            {
                // No model in this build: do not ask again.
                w.Gone[best] = true;
                return;
            }
            w.Awake[best] = true;
            var live = new Live { Who = maw, Body = body, Haunt = best };
            World home = w;
            maw.Died += _ => home.Gone[live.Haunt] = true;
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
            var water = new MawWater { Up = Vector3.up };
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
