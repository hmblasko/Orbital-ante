using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Enemies;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using OrbitalAnte.Tasks;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// The creatures of the land on the worlds of the solar system (Ralph loop pass 8): the Strider, the Sprinter, the
    /// Roller and the Screamer, each world with its own animals (<see cref="LandHabitats"/>). Where they live is seeded
    /// per world (<see cref="LandField"/>, worked out on a background thread), more of the haunts are lived in at each
    /// ante, and this wakes the animal of a haunt when the player comes near it, puts it away when they leave, and
    /// remembers the ones that were killed for the rest of the run.
    ///
    /// - An animal is a child of its world, so it is carried round with it, and stands on the world's own built ground
    ///   (<see cref="LandFooting"/>), which needs no colliders, so it keeps to the ground however far off it is awake.
    /// - At most <see cref="LandFieldRules.liveCap"/> at once, and one is made a think, so nobody arrives as a hitch.
    /// - Every animal is told who pays for it (<see cref="CreatureCredit"/>): its kind, its world and a claim per haunt,
    ///   so a kill counts toward the ante once a run.
    /// </summary>
    public sealed class LandCreatures : MonoBehaviour
    {
        sealed class World
        {
            public List<LandHaunt> Haunts;
            /// <summary>Where each haunt is, in the world's own space, on the ground as the terrain shapes it (for deciding when to wake it).</summary>
            public Vector3[] Places;
            public bool[] Gone, Awake;
            public Transform Root;
            /// <summary>The world's id, for claims ("Mars").</summary>
            public string Id;
            public LandFooting Footing;
        }

        /// <summary>What the background search hands back: the haunts, and how far from the world's middle the ground is at each.</summary>
        sealed class Found
        {
            public List<LandHaunt> Haunts;
            public double[] Ground;
        }

        sealed class Live
        {
            /// <summary>A <see cref="Walker"/>, a <see cref="Roller"/> or a <see cref="Screamer"/>.</summary>
            public Component Who;
            public int Body, Haunt;
        }

        SolarSystemWorld _world;
        CreatureConfig _config;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly Dictionary<int, Found> _found = new Dictionary<int, Found>();
        readonly Dictionary<int, PlanetJobs.Handle> _searches = new Dictionary<int, PlanetJobs.Handle>();
        readonly List<Live> _live = new List<Live>();
        float _nextThink;

        /// <summary>Land creatures awake right now.</summary>
        public int LiveCount => _live.Count;

        /// <summary>
        /// True while a land creature awake sees the player or, hurt, still knows where they are: the music hears it as
        /// being hunted, and holds a creature's fight between its strikes.
        /// </summary>
        public bool AnyAware
        {
            get
            {
                for (int i = 0; i < _live.Count; i++)
                {
                    Component who = _live[i].Who;
                    if (who == null) continue;
                    if (who is Walker walker && walker.AwareOfPlayer) return true;
                    if (who is Roller roller && roller.AwareOfPlayer) return true;
                    if (who is Screamer screamer && screamer.AwareOfPlayer) return true;
                }
                return false;
            }
        }

        public static LandCreatures Build(SolarSystemWorld world, CreatureConfig config)
        {
            var go = new GameObject("Land creatures");
            go.transform.SetParent(world.transform, false);
            var land = go.AddComponent<LandCreatures>();
            land._world = world;
            land._config = config;
            // Read the models while the run is being got ready, not on the frame the first one comes into range.
            if (config != null && config.land.creaturesOnLand)
            {
                foreach (LandKind kind in System.Enum.GetValues(typeof(LandKind))) ModelLibrary.MaterialFor(LandHabitats.ModelOf(kind));
            }
            return land;
        }

        /// <summary>The haunts on a body, once they are known. Null if it has no land creatures or they are still being worked out.</summary>
        public IReadOnlyList<LandHaunt> HauntsOn(int body) => WorldOf(body)?.Haunts;

        World WorldOf(int body)
        {
            if (_worlds.TryGetValue(body, out World w)) return w;
            PlanetSurface surface = _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            if (surface == null)
            {
                _worlds[body] = null;
                return null;
            }
            if (!surface.Ready || surface.Terrain == null) return null;   // not built yet: ask again later
            LandFieldRules rules = _config.land;
            string id = surface.Body.Spec.id;
            LandMix mix = LandHabitats.For(id);
            if (LandField.HauntCount(rules, surface.SurfaceRadius, mix) == 0)
            {
                _worlds[body] = null;
                return null;
            }

            // Looking for gentle dry ground for dozens of haunts takes some thousandths of a second, so it is done on a
            // background thread (the ground's rules are safe there) and this answers "not yet" meanwhile.
            if (!_found.TryGetValue(body, out Found found))
            {
                // A search that was thrown away (the jobs were reset under it) is started again.
                if (_searches.TryGetValue(body, out PlanetJobs.Handle old) && old.Cancelled) _searches.Remove(body);
                if (_searches.ContainsKey(body)) return null;
                // Clear of the camps and the bases' guards, who wait for the bases to be found: ask again until they are.
                IReadOnlyList<EnemyCamp> camps = _world.Enemies != null ? _world.Enemies.CampsOn(body) : null;
                if (_world.Enemies != null && camps == null) return null;
                var clear = new List<Vec3d>();
                for (int i = 0; camps != null && i < camps.Count; i++)
                    if (camps[i].Cave == null) clear.Add(camps[i].Direction);
                // Nowhere near the launch pad (or where it will be: it may not be built yet).
                Vec3d pad = Vec3d.Zero;
                if (_world.Base != null && _world.Base.Surface == surface) pad = _world.Base.Spot.Up;
                else if (id == "Earth") pad = OrbitalAnte.Launch.LaunchSite.Find(surface.Terrain).Up;
                var random = GameSession.Stream("land.creatures." + id);
                PlanetTerrain terrain = surface.Terrain;
                double radius = surface.SurfaceRadius, slope = rules.hauntSlope, around = rules.roomAround, rise = rules.roomRise;
                // It ranks as a ground tile 400 m off and never takes the last free core, so the ground under the player
                // always comes first. A search that fails answers "no haunts".
                _searches[body] = PlanetJobs.Run(() =>
                    {
                        try
                        {
                            var result = new Found { Haunts = LandField.Build(rules, random, radius, mix, d => Fits(terrain, radius, slope, around, rise, d), pad, clear) };
                            result.Ground = new double[result.Haunts.Count];
                            for (int i = 0; i < result.Ground.Length; i++) result.Ground[i] = radius + terrain.Height(result.Haunts[i].Direction);
                            return result;
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogException(e);
                            return new Found { Haunts = new List<LandHaunt>(), Ground = new double[0] };
                        }
                    },
                    result =>
                    {
                        _found[body] = result;
                        _searches.Remove(body);
                    }, () => -2e6f + 400f, true);
                return null;
            }
            _found.Remove(body);
            List<LandHaunt> haunts = found.Haunts;
            w = new World { Haunts = haunts, Id = id, Footing = new LandFooting(surface.Body.transform, surface) };
            w.Places = new Vector3[haunts.Count];
            for (int i = 0; i < haunts.Count; i++) w.Places[i] = SpaceConvert.ToVector3(haunts[i].Direction * found.Ground[i]);
            w.Gone = new bool[haunts.Count];
            w.Awake = new bool[haunts.Count];
            var root = new GameObject("Land creatures");
            root.transform.SetParent(surface.Body.transform, false);
            w.Root = root.transform;
            _worlds[body] = w;
            return w;
        }

        /// <summary>
        /// A haunt's ground: dry (clear of the sea and every pond), gentle for a few metres round, with room to fight
        /// round it (the ground <paramref name="around"/> off no more than <paramref name="rise"/> above or below: a
        /// Strider wanders 22 m, a Roller charges from 5 to 22 m), and not at a cave's mouth. Safe on a background
        /// thread (a world's terrain never changes once it is built).
        /// </summary>
        static bool Fits(PlanetTerrain terrain, double radius, double slope, double around, double rise, Vec3d d)
        {
            double here = terrain.Height(d);
            if (terrain.HasSea && here < terrain.SeaLevel + 1.0) return false;
            for (int p = 0; p < terrain.Ponds.Count; p++)
            {
                Pond pond = terrain.Ponds[p];
                if (Vec3d.Dot(d, pond.Direction.Normalized) > System.Math.Cos(System.Math.Min(System.Math.PI, (pond.Radius + 6.0) / radius))) return false;
            }
            Vec3d east = Vec3d.Cross(System.Math.Abs(d.Y) < 0.9 ? Vec3d.Up : Vec3d.Right, d).Normalized, north = Vec3d.Cross(d, east);
            const double reach = 4.0;
            for (int ring = 0; ring < 2; ring++)
            {
                double angle = (ring == 0 ? reach : around) / radius, most = ring == 0 ? slope * reach : rise;
                for (int k = 0; k < 4; k++)
                {
                    Vec3d way = (k & 1) == 0 ? east : north;
                    double there = terrain.Height((d + way * (k < 2 ? angle : -angle)).Normalized);
                    if (System.Math.Abs(there - here) > most) return false;
                }
            }
            Vec3d point = d * (radius + here);
            for (int c = 0; c < terrain.Caves.Count; c++)
                if (TerrainCollision.InsideCave(terrain.Caves[c], point, 6.0)) return false;
            return true;
        }

        void Update()
        {
            if (_world == null || _config == null) return;
            // A land creature after the player is a fight for the rest of the game too (the readout, the tips, the
            // restless camps), as a sighting is: said every frame one is.
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
            LandFieldRules rules = _config.land;
            World w = rules.creaturesOnLand && surface != null && surface.Ready ? WorldOf(body) : null;
            Transform here = w != null ? surface.Body.transform : null;
            Vector3 at = here != null ? here.InverseTransformPoint(focus.transform.position) : Vector3.zero;

            // Put away whoever the player has left behind (or everyone, on leaving the world).
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                if (l.Who == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                bool stay = here != null && l.Body == body && (here.InverseTransformPoint(l.Who.transform.position) - at).sqrMagnitude < rules.sleepRange * rules.sleepRange;
                if (stay) continue;
                if (_worlds.TryGetValue(l.Body, out World was) && was != null) was.Awake[l.Haunt] = false;
                Destroy(l.Who.gameObject);
                _live.RemoveAt(i);
            }
            // The ground must be showing to stand on, as for the camps.
            if (w == null || !surface.GroundShown || _live.Count >= Mathf.Max(0, rules.liveCap)) return;

            // Wake the nearest haunt in range that is lived in at this ante, one a think; never one nearer than
            // wakeNoNearer, so none is seen to appear beside the player.
            int ante = EnemySpawner.PressureAnte;
            int best = -1;
            float nearest = rules.wakeRange * rules.wakeRange, nearer = rules.wakeNoNearer * rules.wakeNoNearer;
            for (int h = 0; h < w.Haunts.Count; h++)
            {
                if (w.Awake[h] || w.Gone[h] || !LandField.Inhabited(rules, w.Haunts[h], ante)) continue;
                float d = (w.Places[h] - at).sqrMagnitude;
                if (d < nearest && d >= nearer)
                {
                    nearest = d;
                    best = h;
                }
            }
            if (best >= 0) Wake(w, surface, body, best);
        }

        void Wake(World w, PlanetSurface surface, int body, int index)
        {
            LandHaunt haunt = w.Haunts[index];
            LandFieldRules rules = _config.land;
            Transform world = surface.Body.transform;
            LandFooting footing = w.Footing;
            Vector3 up = world.TransformDirection(SpaceConvert.ToVector3(haunt.Direction)).normalized;
            // Facing some seeded way along the ground, drawn in the world's own space, so it does not depend on how far
            // the world has turned.
            var random = new SeededRandom(haunt.Seed);
            Vector3 localWay = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f);
            Vector3 along = Vector3.ProjectOnPlane(world.TransformDirection(localWay), up);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(world.right, up);
            along.Normalize();
            Vector3 place = Clear(footing, world.TransformPoint(Place(surface, haunt)), up, along, haunt.Kind);
            Vector3 placeUp = footing.Up(place);
            Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(along, placeUp).normalized, placeUp);
            string name = haunt.Kind + " " + index;
            int hauntIndex = index;
            World there = w;
            Component who;
            switch (haunt.Kind)
            {
                case LandKind.Strider:
                case LandKind.Sprinter:
                {
                    Walker walker = Walker.Create(haunt.Kind == LandKind.Strider ? WalkerLayout.Strider : WalkerLayout.Sprinter, place, facing, w.Root, _config, haunt.Seed, name);
                    if (walker != null)
                    {
                        walker.Footing = footing;
                        walker.Steepest = rules.SlopeFor(haunt.Kind);
                        walker.StepOver = rules.StepOverFor(haunt.Kind);
                        walker.Credit = Credit(haunt, body, w.Id, hauntIndex);
                        walker.Died += _ => there.Gone[hauntIndex] = true;
                    }
                    who = walker;
                    break;
                }
                case LandKind.Roller:
                {
                    Roller roller = Roller.Create(place, facing, w.Root, _config, haunt.Seed, name);
                    if (roller != null)
                    {
                        roller.Footing = footing;
                        roller.Steepest = rules.rollerSlope;
                        roller.RollSteepest = rules.rollSlope;
                        roller.Credit = Credit(haunt, body, w.Id, hauntIndex);
                        roller.Died += _ => there.Gone[hauntIndex] = true;
                    }
                    who = roller;
                    break;
                }
                default:
                {
                    Screamer screamer = Screamer.Create(place, facing, w.Root, _config, haunt.Seed, name);
                    if (screamer != null)
                    {
                        screamer.Footing = footing;
                        screamer.Steepest = rules.screamerSlope;
                        screamer.Credit = Credit(haunt, body, w.Id, hauntIndex);
                        screamer.Died += _ => there.Gone[hauntIndex] = true;
                    }
                    who = screamer;
                    break;
                }
            }
            if (who == null)
            {
                // No model in this build: do not ask again.
                w.Gone[index] = true;
                return;
            }
            w.Awake[index] = true;
            _live.Add(new Live { Who = who, Body = body, Haunt = index });
        }

        static CreatureCredit Credit(in LandHaunt haunt, int body, string world, int index)
            => new CreatureCredit(LandHabitats.ModelOf(haunt.Kind), body, TaskIds.LandCreature(world, index));

        /// <summary>
        /// Where to stand it up: its haunt, or the nearest spot round it with nothing standing in the way and the ground
        /// above the sea (a rock or a tree where it stands would hold it inside for good).
        /// </summary>
        static Vector3 Clear(LandFooting footing, Vector3 place, Vector3 up, Vector3 along, LandKind kind)
        {
            bool big = kind == LandKind.Strider;
            float height = big ? 2.2f : 0.9f, radius = big ? 1.6f : 0.7f;
            Vector3 side = Vector3.Cross(up, along).normalized;
            for (int ring = 0; ring < 3; ring++)
            {
                int around = ring == 0 ? 1 : 8;
                for (int k = 0; k < around; k++)
                {
                    Vector3 at = place;
                    if (ring > 0)
                    {
                        float a = k * Mathf.PI * 2f / around;
                        at = place + (along * Mathf.Cos(a) + side * Mathf.Sin(a)) * (ring * 5f);
                    }
                    Vector3 ground = footing.OnTheGround(at);
                    if (footing.AboveWater(ground) < 1.0) continue;
                    if (!Physics.CheckSphere(ground + footing.Up(ground) * height, radius, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return ground;
                }
            }
            return footing.OnTheGround(place);
        }

        /// <summary>Where a haunt is, in its world's own space: on the ground as it is built there.</summary>
        static Vector3 Place(PlanetSurface surface, in LandHaunt haunt)
            => SpaceConvert.ToVector3(haunt.Direction * surface.GroundRadius(haunt.Direction));

        void OnDestroy()
        {
            foreach (KeyValuePair<int, PlanetJobs.Handle> search in _searches) search.Value.Cancel();
            _searches.Clear();
        }
    }
}
