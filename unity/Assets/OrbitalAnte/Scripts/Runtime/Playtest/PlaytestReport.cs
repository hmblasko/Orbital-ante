using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;

namespace OrbitalAnte.Playtest
{
    /// <summary>
    /// Turns a run's <see cref="PlaytestLog"/> into the text written to the playtest file (Ralph loop pass 9): the run, then
    /// one section for each thing a pass's playtest protocol asks to be counted (the creatures' warnings and the dodge for
    /// pass 2, the music for pass 3, where creatures were hit for pass 7, the animals met for pass 8, the unlocks for passes
    /// 1 and 5), one row per warning, then every event in order. Counts only, never percentages, and never a judgment of
    /// the play. Plain C#.
    /// </summary>
    public static class PlaytestReport
    {
        public const string Title = "ORBITAL ANTE PLAYTEST LOG";

        public static string Write(PlaytestLog log)
        {
            var sb = new StringBuilder(32768);
            Header(sb, log);
            AntesAndPoints(sb, log);
            Unlocks(sb, log);
            Warnings(sb, log);
            Dodges(sb, log);
            CreatureHits(sb, log);
            Met(sb, log);
            People(sb, log);
            HurtAndDeaths(sb, log);
            Music(sb, log);
            EveryWarning(sb, log);
            Events(sb, log);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the run

        static void Header(StringBuilder sb, PlaytestLog log)
        {
            sb.Append(Title).Append('\n');
            sb.Append("What happened in one run, kept by a development build for counting playtests (docs/reports/playtest-log.md).\n");
            sb.Append("It says what happened, not whether it was fun.\n\n");
            sb.Append("Run:      ").Append(log.Scenario).Append(", seed ").Append(log.Seed.ToString(CultureInfo.InvariantCulture));
            if (log.RunNumber > 0) sb.Append(", the profile's run ").Append(PlaytestLog.N(log.RunNumber));
            sb.Append('\n');
            sb.Append("Started:  ").Append(log.StartedAt).Append(" (this machine's clock)\n");
            if (!string.IsNullOrEmpty(log.Build)) sb.Append("Build:    ").Append(log.Build).Append('\n');
            sb.Append("Kit:      ").Append(Or(log.Kit, "none")).Append("    Upgrade in use: ").Append(Or(log.Upgrade, "none"))
              .Append("    New-player tips: ").Append(log.TipsOn ? "on" : "off").Append('\n');
            if (log.UnlocksHeld >= 0 || !string.IsNullOrEmpty(log.LooksInUse))
            {
                sb.Append("Profile:  ").Append(log.UnlocksHeld >= 0 ? Count(log.UnlocksHeld, "unlock", "unlocks") + " earned" : "unlocks not read")
                  .Append("; looks in use: ").Append(Or(log.LooksInUse, "none")).Append('\n');
            }
            sb.Append("Length:   ").Append(PlaytestLog.Minutes(log.Length));
            if (log.Pauses > 0) sb.Append(" (paused ").Append(Count(log.Pauses, "time", "times")).Append(", ").Append(PlaytestLog.Minutes(log.PausedSeconds)).Append(" in all)");
            sb.Append('\n');
            sb.Append("Ended:    ").Append(PlaytestLog.EndPhrase(log.End));
            if (!string.IsNullOrEmpty(log.EndWords)) sb.Append(" (\"").Append(log.EndWords).Append("\")");
            if (log.AnteReached > 0) sb.Append(", on ante ").Append(PlaytestLog.N(log.AnteReached)).Append(", score ").Append(PlaytestLog.N(log.Score));
            sb.Append('\n');
            sb.Append("Times (m:ss) are real time from the start of the run, pauses included, so they line up with a screen\n");
            sb.Append("recording started with the run. How long something took (a kill, a dodge after a mark) is in game seconds,\n");
            sb.Append("which stop for pauses and slow with Focus, as the creatures' and the guns' own clocks do.\n");
        }

        static void AntesAndPoints(StringBuilder sb, PlaytestLog log)
        {
            if (log.Antes.Count > 0)
            {
                Heading(sb, "ANTES");
                sb.Append("  The ante's own clock stops for pauses, and ante 1's waits on the pad until liftoff; \"left\" is read from it.\n");
                for (int i = 0; i < log.Antes.Count; i++)
                {
                    AnteRecord a = log.Antes[i];
                    sb.Append("  Ante ").Append(PlaytestLog.N(a.Ante)).Append(": began ").Append(PlaytestLog.Minutes(a.BeganAt))
                      .Append(", ").Append(PlaytestLog.N(a.Target)).Append(" to bank");
                    if (a.ClockFrom >= 0f) sb.Append(", its clock started ").Append(PlaytestLog.Minutes(a.ClockFrom));
                    if (a.OvertimeAt >= 0f) sb.Append(", overtime from ").Append(PlaytestLog.Minutes(a.OvertimeAt));
                    if (a.TargetMetAt >= 0f)
                    {
                        sb.Append(", target met ").Append(PlaytestLog.Minutes(a.TargetMetAt));
                        if (a.LeftAtTarget >= 0f) sb.Append(" (").Append(PlaytestLog.Minutes(a.LeftAtTarget)).Append(" left)");
                    }
                    if (a.ClearedAt >= 0f)
                    {
                        sb.Append(", cleared ").Append(PlaytestLog.Minutes(a.ClearedAt)).Append(" with ").Append(PlaytestLog.N(a.Banked)).Append(" banked");
                        if (a.LeftAtClear >= 0f) sb.Append(" (").Append(PlaytestLog.Minutes(a.LeftAtClear)).Append(" left)");
                    }
                    else sb.Append(", not cleared");
                    sb.Append(".\n");
                    if (a.Perks.Count > 0) sb.Append("    Perks taken: ").Append(string.Join(", ", a.Perks)).Append(".\n");
                }
            }
            if (log.Awards == 0 && log.Refused.Count == 0 && log.NotesOffered == 0 && log.NotesShown == 0) return;
            Heading(sb, "POINTS");
            sb.Append("  ").Append(Count(log.Awards, "award", "awards")).Append(", ").Append(PlaytestLog.N(log.PointsAwarded)).Append(" points.\n");
            foreach (KeyValuePair<string, Tally> p in Sorted(log.Paid))
                sb.Append("    ").Append(p.Key).Append(": ").Append(PlaytestLog.N(p.Value.Count)).Append(" for ").Append(PlaytestLog.N((long)p.Value.Total)).Append('\n');
            if (log.Refused.Count > 0)
            {
                sb.Append("  Not paid:\n");
                foreach (KeyValuePair<string, Tally> p in Sorted(log.Refused)) sb.Append("    ").Append(p.Key).Append(": ").Append(PlaytestLog.N(p.Value.Count)).Append('\n');
            }
            if (log.NotesOffered > 0 || log.NotesShown > 0)
            {
                sb.Append("  Notes on the readout: ").Append(PlaytestLog.N(log.NotesOffered)).Append(" offered, ").Append(PlaytestLog.N(log.NotesShown))
                  .Append(" shown (a plain note offered during an important one's hold can be replaced before it shows):\n");
                foreach (KeyValuePair<string, Tally> p in Sorted(log.Notes)) sb.Append("    ").Append(PlaytestLog.N(p.Value.Count)).Append(" x ").Append(p.Key).Append('\n');
            }
        }

        static void Unlocks(StringBuilder sb, PlaytestLog log)
        {
            if (log.Unlocks.Count == 0) return;
            Heading(sb, "UNLOCKS (passes 1 and 5)");
            for (int i = 0; i < log.Unlocks.Count; i++) sb.Append("  ").Append(log.Unlocks[i]).Append('\n');
        }

        // ------------------------------------------------------------------ creatures

        static void Warnings(StringBuilder sb, PlaytestLog log)
        {
            if (log.Warnings.Count == 0) return;
            Heading(sb, "CREATURE WARNINGS (pass 2: move on the mark)");
            sb.Append("  Marked: it took aim (its mark sounded). Hurt you: its strike did. Missed: it struck and did not.\n");
            sb.Append("  Stopped: it took aim but never struck. Killed: you killed it during the warning before it hurt you.\n");
            sb.Append("  Called off: it stopped before its mark. Under way: not over when the log was written.\n");
            sb.Append("  ").Append(Col("kind", 10)).Append(Num("warnings", 9)).Append(Num("marked", 7)).Append(Num("hurt you", 9)).Append(Num("missed", 7))
              .Append(Num("stopped", 8)).Append(Num("killed", 7)).Append(Num("called off", 11)).Append(Num("under way", 10)).Append('\n');
            foreach (string kind in KindsIn(log.Warnings))
            {
                int all = 0, marked = 0, hurt = 0, missed = 0, stopped = 0, killed = 0, off = 0, open = 0;
                for (int i = 0; i < log.Warnings.Count; i++)
                {
                    WarningRecord w = log.Warnings[i];
                    if (w.Animal.Kind != kind) continue;
                    all++;
                    if (w.Marked) marked++;
                    switch (w.End)
                    {
                        case WarningEnd.Hurt: hurt++; break;
                        case WarningEnd.Missed: missed++; break;
                        case WarningEnd.Stopped: stopped++; break;
                        case WarningEnd.Killed: killed++; break;
                        case WarningEnd.CalledOff: off++; break;
                        default: open++; break;
                    }
                }
                sb.Append("  ").Append(Col(kind, 10)).Append(Num(all, 9)).Append(Num(marked, 7)).Append(Num(hurt, 9)).Append(Num(missed, 7))
                  .Append(Num(stopped, 8)).Append(Num(killed, 7)).Append(Num(off, 11)).Append(Num(open, 10)).Append('\n');
            }
            sb.Append("  Hurt by a creature with no marked warning of its kind under way (a Roller's butt has none): ").Append(PlaytestLog.N(log.UnwarnedHurts)).Append('\n');
            if (log.WarningsDropped > 0) sb.Append("  Warnings not kept (the log was full): ").Append(PlaytestLog.N(log.WarningsDropped)).Append('\n');
        }

        static void Dodges(StringBuilder sb, PlaytestLog log)
        {
            int marked = 0;
            for (int i = 0; i < log.Warnings.Count; i++) if (log.Warnings[i].Marked) marked++;
            if (marked == 0) return;
            Heading(sb, "THE DODGE (pass 2: marked warnings only)");
            sb.Append("  Moving across: faster than ").Append(PlaytestLog.Amount(DodgeRules.SidestepSpeed))
              .Append(" m/s across the line between you and it, along the ground (a first guess; walking is 5).\n");
            sb.Append("  Already: moving across as it took aim. On its mark: began moving across after its mark, before it struck.\n");
            sb.Append("  After the strike: began only once it struck. Not across: did not move across from its mark on.\n");
            sb.Append("  ").Append(Col("kind", 10)).Append(Num("marked", 7)).Append(Num("already", 8)).Append(Num("on its mark", 12)).Append(Num("after the strike", 17))
              .Append(Num("not across", 11)).Append('\n');
            List<string> kinds = KindsIn(log.Warnings);
            foreach (string kind in kinds)
            {
                int n = 0, already = 0, onMark = 0, after = 0, not = 0;
                for (int i = 0; i < log.Warnings.Count; i++)
                {
                    WarningRecord w = log.Warnings[i];
                    if (w.Animal.Kind != kind || !w.Marked) continue;
                    n++;
                    switch (w.Move)
                    {
                        case MoveOnMark.AlreadyMoving: already++; break;
                        case MoveOnMark.OnTheMark: onMark++; break;
                        case MoveOnMark.AfterTheStrike: after++; break;
                        default: not++; break;
                    }
                }
                if (n == 0) continue;
                sb.Append("  ").Append(Col(kind, 10)).Append(Num(n, 7)).Append(Num(already, 8)).Append(Num(onMark, 12)).Append(Num(after, 17)).Append(Num(not, 11)).Append('\n');
            }
            sb.Append("  Where it was at its mark: ahead (within ").Append(PlaytestLog.AheadMost.ToString("0", CultureInfo.InvariantCulture))
              .Append(" degrees of where you looked), to the side, behind (").Append(PlaytestLog.BehindLeast.ToString("0", CultureInfo.InvariantCulture))
              .Append(" or more); in view: inside the screen's edges.\n");
            sb.Append("  Turned to face it: it began behind you and was ahead by its mark. Shots: fired while it warned or struck.\n");
            sb.Append("  ").Append(Col("kind", 10)).Append(Num("ahead", 6)).Append(Num("side", 5)).Append(Num("behind", 7)).Append(Num("in view", 8))
              .Append(Num("turned to face it", 18)).Append(Num("shots", 6)).Append('\n');
            foreach (string kind in kinds)
            {
                int n = 0, ahead = 0, side = 0, behind = 0, inView = 0, turned = 0, shots = 0;
                for (int i = 0; i < log.Warnings.Count; i++)
                {
                    WarningRecord w = log.Warnings[i];
                    if (w.Animal.Kind != kind || !w.Marked) continue;
                    n++;
                    string facing = PlaytestLog.FacingWords(w.FacingAtMark);
                    if (facing == "ahead") ahead++;
                    else if (facing == "side") side++;
                    else if (facing == "behind") behind++;
                    if (w.InFrameAtMark) inView++;
                    if (w.FacingAtBegin >= PlaytestLog.BehindLeast && w.FacingAtMark >= 0f && w.FacingAtMark <= PlaytestLog.AheadMost) turned++;
                    shots += w.Shots;
                }
                if (n == 0) continue;
                sb.Append("  ").Append(Col(kind, 10)).Append(Num(ahead, 6)).Append(Num(side, 5)).Append(Num(behind, 7)).Append(Num(inView, 8))
                  .Append(Num(turned, 18)).Append(Num(shots, 6)).Append('\n');
            }
        }

        static void CreatureHits(StringBuilder sb, PlaytestLog log)
        {
            bool any = false;
            for (int i = 0; i < log.Animals.Count && !any; i++) any = !log.Animals[i].IsGroup && log.Animals[i].TotalHits > 0;
            if (!any && log.AimLineRecords.Count == 0) return;
            Heading(sb, "HITS ON CREATURES (pass 7: where to shoot)");
            sb.Append("  Armour: it held (grey mark). Weak place: the quickest kill (gold). Weapon off: a claw, jaw or drum shot off (cyan).\n");
            sb.Append("  ").Append(Col("kind", 10)).Append(Num("armour", 7)).Append(Num("weak place", 11)).Append(Num("weapon off", 11)).Append(Num("plain", 6))
              .Append(Num("kills", 6)).Append(Num("through the weak place", 23)).Append(Num("aim lines", 10)).Append('\n');
            foreach (string kind in KindsIn(log.Animals))
            {
                int armour = 0, vital = 0, off = 0, plain = 0, kills = 0, throughWeak = 0;
                for (int i = 0; i < log.Animals.Count; i++)
                {
                    AnimalRecord a = log.Animals[i];
                    if (a.IsGroup || a.Kind != kind) continue;
                    plain += a.Hits[(int)HitZone.Plain];
                    armour += a.Hits[(int)HitZone.Armour];
                    vital += a.Hits[(int)HitZone.Vital];
                    off += a.Hits[(int)HitZone.WeaponOff];
                    if (!a.Killed) continue;
                    kills++;
                    if (a.KillZone == HitZone.Vital) throughWeak++;
                }
                log.AimLines.TryGetValue(kind, out int lines);
                if (armour + vital + off + plain == 0 && lines == 0) continue;
                sb.Append("  ").Append(Col(kind, 10)).Append(Num(armour, 7)).Append(Num(vital, 11)).Append(Num(off, 11)).Append(Num(plain, 6))
                  .Append(Num(kills, 6)).Append(Num(throughWeak, 23)).Append(Num(lines, 10)).Append('\n');
            }
            if (log.AimLineRecords.Count == 0) return;
            sb.Append("  While each aim line showed, looked at a few times a second along the line's words (pass 7's question: does it\n");
            sb.Append("  ever cover the place it names?):\n");
            for (int i = 0; i < log.AimLineRecords.Count; i++)
            {
                AimLineRecord r = log.AimLineRecords[i];
                sb.Append("    ").Append(PlaytestLog.Clock(r.At)).Append("  ").Append(r.Name).Append(": ");
                if (r.Looks == 0) sb.Append("not looked at (the line went, or the creature did, before the first look)");
                else
                    sb.Append(Count(r.Looks, "look", "looks")).Append("; it lay over the creature in ").Append(PlaytestLog.N(r.OverIt))
                      .Append(", over its weak place in ").Append(PlaytestLog.N(r.OverWeak));
                sb.Append(".\n");
            }
        }

        static void Met(StringBuilder sb, PlaytestLog log)
        {
            bool any = false;
            for (int i = 0; i < log.Animals.Count && !any; i++) any = log.Animals[i].Group == null;
            if (!any) return;
            Heading(sb, "CREATURES MET (pass 8: animals on the worlds; practice range ones too)");
            sb.Append("  Met: woken by a world (or laid out by the practice range), or first heard from at its warning or a hit.\n");
            sb.Append("  In view: inside the screen's edges with the ground not between you (hills and the horizon checked on the\n");
            sb.Append("  worlds; rocks, trees, buildings, night and water not checked). A pack is counted as one.\n");
            sb.Append("  ").Append(Col("kind", 12)).Append(Num("met", 4)).Append(Num("in view", 8)).Append(Num("after you", 10)).Append(Num("shot", 5))
              .Append(Num("killed by you", 14)).Append(Num("killed you", 11)).Append(Num("put away", 9)).Append(Num("still there", 12)).Append('\n');
            var kinds = new List<string>();
            for (int i = 0; i < log.Animals.Count; i++)
            {
                AnimalRecord a = log.Animals[i];
                if (a.Group == null && !kinds.Contains(a.Kind)) kinds.Add(a.Kind);
            }
            int notPaid = 0, peopleSaw = 0, peopleFirst = 0;
            foreach (string kind in Ordered(new HashSet<string>(kinds)))
            {
                int met = 0, seen = 0, after = 0, shot = 0, killed = 0, killedYou = 0, away = 0, still = 0;
                for (int i = 0; i < log.Animals.Count; i++)
                {
                    AnimalRecord a = log.Animals[i];
                    if (a.Group != null || a.Kind != kind) continue;
                    met++;
                    if (a.SeenAt >= 0f) seen++;
                    if (a.NoticedAt >= 0f) after++;
                    if (a.TotalHits > 0) shot++;
                    switch (a.End)
                    {
                        case AnimalEnd.KilledByYou:
                        case AnimalEnd.AllDead: killed++; break;
                        case AnimalEnd.KilledYou: killedYou++; break;
                        case AnimalEnd.PutAway: away++; break;
                        default: still++; break;
                    }
                    if (a.EnemySawYou) peopleSaw++;
                    if (a.EnemySawYou && a.PeopleFirst) peopleFirst++;
                }
                sb.Append("  ").Append(Col(kind, 12)).Append(Num(met, 4)).Append(Num(seen, 8)).Append(Num(after, 10)).Append(Num(shot, 5))
                  .Append(Num(killed, 14)).Append(Num(killedYou, 11)).Append(Num(away, 9)).Append(Num(still, 12)).Append('\n');
            }
            for (int i = 0; i < log.Animals.Count; i++)
                if (log.Animals[i].Killed && log.Animals[i].Claim != null && !log.Animals[i].Paid) notPaid++;
            sb.Append("  Kills on the worlds that paid nothing: ").Append(PlaytestLog.N(notPaid)).Append(" (a pack's fish counted one by one). An enemy had you in sight during a\n");
            sb.Append("  creature's fight: ").Append(PlaytestLog.N(peopleSaw)).Append(" (").Append(PlaytestLog.N(peopleFirst))
              .Append(" of them with people already after you as it began).\n");
            if (log.AnimalsDropped > 0) sb.Append("  Creatures not kept (the log was full): ").Append(PlaytestLog.N(log.AnimalsDropped)).Append('\n');
            sb.Append('\n');
            for (int i = 0; i < log.Animals.Count; i++)
            {
                AnimalRecord a = log.Animals[i];
                // A pack's fish are counted in its lines.
                if (a.Group != null) continue;
                AnimalLines(sb, log, a);
            }
        }

        static void AnimalLines(StringBuilder sb, PlaytestLog log, AnimalRecord a)
        {
            sb.Append("  ").Append(a.Name).Append(" (").Append(PlaytestLog.AnimalOf(a.IsGroup ? PlaytestLog.MemberOf(a.Kind) : a.Kind) ?? a.Kind).Append(") ")
              .Append(a.InTheRange || a.World == "practice range" ? "in the practice range" : "on " + a.World).Append(", ante ").Append(PlaytestLog.N(a.Ante)).Append(":\n");

            var parts = new List<string>();
            if (a.WokeAt >= 0f) parts.Add((a.InTheRange ? "laid out " : "woke ") + PlaytestLog.Minutes(a.WokeAt) + " at " + PlaytestLog.Metres(a.WokeFrom));
            else parts.Add("met at its first warning or hit");
            parts.Add(a.SeenAt >= 0f ? "first in view " + PlaytestLog.Minutes(a.SeenAt) + " at " + PlaytestLog.Metres(a.SeenFrom) : "never in view");
            parts.Add(a.NoticedAt >= 0f ? "after you from " + PlaytestLog.Minutes(a.NoticedAt) : NoticeWords(a));
            Lines(sb, parts);

            parts.Clear();
            parts.Add(a.TotalHits > 0 ? "shot: " + HitWords(a) : "not shot");
            if (a.IsGroup) parts.Add(PlaytestLog.N(a.Kills) + " fish killed" + (a.Kills > 0 ? " (" + PackPay(log, a) + ")" : ""));
            if (a.Warnings > 0 || a.IsGroup) parts.Add(WarningWords(log, a));
            if (a.DamageToYou > 0f) parts.Add("hurt you " + PlaytestLog.Amount(a.DamageToYou));
            if (a.AimLineShown) parts.Add("an aim line named it");
            Lines(sb, parts);

            parts.Clear();
            if (a.FightFrom >= 0f)
                parts.Add(a.EnemySawYou ? "an enemy had you in sight during its fight" + (a.PeopleFirst ? " (people were after you already as it began)" : " (only after it began)")
                                        : "no enemy saw you during its fight");
            if (a.Slips >= 0) parts.Add("feet slipped " + Count(a.Slips, "time", "times"));
            if (a.WakeUntimed) parts.Add("its wake not timed (a pause or a write of this log fell in it)");
            else if (a.WakeFrameMs >= 0f)
                parts.Add("its wake frame " + Ms(a.WakeFrameOwnMs) + ", the longest in the half second after " + Ms(a.WakeFrameMs)
                          + (a.WakeBaselineMs >= 0f ? ", the middle frame of the second before " + Ms(a.WakeBaselineMs) : "")
                          + (a.WakeFirstOfKind ? " (the first of its kind this session)" : ""));
            if (parts.Count > 0) Lines(sb, parts);

            sb.Append("    ").Append(EndWords(a)).Append(".\n");
        }

        static void Lines(StringBuilder sb, List<string> parts)
        {
            if (parts.Count == 0) return;
            sb.Append("    ").Append(string.Join("; ", parts)).Append(".\n");
        }

        /// <summary>Why "after you" is not counted for the kinds that do not say it (the Cave hunter has no state for it).</summary>
        static string NoticeWords(AnimalRecord a)
        {
            string kind = a.IsGroup ? PlaytestLog.MemberOf(a.Kind) : a.Kind;
            return kind == "Hunter" ? "whether it was after you is not read for this kind" : "never after you";
        }

        static string HitWords(AnimalRecord a)
        {
            var words = new List<string>();
            if (a.Hits[(int)HitZone.Vital] > 0) words.Add(PlaytestLog.N(a.Hits[(int)HitZone.Vital]) + " weak place");
            if (a.Hits[(int)HitZone.Armour] > 0) words.Add(PlaytestLog.N(a.Hits[(int)HitZone.Armour]) + " armour");
            if (a.Hits[(int)HitZone.WeaponOff] > 0) words.Add(PlaytestLog.N(a.Hits[(int)HitZone.WeaponOff]) + " weapon off");
            if (a.Hits[(int)HitZone.Plain] > 0) words.Add(PlaytestLog.N(a.Hits[(int)HitZone.Plain]) + " plain");
            return Count(a.TotalHits, "hit", "hits") + " (" + string.Join(", ", words) + ")";
        }

        static string WarningWords(PlaytestLog log, AnimalRecord a)
        {
            int all = 0, marked = 0, hurt = 0, missed = 0, stopped = 0, killed = 0, off = 0;
            // The counts for this animal (and a pack's from its fish). The report is written a few times a run, so one look
            // through the warnings per animal line is cheap enough: at the caps that is 400 x 2,000.
            for (int i = 0; i < log.Warnings.Count; i++)
            {
                WarningRecord w = log.Warnings[i];
                if (w.Animal != a && w.Animal.Group != a) continue;
                all++;
                if (w.Marked) marked++;
                switch (w.End)
                {
                    case WarningEnd.Hurt: hurt++; break;
                    case WarningEnd.Missed: missed++; break;
                    case WarningEnd.Stopped: stopped++; break;
                    case WarningEnd.Killed: killed++; break;
                    case WarningEnd.CalledOff: off++; break;
                }
            }
            if (all == 0) return "no warnings";
            return Count(all, "warning", "warnings") + " (" + PlaytestLog.N(marked) + " marked: " + PlaytestLog.N(hurt) + " hurt you, " + PlaytestLog.N(missed) + " missed"
                   + (stopped > 0 ? ", " + PlaytestLog.N(stopped) + " stopped" : "") + "; " + PlaytestLog.N(off) + " called off"
                   + (killed > 0 ? "; killed during " + PlaytestLog.N(killed) : "") + ")";
        }

        static string EndWords(AnimalRecord a)
        {
            switch (a.End)
            {
                case AnimalEnd.KilledByYou:
                    return "killed by you " + PlaytestLog.Minutes(a.EndAt) + " through its " + a.KillPiece + " (" + PlaytestLog.ZoneWords(a.KillZone) + "), "
                           + PlaytestLog.Seconds(a.EndGame - a.FirstHitGame) + " of game time after your first hit; " + PayWords(a);
                case AnimalEnd.KilledYou: return "killed you " + PlaytestLog.Minutes(a.EndAt);
                case AnimalEnd.PutAway:
                    return a.InTheRange ? "cleared " + PlaytestLog.Minutes(a.EndAt) + " (the practice range was laid out again)"
                                        : "put away " + PlaytestLog.Minutes(a.EndAt) + " (you left it)";
                case AnimalEnd.AllDead: return "every fish dead by " + PlaytestLog.Minutes(a.EndAt);
                default: return a.IsGroup && a.Kills > 0 ? "the rest still there at the end" : "still there at the end";
            }
        }

        /// <summary>What a pack's dead fish paid between them.</summary>
        static string PackPay(PlaytestLog log, AnimalRecord pack)
        {
            int paid = 0, points = 0, nothing = 0, range = 0, away = 0;
            for (int i = 0; i < log.Animals.Count; i++)
            {
                AnimalRecord fish = log.Animals[i];
                if (fish.Group != pack || !fish.Killed) continue;
                if (fish.Claim == null) range++;
                else if (fish.Paid)
                {
                    paid++;
                    points += fish.PaidPoints;
                }
                else if (fish.OutOfReach) away++;
                else nothing++;
            }
            var words = new List<string>();
            if (range > 0) words.Add("nothing pays for them here");
            if (paid > 0) words.Add(PlaytestLog.N(paid) + " paid " + PlaytestLog.N(points) + " points");
            if (away > 0) words.Add(PlaytestLog.N(away) + " paid nothing: you were not in the water with them");
            if (nothing > 0) words.Add(PlaytestLog.N(nothing) + " brought no points");
            return string.Join(", ", words);
        }

        /// <summary>What a kill paid, from the ante's own answer on its claim (never a guess).</summary>
        public static string PayWords(AnimalRecord a)
        {
            if (a.Claim == null) return "nothing pays for it here (the practice range gives no credit)";
            if (a.OutOfReach) return "it paid nothing: it could not answer you there (\"" + Tasks.CreatureTiers.NoPayNote(a.IsGroup ? PlaytestLog.MemberOf(a.Kind) : a.Kind) + "\")";
            if (a.PaidPoints > 0) return "it paid " + PlaytestLog.N(a.PaidPoints) + " points";
            if (a.PayRefused != null) return "it paid nothing: the ante refused it (" + a.PayRefused + ")";
            return "no points came for it";
        }

        // ------------------------------------------------------------------ people, hurt, music

        static void People(StringBuilder sb, PlaytestLog log)
        {
            if (log.PeopleHits == 0 && log.Guns.Count == 0 && log.Sightings == 0 && log.SquadsSent == 0 && log.Alarms == 0) return;
            Heading(sb, "PEOPLE AND GUNS");
            if (log.PeopleHits > 0)
            {
                sb.Append("  Hits on people ").Append(PlaytestLog.N(log.PeopleHits)).Append(" (heads ").Append(PlaytestLog.N(log.PeopleHeads))
                  .Append("), limbs off ").Append(PlaytestLog.N(log.PeopleLimbsOff)).Append(", guns shot out of hands ").Append(PlaytestLog.N(log.PeopleDisarmed))
                  .Append(", kills ").Append(PlaytestLog.N(log.PeopleKills)).Append(" (through the head ").Append(PlaytestLog.N(log.PeopleHeadKills)).Append(").\n");
            }
            if (log.Sightings > 0 || log.SquadsSent > 0 || log.Alarms > 0)
            {
                sb.Append("  Enemies picked you out ").Append(Count(log.Sightings, "time", "times")).Append(" (").Append(PlaytestLog.N(log.SightingsWhileHidden))
                  .Append(" while you were hidden); restless squads sent ").Append(PlaytestLog.N(log.SquadsSent)).Append("; alarms raised ")
                  .Append(PlaytestLog.N(log.Alarms)).Append(".\n");
            }
            var guns = new List<string>(log.Guns.Keys);
            guns.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < guns.Count; i++)
            {
                GunTally g = log.Guns[guns[i]];
                sb.Append("  ").Append(guns[i]).Append(": ").Append(Count(g.Shots, "shot", "shots")).Append(", ").Append(Count(g.Hits, "hit", "hits"))
                  .Append(", ").Append(Count(g.Kills, "kill", "kills")).Append('\n');
            }
        }

        static void HurtAndDeaths(StringBuilder sb, PlaytestLog log)
        {
            if (log.HurtBy.Count == 0 && log.Deaths.Count == 0) return;
            Heading(sb, log.InTheRange ? "YOUR HURT AND KNOCK-DOWNS" : "YOUR HURT AND DEATHS");
            sb.Append("  Armour lost, by what did it (the death page's words; a hit past the last plate counts only what was left):\n");
            foreach (KeyValuePair<string, Tally> p in Sorted(log.HurtBy))
            {
                sb.Append("  ").Append(p.Key == PlaytestLog.ShotsAndOther ? p.Key : "\"" + p.Key + "\"").Append(": ").Append(PlaytestLog.Amount((float)p.Value.Total))
                  .Append(" in ").Append(Count(p.Value.Count, "hit", "hits")).Append('\n');
            }
            sb.Append(log.InTheRange ? "  Knock-downs: " : "  Deaths: ").Append(PlaytestLog.N(log.Deaths.Count)).Append('\n');
            for (int i = 0; i < log.Deaths.Count; i++) sb.Append("    ").Append(log.Deaths[i]).Append('\n');
        }

        static void Music(StringBuilder sb, PlaytestLog log)
        {
            double total = 0.0;
            for (int b = 0; b <= (int)IntensityBand.Chaos; b++) total += log.SecondsIn((IntensityBand)b);
            if (total <= 0.0 && log.FightMusicTimes == 0 && log.LayerChanges == 0) return;
            Heading(sb, "MUSIC (pass 3)");
            if (total > 0.0)
            {
                sb.Append("  Time the music aimed for each band (the layers heard follow a bar or two later; paused time not counted):");
                for (int b = 0; b <= (int)IntensityBand.Chaos; b++)
                {
                    double s = log.SecondsIn((IntensityBand)b);
                    if (s > 0.0) sb.Append(' ').Append((IntensityBand)b).Append(' ').Append(PlaytestLog.Minutes(s)).Append(',');
                }
                sb.Length--;
                sb.Append(".\n");
            }
            sb.Append("  Band changes ").Append(PlaytestLog.N(log.BandChanges)).Append(", layer changes heard ").Append(PlaytestLog.N(log.LayerChanges))
              .Append(", the fight music came in ").Append(Count(log.FightMusicTimes, "time", "times")).Append(", someone after you (no rest) ")
              .Append(Count(log.SomeoneAfterTimes, "time", "times")).Append(", thinned on the last plates ").Append(Count(log.ThinTimes, "time", "times"))
              .Append(", most layers at once ").Append(PlaytestLog.N(log.MostLayers)).Append(".\n");
            if (log.FightEntries.Count > 0) sb.Append("  Where each fight's music came in: ").Append(string.Join("; ", log.FightEntries)).Append(".\n");
            if (log.Pieces.Count > 0) sb.Append("  Pieces, in order: ").Append(string.Join(", ", log.Pieces)).Append(".\n");
            sb.Append("  To find when a layer came in after a squad, an alarm or a sighting, read the music lines among the events below.\n");
        }

        static void EveryWarning(StringBuilder sb, PlaytestLog log)
        {
            if (log.Warnings.Count == 0) return;
            Heading(sb, "EVERY WARNING (one row each; + times are game seconds after it began)");
            for (int i = 0; i < log.Warnings.Count; i++)
            {
                WarningRecord w = log.Warnings[i];
                sb.Append("  ").Append(PlaytestLog.Clock(w.BeganAt)).Append("  ").Append(w.Animal.Name).Append(": ").Append(EndPhrase(w.End));
                if (w.Marked) sb.Append("; mark +").Append(Plus(w.AimedGame - w.BeganGame));
                if (w.StruckAt >= 0f) sb.Append(", strike +").Append(Plus(w.StruckGame - w.BeganGame));
                if (w.OverAt >= 0f) sb.Append(", over +").Append(Plus(w.OverGame - w.BeganGame));
                sb.Append("; ").Append(PlaytestLog.Degrees(w.FacingAtBegin));
                if (w.Marked)
                {
                    sb.Append(" then ").Append(PlaytestLog.Degrees(w.FacingAtMark)).Append(" at its mark (").Append(PlaytestLog.FacingWords(w.FacingAtMark))
                      .Append(w.InFrameAtMark ? ", in view" : ", out of view").Append("), ").Append(PlaytestLog.Metres(w.RangeAtMark));
                    sb.Append("; ").Append(MoveWords(w));
                }
                if (w.Shots > 0) sb.Append("; ").Append(Count(w.Shots, "shot", "shots"));
                if (w.Damage > 0f) sb.Append("; hurt you ").Append(PlaytestLog.Amount(w.Damage)).Append(w.Beats > 1 ? " in " + PlaytestLog.N(w.Beats) + " bites" : "");
                sb.Append('\n');
            }
        }

        static string EndPhrase(WarningEnd end)
        {
            switch (end)
            {
                case WarningEnd.Hurt: return "hurt you";
                case WarningEnd.Missed: return "missed";
                case WarningEnd.Stopped: return "stopped after its mark";
                case WarningEnd.Killed: return "killed during it";
                case WarningEnd.CalledOff: return "called off";
                default: return "under way";
            }
        }

        static string MoveWords(WarningRecord w)
        {
            switch (w.Move)
            {
                case MoveOnMark.AlreadyMoving: return "already moving across at its mark";
                case MoveOnMark.OnTheMark: return "moved across " + PlaytestLog.Seconds(w.MovedGame - w.AimedGame) + " after its mark";
                case MoveOnMark.AfterTheStrike: return "moved across only after it struck";
                default: return "did not move across";
            }
        }

        static string Plus(float s) => (s > 0f ? s : 0f).ToString("0.00", CultureInfo.InvariantCulture) + " s";

        static void Events(StringBuilder sb, PlaytestLog log)
        {
            Heading(sb, "EVERY EVENT");
            sb.Append("  (a creature's first hit is listed, and every hit after it that took a piece off, found its weak place or\n")
              .Append("  killed it; the hits that only struck it are counted under HITS ON CREATURES)\n");
            for (int i = 0; i < log.Lines.Count; i++)
            {
                LogLine line = log.Lines[i];
                sb.Append("  ").Append(PlaytestLog.Clock(line.At)).Append("  ").Append(line.Text).Append('\n');
            }
            if (log.LinesDropped > 0) sb.Append("  (").Append(PlaytestLog.N(log.LinesDropped)).Append(" more events not kept: the log was full)\n");
        }

        // ------------------------------------------------------------------ helpers

        static void Heading(StringBuilder sb, string words) => sb.Append('\n').Append(words).Append('\n');

        static string Or(string s, string otherwise) => string.IsNullOrEmpty(s) ? otherwise : s;

        static string Count(int n, string one, string many) => PlaytestLog.N(n) + " " + (n == 1 ? one : many);

        static string Ms(float ms) => ms < 0f ? "? ms" : ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms";

        static string Col(string s, int width) => (s ?? "").PadRight(width);
        static string Num(string s, int width) => s.PadLeft(width);
        static string Num(int n, int width) => PlaytestLog.N(n).PadLeft(width);

        /// <summary>The kinds in the log, in the order of <see cref="PlaytestLog.Kinds"/>, then any others.</summary>
        static List<string> KindsIn(IReadOnlyList<WarningRecord> warnings)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < warnings.Count; i++) seen.Add(warnings[i].Animal.Kind);
            return Ordered(seen);
        }

        static List<string> KindsIn(IReadOnlyList<AnimalRecord> animals)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < animals.Count; i++) if (!animals[i].IsGroup) seen.Add(animals[i].Kind);
            return Ordered(seen);
        }

        static List<string> Ordered(HashSet<string> seen)
        {
            var kinds = new List<string>();
            for (int i = 0; i < PlaytestLog.Kinds.Length; i++)
            {
                if (seen.Remove(PlaytestLog.Kinds[i])) kinds.Add(PlaytestLog.Kinds[i]);
                string pack = PlaytestLog.PackOf(PlaytestLog.Kinds[i]);
                if (seen.Remove(pack)) kinds.Add(pack);
            }
            var rest = new List<string>(seen);
            rest.Sort(System.StringComparer.Ordinal);
            kinds.AddRange(rest);
            return kinds;
        }

        /// <summary>Most first, then by name, so the report reads the same each time it is written.</summary>
        static List<KeyValuePair<string, Tally>> Sorted(IReadOnlyDictionary<string, Tally> tallies)
        {
            var list = new List<KeyValuePair<string, Tally>>(tallies);
            list.Sort((x, y) =>
            {
                int byCount = y.Value.Count.CompareTo(x.Value.Count);
                return byCount != 0 ? byCount : string.CompareOrdinal(x.Key, y.Key);
            });
            return list;
        }
    }
}
