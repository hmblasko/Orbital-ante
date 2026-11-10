using System;

namespace OrbitalAnte.Save
{
    /// <summary>
    /// Everything that is kept between sessions. Deliberately tiny for now: Phase 1 has nothing to unlock or
    /// collect yet, so this holds only what the test builds use. New fields are added here as systems arrive,
    /// and <see cref="SaveMigrations"/> brings old saves up to date so nobody's save is ever thrown away.
    ///
    /// Key bindings and the Feel toggles are kept in Unity's PlayerPrefs instead, because they belong to the
    /// machine, not to the save.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        /// <summary>Which layout of this file was written. 0 means "not a save".</summary>
        public int version = CurrentVersion;

        /// <summary>The seed of the most recent run, so it can be played again.</summary>
        public bool hasLastSeed;
        public int lastSeed;

        public int runsStarted;
        /// <summary>Total time spent in runs, in seconds.</summary>
        public double secondsPlayed;
    }

    /// <summary>Brings a save written by an older build up to the current layout, one version at a time.</summary>
    public static class SaveMigrations
    {
        /// <returns>False if the save is from a newer build than this one and cannot be read safely.</returns>
        public static bool Upgrade(SaveData data)
        {
            if (data == null || data.version <= 0) return false;
            if (data.version > SaveData.CurrentVersion) return false;

            // Each future change adds one step here, for example:
            // if (data.version == 1) { ...fill in the new fields...; data.version = 2; }

            return data.version == SaveData.CurrentVersion;
        }
    }
}
