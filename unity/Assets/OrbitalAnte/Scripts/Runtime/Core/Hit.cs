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
        /// <summary>Kills a living character wherever it lands (a one-hit-kill gun).</summary>
        public bool Lethal;
        /// <summary>A cut: an arm or leg part comes off whatever hit points it has left (see BodyModel.ApplySever).</summary>
        public bool Sever;
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
        /// <summary>The part hit was a head (whether or not it killed).</summary>
        public bool Head;
        /// <summary>
        /// Where on a creature the hit landed (its armour, its one weak place, or a piece it fights with coming off),
        /// for the hit marker and its sound. Left Plain for people and everything else.
        /// </summary>
        public HitZone Zone;
        /// <summary>
        /// How big a moment this was, from 0 (nothing came off) to 1 (a kill). Drives hitstop and camera shake.
        /// </summary>
        public float Juice;
        /// <summary>Short label for the debug readout, for example "Left Shin".</summary>
        public string Label;
        /// <summary>
        /// When above zero, the freeze this hit asks for, in seconds, in place of the one worked out from
        /// <see cref="Juice"/>. Melee swings set it (capped, and shorter when one swing hits several).
        /// </summary>
        public float HitstopSeconds;
        /// <summary>
        /// What was hit (the Mannequin for a body part, the creature for any piece of a creature), for systems that need
        /// to know who: the ante tasks credit kills to the enemy's seeded id, or to the creature's own claim
        /// (<c>Creatures.ICreature</c>). Null for things that do not say.
        /// </summary>
        public Component Target;
        /// <summary>What the player hit it with. Set where the hit is raised.</summary>
        public HitSource Source;
        /// <summary>
        /// The gun that fired, for a gun hit (its <c>WeaponData.Id</c>), set when the shot is made. Read this, not the
        /// player's current gun: a kill can hand over the next ladder gun before other listeners hear the hit (Codex, PR 77).
        /// </summary>
        public string SourceId;
    }

    /// <summary>What a player's hit came from: a gun (the default), a melee swing, or the rover running someone over.</summary>
    public enum HitSource : byte { Gun = 0, Melee = 1, Rover = 2 }

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
