using System;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>Everything a target needs to know about one bullet hit.</summary>
    public struct HitInfo
    {
        public float Damage;
        public Vector3 Point;
        /// <summary>Normalised travel direction of the bullet.</summary>
        public Vector3 Direction;
        public Vector3 Normal;
    }

    /// <summary>What happened when something was hit, so the shooter can give feedback.</summary>
    public struct HitOutcome
    {
        public bool Hit;
        /// <summary>A part came off.</summary>
        public bool Severed;
        public bool Killed;
        /// <summary>The kill came from destroying a head that was still on its body.</summary>
        public bool HeadshotKill;
        /// <summary>A weapon was knocked out of someone's hands by this hit.</summary>
        public bool Disarmed;
        /// <summary>The thing hit was a living enemy's body (not a ship, a rock or a loose piece).</summary>
        public bool Enemy;
        /// <summary>
        /// How big a moment this was, from 0 (nothing came off) to 1 (a kill). Drives hitstop and camera shake.
        /// </summary>
        public float Juice;
        /// <summary>Short label for the debug readout, for example "Left Shin".</summary>
        public string Label;
    }

    public interface IHittable
    {
        HitOutcome Hit(in HitInfo hit);
        /// <summary>Text for the "what am I aiming at" debug readout.</summary>
        string DescribeForDebug();
    }

    /// <summary>Loose coupling between combat and feedback (HUD, hitstop, camera shake).</summary>
    public static class CombatEvents
    {
        public static event Action<HitOutcome> PlayerHitSomething;

        public static void RaisePlayerHit(HitOutcome outcome) => PlayerHitSomething?.Invoke(outcome);

        public static void ResetStatics() => PlayerHitSomething = null;
    }
}
