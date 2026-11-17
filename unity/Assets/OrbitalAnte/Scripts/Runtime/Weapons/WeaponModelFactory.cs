using OrbitalAnte.Core;
using OrbitalAnte.Look;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    /// <summary>The four attachment points every gun has, whatever it looks like.</summary>
    public struct WeaponSockets
    {
        /// <summary>Where the firing hand holds it. This is the weapon's origin.</summary>
        public Transform Grip;
        /// <summary>Where the other hand goes on a two-handed gun.</summary>
        public Transform SupportHand;
        /// <summary>Where bullets, tracers and the muzzle flash come from. Blue (Z) points out of the barrel.</summary>
        public Transform Muzzle;
        /// <summary>Where the magazine sits.</summary>
        public Transform Magazine;
        /// <summary>The visible magazine, if the model has a separate one. Hidden for a moment during a reload.</summary>
        public GameObject MagazineMesh;
        /// <summary>Box around the whole model, in the weapon's own space.</summary>
        public Bounds LocalBounds;
    }

    /// <summary>
    /// Builds what a gun looks like. If the weapon data has a model assigned (a CC0 model from
    /// Assets/ThirdParty/Weapons), that is used. Otherwise the game's own Blender-made model is used, and if that is missing too, a placeholder built from primitives. Either way the
    /// result has the same four sockets, so nothing else in the game cares which one it got.
    ///
    /// A gun's own space: origin at the grip, barrel along +Z, top along +Y.
    /// </summary>
    public static class WeaponModelFactory
    {
        public static WeaponSockets Build(WeaponData data, Transform weaponRoot)
        {
            WeaponSockets sockets = default;
            bool built = false;
            if (data.modelPrefab != null)
            {
                try
                {
                    sockets = BuildFromPrefab(data, weaponRoot);
                    built = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Weapon '" + data.displayName + "': the assigned model could not be used (" + e.Message +
                                     "). Using the placeholder shape instead.");
                }
            }
            if (!built)
            {
                built = BuildFromModelFile(data, weaponRoot, out sockets);
            }
            if (!built && !string.IsNullOrEmpty(data.modelFile))
            {
                // The gun names a model of its own that has not been made yet: borrow the one for its shape,
                // so it still looks like a gun and not a stack of boxes.
                string named = data.modelFile;
                data.modelFile = string.Empty;
                try { built = BuildFromModelFile(data, weaponRoot, out sockets); }
                finally { data.modelFile = named; }
            }
            if (!built)
            {
                sockets = data.placeholderShape == WeaponShape.Pistol ? BuildPistol(weaponRoot) : BuildRifle(weaponRoot);
            }
            sockets.LocalBounds = MeasureLocalBounds(weaponRoot);
            return sockets;
        }

        // ------------------------------------------------------------------ the game's own models

        /// <summary>The model file a gun uses: the one named on its data, or the one for its shape.</summary>
        public static string ModelIdFor(WeaponData data)
        {
            if (!string.IsNullOrEmpty(data.modelFile)) return data.modelFile;
            return data.placeholderShape == WeaponShape.Pistol ? "Pistol" : "Rifle";
        }

        /// <summary>
        /// A gun made in Blender (tools/blender/build_weapons.py), drawn with the game's shared look. The model
        /// carries its own attachment points. Its "Magazine" piece is separate so a reload can take it away.
        /// </summary>
        static bool BuildFromModelFile(WeaponData data, Transform root, out WeaponSockets sockets)
        {
            sockets = default;
            string id = ModelIdFor(data);
            ModelFile file = ModelLibrary.Find(id);
            if (file == null) return false;
            if (!file.TrySocket("Muzzle", out ModelSocket muzzle) || !file.TrySocket("SupportHand", out ModelSocket support) ||
                !file.TrySocket("Magazine", out ModelSocket magazine))
            {
                Debug.LogWarning("Weapon '" + data.displayName + "': the model '" + id + "' is missing an attachment point. Using the placeholder shape instead.");
                return false;
            }

            Transform model = NewChild("Model", root, Vector3.zero);
            var pieces = new System.Collections.Generic.Dictionary<string, GameObject>();
            if (!ModelLibrary.Build(id, model, pieces)) return false;
            // In an enemy's hands or lying on the ground far off, the gun is drawn with its far copy.
            ModelDetail.Attach(root.gameObject, id, DetailRule.GunRange, false);

            Vector3 magazineAt = new Vector3(magazine.X, magazine.Y, magazine.Z);
            Transform magazineSocket = NewChild("Magazine", root, magazineAt);
            GameObject magazineMesh = null;
            if (pieces.TryGetValue("Magazine", out GameObject piece))
            {
                // The piece's corners are in the gun's space, so it is shifted back by where its socket sits.
                piece.name = "MagazineMesh";
                piece.transform.SetParent(magazineSocket, false);
                piece.transform.localPosition = -magazineAt;
                magazineMesh = piece;
            }

            sockets = new WeaponSockets
            {
                Grip = NewChild("Grip", root, Vector3.zero),
                SupportHand = NewChild("SupportHand", root, new Vector3(support.X, support.Y, support.Z)),
                Muzzle = NewChild("Muzzle", root, new Vector3(muzzle.X, muzzle.Y, muzzle.Z)),
                Magazine = magazineSocket,
                MagazineMesh = magazineMesh,
            };
            return true;
        }

        // ------------------------------------------------------------------ placeholders

        static WeaponSockets BuildRifle(Transform root)
        {
            Material metal = Mat.Lit(new Color(0.16f, 0.17f, 0.20f), 0.5f);
            Material wood = Mat.Lit(new Color(0.58f, 0.33f, 0.15f), 0.3f);
            Transform model = NewChild("Model", root, Vector3.zero);

            // Receiver, barrel and gas tube.
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.045f, 0.10f), new Vector3(0.05f, 0.07f, 0.36f), Quaternion.identity, metal);
            Block(model, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0.50f), new Vector3(0.022f, 0.16f, 0.022f), Quaternion.Euler(90f, 0f, 0f), metal);
            Block(model, PrimitiveType.Cylinder, new Vector3(0f, 0.072f, 0.40f), new Vector3(0.018f, 0.09f, 0.018f), Quaternion.Euler(90f, 0f, 0f), metal);
            // Front sight post and muzzle brake.
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.085f, 0.60f), new Vector3(0.012f, 0.05f, 0.016f), Quaternion.identity, metal);
            Block(model, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0.665f), new Vector3(0.030f, 0.022f, 0.030f), Quaternion.Euler(90f, 0f, 0f), metal);
            // Wooden handguard, pistol grip and stock.
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.37f), new Vector3(0.056f, 0.06f, 0.17f), Quaternion.identity, wood);
            Block(model, PrimitiveType.Cube, new Vector3(0f, -0.045f, -0.01f), new Vector3(0.04f, 0.11f, 0.05f), Quaternion.Euler(18f, 0f, 0f), wood);
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.025f, -0.20f), new Vector3(0.042f, 0.085f, 0.26f), Quaternion.Euler(-6f, 0f, 0f), wood);

            // The curved magazine, as two angled pieces, so it reads as the famous banana shape.
            Transform magazine = NewChild("Magazine", root, new Vector3(0f, 0.01f, 0.15f));
            var magMesh = NewChild("MagazineMesh", magazine, Vector3.zero);
            Block(magMesh, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.01f), new Vector3(0.034f, 0.11f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), metal);
            Block(magMesh, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0.045f), new Vector3(0.034f, 0.10f, 0.058f), Quaternion.Euler(-32f, 0f, 0f), metal);

            return new WeaponSockets
            {
                Grip = NewChild("Grip", root, Vector3.zero),
                SupportHand = NewChild("SupportHand", root, new Vector3(0f, 0.01f, 0.37f)),
                Muzzle = NewChild("Muzzle", root, new Vector3(0f, 0.045f, 0.69f)),
                Magazine = magazine,
                MagazineMesh = magMesh.gameObject,
            };
        }

        static WeaponSockets BuildPistol(Transform root)
        {
            Material metal = Mat.Lit(new Color(0.22f, 0.23f, 0.27f), 0.6f);
            Material grip = Mat.Lit(new Color(0.12f, 0.12f, 0.13f), 0.25f);
            Transform model = NewChild("Model", root, Vector3.zero);

            // Slide, frame, trigger guard and grip.
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.062f, 0.055f), new Vector3(0.030f, 0.036f, 0.19f), Quaternion.identity, metal);
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.036f, 0.07f), new Vector3(0.026f, 0.02f, 0.13f), Quaternion.identity, grip);
            Block(model, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.055f), new Vector3(0.012f, 0.03f, 0.05f), Quaternion.identity, grip);
            Block(model, PrimitiveType.Cube, new Vector3(0f, -0.022f, -0.012f), new Vector3(0.030f, 0.105f, 0.046f), Quaternion.Euler(14f, 0f, 0f), grip);

            Transform magazine = NewChild("Magazine", root, new Vector3(0f, -0.07f, -0.022f));
            var magMesh = NewChild("MagazineMesh", magazine, Vector3.zero);
            Block(magMesh, PrimitiveType.Cube, new Vector3(0f, -0.008f, 0f), new Vector3(0.024f, 0.02f, 0.04f), Quaternion.Euler(14f, 0f, 0f), metal);

            return new WeaponSockets
            {
                Grip = NewChild("Grip", root, Vector3.zero),
                SupportHand = NewChild("SupportHand", root, new Vector3(0f, -0.03f, 0.0f)),
                Muzzle = NewChild("Muzzle", root, new Vector3(0f, 0.062f, 0.155f)),
                Magazine = magazine,
                MagazineMesh = magMesh.gameObject,
            };
        }

        // ------------------------------------------------------------------ imported models

        static WeaponSockets BuildFromPrefab(WeaponData data, Transform root)
        {
            Transform model = NewChild("Model", root, Vector3.zero);
            GameObject instance = Object.Instantiate(data.modelPrefab, model, false);
            instance.name = data.modelPrefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(data.modelRotation);

            // The game adds its own collider and physics; anything the model came with would fight them.
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);

            Bounds bounds = MeasureLocalBounds(root);
            if (bounds.size.z < 0.0001f) throw new System.InvalidOperationException("the model has no visible mesh");

            if (data.modelLength > 0.01f)
            {
                float scale = data.modelLength / bounds.size.z;
                model.localScale = Vector3.one * scale;
                bounds = MeasureLocalBounds(root);
            }

            bool pistol = data.placeholderShape == WeaponShape.Pistol;
            Vector3 size = bounds.size;
            Vector3 min = bounds.min;

            // Guess where the hand goes, then slide the model so that point is the weapon's origin.
            Vector3 gripGuess = new Vector3(bounds.center.x, min.y + size.y * (pistol ? 0.40f : 0.38f), min.z + size.z * (pistol ? 0.18f : 0.32f));
            Vector3 grip = data.useCustomSockets ? bounds.center + data.gripSocket : gripGuess;
            model.localPosition -= grip;
            bounds.center -= grip;
            min = bounds.min;

            Vector3 muzzle, support, magazine;
            if (data.useCustomSockets)
            {
                muzzle = bounds.center + data.muzzleSocket;
                support = bounds.center + data.supportHandSocket;
                magazine = bounds.center + data.magazineSocket;
            }
            else
            {
                muzzle = new Vector3(bounds.center.x, min.y + size.y * (pistol ? 0.82f : 0.68f), bounds.max.z);
                support = pistol
                    ? new Vector3(bounds.center.x, -0.03f, 0f)
                    : new Vector3(bounds.center.x, min.y + size.y * 0.5f, min.z + size.z * 0.66f);
                magazine = new Vector3(bounds.center.x, min.y + size.y * 0.25f, min.z + size.z * (pistol ? 0.16f : 0.45f));
            }

            // A model that names its own attachment points wins over any guess.
            Transform namedMuzzle = FindNamed(instance.transform, "muzzle");
            Transform namedSupport = FindNamed(instance.transform, "support");
            Transform namedMagazine = FindNamed(instance.transform, "magazine");
            if (namedMuzzle != null) muzzle = root.InverseTransformPoint(namedMuzzle.position);
            if (namedSupport != null) support = root.InverseTransformPoint(namedSupport.position);
            if (namedMagazine != null) magazine = root.InverseTransformPoint(namedMagazine.position);

            return new WeaponSockets
            {
                Grip = NewChild("Grip", root, Vector3.zero),
                SupportHand = NewChild("SupportHand", root, support),
                Muzzle = NewChild("Muzzle", root, muzzle),
                Magazine = NewChild("Magazine", root, magazine),
                // Only a separate renderer can be hidden without hiding the whole gun.
                MagazineMesh = namedMagazine != null && namedMagazine.GetComponentInChildren<Renderer>() != null
                               && namedMagazine != instance.transform ? namedMagazine.gameObject : null,
            };
        }

        static Transform FindNamed(Transform root, string fragment)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (t.name.ToLowerInvariant().Contains(fragment)) return t;
            }
            return null;
        }

        // ------------------------------------------------------------------ helpers

        static Transform NewChild(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static void Block(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "Mesh";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>The box around every visible mesh under a weapon, measured in the weapon's own space.</summary>
        public static Bounds MeasureLocalBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Grow(ref bounds, ref any, toRoot * filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
            }
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) continue;
                Grow(ref bounds, ref any, toRoot * skinned.transform.localToWorldMatrix, skinned.sharedMesh.bounds);
            }
            return bounds;
        }

        static void Grow(ref Bounds bounds, ref bool any, Matrix4x4 toRoot, Bounds meshBounds)
        {
            Vector3 c = meshBounds.center;
            Vector3 e = meshBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 p = toRoot.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }
    }
}
