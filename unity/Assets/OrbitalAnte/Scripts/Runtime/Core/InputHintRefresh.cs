namespace OrbitalAnte.Core
{
    /// <summary>
    /// Keeps control hints current without rebuilding text on every device event. Time is unscaled seconds.
    /// Saved binding changes bypass the device limit; periodic refresh also catches keyboard-layout changes.
    /// </summary>
    public sealed class InputHintRefresh
    {
        const double PeriodicInterval = 2.0;
        const double DeviceInterval = 0.25;
        bool _initialized;
        bool _shownGamepad;
        int _shownRevision;
        double _nextPeriodic;
        double _nextDevice;

        public bool TryRefresh(double now, int bindingRevision, bool gamepad)
        {
            if (_initialized && bindingRevision == _shownRevision && now < _nextPeriodic
                && (gamepad == _shownGamepad || now < _nextDevice)) return false;

            _initialized = true;
            _shownRevision = bindingRevision;
            _shownGamepad = gamepad;
            _nextPeriodic = now + PeriodicInterval;
            _nextDevice = now + DeviceInterval;
            return true;
        }

        /// <summary>A newly shown view needs its hint even if its previous refresh was recent.</summary>
        public void Invalidate() => _initialized = false;
    }
}
