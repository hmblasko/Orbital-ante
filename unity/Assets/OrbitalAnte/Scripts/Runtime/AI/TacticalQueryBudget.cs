using System;

namespace OrbitalAnte.AI
{
    public enum TacticalQuery { Sense, Plan, Shot, Movement }

    /// <summary>Fail closed when a frame's physics allowance is used. Separate lanes prevent starvation.</summary>
    public sealed class TacticalQueryBudget
    {
        readonly int[] _limits = new int[4], _used = new int[4];
        int _frame = int.MinValue;
        public TacticalQueryBudget(int sense, int plan, int shot, int movement)
        {
            _limits[0] = Math.Max(1, Math.Min(16, sense)); _limits[1] = Math.Max(1, Math.Min(32, plan));
            _limits[2] = Math.Max(1, Math.Min(32, shot)); _limits[3] = Math.Max(1, Math.Min(256, movement));
        }
        public bool Take(TacticalQuery query, int frame)
        {
            if (_frame != frame) { Array.Clear(_used, 0, _used.Length); _frame = frame; }
            int at = (int)query;
            if (_used[at] >= _limits[at]) return false;
            _used[at]++; return true;
        }
        public int Used(TacticalQuery query) => _used[(int)query];
        public int Limit(TacticalQuery query) => _limits[(int)query];
    }
}
