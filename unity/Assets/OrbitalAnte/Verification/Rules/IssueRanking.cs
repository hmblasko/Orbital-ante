using System;
using System.Collections.Generic;

namespace OrbitalAnte.Verification
{
    /// <summary>
    /// Orders issues the way the quality brief asks: severity first, then expected benefit, confidence in the
    /// diagnosis, effort, regression risk, and whether the fix helps more than one asset or scene.
    /// The Critic may override the order by hand, but must keep severity first (the gate checks that).
    /// </summary>
    public static class IssueRanking
    {
        static int Level(string value, params string[] order)
        {
            int i = Array.IndexOf(order, (value ?? string.Empty).ToLowerInvariant());
            return i < 0 ? order.Length : i;
        }

        public static List<Dictionary<string, object>> Rank(IEnumerable<Dictionary<string, object>> issues)
        {
            var list = new List<Dictionary<string, object>>(issues);
            var index = new Dictionary<Dictionary<string, object>, int>();
            for (int i = 0; i < list.Count; i++) index[list[i]] = i;
            list.Sort((a, b) =>
            {
                int c = Level(MiniJson.Str(a, "severity"), "p0", "p1", "p2", "p3").CompareTo(Level(MiniJson.Str(b, "severity"), "p0", "p1", "p2", "p3"));
                if (c != 0) return c;
                c = Level(MiniJson.Str(a, "expectedBenefit"), "high", "medium", "low").CompareTo(Level(MiniJson.Str(b, "expectedBenefit"), "high", "medium", "low"));
                if (c != 0) return c;
                c = (MiniJson.Num(b, "confidence") ?? 0).CompareTo(MiniJson.Num(a, "confidence") ?? 0);
                if (c != 0) return c;
                c = Level(MiniJson.Str(a, "effort"), "small", "medium", "large").CompareTo(Level(MiniJson.Str(b, "effort"), "small", "medium", "large"));
                if (c != 0) return c;
                c = Level(MiniJson.Str(a, "regressionRisk"), "low", "medium", "high").CompareTo(Level(MiniJson.Str(b, "regressionRisk"), "low", "medium", "high"));
                if (c != 0) return c;
                c = MiniJson.Items(b, "alsoAffects").Count.CompareTo(MiniJson.Items(a, "alsoAffects").Count);
                return c != 0 ? c : index[a].CompareTo(index[b]);
            });
            return list;
        }
    }
}
