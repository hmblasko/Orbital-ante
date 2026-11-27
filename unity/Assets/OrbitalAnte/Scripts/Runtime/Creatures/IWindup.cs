using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// A creature whose warning the visor shows (Ralph loop pass 2, 27 Nov 2026; UI/WindupCues): each creature's Unity
    /// class. It raises <see cref="AI.CombatWorld.CreatureWindup"/> once as a warning begins, and the visor then reads
    /// these two every frame until the warning is over.
    /// </summary>
    public interface IWindup
    {
        /// <summary>Where the danger is now, in world space: the head, the mouth or the body that will strike.</summary>
        Vector3 WindupPoint { get; }

        /// <summary>How far its warning has got now (<see cref="Windups"/>). None once it is over, and whenever it is dead or put away.</summary>
        WindupStage Windup { get; }
    }
}
