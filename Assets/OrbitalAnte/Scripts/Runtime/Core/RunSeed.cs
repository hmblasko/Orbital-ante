namespace OrbitalAnte.Core
{
    /// <summary>
    /// The one master seed for the current run. Every system asks for its own stream by
    /// name, so adding a new random system never changes the results of existing ones.
    /// </summary>
    public static class RunSeed
    {
        static bool initialized;

        public static int Current { get; private set; }

        public static void EnsureInitialized()
        {
            if (!initialized) NewRandom();
        }

        public static void NewRandom()
        {
            int s = System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode();
            Set(s & 0x7FFFFFFF);
        }

        public static void Set(int seed)
        {
            Current = seed;
            initialized = true;
        }

        public static SeededRandom CreateStream(string streamName)
        {
            EnsureInitialized();
            ulong mixed = ((ulong)(uint)Current << 32) ^ StableHash(streamName);
            return new SeededRandom(mixed);
        }

        /// <summary>FNV-1a. string.GetHashCode is not stable between runs, this is.</summary>
        public static uint StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619;
                }
                return h;
            }
        }
    }
}
