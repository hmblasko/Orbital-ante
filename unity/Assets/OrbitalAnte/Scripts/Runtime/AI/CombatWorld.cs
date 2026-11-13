using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// What enemies need to know about the world they are fighting in: where the player is, where dropped
    /// things should live. The arena fills this in when it is built; its squad owns tactical cover queries.
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

        /// <summary>True when there is a living player for enemies to react to.</summary>
        public static bool PlayerPresent => PlayerEye != null && (Player == null || Player.Alive);

        public static void Reset()
        {
            PlayerEye = null;
            Player = null;
            DropParent = null;
            EnemiesHoldFire = false;
        }
    }
}
