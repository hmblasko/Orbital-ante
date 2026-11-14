using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// The bones of a mannequin. Index 0 is the left side and index 1 is the right side.
    /// In the rest pose (a T-pose facing +Z) every bone has an identity local rotation.
    /// </summary>
    public sealed class MannequinRig
    {
        public const int Left = 0;
        public const int Right = 1;

        /// <summary>Sits between the character root and the skeleton. Procedural motion tilts and offsets this.</summary>
        public Transform Pose;
        /// <summary>The skeleton root. Carries the Animator and the Humanoid avatar.</summary>
        public Transform RigRoot;
        public Animator Animator;

        public Transform Hips;
        public Transform Spine;
        public Transform Head;
        public readonly Transform[] UpperArm = new Transform[2];
        public readonly Transform[] Forearm = new Transform[2];
        public readonly Transform[] Hand = new Transform[2];
        public readonly Transform[] Thigh = new Transform[2];
        public readonly Transform[] Shin = new Transform[2];
        public readonly Transform[] Foot = new Transform[2];

        /// <summary>Height of the centre of the head above the feet in the rest pose.</summary>
        public float HeadCentreHeight;
        public float HeadRadius;
        /// <summary>Height of the middle of the torso above the feet in the rest pose.</summary>
        public float TorsoCentreHeight;
        /// <summary>Half the front-to-back thickness of the torso.</summary>
        public float TorsoHalfDepth;
        /// <summary>True when the pieces are the enemy model rather than the stand-in shapes.</summary>
        public bool Modelled;

        /// <summary>Puts every bone where a worked-out pose says (see BodyAnimator).</summary>
        public void Apply(BodyPose pose)
        {
            Pose.localPosition = ToVector(pose.RootPosition);
            Pose.localRotation = ToQuaternion(pose.RootRotation);
            Hips.localRotation = ToQuaternion(pose.Local[(int)BodyBone.Hips]);
            Spine.localRotation = ToQuaternion(pose.Local[(int)BodyBone.Spine]);
            Head.localRotation = ToQuaternion(pose.Local[(int)BodyBone.Head]);
            for (int s = 0; s < 2; s++)
            {
                UpperArm[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.UpperArm(s)]);
                Forearm[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.Forearm(s)]);
                Hand[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.Hand(s)]);
                Thigh[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.Thigh(s)]);
                Shin[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.Shin(s)]);
                Foot[s].localRotation = ToQuaternion(pose.Local[(int)BodySkeleton.Foot(s)]);
            }
        }

        static Vector3 ToVector(OrbitalAnte.Core.Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion ToQuaternion(OrbitalAnte.Core.QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);

        public void ResetBones()
        {
            Hips.localRotation = Quaternion.identity;
            Spine.localRotation = Quaternion.identity;
            Head.localRotation = Quaternion.identity;
            for (int s = 0; s < 2; s++)
            {
                UpperArm[s].localRotation = Quaternion.identity;
                Forearm[s].localRotation = Quaternion.identity;
                Hand[s].localRotation = Quaternion.identity;
                Thigh[s].localRotation = Quaternion.identity;
                Shin[s].localRotation = Quaternion.identity;
                Foot[s].localRotation = Quaternion.identity;
            }
        }
    }
}
