using System.Collections.Generic;

namespace OrbitalAnte.Content
{
    /// <summary>Checks content for mistakes that would otherwise show up as odd bugs in play.</summary>
    public static class ContentValidation
    {
        /// <summary>Ids look like <c>weapon.rifle_ak</c>: a type prefix, a dot, then a name.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool dot = false;
            char prev = '.';
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.';
                if (!ok) return false;
                if (c == '.')
                {
                    if (prev == '.') return false; // leading or doubled dot
                    dot = true;
                }
                prev = c;
            }
            return dot && prev != '.';
        }

        /// <summary>Returns one readable line per problem. Empty means everything is fine.</summary>
        public static List<string> FindProblems(IEnumerable<ContentDefinition> all)
        {
            var problems = new List<string>();
            var seen = new Dictionary<string, ContentDefinition>();
            foreach (var def in all)
            {
                if (def == null) continue;
                if (!IsValidId(def.id))
                {
                    problems.Add($"{def.name}: id \"{def.id}\" is missing or badly formed (use something like weapon.rifle_ak).");
                }
                else if (seen.TryGetValue(def.id, out var other))
                {
                    problems.Add($"{def.name}: id \"{def.id}\" is already used by {other.name}.");
                }
                else
                {
                    seen[def.id] = def;
                }
                int before = problems.Count;
                def.Validate(problems);
                for (int i = before; i < problems.Count; i++) problems[i] = def.name + ": " + problems[i];
            }
            return problems;
        }
    }
}
