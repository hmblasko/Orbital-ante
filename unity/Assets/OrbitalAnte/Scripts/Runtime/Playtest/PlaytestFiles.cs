using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OrbitalAnte.Playtest
{
    /// <summary>
    /// Where a run's playtest log is written and how many are kept (Ralph loop pass 9). One file a run, named by when it
    /// began, so the names sort by time; the newest is copied to <see cref="Latest"/>, so there is always one fixed place
    /// to look; only the newest <see cref="Keep"/> runs are kept. Plain C#: the game gives it the folder.
    /// </summary>
    public static class PlaytestFiles
    {
        public const int Keep = 40;
        public const string Latest = "latest.txt";
        const string Prefix = "run_";

        /// <summary>The file a run's log goes in: "run_2026-11-28_100215_solar-system_seed12345.txt".</summary>
        public static string NameFor(DateTime started, string scenario, int seed)
        {
            return Prefix + started.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_" + Slug(scenario) + "_seed"
                   + seed.ToString(CultureInfo.InvariantCulture) + ".txt";
        }

        static string Slug(string words)
        {
            if (string.IsNullOrEmpty(words)) return "run";
            var sb = new StringBuilder(words.Length);
            for (int i = 0; i < words.Length; i++)
            {
                char c = char.ToLowerInvariant(words[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '-') sb.Length--;
            return sb.Length > 0 ? sb.ToString() : "run";
        }

        /// <summary>Writes <paramref name="text"/> to the run's file and to <see cref="Latest"/>, then trims old runs. Returns the run's path.</summary>
        public static string Write(string folder, string name, string text)
        {
            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text, utf8);
            // A copy, so the text is encoded once.
            File.Copy(path, Path.Combine(folder, Latest), true);
            Trim(folder, Keep);
            return path;
        }

        /// <summary>Deletes all but the newest <paramref name="keep"/> run files (by name, which is by time).</summary>
        public static int Trim(string folder, int keep)
        {
            if (!Directory.Exists(folder)) return 0;
            string[] files = Directory.GetFiles(folder, Prefix + "*.txt");
            if (files.Length <= keep) return 0;
            Array.Sort(files, StringComparer.Ordinal);
            int deleted = 0;
            for (int i = 0; i < files.Length - Math.Max(0, keep); i++)
            {
                File.Delete(files[i]);
                deleted++;
            }
            return deleted;
        }
    }
}
