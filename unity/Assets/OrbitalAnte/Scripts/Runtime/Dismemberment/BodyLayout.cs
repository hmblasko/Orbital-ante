using System.Collections.Generic;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>The 14 breakable parts of a humanoid. Order is stable: it is used as an array index.</summary>
    public enum BodyPartId
    {
        Head = 0,
        Torso = 1,
        LeftUpperArm = 2,
        LeftForearm = 3,
        LeftHand = 4,
        RightUpperArm = 5,
        RightForearm = 6,
        RightHand = 7,
        LeftThigh = 8,
        LeftShin = 9,
        LeftFoot = 10,
        RightThigh = 11,
        RightShin = 12,
        RightFoot = 13,
    }

    /// <summary>How a character can still move, decided only by which parts are left.</summary>
    public enum MovementState
    {
        /// <summary>Both legs work: walk and sprint.</summary>
        Intact,
        /// <summary>Exactly one working leg: limp/hop, slower, no sprint.</summary>
        OneLeg,
        /// <summary>No working legs, at least one upper arm: crawl.</summary>
        Crawl,
        /// <summary>No working legs and no arms: torso wriggle.</summary>
        Wriggle,
        /// <summary>Torso gone: the head rolls and hops.</summary>
        HeadOnly,
        /// <summary>Head destroyed. The only death condition.</summary>
        Dead,
    }

    /// <summary>Static description of how the 14 parts hang off each other.</summary>
    public static class BodyLayout
    {
        public const int Count = 14;

        static readonly int[] ParentIndex =
        {
            (int)BodyPartId.Torso,          // Head
            -1,                             // Torso
            (int)BodyPartId.Torso,          // LeftUpperArm
            (int)BodyPartId.LeftUpperArm,   // LeftForearm
            (int)BodyPartId.LeftForearm,    // LeftHand
            (int)BodyPartId.Torso,          // RightUpperArm
            (int)BodyPartId.RightUpperArm,  // RightForearm
            (int)BodyPartId.RightForearm,   // RightHand
            (int)BodyPartId.Torso,          // LeftThigh
            (int)BodyPartId.LeftThigh,      // LeftShin
            (int)BodyPartId.LeftShin,       // LeftFoot
            (int)BodyPartId.Torso,          // RightThigh
            (int)BodyPartId.RightThigh,     // RightShin
            (int)BodyPartId.RightShin,      // RightFoot
        };

        static readonly string[] Names =
        {
            "Head", "Torso",
            "Left Upper Arm", "Left Forearm", "Left Hand",
            "Right Upper Arm", "Right Forearm", "Right Hand",
            "Left Thigh", "Left Shin", "Left Foot",
            "Right Thigh", "Right Shin", "Right Foot",
        };

        static readonly BodyPartId[][] ChildLists = BuildChildren();

        static BodyPartId[][] BuildChildren()
        {
            var lists = new List<BodyPartId>[Count];
            for (int i = 0; i < Count; i++) lists[i] = new List<BodyPartId>();
            for (int i = 0; i < Count; i++)
            {
                if (ParentIndex[i] >= 0) lists[ParentIndex[i]].Add((BodyPartId)i);
            }
            var result = new BodyPartId[Count][];
            for (int i = 0; i < Count; i++) result[i] = lists[i].ToArray();
            return result;
        }

        public static bool HasParent(BodyPartId id) => ParentIndex[(int)id] >= 0;

        /// <summary>Parent part. Only valid when <see cref="HasParent"/> is true (everything except the torso).</summary>
        public static BodyPartId Parent(BodyPartId id) => (BodyPartId)ParentIndex[(int)id];

        public static IReadOnlyList<BodyPartId> Children(BodyPartId id) => ChildLists[(int)id];

        public static string DisplayName(BodyPartId id) => Names[(int)id];

        public static bool IsLeftSide(BodyPartId id)
        {
            int i = (int)id;
            return (i >= 2 && i <= 4) || (i >= 8 && i <= 10);
        }
    }
}
