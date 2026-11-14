using System;

namespace OrbitalAnte.Bases
{
    /// <summary>One design of base: which model it is and what the model promises.</summary>
    public sealed class BaseKind
    {
        /// <summary>The model file in Resources/Models (written by tools/blender/build_bases.py).</summary>
        public string Model;
        /// <summary>What it is, in plain words, for the screen and the notes.</summary>
        public string Name;
        public BaseSize Size;
        /// <summary>The open ground in the middle, metres from the centre: nothing solid stands inside it.</summary>
        public float Yard;
        /// <summary>How many of each attachment point the model has: open ground for guards, lookouts up on towers, loot.</summary>
        public int Spawns, Guards, Loot;
        /// <summary>Whether there is a marked landing circle (the "Pad" point), and its radius in metres. Nobody is stood on it.</summary>
        public bool Pad;
        public float PadRadius;
    }

    /// <summary>
    /// The designs of base, by size, and what every base model must contain. Plain rules, no Unity types.
    ///
    /// A base model is built in base space (origin in the middle of the floor, +Y up, +Z north) to stand on the
    /// round foundation <see cref="PlanetBases"/> makes: it brings no floor of its own. Pieces: Shell, Frame and
    /// Detail are drawn; WindowGlow and LampGlow shine; Hit is what is solid (closed boxes and drums, never drawn).
    /// Points: Spawn0.., Guard0.., Loot0.., Door, Pad. Nothing in a model says whose base it is.
    /// </summary>
    public static class BaseKinds
    {
        public const string Shell = "Shell", Frame = "Frame", Detail = "Detail", WindowGlow = "WindowGlow", LampGlow = "LampGlow", Hit = "Hit";
        public const string Spawn = "Spawn", Guard = "Guard", Loot = "Loot", Door = "Door", Pad = "Pad";

        public static readonly string[] Drawn = { Shell, Frame, Detail };
        public static readonly string[] Lit = { WindowGlow, LampGlow };

        /// <summary>The most triangles a base may draw, by size, and the most its solid piece may have (the build script enforces the same).</summary>
        public static int TriangleCap(BaseSize size) => size == BaseSize.Small ? 9000 : size == BaseSize.Medium ? 16000 : 34000;
        public static int SolidTriangleCap(BaseSize size) => size == BaseSize.Large ? 1400 : 700;

        /// <summary>The radius of the floor each size is built for, metres (the same numbers as <see cref="BaseFieldRules"/>' defaults).</summary>
        public static float FloorRadius(BaseSize size) => size == BaseSize.Small ? 10f : size == BaseSize.Medium ? 18f : 30f;

        static readonly BaseKind[][] BySize =
        {
            new[]
            {
                new BaseKind { Model = "BaseRelay", Name = "Relay outpost", Size = BaseSize.Small, Yard = 2.0f, Spawns = 4, Guards = 0, Loot = 1, Pad = false },
                new BaseKind { Model = "BaseDepot", Name = "Fuel depot", Size = BaseSize.Small, Yard = 2.5f, Spawns = 4, Guards = 1, Loot = 1, Pad = false },
            },
            new[]
            {
                new BaseKind { Model = "BaseDomes", Name = "Dome colony", Size = BaseSize.Medium, Yard = 3.0f, Spawns = 6, Guards = 0, Loot = 1, Pad = true, PadRadius = 4.2f },
                new BaseKind { Model = "BaseMine", Name = "Mining works", Size = BaseSize.Medium, Yard = 2.5f, Spawns = 6, Guards = 1, Loot = 1, Pad = true, PadRadius = 3.6f },
            },
            new[]
            {
                new BaseKind { Model = "BaseStronghold", Name = "Stronghold", Size = BaseSize.Large, Yard = 7.0f, Spawns = 10, Guards = 4, Loot = 3, Pad = true, PadRadius = 6.2f },
            },
        };

        /// <summary>How many designs there are of each size (small, medium, large): what <see cref="PlanetBases.Kinds"/> is set to.</summary>
        public static int[] Counts => new[] { BySize[0].Length, BySize[1].Length, BySize[2].Length };

        /// <summary>The design for a size and a kind number. Any kind number is safe: it wraps.</summary>
        public static BaseKind Of(BaseSize size, int kind)
        {
            BaseKind[] row = BySize[Math.Min(Math.Max((int)size, 0), BySize.Length - 1)];
            return row[((kind % row.Length) + row.Length) % row.Length];
        }

        public static BaseKind Of(BaseSite site) => Of(site.Size, site.Kind);

        /// <summary>Every design, for tests and lists.</summary>
        public static BaseKind[] All()
        {
            var all = new BaseKind[BySize[0].Length + BySize[1].Length + BySize[2].Length];
            int n = 0;
            for (int s = 0; s < BySize.Length; s++)
                for (int k = 0; k < BySize[s].Length; k++) all[n++] = BySize[s][k];
            return all;
        }

        /// <summary>
        /// Which way a base is turned on its floor, in quarter turns clockwise seen from above, so two bases of
        /// one design do not face the same way. The same for a seed every time.
        /// </summary>
        public static int QuarterTurns(int seed) => (int)(((uint)seed * 2654435761u) >> 30);

        /// <summary>A place in the model's space after the base has been turned. x is east, z is north.</summary>
        public static void Turned(int quarterTurns, float x, float z, out float outX, out float outZ)
        {
            switch (((quarterTurns % 4) + 4) % 4)
            {
                case 1: outX = z; outZ = -x; break;
                case 2: outX = -x; outZ = -z; break;
                case 3: outX = -z; outZ = x; break;
                default: outX = x; outZ = z; break;
            }
        }
    }
}
