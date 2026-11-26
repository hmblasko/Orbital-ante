using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Bases
{
    /// <summary>
    /// The bases on the worlds. Where they are is a rule (<see cref="BaseField"/>, the same for a seed every
    /// time). This builds the ones near the player as real objects, each a child of its body so it rides with
    /// it, and puts them away again when the player leaves.
    ///
    /// Every base stands on a foundation that reaches down to the ground beneath it (the ground itself is never
    /// reshaped). What stands on the foundation is the art's business: <see cref="BuildView"/> is called with
    /// the site and the object to build under. Until that is set, a base is its foundation and its guards.
    /// </summary>
    public sealed class PlanetBases : MonoBehaviour
    {
        /// <summary>
        /// Builds what stands on a base's floor, under the given parent (origin in the middle of the floor,
        /// +Y up, +Z north). Returns false if it built nothing. Set by the base models' own code.
        /// </summary>
        public static Func<BaseSite, Transform, bool> BuildView;
        /// <summary>How many designs there are of each size (small, medium, large). Set with <see cref="BuildView"/>.</summary>
        public static int[] Kinds = { 1, 1, 1 };

        /// <summary>A base is built when the player comes this near, and put away past the second distance, in metres.</summary>
        const float ShowWithin = 350f, HideBeyond = 450f;
        /// <summary>
        /// The most bases standing at once. The worlds are small: without this, most of a world's bases would be
        /// built at once, nearly all of them out of sight behind the curve of the ground.
        /// </summary>
        public const int ShownCap = 6;

        sealed class World
        {
            public List<BaseSite> Sites;
            public GameObject[] Built;
            /// <summary>Each base's foundation, kept once made so coming back to a base does not make it again.</summary>
            public Mesh[] Foundations;
            /// <summary>The rare gun lying at a base that holds one, while the base stands; and whether it has been taken this run.</summary>
            public Weapons.Weapon[] Rare;
            public bool[] RareTaken;
            /// <summary>The one base on this world that holds the rare gun, or -1.</summary>
            public int RareBase = -1;
            /// <summary>The one base on this world that holds a melee item (never the rare gun's), or -1, and which item.</summary>
            public int MeleeBase = -1;
            public string MeleeId;
            /// <summary>The melee item lying at a base while it stands; and whether the player has taken it this run.</summary>
            public Melee.MeleeItem[] MeleeItems;
            public bool[] MeleeTaken;
        }

        SolarSystemWorld _world;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
        readonly Dictionary<int, PlanetJobs.Handle> _searches = new Dictionary<int, PlanetJobs.Handle>();
        readonly Dictionary<int, List<BaseSite>> _found = new Dictionary<int, List<BaseSite>>();
        readonly List<Mesh> _owned = new List<Mesh>();
        Material _material;
        float _nextLook;

        public BaseFieldRules Rules { get; private set; }
        /// <summary>Bases standing as real objects right now.</summary>
        public int ShownCount { get; private set; }

        public static PlanetBases Build(SolarSystemWorld world, BaseFieldRules rules)
        {
            var go = new GameObject("Bases");
            go.transform.SetParent(world.transform, false);
            var bases = go.AddComponent<PlanetBases>();
            bases._world = world;
            bases.Rules = rules ?? new BaseFieldRules();
            return bases;
        }

        public static void ResetStatics()
        {
            BuildView = null;
            Kinds = new[] { 1, 1, 1 };
        }

        /// <summary>
        /// True once it is known what bases a body has (or that it has none). False while its ground is not
        /// built or the search for places is still running: ask again later, and do not take "no bases" as the answer.
        /// </summary>
        public bool Known(int body) => WorldOf(body) != null || _worlds.ContainsKey(body);

        /// <summary>The bases on a body, worked out the first time they are asked for. Null if its ground is not ready yet or it has none.</summary>
        public IReadOnlyList<BaseSite> SitesOn(int body) => WorldOf(body)?.Sites;

        /// <summary>The base whose middle is in this direction on a body, if there is one.</summary>
        public BaseSite? SiteAt(int body, Vec3d direction)
        {
            World w = WorldOf(body);
            if (w == null) return null;
            for (int i = 0; i < w.Sites.Count; i++)
                if (Vec3d.Dot(w.Sites[i].Up, direction) > 0.9999999) return w.Sites[i];
            return null;
        }

        World WorldOf(int body)
        {
            if (_worlds.TryGetValue(body, out World w)) return w;
            PlanetSurface surface = _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            if (surface == null) { _worlds[body] = null; return null; }
            if (!surface.Ready || surface.Terrain == null) return null;   // not built yet: ask again later

            // Looking for level ground for dozens of bases takes tens of thousandths of a second, so it is done
            // on a background thread (the ground's rules are safe there) and this answers "not yet" meanwhile.
            if (!_found.TryGetValue(body, out List<BaseSite> sites))
            {
                // A search that was thrown away (the jobs were reset under it) is started again.
                if (_searches.TryGetValue(body, out PlanetJobs.Handle old) && old.Cancelled) _searches.Remove(body);
                if (!_searches.ContainsKey(body))
                {
                    // Where the launch pad is (or will be: it may not be built yet), so the same seed always gives the same bases.
                    Vec3d pad = Vec3d.Zero;
                    if (_world.Base != null && _world.Base.Surface == surface) pad = _world.Base.Spot.Up;
                    else if (surface.Body.Spec.id == "Earth") pad = LaunchSite.Find(surface.Terrain).Up;
                    var random = GameSession.Stream("bases." + surface.Body.Spec.id);
                    PlanetTerrain terrain = surface.Terrain;
                    BaseFieldRules rules = Rules;
                    int[] kinds = (int[])Kinds.Clone();
                    // It ranks as a ground tile 300 m off, and never takes the last free core, so the ground under
                    // the player always comes first. A search that fails answers "no bases", so the world's
                    // enemies, who wait for it, are never held up for good.
                    _searches[body] = PlanetJobs.Run(() =>
                        {
                            try { return BaseField.Build(rules, terrain, random, kinds, pad); }
                            catch (Exception e) { Debug.LogException(e); return new List<BaseSite>(); }
                        },
                        result => { _found[body] = result; _searches.Remove(body); }, () => -2e6f + 300f, true);
                }
                return null;
            }
            w = new World { Sites = sites };
            _worlds[body] = w;   // before the rest, so a fault below never starts the search over
            _found.Remove(body);
            w.Built = new GameObject[w.Sites.Count];
            w.Foundations = new Mesh[w.Sites.Count];
            w.Rare = new Weapons.Weapon[w.Sites.Count];
            w.RareTaken = new bool[w.Sites.Count];
            var seeds = new List<int>(w.Sites.Count);
            for (int i = 0; i < w.Sites.Count; i++) seeds.Add(w.Sites[i].Seed);
            w.RareBase = Combat.RareGuns.PickBase(seeds);
            // At most one found melee item per world, at another base, chosen by weight from the world's own stream.
            w.MeleeItems = new Melee.MeleeItem[w.Sites.Count];
            w.MeleeTaken = new bool[w.Sites.Count];
            w.MeleeBase = Melee.MeleeLoot.PickBase(seeds, w.RareBase);
            if (w.MeleeBase >= 0)
            {
                var specs = new List<Melee.MeleeSpec>();
                foreach (Melee.MeleeData d in Melee.MeleeData.All()) specs.Add(d.ToSpec());
                w.MeleeId = Melee.MeleeLoot.PickItem(GameSession.Stream("melee.loot." + surface.Body.Spec.id).NextDouble(), specs);
            }
            // Nothing natural grows through a base's floor.
            if (surface.Features != null)
            {
                // Out to the foot of the foundation's skirt, not only the floor.
                for (int i = 0; i < w.Sites.Count; i++)
                    surface.Features.AlsoKeepClear(w.Sites[i].Centre, w.Sites[i].Footprint + (w.Sites[i].Depth + 1.5) * 1.15 + 1.0);
            }
            _worlds[body] = w;
            // From space, the bases are the lights on the world's night side.
            if (w.Sites.Count > 0)
            {
                var places = new Vec3d[w.Sites.Count];
                var sizes = new int[w.Sites.Count];
                for (int i = 0; i < places.Length; i++)
                {
                    places[i] = w.Sites[i].Up;
                    sizes[i] = (int)w.Sites[i].Size;
                }
                surface.SetNightLights(places, sizes, places.Length);
            }
            return w;
        }

        void Update()
        {
            if (_world == null || Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.5f;

            SolarSystem system = _world.System;
            GravityBody focus = system != null ? system.Focus : null;
            int body = focus != null ? focus.Neighbourhood : -1;
            PlanetSurface surface = body > 0 && _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            World here = surface != null ? WorldOf(body) : null;

            // Put away the bases of every world the player is not at.
            foreach (KeyValuePair<int, World> pair in _worlds)
            {
                World w = pair.Value;
                if (w == null || w == here) continue;
                PlanetSurface other = _world.Planets != null ? _world.Planets.SurfaceOf(pair.Key) : null;
                for (int i = 0; i < w.Built.Length; i++)
                {
                    if (w.Built[i] != null && other != null && SomethingStandsOn(w.Sites[i], other, pair.Key)) continue;
                    PutAway(w, i);
                }
            }
            if (here == null) return;

            Vector3 local = surface.Body.transform.InverseTransformPoint(focus.transform.position);
            bool builtOne = false;
            for (int i = 0; i < here.Sites.Count; i++)
            {
                float distance = (SpaceConvert.ToVector3(here.Sites[i].Centre) - local).magnitude;
                if (here.Built[i] != null)
                {
                    // Never from under something parked on it (the ship, while the player is off on foot).
                    if (distance > HideBeyond && !SomethingStandsOn(here.Sites[i], surface, body)) PutAway(here, i);
                }
                else if (distance < ShowWithin && !builtOne && ShownCount < ShownCap)
                {
                    // One a look, so several coming into range together do not arrive in one frame.
                    here.Built[i] = Make(here, i, surface);
                    ShownCount++;
                    builtOne = true;
                }
            }
        }

        bool SomethingStandsOn(BaseSite site, PlanetSurface surface, int body)
        {
            IReadOnlyList<GravityBody> movers = _world.System.Movers;
            Vector3 centre = SpaceConvert.ToVector3(site.Centre);
            float reach = (float)(site.Footprint + (site.Depth + 1.5) * 1.15 + 10.0);
            for (int i = 0; i < movers.Count; i++)
            {
                GravityBody m = movers[i];
                if (m == null || !m.NeedsGround || m.Neighbourhood != body) continue;
                if ((surface.Body.transform.InverseTransformPoint(m.transform.position) - centre).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        void PutAway(World w, int i)
        {
            if (w.Built[i] == null) return;
            // The rare gun is gone from the base if it is no longer lying in it (picked up, or carried off).
            if (w.Rare != null && w.Rare[i] != null && !w.Rare[i].transform.IsChildOf(w.Built[i].transform)) w.RareTaken[i] = true;
            else if (w.Rare != null && w.Rare[i] == null && w.RareBase == i) w.RareTaken[i] = true;
            if (w.Rare != null) w.Rare[i] = null;
            // A melee item still lying in the base goes with it and comes back with it; a taken one is not ours now.
            if (w.MeleeItems != null) w.MeleeItems[i] = null;
            Destroy(w.Built[i]);
            w.Built[i] = null;
            ShownCount--;
        }

        GameObject Make(World w, int index, PlanetSurface surface)
        {
            BaseSite site = w.Sites[index];
            var go = new GameObject("Base " + site.Size + " " + site.Kind);
            go.layer = Layers.Default;
            go.transform.SetParent(surface.Body.transform, false);
            go.transform.localPosition = SpaceConvert.ToVector3(site.Centre);
            go.transform.localRotation = Quaternion.LookRotation(SpaceConvert.ToVector3(site.North), SpaceConvert.ToVector3(site.Up));

            // The foundation: a low mound with a flat top, reaching below the lowest ground it covers.
            Mesh mesh = w.Foundations[index];
            if (mesh == null)
            {
                float depth = (float)site.Depth + 1.5f, top = (float)site.Footprint;
                var b = new MeshBuilder();
                b.Color(FoundationColour);
                b.Lathe(P3.Zero, new[] { top + depth * 1.15f, top + 0.5f, top }, new[] { -depth, -0.5f, 0f }, 28, capBottom: true, capTop: true);
                mesh = ShipMeshes.ToMesh(b.Finish(), "BaseFoundation");
                _owned.Add(mesh);
                w.Foundations[index] = mesh;
            }
            if (_material == null)
            {
                _material = new Material(PlanetMaterials.VertexColour(Color.white));
                _material.name = "OA_BaseFoundation";
            }
            var foundation = new GameObject("Foundation");
            foundation.layer = Layers.Default;
            foundation.transform.SetParent(go.transform, false);
            foundation.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = foundation.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            foundation.AddComponent<MeshCollider>().sharedMesh = mesh;

            if (BuildView != null)
            {
                try { BuildView(site, go.transform); }
                catch (Exception e) { Debug.LogException(e); }
            }
            PlaceRareGun(w, index, go.transform);
            PlaceMelee(w, index, go.transform);
            return go;
        }

        /// <summary>
        /// At most one base per world holds the rare one-hit gun, lying at its first loot point (Matthew, 16 Nov
        /// 2026: "a gun you found that is rare"; see <see cref="Combat.RareGuns.PickBase"/>). It lies still until
        /// picked up, and once taken it does not come back this run.
        /// </summary>
        void PlaceRareGun(World w, int index, Transform baseRoot)
        {
            if (w.Rare == null || w.RareTaken[index]) return;
            if (w.RareBase != index) return;
            BaseSite site = w.Sites[index];
            var places = new List<Vector3>();
            BaseView.Places(site, BaseKinds.Loot, places);
            if (places.Count == 0) return;
            Weapons.WeaponData data = Content.ContentDatabase.Get<Weapons.WeaponData>(Weapons.WeaponData.WildcardId);
            if (data == null) return;
            try
            {
                Weapons.Weapon gun = Weapons.Weapon.Create(data, baseRoot, true);
                gun.transform.localPosition = places[0] + Vector3.up * 0.12f;
                gun.transform.localRotation = Quaternion.Euler(0f, (site.Seed & 255) * 1.4f, 90f);
                // Lying on show: no falling through a floor that is not solid yet, and it goes with the base.
                // No interpolation while it rides the turning, origin-shifting base, or it lags a step behind; and no
                // pull of its own, or a re-centre would move it once with the base and again by itself.
                gun.HoldStill();
                w.Rare[index] = gun;
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>
        /// At most one base per world holds a found melee item (Part B section 9: "melee weapons are rare and must
        /// be found"; see <see cref="Melee.MeleeLoot"/>), at its last loot point, never the rare gun's base. It lies
        /// still until taken, and once the player takes it it does not come back this run. Enemies never take it.
        /// </summary>
        void PlaceMelee(World w, int index, Transform baseRoot)
        {
            if (w.MeleeItems == null || w.MeleeBase != index || w.MeleeTaken[index] || string.IsNullOrEmpty(w.MeleeId)) return;
            BaseSite site = w.Sites[index];
            var places = new List<Vector3>();
            BaseView.Places(site, BaseKinds.Loot, places);
            int at = Melee.MeleeLoot.LootPoint(places.Count, w.RareBase == index);
            if (at < 0) return;
            Melee.MeleeData data = Content.ContentDatabase.Get<Melee.MeleeData>(w.MeleeId);
            if (data == null) return;
            try
            {
                Melee.MeleeItem item = Melee.MeleeItem.Create(data, baseRoot);
                item.transform.localPosition = places[at] + Vector3.up * 0.12f;
                item.transform.localRotation = Quaternion.Euler(0f, ((site.Seed >> 8) & 255) * 1.4f, 90f);
                // On show like the rare gun: still, riding with the base, no pull of its own until taken.
                item.HoldStill();
                item.Taken += _ => w.MeleeTaken[index] = true;
                w.MeleeItems[index] = item;
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Poured-concrete grey, a little warm. The models bring their own colours.</summary>
        const string FoundationColour = "#8C8A84";

        void OnDestroy()
        {
            foreach (KeyValuePair<int, PlanetJobs.Handle> search in _searches) search.Value.Cancel();
            _searches.Clear();
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) Destroy(_owned[i]);
            if (_material != null) Destroy(_material);
        }
    }
}
