using System.Collections.Generic;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// Remembers what has already been paid for, so nothing pays twice: not in the same ante, not in a later one,
    /// and not after saving and loading. Each scoring thing in the world has one claim id for the whole run
    /// ("enemy:mars.base2.guard7", "landmark:mars.canyon", "landing:europa"). It also keeps the small counters
    /// behind "at most N of these" caps.
    /// </summary>
    public sealed class ClaimRegistry
    {
        readonly HashSet<string> _claims = new HashSet<string>();
        readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        public int ClaimCount => _claims.Count;

        public bool IsClaimed(string claimId) => claimId != null && _claims.Contains(claimId);

        /// <returns>True if this id had not been claimed before. False changes nothing.</returns>
        public bool TryClaim(string claimId)
        {
            if (string.IsNullOrEmpty(claimId)) return false;
            return _claims.Add(claimId);
        }

        public int Count(string counterKey)
        {
            return counterKey != null && _counters.TryGetValue(counterKey, out int n) ? n : 0;
        }

        public void Increment(string counterKey)
        {
            if (string.IsNullOrEmpty(counterKey)) return;
            _counters[counterKey] = Count(counterKey) + 1;
        }

        /// <summary>Copies everything out in a fixed (sorted) order, ready to be saved.</summary>
        public void Export(List<string> claims, List<string> counterKeys, List<int> counterValues)
        {
            claims.Clear();
            claims.AddRange(_claims);
            claims.Sort(string.CompareOrdinal);

            counterKeys.Clear();
            counterValues.Clear();
            counterKeys.AddRange(_counters.Keys);
            counterKeys.Sort(string.CompareOrdinal);
            for (int i = 0; i < counterKeys.Count; i++) counterValues.Add(_counters[counterKeys[i]]);
        }

        public void Import(List<string> claims, List<string> counterKeys, List<int> counterValues)
        {
            _claims.Clear();
            _counters.Clear();
            if (claims != null)
            {
                for (int i = 0; i < claims.Count; i++)
                    if (!string.IsNullOrEmpty(claims[i])) _claims.Add(claims[i]);
            }
            if (counterKeys != null && counterValues != null)
            {
                int n = System.Math.Min(counterKeys.Count, counterValues.Count);
                for (int i = 0; i < n; i++)
                    if (!string.IsNullOrEmpty(counterKeys[i])) _counters[counterKeys[i]] = counterValues[i];
            }
        }
    }
}
