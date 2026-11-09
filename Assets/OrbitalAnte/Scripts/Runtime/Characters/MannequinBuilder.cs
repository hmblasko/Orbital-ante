using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// Builds the placeholder humanoid from primitives: a Unity Humanoid skeleton (so real
    /// animation clips can drive it later) with one mesh and one hitbox per body part
    /// parented to the matching bone.
    /// </summary>
    public static class MannequinBuilder
    {
        // Rest-pose layout in metres. Bones all have identity rotation in the T-pose, which keeps
        // the procedural gait maths simple.
        const float HipHeight = 0.92f;
        const float SpineUp = 0.10f, ChestUp = 0.22f, NeckUp = 0.28f, HeadUp = 0.08f;
        const float HeadRadius = 0.13f, HeadCentreUp = 0.11f;
        const float ShoulderOut = 0.21f, ShoulderUp = 0.22f;
        const float UpperArmLen = 0.29f, ForearmLen = 0.26f, HandLen = 0.15f;
        const float HipOut = 0.10f, HipDown = 0.03f;
        const float ThighLen = 0.42f, ShinLen = 0.40f;

        static Avatar cachedAvatar;

        public static Mannequin Build(MannequinConfig cfg, Vector3 position, float yawDegrees, Color bodyColor, Transform parent = null)
        {
            var root = new GameObject("Mannequin");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yawDegrees, 0));

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;

            var rig = BuildSkeleton(root.transform);
            BuildAvatar(root, rig);

            // Kinematic body on the hips so the moving hitboxes are cheap for physics to track.
            var hipsBody = rig.Hips.gameObject.AddComponent<Rigidbody>();
            hipsBody.isKinematic = true;
            hipsBody.useGravity = false;

            var mannequin = root.AddComponent<Mannequin>();
            mannequin.Config = cfg;
            mannequin.Rig = rig;

            var body = MaterialLibrary.Lit(bodyColor);
            var joint = MaterialLibrary.Lit(cfg.jointColor);
            var stump = MaterialLibrary.Lit(cfg.stumpColor, 0.6f);

            var parts = new Dictionary<BodyPartId, BodyPart>();
            void Add(BodyPart p) { parts[p.Id] = p; mannequin.Register(p); }

            // Torso spans hips to neck, hangs off the spine bone.
            Add(CapsulePart(BodyPartId.Torso, rig.Spine, new Vector3(0, -0.12f, 0), new Vector3(0, ChestUp + NeckUp, 0),
                0.2f, new Vector2(0.42f, 0.26f), body, cfg));
            Add(HeadPart(rig.Head, body, joint, cfg));

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -1f : 1f; // character's left is -X
                bool left = side == 0;
                Add(CapsulePart(left ? BodyPartId.UpperArmL : BodyPartId.UpperArmR, rig.UpperArm[side], Vector3.zero,
                    new Vector3(x * UpperArmLen, 0, 0), 0.058f, null, body, cfg));
                Add(CapsulePart(left ? BodyPartId.ForearmL : BodyPartId.ForearmR, rig.Forearm[side], Vector3.zero,
                    new Vector3(x * ForearmLen, 0, 0), 0.05f, null, body, cfg));
                Add(CapsulePart(left ? BodyPartId.HandL : BodyPartId.HandR, rig.Hand[side], Vector3.zero,
                    new Vector3(x * HandLen, 0, 0), 0.047f, new Vector2(0.11f, 0.06f), joint, cfg));
                Add(CapsulePart(left ? BodyPartId.ThighL : BodyPartId.ThighR, rig.Thigh[side], Vector3.zero,
                    new Vector3(0, -ThighLen, 0), 0.078f, null, body, cfg));
                Add(CapsulePart(left ? BodyPartId.ShinL : BodyPartId.ShinR, rig.Shin[side], Vector3.zero,
                    new Vector3(0, -ShinLen, 0), 0.062f, null, body, cfg));
                Add(CapsulePart(left ? BodyPartId.FootL : BodyPartId.FootR, rig.Foot[side], new Vector3(0, -0.02f, -0.03f),
                    new Vector3(0, -0.06f, 0.17f), 0.052f, new Vector2(0.11f, 0.09f), joint, cfg));
            }

            // Wire the part tree and the stump caps.
            foreach (var p in parts.Values)
            {
                var parentId = MannequinAnatomy.Parent(p.Id);
                if (parentId == null) continue;
                var parentPart = parts[parentId.Value];
                p.Parent = parentPart;
                parentPart.Children.Add(p);

                Vector3 jointWorld = p.transform.TransformPoint(p.ProximalLocal);
                p.StumpCapOnParent = Cap("StumpCap_" + p.Id, parentPart.transform, jointWorld, p.transform.rotation, p.Radius, stump);
                p.ProximalCap = Cap("Cap", p.transform, jointWorld, p.transform.rotation, p.Radius, stump);
            }

            root.AddComponent<ProceduralGait>();
            root.AddComponent<MannequinBrain>();
            return mannequin;
        }

        static MannequinRig BuildSkeleton(Transform root)
        {
            Transform Bone(string name, Transform parent, Vector3 local)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = local;
                t.localRotation = Quaternion.identity;
                return t;
            }

            var rig = new MannequinRig();
            rig.Hips = Bone("Hips", root, new Vector3(0, HipHeight, 0));
            rig.Spine = Bone("Spine", rig.Hips, new Vector3(0, SpineUp, 0));
            rig.Chest = Bone("Chest", rig.Spine, new Vector3(0, ChestUp, 0));
            rig.Neck = Bone("Neck", rig.Chest, new Vector3(0, NeckUp, 0));
            rig.Head = Bone("HeadBone", rig.Neck, new Vector3(0, HeadUp, 0));
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -1f : 1f;
                string s = side == 0 ? "_L" : "_R";
                rig.UpperArm[side] = Bone("UpperArm" + s, rig.Chest, new Vector3(x * ShoulderOut, ShoulderUp, 0));
                rig.Forearm[side] = Bone("Forearm" + s, rig.UpperArm[side], new Vector3(x * UpperArmLen, 0, 0));
                rig.Hand[side] = Bone("Hand" + s, rig.Forearm[side], new Vector3(x * ForearmLen, 0, 0));
                rig.Thigh[side] = Bone("Thigh" + s, rig.Hips, new Vector3(x * HipOut, -HipDown, 0));
                rig.Shin[side] = Bone("Shin" + s, rig.Thigh[side], new Vector3(0, -ThighLen, 0));
                rig.Foot[side] = Bone("Foot" + s, rig.Shin[side], new Vector3(0, -ShinLen, 0));
            }
            rig.HipsRestLocal = rig.Hips.localPosition;
            rig.HeadCentreAboveHips = SpineUp + ChestUp + NeckUp + HeadUp + HeadCentreUp;
            return rig;
        }

        static void BuildAvatar(GameObject root, MannequinRig rig)
        {
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            if (cachedAvatar == null)
            {
                var map = new (string human, Transform bone)[]
                {
                    ("Hips", rig.Hips), ("Spine", rig.Spine), ("Chest", rig.Chest), ("Neck", rig.Neck), ("Head", rig.Head),
                    ("LeftUpperArm", rig.UpperArm[0]), ("LeftLowerArm", rig.Forearm[0]), ("LeftHand", rig.Hand[0]),
                    ("RightUpperArm", rig.UpperArm[1]), ("RightLowerArm", rig.Forearm[1]), ("RightHand", rig.Hand[1]),
                    ("LeftUpperLeg", rig.Thigh[0]), ("LeftLowerLeg", rig.Shin[0]), ("LeftFoot", rig.Foot[0]),
                    ("RightUpperLeg", rig.Thigh[1]), ("RightLowerLeg", rig.Shin[1]), ("RightFoot", rig.Foot[1]),
                };
                var human = new HumanBone[map.Length];
                for (int i = 0; i < map.Length; i++)
                {
                    var limit = new HumanLimit { useDefaultValues = true };
                    human[i] = new HumanBone { humanName = map[i].human, boneName = map[i].bone.name, limit = limit };
                }
                var transforms = root.GetComponentsInChildren<Transform>();
                var skeleton = new SkeletonBone[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    skeleton[i] = new SkeletonBone
                    {
                        name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                    };
                }
                var desc = new HumanDescription
                {
                    human = human, skeleton = skeleton,
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
                };
                cachedAvatar = AvatarBuilder.BuildHumanAvatar(root, desc);
                cachedAvatar.name = "MannequinAvatar";
                if (!cachedAvatar.isValid || !cachedAvatar.isHuman)
                    Debug.LogWarning("[OA] Mannequin Humanoid avatar is not valid; procedural gait still works.");
            }
            animator.avatar = cachedAvatar;
        }

        /// <summary>A capsule part from a to b (bone-local), with a trigger hitbox and a visual child.</summary>
        static BodyPart CapsulePart(BodyPartId id, Transform bone, Vector3 a, Vector3 b, float radius, Vector2? crossSection,
            Material mat, MannequinConfig cfg)
        {
            Vector3 seg = b - a;
            float len = seg.magnitude;
            float height = Mathf.Max(len + radius * 0.6f, radius * 2f);
            Vector2 xs = crossSection ?? new Vector2(radius * 2f, radius * 2f);

            var part = GibManager.TakePooled(id);
            GameObject go = part != null ? part.gameObject : new GameObject(id.ToString());
            go.transform.SetParent(bone, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, seg);
            go.transform.localScale = Vector3.one;

            var col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = crossSection.HasValue ? Mathf.Max(crossSection.Value.x, crossSection.Value.y) * 0.5f : radius;
            col.height = Mathf.Max(height, col.radius * 2f);
            col.isTrigger = true;

            var mesh = go.transform.Find("Mesh");
            if (mesh == null)
                PrimitiveMeshes.CreateVisual("Mesh", PrimitiveType.Capsule, go.transform, mat, Vector3.zero, Quaternion.identity,
                    new Vector3(xs.x, height * 0.5f, xs.y));
            else
                mesh.GetComponent<MeshRenderer>().sharedMaterial = mat;

            if (part == null) part = go.AddComponent<BodyPart>();
            part.Init(id, cfg.Tuning(id), Mathf.Max(xs.x, xs.y) * 0.5f, col, cfg);
            part.ProximalLocal = new Vector3(0, -len * 0.5f, 0);
            go.SetActive(true);
            return part;
        }

        static BodyPart HeadPart(Transform headBone, Material skin, Material visor, MannequinConfig cfg)
        {
            var part = GibManager.TakePooled(BodyPartId.Head);
            GameObject go = part != null ? part.gameObject : new GameObject(BodyPartId.Head.ToString());
            go.transform.SetParent(headBone, false);
            go.transform.localPosition = new Vector3(0, HeadCentreUp, 0);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.radius = HeadRadius;
            col.isTrigger = true;

            if (go.transform.Find("Mesh") == null)
            {
                PrimitiveMeshes.CreateVisual("Mesh", PrimitiveType.Sphere, go.transform, skin, Vector3.zero, Quaternion.identity,
                    new Vector3(HeadRadius * 2f, HeadRadius * 2.15f, HeadRadius * 2f));
                // Neck stub and a visor stripe so you can tell which way the dummy faces.
                PrimitiveMeshes.CreateVisual("Neck", PrimitiveType.Capsule, go.transform, skin, new Vector3(0, -0.13f, 0),
                    Quaternion.identity, new Vector3(0.09f, 0.07f, 0.09f));
                PrimitiveMeshes.CreateVisual("Visor", PrimitiveType.Capsule, go.transform, visor, new Vector3(0, 0.02f, 0.105f),
                    Quaternion.Euler(0, 0, 90), new Vector3(0.07f, 0.09f, 0.05f));
            }
            else
            {
                go.transform.Find("Mesh").GetComponent<MeshRenderer>().sharedMaterial = skin;
                go.transform.Find("Neck").GetComponent<MeshRenderer>().sharedMaterial = skin;
            }

            if (part == null) part = go.AddComponent<BodyPart>();
            part.Init(BodyPartId.Head, cfg.Tuning(BodyPartId.Head), HeadRadius * 0.6f, col, cfg);
            part.ProximalLocal = new Vector3(0, -0.17f, 0);
            go.SetActive(true);
            return part;
        }

        /// <summary>Stump cap (reused if a pooled part already has one with this name).</summary>
        static GameObject Cap(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, float radius, Material mat)
        {
            var existing = parent.Find(name);
            GameObject cap = existing != null
                ? existing.gameObject
                : PrimitiveMeshes.CreateVisual(name, PrimitiveType.Sphere, parent, mat, Vector3.zero, Quaternion.identity, Vector3.one);
            cap.transform.localScale = new Vector3(radius * 2.15f, radius * 0.55f, radius * 2.15f);
            cap.transform.SetPositionAndRotation(worldPos, worldRot);
            cap.SetActive(false);
            return cap;
        }
    }
}
