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
