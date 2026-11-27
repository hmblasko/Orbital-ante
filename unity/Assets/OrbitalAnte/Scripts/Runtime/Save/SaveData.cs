using System;
using System.Collections.Generic;
using OrbitalAnte.Items;

namespace OrbitalAnte.Save
{
    /// <summary>
    /// Everything that is kept between sessions: the last seed, the collection and its envelopes, the run records
    /// and the goal unlocks. New fields are added here as systems arrive,
    /// and <see cref="SaveMigrations"/> brings old saves up to date so nobody's save is ever thrown away.
    ///
    /// Key bindings and the Feel toggles are kept in Unity's PlayerPrefs instead, because they belong to the
    /// machine, not to the save.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 5;

        /// <summary>Which layout of this file was written. 0 means "not a save".</summary>
        public int version = CurrentVersion;

        /// <summary>The seed of the most recent run, so it can be played again.</summary>
        public bool hasLastSeed;
        public int lastSeed;
        /// <summary>Which test world that run was in: 0 combat arena, 1 solar system. (Added in version 2.)</summary>
        public int lastScenario;

        public int runsStarted;
        /// <summary>Total time spent in runs, in seconds.</summary>
        public double secondsPlayed;

        // ---- Envelopes and the collection (added in version 3, 16 Nov 2026) ----
        /// <summary>This save's own number, mixed into every envelope's prize so it never follows the run seed. 0 until first set.</summary>
        public int profileSalt;
        /// <summary>How many envelopes have been numbered (one per run started, plus test envelopes).</summary>
        public int envelopesSealed;
        public int envelopesOpened;
        /// <summary>Envelopes won (Ante 9 cleared) and not opened yet.</summary>
        public List<EnvelopeTicket> unopenedEnvelopes = new List<EnvelopeTicket>();
        /// <summary>The player's collection, kept across runs.</summary>
        public List<OwnedItem> items = new List<OwnedItem>();

        // ---- Run records (added in version 4, 20 Nov 2026: the run recap shows them) ----
        /// <summary>The highest ante reached in any solar-system run (1 once one has been played).</summary>
        public int bestAnte;
        /// <summary>The best run score (points banked over a whole run).</summary>
        public int bestRunScore;
        /// <summary>Antes cleared over every run.</summary>
        public int antesCleared;
        /// <summary>Solar-system runs that have ended (by death, the clock or victory).</summary>
        public int runsEnded;

        // ---- Goal unlocks (added in version 5, 27 Nov 2026: Ante/Unlocks.cs) ----
        /// <summary>Unlock ids earned ("unlock.kit.ghost"); each is one goal met in one run.</summary>
        public List<string> unlocks = new List<string>();
        /// <summary>Unlocks already shown on the title screen's Unlocks page (the rest are marked NEW).</summary>
        public List<string> unlocksSeen = new List<string>();
        /// <summary>The starting kit chosen for the next run ("kit.standard" for none).</summary>
        public string kit = OrbitalAnte.Ante.StartingKits.StandardId;
    }

    /// <summary>Brings a save written by an older build up to the current layout, one version at a time.</summary>
    public static class SaveMigrations
    {
        /// <returns>False if the save is from a newer build than this one and cannot be read safely.</returns>
        public static bool Upgrade(SaveData data)
        {
            if (data == null || data.version <= 0) return false;
            if (data.version > SaveData.CurrentVersion) return false;

            // One step per change to the layout, oldest first.
            if (data.version == 1)
            {
                // Version 1 only knew the combat arena.
                data.lastScenario = 0;
                data.version = 2;
            }
            if (data.version == 2)
            {
                // Version 3 added envelopes and the collection; an older save starts with none.
                data.profileSalt = 0;
                data.envelopesSealed = 0;
                data.envelopesOpened = 0;
                data.unopenedEnvelopes = new List<EnvelopeTicket>();
                data.items = new List<OwnedItem>();
                data.version = 3;
            }
            if (data.version == 3)
            {
                // Version 4 added run records; an older save has none yet.
                data.bestAnte = 0;
                data.bestRunScore = 0;
                data.antesCleared = 0;
                data.runsEnded = 0;
                data.version = 4;
            }
            if (data.version == 4)
            {
                // Version 5 added goal unlocks and the starting kit; an older save has earned none and starts Standard.
                data.unlocks = new List<string>();
                data.unlocksSeen = new List<string>();
                data.kit = OrbitalAnte.Ante.StartingKits.StandardId;
                data.version = 5;
            }
            // JsonUtility leaves a missing list as null on some paths; never hand one out.
            if (data.unopenedEnvelopes == null) data.unopenedEnvelopes = new List<EnvelopeTicket>();
            if (data.items == null) data.items = new List<OwnedItem>();
            if (data.unlocks == null) data.unlocks = new List<string>();
            if (data.unlocksSeen == null) data.unlocksSeen = new List<string>();
            if (string.IsNullOrEmpty(data.kit)) data.kit = OrbitalAnte.Ante.StartingKits.StandardId;

            return data.version == SaveData.CurrentVersion;
        }
    }
}
