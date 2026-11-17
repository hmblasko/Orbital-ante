using System;

namespace OrbitalAnte.Planets
{
    /// <summary>A shared frame allowance for fixed features and scatter. Counts attempts, including refused ones.</summary>
    public sealed class SpawnBudget
    {
        public int Remaining { get; private set; }

        public void BeginFrame(int allowance) => Remaining = Math.Max(0, allowance);

        public int Take(int requested)
        {
            int count = Math.Min(Remaining, Math.Max(0, requested));
            Remaining -= count;
            return count;
        }

        /// <summary>Progress through immutable placements, independent of how many attempts produced an object.</summary>
        public sealed class Group
        {
            public int Count { get; }
            public int NextIndex { get; private set; }
            public bool Cancelled { get; private set; }
            public bool Complete => !Cancelled && NextIndex == Count;

            public Group(int count) => Count = Math.Max(0, count);

            /// <summary>Reserves the next consecutive range once. Hiding never refunds this frame's work.</summary>
            public int Take(SpawnBudget budget, out int first)
            {
                first = NextIndex;
                if (Cancelled) return 0;
                int count = budget.Take(Count - NextIndex);
                NextIndex += count;
                return count;
            }

            /// <summary>The owner retires its shown objects; cancelled progress cannot be resumed.</summary>
            public void Cancel() => Cancelled = true;
        }
    }
}
