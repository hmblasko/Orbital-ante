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
        }

        SolarSystemWorld _world;
        readonly Dictionary<int, World> _worlds = new Dictionary<int, World>();
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

            Vec3d pad = Vec3d.Zero;
            if (_world.Base != null && _world.Base.Surface == surface) pad = _world.Base.Spot.Up;
            var random = GameSession.Stream("bases." + surface.Body.Spec.id);
            w = new World { Sites = BaseField.Build(Rules, surface.Terrain, random, Kinds, pad) };
            w.Built = new GameObject[w.Sites.Count];
            w.Foundations = new Mesh[w.Sites.Count];
            // Nothing natural grows through a base's floor.
            if (surface.Features != null)
            {
                for (int i = 0; i < w.Sites.Count; i++) surface.Features.AlsoKeepClear(w.Sites[i].Centre, w.Sites[i].Footprint + 2.0);
            }
            _worlds[body] = w;
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
                for (int i = 0; i < w.Built.Length; i++) PutAway(w, i);
            }
            if (here == null) return;

            Vector3 local = surface.Body.transform.InverseTransformPoint(focus.transform.position);
            bool builtOne = false;
            for (int i = 0; i < here.Sites.Count; i++)
            {
                float distance = (SpaceConvert.ToVector3(here.Sites[i].Centre) - local).magnitude;
                if (here.Built[i] != null)
                {
                    if (distance > HideBeyond) PutAway(here, i);
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

        void PutAway(World w, int i)
        {
            if (w.Built[i] == null) return;
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
            return go;
        }

        /// <summary>Poured-concrete grey, a little warm. The models bring their own colours.</summary>
        const string FoundationColour = "#8C8A84";

        void OnDestroy()
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) Destroy(_owned[i]);
            if (_material != null) Destroy(_material);
        }
    }
}
