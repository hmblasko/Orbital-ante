using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Physics layers used by the game. They are referred to by number so the project works even before
    /// the layer names have been written into Project Settings (the setup tool names them for convenience).
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        /// <summary>Per-body-part hitboxes. Only bullets (raycasts) see these; nothing collides with them.</summary>
        public const int Hitbox = 8;
        /// <summary>Detached parts flying around.</summary>
        public const int Gib = 9;
        public const int Player = 10;
        /// <summary>The simple capsule that stops a character walking through the player.</summary>
        public const int CharacterBody = 11;
        /// <summary>Loose things lying in the world that can be shot and picked up, such as dropped weapons.</summary>
        public const int Item = 12;

        public const int WorldMask = 1 << Default;
        public const int ShootMask = (1 << Default) | (1 << Hitbox) | (1 << Gib) | (1 << Item);
        /// <summary>What an enemy's bullet can hit: the world and the player.</summary>
        public const int EnemyShootMask = (1 << Default) | (1 << Player);

        public static readonly string[] Names = { "Hitbox", "Gib", "Player", "CharacterBody", "Item" };
        public const int FirstNamedLayer = 8;

        public static void Configure()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(Hitbox, i, true);
            }
            // Gibs should never trip the player up or shove characters around.
            Physics.IgnoreLayerCollision(Gib, Player, true);
            Physics.IgnoreLayerCollision(Gib, CharacterBody, true);
            // Dropped weapons land on the world and on each other, but are never in anyone's way.
            Physics.IgnoreLayerCollision(Item, Player, true);
            Physics.IgnoreLayerCollision(Item, CharacterBody, true);
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            var t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
