using System;
using System.IO;
using UnityEngine;

namespace OrbitalAnte.Save
{
    /// <summary>
    /// One save file on disk, written safely:
    /// the new text goes to a temporary file first and is then swapped in, and the previous good
    /// save is kept as a backup. A damaged save falls back to the backup; a save from a newer
    /// build is never overwritten.
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "save.json";
        const string BackupSuffix = ".bak";
        const string TempSuffix = ".tmp";
        const string CorruptSuffix = ".corrupt";

        static SaveFile current;
        static string directoryOverride;

        /// <summary>True when the file on disk came from a newer build, so saving is switched off.</summary>
        public static bool ReadOnly { get; private set; }

        public static string Directory => directoryOverride ?? Application.persistentDataPath;
        public static string MainPath => Path.Combine(Directory, FileName);
        public static string BackupPath => MainPath + BackupSuffix;

        public static SaveFile Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        /// <summary>Tests point the save system at a scratch folder. Pass null to go back to normal.</summary>
        public static void UseDirectory(string directory)
        {
            directoryOverride = directory;
            current = null;
            ReadOnly = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            ReadOnly = false;
            directoryOverride = null;
        }

        public static SaveFile Load()
        {
            ReadOnly = false;
            var main = ReadPath(MainPath, out var file);
            if (main == SaveReadResult.Ok)
            {
                current = file;
                return current;
            }
            if (main == SaveReadResult.TooNew)
            {
                Debug.LogWarning("[OA] The save file was made by a newer build. Playing without saving so it is not damaged.");
                ReadOnly = true;
                current = new SaveFile();
                return current;
            }
            if (main == SaveReadResult.Corrupt)
            {
                // Keep the damaged file for inspection instead of silently deleting it.
                TryCopy(MainPath, MainPath + CorruptSuffix);
                Debug.LogWarning("[OA] The save file could not be read. Trying the backup.");
            }

            if (ReadPath(BackupPath, out file) == SaveReadResult.Ok)
            {
                if (main == SaveReadResult.Corrupt) Debug.Log("[OA] Loaded the backup save.");
                current = file;
                return current;
            }

            current = new SaveFile();
            return current;
        }

        /// <summary>Writes <see cref="Current"/> to disk. Returns false if nothing was written.</summary>
        public static bool Save()
        {
            if (ReadOnly) return false;
            if (current == null) current = new SaveFile();
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string temp = MainPath + TempSuffix;
                File.WriteAllText(temp, SaveCodec.Write(current));
                if (File.Exists(MainPath))
                {
                    // Only keep the old file as the backup if it is readable; never replace a
                    // good backup with a damaged file.
                    if (ReadPath(MainPath, out _) == SaveReadResult.Ok) File.Copy(MainPath, BackupPath, true);
                    File.Delete(MainPath);
                }
                File.Move(temp, MainPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[OA] Could not write the save file: " + e.Message);
                return false;
            }
        }

        /// <summary>Removes the save and its backup (used by tests and a future "erase data" option).</summary>
        public static void DeleteAll()
        {
            foreach (var suffix in new[] { "", BackupSuffix, TempSuffix, CorruptSuffix })
            {
                string p = MainPath + suffix;
                if (File.Exists(p)) File.Delete(p);
            }
            current = null;
            ReadOnly = false;
        }

        // ---- Small helpers the game calls, so callers never touch the file layout directly.

        public static void RecordRunStarted(int seed)
        {
            var s = Current;
            bool sameRun = s.run.active && s.run.seed == seed;
            s.run.active = true;
            s.run.seed = seed;
            if (!sameRun) s.profile.runsStarted++;
            Save();
        }

        public static void ClearRun()
        {
            Current.run = new RunData();
            Save();
        }

        static SaveReadResult ReadPath(string path, out SaveFile file)
        {
            file = null;
            try
            {
                if (!File.Exists(path)) return SaveReadResult.Empty;
                return SaveCodec.Read(File.ReadAllText(path), out file);
            }
            catch (Exception)
            {
                return SaveReadResult.Corrupt;
            }
        }

        static void TryCopy(string from, string to)
        {
            try { File.Copy(from, to, true); }
            catch (Exception) { /* best effort */ }
        }
    }
}
