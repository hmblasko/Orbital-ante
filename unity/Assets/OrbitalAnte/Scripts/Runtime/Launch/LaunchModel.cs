using System;

namespace OrbitalAnte.Launch
{
    public enum EngineState
    {
        /// <summary>Engines cold.</summary>
        Off,
        /// <summary>Lit and building up, the ship still held down by its own weight. Only happens on the ground.</summary>
        Igniting,
        /// <summary>Giving whatever the pilot asks for.</summary>
        Running,
    }

    /// <summary>The numbers behind a lift-off. Held in the LaunchConfig asset.</summary>
    [Serializable]
    public sealed class LaunchRules
    {
        /// <summary>Seconds from pressing "up" on the ground to the ship leaving it. Short: it is a build-up, not a wait.</summary>
        public float ignitionSeconds = 1.1f;
        /// <summary>The build-up when the engines are still hot (a hop, a second try). Barely a pause.</summary>
        public float hotIgnitionSeconds = 0.35f;
        /// <summary>How long after cut-off the engines still count as hot, in seconds.</summary>
        public float hotSeconds = 3f;
        /// <summary>How much harder the cabin shakes at the instant the ship is let go (1 = no kick).</summary>
        public float releaseKick = 1.5f;
        /// <summary>How long that kick lasts, in seconds.</summary>
        public float kickSeconds = 0.4f;
        /// <summary>The share of full push the engines give while building up. Not enough to lift the ship.</summary>
        public float ignitionPush = 0.12f;
        /// <summary>Metres above the ground at which the ground stops mattering (no more kicked-up smoke, echo or shake from it).</summary>
        public float groundEffectHeight = 45f;
        /// <summary>How quickly the roar dies when the engines are cut, in seconds.</summary>
        public float fadeSeconds = 0.35f;
        /// <summary>Camera shake at full push right on the ground, in degrees.</summary>
        public float shakeOnGround = 1.1f;
        /// <summary>Camera shake at full push in open space, in degrees. Small: enough to feel the engines.</summary>
        public float shakeInSpace = 0.05f;
        /// <summary>Lumps of smoke a second at full push on the ground, in thick air.</summary>
        public float smokePerSecond = 34f;
        /// <summary>Bits of grit thrown out at the moment of ignition.</summary>
        public int debrisAtIgnition = 26;
        /// <summary>Bits of grit a second while the engines blast the ground.</summary>
        public float debrisPerSecond = 9f;
    }

    /// <summary>
    /// The rules of the ship's main engines as you feel them: the short build-up before it leaves the ground, and
    /// how much smoke, grit, light, shake and noise each moment deserves. Plain C#, no Unity types.
    ///
    /// The ground is what makes a launch violent: the same engines that shake the cabin on the pad are a steady
    /// hum in open space. Everything here follows from three things: how hard the engines are pushing, how close
    /// the ground is, and how much air there is to carry smoke and sound.
    /// </summary>
    public sealed class LaunchModel
    {
        public readonly LaunchRules Rules;
        public EngineState State { get; private set; } = EngineState.Off;

        /// <summary>How hard the engines are burning, 0 to 1. This is what you see and hear; it lingers a moment after cut-off.</summary>
        public float Burn { get; private set; }
        /// <summary>The share of the pilot's upward push that actually reaches the ship, 0 to 1.</summary>
        public float LiftGate { get; private set; } = 1f;
        /// <summary>How far through the build-up, 0 to 1. 0 when not igniting.</summary>
        public float Ignition { get; private set; }
        /// <summary>How much the ground matters right now: 1 standing on it, 0 above <see cref="LaunchRules.groundEffectHeight"/>.</summary>
        public float GroundEffect { get; private set; }
        /// <summary>True for the one step in which the engines light on the ground.</summary>
        public bool JustIgnited { get; private set; }
        /// <summary>True for the one step in which the build-up ends and the ship is let go.</summary>
        public bool JustReleased { get; private set; }
        /// <summary>True for the one step in which the pilot lets go before the build-up ends.</summary>
        public bool JustCancelled { get; private set; }
        /// <summary>The kick of being let go: 1 at release, fading to 0.</summary>
        public float Kick { get; private set; }

        float _sinceRan = 1e9f;
        float _buildUp = 1f;

        /// <summary>Camera shake, in degrees.</summary>
        public float Shake { get; private set; }
        /// <summary>Lumps of smoke to let out a second.</summary>
        public float SmokeRate { get; private set; }
        /// <summary>Bits of grit to throw out a second (the ignition burst is separate: see <see cref="JustIgnited"/>).</summary>
        public float DebrisRate { get; private set; }
        /// <summary>Brightness of the engine glow on the ground and on the ship's belly, 0 to 1.</summary>
        public float GroundGlow { get; private set; }
        /// <summary>Strength of the heat shimmer under the ship, 0 to 1. Needs air.</summary>
        public float Shimmer { get; private set; }
        /// <summary>Loudness of the deep roar, 0 to 1.</summary>
        public float Roar { get; private set; }
        /// <summary>Loudness of the crackle on top (the sound bouncing back off the ground through air), 0 to 1.</summary>
        public float Crackle { get; private set; }

        public LaunchModel(LaunchRules rules = null)
        {
            Rules = rules ?? new LaunchRules();
        }

        /// <param name="dt">Seconds since the last call.</param>
        /// <param name="up">How hard the pilot is asking to go up, 0 to 1.</param>
        /// <param name="boost">True while the pilot holds the boost.</param>
        /// <param name="onGround">True when the ship is standing still on something. While igniting: true while it still touches.
        /// A ship that is bouncing or scraping is not "on the ground": its engines answer at once.</param>
        /// <param name="height">Metres from the engines to the ground below, or a large number when there is none.</param>
        /// <param name="air">How thick the air is, 0 (space, the Moon) to 1 (Earth at sea level) and above.</param>
        /// <param name="engineHealth">The share of push the engines can still give, 0 to 1.</param>
        public void Tick(float dt, float up, bool boost, bool onGround, float height, float air, float engineHealth)
        {
            JustIgnited = false;
            JustReleased = false;
            JustCancelled = false;
            if (dt <= 0f) return;
            if (State == EngineState.Off) _sinceRan += dt; else _sinceRan = 0f;
            Kick = Rules.kickSeconds > 0.01f ? Math.Max(0f, Kick - dt / Rules.kickSeconds) : 0f;
            up = Clamp01(up);
            air = Clamp01(air);
            engineHealth = Clamp01(engineHealth);
            bool asking = up > 0.05f;

            switch (State)
            {
                case EngineState.Off:
                    if (asking)
                    {
                        _buildUp = _sinceRan < Rules.hotSeconds ? Rules.hotIgnitionSeconds : Rules.ignitionSeconds;
                        if (onGround && _buildUp > 0.01f)
                        {
                            State = EngineState.Igniting;
                            Ignition = 0f;
                            JustIgnited = true;
                        }
                        else State = EngineState.Running;
                    }
                    break;
                case EngineState.Igniting:
                    if (!asking)
                    {
                        // Let go early and it simply winds down. Nothing is lost.
                        State = EngineState.Off;
                        Ignition = 0f;
                        JustCancelled = true;
                    }
                    else
                    {
                        Ignition += dt / _buildUp;
                        if (Ignition >= 1f || !onGround)
                        {
                            Ignition = 0f;
                            State = EngineState.Running;
                            JustReleased = true;
                            Kick = 1f;
                        }
                    }
                    break;
                case EngineState.Running:
                    if (!asking) State = EngineState.Off;
                    break;
            }

            float wanted;
            if (State == EngineState.Igniting)
            {
                // The burn climbs through the build-up, ahead of the push, so you see and hear it coming.
                // It tops out where a normal burn sits, so letting go is a step up, never a step down.
                // It also follows how hard the pilot is asking, as a running engine does: pushing up and forward at once
                // asks for less "up", and the build-up must still end at or below what follows it.
                wanted = up * (0.25f + 0.55f * Ignition * Ignition);
                LiftGate = Rules.ignitionPush;
            }
            else if (State == EngineState.Running)
            {
                wanted = up * (boost ? 1f : 0.8f);
                LiftGate = 1f;
            }
            else
            {
                wanted = 0f;
                LiftGate = 1f;
            }
            wanted *= 0.35f + 0.65f * engineHealth;

            float rate = dt / Math.Max(0.02f, wanted > Burn ? 0.08f : Rules.fadeSeconds);
            Burn = wanted > Burn ? Math.Min(wanted, Burn + rate) : Math.Max(wanted, Burn - rate);

            float reach = Math.Max(1f, Rules.groundEffectHeight);
            float near = Clamp01(1f - Math.Max(0f, height) / reach);
            GroundEffect = near * near;

            Shake = Burn * (Rules.shakeInSpace + (Rules.shakeOnGround - Rules.shakeInSpace) * GroundEffect)
                * (1f + (Math.Max(1f, Rules.releaseKick) - 1f) * Kick);
            // Smoke is exhaust and dust held up by air. With no air, grit still flies but nothing billows.
            SmokeRate = Burn * GroundEffect * Rules.smokePerSecond * (0.15f + 0.85f * air);
            DebrisRate = Burn * GroundEffect * GroundEffect * Rules.debrisPerSecond;
            GroundGlow = Burn * (0.35f + 0.65f * GroundEffect);
            Shimmer = Burn * GroundEffect * air;
            // You are inside the ship: the roar comes through its frame whatever is outside.
            // (The steady engine note in flight is the audio system's; this is the extra weight the ground gives it.)
            Roar = Burn * (0.15f + 0.85f * GroundEffect);
            Crackle = Burn * GroundEffect * air;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
