using System;
using System.Collections.Generic;

namespace OrbitalAnte.Combat
{
    /// <summary>One step of the gun ladder: which gun you hold there, and how many kills it takes to climb off it.</summary>
    [Serializable]
    public sealed class GunLadderRung
    {
        /// <summary>The gun's id: its file name in Resources/Weapons (for example Rifle).</summary>
        public string weaponId = "Rifle";
        /// <summary>Kills needed on this step to be handed the next gun. Ignored on the last step.</summary>
        public int killsToNext = 4;

        public GunLadderRung() { }

        public GunLadderRung(string weaponId, int killsToNext)
        {
            this.weaponId = weaponId;
            this.killsToNext = killsToNext;
        }
    }

    /// <summary>The ladder's numbers. Kept on the GunLadderConfig asset.</summary>
    [Serializable]
    public sealed class GunLadderRules
    {
        /// <summary>Off: the run starts with the rifle and kills never change your gun (the game as it was).</summary>
        public bool enabled = true;

        /// <summary>
        /// On: the gun you climb off is carried, and Switch Gun brings it back out (Matthew, 16 Nov 2026). Off: it
        /// is gone when the next one comes, and a gun you picked up is dropped instead.
        /// </summary>
        public bool keepOldGuns = true;

        /// <summary>
        /// The guns in the order they are handed out. Defaults (a first guess, not playtested), ordered by how much
        /// they put out in a long fight, with the specialists early so each step plays differently:
        /// test pistol 4 kills, Double Zapper 5, Long Shot 6, Buzz Gun 7, then the AK-ish to the end of the run.
        /// 22 kills reach the last gun.
        /// </summary>
        public GunLadderRung[] rungs = Defaults();

        public static GunLadderRung[] Defaults() => new[]
        {
            new GunLadderRung("Pistol", 4),
            new GunLadderRung("Shotgun", 5),
            new GunLadderRung("Sniper", 6),
            new GunLadderRung("Smg", 7),
            new GunLadderRung("Rifle", 0),
        };
    }

    /// <summary>
    /// The gun ladder: every kill counts toward your next gun, and each gun up the ladder is a stronger one.
    ///
    /// The whole rule, so nothing is hidden from the player:
    /// - A run starts on the first step, holding its gun.
    /// - Each enemy you kill counts one, whatever gun you killed it with (a gun picked up off the ground counts).
    /// - When the kills on a step reach its number, you are handed the next step's gun at once, fully loaded.
    ///   The gun you were holding is put away and carried, and Switch Gun brings it back out (with
    ///   <see cref="GunLadderRules.keepOldGuns"/> off it is gone instead, or dropped if you had picked it up).
    ///   The count starts again from nothing on the new step.
    /// - The last step's gun stays with you for the rest of the run. Kills there are still counted.
    /// - You never go back down. Dying ends the run, and a new run starts at the bottom.
    ///
    /// Plain C# with no engine types, so it can be tested anywhere.
    /// </summary>
    public sealed class GunLadder
    {
        readonly GunLadderRung[] _rungs;

        /// <summary>Which step you are on, from 0.</summary>
        public int Rung { get; private set; }
        /// <summary>Kills made on this step so far.</summary>
        public int KillsOnRung { get; private set; }
        /// <summary>Every kill this run, on any step.</summary>
        public int TotalKills { get; private set; }

        public int RungCount => _rungs.Length;
        public bool AtTop => Rung >= _rungs.Length - 1;
        public string WeaponId => _rungs[Rung].weaponId;
        /// <summary>The gun the next step hands out, or null on the last step.</summary>
        public string NextWeaponId => AtTop ? null : _rungs[Rung + 1].weaponId;
        /// <summary>Kills this step needs in all (at least 1). 0 on the last step.</summary>
        public int KillsNeeded => AtTop ? 0 : Math.Max(1, _rungs[Rung].killsToNext);
        /// <summary>Kills still to make before the next gun. 0 on the last step.</summary>
        public int KillsLeft => AtTop ? 0 : Math.Max(0, KillsNeeded - KillsOnRung);
        /// <summary>Kills from the start of the run to the last gun.</summary>
        public int KillsToTop
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _rungs.Length - 1; i++) sum += Math.Max(1, _rungs[i].killsToNext);
                return sum;
            }
        }

        /// <param name="rules">The ladder's numbers; null or an empty list uses the defaults.</param>
        /// <param name="gunExists">Says whether a gun id is real. Steps naming a gun that does not exist are left
        /// out, so a typo in the asset cannot hand you nothing. Null keeps every step.</param>
        public GunLadder(GunLadderRules rules = null, Func<string, bool> gunExists = null)
        {
            var kept = new List<GunLadderRung>();
            GunLadderRung[] given = rules != null && rules.rungs != null && rules.rungs.Length > 0
                ? rules.rungs
                : GunLadderRules.Defaults();
            foreach (GunLadderRung rung in given)
            {
                if (rung == null || string.IsNullOrEmpty(rung.weaponId)) continue;
                if (gunExists != null && !gunExists(rung.weaponId)) continue;
                kept.Add(rung);
            }
            if (kept.Count == 0) kept.AddRange(GunLadderRules.Defaults());
            _rungs = kept.ToArray();
        }

        /// <summary>The gun the step below this one handed out, or null on the first step.</summary>
        public string PreviousWeaponId => Rung > 0 ? _rungs[Rung - 1].weaponId : null;

        /// <summary>The gun on a step, for readouts.</summary>
        public string WeaponAt(int rung) => _rungs[Math.Max(0, Math.Min(_rungs.Length - 1, rung))].weaponId;

        /// <summary>Counts one kill. True when it climbed you onto the next step (hand over the new gun).</summary>
        public bool AddKill()
        {
            TotalKills++;
            if (AtTop)
            {
                KillsOnRung++;
                return false;
            }
            KillsOnRung++;
            if (KillsOnRung < KillsNeeded) return false;
            Rung++;
            KillsOnRung = 0;
            return true;
        }

        /// <summary>
        /// Climbs one step without a kill (a supply drop's gun). Kills already made on this step are not carried
        /// over. False at the top, where there is nothing more to give.
        /// </summary>
        public bool SkipStep()
        {
            if (AtTop) return false;
            Rung++;
            KillsOnRung = 0;
            return true;
        }

        /// <summary>A new run: back to the first step with no kills.</summary>
        public void Reset()
        {
            Rung = 0;
            KillsOnRung = 0;
            TotalKills = 0;
        }

        /// <summary>Plain words for what the asset holds that the game will quietly fix or leave out.</summary>
        public static List<string> Problems(GunLadderRules rules, Func<string, bool> gunExists)
        {
            var problems = new List<string>();
            if (rules == null || rules.rungs == null || rules.rungs.Length == 0)
            {
                problems.Add("the ladder has no steps; the default ladder is used.");
                return problems;
            }
            for (int i = 0; i < rules.rungs.Length; i++)
            {
                GunLadderRung rung = rules.rungs[i];
                bool last = i == rules.rungs.Length - 1;
                if (rung == null || string.IsNullOrEmpty(rung.weaponId)) problems.Add("step " + (i + 1) + " names no gun and is left out.");
                else if (gunExists != null && !gunExists(rung.weaponId)) problems.Add("step " + (i + 1) + " names a gun that does not exist (" + rung.weaponId + ") and is left out.");
                else if (!last && rung.killsToNext < 1) problems.Add("step " + (i + 1) + " needs at least 1 kill; 1 is used.");
            }
            return problems;
        }

        /// <summary>A gun's name for the screen, without the "(name TBD)" the placeholder names carry.</summary>
        public static string ShortName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return string.Empty;
            int cut = displayName.IndexOf(" (", StringComparison.Ordinal);
            return cut > 0 ? displayName.Substring(0, cut) : displayName;
        }
    }
}
