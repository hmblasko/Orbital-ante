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
        /// <summary>
        /// Parent for weapons lying in the world, so resetting the arena can clear them. In the solar system it is
        /// the enemy spawner, which never moves: a loose gun there is moved by the solar system itself (its own
        /// pull), and a parent that moved with a world would move it a second time.
        /// </summary>
        public static Transform DropParent;
        /// <summary>
        /// In the solar system, the world the player is on (null away from any): a loose gun that has come to rest
        /// on ground that is not solid is laid still there and handed to it, so it turns with the ground.
        /// </summary>
        public static Transform GroundParent;
        /// <summary>Raised when the player lets go of a gun, so whoever keeps count of loose guns can count it too.</summary>
        public static event Action<OrbitalAnte.Weapons.Weapon> PlayerDroppedGun;
        public static void RaisePlayerDroppedGun(OrbitalAnte.Weapons.Weapon gun) => PlayerDroppedGun?.Invoke(gun);
        /// <summary>
        /// A signal flare went up (Faction A's Scout): where it was fired, how far it is heard, and where the player
        /// was at that moment. Listeners get a place to go and look, never a live track.
        /// </summary>
        public static event Action<Vector3, float, Vector3, string> Alarm;
        public static void RaiseAlarm(Vector3 at, float radius, Vector3 playerAt, string faction) => Alarm?.Invoke(at, radius, playerAt, faction);
        /// <summary>
        /// An armed enemy has just picked the player out after not having them in sight (critique loop round 1): where
        /// its head is, the beat in seconds before it may fire, and whether the player was hidden until now (nobody
        /// had seen them for that enemy's chase memory, so nobody was hunting them). The visor shows it as an amber arc (UI/ThreatCues). Not cleared by
        /// <see cref="Reset"/>: its listener is the long-lived HUD, which unsubscribes itself.
        /// </summary>
        public static event Action<Vector3, float, bool> PlayerSpotted;
        public static void RaisePlayerSpotted(Vector3 from, float beat, bool hidden) => PlayerSpotted?.Invoke(from, beat, hidden);
        /// <summary>
        /// A squad was sent to look for the player after a long quiet (Enemies/LullRules): where it set off from.
        /// The suit picks up its radio call. Not cleared by <see cref="Reset"/>, as above.
        /// </summary>
        public static event Action<Vector3> SquadSent;
        public static void RaiseSquadSent(Vector3 from) => SquadSent?.Invoke(from);
        /// <summary>
        /// A creature has begun to wind up a strike (Ralph loop pass 2): the visor puts an amber arc on it that follows it
        /// and shows how far the warning has got, read from the creature every frame until it is over (UI/WindupCues).
        /// Not cleared by <see cref="Reset"/>, as above.
        /// </summary>
        public static event Action<Creatures.IWindup> CreatureWindup;
        public static void RaiseCreatureWindup(Creatures.IWindup source)
        {
            CreatureWarnedAt = Time.time;
            CreatureWindup?.Invoke(source);
        }
        /// <summary>Time.time when a creature last began a warning (for "in a fight": <see cref="Tasks.ReadoutFocus.InFight(float, float, float)"/>).</summary>
        public static float CreatureWarnedAt = -1000f;
        /// <summary>
        /// Time.time when a creature of a world last knew where the player is and could hurt them (it sees them, or was hurt
        /// and is after them): said every frame one does, by the creatures' own spawners (land and sea), for "in a fight".
        /// </summary>
        public static float CreatureAwareAt = -1000f;
        /// <summary>Time.time when any enemy last had the player in sight.</summary>
        public static float PlayerLastSeenAt = -1000f;
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
            GroundParent = null;
            PlayerDroppedGun = null;
            Alarm = null;
            EnemiesHoldFire = false;
            FindCover = null;
            PlayerSeenFrom = 1f;
            PlayerHeardFrom = 1f;
            PlayerAimBelowEye = 0.3f;
            PlayerLastSeenAt = -1000f;
            CreatureWarnedAt = -1000f;
            CreatureAwareAt = -1000f;
        }
    }
}
