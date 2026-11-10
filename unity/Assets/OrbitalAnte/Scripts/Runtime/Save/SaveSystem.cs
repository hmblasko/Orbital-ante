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

        public static SaveData Data { get; private set; } = new SaveData();
        /// <summary>Where the last load came from. Backup means the main file was damaged and recovery worked.</summary>
        public static SaveSource LoadedFrom { get; private set; } = SaveSource.None;
        public static string FilePath => _store != null ? _store.MainPath : string.Empty;

        /// <summary>Points the save system at a folder. Tests use this to stay away from the real save.</summary>
        public static void UseFolder(string folder)
        {
            _store = new SaveFileStore(folder, FileName);
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
            try
            {
                SaveSource source = Store.TryRead(IsUsable, out string json);
                if (source == SaveSource.None) return;

                SaveData loaded = Parse(json);
                if (loaded == null || !SaveMigrations.Upgrade(loaded)) return;
                Data = loaded;
                LoadedFrom = source;
                if (source == SaveSource.Backup)
                {
                    Debug.LogWarning("Orbital Ante: the save file was damaged, so the previous save was loaded instead.");
                }
            }
            catch (Exception e)
            {
                // A broken save must never stop the game from starting.
                Debug.LogWarning("Orbital Ante: the save could not be read (" + e.Message + "). Starting fresh.");
                Data = new SaveData();
            }
        }

        public static void Save()
        {
            try
            {
                Data.version = SaveData.CurrentVersion;
                Store.Write(JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Orbital Ante: the game could not be saved (" + e.Message + ").");
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
        }
    }
}
