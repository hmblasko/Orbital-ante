using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// Builds a weapon from its definition: the model prefab if one is set, otherwise a
    /// placeholder made of primitives with the same sockets (Grip, SupportHand, Muzzle, Magazine).
    /// Gun space: +Z forward, +Y up, origin at the grip.
    /// </summary>
    public static class WeaponFactory
    {
        public static WeaponDefinition LoadDefinition(string assetName, System.Func<WeaponDefinition> fallback)
        {
            var d = Resources.Load<WeaponDefinition>("Weapons/" + assetName);
            return d != null ? d : fallback();
        }

        public static WeaponDefinition Rifle => LoadDefinition("RifleAK", WeaponDefinition.DefaultRifle);
        public static WeaponDefinition Pistol => LoadDefinition("TestPistol", WeaponDefinition.DefaultPistol);

        public static Weapon Create(WeaponDefinition def, Vector3 position, Quaternion rotation)
        {
            GameObject root;
            if (def.modelPrefab != null)
            {
                root = Object.Instantiate(def.modelPrefab, position, rotation);
                root.name = def.displayName;
            }
            else
            {
                root = new GameObject(def.displayName);
                root.transform.SetPositionAndRotation(position, rotation);
                if (def.placeholderShape == WeaponShape.Pistol) BuildPistol(root.transform);
                else BuildRifle(root.transform);
            }

            var w = root.AddComponent<Weapon>();
            w.Grip = FindOrCreate(root.transform, "Grip", Vector3.zero);
            w.SupportHand = FindOrCreate(root.transform, "SupportHand", new Vector3(0, 0.03f, 0.3f));
            w.Muzzle = FindOrCreate(root.transform, "Muzzle", new Vector3(0, 0.08f, 0.6f));
            w.Magazine = FindOrCreate(root.transform, "Magazine", new Vector3(0, -0.06f, 0.15f));

            var body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.mass = def.placeholderShape == WeaponShape.Pistol ? 1.1f : 3.6f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            w.Body = body;

            var col = root.GetComponent<Collider>();
            if (col == null)
            {
                var box = root.AddComponent<BoxCollider>();
                FitBox(root.transform, box);
                col = box;
            }
            w.Collider = col;

            // Muzzle flash: a flat bright blob plus a small light, hidden until a shot.
            var flash = PrimitiveMeshes.CreateVisual("Flash", PrimitiveType.Sphere, w.Muzzle, MaterialLibrary.Unlit(def.flashColor),
                Vector3.forward * 0.04f, Quaternion.identity, Vector3.one * def.flashSize);
            flash.transform.localScale = new Vector3(def.flashSize, def.flashSize, def.flashSize * 1.8f);
            flash.SetActive(false);
            w.Flash = flash;
            var light = new GameObject("FlashLight").AddComponent<Light>();
            light.transform.SetParent(w.Muzzle, false);
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 3f;
            light.color = def.flashColor;
            light.shadows = LightShadows.None;
            light.enabled = false;
            w.FlashLight = light;

            w.Init(def);
            return w;
        }

        static Transform FindOrCreate(Transform root, string name, Vector3 fallback)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            var s = new GameObject(name).transform;
            s.SetParent(root, false);
            s.localPosition = fallback;
            return s;
        }

        static void FitBox(Transform root, BoxCollider box)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "Flash") continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            box.center = b.center;
            box.size = Vector3.Max(b.size, Vector3.one * 0.04f);
        }

        static void Part(Transform root, string name, PrimitiveType type, Color c, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            PrimitiveMeshes.CreateVisual(name, type, root, MaterialLibrary.Lit(c, 0.35f), pos, Quaternion.Euler(euler), scale);
        }

        static readonly Color Metal = new Color(0.17f, 0.18f, 0.2f);
        static readonly Color MetalLight = new Color(0.32f, 0.33f, 0.36f);
        static readonly Color Wood = new Color(0.55f, 0.3f, 0.14f);

        static void BuildRifle(Transform r)
        {
            Part(r, "Receiver", PrimitiveType.Cube, Metal, new Vector3(0, 0.06f, 0.1f), new Vector3(0.065f, 0.085f, 0.4f));
            Part(r, "DustCover", PrimitiveType.Cube, MetalLight, new Vector3(0, 0.108f, 0.08f), new Vector3(0.055f, 0.02f, 0.3f));
            Part(r, "Handguard", PrimitiveType.Cube, Wood, new Vector3(0, 0.055f, 0.38f), new Vector3(0.072f, 0.07f, 0.2f));
            Part(r, "Barrel", PrimitiveType.Cylinder, Metal, new Vector3(0, 0.075f, 0.56f), new Vector3(0.026f, 0.17f, 0.026f), new Vector3(90, 0, 0));
            Part(r, "GasTube", PrimitiveType.Cylinder, MetalLight, new Vector3(0, 0.1f, 0.44f), new Vector3(0.022f, 0.1f, 0.022f), new Vector3(90, 0, 0));
            Part(r, "FrontSight", PrimitiveType.Cube, Metal, new Vector3(0, 0.11f, 0.66f), new Vector3(0.012f, 0.05f, 0.02f));
            Part(r, "Stock", PrimitiveType.Cube, Wood, new Vector3(0, 0.035f, -0.22f), new Vector3(0.055f, 0.1f, 0.27f), new Vector3(-6, 0, 0));
            Part(r, "PistolGrip", PrimitiveType.Cube, Wood, new Vector3(0, -0.03f, 0f), new Vector3(0.04f, 0.1f, 0.05f), new Vector3(-18, 0, 0));
            var mag = new GameObject("Magazine").transform;
            mag.SetParent(r, false);
            mag.localPosition = new Vector3(0, 0.0f, 0.17f);
            PrimitiveMeshes.CreateVisual("MagUpper", PrimitiveType.Cube, mag, MaterialLibrary.Lit(new Color(0.6f, 0.32f, 0.12f), 0.3f),
                new Vector3(0, -0.04f, 0f), Quaternion.Euler(12, 0, 0), new Vector3(0.04f, 0.1f, 0.07f));
            PrimitiveMeshes.CreateVisual("MagLower", PrimitiveType.Cube, mag, MaterialLibrary.Lit(new Color(0.6f, 0.32f, 0.12f), 0.3f),
                new Vector3(0, -0.12f, 0.03f), Quaternion.Euler(28, 0, 0), new Vector3(0.04f, 0.09f, 0.068f));
            Socket(r, "Grip", Vector3.zero);
            Socket(r, "SupportHand", new Vector3(0, 0.03f, 0.38f));
            Socket(r, "Muzzle", new Vector3(0, 0.075f, 0.73f));
        }

        static void BuildPistol(Transform r)
        {
            Part(r, "Slide", PrimitiveType.Cube, MetalLight, new Vector3(0, 0.075f, 0.065f), new Vector3(0.034f, 0.042f, 0.19f));
            Part(r, "Frame", PrimitiveType.Cube, Metal, new Vector3(0, 0.042f, 0.055f), new Vector3(0.032f, 0.03f, 0.16f));
            Part(r, "Grip", PrimitiveType.Cube, Metal, new Vector3(0, -0.01f, 0f), new Vector3(0.032f, 0.11f, 0.05f), new Vector3(-12, 0, 0));
            Part(r, "TriggerGuard", PrimitiveType.Cube, Metal, new Vector3(0, 0.015f, 0.05f), new Vector3(0.012f, 0.012f, 0.05f));
            var mag = new GameObject("Magazine").transform;
            mag.SetParent(r, false);
            mag.localPosition = new Vector3(0, -0.045f, -0.004f);
            PrimitiveMeshes.CreateVisual("MagBase", PrimitiveType.Cube, mag, MaterialLibrary.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f),
                new Vector3(0, -0.025f, 0f), Quaternion.Euler(-12, 0, 0), new Vector3(0.03f, 0.02f, 0.052f));
            // Rename the grip mesh so the "Grip" socket below is the empty transform, not this mesh.
            r.Find("Grip").name = "GripMesh";
            Socket(r, "Grip", Vector3.zero);
            Socket(r, "SupportHand", new Vector3(0, -0.02f, -0.015f));
            Socket(r, "Muzzle", new Vector3(0, 0.075f, 0.165f));
        }

        static void Socket(Transform r, string name, Vector3 pos)
        {
            var s = new GameObject(name).transform;
            s.SetParent(r, false);
            s.localPosition = pos;
        }
    }
}
