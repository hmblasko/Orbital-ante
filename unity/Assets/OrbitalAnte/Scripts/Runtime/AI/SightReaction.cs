using System;

namespace OrbitalAnte.AI
{
    /// <summary>Deferred sensing is not confirmed loss. Freshness still gates firing separately.</summary>
    public sealed class SightReaction
    {
        bool _tracking;
        double _readyAt;
        public void ConfirmVisible(double now, double reactionSeconds)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            if (!_tracking) _readyAt = now + Math.Max(0, reactionSeconds);
            _tracking = true;
        }
        public void ConfirmHidden() => _tracking = false;
        public bool Ready(double now) => _tracking && now >= _readyAt;
    }
}
