using System;
using UnityEngine;

namespace OrbitalAnte.Save
{
    public enum SaveReadResult
    {
        Ok,
        /// <summary>No text at all (first launch).</summary>
        Empty,
        /// <summary>Not valid save text.</summary>
        Corrupt,
        /// <summary>Written by a newer build than this one. Must not be overwritten.</summary>
        TooNew,
    }

    /// <summary>Turns a <see cref="SaveFile"/> into text and back. No disk access, so it is unit-tested.</summary>
    public static class SaveCodec
    {
        public const int CurrentVersion = 1;

        public static string Write(SaveFile file)
        {
            file.version = CurrentVersion;
            return JsonUtility.ToJson(file, true);
        }

        public static SaveReadResult Read(string text, out SaveFile file)
        {
            file = null;
            if (string.IsNullOrWhiteSpace(text)) return SaveReadResult.Empty;
            SaveFile parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveFile>(text);
            }
            catch (Exception)
            {
                return SaveReadResult.Corrupt;
            }
            if (parsed == null || parsed.version <= 0) return SaveReadResult.Corrupt;
            if (parsed.version > CurrentVersion) return SaveReadResult.TooNew;

            Upgrade(parsed);
            if (parsed.profile == null) parsed.profile = new ProfileData();
            if (parsed.run == null) parsed.run = new RunData();
            file = parsed;
            return SaveReadResult.Ok;
        }

        /// <summary>Brings an older save up to the current layout, one version at a time.</summary>
        static void Upgrade(SaveFile file)
        {
            // Version 1 is the first layout. Future steps go here, for example:
            // if (file.version == 1) { ...move fields...; file.version = 2; }
            file.version = CurrentVersion;
        }
    }
}
