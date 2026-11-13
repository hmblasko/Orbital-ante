using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.AI
{
    /// <summary>One reused planner; a capped shortlist and total A* node budget bound every decision.</summary>
    public sealed class TacticalPlanner
    {
        public const int MaximumCandidates = 32;
        const int MaximumRouteAttempts = 4;
        readonly int[] _cells = new int[MaximumCandidates];
        readonly double[] _scores = new double[MaximumCandidates];
        readonly List<Vec3d> _trial = new List<Vec3d>();
        public int LastCandidates { get; private set; }
        public int LastExpanded { get; private set; }

        public bool Plan(TacticalGrid grid, SquadTactics squad, int id, Vec3d from, Vec3d target,
            double range, double spacing, Vec3d previous, bool hasPrevious, bool searching,
            int candidateLimit, int nodeLimit, Func<Vec3d, bool?> lineOfFire, List<Vec3d> route,
            out Vec3d goal, bool seekCover = false, double minimumFlankDegrees = 35)
        {
            route.Clear(); goal = from; LastCandidates = 0; LastExpanded = 0;
            int cap = Math.Max(1, Math.Min(MaximumCandidates, candidateLimit));
            int count = 0;
            Vec3d preferred = squad.PreferredDirection(id);
            // Keep angular/radial alternatives: a wall hiding the ideal cell must not hide every
            // member of a shortlist made from adjacent cells at the same side of that wall.
            for (int sample = 0; sample < cap; sample++)
            {
                double angle = sample % 8 == 0 ? 0 : ((sample % 8 + 1) / 2) * 0.25 * (sample % 2 == 0 ? -1 : 1);
                double radius = range * (sample / 8 == 0 ? 1 : sample / 8 == 1 ? 0.72 : sample / 8 == 2 ? 1.28 : 1.55);
                Vec3d samplePoint = target + preferred.RotatedAround(Vec3d.Up, angle) * radius;
                if (sample == cap - 1 && hasPrevious) samplePoint = previous;
                int cell = grid.ClosestCell(samplePoint, grid.Spacing * 2, false);
                if (cell < 0) continue;
                bool duplicate = false;
                for (int j = 0; j < count; j++) if (_cells[j] == cell) { duplicate = true; break; }
                if (duplicate) continue;
                Vec3d p = grid.Position(cell);
                TacticalRole role = squad.RoleOf(id);
                if (role == TacticalRole.FlankLeft || role == TacticalRole.FlankRight)
                {
                    Vec3d offset = p - target; offset.Y = 0;
                    double minimumCos = Math.Cos(Math.Max(15, Math.Min(90, minimumFlankDegrees)) * Math.PI / 180);
                    double side = Vec3d.Cross(squad.Front, offset).Y;
                    if (Vec3d.Dot(offset.Normalized, squad.Front) > minimumCos
                        || (role == TacticalRole.FlankLeft ? side >= 0 : side <= 0)) continue;
                }
                if (!squad.ReservationFree(id, p, spacing)) continue;
                double score = SquadTactics.ScorePosition(new TacticalCandidate { Position = p,
                    Reachable = true, TravelCost = Vec3d.Distance(from, p), CoverNearby = grid.CoverNearby(cell) },
                    target, preferred, range, previous, hasPrevious, true);
                if (seekCover) score += grid.CoverNearby(cell) ? 5 : -5;
                int at = count;
                while (at > 0 && score > _scores[at - 1]) at--;
                if (at >= cap) continue;
                int last = Math.Min(count, cap - 1);
                for (int j = last; j > at; j--) { _cells[j] = _cells[j - 1]; _scores[j] = _scores[j - 1]; }
                _cells[at] = cell; _scores[at] = score; count = Math.Min(cap, count + 1);
            }
            // Physics is queried only for the small shortlist, never all grid cells.
            for (int i = 0; i < count; i++)
            {
                bool? clear = lineOfFire?.Invoke(grid.Position(_cells[i])); LastCandidates++;
                // Unknown (query budget denied) is neither safe cover nor a firing position.
                if (!clear.HasValue || (seekCover ? clear.Value : !clear.Value))
                    _scores[i] = double.NegativeInfinity;
            }
            for (int i = 1; i < count; i++)
            {
                int cell = _cells[i]; double score = _scores[i]; int at = i;
                while (at > 0 && score > _scores[at - 1])
                { _cells[at] = _cells[at - 1]; _scores[at] = _scores[at - 1]; at--; }
                _cells[at] = cell; _scores[at] = score;
            }
            int remaining = Math.Max(0, Math.Min(TacticalGrid.MaximumCells, nodeLimit));
            for (int i = 0; i < Math.Min(count, MaximumRouteAttempts) && remaining > 0; i++)
            {
                if (double.IsNegativeInfinity(_scores[i])) break;
                bool found = grid.FindRoute(from, _cells[i], _trial, remaining);
                LastExpanded += grid.LastExpanded; remaining -= grid.LastExpanded;
                if (!found) continue;
                route.AddRange(_trial); goal = grid.Position(_cells[i]); squad.Reserve(id, goal); return true;
            }
            return false;
        }
    }
}
