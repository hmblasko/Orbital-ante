using System;

namespace OrbitalAnte.Save
{
    /// <summary>
    /// Everything written to disk. Plain data only: numbers, strings and content ids.
    /// Never store references to assets or scene objects; store a content id and look it up.
    /// Adding a field is safe (old saves load it as its default). Renaming or removing one
    /// needs a version bump and a step in <see cref="SaveCodec"/>.
    /// </summary>
    [Serializable]
    public class SaveFile
    {
        /// <summary>Layout version, stamped when written. 0 means "not a save file".</summary>
        public int version;
        public ProfileData profile = new ProfileData();
        public RunData run = new RunData();
    }

    /// <summary>Things that outlive a run: preferences and lifetime counters.</summary>
    [Serializable]
    public class ProfileData
    {
        public bool headshotSlowMoChosen;
        public bool headshotSlowMo;
        public int runsStarted;
    }

    /// <summary>The run in progress. Phase 1 only needs the seed; later milestones add to this.</summary>
    [Serializable]
    public class RunData
    {
        public bool active;
        public int seed;
    }
}
