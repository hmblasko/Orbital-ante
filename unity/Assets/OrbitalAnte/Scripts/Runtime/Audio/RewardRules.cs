using System.Collections.Generic;

namespace OrbitalAnte.Audio
{
    /// <summary>The reward ladder, smallest to biggest. A bigger reward always sounds bigger.</summary>
    public enum RewardKind
    {
        PointsTick = 0,
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3,
        Tier4 = 4,
        Multiplier = 5,
        LootCommon = 6,
        LootRare = 7,
        Discovery = 8,
        Repair = 9,
    }

    /// <summary>
    /// Decides which reward sounds actually play. Rewards often arrive in a burst (a kill, a multiplier and a
    /// pickup in the same half second); played all at once they turn to mush and none of them feels like anything.
    /// So: one reward "voice" at a time, the biggest waiting reward goes first, small ones that have gone stale
    /// are dropped rather than played late, and score ticks are capped to a rate the ear can count.
    /// No Unity types.
    /// </summary>
    public sealed class RewardRules
    {
        /// <summary>Gap kept after a reward starts before the next may start, by kind.</summary>
        public static float Gap(RewardKind k)
        {
            switch (k)
            {
                case RewardKind.PointsTick: return 0.055f;
                case RewardKind.Tier1: return 0.22f;
                case RewardKind.Tier2: return 0.34f;
                case RewardKind.Tier3: return 0.55f;
                case RewardKind.Tier4: return 2.8f;
                case RewardKind.Multiplier: return 0.45f;
                case RewardKind.LootCommon: return 0.3f;
                case RewardKind.LootRare: return 2.0f;
                case RewardKind.Discovery: return 2.6f;
                default: return 0.5f;
            }
        }

        /// <summary>How long a reward may wait its turn before it is not worth playing any more.</summary>
        public static float Patience(RewardKind k)
        {
            switch (k)
            {
                case RewardKind.PointsTick: return 0.08f;
                case RewardKind.Tier1: return 0.35f;
                case RewardKind.Tier2: return 0.6f;
                case RewardKind.LootCommon: return 0.8f;
                default: return 6f; // big rewards wait their turn rather than be lost
            }
        }

        /// <summary>Bigger number wins when two rewards are waiting.</summary>
        public static int Rank(RewardKind k)
        {
            switch (k)
            {
                case RewardKind.PointsTick: return 0;
                case RewardKind.Tier1: return 1;
                case RewardKind.LootCommon: return 2;
                case RewardKind.Tier2: return 3;
                case RewardKind.Repair: return 4;
                case RewardKind.Multiplier: return 5;
                case RewardKind.Tier3: return 6;
                case RewardKind.LootRare: return 7;
                case RewardKind.Discovery: return 8;
                default: return 9; // Tier4
            }
        }

        public static string EventId(RewardKind k)
        {
            switch (k)
            {
                case RewardKind.PointsTick: return "reward.points.tick";
                case RewardKind.Tier1: return "reward.tier1";
                case RewardKind.Tier2: return "reward.tier2";
                case RewardKind.Tier3: return "reward.tier3";
                case RewardKind.Tier4: return "reward.tier4";
                case RewardKind.Multiplier: return "reward.multiplier";
                case RewardKind.LootCommon: return "reward.loot.common";
                case RewardKind.LootRare: return "reward.loot.rare";
                case RewardKind.Discovery: return "reward.discovery";
                default: return "reward.repair";
            }
        }

        /// <summary>Whether the music should dip for this reward so it lands (only the big ones).</summary>
        public static bool DucksMusic(RewardKind k) => Rank(k) >= 6;

        /// <summary>How long the music stays dipped: about as long as the reward's own melody.</summary>
        public static float DuckSeconds(RewardKind k) => k == RewardKind.Tier3 ? 1.2f : 2.4f;

        struct Waiting
        {
            public RewardKind Kind;
            public double At;
        }

        readonly List<Waiting> _queue = new List<Waiting>();
        double _freeAt;
        int _tickStep;
        double _lastTick = -10;

        public int WaitingCount => _queue.Count;
        /// <summary>How far up its climb the score tick is; resets after a pause in the counting.</summary>
        public int TickStep => _tickStep;
        public int Dropped { get; private set; }

        public void Request(RewardKind kind, double now)
        {
            // Never let ticks pile up: one waiting tick is as good as ten.
            if (kind == RewardKind.PointsTick)
            {
                for (int i = 0; i < _queue.Count; i++) if (_queue[i].Kind == RewardKind.PointsTick) { Dropped++; return; }
            }
            if (_queue.Count >= 12)
            {
                // Full: the smallest waiting reward makes room, unless the newcomer is the smallest.
                int low = 0;
                for (int i = 1; i < _queue.Count; i++) if (Rank(_queue[i].Kind) < Rank(_queue[low].Kind)) low = i;
                Dropped++;
                if (Rank(_queue[low].Kind) >= Rank(kind)) return;
                _queue.RemoveAt(low);
            }
            _queue.Add(new Waiting { Kind = kind, At = now });
        }

        /// <summary>Call every frame. Returns true and the reward to play now, at most one per call.</summary>
        public bool Poll(double now, out RewardKind kind)
        {
            kind = RewardKind.PointsTick;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (now - _queue[i].At > Patience(_queue[i].Kind))
                {
                    _queue.RemoveAt(i);
                    Dropped++;
                }
            }
            if (_queue.Count == 0 || now < _freeAt) return false;

            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if (Rank(_queue[i].Kind) > Rank(_queue[best].Kind)) best = i;
            }
            kind = _queue[best].Kind;
            _queue.RemoveAt(best);
            _freeAt = now + Gap(kind);
            if (kind == RewardKind.PointsTick)
            {
                _tickStep = now - _lastTick > 0.6 ? 0 : _tickStep + 1;
                _lastTick = now;
            }
            return true;
        }

        public void Clear()
        {
            _queue.Clear();
            _freeAt = 0;
            _tickStep = 0;
        }
    }
}
