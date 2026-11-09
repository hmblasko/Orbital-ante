namespace OrbitalAnte.Characters
{
    /// <summary>The 14 breakable parts. L/R suffixes are the character's own left and right.</summary>
    public enum BodyPartId
    {
        Head = 0,
        Torso = 1,
        UpperArmL = 2,
        ForearmL = 3,
        HandL = 4,
        UpperArmR = 5,
        ForearmR = 6,
        HandR = 7,
        ThighL = 8,
        ShinL = 9,
        FootL = 10,
        ThighR = 11,
        ShinR = 12,
        FootR = 13,
    }

    /// <summary>What the remaining body can do. Driven only by which parts are still attached.</summary>
    public enum LocomotionState
    {
        Normal,
        Limp,
        Crawl,
        Wriggle,
        HeadOnly,
        Dead,
    }

    /// <summary>Bit set of body parts. Cheap to copy, easy to test.</summary>
    public struct PartSet : System.IEquatable<PartSet>
    {
        public const int Count = 14;
        public ushort Bits;

        public static PartSet All => new PartSet { Bits = (ushort)((1 << Count) - 1) };
        public static PartSet None => new PartSet { Bits = 0 };

        public bool Has(BodyPartId id) => (Bits & (1 << (int)id)) != 0;
        public PartSet With(BodyPartId id) => new PartSet { Bits = (ushort)(Bits | (1 << (int)id)) };
        public PartSet Without(BodyPartId id) => new PartSet { Bits = (ushort)(Bits & ~(1 << (int)id)) };

        public PartSet Without(PartSet other) => new PartSet { Bits = (ushort)(Bits & ~other.Bits) };

        public int CountSet()
        {
            int n = 0;
            for (int i = 0; i < Count; i++) if ((Bits & (1 << i)) != 0) n++;
            return n;
        }

        public bool Equals(PartSet other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is PartSet p && Equals(p);
        public override int GetHashCode() => Bits;
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder("{");
            for (int i = 0; i < Count; i++)
                if ((Bits & (1 << i)) != 0) sb.Append(((BodyPartId)i).ToString()).Append(' ');
            return sb.Append('}').ToString();
        }
    }
}
