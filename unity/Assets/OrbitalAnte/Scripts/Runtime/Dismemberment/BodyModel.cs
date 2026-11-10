using System;
using System.Collections.Generic;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>Why a group of parts came off.</summary>
    public enum DetachReason
    {
        /// <summary>A limb (or a piece of an existing gib) was shot off with everything below it.</summary>
        Severed,
        /// <summary>The torso was destroyed and this chain was hanging off it.</summary>
        TorsoDestroyed,
        /// <summary>The torso itself, blown off the character.</summary>
        TorsoGib,
        /// <summary>The destroyed head.</summary>
        HeadDestroyed,
        /// <summary>Whatever was still attached to the body when the head was destroyed.</summary>
        Corpse,
    }

    /// <summary>One chunk of parts that just separated and should now fly off together as a single gib.</summary>
    public readonly struct Detachment
    {
        /// <summary>The top-most part of the chunk.</summary>
        public readonly BodyPartId Root;
        /// <summary>Every part in the chunk, root first.</summary>
        public readonly BodyPartId[] Members;
        /// <summary>The new group id for the chunk (always greater than 0).</summary>
        public readonly int Group;
        /// <summary>The group the chunk came from: 0 for the living body, otherwise an older gib.</summary>
        public readonly int SourceGroup;
        public readonly DetachReason Reason;

        public Detachment(BodyPartId root, BodyPartId[] members, int group, int sourceGroup, DetachReason reason)
        {
            Root = root;
            Members = members;
            Group = group;
            SourceGroup = sourceGroup;
            Reason = reason;
        }
    }

    /// <summary>What a single hit did.</summary>
    public sealed class DamageResult
    {
        public BodyPartId Part;
        public float DamageApplied;
        public float RemainingHp;
        /// <summary>True when the head was destroyed by this hit.</summary>
        public bool Killed;
        public readonly List<Detachment> Detachments = new List<Detachment>();
    }

    /// <summary>
    /// Pure rules for a breakable character. No Unity types, so the rules can be unit tested anywhere.
    ///
    /// Every part belongs to a group. Group 0 is the living body. Any other group is a gib: a chunk of parts
    /// that flies around as one object. Shooting a part that is not the top of its group can split the group again.
    ///
    /// Rules (Milestone 1):
    /// - Losing a part also takes everything below it.
    /// - The torso can be destroyed. Its limbs fall away as separate chunks; the head stays alive.
    /// - The only death condition is the head being destroyed.
    /// </summary>
    public sealed class BodyModel
    {
        readonly float[] _hp = new float[BodyLayout.Count];
        readonly float[] _maxHp = new float[BodyLayout.Count];
        readonly int[] _group = new int[BodyLayout.Count];
        int _nextGroup = 1;

        public bool Alive { get; private set; } = true;

        /// <param name="maxHp">Hit points for each part, indexed by <see cref="BodyPartId"/>.</param>
        public BodyModel(IReadOnlyList<float> maxHp)
        {
            if (maxHp == null) throw new ArgumentNullException(nameof(maxHp));
            if (maxHp.Count != BodyLayout.Count)
                throw new ArgumentException("Expected " + BodyLayout.Count + " hit point values.", nameof(maxHp));
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                if (maxHp[i] <= 0f) throw new ArgumentException("Hit points must be above zero.", nameof(maxHp));
                _maxHp[i] = maxHp[i];
                _hp[i] = maxHp[i];
            }
        }

        public float Hp(BodyPartId id) => _hp[(int)id];
        public float MaxHp(BodyPartId id) => _maxHp[(int)id];

        /// <summary>0 while the part is on the living body, otherwise the id of the gib it belongs to.</summary>
        public int Group(BodyPartId id) => _group[(int)id];

        /// <summary>True while the part is still attached to the living character.</summary>
        public bool OnBody(BodyPartId id) => Alive && _group[(int)id] == 0;

        /// <summary>True when the part is the top of its group, so there is no joint above it left to break.</summary>
        public bool IsGroupRoot(BodyPartId id)
        {
            if (!BodyLayout.HasParent(id)) return true;
            return _group[(int)BodyLayout.Parent(id)] != _group[(int)id];
        }

        /// <summary>True when a part and its parent are no longer in the same chunk, so wound caps should show.</summary>
        public bool SeparatedFromParent(BodyPartId id)
        {
            if (!BodyLayout.HasParent(id)) return false;
            return _group[(int)BodyLayout.Parent(id)] != _group[(int)id];
        }

        /// <summary>Number of parts still on the living body.</summary>
        public int PartsOnBody
        {
            get
            {
                if (!Alive) return 0;
                int n = 0;
                for (int i = 0; i < BodyLayout.Count; i++) if (_group[i] == 0) n++;
                return n;
            }
        }

        /// <summary>A leg works only while thigh, shin and foot are all attached.</summary>
        public bool LegWorks(bool left)
        {
            return left
                ? OnBody(BodyPartId.LeftThigh) && OnBody(BodyPartId.LeftShin) && OnBody(BodyPartId.LeftFoot)
                : OnBody(BodyPartId.RightThigh) && OnBody(BodyPartId.RightShin) && OnBody(BodyPartId.RightFoot);
        }

        /// <summary>An arm can pull the body along while its upper arm is attached.</summary>
        public bool ArmCanCrawl(bool left)
        {
            return OnBody(left ? BodyPartId.LeftUpperArm : BodyPartId.RightUpperArm);
        }

        /// <summary>A hand can hold things only while the whole arm above it is attached.</summary>
        public bool HandUsable(bool left)
        {
            return left
                ? OnBody(BodyPartId.LeftUpperArm) && OnBody(BodyPartId.LeftForearm) && OnBody(BodyPartId.LeftHand)
                : OnBody(BodyPartId.RightUpperArm) && OnBody(BodyPartId.RightForearm) && OnBody(BodyPartId.RightHand);
        }

        public MovementState State
        {
            get
            {
                if (!Alive) return MovementState.Dead;
                if (!OnBody(BodyPartId.Torso)) return MovementState.HeadOnly;
                bool left = LegWorks(true);
                bool right = LegWorks(false);
                if (left && right) return MovementState.Intact;
                if (left || right) return MovementState.OneLeg;
                if (ArmCanCrawl(true) || ArmCanCrawl(false)) return MovementState.Crawl;
                return MovementState.Wriggle;
            }
        }

        /// <summary>Only an intact character can sprint.</summary>
        public bool CanSprint => State == MovementState.Intact;

        /// <summary>Applies damage to one part and reports everything that came off as a result.</summary>
        public DamageResult ApplyDamage(BodyPartId id, float damage)
        {
            var result = new DamageResult { Part = id, RemainingHp = _hp[(int)id] };
            if (damage <= 0f) return result;

            int group = _group[(int)id];
            bool onLivingBody = Alive && group == 0;

            // The top part of a gib has no joint above it, so there is nothing left to break there.
            // (On the living body the torso is the top part and CAN be destroyed, and so can the head.)
            if (!onLivingBody && IsGroupRoot(id)) return result;

            float before = _hp[(int)id];
            if (before <= 0f) return result;
            float after = Math.Max(0f, before - damage);
            _hp[(int)id] = after;
            result.DamageApplied = before - after;
            result.RemainingHp = after;
            if (after > 0f) return result;

            if (onLivingBody && id == BodyPartId.Head)
            {
                KillByHead(result);
            }
            else if (onLivingBody && id == BodyPartId.Torso)
            {
                DestroyTorso(result);
            }
            else
            {
                result.Detachments.Add(DetachChain(id, group, DetachReason.Severed));
            }
            return result;
        }

        void KillByHead(DamageResult result)
        {
            result.Killed = true;

            // The head pops off on its own.
            int headGroup = _nextGroup++;
            _group[(int)BodyPartId.Head] = headGroup;
            result.Detachments.Add(new Detachment(
                BodyPartId.Head, new[] { BodyPartId.Head }, headGroup, 0, DetachReason.HeadDestroyed));

            // Whatever was still attached becomes one corpse chunk.
            if (_group[(int)BodyPartId.Torso] == 0)
            {
                result.Detachments.Add(DetachChain(BodyPartId.Torso, 0, DetachReason.Corpse));
            }
            Alive = false;
        }

        void DestroyTorso(DamageResult result)
        {
            // Each arm and leg chain still on the body falls away as its own chunk. The head stays alive.
            var children = BodyLayout.Children(BodyPartId.Torso);
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == BodyPartId.Head) continue;
                if (_group[(int)child] != 0) continue;
                result.Detachments.Add(DetachChain(child, 0, DetachReason.TorsoDestroyed));
            }

            int torsoGroup = _nextGroup++;
            _group[(int)BodyPartId.Torso] = torsoGroup;
            result.Detachments.Add(new Detachment(
                BodyPartId.Torso, new[] { BodyPartId.Torso }, torsoGroup, 0, DetachReason.TorsoGib));
        }

        /// <summary>Moves a part and everything below it that shares its group into a new group.</summary>
        Detachment DetachChain(BodyPartId root, int sourceGroup, DetachReason reason)
        {
            var members = new List<BodyPartId>();
            Collect(root, sourceGroup, members);
            int newGroup = _nextGroup++;
            for (int i = 0; i < members.Count; i++) _group[(int)members[i]] = newGroup;
            return new Detachment(root, members.ToArray(), newGroup, sourceGroup, reason);
        }

        void Collect(BodyPartId id, int group, List<BodyPartId> into)
        {
            if (_group[(int)id] != group) return;
            into.Add(id);
            var children = BodyLayout.Children(id);
            for (int i = 0; i < children.Count; i++) Collect(children[i], group, into);
        }
    }
}
