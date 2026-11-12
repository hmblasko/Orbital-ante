using System;

namespace OrbitalAnte.Weapons
{
    /// <summary>The plain numbers a gun needs to work out when it can fire. No Unity types.</summary>
    public struct FirearmSpec
    {
        /// <summary>True for hold-to-fire, false for one shot per trigger pull.</summary>
        public bool Automatic;
        public float ShotsPerSecond;
        public int MagazineSize;
        public float ReloadSeconds;

        /// <summary>Aim error in degrees for the first shot.</summary>
        public float BaseSpread;
        /// <summary>Aim error added by each shot.</summary>
        public float SpreadPerShot;
        /// <summary>The most aim error sustained fire can build up, on top of the base.</summary>
        public float MaxBloom;
        /// <summary>How fast built-up aim error settles, in degrees per second.</summary>
        public float BloomRecovery;

        /// <summary>Multipliers when firing with one hand instead of two.</summary>
        public float OneHandSpreadMultiplier;
        public float OneHandRecoilMultiplier;
        public float OneHandReloadMultiplier;
        /// <summary>The toggle: can a gun be reloaded with only one hand at all?</summary>
        public bool OneHandCanReload;
    }

    public struct FireResult
    {
        /// <summary>A shot left the barrel.</summary>
        public bool Fired;
        /// <summary>The trigger was pulled on an empty magazine (play a click).</summary>
        public bool DryFire;
        /// <summary>Aim error for this shot, in degrees.</summary>
        public float SpreadDegrees;
        /// <summary>Multiplier for recoil on this shot (above 1 when one-handed).</summary>
        public float RecoilScale;
    }

    /// <summary>
    /// Pure rules for one gun: fire rate, magazine, reload, the spread that builds up under sustained fire, and
    /// what changes when the shooter has one hand or none. No Unity types, so it can be unit tested anywhere.
    /// The player's gun and the enemies' guns both run on this, so they obey exactly the same rules.
    ///
    /// Rules (Milestone 3):
    /// - Two hands: normal.
    /// - One hand: much more spread and recoil, slower reload (or no reload at all, by a toggle).
    /// - No hands: cannot fire or reload.
    /// - Ammo in reserve is unlimited; the magazine is not.
    /// </summary>
    public sealed class FirearmState
    {
        readonly FirearmSpec _spec;
        float _cooldown;
        float _reloadLeft;
        float _reloadTotal;

        public FirearmState(FirearmSpec spec, int startingAmmo = -1)
        {
            if (spec.MagazineSize < 1) throw new ArgumentException("A magazine holds at least one round.", nameof(spec));
            if (spec.ShotsPerSecond <= 0f) throw new ArgumentException("Fire rate must be above zero.", nameof(spec));
            _spec = spec;
            Ammo = startingAmmo < 0 ? spec.MagazineSize : Math.Min(startingAmmo, spec.MagazineSize);
        }

        public FirearmSpec Spec => _spec;
        public int Ammo { get; private set; }
        public bool Reloading => _reloadLeft > 0f;
        /// <summary>0 at the start of a reload, 1 when it finishes. 0 when not reloading.</summary>
        public float ReloadProgress => Reloading && _reloadTotal > 0f ? 1f - _reloadLeft / _reloadTotal : 0f;
        /// <summary>Extra aim error built up by recent shots, in degrees.</summary>
        public float Bloom { get; private set; }
        /// <summary>Seconds until the next shot is allowed.</summary>
        public float Cooldown => _cooldown;

        static int ClampHands(int hands) => hands < 0 ? 0 : hands > 2 ? 2 : hands;

        /// <summary>Aim error right now for a shooter with this many usable hands.</summary>
        public float SpreadFor(int hands)
        {
            float spread = _spec.BaseSpread + Bloom;
            return ClampHands(hands) == 1 ? spread * Math.Max(1f, _spec.OneHandSpreadMultiplier) : spread;
        }

        public float RecoilScaleFor(int hands)
        {
            return ClampHands(hands) == 1 ? Math.Max(1f, _spec.OneHandRecoilMultiplier) : 1f;
        }

        public bool CanReload(int hands)
        {
            hands = ClampHands(hands);
            if (hands == 0) return false;
            if (hands == 1 && !_spec.OneHandCanReload) return false;
            return !Reloading && Ammo < _spec.MagazineSize;
        }

        /// <summary>How long a reload would take for this many hands, in seconds.</summary>
        public float ReloadSecondsFor(int hands)
        {
            return ClampHands(hands) == 1
                ? _spec.ReloadSeconds * Math.Max(1f, _spec.OneHandReloadMultiplier)
                : _spec.ReloadSeconds;
        }

        /// <summary>Advances timers. Call once per frame.</summary>
        /// <returns>True on the frame a reload completes.</returns>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;
            // Between shots of a burst the built-up spread does not settle, so holding the trigger really
            // does make the gun wander. It only settles in the time left over once the gun has gone quiet.
            float quiet = dt;
            if (_cooldown > 0f)
            {
                quiet = Math.Max(0f, dt - _cooldown);
                _cooldown = Math.Max(0f, _cooldown - dt);
            }
            if (quiet > 0f && Bloom > 0f) Bloom = Math.Max(0f, Bloom - _spec.BloomRecovery * quiet);

            if (_reloadLeft > 0f)
            {
                _reloadLeft -= dt;
                if (_reloadLeft <= 0f)
                {
                    _reloadLeft = 0f;
                    Ammo = _spec.MagazineSize;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pulls the trigger.
        /// </summary>
        /// <param name="hands">Usable hands on the gun: 0, 1 or 2.</param>
        /// <param name="triggerHeld">The trigger is down this frame.</param>
        /// <param name="triggerPressed">The trigger went down this frame (needed for non-automatic guns).</param>
        public FireResult TryFire(int hands, bool triggerHeld, bool triggerPressed)
        {
            var result = new FireResult();
            hands = ClampHands(hands);
            if (hands == 0 || Reloading) return result;

            bool wants = _spec.Automatic ? (triggerHeld || triggerPressed) : triggerPressed;
            if (!wants || _cooldown > 0f) return result;

            if (Ammo <= 0)
            {
                // Only click once per pull, not every frame the trigger is held.
                result.DryFire = triggerPressed;
                return result;
            }

            result.Fired = true;
            result.SpreadDegrees = SpreadFor(hands);
            result.RecoilScale = RecoilScaleFor(hands);

            Ammo--;
            _cooldown = 1f / _spec.ShotsPerSecond;
            Bloom = Math.Min(Math.Max(0f, _spec.MaxBloom), Bloom + _spec.SpreadPerShot);
            return result;
        }

        /// <summary>Starts a reload if this many hands can do one. Returns true if it started.</summary>
        public bool StartReload(int hands)
        {
            if (!CanReload(hands)) return false;
            _reloadTotal = ReloadSecondsFor(hands);
            _reloadLeft = _reloadTotal;
            if (_reloadLeft <= 0f)
            {
                Ammo = _spec.MagazineSize;
                _reloadLeft = 0f;
            }
            return true;
        }

        /// <summary>Abandons a reload in progress (the gun was dropped or knocked away). The magazine keeps what it had.</summary>
        public void CancelReload()
        {
            _reloadLeft = 0f;
        }

        /// <summary>
        /// Called when the number of hands on the gun changes mid-reload: losing the last spare hand stops a
        /// reload that one hand is not allowed to do.
        /// </summary>
        public void HandsChanged(int hands)
        {
            hands = ClampHands(hands);
            if (!Reloading) return;
            if (hands == 0 || (hands == 1 && !_spec.OneHandCanReload)) CancelReload();
        }
    }
}
