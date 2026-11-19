using System;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// The one thing a Faction A tier does that nobody else does (Milestone 13). Each has a tell the player can
    /// learn and a way to stop it, and none of them is extra health.
    /// </summary>
    public enum FactionAbility
    {
        None,
        /// <summary>The Scout: on first sight it raises its free arm and fires a flare that brings its own side near by to look.</summary>
        Flare,
        /// <summary>The Line: while a squadmate is out on a flank it holds the player down with long, wide bursts.</summary>
        Suppress,
        /// <summary>The Heavy: plants itself and spins its gun up before every burst, turning slowly while braced.</summary>
        SpinUp,
    }

    /// <summary>
    /// The factions, by placeholder only (never a name until Matthew approves one). Faction A is
    /// Soviet-spacesuit inspired; B and C come in Phase 2's last milestone.
    /// </summary>
    public static class Factions
    {
        /// <summary>Not in any faction: the four kinds built before there were factions.</summary>
        public const string None = "";
        public const string A = "A";

        public static bool Same(string a, string b) => string.Equals(a ?? None, b ?? None, StringComparison.Ordinal);
    }

    /// <summary>Where a faction lives and how widely it is spread. Plain numbers, on the enemy config asset.</summary>
    [Serializable]
    public sealed class FactionFieldRules
    {
        /// <summary>The faction's home world (a body id). Every camp and every guard there is the faction's.</summary>
        public string homeA = "Venus";
        /// <summary>On every other world, this share of camps (0 to 1) is a Faction A squad. The rest are unaligned.</summary>
        public float shareA = 0.3f;
        /// <summary>
        /// Faction A's band colour, multiplied into the helmet and chest so a Faction A squad can be told from an
        /// unaligned one while its tiers still wear the unaligned bodies (colour for the side, shape for the kind).
        /// </summary>
        public float bandRed = 0.95f, bandGreen = 0.62f, bandBlue = 0.55f;
    }

    /// <summary>
    /// The Scout's signal flare. On first sight of the player (its squad not already roused) it raises its free
    /// arm for <see cref="raiseSeconds"/> with the hand glowing, then fires; the flare hangs where it burst and
    /// every Faction A enemy awake within <see cref="wakeRadius"/> hears it (camps not yet woken do not), which gives them a place, never a live track. Killing
    /// it, taking its hand or forearm, or knocking its gun away during the raise stops it. One flare a life.
    /// </summary>
    [Serializable]
    public sealed class FlareRules
    {
        public float raiseSeconds = 1.0f;
        public float wakeRadius = 150f;
        public float hangSeconds = 8f;
        /// <summary>How high above the Scout the flare bursts, metres.</summary>
        public float burstHeight = 30f;
        /// <summary>Seconds those who heard it keep going to look where the player was (longer than an ordinary sound).</summary>
        public float memory = 20f;
    }

    public enum FlareStep { Ready, Raising, Fired, Spent }

    /// <summary>One Scout's flare, over its life. Plain C#.</summary>
    public sealed class FlareState
    {
        public FlareStep Step { get; private set; } = FlareStep.Ready;
        float _firesAt;

        /// <summary>Starts the raise, if there is still a flare and the squad has not been roused already. True if it started.</summary>
        public bool Begin(float now, FlareRules rules, bool squadAlready)
        {
            if (Step != FlareStep.Ready || squadAlready) return false;
            Step = FlareStep.Raising;
            _firesAt = now + Math.Max(0f, rules.raiseSeconds);
            return true;
        }

        /// <summary>
        /// Moves the raise on. True only on the moment the flare goes up. <paramref name="canSignal"/> is false
        /// once the raising hand or forearm is gone, the gun is lost or the Scout is dead: that spends the flare.
        /// </summary>
        public bool Tick(float now, bool canSignal)
        {
            if (Step != FlareStep.Raising) return false;
            if (!canSignal)
            {
                Step = FlareStep.Spent;
                return false;
            }
            if (now < _firesAt) return false;
            Step = FlareStep.Fired;
            return true;
        }

        public bool Raising => Step == FlareStep.Raising;

        /// <summary>0 to 1: how far through the raise it is, for the arm and the glowing hand.</summary>
        public float RaiseAmount(float now, FlareRules rules)
        {
            if (Step != FlareStep.Raising) return 0f;
            float total = Math.Max(0.01f, rules.raiseSeconds);
            return Math.Max(0f, Math.Min(1f, 1f - (_firesAt - now) / total));
        }

        /// <summary>Whether a camp this far from the flare hears it.</summary>
        public static bool Wakes(double distance, FlareRules rules) => distance <= rules.wakeRadius;
    }

    /// <summary>
    /// The Heavy's gun: it plants its feet and spins up for <see cref="spinSeconds"/> (the gun's cell glows
    /// brighter and it whines), then fires for <see cref="burstSeconds"/>. While braced it turns no faster than
    /// <see cref="bracedTurn"/> degrees a second and does not step about, so circling it close in, or breaking
    /// line of sight, is the answer. Losing the gun stops the spin (the next burst starts from cold); losing a
    /// leg puts it on the ground for good, and a Heavy on the ground cannot brace, so it never fires again.
    /// It does not wait for its squad's turns to shoot: the spin-up is its warning instead.
    /// </summary>
    [Serializable]
    public sealed class SpinUpRules
    {
        public float spinSeconds = 1.2f;
        public float burstSeconds = 3f;
        public float bracedTurn = 20f;
    }

    public enum SpinStep { Cold, Spinning, Firing }

    /// <summary>One Heavy's spin, plain C#.</summary>
    public sealed class SpinUpState
    {
        public SpinStep Step { get; private set; } = SpinStep.Cold;
        float _readyAt, _stopAt;

        /// <summary>Wants to shoot: starts spinning from cold. Returns true while it may fire.</summary>
        public bool Want(float now, SpinUpRules rules)
        {
            switch (Step)
            {
                case SpinStep.Cold:
                    Step = SpinStep.Spinning;
                    _readyAt = now + Math.Max(0f, rules.spinSeconds);
                    return false;
                case SpinStep.Spinning:
                    if (now < _readyAt) return false;
                    Step = SpinStep.Firing;
                    _stopAt = now + Math.Max(0.1f, rules.burstSeconds);
                    return true;
                default:
                    if (now < _stopAt) return true;
                    // The burst is done: it winds down and has to spin up again.
                    Step = SpinStep.Cold;
                    return false;
            }
        }

        /// <summary>Stops wanting (lost sight, lost a leg, lost the gun): winds down at once.</summary>
        public void Stop() => Step = SpinStep.Cold;

        /// <summary>Planted and turning slowly: spinning up or firing.</summary>
        public bool Braced => Step != SpinStep.Cold;

        /// <summary>0 to 1: how bright the gun's cell is (rises through the spin, full while firing).</summary>
        public float Glow(float now, SpinUpRules rules)
        {
            switch (Step)
            {
                case SpinStep.Spinning:
                    float total = Math.Max(0.01f, rules.spinSeconds);
                    return Math.Max(0f, Math.Min(1f, 1f - (_readyAt - now) / total));
                case SpinStep.Firing:
                    return 1f;
                default:
                    return 0f;
            }
        }

        /// <summary>The most it may turn this step, in degrees (unlimited when not braced).</summary>
        public float MaxTurn(float dt, SpinUpRules rules) => Braced ? rules.bracedTurn * dt : float.PositiveInfinity;
    }

    /// <summary>
    /// The Line's covering fire: while a squadmate is out on a flank, a Line with nothing better to do fires
    /// long bursts at where the player was last seen, wider than its aimed fire, without waiting for a turn.
    /// It is never silent: the long, unbroken burst is the tell that someone is coming round the side.
    /// </summary>
    [Serializable]
    public sealed class SuppressRules
    {
        public float spreadScale = 1.5f;
        public float burstSeconds = 2.4f;
        public float pauseSeconds = 0.8f;
    }
}
