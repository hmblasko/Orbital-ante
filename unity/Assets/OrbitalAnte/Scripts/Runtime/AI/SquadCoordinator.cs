using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>The flat arena's bounded adapter. Only Sense reads the live player transform.</summary>
    [DefaultExecutionOrder(-80)]
    public sealed class SquadCoordinator : MonoBehaviour
    {
        sealed class Agent
        {
            public int Id;
            public MannequinCombat Combat;
            public MannequinLocomotion Loco;
            public readonly List<Vec3d> Route = new List<Vec3d>();
            public int Waypoint;
            public Vector3 Goal, PlannedTarget;
            public bool HasGoal, Recovering;
            public Vector3? RecoveryDestination;
            public float NextSense, NextPlan, HoldUntil;
            public float LastProgressAt;
            public Vector3 ProgressPosition;
            public TacticalRole PlannedRole;
        }
        Agent[] _agents;
        TacticalAIConfig _cfg;
        TacticalGrid _grid;
        SquadTactics _model;
        readonly TacticalPlanner _planner = new TacticalPlanner();
        TacticalQueryBudget _budget;
        readonly Vec3d[] _occupants = new Vec3d[SquadTactics.MaximumMembers];
        Func<Vec3d, bool?> _candidateLineOfFire;
        int _senseCursor, _planCursor;
        Vector3 _planningTarget;
        float _planningHeight;
        public SquadTactics Model => _model;
        public TacticalQueryBudget Budget => _budget;
        public TacticalGrid Grid => _grid;

        public void Initialize(TacticalAIConfig config, TacticalGrid grid)
        {
            ClearMembers(); _cfg = config; _grid = grid;
            _candidateLineOfFire = CandidateLineOfFire;
            int capacity = Mathf.Clamp(config.maxAgents, 1, SquadTactics.MaximumMembers);
            _agents = new Agent[capacity];
            _model = new SquadTactics(capacity, Mathf.Clamp(config.simultaneousAttackers, 1, 8),
                Mathf.Max(0, config.memorySeconds), Mathf.Max(0.05f, config.sightFreshness),
                Mathf.Max(0.05f, config.attackLease), Mathf.Max(0, config.attackRest), Mathf.Max(0.1f, config.roleInterval), config.frontTurnDegrees);
            _budget = new TacticalQueryBudget(config.sensesPerFrame, config.planningProbesPerFrame,
                config.shotProbesPerFrame, config.movementProbesPerFrame);
        }

        public bool Register(MannequinCombat combat, MannequinLocomotion locomotion, int id)
        {
            if (_model == null || combat == null || locomotion == null) return false;
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent existing = _agents[i]; if (existing == null) continue;
                if (existing.Id == id) return existing.Combat == combat;
                if (existing.Combat == combat) return false;
            }
            for (int i = 0; i < _agents.Length; i++)
            {
                if (_agents[i] != null) continue;
                if (!_model.Register(id)) return false;
                _agents[i] = new Agent { Id = id, Combat = combat, Loco = locomotion };
                locomotion.SuppressIdleWander = true;
                locomotion.MovementProbe = TakeMovementProbe;
                combat.JoinSquad(this, id); return true;
            }
            return false;
        }
        Agent Find(int id)
        {
            if (_agents != null) for (int i = 0; i < _agents.Length; i++)
                if (_agents[i] != null && _agents[i].Id == id) return _agents[i];
            return null;
        }
        public void Remove(int id, MannequinCombat expected = null, bool preserveRegistration = false)
        {
            if (_agents == null) return;
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i] != null && _agents[i].Id == id && (object.ReferenceEquals(expected, null) || _agents[i].Combat == expected))
                {
                    Agent a = _agents[i]; Stop(a); _model.Remove(id);
                    if (a.Loco != null) a.Loco.MovementProbe = null;
                    if (!preserveRegistration && a.Combat != null) a.Combat.JoinSquad(null, -1);
                    _agents[i] = null; return;
                }
        }
        public void RefreshMember(int id)
        { Agent a = Find(id); if (a != null && a.Combat != null) _model.UpdateMember(id, a.Combat.TacticalState); }
        public void ClearMembers()
        {
            if (_agents == null) return;
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent a = _agents[i]; if (a == null) continue;
                if (a.Combat != null) a.Combat.JoinSquad(null, -1);
                if (a.Loco != null) a.Loco.MovementProbe = null;
                Stop(a);
                _model.Remove(a.Id); _agents[i] = null;
            }
            _model.ForgetContacts(); _senseCursor = 0; _planCursor = 0;
        }
        void OnDestroy() => ClearMembers();
        void OnDisable()
        {
            if (_model == null) return;
            _model.ForgetContacts();
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent a = _agents[i]; if (a == null) continue;
                Stop(a); a.NextPlan = 0; a.NextSense = 0;
                if (a.Combat != null) a.Combat.LoseTarget();
            }
        }
        bool TakeMovementProbe() => Take(TacticalQuery.Movement);
        bool Take(TacticalQuery query) => isActiveAndEnabled && _budget != null && _budget.Take(query, Time.frameCount);
        static Vec3d V(Vector3 p) => new Vec3d(p.x, p.y, p.z);
        static Vector3 V(Vec3d p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);

        void Update()
        {
            if (_model == null || Time.deltaTime <= 0) return;
            float now = Time.time;
            bool combatEnabled = CombatWorld.PlayerPresent && !CombatWorld.EnemiesHoldFire;
            if (!combatEnabled)
            {
                _model.ForgetContacts();
                for (int i = 0; i < _agents.Length; i++)
                    if (_agents[i] != null && _agents[i].Combat != null) _agents[i].Combat.LoseTarget();
            }
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent a = _agents[i]; if (a == null || a.Combat == null) continue;
                _model.UpdateMember(a.Id, a.Combat.TacticalState);
            }
            int senses = Mathf.Clamp(_cfg.sensesPerFrame, 1, 16), checkedCount = 0;
            while (combatEnabled && checkedCount++ < _agents.Length && senses > 0)
            {
                Agent a = _agents[_senseCursor]; _senseCursor = (_senseCursor + 1) % _agents.Length;
                if (a == null || a.Combat == null || now < a.NextSense) continue;
                if (!Take(TacticalQuery.Sense)) break;
                Sense(a, now); senses--;
            }
            _model.Tick(now);
            bool knowsTarget = _model.TryKnownTarget(now, out Vec3d known);
            if (!knowsTarget)
            {
                for (int i = 0; i < _agents.Length; i++)
                    if (_agents[i] != null && !_agents[i].RecoveryDestination.HasValue) Stop(_agents[i]);
            }
            // At most one plan per frame: bounded nearby-cell samples, occupancy snapshot, and total A* node budget.
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent a = _agents[_planCursor]; _planCursor = (_planCursor + 1) % _agents.Length;
                if (a == null || a.Combat == null || !a.Combat.TacticalState.CanMove || now < a.NextPlan) continue;
                bool fetching = a.RecoveryDestination.HasValue && !a.Combat.TacticalState.Armed;
                if (!knowsTarget && !fetching) continue;
                TacticalRole role = fetching ? TacticalRole.Recover : _model.RoleOf(a.Id);
                bool roleChanged = a.PlannedRole != role;
                if (a.HasGoal && !roleChanged && now < a.HoldUntil) continue;
                bool recoveryChanged = a.RecoveryDestination.HasValue && (!a.Recovering || (a.RecoveryDestination.Value - a.Goal).sqrMagnitude > 4);
                bool stuck = a.HasGoal && a.Waypoint < a.Route.Count && now - a.LastProgressAt >= Mathf.Max(0.2f, _cfg.stuckReplanSeconds);
                bool targetMoved = recoveryChanged || (V(known) - a.PlannedTarget).sqrMagnitude >= Mathf.Pow(Mathf.Max(1, _cfg.targetMoveReplan), 2);
                if (a.HasGoal && !roleChanged && !targetMoved && !stuck && a.Waypoint < a.Route.Count) continue;
                if (a.HasGoal && !roleChanged && !targetMoved && !stuck && _model.SeesTarget(a.Id, now)) continue;
                Plan(a, knowsTarget ? V(known) : a.RecoveryDestination.Value, now); break;
            }
        }

        void Sense(Agent a, float now)
        {
            a.NextSense = now + Mathf.Max(0.05f, _cfg.senseInterval);
            if (!a.Combat.TacticalState.Alive) { _model.ReportHidden(a.Id); return; }
            Vector3 target = CombatWorld.PlayerEye.position + Vector3.down * 0.3f;
            bool sees = (target - a.Combat.ObservationOrigin).sqrMagnitude <= a.Combat.EngageRange * a.Combat.EngageRange
                && !Physics.Linecast(a.Combat.ObservationOrigin, target, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (sees) { _model.ReportSight(a.Id, V(target), now); a.Combat.ObserveTarget(target, now); }
            else { _model.ReportHidden(a.Id); a.Combat.LoseTarget(); }
        }

        bool? CandidateLineOfFire(Vec3d point)
        {
            if (!Take(TacticalQuery.Plan)) return null;
            return !Physics.Linecast(V(point) + Vector3.up * _planningHeight, _planningTarget,
                Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }
        void Plan(Agent a, Vector3 target, float now)
        {
            a.NextPlan = now + Mathf.Max(0.1f, _cfg.planInterval);
            a.LastProgressAt = now; a.ProgressPosition = a.Combat.transform.position;
            int occupied = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent other = _agents[i];
                if (other != null && other != a && other.Combat != null && other.Combat.TacticalState.Alive)
                    _occupants[occupied++] = V(other.Combat.transform.position);
            }
            _grid.SetOccupants(_occupants, occupied, Mathf.Clamp(_cfg.navigationClearance, 0.7f, 1f) + _grid.Spacing * 0.5);
            TacticalRole role = a.RecoveryDestination.HasValue && !a.Combat.TacticalState.Armed ? TacticalRole.Recover : _model.RoleOf(a.Id);
            if (role == TacticalRole.Hold) { Stop(a); return; }
            bool recovery = role == TacticalRole.Recover;
            if (role == TacticalRole.Anchor && _model.SeesTarget(a.Id, now)
                && Vector3.Distance(a.Combat.transform.position, target) >= Mathf.Max(1, _cfg.anchorRange) * 0.4f
                && _model.ReservationFree(a.Id, V(a.Combat.transform.position), Mathf.Max(0.5f, _cfg.reservationSpacing)))
            {
                // Keep pressure on the observed target while the two flankers travel.
                a.Route.Clear(); a.Waypoint = 0; a.HasGoal = true; a.Goal = a.Combat.transform.position;
                a.PlannedRole = role; a.PlannedTarget = target; a.Recovering = false;
                a.HoldUntil = now + Mathf.Max(0.1f, _cfg.minimumGoalHold);
                _model.Reserve(a.Id, V(a.Goal)); return;
            }
            if (recovery && a.RecoveryDestination.HasValue)
            {
                int cell = _grid.ClosestCell(V(a.RecoveryDestination.Value), _grid.Spacing * 2, true);
                a.HasGoal = _grid.FindRoute(V(a.Combat.transform.position), cell, a.Route,
                    Mathf.Clamp(_cfg.nodesPerPlan, 1, TacticalGrid.MaximumCells));
                a.Waypoint = 0; a.Recovering = true; a.PlannedRole = role; a.PlannedTarget = target;
                if (a.HasGoal)
                {
                    Vector3 approach = a.RecoveryDestination.Value; approach.y = 0;
                    // ClosestCell required a clear cell-to-gun connection; do not stop metres short on coarse grids.
                    a.Route.Add(V(approach)); a.Goal = approach; _model.Reserve(a.Id, V(a.Goal));
                    a.HoldUntil = now + Mathf.Max(0.1f, _cfg.minimumGoalHold);
                }
                else _model.ReleaseReservation(a.Id);
                return;
            }
            float range = role == TacticalRole.Anchor ? _cfg.anchorRange
                : role == TacticalRole.FlankLeft || role == TacticalRole.FlankRight ? _cfg.flankRange : _cfg.supportRange;
            _planningTarget = target;
            _planningHeight = Mathf.Clamp(a.Combat.ObservationOrigin.y - a.Combat.transform.position.y, 0.2f, 1.8f);
            bool found = _planner.Plan(_grid, _model, a.Id, V(a.Combat.transform.position), V(target),
                Mathf.Max(1, range), Mathf.Max(0.5f, _cfg.reservationSpacing), V(a.Goal), a.HasGoal,
                !_model.SeesTarget(a.Id, now), Mathf.Min(_cfg.candidatesPerPlan, _cfg.planningProbesPerFrame),
                _cfg.nodesPerPlan, _candidateLineOfFire, a.Route, out Vec3d goal, recovery, _cfg.minimumFlankAngle);
            a.HasGoal = found; a.Waypoint = 0; a.PlannedTarget = target; a.PlannedRole = role;
            a.Recovering = recovery;
            if (found) { a.Goal = V(goal); a.HoldUntil = now + Mathf.Max(0.1f, _cfg.minimumGoalHold); }
            else _model.ReleaseReservation(a.Id);
        }
        void Stop(Agent a)
        {
            a.HasGoal = false; a.Route.Clear(); _model.ReleaseReservation(a.Id);
            if (a.Loco != null) { a.Loco.Destination = null; a.Loco.FacePoint = null; a.Loco.Hurry = false; }
        }

        public bool Sees(int id) => isActiveAndEnabled && _model != null && _model.SeesTarget(id, Time.time);
        public bool KnowsTarget(out Vector3 target)
        {
            target = Vector3.zero;
            if (!isActiveAndEnabled || _model == null || !_model.TryKnownTarget(Time.time, out Vec3d p)) return false;
            target = V(p); return true;
        }
        public void Drive(int id)
        {
            Agent a = Find(id); if (a == null) return;
            if (!isActiveAndEnabled) { Stop(a); return; }
            bool knows = KnowsTarget(out Vector3 known);
            bool fetching = a.RecoveryDestination.HasValue && !a.Combat.TacticalState.Armed;
            if (!fetching && (!CombatWorld.PlayerPresent || CombatWorld.EnemiesHoldFire || !knows)) { Stop(a); return; }
            if ((a.Combat.transform.position - a.ProgressPosition).sqrMagnitude >= 0.1f * 0.1f)
            { a.ProgressPosition = a.Combat.transform.position; a.LastProgressAt = Time.time; }
            while (a.Waypoint < a.Route.Count)
            {
                Vector3 delta = V(a.Route[a.Waypoint]) - a.Combat.transform.position; delta.y = 0;
                if (delta.sqrMagnitude > 0.2f * 0.2f) break;
                a.Waypoint++;
            }
            // A route is followed exactly; locomotion's capsule sweep guards the small physical step.
            a.Loco.FacePoint = null; a.Loco.Hurry = false;
            a.Loco.Destination = a.HasGoal && a.Waypoint < a.Route.Count ? V(a.Route[a.Waypoint]) : (Vector3?)null;
            if (!a.Loco.Destination.HasValue && knows && Sees(id) && a.Combat.CanFire) a.Loco.FacePoint = known;
        }

        /// <summary>Queues recovery into the coordinator's single plan lane; no extra search here.</summary>
        public bool RouteToRecovery(int id, Vector3 destination)
        {
            Agent a = Find(id); if (a == null) return false;
            a.RecoveryDestination = destination; return true;
        }
        public void ClearRecoveryDestination(int id)
        {
            Agent a = Find(id); if (a == null || !a.RecoveryDestination.HasValue) return;
            a.RecoveryDestination = null; Stop(a); a.NextPlan = 0;
        }

        public bool MayShoot(int id, Vector3 muzzle, Vector3 target)
        {
            Agent a = Find(id);
            if (!isActiveAndEnabled || a == null || !a.Combat.TacticalState.ReadyToFire || !_model.HasFirePermit(id, Time.time)) return false;
            if (!Take(TacticalQuery.Shot)) { _model.ReleaseFirePermit(id, Time.time); return false; }
            if (Physics.Linecast(muzzle, target, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            { _model.ReportHidden(id); a.Combat.LoseTarget(); return false; }
            // Enemy bullets currently hit world/player only. Still withhold a shot through a teammate's body.
            Vector3 line = target - muzzle; float sqr = line.sqrMagnitude;
            for (int i = 0; i < _agents.Length; i++)
            {
                Agent other = _agents[i]; if (other == null || other == a || other.Combat == null) continue;
                Vector3 p = other.Combat.transform.position;
                float t = sqr > 0.001f ? Vector3.Dot(p - muzzle, line) / sqr : -1;
                if (t <= 0 || t >= 1) continue;
                Vector3 closest = muzzle + line * t;
                float height = other.Combat.TacticalBodyHeight;
                if (closest.y < p.y - 0.1f || closest.y > p.y + height) continue;
                Vector2 flat = new Vector2(closest.x - p.x, closest.z - p.z);
                float clearance = Mathf.Max(0.1f, _cfg.friendlyClearance);
                if (flat.sqrMagnitude < clearance * clearance) { _model.ReleaseFirePermit(id, Time.time); return false; }
            }
            return true;
        }
    }
}
