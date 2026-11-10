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
    /// A save is never written over the old one directly. The new text goes to a temporary file first; only
    /// when that is complete does the old save become the backup and the new file take its place. If the game
    /// or the Steam Deck dies halfway through, either the old save or the new one is intact, never a torn half.
    /// </summary>
    public sealed class SaveFileStore
    {
        readonly string _main;
        readonly string _backup;
        readonly string _temp;

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
            if (TryReadFile(_main, isValid, out content)) return SaveSource.Main;
            if (TryReadFile(_backup, isValid, out content)) return SaveSource.Backup;
            content = null;
            return SaveSource.None;
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

        /// <summary>Writes a new save. The previous save, if any, becomes the backup.</summary>
        public void Write(string content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            System.IO.Directory.CreateDirectory(Directory);

            using (var stream = new FileStream(_temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
                // Make sure it is really on the disk before the old save is touched.
                stream.Flush(true);
            }

            if (File.Exists(_main))
            {
                if (File.Exists(_backup)) File.Delete(_backup);
                File.Move(_main, _backup);
            }
            File.Move(_temp, _main);
        }

        /// <summary>Removes the save, its backup and any leftover temporary file.</summary>
        public void DeleteAll()
        {
            if (File.Exists(_main)) File.Delete(_main);
            if (File.Exists(_backup)) File.Delete(_backup);
            if (File.Exists(_temp)) File.Delete(_temp);
        }
    }
}
