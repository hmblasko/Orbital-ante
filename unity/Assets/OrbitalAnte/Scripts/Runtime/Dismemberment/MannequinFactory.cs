using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Builds the placeholder test humanoid entirely from primitives: one mesh per body part, each parented to
    /// the matching bone of a Unity Humanoid skeleton. Because the skeleton has a valid Humanoid avatar, any
    /// Humanoid animation clip can drive it later; for now the motion is procedural (see MannequinLocomotion).
    /// </summary>
    public static class MannequinFactory
    {
        // Rest-pose joint positions in metres, character standing at the origin facing +Z. Left side is -X.
        const float HipsY = 0.95f;
        const float SpineY = 1.05f;
        const float NeckY = 1.50f;
        const float ShoulderX = 0.24f;
        const float ShoulderY = 1.40f;
        const float UpperArmLength = 0.28f;
        const float ForearmLength = 0.26f;
        const float HipX = 0.105f;
        const float HipJointY = 0.92f;
        const float KneeY = 0.50f;
        const float AnkleY = 0.09f;
        const float HeadRadius = 0.15f;
        const float HeadCentreAboveNeck = 0.19f;
        const float TorsoCentreAboveSpine = 0.14f;
        const float TorsoHalfDepth = 0.15f;

        public static Mannequin Create(Vector3 position, Quaternion rotation, Transform parent, MannequinConfig config, string name = "Mannequin")
        {
            if (config == null) config = MannequinConfig.Load();

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            root.layer = Layers.CharacterBody;

            // A kinematic body makes the moving hitboxes cheap and correct for the physics engine.
            var rootBody = root.AddComponent<Rigidbody>();
            rootBody.isKinematic = true;
            rootBody.useGravity = false;

            var rig = BuildSkeleton(root.transform);

            var mannequin = root.AddComponent<Mannequin>();
            var parts = BuildParts(mannequin, rig, config);

            // Simple capsule that stops the character and the player walking through each other.
            var blockerGo = new GameObject("BodyBlocker");
            blockerGo.layer = Layers.CharacterBody;
            blockerGo.transform.SetParent(root.transform, false);
            var blocker = blockerGo.AddComponent<CapsuleCollider>();
            blocker.direction = 1;
            blocker.radius = 0.28f;
            blocker.height = 1.8f;
            blocker.center = new Vector3(0f, 0.9f, 0f);

            mannequin.Initialize(config, rig, parts, blocker);

            var locomotion = root.AddComponent<MannequinLocomotion>();
            locomotion.Initialize(mannequin, blocker);

            // The fighting brain: holds a gun, shoots, fetches dropped guns, takes cover.
            var combat = root.AddComponent<AI.MannequinCombat>();
            combat.Initialize(mannequin, locomotion);
            return mannequin;
        }

        // ------------------------------------------------------------------ skeleton

        static MannequinRig BuildSkeleton(Transform root)
        {
            var rig = new MannequinRig
            {
                HeadRadius = HeadRadius,
                HeadCentreHeight = NeckY + HeadCentreAboveNeck,
                TorsoCentreHeight = SpineY + TorsoCentreAboveSpine,
                TorsoHalfDepth = TorsoHalfDepth,
            };

            rig.Pose = NewBone("Pose", root, Vector3.zero);
            rig.RigRoot = NewBone("Rig", rig.Pose, Vector3.zero);

            rig.Hips = NewBone("Hips", rig.RigRoot, new Vector3(0f, HipsY, 0f));
            rig.Spine = NewBone("Spine", rig.Hips, new Vector3(0f, SpineY, 0f));
            rig.Head = NewBone("Head", rig.Spine, new Vector3(0f, NeckY, 0f));

            for (int s = 0; s < 2; s++)
            {
                float sx = s == MannequinRig.Left ? -1f : 1f;
                string side = s == MannequinRig.Left ? "Left" : "Right";

                rig.UpperArm[s] = NewBone(side + "UpperArm", rig.Spine, new Vector3(sx * ShoulderX, ShoulderY, 0f));
                rig.Forearm[s] = NewBone(side + "LowerArm", rig.UpperArm[s], new Vector3(sx * (ShoulderX + UpperArmLength), ShoulderY, 0f));
                rig.Hand[s] = NewBone(side + "Hand", rig.Forearm[s], new Vector3(sx * (ShoulderX + UpperArmLength + ForearmLength), ShoulderY, 0f));

                rig.Thigh[s] = NewBone(side + "UpperLeg", rig.Hips, new Vector3(sx * HipX, HipJointY, 0f));
                rig.Shin[s] = NewBone(side + "LowerLeg", rig.Thigh[s], new Vector3(sx * HipX, KneeY, 0f));
                rig.Foot[s] = NewBone(side + "Foot", rig.Shin[s], new Vector3(sx * HipX, AnkleY, 0f));
            }

            BuildHumanoidAvatar(rig);
            return rig;
        }

        /// <summary>Creates a bone at a rest-pose position given relative to the character's feet.</summary>
        static Transform NewBone(string name, Transform parent, Vector3 restPositionFromFeet)
        {
            var go = new GameObject(name);
            go.layer = Layers.Hitbox;
            var t = go.transform;
            t.SetParent(parent, false);
            // Every ancestor is unrotated and unscaled in the rest pose, so local = desired - parent's rest position.
            Vector3 parentRest = RestPositions.TryGetValue(parent, out var p) ? p : Vector3.zero;
            t.localPosition = restPositionFromFeet - parentRest;
            t.localRotation = Quaternion.identity;
            RestPositions[t] = restPositionFromFeet;
            return t;
        }

        static readonly Dictionary<Transform, Vector3> RestPositions = new Dictionary<Transform, Vector3>();

        static void BuildHumanoidAvatar(MannequinRig rig)
        {
            var human = new List<HumanBone>();
            AddHumanBone(human, rig.Hips, "Hips");
            AddHumanBone(human, rig.Spine, "Spine");
            AddHumanBone(human, rig.Head, "Head");
            for (int s = 0; s < 2; s++)
            {
                string side = s == MannequinRig.Left ? "Left" : "Right";
                AddHumanBone(human, rig.UpperArm[s], side + "UpperArm");
                AddHumanBone(human, rig.Forearm[s], side + "LowerArm");
                AddHumanBone(human, rig.Hand[s], side + "Hand");
                AddHumanBone(human, rig.Thigh[s], side + "UpperLeg");
                AddHumanBone(human, rig.Shin[s], side + "LowerLeg");
                AddHumanBone(human, rig.Foot[s], side + "Foot");
            }

            var skeleton = new List<SkeletonBone>();
            var all = rig.RigRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                skeleton.Add(new SkeletonBone
                {
                    name = all[i].name,
                    position = all[i].localPosition,
                    rotation = all[i].localRotation,
                    scale = all[i].localScale,
                });
            }

            var description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };

            rig.Animator = rig.RigRoot.gameObject.AddComponent<Animator>();
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(rig.RigRoot.gameObject, description);
            if (avatar != null)
            {
                avatar.name = "MannequinAvatar";
                if (avatar.isValid && avatar.isHuman)
                {
                    rig.Animator.avatar = avatar;
                }
                else
                {
                    Debug.LogWarning("Mannequin: the Humanoid avatar did not validate. Procedural motion still works, " +
                                     "but Humanoid animation clips will not until this is fixed.");
                }
            }

            // Nothing drives the Animator yet (no controller), and the procedural motion writes the bones directly.
            // Enable this again when an Animator Controller with Humanoid clips is assigned.
            rig.Animator.enabled = false;

            // The lookup table is only needed while building.
            RestPositions.Clear();
        }

        static void AddHumanBone(List<HumanBone> list, Transform bone, string humanName)
        {
            var hb = new HumanBone { boneName = bone.name, humanName = humanName };
            hb.limit.useDefaultValues = true;
            list.Add(hb);
        }

        // ------------------------------------------------------------------ parts

        static BodyPart[] BuildParts(Mannequin owner, MannequinRig rig, MannequinConfig cfg)
        {
            var parts = new BodyPart[BodyLayout.Count];
            Material wound = Mat.Lit(cfg.woundColor, 0.55f);

            // Torso: a chunky rounded barrel, wider than it is deep.
            {
                var go = NewPartObject("Torso", rig.Spine, Vector3.zero);
                var centre = new Vector3(0f, TorsoCentreAboveSpine, 0f);
                AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.44f, 0.335f, TorsoHalfDepth * 2f), Mat.Lit(cfg.torsoColor));
                var col = AddCapsule(go, 1, 0.19f, 0.67f, centre);
                parts[(int)BodyPartId.Torso] = Finish(go, BodyPartId.Torso, owner, col, 30f, null);
            }

            // Head: slightly oversized, with a dark visor so you can tell which way it is facing.
            {
                var go = NewPartObject("Head", rig.Head, new Vector3(0f, HeadCentreAboveNeck, 0f));
                AddMesh(go, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * (HeadRadius * 2f), Mat.Lit(cfg.headColor));
                AddMesh(go, PrimitiveType.Sphere, new Vector3(0f, 0.012f, 0.085f), Quaternion.identity, new Vector3(0.21f, 0.10f, 0.14f), Mat.Lit(cfg.visorColor, 0.8f));
                var col = go.AddComponent<SphereCollider>();
                col.radius = HeadRadius;
                var cap = AddCap(go, new Vector3(0f, -HeadRadius + 0.03f, 0f), 0.075f, wound);
                parts[(int)BodyPartId.Head] = Finish(go, BodyPartId.Head, owner, col, 5f, cap);
            }

            for (int s = 0; s < 2; s++)
            {
                bool left = s == MannequinRig.Left;
                float sx = left ? -1f : 1f;
                Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

                // Upper arm
                {
                    var id = left ? BodyPartId.LeftUpperArm : BodyPartId.RightUpperArm;
                    var go = NewPartObject(id.ToString(), rig.UpperArm[s], Vector3.zero);
                    var centre = new Vector3(sx * UpperArmLength * 0.5f, 0f, 0f);
                    AddMesh(go, PrimitiveType.Capsule, centre, alongX, new Vector3(0.125f, 0.17f, 0.125f), Mat.Lit(cfg.upperLimbColor));
                    var col = AddCapsule(go, 0, 0.0625f, 0.34f, centre);
                    parts[(int)id] = Finish(go, id, owner, col, 3f, AddCap(go, Vector3.zero, 0.07f, wound));
                }
                // Forearm
                {
                    var id = left ? BodyPartId.LeftForearm : BodyPartId.RightForearm;
                    var go = NewPartObject(id.ToString(), rig.Forearm[s], Vector3.zero);
                    var centre = new Vector3(sx * ForearmLength * 0.5f, 0f, 0f);
                    AddMesh(go, PrimitiveType.Capsule, centre, alongX, new Vector3(0.105f, 0.16f, 0.105f), Mat.Lit(cfg.lowerLimbColor));
                    var col = AddCapsule(go, 0, 0.0525f, 0.32f, centre);
                    parts[(int)id] = Finish(go, id, owner, col, 2f, AddCap(go, Vector3.zero, 0.06f, wound));
                }
                // Hand: a chunky mitt.
                {
                    var id = left ? BodyPartId.LeftHand : BodyPartId.RightHand;
                    var go = NewPartObject(id.ToString(), rig.Hand[s], Vector3.zero);
                    var centre = new Vector3(sx * 0.07f, 0f, 0f);
                    AddMesh(go, PrimitiveType.Sphere, centre, Quaternion.identity, new Vector3(0.16f, 0.12f, 0.13f), Mat.Lit(cfg.extremityColor));
                    var col = go.AddComponent<SphereCollider>();
                    col.center = centre;
                    col.radius = 0.07f;
                    parts[(int)id] = Finish(go, id, owner, col, 1f, AddCap(go, Vector3.zero, 0.052f, wound));
                }
                // Thigh
                {
                    var id = left ? BodyPartId.LeftThigh : BodyPartId.RightThigh;
                    var go = NewPartObject(id.ToString(), rig.Thigh[s], Vector3.zero);
                    var centre = new Vector3(0f, -(HipJointY - KneeY) * 0.5f, 0f);
                    AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.165f, 0.25f, 0.165f), Mat.Lit(cfg.upperLimbColor));
                    var col = AddCapsule(go, 1, 0.0825f, 0.50f, centre);
                    parts[(int)id] = Finish(go, id, owner, col, 8f, AddCap(go, Vector3.zero, 0.09f, wound));
                }
                // Shin
                {
                    var id = left ? BodyPartId.LeftShin : BodyPartId.RightShin;
                    var go = NewPartObject(id.ToString(), rig.Shin[s], Vector3.zero);
                    var centre = new Vector3(0f, -(KneeY - AnkleY) * 0.5f, 0f);
                    AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.13f, 0.24f, 0.13f), Mat.Lit(cfg.lowerLimbColor));
                    var col = AddCapsule(go, 1, 0.065f, 0.48f, centre);
                    parts[(int)id] = Finish(go, id, owner, col, 5f, AddCap(go, Vector3.zero, 0.072f, wound));
                }
                // Foot: a rounded boot pointing forward.
                {
                    var id = left ? BodyPartId.LeftFoot : BodyPartId.RightFoot;
                    var go = NewPartObject(id.ToString(), rig.Foot[s], Vector3.zero);
                    var centre = new Vector3(0f, -0.04f, 0.06f);
                    AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.11f, 0.14f, 0.10f), Mat.Lit(cfg.extremityColor));
                    var col = AddCapsule(go, 2, 0.05f, 0.28f, centre);
                    parts[(int)id] = Finish(go, id, owner, col, 1.5f, AddCap(go, Vector3.zero, 0.058f, wound));
                }
            }

            // A wound cap on each parent, exactly where each child joins it.
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                if (!BodyLayout.HasParent(id)) continue;
                BodyPart child = parts[i];
                BodyPart parent = parts[(int)BodyLayout.Parent(id)];

                Vector3 jointWorld = child.ProximalCap != null ? child.ProximalCap.transform.position : child.transform.position;
                Vector3 local = parent.transform.InverseTransformPoint(jointWorld);
                float radius = child.ProximalCap != null ? child.ProximalCap.transform.localScale.x * 0.5f : 0.06f;
                parent.SetChildCap(id, AddCap(parent.gameObject, local, radius, wound));
            }

            return parts;
        }

        static GameObject NewPartObject(string name, Transform bone, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        static BodyPart Finish(GameObject go, BodyPartId id, Mannequin owner, Collider hitbox, float mass, GameObject proximalCap)
        {
            var part = go.AddComponent<BodyPart>();
            part.Setup(id, owner, hitbox, mass, proximalCap);
            return part;
        }

        static GameObject AddMesh(GameObject parent, PrimitiveType type, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var mesh = GameObject.CreatePrimitive(type);
            mesh.name = "Mesh";
            // The primitive's own collider is not wanted: the part has a hand-fitted hitbox instead.
            var builtIn = mesh.GetComponent<Collider>();
            if (builtIn != null) Object.DestroyImmediate(builtIn);
            mesh.layer = parent.layer;
            var t = mesh.transform;
            t.SetParent(parent.transform, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        static GameObject AddCap(GameObject parent, Vector3 localPosition, float radius, Material material)
        {
            var cap = AddMesh(parent, PrimitiveType.Sphere, localPosition, Quaternion.identity, Vector3.one * (radius * 2f), material);
            cap.name = "WoundCap";
            return cap;
        }

        static CapsuleCollider AddCapsule(GameObject go, int axis, float radius, float height, Vector3 centre)
        {
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = axis;
            col.radius = radius;
            col.height = height;
            col.center = centre;
            return col;
        }
    }
}
