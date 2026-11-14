using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Swaps a library model between its full copy and its far copy as the player's view comes and goes
    /// (<see cref="DetailRule"/>). Put on the root of the thing (an enemy, a gun) by <see cref="Attach"/>, which
    /// finds every piece under it that is drawn with one of the model's meshes. The meshes are the library's
    /// shared ones: nothing is created, copied or destroyed here, only which mesh a piece points at.
    ///
    /// All of them are looked at together a few times a second by one hidden runner, never one by one in
    /// their own Update, so the count of full bodies can be kept and the cost does not grow per enemy.
    ///
    /// A piece that leaves its root (a limb shot off, a body blown apart) goes on being watched by itself,
    /// from where it lies, and takes no body's place; a body with nothing left under it stops being counted.
    /// A gun in somebody's hands is never drawn finer than the body holding it.
    /// </summary>
    public sealed class ModelDetail : MonoBehaviour
    {
        /// <summary>How often everything is looked at again, in seconds.</summary>
        public const float Every = 0.2f;

        /// <summary>Set to look from somewhere other than the camera that draws to the screen (tests). Usually null.</summary>
        public static Transform Viewer;

        /// <summary>For the F3 readout, as of the last look: bodies watched and how many of them are far copies.</summary>
        public static int Bodies { get; private set; }
        public static int BodiesFar { get; private set; }
        /// <summary>Guns and anything else watched but not counted, and how many of them are far copies.</summary>
        public static int Others { get; private set; }
        public static int OthersFar { get; private set; }
        /// <summary>Pieces lying loose, and how many of them are far copies.</summary>
        public static int LoosePieces => _looseCount;
        public static int LooseFar { get; private set; }
        /// <summary>Metres to the nearest body wearing its far copy, or -1. Under the body range it means the limit on full bodies is biting.</summary>
        public static float NearestFarBody { get; private set; } = -1f;
        /// <summary>How many things (not loose pieces) are being watched.</summary>
        public static int Watched => Live.Count;
        /// <summary>How many bodies are drawn in full, and the most that may be (the Look asset's dial).</summary>
        public static int FullBodiesShown => Bodies - BodiesFar;
        public static int FullBodiesAllowed { get; private set; } = DetailRule.FullBodies;

        /// <summary>One line for the debug readout (F3). Makes a string: only call it while the readout shows.</summary>
        public static string Describe()
        {
            if (FullBodiesAllowed == int.MaxValue) return "Far copies: switched off on the Look asset (everything is drawn in full)";
            return "Far copies: bodies " + BodiesFar + " of " + Bodies + " (full " + FullBodiesShown + "/" + FullBodiesAllowed +
                   (NearestFarBody >= 0f ? ", nearest far one " + NearestFarBody.ToString("0") + " m" : string.Empty) +
                   ")   guns " + OthersFar + " of " + Others + "   loose pieces " + LooseFar + " of " + _looseCount;
        }

        struct Loose
        {
            public MeshFilter Filter;
            public Mesh Full, Thin;
            public float Range;
            public bool Far;
        }

        static readonly List<ModelDetail> Live = new List<ModelDetail>();
        // Room for 30 enemies, their guns and the guns lying about without ever being re-made.
        static float[] _distance = new float[128], _range = new float[128], _scratch = new float[128];
        static bool[] _counted = new bool[128], _unseen = new bool[128], _far = new bool[128];
        static Loose[] _loose = new Loose[64];
        static int _looseCount;
        static Camera[] _cameras = new Camera[8];
        static Runner _runner;
        static bool _lookNow;       // something new is being watched: look this frame, not in a fifth of a second
        static Mesh[] _shown = new Mesh[64];

        MeshFilter[] _filters;
        Mesh[] _full, _thin;
        float _rangeMetres;
        bool _countedBody;
        int _held = -1;     // pieces still under the root; -1 until Attach has filled it in

        /// <summary>True while the far copy is showing.</summary>
        public bool Far { get; private set; }

        public static void ResetStatics()
        {
            Live.Clear();
            System.Array.Clear(_loose, 0, _loose.Length);
            _looseCount = 0;
            Viewer = null;
            Bodies = BodiesFar = Others = OthersFar = LooseFar = 0;
            NearestFarBody = -1f;
            FullBodiesAllowed = DetailRule.FullBodies;
            _runner = null;
            _lookNow = false;
        }

        /// <summary>
        /// Watches the model <paramref name="model"/> under <paramref name="root"/>. <paramref name="range"/> is the
        /// distance at which a thing of ordinary size changes (it is stretched by how large the root is drawn).
        /// <paramref name="countedBody"/>: one of the bodies of which only <see cref="DetailRule.FullBodies"/>
        /// are full at once. Returns null, and changes nothing, if the model has no far copy.
        /// </summary>
        public static ModelDetail Attach(GameObject root, string model, float range, bool countedBody)
        {
            if (root == null || string.IsNullOrEmpty(model)) return null;
            ModelFile full = ModelLibrary.Find(model);
            ModelFile thin = ModelLibrary.Find(ModelFile.FarOf(model));
            if (full == null || thin == null) return null;

            var filters = new List<MeshFilter>();
            var fullMeshes = new List<Mesh>();
            var thinMeshes = new List<Mesh>();
            MeshFilter[] found = root.GetComponentsInChildren<MeshFilter>(true);
            // Ask each piece what it is wearing once, not once for every piece of the model.
            if (_shown.Length < found.Length) _shown = new Mesh[Mathf.NextPowerOfTwo(found.Length)];
            for (int i = 0; i < found.Length; i++) _shown[i] = found[i] != null ? found[i].sharedMesh : null;
            for (int p = 0; p < full.Parts.Length; p++)
            {
                string piece = full.Parts[p].Name;
                // What shines is the same in both copies, and the game switches it by name: leave it be.
                if (ModelFile.Shines(piece)) continue;
                Mesh a = ModelLibrary.PartMesh(model, piece), b = ModelLibrary.PartMesh(ModelFile.FarOf(model), piece);
                if (a == null || b == null) continue;
                for (int i = 0; i < found.Length; i++)
                {
                    Mesh shown = _shown[i];
                    if (ReferenceEquals(shown, null) || (shown != a && shown != b)) continue;
                    // Start from the full copy whatever it wore, so everything under the root agrees.
                    if (shown == b) found[i].sharedMesh = a;
                    filters.Add(found[i]);
                    fullMeshes.Add(a);
                    thinMeshes.Add(b);
                }
            }
            System.Array.Clear(_shown, 0, found.Length);
            if (filters.Count == 0) return null;

            ModelDetail detail = root.GetComponent<ModelDetail>();
            if (detail == null) detail = root.AddComponent<ModelDetail>();
            detail._filters = filters.ToArray();
            detail._full = fullMeshes.ToArray();
            detail._thin = thinMeshes.ToArray();
            detail._rangeMetres = range;
            detail._countedBody = countedBody;
            detail._held = detail._filters.Length;
            detail.Far = false;
            if (detail.isActiveAndEnabled && !Live.Contains(detail)) Live.Add(detail);
            // Everything starts full. A camp wakes far away, so do not leave it full until the next look comes round.
            _lookNow = true;
            return detail;
        }

        void OnEnable()
        {
            // A body with nothing left under it has nothing to decide, and must not take a living body's place.
            if (_held == 0) return;
            if (!Live.Contains(this)) Live.Add(this);
            _lookNow = true;
            if (_runner == null)
            {
                var go = new GameObject("ModelDetail runner");
                go.hideFlags = HideFlags.HideInHierarchy;
                _runner = go.AddComponent<Runner>();
            }
        }

        void OnDisable()
        {
            Live.Remove(this);
        }

        /// <summary>
        /// Hands every piece that is no longer under the root to the loose list, wearing what it wore, and
        /// forgets pieces that no longer exist. Returns how many are still held.
        /// </summary>
        int LetGoOfWhatLeft()
        {
            if (_filters == null) return 0;
            Transform root = transform;
            float scale = Mathf.Max(0.1f, root.lossyScale.y);
            int held = 0;
            for (int i = 0; i < _filters.Length; i++)
            {
                MeshFilter filter = _filters[i];
                if (ReferenceEquals(filter, null)) continue;
                if (filter == null) { _filters[i] = null; continue; }
                if (filter.transform.IsChildOf(root)) { held++; continue; }
                if (_looseCount == _loose.Length) System.Array.Resize(ref _loose, _loose.Length * 2);
                _loose[_looseCount++] = new Loose { Filter = filter, Full = _full[i], Thin = _thin[i], Range = _rangeMetres * scale, Far = Far };
                _filters[i] = null;
            }
            _held = held;
            return held;
        }

        void Show(bool far)
        {
            if (Far == far || _filters == null) return;
            Far = far;
            for (int i = 0; i < _filters.Length; i++)
            {
                MeshFilter filter = _filters[i];
                if (filter != null) filter.sharedMesh = far ? _thin[i] : _full[i];
            }
        }

        /// <summary>Looks at every watched thing once. Called by the runner; public so a test can call it.</summary>
        public static void LookAtAll()
        {
            Transform eye = Viewer != null ? Viewer : ScreenCamera();
            if (eye == null) return;
            Vector3 from = eye.position, ahead = eye.forward;
            // The dials (read every look, so they can be turned while the game runs in the editor).
            LookConfig look = LookConfig.Load();
            float reach = look.farCopies ? Mathf.Max(0.1f, look.farCopyDistance) : 0f;   // a range of 0 means always full
            FullBodiesAllowed = look.farCopies ? Mathf.Max(0, look.fullBodies) : int.MaxValue;

            // First, what has come off: it must not be swapped with the body it left, and an emptied body leaves the list.
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                ModelDetail d = Live[i];
                if (d == null) { Live.RemoveAt(i); continue; }
                if (d.LetGoOfWhatLeft() == 0) Live.RemoveAt(i);
            }

            int count = Live.Count;
            if (_distance.Length < count)
            {
                int room = Mathf.NextPowerOfTwo(count);
                _distance = new float[room]; _range = new float[room]; _scratch = new float[room];
                _counted = new bool[room]; _unseen = new bool[room]; _far = new bool[room];
            }
            for (int i = 0; i < count; i++)
            {
                ModelDetail d = Live[i];
                Transform t = d.transform;
                Vector3 to = t.position - from;
                _distance[i] = to.magnitude;
                // A body drawn half as big again can be told from its far copy half as far again.
                _range[i] = d._rangeMetres * Mathf.Max(0.1f, t.lossyScale.y) * reach;
                _counted[i] = d._countedBody;
                _unseen[i] = d._countedBody && DetailRule.Unseen(Vector3.Dot(to, ahead), _distance[i]);
                _far[i] = d.Far;
            }
            DetailRule.Pick(_distance, _range, _counted, _unseen, _far, _scratch, count, FullBodiesAllowed);

            int bodies = 0, bodiesFar = 0, others = 0, othersFar = 0;
            float nearestFar = -1f;
            for (int i = 0; i < count; i++)
            {
                if (!_counted[i]) continue;
                Live[i].Show(_far[i]);
                bodies++;
                if (!_far[i]) continue;
                bodiesFar++;
                if (nearestFar < 0f || _distance[i] < nearestFar) nearestFar = _distance[i];
            }
            for (int i = 0; i < count; i++)
            {
                if (_counted[i]) continue;
                ModelDetail d = Live[i];
                bool far = _far[i];
                if (!far)
                {
                    // In somebody's hands: no finer than the body that holds it (already decided above).
                    Transform parent = d.transform.parent;
                    ModelDetail holder = parent != null ? parent.GetComponentInParent<ModelDetail>() : null;
                    if (holder != null && holder._countedBody && holder._held > 0 && holder.Far) far = true;
                }
                d.Show(far);
                others++;
                if (far) othersFar++;
            }

            int looseFar = 0;
            for (int i = _looseCount - 1; i >= 0; i--)
            {
                MeshFilter filter = _loose[i].Filter;
                if (filter == null)
                {
                    // Cleaned up with its chunk: close the gap with the last one.
                    _loose[i] = _loose[--_looseCount];
                    _loose[_looseCount] = default;
                    continue;
                }
                bool far = DetailRule.Far(Vector3.Distance(from, filter.transform.position), _loose[i].Range * reach, _loose[i].Far);
                if (far != _loose[i].Far)
                {
                    _loose[i].Far = far;
                    filter.sharedMesh = far ? _loose[i].Thin : _loose[i].Full;
                }
                if (far) looseFar++;
            }

            Bodies = bodies; BodiesFar = bodiesFar; Others = others; OthersFar = othersFar;
            LooseFar = looseFar;
            NearestFarBody = nearestFar;
        }

        /// <summary>The camera the player is looking through: the one drawing to the screen, the topmost if there are several.</summary>
        static Transform ScreenCamera()
        {
            if (Camera.allCamerasCount > _cameras.Length) _cameras = new Camera[Camera.allCamerasCount * 2];
            int n = Camera.GetAllCameras(_cameras);
            Camera best = null;
            for (int i = 0; i < n; i++)
            {
                Camera c = _cameras[i];
                _cameras[i] = null;
                if (c == null || c.targetTexture != null) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best != null ? best.transform : null;
        }

        /// <summary>One for the whole scene: looks at everything on a timer that does not slow with the world.</summary>
        sealed class Runner : MonoBehaviour
        {
            float _next;

            void LateUpdate()
            {
                _next -= Time.unscaledDeltaTime;
                if (_next > 0f && !_lookNow) return;
                _next = Every;
                _lookNow = false;
                if (Live.Count > 0 || _looseCount > 0) LookAtAll();
            }
        }
    }
}
