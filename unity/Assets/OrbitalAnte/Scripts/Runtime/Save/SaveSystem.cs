using System;
using UnityEngine;

namespace OrbitalAnte.Save
{
    /// <summary>
    /// The game's one save file. Loaded once at start-up, written at natural moments (a run starting, leaving a
    /// run, quitting). Stored as readable JSON in Unity's per-user data folder, which Steam Cloud can sync later.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "orbital_ante_save.json";

        static SaveFileStore _store;
        // An uncertain load must not later replace unread progress with a fresh in-memory session.
        static bool _reloadRequired;

        // Read only the format header: a newer build can change the types of payload fields we understand.
        [Serializable]
        sealed class SaveHeader
        {
            public int version;
        }

        public static SaveData Data { get; private set; } = new SaveData();
        /// <summary>Where the last load came from. Backup means the main file was damaged and recovery worked.</summary>
        public static SaveSource LoadedFrom { get; private set; } = SaveSource.None;
        /// <summary>A newer save exists in this folder. The session can run, but cannot replace either file.</summary>
        public static bool WriteProtectedByNewerSave { get; private set; }
        public static string FilePath => _store != null ? _store.MainPath : string.Empty;

        /// <summary>Points the save system at a folder. Tests use this to stay away from the real save.</summary>
        public static void UseFolder(string folder)
        {
            _store = new SaveFileStore(folder, FileName);
            LoadedFrom = SaveSource.None;
            WriteProtectedByNewerSave = false;
            _reloadRequired = false;
        }

        static SaveFileStore Store
        {
            get
            {
                if (_store == null) UseFolder(Application.persistentDataPath);
                return _store;
            }
        }

        /// <summary>Loads the save, falling back to the backup, and then to a fresh save if neither can be read.</summary>
        public static void Load()
        {
            Data = new SaveData();
            LoadedFrom = SaveSource.None;
            WriteProtectedByNewerSave = false;
            _reloadRequired = false;
            try
            {
                try
                {
                    CheckForNewerSave();
                }
                catch (Exception e)
                {
                    // An unreadable slot must block writes, but need not hide a readable main or backup.
                    _reloadRequired = true;
                    Debug.LogWarning("Orbital Ante: the save format could not be checked (" + e.Message + "). Trying the readable save files; writes still require readable files.");
                }
                SaveSource source = Store.TryRead(IsUsable, out string json);
                if (source == SaveSource.None)
                {
                    if (_reloadRequired)
                        Debug.LogWarning("Orbital Ante: no save could be loaded. This session will not save until the files can be loaded again.");
                    return;
                }

                SaveData loaded = Parse(json);
                if (loaded == null || !SaveMigrations.Upgrade(loaded)) return;
                Data = loaded;
                LoadedFrom = source;
                _reloadRequired = false;
                if (source == SaveSource.Backup && !WriteProtectedByNewerSave)
                {
                    Debug.LogWarning("Orbital Ante: the save file was damaged, so the previous save was loaded instead.");
                }
            }
            catch (Exception e)
            {
                // A broken save must never stop the game from starting.
                Debug.LogWarning("Orbital Ante: the save could not be read (" + e.Message + "). Starting fresh for this session; reload the save before saving again.");
                Data = new SaveData();
                _reloadRequired = true;
            }
        }

        public static void Save()
        {
            try
            {
                if (_reloadRequired) return;
                // Check even before the first Load, and again after external changes to either slot.
                // A probe read failure also aborts here, before serialization or any disk write.
                if (CheckForNewerSave()) return;
                Data.version = SaveData.CurrentVersion;
                Store.Write(JsonUtility.ToJson(Data, true), IsUsable);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Orbital Ante: the game could not be saved (" + e.Message + ").");
            }
        }

        static bool CheckForNewerSave()
        {
            if (WriteProtectedByNewerSave) return true;
            if (!Store.AnyFileMatches(IsFromNewerBuild)) return false;
            WriteProtectedByNewerSave = true;
            Debug.LogWarning("Orbital Ante: a save from a newer build was found. This session's progress will not be saved, so the newer save stays intact. Use the newer build to keep saving.");
            return true;
        }

        static bool IsFromNewerBuild(string json)
        {
            try
            {
                SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);
                return header != null && header.version > SaveData.CurrentVersion;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static SaveData Parse(string json)
        {
            try
            {
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A file counts as a save only if it parses and is from this build or an older one.</summary>
        static bool IsUsable(string json)
        {
            SaveData parsed = Parse(json);
            return parsed != null && parsed.version > 0 && parsed.version <= SaveData.CurrentVersion;
        }

        public static void ResetStatics()
        {
            _store = null;
            Data = new SaveData();
            LoadedFrom = SaveSource.None;
            WriteProtectedByNewerSave = false;
            _reloadRequired = false;
        }
    }
}
