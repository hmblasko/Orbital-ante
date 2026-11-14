namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// How thick each part of a body model is beside the trooper's, so hitboxes can be fitted to it. Every kind
    /// is built on the same joints at the same size (the game then draws it larger or smaller as a whole), so
    /// only the thickness differs: multiply the trooper's hitbox radius for a part by <see cref="Thickness"/>.
    /// The numbers are the ones tools/blender/build_enemy_types.py builds the limbs with; armour that stands
    /// proud of a limb (pauldrons, spikes, packs) is left outside the hitbox on purpose, as it is on the trooper.
    /// </summary>
    public static class BodyFit
    {
        // Head, torso, upper arm, forearm, hand, thigh, shin, foot.
        static readonly float[] Trooper = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        static readonly float[] Runner = { 0.88f, 0.88f, 0.76f, 0.80f, 0.88f, 0.80f, 0.78f, 0.88f };
        static readonly float[] Bruiser = { 0.80f, 1.25f, 1.26f, 1.62f, 1.42f, 1.26f, 1.34f, 1.26f };
        static readonly float[] Marksman = { 0.88f, 0.90f, 0.84f, 0.84f, 0.94f, 0.86f, 0.84f, 0.94f };

        /// <summary>
        /// How far above the trooper's the middle of this kind's helmet sits, in metres before the body is scaled
        /// (negative is lower). The head's hitbox is a ball and the head is the only way to end an enemy, so the
        /// ball must sit on the helmet the player sees: the Bruiser's is sunk into its collar.
        /// </summary>
        public static float HeadRise(string model)
        {
            switch (model)
            {
                case "EnemyRunner": return -0.012f;
                case "EnemyBruiser": return -0.050f;
                case "EnemyMarksman": return 0.010f;
                default: return 0f;
            }
        }

        /// <summary>The thickness of <paramref name="part"/> on the body model named, beside the trooper's. 1 for anything unknown.</summary>
        public static float Thickness(string model, BodyPartId part)
        {
            float[] kind;
            switch (model)
            {
                case "EnemyRunner": kind = Runner; break;
                case "EnemyBruiser": kind = Bruiser; break;
                case "EnemyMarksman": kind = Marksman; break;
                default: kind = Trooper; break;
            }
            switch (part)
            {
                case BodyPartId.Head: return kind[0];
                case BodyPartId.Torso: return kind[1];
                case BodyPartId.LeftUpperArm: case BodyPartId.RightUpperArm: return kind[2];
                case BodyPartId.LeftForearm: case BodyPartId.RightForearm: return kind[3];
                case BodyPartId.LeftHand: case BodyPartId.RightHand: return kind[4];
                case BodyPartId.LeftThigh: case BodyPartId.RightThigh: return kind[5];
                case BodyPartId.LeftShin: case BodyPartId.RightShin: return kind[6];
                case BodyPartId.LeftFoot: case BodyPartId.RightFoot: return kind[7];
                default: return 1f;
            }
        }
    }
}
