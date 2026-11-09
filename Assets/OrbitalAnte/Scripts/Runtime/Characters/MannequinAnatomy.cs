using System.Collections.Generic;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// The rules of the body, with no Unity objects involved so they can be unit-tested:
    /// which part hangs off which, what comes off with it, and how the body can still move.
    /// </summary>
    public static class MannequinAnatomy
    {
        public static readonly BodyPartId[] AllParts =
        {
            BodyPartId.Torso, BodyPartId.Head,
            BodyPartId.UpperArmL, BodyPartId.ForearmL, BodyPartId.HandL,
            BodyPartId.UpperArmR, BodyPartId.ForearmR, BodyPartId.HandR,
            BodyPartId.ThighL, BodyPartId.ShinL, BodyPartId.FootL,
            BodyPartId.ThighR, BodyPartId.ShinR, BodyPartId.FootR,
        };

        /// <summary>Parent part, or null for the torso (the root of the body).</summary>
        public static BodyPartId? Parent(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.Torso: return null;
                case BodyPartId.Head: return BodyPartId.Torso;
                case BodyPartId.UpperArmL: return BodyPartId.Torso;
                case BodyPartId.ForearmL: return BodyPartId.UpperArmL;
                case BodyPartId.HandL: return BodyPartId.ForearmL;
                case BodyPartId.UpperArmR: return BodyPartId.Torso;
                case BodyPartId.ForearmR: return BodyPartId.UpperArmR;
                case BodyPartId.HandR: return BodyPartId.ForearmR;
                case BodyPartId.ThighL: return BodyPartId.Torso;
                case BodyPartId.ShinL: return BodyPartId.ThighL;
                case BodyPartId.FootL: return BodyPartId.ShinL;
                case BodyPartId.ThighR: return BodyPartId.Torso;
                case BodyPartId.ShinR: return BodyPartId.ThighR;
                case BodyPartId.FootR: return BodyPartId.ShinR;
                default: return null;
            }
        }

        public static List<BodyPartId> Children(BodyPartId id)
        {
            var list = new List<BodyPartId>();
            foreach (var p in AllParts)
                if (Parent(p) == id) list.Add(p);
            return list;
        }

        /// <summary>
        /// The part plus everything below it that is still in <paramref name="attached"/>,
        /// parents before children. <paramref name="exclude"/> (and its children) are skipped.
        /// </summary>
        public static List<BodyPartId> AttachedSubtree(BodyPartId root, PartSet attached, BodyPartId? exclude = null)
        {
            var result = new List<BodyPartId>();
            if (!attached.Has(root) || root == exclude) return result;
            var stack = new Stack<BodyPartId>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                result.Add(id);
                var kids = Children(id);
                for (int i = kids.Count - 1; i >= 0; i--)
                {
                    var k = kids[i];
                    if (attached.Has(k) && k != exclude) stack.Push(k);
                }
            }
            return result;
        }

        /// <summary>
        /// Which parts come away when <paramref name="broken"/> runs out of HP.
        /// The head never leaves with the torso: destroying the torso leaves a living head.
        /// </summary>
        public static List<BodyPartId> PartsLostWhenBroken(BodyPartId broken, PartSet attached)
        {
            if (broken == BodyPartId.Torso)
                return AttachedSubtree(BodyPartId.Torso, attached, exclude: BodyPartId.Head);
            return AttachedSubtree(broken, attached);
        }

        public static bool LegIntact(PartSet a, bool left) => left
            ? a.Has(BodyPartId.ThighL) && a.Has(BodyPartId.ShinL) && a.Has(BodyPartId.FootL)
            : a.Has(BodyPartId.ThighR) && a.Has(BodyPartId.ShinR) && a.Has(BodyPartId.FootR);

        /// <summary>An arm can drag the body if the upper arm and forearm are there (hand optional).</summary>
        public static bool ArmUsable(PartSet a, bool left) => left
            ? a.Has(BodyPartId.UpperArmL) && a.Has(BodyPartId.ForearmL)
            : a.Has(BodyPartId.UpperArmR) && a.Has(BodyPartId.ForearmR);

        /// <summary>A hand that can hold a weapon needs the whole arm attached.</summary>
        public static bool HandUsable(PartSet a, bool left) => left
            ? ArmUsable(a, true) && a.Has(BodyPartId.HandL)
            : ArmUsable(a, false) && a.Has(BodyPartId.HandR);

        public static int UsableHands(PartSet a) => (HandUsable(a, true) ? 1 : 0) + (HandUsable(a, false) ? 1 : 0);

        public static LocomotionState Evaluate(PartSet a)
        {
            if (!a.Has(BodyPartId.Head)) return LocomotionState.Dead;
            if (!a.Has(BodyPartId.Torso)) return LocomotionState.HeadOnly;
            int legs = (LegIntact(a, true) ? 1 : 0) + (LegIntact(a, false) ? 1 : 0);
            if (legs == 2) return LocomotionState.Normal;
            if (legs == 1) return LocomotionState.Limp;
            int arms = (ArmUsable(a, true) ? 1 : 0) + (ArmUsable(a, false) ? 1 : 0);
            return arms > 0 ? LocomotionState.Crawl : LocomotionState.Wriggle;
        }

        /// <summary>Sprinting needs both legs. Shooting needs a torso and at least one usable hand.</summary>
        public static bool CanSprint(PartSet a) => Evaluate(a) == LocomotionState.Normal;

        public static bool CanShoot(PartSet a) =>
            a.Has(BodyPartId.Head) && a.Has(BodyPartId.Torso) && UsableHands(a) > 0;
    }
}
