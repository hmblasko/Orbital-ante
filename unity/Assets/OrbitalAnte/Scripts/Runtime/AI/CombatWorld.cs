using System;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// What enemies need to know about the world they are fighting in: where the player is, where dropped
    /// things should live, and how to find cover. The arena fills this in when it is built.
    /// </summary>
    public static class CombatWorld
    {
        /// <summary>The player's camera. Null when there is no player (menus, some tests).</summary>
        public static Transform PlayerEye;
        public static PlayerHealth Player;
        /// <summary>Parent for weapons lying in the world, so resetting the arena can clear them.</summary>
        public static Transform DropParent;
        /// <summary>When true, enemies keep their guns but do not shoot. A playtest switch.</summary>
        public static bool EnemiesHoldFire;
        /// <summary>Given where a character is and where the threat is, returns a spot out of sight, or null.</summary>
        public static Func<Vector3, Vector3, Vector3?> FindCover;

        /// <summary>How far off an enemy that has not seen the player lately can pick them out, against its sight
        /// range (1 standing; less crouched, prone or keeping still; see Spacewalk/StanceState).</summary>
        public static float PlayerSeenFrom = 1f;
        /// <summary>How far off the player can be heard, against an enemy's hearing (less sneaking, more with jets or a shot).</summary>
        public static float PlayerHeardFrom = 1f;
        /// <summary>How far below the eye enemies aim: less when the player is low, or they would aim into the ground.</summary>
        public static float PlayerAimBelowEye = 0.3f;

        /// <summary>True when there is a living player for enemies to react to.</summary>
        public static bool PlayerPresent => PlayerEye != null && (Player == null || Player.Alive);

        public static void Reset()
        {
            PlayerEye = null;
            Player = null;
            DropParent = null;
            EnemiesHoldFire = false;
            FindCover = null;
            PlayerSeenFrom = 1f;
            PlayerHeardFrom = 1f;
            PlayerAimBelowEye = 0.3f;
        }
    }
}
