using System;
using System.IO;
using System.Text;

namespace OrbitalAnte.Save
{
    public enum SaveSource
    {
        /// <summary>No usable save was found.</summary>
        None,
        Main,
        /// <summary>The main file was missing or damaged, so the previous good save was used.</summary>
        Backup,
    }

    /// <summary>
    /// Reads and writes one save file safely. No Unity types, so it can be unit tested anywhere.
    ///
    /// The new text is flushed to a temporary file before it replaces the main file. A rejected main file
    /// never replaces a recovered backup. Replacement uses the operating system's file guarantees; flushing
    /// the text does not guarantee that directory changes survive a power loss on every filesystem.
    /// </summary>
    public sealed class SaveFileStore
    {
        readonly string _main;
        readonly string _backup;
        readonly string _temp;
        bool _preserveBackup;

        public SaveFileStore(string directory, string fileName)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A folder is required.", nameof(directory));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("A file name is required.", nameof(fileName));
            Directory = directory;
            _main = Path.Combine(directory, fileName);
            _backup = _main + ".bak";
            _temp = _main + ".tmp";
        }

        public string Directory { get; }
        public string MainPath => _main;
        public string BackupPath => _backup;

        /// <summary>
        /// Loads the newest save that passes <paramref name="isValid"/>: the main file, or failing that the backup.
        /// </summary>
        public SaveSource TryRead(Func<string, bool> isValid, out string content)
        {
            if (TryReadFile(_main, isValid, out content))
            {
                _preserveBackup = false;
                return SaveSource.Main;
            }
            _preserveBackup = true;
            if (TryReadFile(_backup, isValid, out content)) return SaveSource.Backup;
            content = null;
            return SaveSource.None;
        }

        /// <summary>
        /// Checks both files without changing recovery state. Missing files are skipped; other read errors
        /// are reported to the caller so an unreadable save cannot be mistaken for an absent one.
        /// </summary>
        public bool AnyFileMatches(Func<string, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return FileMatches(_main, predicate) || FileMatches(_backup, predicate);
        }

        static bool FileMatches(string path, Func<string, bool> predicate)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            return predicate(text);
        }

        static bool TryReadFile(string path, Func<string, bool> isValid, out string content)
        {
            content = null;
            try
            {
                if (!File.Exists(path)) return false;
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrEmpty(text)) return false;
                if (isValid != null && !isValid(text)) return false;
                content = text;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a new save. The previous usable main becomes the backup; after recovery the good backup
        /// is kept instead. Supply <paramref name="isValid"/> to check the current main before promotion,
        /// including when this instance has not loaded a save or the main has changed since loading.
        /// </summary>
        public void Write(string content, Func<string, bool> isValid = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            System.IO.Directory.CreateDirectory(Directory);

            using (var stream = new FileStream(_temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
                // Request a disk flush before changing the existing save files.
                stream.Flush(true);
            }

            if (isValid != null) _preserveBackup = !TryReadFile(_main, isValid, out _);
            if (File.Exists(_main))
                File.Replace(_temp, _main, _preserveBackup ? null : _backup);
            else
                File.Move(_temp, _main);
            // A failed promotion must keep recovery protection for the next attempt.
            _preserveBackup = false;
        }

        /// <summary>Removes the save, its backup and any leftover temporary file.</summary>
        public void DeleteAll()
        {
            if (File.Exists(_main)) File.Delete(_main);
            if (File.Exists(_backup)) File.Delete(_backup);
            if (File.Exists(_temp)) File.Delete(_temp);
            _preserveBackup = false;
        }
    }
}
