using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.AI
{
    /// <summary>Axis-aligned footprint of an actual arena obstacle, not a generated cover promise.</summary>
    public struct TacticalObstacle
    {
        public double MinX, MinZ, MaxX, MaxZ;
        public TacticalObstacle(double minX, double minZ, double maxX, double maxZ)
        { MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; }
    }

    /// <summary>
    /// Small deterministic ground graph for the current flat, box-built arena. Built from its real footprints,
    /// inflated for a character. Four-way links cannot cut corners or jump across a thin wall. No Unity types.
    /// Bounded A* routes use reusable buffers.
    /// This is deliberately not a navigation solution for planets or moving ships.
    /// </summary>
    public sealed class TacticalGrid
    {
        public const int MaximumCells = 4096;
        public const int MaximumObstacles = 64;
        readonly double _minX, _minZ, _maxX, _maxZ, _spacing, _clearance;
        readonly int _width, _depth;
        readonly TacticalObstacle[] _obstacles;
        readonly bool[] _free;
        readonly byte[] _links;
        readonly int[] _distance, _parent;
        readonly int[] _heap, _heapIndex;
        readonly bool[] _closed;
        readonly bool[] _occupied;
        int _heapCount, _destination;
        int _source = -1;
        public int Count => _free.Length;
        public double Spacing => _spacing;
        public int LastExpanded { get; private set; }

        public TacticalGrid(double minX, double minZ, double maxX, double maxZ, double spacing,
            double clearance, TacticalObstacle[] obstacles)
        {
            if (!(maxX > minX) || !(maxZ > minZ) || !Finite(spacing) || spacing <= 0
                || !Finite(clearance) || clearance < 0) throw new ArgumentException("Invalid tactical grid dimensions.");
            _minX = minX; _minZ = minZ; _maxX = maxX; _maxZ = maxZ;
            _spacing = spacing; _clearance = clearance;
            double width = Math.Floor((maxX - minX) / spacing) + 1;
            double depth = Math.Floor((maxZ - minZ) / spacing) + 1;
            if (!Finite(width) || !Finite(depth) || width * depth > MaximumCells)
                throw new ArgumentException("The tactical grid exceeds its cell cap.");
            _width = (int)width; _depth = (int)depth;
            _obstacles = obstacles == null ? Array.Empty<TacticalObstacle>() : (TacticalObstacle[])obstacles.Clone();
            if (_obstacles.Length > MaximumObstacles) throw new ArgumentException("Too many tactical obstacles.");
            _free = new bool[_width * _depth]; _links = new byte[Count];
            _distance = new int[Count]; _parent = new int[Count];
            _heap = new int[Count]; _heapIndex = new int[Count]; _closed = new bool[Count];
            _occupied = new bool[Count];
            for (int i = 0; i < Count; i++) _free[i] = PointClear(Position(i));
            for (int i = 0; i < Count; i++)
            {
                if (!_free[i]) continue;
                for (int d = 0; d < 4; d++)
                {
                    int other = Neighbour(i, d);
                    if (other >= 0 && _free[other] && SegmentClear(Position(i), Position(other))) _links[i] |= (byte)(1 << d);
                }
            }
            ResetField();
        }

        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public Vec3d Position(int cell) => new Vec3d(_minX + cell % _width * _spacing, 0, _minZ + cell / _width * _spacing);
        public bool IsFree(int cell) => cell >= 0 && cell < Count && _free[cell];

        public bool PointClear(Vec3d p)
        {
            if (!Finite(p.X) || !Finite(p.Z) || p.X < _minX || p.X > _maxX || p.Z < _minZ || p.Z > _maxZ) return false;
            for (int i = 0; i < _obstacles.Length; i++)
            {
                TacticalObstacle b = _obstacles[i];
                if (p.X >= b.MinX - _clearance && p.X <= b.MaxX + _clearance
                    && p.Z >= b.MinZ - _clearance && p.Z <= b.MaxZ + _clearance) return false;
            }
            return true;
        }

        public bool SegmentClear(Vec3d from, Vec3d to)
        {
            if (!PointClear(from) || !PointClear(to)) return false;
            for (int i = 0; i < _obstacles.Length; i++)
            {
                TacticalObstacle b = _obstacles[i];
                double lo = 0, hi = 1;
                if (Clip(from.X, to.X - from.X, b.MinX - _clearance, b.MaxX + _clearance, ref lo, ref hi)
                    && Clip(from.Z, to.Z - from.Z, b.MinZ - _clearance, b.MaxZ + _clearance, ref lo, ref hi)) return false;
            }
            return true;
        }

        static bool Clip(double origin, double delta, double min, double max, ref double lo, ref double hi)
        {
            if (Math.Abs(delta) < 1e-12) return origin >= min && origin <= max;
            double a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) { double t = a; a = b; b = t; }
            lo = Math.Max(lo, a); hi = Math.Min(hi, b);
            return lo <= hi;
        }

        int Neighbour(int cell, int direction)
        {
            int x = cell % _width, z = cell / _width;
            switch (direction)
            {
                case 0: return x + 1 < _width ? cell + 1 : -1;
                case 1: return z + 1 < _depth ? cell + _width : -1;
                case 2: return x > 0 ? cell - 1 : -1;
                default: return z > 0 ? cell - _width : -1;
            }
        }

        public int ClosestCell(Vec3d p, double maxDistance, bool requireClearConnection)
        {
            if (!Finite(p.X) || !Finite(p.Z) || !Finite(maxDistance) || maxDistance < 0) return -1;
            int best = -1;
            double bestDistance = maxDistance * maxDistance;
            double lowX = Math.Max(0, Math.Ceiling((p.X - maxDistance - _minX) / _spacing));
            double highX = Math.Min(_width - 1, Math.Floor((p.X + maxDistance - _minX) / _spacing));
            double lowZ = Math.Max(0, Math.Ceiling((p.Z - maxDistance - _minZ) / _spacing));
            double highZ = Math.Min(_depth - 1, Math.Floor((p.Z + maxDistance - _minZ) / _spacing));
            if (lowX > highX || lowZ > highZ) return -1;
            for (int z = (int)lowZ; z <= (int)highZ; z++)
            for (int x = (int)lowX; x <= (int)highX; x++)
            {
                int i = z * _width + x;
                if (!_free[i]) continue;
                Vec3d offset = Position(i) - p;
                double distance = offset.X * offset.X + offset.Z * offset.Z;
                if (distance > bestDistance || (distance == bestDistance && best >= 0)) continue;
                if (requireClearConnection && !SegmentClear(p, Position(i))) continue;
                bestDistance = distance; best = i;
            }
            return best;
        }

        /// <summary>A per-plan snapshot of teammate footprints. No physics queries or unbounded collections.</summary>
        public void SetOccupants(Vec3d[] positions, int count, double radius)
        {
            Array.Clear(_occupied, 0, _occupied.Length);
            int cap = Math.Min(SquadTactics.MaximumMembers, Math.Min(count, positions?.Length ?? 0));
            double sqr = Math.Max(0, radius) * Math.Max(0, radius);
            for (int cell = 0; cell < Count; cell++)
            {
                if (!_free[cell]) continue;
                Vec3d p = Position(cell);
                for (int i = 0; i < cap; i++)
                {
                    Vec3d delta = p - positions[i]; delta.Y = 0;
                    if (delta.SqrMagnitude <= sqr) { _occupied[cell] = true; break; }
                }
            }
        }

        void ResetField()
        {
            for (int i = 0; i < Count; i++) { _distance[i] = -1; _parent[i] = -1; }
            _source = -1; LastExpanded = 0;
        }

        int Heuristic(int cell) => Math.Abs(cell % _width - _destination % _width)
            + Math.Abs(cell / _width - _destination / _width);
        bool Earlier(int a, int b)
        {
            int fa = _distance[a] + Heuristic(a), fb = _distance[b] + Heuristic(b);
            return fa < fb || (fa == fb && a < b);
        }
        void SwapHeap(int a, int b)
        {
            int t = _heap[a]; _heap[a] = _heap[b]; _heap[b] = t;
            _heapIndex[_heap[a]] = a; _heapIndex[_heap[b]] = b;
        }
        void PushOrImprove(int cell)
        {
            int at = _heapIndex[cell];
            if (at < 0) { at = _heapCount++; _heap[at] = cell; _heapIndex[cell] = at; }
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (!Earlier(_heap[at], _heap[parent])) break;
                SwapHeap(at, parent); at = parent;
            }
        }
        int Pop()
        {
            int result = _heap[0]; _heapIndex[result] = -1; _heapCount--;
            if (_heapCount == 0) return result;
            _heap[0] = _heap[_heapCount]; _heapIndex[_heap[0]] = 0;
            int at = 0;
            while (at * 2 + 1 < _heapCount)
            {
                int child = at * 2 + 1;
                if (child + 1 < _heapCount && Earlier(_heap[child + 1], _heap[child])) child++;
                if (!Earlier(_heap[child], _heap[at])) break;
                SwapHeap(at, child); at = child;
            }
            return result;
        }

        /// <summary>Returns no partial route when the expansion budget is exhausted.</summary>
        public bool FindRoute(Vec3d from, int destination, List<Vec3d> route, int nodeBudget)
        {
            route.Clear(); ResetField(); _heapCount = 0; _destination = destination;
            if (!IsFree(destination) || _occupied[destination] || nodeBudget <= 0) return false;
            _source = ClosestCell(from, _spacing * 2, true);
            if (_source < 0) return false;
            for (int i = 0; i < Count; i++) { _heapIndex[i] = -1; _closed[i] = false; }
            _distance[_source] = 0; PushOrImprove(_source);
            while (_heapCount > 0 && LastExpanded < Math.Min(Count, nodeBudget))
            {
                int cell = Pop(); _closed[cell] = true; LastExpanded++;
                if (cell == destination) return BuildRoute(destination, route);
                for (int d = 0; d < 4; d++)
                {
                    if ((_links[cell] & (1 << d)) == 0) continue;
                    int other = Neighbour(cell, d);
                    if (_closed[other] || _occupied[other]) continue;
                    int cost = _distance[cell] + 1;
                    if (_distance[other] >= 0 && _distance[other] <= cost) continue;
                    _distance[other] = cost; _parent[other] = cell; PushOrImprove(other);
                }
            }
            return false;
        }

        public bool BuildRoute(int cell, List<Vec3d> route)
        {
            route.Clear();
            if (cell < 0 || cell >= Count || _source < 0 || _distance[cell] < 0) return false;
            for (int cursor = cell; cursor >= 0; cursor = _parent[cursor]) route.Add(Position(cursor));
            route.Reverse();
            return true;
        }

        public bool CoverNearby(int cell)
        {
            if (!IsFree(cell)) return false;
            for (int d = 0; d < 4; d++)
            {
                int n = Neighbour(cell, d);
                if (n >= 0 && !_free[n]) return true;
            }
            return false;
        }
    }
}
