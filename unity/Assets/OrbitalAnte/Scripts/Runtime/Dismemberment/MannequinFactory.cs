using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Builds a breakable humanoid: one piece per body part, each parented to the matching bone of a Unity
    /// Humanoid skeleton. The pieces are the game's own enemy model (Resources/Models/Enemy, made in Blender by
    /// tools/blender/build_enemy.py); if that file is missing the old primitive shapes are used instead.
    /// Because the skeleton has a valid Humanoid avatar, any Humanoid animation clip could drive it later; for now
    /// the motion is worked out every frame (see BodyAnimator and MannequinLocomotion).
    /// </summary>
    public static class MannequinFactory
    {
        /// <summary>The model the pieces come from. Its pieces are named after <see cref="BodyPartId"/>, plus "Stump".</summary>
        public const string ModelId = "Enemy";
        // Which body model the character being built uses. Only while one character is being built.
        static string _model = ModelId;
        static bool _fitted;
        public const string StumpPiece = "Stump";

        // Rest-pose joint positions in metres, character standing at the origin facing +Z. Left side is -X.
        const float HipsY = BodySkeleton.HipsY;
        const float SpineY = BodySkeleton.SpineY;
        const float NeckY = BodySkeleton.NeckY;
        const float ShoulderX = BodySkeleton.ShoulderX;
        const float ShoulderY = BodySkeleton.ShoulderY;
        const float UpperArmLength = BodySkeleton.UpperArmLength;
        const float ForearmLength = BodySkeleton.ForearmLength;
        const float HipX = BodySkeleton.HipX;
        const float HipJointY = BodySkeleton.HipJointY;
        const float KneeY = BodySkeleton.KneeY;
        const float AnkleY = BodySkeleton.AnkleY;
        const float HeadRadius = 0.15f;
        /// <summary>The model's helmet is a little bigger than the stand-in ball, and so is its hitbox.</summary>
        const float ModelHeadRadius = 0.17f;
        const float HeadCentreAboveNeck = BodySkeleton.HeadCentreAboveNeck;
        const float TorsoCentreAboveSpine = BodySkeleton.TorsoCentreAboveSpine;
        const float TorsoHalfDepth = 0.15f;

        // Only while one character is being built.
        static bool _shadowCopies;

        /// <summary>
        /// True if a sun in the scene casts real shadows (the combat arena). Planets and space are lit by the
        /// game's own shaders with Unity's shadows off, so shadow copies there would be drawn for nothing.
        /// </summary>
        static bool SceneCastsShadows()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.isActiveAndEnabled && light.type == LightType.Directional && light.shadows != LightShadows.None) return true;
            }
            return false;
        }

        /// <summary>True when every piece of the enemy model is there to use.</summary>
        public static bool ModelAvailable() => ModelAvailable(ModelId);

        /// <summary>True when every piece of the body model named is there to use.</summary>
        public static bool ModelAvailable(string model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            if (ModelLibrary.PartMesh(model, StumpPiece) == null) return false;
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                if (ModelLibrary.PartMesh(model, ((BodyPartId)i).ToString()) == null) return false;
            }
            return true;
        }

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

            // The kind's own body if it has one, the ordinary body if not, the stand-in shapes if neither.
            _model = config != null && ModelAvailable(config.modelId) ? config.modelId : ModelId;
            bool modelled = ModelAvailable(_model);
            _fitted = modelled;
            _shadowCopies = modelled && SceneCastsShadows();
            var rig = BuildSkeleton(root.transform, modelled);

            var mannequin = root.AddComponent<Mannequin>();
            var parts = BuildParts(mannequin, rig, config, modelled);

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

        static MannequinRig BuildSkeleton(Transform root, bool modelled)
        {
            var rig = new MannequinRig
            {
                HeadRadius = modelled ? ModelHeadRadius : HeadRadius,
                Modelled = modelled,
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

        static BodyPart[] BuildParts(Mannequin owner, MannequinRig rig, MannequinConfig cfg, bool modelled)
        {
            var parts = new BodyPart[BodyLayout.Count];
            var capRadius = new float[BodyLayout.Count];
            Material wound = Mat.Lit(cfg.woundColor, 0.55f);
            // The model is chunkier than the stand-in shapes, so its hitboxes are fitted to it: what looks hit is hit.

            // Torso: a chunky rounded barrel, wider than it is deep.
            {
                var go = NewPartObject("Torso", rig.Spine, Vector3.zero);
                var centre = new Vector3(0f, TorsoCentreAboveSpine, 0f);
                if (!AddModel(go, BodyPartId.Torso, modelled))
                    AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.44f, 0.335f, TorsoHalfDepth * 2f), Mat.Lit(cfg.torsoColor));
                var col = AddCapsule(go, 1, modelled ? 0.215f : 0.19f, 0.67f, centre);
                parts[(int)BodyPartId.Torso] = Finish(go, BodyPartId.Torso, owner, col, 30f, null);
            }

            // Head: slightly oversized, with a dark visor so you can tell which way it is facing.
            {
                var go = NewPartObject("Head", rig.Head, new Vector3(0f, HeadCentreAboveNeck, 0f));
                var col = go.AddComponent<SphereCollider>();
                float neck;
                if (AddModel(go, BodyPartId.Head, modelled))
                {
                    col.radius = ModelHeadRadius;
                    col.center = new Vector3(0f, 0.015f + (_fitted ? BodyFit.HeadRise(_model) : 0f), 0f);
                    neck = 0.165f;
                }
                else
                {
                    AddMesh(go, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * (HeadRadius * 2f), Mat.Lit(cfg.headColor));
                    AddMesh(go, PrimitiveType.Sphere, new Vector3(0f, 0.012f, 0.085f), Quaternion.identity, new Vector3(0.21f, 0.10f, 0.14f), Mat.Lit(cfg.visorColor, 0.8f));
                    col.radius = HeadRadius;
                    neck = HeadRadius - 0.03f;
                }
                capRadius[(int)BodyPartId.Head] = 0.075f;
                var cap = AddCap(go, new Vector3(0f, -neck, 0f), 0.075f, Vector3.down, wound, modelled);
                parts[(int)BodyPartId.Head] = Finish(go, BodyPartId.Head, owner, col, 5f, cap);
            }

            for (int s = 0; s < 2; s++)
            {
                bool left = s == MannequinRig.Left;
                float sx = left ? -1f : 1f;
                Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
                Vector3 inward = new Vector3(-sx, 0f, 0f);

                // Upper arm
                {
                    var id = left ? BodyPartId.LeftUpperArm : BodyPartId.RightUpperArm;
                    var go = NewPartObject(id.ToString(), rig.UpperArm[s], Vector3.zero);
                    var centre = new Vector3(sx * UpperArmLength * 0.5f, 0f, 0f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Capsule, centre, alongX, new Vector3(0.125f, 0.17f, 0.125f), Mat.Lit(cfg.upperLimbColor));
                    var col = AddCapsule(go, 0, modelled ? 0.085f : 0.0625f, 0.34f, centre);
                    capRadius[(int)id] = 0.07f;
                    parts[(int)id] = Finish(go, id, owner, col, 3f, AddCap(go, ProximalCapPosition(inward, 0.07f, modelled), 0.07f, inward, wound, modelled));
                }
                // Forearm
                {
                    var id = left ? BodyPartId.LeftForearm : BodyPartId.RightForearm;
                    var go = NewPartObject(id.ToString(), rig.Forearm[s], Vector3.zero);
                    var centre = new Vector3(sx * ForearmLength * 0.5f, 0f, 0f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Capsule, centre, alongX, new Vector3(0.105f, 0.16f, 0.105f), Mat.Lit(cfg.lowerLimbColor));
                    var col = AddCapsule(go, 0, modelled ? 0.066f : 0.0525f, 0.32f, centre);
                    capRadius[(int)id] = 0.06f;
                    parts[(int)id] = Finish(go, id, owner, col, 2f, AddCap(go, ProximalCapPosition(inward, 0.06f, modelled), 0.06f, inward, wound, modelled));
                }
                // Hand: a chunky mitt.
                {
                    var id = left ? BodyPartId.LeftHand : BodyPartId.RightHand;
                    var go = NewPartObject(id.ToString(), rig.Hand[s], Vector3.zero);
                    var centre = new Vector3(sx * (modelled ? 0.08f : 0.07f), 0f, 0f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Sphere, centre, Quaternion.identity, new Vector3(0.16f, 0.12f, 0.13f), Mat.Lit(cfg.extremityColor));
                    var col = go.AddComponent<SphereCollider>();
                    col.center = centre;
                    col.radius = modelled ? 0.082f : 0.07f;
                    capRadius[(int)id] = 0.052f;
                    parts[(int)id] = Finish(go, id, owner, col, 1f, AddCap(go, ProximalCapPosition(inward, 0.052f, modelled), 0.052f, inward, wound, modelled));
                }
                // Thigh
                {
                    var id = left ? BodyPartId.LeftThigh : BodyPartId.RightThigh;
                    var go = NewPartObject(id.ToString(), rig.Thigh[s], Vector3.zero);
                    var centre = new Vector3(0f, -(HipJointY - KneeY) * 0.5f, 0f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.165f, 0.25f, 0.165f), Mat.Lit(cfg.upperLimbColor));
                    var col = AddCapsule(go, 1, modelled ? 0.10f : 0.0825f, 0.50f, centre);
                    capRadius[(int)id] = 0.09f;
                    parts[(int)id] = Finish(go, id, owner, col, 8f, AddCap(go, ProximalCapPosition(Vector3.up, 0.09f, modelled), 0.09f, Vector3.up, wound, modelled));
                }
                // Shin
                {
                    var id = left ? BodyPartId.LeftShin : BodyPartId.RightShin;
                    var go = NewPartObject(id.ToString(), rig.Shin[s], Vector3.zero);
                    var centre = new Vector3(0f, -(KneeY - AnkleY) * 0.5f, 0f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.identity, new Vector3(0.13f, 0.24f, 0.13f), Mat.Lit(cfg.lowerLimbColor));
                    var col = AddCapsule(go, 1, modelled ? 0.076f : 0.065f, 0.48f, centre);
                    capRadius[(int)id] = 0.072f;
                    parts[(int)id] = Finish(go, id, owner, col, 5f, AddCap(go, ProximalCapPosition(Vector3.up, 0.072f, modelled), 0.072f, Vector3.up, wound, modelled));
                }
                // Foot: a rounded boot pointing forward.
                {
                    var id = left ? BodyPartId.LeftFoot : BodyPartId.RightFoot;
                    var go = NewPartObject(id.ToString(), rig.Foot[s], Vector3.zero);
                    var centre = new Vector3(0f, -0.04f, 0.06f);
                    if (!AddModel(go, id, modelled))
                        AddMesh(go, PrimitiveType.Capsule, centre, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.11f, 0.14f, 0.10f), Mat.Lit(cfg.extremityColor));
                    var col = AddCapsule(go, 2, modelled ? 0.058f : 0.05f, modelled ? 0.32f : 0.28f, centre);
                    capRadius[(int)id] = 0.058f;
                    parts[(int)id] = Finish(go, id, owner, col, 1.5f, AddCap(go, ProximalCapPosition(Vector3.up, 0.058f, modelled), 0.058f, Vector3.up, wound, modelled));
                }
            }

            // A wound on each parent, exactly where each child joined it, facing the way the child pointed.
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                if (!BodyLayout.HasParent(id)) continue;
                BodyPart parent = parts[(int)BodyLayout.Parent(id)];

                Core.Vec3d joint = BodySkeleton.RestPosition(BodySkeleton.BoneOf(id));
                Core.Vec3d origin = BodySkeleton.PartOrigin(BodyLayout.Parent(id));
                Core.Vec3d pointing = BodySkeleton.PartDirection(id);
                var direction = new Vector3((float)pointing.X, (float)pointing.Y, (float)pointing.Z);
                var local = new Vector3((float)(joint.X - origin.X), (float)(joint.Y - origin.Y), (float)(joint.Z - origin.Z));
                // The hips and neck are buried inside the torso piece, so their wounds are pushed out to its skin.
                if (modelled && BodyLayout.Parent(id) == BodyPartId.Torso)
                {
                    if (id == BodyPartId.LeftThigh || id == BodyPartId.RightThigh) local += direction * 0.07f;
                    else if (id == BodyPartId.Head) local += direction * 0.025f;
                }
                float capFit = _fitted ? BodyFit.Thickness(_model, id) : 1f;
                parent.SetChildCap(id, AddCap(parent.gameObject, local, capRadius[i] * capFit, direction, wound, modelled));
            }

            return parts;
        }

        /// <summary>
        /// Where the wound on a severed piece sits. A model piece begins with a ball at its joint, so the wound is
        /// pushed out of the ball toward where the parent was; the stand-in shapes keep it on the joint.
        /// </summary>
        static Vector3 ProximalCapPosition(Vector3 towardParent, float radius, bool modelled)
        {
            return modelled ? towardParent * (radius * 0.55f) : Vector3.zero;
        }

        /// <summary>Hangs the model's piece for a body part on its object. Returns false if the model is not being used.</summary>
        static bool AddModel(GameObject parent, BodyPartId id, bool modelled)
        {
            if (!modelled) return false;
            Mesh mesh = ModelLibrary.PartMesh(_model, id.ToString());
            if (mesh == null) return false;
            AddModelMesh(parent, mesh, "Mesh", true);
            return true;
        }

        static GameObject AddModelMesh(GameObject parent, Mesh mesh, string name, bool castsShadow)
        {
            var go = new GameObject(name);
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.SharedMaterial();
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // The game's own shader draws no shadow, so a second, unseen copy casts it where a scene has real lights.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (castsShadow && _shadowCopies)
            {
                var shadow = new GameObject("Shadow");
                shadow.layer = parent.layer;
                shadow.transform.SetParent(go.transform, false);
                shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
                var caster = shadow.AddComponent<MeshRenderer>();
                caster.sharedMaterial = Mat.Lit(Color.grey);
                caster.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                caster.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                caster.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            return go;
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
            // Each kind's body is thicker or thinner than the ordinary one, part by part: what looks hit is hit.
            if (_fitted)
            {
                float thick = BodyFit.Thickness(_model, id);
                if (hitbox is CapsuleCollider capsule) capsule.radius *= thick;
                else if (hitbox is SphereCollider sphere) sphere.radius *= thick;
            }
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

        /// <summary>
        /// A wound, hidden until the joint it covers is cut. With the model it is a torn stump with a bone end,
        /// pointing out of the piece; otherwise a plain red ball.
        /// </summary>
        static GameObject AddCap(GameObject parent, Vector3 localPosition, float radius, Vector3 facing, Material material, bool modelled)
        {
            Mesh stump = modelled ? ModelLibrary.PartMesh(_model, StumpPiece) : null;
            if (stump == null)
            {
                var ball = AddMesh(parent, PrimitiveType.Sphere, localPosition, Quaternion.identity, Vector3.one * (radius * 2f), material);
                ball.name = "WoundCap";
                return ball;
            }
            // The stump is modelled with a radius of one unit, pointing up.
            GameObject cap = AddModelMesh(parent, stump, "WoundCap", false);
            cap.transform.localPosition = localPosition;
            cap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, facing);
            cap.transform.localScale = Vector3.one * radius;
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
