using System.Collections.Generic;
using System.Xml.Linq;

namespace OrbitalAnte.Verification
{
    /// <summary>
    /// Turns the NUnit result file that Unity's test runner writes into the small summary kept in
    /// test-results.json. Only what was actually in the file is reported: no file, no result.
    /// </summary>
    public static class TestResultsSummary
    {
        public static Dictionary<string, object> FromNUnitXml(string xml, string label)
        {
            XElement run = XDocument.Parse(xml).Root;
            var failures = new List<object>();
            int passed = 0, failed = 0, skipped = 0, inconclusive = 0, total = 0;
            foreach (XElement test in run.Descendants("test-case"))
            {
                total++;
                string result = (string)test.Attribute("result") ?? string.Empty;
                switch (result)
                {
                    case "Passed": passed++; break;
                    case "Failed":
                        failed++;
                        failures.Add(new Dictionary<string, object>
                        {
                            ["name"] = (string)test.Attribute("fullname") ?? (string)test.Attribute("name") ?? "?",
                            ["message"] = Trim((string)test.Element("failure")?.Element("message") ?? string.Empty),
                        });
                        break;
                    case "Skipped": skipped++; break;
                    default: inconclusive++; break;
                }
            }
            return new Dictionary<string, object>
            {
                ["suite"] = label,
                ["ran"] = total > 0,
                ["total"] = total,
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["inconclusive"] = inconclusive,
                ["allPassed"] = total > 0 && failed == 0 && inconclusive == 0,
                ["durationSeconds"] = (double?)run.Attribute("duration") ?? 0.0,
                ["failures"] = failures,
            };
        }

        static string Trim(string text)
        {
            text = text.Trim();
            return text.Length > 600 ? text.Substring(0, 600) + "..." : text;
        }
    }
}
