using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>The fifteen bones a humanoid is posed by. Order is stable: it is used as an array index.</summary>
    public enum BodyBone
    {
        Hips = 0,
        Spine = 1,
        Head = 2,
        LeftUpperArm = 3,
        LeftForearm = 4,
        LeftHand = 5,
        RightUpperArm = 6,
        RightForearm = 7,
        RightHand = 8,
        LeftThigh = 9,
        LeftShin = 10,
        LeftFoot = 11,
        RightThigh = 12,
        RightShin = 13,
        RightFoot = 14,
    }

    /// <summary>
    /// Where a humanoid's joints are at rest: standing at the origin in a T-pose, facing +Z, left side at -X,
    /// in metres above the feet. No Unity types. The character factory, the animation rules and the Blender
    /// script that builds the enemy model (tools/blender/build_enemy.py) all use these numbers.
    /// </summary>
    public static class BodySkeleton
    {
        public const int BoneCount = 15;
        public const int Left = 0;
        public const int Right = 1;

        public const float HipsY = 0.95f;
        public const float SpineY = 1.05f;
        public const float NeckY = 1.50f;
        public const float ShoulderX = 0.24f;
        public const float ShoulderY = 1.40f;
        public const float UpperArmLength = 0.28f;
        public const float ForearmLength = 0.26f;
        public const float HipX = 0.105f;
        public const float HipJointY = 0.92f;
        public const float KneeY = 0.50f;
        public const float AnkleY = 0.09f;
        public const float HeadCentreAboveNeck = 0.19f;
        public const float TorsoCentreAboveSpine = 0.14f;
        public const float ThighLength = HipJointY - KneeY;
        public const float ShinLength = KneeY - AnkleY;

        static readonly int[] ParentIndex =
        {
            -1,                          // Hips
            (int)BodyBone.Hips,          // Spine
            (int)BodyBone.Spine,         // Head
            (int)BodyBone.Spine,         // LeftUpperArm
            (int)BodyBone.LeftUpperArm,  // LeftForearm
            (int)BodyBone.LeftForearm,   // LeftHand
            (int)BodyBone.Spine,         // RightUpperArm
            (int)BodyBone.RightUpperArm, // RightForearm
            (int)BodyBone.RightForearm,  // RightHand
            (int)BodyBone.Hips,          // LeftThigh
            (int)BodyBone.LeftThigh,     // LeftShin
            (int)BodyBone.LeftShin,      // LeftFoot
            (int)BodyBone.Hips,          // RightThigh
            (int)BodyBone.RightThigh,    // RightShin
            (int)BodyBone.RightShin,     // RightFoot
        };

        static readonly Vec3d[] Rest = BuildRest();

        static Vec3d[] BuildRest()
        {
            var rest = new Vec3d[BoneCount];
            rest[(int)BodyBone.Hips] = new Vec3d(0, HipsY, 0);
            rest[(int)BodyBone.Spine] = new Vec3d(0, SpineY, 0);
            rest[(int)BodyBone.Head] = new Vec3d(0, NeckY, 0);
            for (int s = 0; s < 2; s++)
            {
                double sx = s == Left ? -1.0 : 1.0;
                rest[(int)UpperArm(s)] = new Vec3d(sx * ShoulderX, ShoulderY, 0);
                rest[(int)Forearm(s)] = new Vec3d(sx * (ShoulderX + UpperArmLength), ShoulderY, 0);
                rest[(int)Hand(s)] = new Vec3d(sx * (ShoulderX + UpperArmLength + ForearmLength), ShoulderY, 0);
                rest[(int)Thigh(s)] = new Vec3d(sx * HipX, HipJointY, 0);
                rest[(int)Shin(s)] = new Vec3d(sx * HipX, KneeY, 0);
                rest[(int)Foot(s)] = new Vec3d(sx * HipX, AnkleY, 0);
            }
            return rest;
        }

        public static BodyBone UpperArm(int side) => side == Left ? BodyBone.LeftUpperArm : BodyBone.RightUpperArm;
        public static BodyBone Forearm(int side) => side == Left ? BodyBone.LeftForearm : BodyBone.RightForearm;
        public static BodyBone Hand(int side) => side == Left ? BodyBone.LeftHand : BodyBone.RightHand;
        public static BodyBone Thigh(int side) => side == Left ? BodyBone.LeftThigh : BodyBone.RightThigh;
        public static BodyBone Shin(int side) => side == Left ? BodyBone.LeftShin : BodyBone.RightShin;
        public static BodyBone Foot(int side) => side == Left ? BodyBone.LeftFoot : BodyBone.RightFoot;

        /// <summary>The bone a bone hangs from, or -1 for the hips.</summary>
        public static int Parent(BodyBone bone) => ParentIndex[(int)bone];

        /// <summary>A joint's place at rest, measured from the feet.</summary>
        public static Vec3d RestPosition(BodyBone bone) => Rest[(int)bone];

        /// <summary>A joint's place at rest, measured from the joint it hangs from (from the feet, for the hips).</summary>
        public static Vec3d RestOffset(BodyBone bone)
        {
            int parent = ParentIndex[(int)bone];
            return parent < 0 ? Rest[(int)bone] : Rest[(int)bone] - Rest[parent];
        }

        /// <summary>The bone a breakable part rides on.</summary>
        public static BodyBone BoneOf(BodyPartId part)
        {
            switch (part)
            {
                case BodyPartId.Head: return BodyBone.Head;
                case BodyPartId.Torso: return BodyBone.Spine;
                case BodyPartId.LeftUpperArm: return BodyBone.LeftUpperArm;
                case BodyPartId.LeftForearm: return BodyBone.LeftForearm;
                case BodyPartId.LeftHand: return BodyBone.LeftHand;
                case BodyPartId.RightUpperArm: return BodyBone.RightUpperArm;
                case BodyPartId.RightForearm: return BodyBone.RightForearm;
                case BodyPartId.RightHand: return BodyBone.RightHand;
                case BodyPartId.LeftThigh: return BodyBone.LeftThigh;
                case BodyPartId.LeftShin: return BodyBone.LeftShin;
                case BodyPartId.LeftFoot: return BodyBone.LeftFoot;
                case BodyPartId.RightThigh: return BodyBone.RightThigh;
                case BodyPartId.RightShin: return BodyBone.RightShin;
                default: return BodyBone.RightFoot;
            }
        }

        /// <summary>
        /// Where a breakable part's own origin is at rest, measured from the feet. Every part sits on its joint
        /// except the head, whose origin is the middle of the head.
        /// </summary>
        public static Vec3d PartOrigin(BodyPartId part)
        {
            Vec3d joint = Rest[(int)BoneOf(part)];
            return part == BodyPartId.Head ? joint + new Vec3d(0, HeadCentreAboveNeck, 0) : joint;
        }

        /// <summary>
        /// Which way a part points away from the joint that holds it, in the part's own space at rest:
        /// arms out to the side, legs and feet down, the head up. A wound on the part faces the other way.
        /// </summary>
        public static Vec3d PartDirection(BodyPartId part)
        {
            switch (part)
            {
                case BodyPartId.Head: return new Vec3d(0, 1, 0);
                case BodyPartId.LeftUpperArm:
                case BodyPartId.LeftForearm:
                case BodyPartId.LeftHand: return new Vec3d(-1, 0, 0);
                case BodyPartId.RightUpperArm:
                case BodyPartId.RightForearm:
                case BodyPartId.RightHand: return new Vec3d(1, 0, 0);
                case BodyPartId.Torso: return new Vec3d(0, 1, 0);
                default: return new Vec3d(0, -1, 0);
            }
        }
    }

    /// <summary>
    /// One pose of a humanoid: where the whole figure is shifted and turned, and how each bone is turned
    /// against the bone it hangs from. No Unity types, so the same numbers pose the game's character,
    /// the rule tests and the preview pictures.
    /// </summary>
    public sealed class BodyPose
    {
        /// <summary>Shift of the whole figure from where the character stands.</summary>
        public Vec3d RootPosition;
        /// <summary>Turn of the whole figure about its feet.</summary>
        public QuatD RootRotation = QuatD.Identity;
        /// <summary>Each bone's turn against its parent, indexed by <see cref="BodyBone"/>.</summary>
        public readonly QuatD[] Local = new QuatD[BodySkeleton.BoneCount];

        public BodyPose() => Reset();

        public void Reset()
        {
            RootPosition = Vec3d.Zero;
            RootRotation = QuatD.Identity;
            for (int i = 0; i < Local.Length; i++) Local[i] = QuatD.Identity;
        }

        public void CopyFrom(BodyPose other)
        {
            RootPosition = other.RootPosition;
            RootRotation = other.RootRotation;
            Array.Copy(other.Local, Local, Local.Length);
        }

        /// <summary>Moves this pose a fraction of the way to another (0 keeps this pose, 1 becomes the other).</summary>
        public void BlendToward(BodyPose other, double t)
        {
            RootPosition = RootPosition + (other.RootPosition - RootPosition) * t;
            RootRotation = Nlerp(RootRotation, other.RootRotation, t);
            for (int i = 0; i < Local.Length; i++) Local[i] = Nlerp(Local[i], other.Local[i], t);
        }

        /// <summary>A bone's place and turn in the character's space (origin at the feet, +Z forward).</summary>
        public void World(BodyBone bone, out Vec3d position, out QuatD rotation)
        {
            int parent = BodySkeleton.Parent(bone);
            if (parent < 0)
            {
                position = RootPosition + RootRotation.Rotate(BodySkeleton.RestOffset(bone));
                rotation = RootRotation * Local[(int)bone];
                return;
            }
            World((BodyBone)parent, out Vec3d parentPosition, out QuatD parentRotation);
            position = parentPosition + parentRotation.Rotate(BodySkeleton.RestOffset(bone));
            rotation = parentRotation * Local[(int)bone];
        }

        /// <summary>The short way from one turn to another, straight through (cheap, and fine for small steps).</summary>
        public static QuatD Nlerp(QuatD a, QuatD b, double t)
        {
            double dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            double s = dot < 0 ? -t : t;
            double u = 1.0 - t;
            return new QuatD(a.X * u + b.X * s, a.Y * u + b.Y * s, a.Z * u + b.Z * s, a.W * u + b.W * s).Normalized;
        }
    }
}
