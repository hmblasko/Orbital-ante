using System;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// The dials for how enemies use cover and flank (Matthew, 16 Nov 2026: "enemies try to take cover" and
    /// "try to flank"). Held on the EnemyConfig asset (<c>tactics</c>); these are the code defaults.
    /// Plain C#, no engine.
    /// </summary>
    [Serializable]
    public sealed class TacticsRules
    {
        /// <summary>Enemies in a fight go to cover and shoot from it. Off: the old side-stepping in the open.</summary>
        public bool cover = true;
        /// <summary>Squads of two or more send members round the player's side. Off: everyone fights from the front.</summary>
        public bool flank = true;

        /// <summary>How far from itself (metres) an enemy looks for cover.</summary>
        public float coverSearch = 13f;
        /// <summary>How far an enemy that has lost a leg will drag itself for cover.</summary>
        public float crawlCoverSearch = 5f;
        /// <summary>Seconds hidden behind cover between looks out (a range: shortest, longest).</summary>
        public float hideMin = 1.0f, hideMax = 2.0f;
        /// <summary>Seconds out of cover shooting before ducking back (a range).</summary>
        public float peekMin = 1.6f, peekMax = 2.8f;
        /// <summary>A spot is given up once the player has moved this far from where they were when it was chosen.</summary>
        public float threatMoved = 6f;
        /// <summary>Seconds a cover spot is kept at most before a better one is looked for.</summary>
        public float keepSpot = 9f;
        /// <summary>Seconds an enemy in cover keeps waiting for the player to show again before it comes looking.</summary>
        public float waitInCover = 4f;
        /// <summary>Two of a squad never take spots closer together than this (metres).</summary>
        public float spacing = 2.2f;

        /// <summary>Seconds a squad fights face on before anyone is sent round the side, so first contact is readable.</summary>
        public float flankAfter = 2.5f;
        /// <summary>How far round the player (degrees from the squad's side) a flanker heads.</summary>
        public float flankAngle = 80f;
        /// <summary>Seconds a flank may take before the flanker gives up and fights from where it is.</summary>
        public float flankGiveUp = 14f;
        /// <summary>Seconds before a flanker who fell is replaced by another member.</summary>
        public float flankRefill = 5f;

        /// <summary>Whether a kind uses cover at all. A kind that walks you down (closes in without rushing: the
        /// Bruiser) stays the squad's frontal pressure and never hides.</summary>
        public static bool UsesCover(bool holdsStill, bool closesIn, bool rushes) => holdsStill || !closesIn || rushes;

        /// <summary>Whether a kind may be sent round the side. Snipers (hold still) and walkers-down never are.</summary>
        public static bool CanFlank(bool holdsStill, bool closesIn, bool rushes) => !holdsStill && (!closesIn || rushes);

        /// <summary>How keen a kind is to flank: quick ones that rush first.</summary>
        public static int Eagerness(bool rushes) => rushes ? 2 : 1;
    }

    /// <summary>What a possible cover spot looks like from where an enemy stands, measured by the engine side.</summary>
    public struct CoverCandidate
    {
        /// <summary>Metres to walk to get there.</summary>
        public float Travel;
        /// <summary>Metres from the spot to the player.</summary>
        public float ToThreat;
        /// <summary>The player cannot see the spot at chest height.</summary>
        public bool ChestHidden;
        /// <summary>The player cannot see the spot at head height either (full cover).</summary>
        public bool HeadHidden;
        /// <summary>There is somewhere to shoot from: standing up over it, or a short step to the side.</summary>
        public bool CanPeek;
        /// <summary>Degrees between the way the spot lies from the player and the way the enemy wants to be (0 when it does not care).</summary>
        public float OffBearing;
        /// <summary>A squadmate already has a spot here.</summary>
        public bool Taken;
    }

    /// <summary>How cover spots are compared. Lower is better; <see cref="Rejected"/> means never.</summary>
    public static class CoverScore
    {
        public const float Rejected = float.PositiveInfinity;

        /// <param name="needsPeek">False for those that only want to hide (no gun, or dragging itself on the ground).</param>
        /// <param name="keepNear">The closest this kind likes to fight.</param>
        /// <param name="keepFar">The furthest this kind likes to fight.</param>
        public static float Of(in CoverCandidate c, bool needsPeek, float keepNear, float keepFar)
        {
            if (!c.ChestHidden || c.Taken) return Rejected;
            if (needsPeek && !c.CanPeek) return Rejected;
            // Never hide right in the player's face: that is not cover, it is a gift.
            if (c.ToThreat < Math.Max(2.5f, keepNear * 0.6f)) return Rejected;

            float score = c.Travel;
            // Full cover is worth a few metres' walk more than a low wall.
            if (!c.HeadHidden) score += 3f;
            // Outside its liked fighting distance costs as if it were further to walk.
            if (needsPeek)
            {
                if (c.ToThreat < keepNear) score += (keepNear - c.ToThreat) * 1.5f;
                else if (c.ToThreat > keepFar) score += (c.ToThreat - keepFar) * 0.5f;
            }
            // A flanker wants spots round the side, not the nearest rock.
            score += c.OffBearing * 0.12f;
            return score;
        }
    }

    /// <summary>The hide-and-look-out rhythm of an enemy fighting from cover. Plain C#.</summary>
    public static class CoverRhythm
    {
        /// <summary>
        /// Whether it should be out shooting now. It hides while reloading or empty, and when every one of the
        /// squad's turns to shoot is already taken (so others shoot while it waits, and the squad takes turns).
        /// </summary>
        public static bool Peeking(bool phaseSaysPeek, bool reloading, bool squadTurnsAllTaken)
        {
            if (reloading) return false;
            if (phaseSaysPeek && squadTurnsAllTaken) return false;
            return phaseSaysPeek;
        }
    }
}
