using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// Bonus missions ("contracts"; Part B section 5: "Bonus missions unlock after each ante for extra points";
    /// Matthew, 25 Nov 2026: "clear and fun goals ... a point system and goals that make the game engaging").
    ///
    /// At each break the player is offered two contracts of different kinds for the next ante and may take one or
    /// none. A contract is one specific goal with its reward printed on it (goal-setting: specific and a little hard
    /// beats "do your best"; the goal gradient: its count shows how close it is). It lasts that ante only, overtime
    /// included, and pays once, the moment it is done. Nothing is lost by refusing one, and an unfinished one only
    /// goes away: no penalty, no streak, nothing outside the run (constitution rule 9).
    ///
    /// Kinds use only what every world already has: enemies (every world has camps), a landing, style kills, signal
    /// masts and salvage. Worlds named on a contract are the nearest few to the player when the break opens, so a
    /// contract never asks for a trip the clock cannot fit.
    ///
    /// Plain C#, no Unity types. AnteLoop offers and holds the contract; TaskDirector reports what happens.
    /// </summary>
    [Serializable]
    public sealed class ContractRules
    {
        /// <summary>Untick to play with no bonus missions at the break.</summary>
        public bool enabled = true;
        /// <summary>How many contracts the break offers (each a different kind).</summary>
        public int offers = 2;
        /// <summary>Named worlds are picked from this many of the nearest worlds.</summary>
        public int nearestWorlds = 4;

        /// <summary>Bounty: enemies to take down on one world: this many at ante 2, plus one every <see cref="huntEveryAntes"/>.</summary>
        public int huntFirst = 5;
        public int huntEveryAntes = 3;
        public int huntMost = 9;
        /// <summary>Style: kills of one kind of style feat.</summary>
        public int styleCount = 3;
        /// <summary>Signal masts to bring down, anywhere.</summary>
        public int mastCount = 1;
        /// <summary>Salvage crates to bring aboard, anywhere.</summary>
        public int salvageCount = 1;

        /// <summary>
        /// Base points for each kind, before the ante's value (the same scale as every other award; the score service
        /// keeps a mission between AnteBalance.optionalMissionMin and Max). Ante 1's target is 850, so a contract is
        /// worth about a third of an early ante: a real reason to take one, never a replacement for playing.
        /// </summary>
        public int huntPoints = 300;
        public int landPoints = 260;
        public int stylePoints = 340;
        public int takenApartPoints = 400;
        public int mastPoints = 320;
        public int salvagePoints = 380;

        public int HuntCount(int ante)
        {
            int every = Math.Max(1, huntEveryAntes);
            return Math.Max(1, Math.Min(huntMost, huntFirst + Math.Max(0, ante - 2) / every));
        }
    }

    public enum ContractKind
    {
        /// <summary>Take down enemies on one named world.</summary>
        Bounty = 0,
        /// <summary>Land the ship on a named world you have not landed on this run.</summary>
        Survey = 1,
        /// <summary>Kills with one kind of style feat, anywhere.</summary>
        Style = 2,
        /// <summary>Bring down signal masts, anywhere.</summary>
        Masts = 3,
        /// <summary>Bring salvage crates aboard the ship, anywhere.</summary>
        Salvage = 4,
    }

    public enum ContractStyle
    {
        None = 0,
        OneShot = 1,
        LongShot = 2,
        Airborne = 3,
        Melee = 4,
        TakenApart = 5,
    }

    /// <summary>A world a contract may name: the nearest few, given by the game when the break opens.</summary>
    public struct ContractWorld
    {
        public int body;
        public string name;
        /// <summary>The ship has landed there this run (it cannot be a survey).</summary>
        public bool landedOn;
        /// <summary>The player is there now.</summary>
        public bool here;
    }

    /// <summary>One bonus mission: what to do, how far along, and what it pays.</summary>
    public sealed class Contract
    {
        public ContractKind Kind;
        public ContractStyle Style;
        /// <summary>The named world's body index, or -1 for "anywhere".</summary>
        public int Body = -1;
        public string World;
        public int Needed = 1;
        public int Done;
        public int BasePoints;
        /// <summary>The ante it is for.</summary>
        public int Ante;
        /// <summary>Paid (it stays on screen as DONE until the ante ends).</summary>
        public bool Paid;

        public bool Complete => Done >= Needed;
        public string ClaimId => "contract.ante" + Ante;

        static string Upper(string s) => string.IsNullOrEmpty(s) ? "?" : s.ToUpperInvariant();
        static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);

        public static string StyleName(ContractStyle s)
        {
            switch (s)
            {
                case ContractStyle.OneShot: return "ONE SHOT";
                case ContractStyle.LongShot: return "LONG SHOT";
                case ContractStyle.Airborne: return "AIRBORNE";
                case ContractStyle.Melee: return "MELEE";
                case ContractStyle.TakenApart: return "TAKEN APART";
                default: return "STYLE";
            }
        }

        static string StyleHow(ContractStyle s)
        {
            switch (s)
            {
                case ContractStyle.OneShot: return "the first hit kills";
                case ContractStyle.LongShot: return "from long range";
                case ContractStyle.Airborne: return "while off the ground";
                case ContractStyle.Melee: return "with a melee weapon";
                case ContractStyle.TakenApart: return "all four limbs off first";
                default: return string.Empty;
            }
        }

        /// <summary>Short name, for the readout and the pop-up: "BOUNTY ON MARS".</summary>
        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case ContractKind.Bounty: return "BOUNTY ON " + Upper(World);
                    case ContractKind.Survey: return "SURVEY " + Upper(World);
                    case ContractKind.Style: return StyleName(Style) + " KILLS";
                    case ContractKind.Masts: return "SIGNAL JAMMER";
                    case ContractKind.Salvage: return "SALVAGE RUN";
                    default: return "CONTRACT";
                }
            }
        }

        /// <summary>The whole task in one sentence, for the break: "Take down 6 enemies on Mars."</summary>
        public string Task
        {
            get
            {
                switch (Kind)
                {
                    case ContractKind.Bounty: return "Take down " + Plural(Needed, "enemy", "enemies") + " on " + World + ".";
                    case ContractKind.Survey: return "Land the ship on " + World + ".";
                    case ContractKind.Style:
                        return "Get " + Plural(Needed, StyleName(Style) + " kill", StyleName(Style) + " kills") + " (" + StyleHow(Style) + "), anywhere.";
                    case ContractKind.Masts: return "Bring down " + Plural(Needed, "signal mast", "signal masts") + ", anywhere.";
                    case ContractKind.Salvage: return "Bring " + Plural(Needed, "salvage crate", "salvage crates") + " aboard your ship, anywhere.";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>The readout line: "BOUNTY ON MARS 3/6" or "SURVEY TITAN: DONE".</summary>
        public string Progress
        {
            get
            {
                if (Paid || Complete) return Title + ": DONE";
                return Needed > 1 ? Title + "  " + Done + "/" + Needed : Title;
            }
        }

        // ------------------------------------------------------------------ what happened (true when it moved)

        /// <summary>An enemy the player killed went down on <paramref name="body"/>.</summary>
        public bool Kill(int body, KillFacts facts)
        {
            if (Complete) return false;
            switch (Kind)
            {
                case ContractKind.Bounty:
                    if (body != Body) return false;
                    break;
                case ContractKind.Style:
                    if (!Matches(Style, facts)) return false;
                    break;
                default:
                    return false;
            }
            Done++;
            return true;
        }

        public static bool Matches(ContractStyle style, KillFacts f)
        {
            switch (style)
            {
                case ContractStyle.OneShot: return f.oneShot && !f.rover;
                case ContractStyle.LongShot: return f.longShot;
                case ContractStyle.Airborne: return f.airborne && !f.rover;
                case ContractStyle.Melee: return f.melee;
                case ContractStyle.TakenApart: return f.takenApart;
                default: return false;
            }
        }

        /// <summary>The ship touched down on <paramref name="body"/>.</summary>
        public bool Landed(int body)
        {
            if (Complete || Kind != ContractKind.Survey || body != Body) return false;
            Done = Needed;
            return true;
        }

        public bool MastDown()
        {
            if (Complete || Kind != ContractKind.Masts) return false;
            Done++;
            return true;
        }

        public bool CrateDelivered()
        {
            if (Complete || Kind != ContractKind.Salvage) return false;
            Done++;
            return true;
        }
    }

    /// <summary>Draws the break's offers.</summary>
    public static class ContractBoard
    {
        static readonly ContractStyle[] Styles =
            { ContractStyle.OneShot, ContractStyle.LongShot, ContractStyle.Airborne, ContractStyle.TakenApart, ContractStyle.Melee };

        /// <summary>
        /// Up to <see cref="ContractRules.offers"/> contracts for <paramref name="ante"/>, each of a different kind.
        /// <paramref name="nearby"/> is the worlds nearest the player, nearest first. Melee style is only offered to a
        /// player holding a melee weapon (they are rare and must be found). Same inputs, same offers.
        /// </summary>
        public static List<Contract> Offer(ContractRules rules, int ante, IReadOnlyList<ContractWorld> nearby, bool hasMelee, SeededRandom random)
        {
            var offers = new List<Contract>();
            if (rules == null || !rules.enabled || rules.offers <= 0 || random == null) return offers;

            int reach = Math.Max(1, rules.nearestWorlds);
            var huntWorlds = new List<ContractWorld>();
            var surveyWorlds = new List<ContractWorld>();
            if (nearby != null)
            {
                for (int i = 0; i < nearby.Count && i < reach + 1; i++)
                {
                    ContractWorld w = nearby[i];
                    if (w.body < 0 || string.IsNullOrEmpty(w.name)) continue;
                    if (huntWorlds.Count < reach) huntWorlds.Add(w);
                    if (!w.here && !w.landedOn && surveyWorlds.Count < reach) surveyWorlds.Add(w);
                }
            }

            var kinds = new List<ContractKind> { ContractKind.Style, ContractKind.Masts, ContractKind.Salvage };
            if (huntWorlds.Count > 0) kinds.Add(ContractKind.Bounty);
            if (surveyWorlds.Count > 0) kinds.Add(ContractKind.Survey);
            random.Shuffle(kinds);

            for (int i = 0; i < kinds.Count && offers.Count < rules.offers; i++)
            {
                var c = new Contract { Kind = kinds[i], Ante = ante };
                switch (c.Kind)
                {
                    case ContractKind.Bounty:
                    {
                        ContractWorld w = random.Pick(huntWorlds);
                        c.Body = w.body;
                        c.World = w.name;
                        c.Needed = rules.HuntCount(ante);
                        c.BasePoints = rules.huntPoints;
                        break;
                    }
                    case ContractKind.Survey:
                    {
                        ContractWorld w = random.Pick(surveyWorlds);
                        c.Body = w.body;
                        c.World = w.name;
                        c.Needed = 1;
                        c.BasePoints = rules.landPoints;
                        break;
                    }
                    case ContractKind.Style:
                    {
                        int count = hasMelee ? Styles.Length : Styles.Length - 1;
                        c.Style = Styles[random.Range(0, count)];
                        bool hard = c.Style == ContractStyle.TakenApart;
                        c.Needed = hard ? 1 : Math.Max(1, rules.styleCount);
                        c.BasePoints = hard ? rules.takenApartPoints : rules.stylePoints;
                        break;
                    }
                    case ContractKind.Masts:
                        c.Needed = Math.Max(1, rules.mastCount);
                        c.BasePoints = rules.mastPoints;
                        break;
                    case ContractKind.Salvage:
                        c.Needed = Math.Max(1, rules.salvageCount);
                        c.BasePoints = rules.salvagePoints;
                        break;
                }
                offers.Add(c);
            }
            return offers;
        }
    }
}
