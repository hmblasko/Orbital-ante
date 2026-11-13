using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.AI
{
    public enum TacticalRole { Hold, Anchor, FlankLeft, FlankRight, Support, Recover }

    /// <summary>Observed capabilities only: no access to Unity objects or the player's current transform.</summary>
    public struct SquadMemberState
    {
        public bool Alive, Armed, ReadyToFire, CanMove, CanFlank;
        public Vec3d Position;
    }

    public struct TacticalCandidate
    {
        public Vec3d Position;
        public double TravelCost;
        public bool Reachable, HasLineOfFire, CoverNearby;
    }

    /// <summary>
    /// Shared, expiring sight reports; stable tactical roles; and finite, rotating fire leases. Tactical
    /// decisions consume sightings rather than an omniscient player position. No Unity types or random walks.
    /// </summary>
    public sealed class SquadTactics
    {
        public const int MaximumMembers = 64;
        sealed class Member
        {
            public int Id;
            public SquadMemberState State;
            public TacticalRole Role;
            public bool Seeing;
            public double SeenAt = double.NegativeInfinity;
            public double PermitUntil, NextPermitAt;
            public double LastPermitAt = double.NegativeInfinity;
            public Vec3d Reserved;
            public bool HasReservation;
        }
        readonly Member[] _members;
        readonly int _attackLimit;
        readonly double _memory, _sightFreshness, _leaseSeconds, _restSeconds, _roleSeconds, _frontTurnCosine;
        double _lastSeenAt = double.NegativeInfinity, _nextRoles;
        Vec3d _knownTarget, _front = Vec3d.Forward;
        bool _hasFront;
        int _count;
        public int Count => _count;
        public int AttackLimit => _attackLimit;
        public Vec3d Front => _front;

        public SquadTactics(int capacity, int attackLimit, double memorySeconds, double sightFreshness,
            double leaseSeconds, double restSeconds, double roleSeconds, double frontTurnDegrees = 60)
        {
            _members = new Member[Math.Max(1, Math.Min(MaximumMembers, capacity))];
            _attackLimit = Math.Max(1, Math.Min(_members.Length, attackLimit));
            _memory = Math.Max(0, memorySeconds); _sightFreshness = Math.Max(0.01, sightFreshness);
            _leaseSeconds = Math.Max(0.05, leaseSeconds); _restSeconds = Math.Max(0, restSeconds);
            _roleSeconds = Math.Max(0.1, roleSeconds);
            _frontTurnCosine = Math.Cos(Math.Max(15, Math.Min(150, frontTurnDegrees)) * Math.PI / 180);
        }

        Member Find(int id)
        {
            for (int i = 0; i < _count; i++) if (_members[i].Id == id) return _members[i];
            return null;
        }

        public bool Register(int id)
        {
            if (Find(id) != null) return true;
            if (_count >= _members.Length) return false;
            _members[_count++] = new Member { Id = id };
            return true;
        }

        public void Remove(int id)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_members[i].Id != id) continue;
                for (int j = i; j < _count - 1; j++) _members[j] = _members[j + 1];
                _members[--_count] = null; _nextRoles = 0;
                return;
            }
        }

        public void UpdateMember(int id, SquadMemberState state)
        {
            Member m = Find(id); if (m == null) return;
            m.State = state;
            if (!state.Alive || !state.ReadyToFire) m.PermitUntil = 0;
            if (!state.Alive || !state.CanMove) m.HasReservation = false;
            if (!state.Alive) m.Seeing = false;
        }

        public void ReportSight(int id, Vec3d target, double now)
        {
            Member m = Find(id);
            if (m == null || !m.State.Alive || !Finite(target) || !Finite(now)) return;
            m.Seeing = true; m.SeenAt = now;
            _knownTarget = target; _lastSeenAt = now;
        }

        public void ReportHidden(int id)
        {
            Member m = Find(id); if (m == null) return;
            m.Seeing = false; m.PermitUntil = 0;
        }

        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        static bool Finite(Vec3d p) => Finite(p.X) && Finite(p.Y) && Finite(p.Z);

        public bool TryKnownTarget(double now, out Vec3d target)
        {
            target = _knownTarget;
            return Finite(now) && now >= _lastSeenAt && now - _lastSeenAt <= _memory;
        }

        public bool SeesTarget(int id, double now)
        {
            Member m = Find(id);
            return m != null && m.State.Alive && m.Seeing && now >= m.SeenAt && now - m.SeenAt <= _sightFreshness;
        }

        public TacticalRole RoleOf(int id) => Find(id)?.Role ?? TacticalRole.Hold;

        public void ForgetContacts()
        {
            _lastSeenAt = double.NegativeInfinity; _hasFront = false; _nextRoles = 0;
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i]; m.Seeing = false; m.PermitUntil = 0;
                m.Role = TacticalRole.Hold; m.HasReservation = false;
            }
        }

        public void Tick(double now)
        {
            if (!TryKnownTarget(now, out Vec3d target))
            {
                for (int i = 0; i < _count; i++)
                { _members[i].Role = TacticalRole.Hold; _members[i].PermitUntil = 0; _members[i].HasReservation = false; }
                _hasFront = false; _nextRoles = 0;
                return;
            }
            if (now >= _nextRoles || RolesInvalid()) AssignRoles(target, now);

            int active = 0;
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (m.PermitUntil > now && Eligible(m, now)) { active++; continue; }
                if (m.PermitUntil > 0) { m.PermitUntil = 0; m.NextPermitAt = now + _restSeconds; }
            }
            while (active < _attackLimit)
            {
                Member best = null;
                for (int i = 0; i < _count; i++)
                {
                    Member m = _members[i];
                    if (!Eligible(m, now) || m.PermitUntil > now || now < m.NextPermitAt) continue;
                    if (best == null || m.LastPermitAt < best.LastPermitAt
                        || (m.LastPermitAt == best.LastPermitAt && m.Id < best.Id)) best = m;
                }
                if (best == null) break;
                best.PermitUntil = now + _leaseSeconds; best.LastPermitAt = now; active++;
            }
        }

        bool Eligible(Member m, double now) => m.State.Alive && m.State.ReadyToFire && m.Seeing
            && now >= m.SeenAt && now - m.SeenAt <= _sightFreshness;

        public bool HasFirePermit(int id, double now)
        {
            Member m = Find(id);
            return m != null && Eligible(m, now) && m.PermitUntil > now && TryKnownTarget(now, out _);
        }
        public void ReleaseFirePermit(int id, double now = double.NaN)
        { Member m = Find(id); if (m != null) { m.PermitUntil = 0; if (Finite(now)) m.NextPermitAt = now + _restSeconds; } }

        bool RolesInvalid()
        {
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (!m.State.Alive && m.Role != TacticalRole.Hold) return true;
                if (m.State.Alive && !m.State.Armed
                    && m.Role != (m.State.CanMove ? TacticalRole.Recover : TacticalRole.Hold)) return true;
                if (m.State.Alive && m.State.Armed && (m.Role == TacticalRole.Hold || m.Role == TacticalRole.Recover)) return true;
                if ((m.Role == TacticalRole.FlankLeft || m.Role == TacticalRole.FlankRight) && !m.State.CanFlank) return true;
                if ((m.Role == TacticalRole.Anchor || m.Role == TacticalRole.FlankLeft || m.Role == TacticalRole.FlankRight)
                    && (!m.State.Alive || !m.State.Armed)) return true;
            }
            return false;
        }

        void AssignRoles(Vec3d target, double now)
        {
            _nextRoles = now + _roleSeconds;
            Vec3d centre = Vec3d.Zero; int alive = 0;
            for (int i = 0; i < _count; i++) if (_members[i].State.Alive) { centre += _members[i].State.Position; alive++; }
            Vec3d from = alive > 0 ? centre / alive - target : Vec3d.Forward; from.Y = 0;
            Vec3d nextFront = from.SqrMagnitude > 0.01 ? from.Normalized : _front;
            if (!_hasFront || Vec3d.Dot(_front, nextFront) < _frontTurnCosine) _front = nextFront;
            _hasFront = true;
            Member pressure = Retain(TacticalRole.Anchor, false);
            Member left = Retain(TacticalRole.FlankLeft, true);
            Member right = Retain(TacticalRole.FlankRight, true);
            // A surviving flanker may be promoted: even a lone survivor needs the anchor job.
            if (pressure == null) pressure = BestFor(target, _front, null, null, null, false);
            if (left == pressure) left = null;
            if (right == pressure) right = null;
            Vec3d leftDir = _front.RotatedAround(Vec3d.Up, -Math.PI * 0.4);
            Vec3d rightDir = _front.RotatedAround(Vec3d.Up, Math.PI * 0.4);
            if (left == null) left = BestFor(target, leftDir, pressure, right, null, true);
            if (right == null) right = BestFor(target, rightDir, pressure, left, null, true);
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (!m.State.Alive) m.Role = TacticalRole.Hold;
                else if (!m.State.Armed) m.Role = m.State.CanMove ? TacticalRole.Recover : TacticalRole.Hold;
                else if (m == pressure) m.Role = TacticalRole.Anchor;
                else if (m == left) m.Role = TacticalRole.FlankLeft;
                else if (m == right) m.Role = TacticalRole.FlankRight;
                else m.Role = TacticalRole.Support;
            }
        }

        Member Retain(TacticalRole role, bool flank)
        {
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (m.Role == role && m.State.Alive && m.State.Armed && (!flank || m.State.CanFlank)) return m;
            }
            return null;
        }

        Member BestFor(Vec3d target, Vec3d direction, Member a, Member b, Member c, bool flank)
        {
            Member best = null; double score = double.NegativeInfinity;
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (m == a || m == b || m == c || !m.State.Alive || !m.State.Armed || (flank && !m.State.CanFlank)) continue;
                Vec3d delta = m.State.Position - target; delta.Y = 0;
                double value = Vec3d.Dot(delta.Normalized, direction) * 4 - delta.Magnitude * 0.02;
                if (value > score || (value == score && best != null && m.Id < best.Id)) { score = value; best = m; }
            }
            return best;
        }

        public Vec3d PreferredDirection(int id)
        {
            Member m = Find(id);
            if (m == null) return _front;
            double angle = m.Role == TacticalRole.FlankLeft ? -Math.PI * 0.4
                : m.Role == TacticalRole.FlankRight ? Math.PI * 0.4 : (m.Id % 3 - 1) * Math.PI * 0.12;
            return _front.RotatedAround(Vec3d.Up, angle);
        }

        public void Reserve(int id, Vec3d point)
        { Member m = Find(id); if (m != null) { m.Reserved = point; m.HasReservation = true; } }
        public void ReleaseReservation(int id) { Member m = Find(id); if (m != null) m.HasReservation = false; }
        public bool ReservationFree(int id, Vec3d point, double spacing)
        {
            for (int i = 0; i < _count; i++)
            {
                Member m = _members[i];
                if (m.Id == id || !m.State.Alive || !m.HasReservation) continue;
                Vec3d delta = m.Reserved - point; delta.Y = 0;
                if (delta.SqrMagnitude < spacing * spacing) return false;
            }
            return true;
        }

        public static double ScorePosition(TacticalCandidate candidate, Vec3d target, Vec3d preferredDirection,
            double preferredRange, Vec3d previousGoal, bool hasPreviousGoal, bool searching)
        {
            if (!candidate.Reachable || double.IsNaN(candidate.TravelCost) || double.IsInfinity(candidate.TravelCost))
                return double.NegativeInfinity;
            Vec3d offset = candidate.Position - target; offset.Y = 0;
            double range = offset.Magnitude;
            double score = Vec3d.Dot(offset.Normalized, preferredDirection) * 8
                - Math.Abs(range - preferredRange) * 0.7 - candidate.TravelCost * 0.15;
            score += candidate.HasLineOfFire ? 4 : searching ? 0 : -4;
            if (candidate.CoverNearby) score += 1.2;
            if (hasPreviousGoal && Vec3d.Distance(candidate.Position, previousGoal) < 2) score += 2;
            return score;
        }
    }
}
