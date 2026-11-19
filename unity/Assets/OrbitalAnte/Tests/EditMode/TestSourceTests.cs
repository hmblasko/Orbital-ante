using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// Unity's test runner ships an old NUnit (3.5), while the cloud rules project builds against a newer one,
    /// and test files kept behind <c>#if UNITY_...</c> are never compiled in the cloud at all. So a test that uses
    /// a newer NUnit feature passes every cloud check and then stops the whole project compiling in Unity
    /// (17 Nov 2026: one such attribute blocked Play on the Deck). This reads every test file's text, including
    /// the Unity-only ones, and fails on anything Unity's NUnit does not have.
    /// </summary>
    public class TestSourceTests
    {
        // Added to NUnit after 3.5. Names are split so this file does not find itself.
        static readonly string[] Missing =
        {
            "Non" + "Parallel" + "izable", "Parallel" + "izable", "LevelOf" + "Parallelism", "FixtureLife" + "Cycle",
            "DefaultFloatingPoint" + "Tolerance", "Assert." + "Multiple", "Assume." + "Multiple", "Assert." + "Warn",
        };

        static string TestsFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Tests"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Tests") })
                    if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Tests");
        }

        [Test]
        public void NoTest_UsesAnNUnitFeatureThatUnitysOwnNUnitLacks()
        {
            string folder = TestsFolder();
            Assume.That(Directory.Exists(folder), "tests folder not found: " + folder);
            var found = new List<string>();
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) line = line.Substring(0, comment);
                    foreach (string name in Missing)
                        if (Regex.IsMatch(line, @"(?<![\w.])" + Regex.Escape(name) + @"(?!\w)"))
                            found.Add(Path.GetFileName(file) + ":" + (i + 1) + " uses " + name);
                }
            }
            Assert.IsEmpty(found, "Unity's NUnit (3.5) does not have these, so Unity would not compile:\n" + string.Join("\n", found));
        }
    }
}
