using OrbitalAnte.AI;
using OrbitalAnte.Audio;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// A creature whose warning the visor shows (Ralph loop pass 2, 27 Nov 2026; UI/WindupCues): each creature's Unity
    /// class. It raises <see cref="CombatWorld.CreatureWindup"/> each time a warning begins, and the visor then reads
    /// these every frame until the warning is over.
    /// </summary>
    public interface IWindup
    {
        /// <summary>Where the danger is now, in world space: the head, the mouth or the body that will strike.</summary>
        Vector3 WindupPoint { get; }

        /// <summary>How far its warning has got now (<see cref="Windups"/>). None once it is over, and whenever it is dead or put away.</summary>
        WindupStage Windup { get; }

        /// <summary>While it winds up, how near it is to taking aim: 0 as it begins, 1 at the lock. The visor's countdown.</summary>
        float WindupProgress { get; }

        /// <summary>
        /// While it strikes, whether the strike can still reach the player (a scream that still has them in its cone, a
        /// draw-in that still pulls them, a roll still coming their way). False once they are out of its way, so the arc
        /// dims and they know the dodge worked. A strike too quick to dodge part-way through always says true.
        /// </summary>
        bool Threatens { get; }
    }

    /// <summary>The Unity side of a change of warning stage, the same for every creature.</summary>
    public static class WindupTell
    {
        /// <summary>
        /// Does what a change asks for: plays the creature's wind-up, plays its aim (the hard mark) the very frame it takes
        /// aim, and tells the visor once a warning begins. Each creature plays only its own sounds; one that does not play
        /// (a full voice list, say) is never replaced by another's.
        /// </summary>
        public static void Tell(IWindup who, in WindupChange change, string warnSound, string aimSound, Vector3 at)
        {
            if (change.Warn) AudioDirector.Play(warnSound, at);
            if (change.Aim) AudioDirector.Play(aimSound, at);
            if (change.Began) CombatWorld.RaiseCreatureWindup(who);
            // The playtest log (editor and development builds only; null otherwise) counts every warning's stages.
            if (change.Changed && Playtest.PlaytestHooks.Log != null) Playtest.PlaytestHooks.Warning(who, change);
        }
    }
}
