using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OrbitalAnte.Ante;

namespace OrbitalAnte.RunSim
{
    /// <summary>
    /// Simulated runs over the game's real ante, score, perk, shop, scratch-off, hazard and enemy-pressure rules
    /// (21 Nov 2026). The player is a made-up model (<see cref="Profile"/>): how often it kills, how often it is hit,
    /// how often it lands somewhere new. Those numbers are guesses, not measurements, so the answers are about the
    /// shape of the game (which perk is far ahead of the rest, whether targets or deaths end runs, how long the
    /// player waits between rewards), never about how it feels. Nothing here replaces a playtest on the Deck.
    /// Run: dotnet run -c Release --project tools/run-sim [runs]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int runs = args.Length > 0 && int.TryParse(args[0], out int r) ? r : 2000;
            var report = new StringBuilder();
            report.AppendLine("# Orbital Ante run simulator");
            report.AppendLine();
            report.AppendLine($"{runs} simulated runs per row. The player model is a guess (see Profile in tools/run-sim/Sim.cs); these are not playtest results.");
            report.AppendLine();
            float danger = Sim.Calibrate(Math.Min(runs, 800), new Policy { Overtime = true });
            report.AppendLine($"Danger scale {danger:0.000}: set so half of first-time players (overtime rule, staying in a family) reach ante 4, as Part B section 5 asks. Every other number is read against that.");
            report.AppendLine();

            Kits(report, runs);
            if (args.Length > 1 && args[1] == "kits")
            {
                Console.WriteLine(report.ToString());
                return 0;
            }

            foreach (bool overtime in new[] { false, true })
            {
                report.AppendLine(overtime ? "## Missed target goes to overtime (Matthew, 21 Nov 2026)" : "## Missed target ends the run (rule on PR 75)");
                report.AppendLine();
                report.AppendLine("| Player | Median ante reached | Reach ante 4 | Clear all 9 | Runs ended by the clock | Ended by death | Near misses (within 15% of target) | Avg wait between point pop-ups | Longest wait (p90) | Sets completed |");
                report.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                foreach (Profile p in Profile.All)
                {
                    var s = Sim.Many(p, runs, new Policy { Overtime = overtime });
                    report.AppendLine($"| {p.Name} | {s.MedianAnte} | {s.Pct(s.Reach4)} | {s.Pct(s.ClearAll)} | {s.Pct(s.EndedByClock)} | {s.Pct(s.EndedByDeath)} | {s.Pct(s.NearMiss)} | {s.MeanGap:0.0} s | {s.P90Gap:0} s | {s.Pct(s.SetsDone)} |");
                }
                report.AppendLine();
            }

            report.AppendLine("## What each perk is worth (Average player, overtime rule, perk forced at the first break)");
            report.AppendLine();
            report.AppendLine("| Perk | Family | Mean antes cleared | vs no perk | Mean run score | vs no perk |");
            report.AppendLine("|---|---|---|---|---|---|");
            var basePolicy = new Policy { Overtime = true, Draft = DraftPolicy.Skip };
            var baseline = Sim.Many(Profile.Average, runs, basePolicy);
            foreach (PerkDefinition perk in RoguelitePerks.Create())
            {
                var s = Sim.Many(Profile.Average, runs, new Policy { Overtime = true, Draft = DraftPolicy.Skip, Forced = perk.id });
                report.AppendLine($"| {perk.displayName} | {perk.family} | {s.MeanCleared:0.00} | {s.MeanCleared - baseline.MeanCleared:+0.00;-0.00} | {s.MeanScore:0} | {(s.MeanScore / baseline.MeanScore - 1f) * 100f:+0;-0}% |");
            }
            report.AppendLine();
            report.AppendLine($"No perk at all: {baseline.MeanCleared:0.00} antes cleared, {baseline.MeanScore:0} points.");
            report.AppendLine();

            report.AppendLine("## Draft habits (Average player, overtime rule)");
            report.AppendLine();
            report.AppendLine("| Habit | Mean antes cleared | Mean run score | Sets completed |");
            report.AppendLine("|---|---|---|---|");
            foreach (DraftPolicy d in new[] { DraftPolicy.Skip, DraftPolicy.Random, DraftPolicy.StayInFamily })
            {
                var s = Sim.Many(Profile.Average, runs, new Policy { Overtime = true, Draft = d });
                report.AppendLine($"| {d} | {s.MeanCleared:0.00} | {s.MeanScore:0} | {s.Pct(s.SetsDone)} |");
            }
            report.AppendLine();

            report.AppendLine("## The upgrade screen (21 Nov 2026): pick at the break, in the field, and with rerolls (overtime rule, stays in a family)");
            report.AppendLine();
            report.AppendLine("| Player | Habit | Mean antes cleared | Mean run score | Sets completed | Field picks a run | Rerolls a run |");
            report.AppendLine("|---|---|---|---|---|---|---|");
            foreach (Profile p in Profile.All)
            {
                foreach (var (name, pol) in new[]
                {
                    ("At the break", new Policy { Overtime = true }),
                    ("In the field", new Policy { Overtime = true, FieldPick = true }),
                    ("Field + rerolls", new Policy { Overtime = true, FieldPick = true, Reroll = true }),
                })
                {
                    var s = Sim.Many(p, runs, pol);
                    report.AppendLine($"| {p.Name} | {name} | {s.MeanCleared:0.00} | {s.MeanScore:0} | {s.Pct(s.SetsDone)} | {s.FieldPicks / (float)Math.Max(1, s.Runs):0.0} | {s.Rerolls / (float)Math.Max(1, s.Runs):0.00} |");
                }
            }
            report.AppendLine();

            report.AppendLine("## Raising the stakes every ante (Average player, overtime rule)");
            report.AppendLine();
            report.AppendLine("| Hazard | Mean antes cleared | Mean run score | Died |");
            report.AppendLine("|---|---|---|---|");
            foreach (Hazard h in new[] { Hazard.None, Hazard.Hunted, Hazard.ThinAir, Hazard.ShortFuse })
            {
                var s = Sim.Many(Profile.Average, runs, new Policy { Overtime = true, Draft = DraftPolicy.StayInFamily, AlwaysHazard = h });
                report.AppendLine($"| {h} | {s.MeanCleared:0.00} | {s.MeanScore:0} | {s.Pct(s.EndedByDeath)} |");
            }
            report.AppendLine();

            report.AppendLine("## Ante by ante (Average player, overtime rule, stays in a family)");
            report.AppendLine();
            report.AppendLine("| Ante | Target | Reached it | Cleared on the clock | Needed overtime | Mean overtime | Died in it | Wallet at the break |");
            report.AppendLine("|---|---|---|---|---|---|---|---|");
            var perAnte = Sim.Many(Profile.Average, runs, new Policy { Overtime = true, Draft = DraftPolicy.StayInFamily });
            for (int a = 1; a <= 9; a++)
            {
                var row = perAnte.Antes[a];
                if (row.Entered == 0) continue;
                report.AppendLine($"| {a} | {new AnteBalance().TargetFor(a):N0} | {row.Entered} | {Sim.P(row.OnClock, row.Entered)} | {Sim.P(row.Overtime, row.Entered)} | {(row.Overtime > 0 ? row.OvertimeSeconds / row.Overtime : 0):0} s | {Sim.P(row.Died, row.Entered)} | {(row.Cleared > 0 ? row.Wallet / row.Cleared : 0):N0} |");
            }

            Console.WriteLine(report.ToString());
            return 0;
        }

        /// <summary>
        /// Each starting kit against Standard (design review, 27 Nov 2026: a kit must be a sidegrade, ahead early and
        /// behind late, never ahead all run). Acceptance: no kit beats Standard on reaching ante 4 by more than a few
        /// points, or on the Skilled player's mean antes cleared. The Ghost kit's perks (Eagle Eye, Cold Blood) are a
        /// blind spot of the model, so its row reads only the price. The model's Standard player takes its first perk
        /// at random and values seven perks at nothing, while a kit's two perks are chosen ones, so the model leans
        /// toward kits early. Run only this table with a second argument: <c>1000 kits</c>.
        /// </summary>
        static void Kits(StringBuilder report, int runs)
        {
            report.AppendLine("## Starting kits against Standard (overtime rule, stays in a family)");
            report.AppendLine();
            report.AppendLine("| Player | Start | Reach ante 4 | Mean antes cleared | Mean run score | Sets completed |");
            report.AppendLine("|---|---|---|---|---|---|");
            foreach (Profile p in Profile.All)
            {
                var standard = Sim.Many(p, runs, new Policy { Overtime = true });
                report.AppendLine($"| {p.Name} | Standard | {standard.Pct(standard.Reach4)} | {standard.MeanCleared:0.00} | {standard.MeanScore:0} | {standard.Pct(standard.SetsDone)} |");
                foreach (StartingKit kit in StartingKits.All)
                {
                    if (kit.perkIds.Length == 0) continue;
                    var s = Sim.Many(p, runs, new Policy { Overtime = true, Kit = kit.id });
                    string blind = kit.id == "kit.ghost" ? " (perks not valued)" : string.Empty;
                    report.AppendLine($"| {p.Name} | {kit.displayName}{blind} | {s.Pct(s.Reach4)} | {s.MeanCleared:0.00} ({s.MeanCleared - standard.MeanCleared:+0.00;-0.00}) | {s.MeanScore:0} ({(s.MeanScore / Math.Max(1f, standard.MeanScore) - 1f) * 100f:+0;-0}%) | {s.Pct(s.SetsDone)} |");
                }
            }
            report.AppendLine();
        }
    }
}
