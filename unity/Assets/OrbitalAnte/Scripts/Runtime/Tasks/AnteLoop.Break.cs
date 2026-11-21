using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Ante;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// The ante break (20 Nov 2026, roguelite fun pass; research doc "Roguelite fun: research and plan"). When an
    /// ante is banked the card between antes becomes a short break, all on the D-pad and A:
    ///  1. pick one perk of three (or skip for points to spend), with each card's family and how close its set is;
    ///  2. a shop for the four attributes;
    ///  3. the free scratch-off, odds printed;
    ///  4. "raise the stakes": an optional hazard for the next ante, for the formula's chosen-risk bonus;
    ///  5. start the next ante.
    /// Also here: the build published to the game (<see cref="RunEffects"/>, enemy pressure), the time perks, and
    /// the run recap with lifetime records.
    /// </summary>
    public sealed partial class AnteLoop
    {
        /// <summary>The hazard the running ante is played under.</summary>
        public Hazard CurrentHazard { get; private set; }

        Hazard _nextHazard;
        string _breakNote;
        bool _tacticalNextAnte, _tacticalThisAnte, _voucherNextAnte;
        bool _recorded;
        int _landingRefundFrame = -1;

        // ------------------------------------------------------------------ the build, published

        /// <summary>Folds perks, attributes, hazard and boosts into the numbers the game reads.</summary>
        void PublishBuild()
        {
            if (Run == null) { RunEffects.Clear(); return; }
            RunEffects.Publish(RunMods.From(Run.Perks, Run.Attributes, CurrentHazard, Run.Balance, _tacticalThisAnte));
            // In overtime the enemies hunt as under Hunted, whatever was chosen (the points multiplier stays the chosen one's).
            Hazard pressure = Run.Director.InOvertime ? Hazard.Hunted : CurrentHazard;
            Enemies.EnemySpawner.Pressure = EnemyPressure.For(Run.Director.Ante, pressure, Run.Balance);
            Enemies.EnemySpawner.PressureAnte = Run.Director.Ante;
            Enemies.EnemySpawner.PressureSeed = Run.Seed;
        }

        void ResetBreak()
        {
            CurrentHazard = Hazard.None;
            _nextHazard = Hazard.None;
            _breakNote = null;
            _tacticalNextAnte = _tacticalThisAnte = _voucherNextAnte = false;
            _recorded = false;
        }

        /// <summary>Called by <see cref="Report"/>: the chosen risk, and time back from perks.</summary>
        void BeforeReport(ref ScoreEvent e)
        {
            if (CurrentHazard == Hazard.None) return;
            e.acceptedRisk = true;
            e.riskMultiplier = HazardRules.Multiplier(CurrentHazard, Run.Balance);
        }

        void AfterReport(ScoreEvent e, ScoreResult result, string label)
        {
            if (!result.Awarded || Run == null) return;
            RunMods mods = RunEffects.Mods;
            float back = 0f;
            // One touchdown can score twice (a first landing that is also a hard one): time back once per touchdown
            // (Codex review, PR 75), so a perk gives exactly what its card says.
            if (mods.LandingTimeBack > 0f && (e.kind == ScoreEventKind.NewPlanet || e.kind == ScoreEventKind.NewMoon || e.kind == ScoreEventKind.DifficultLanding)
                && Time.frameCount != _landingRefundFrame)
            {
                _landingRefundFrame = Time.frameCount;
                back += mods.LandingTimeBack;
            }
            if (back > 0f) GiveTimeBack(back);
        }

        void GiveTimeBack(float seconds)
        {
            Run.Director.AdjustSeconds(seconds);
            Pop("+" + Mathf.RoundToInt(seconds) + " s ON THE CLOCK");
        }

        /// <summary>The task director saw a LONG SHOT kill that scored (the Ghost set puts time back for it).</summary>
        public static void LongShotKill()
        {
            AnteLoop loop = Current;
            if (loop == null || loop.Run == null || !loop.Run.Director.IsRunning) return;
            float back = RunEffects.Mods.TimeOnLongShot;
            if (back > 0f) loop.GiveTimeBack(back);
        }

        /// <summary>A new ante has begun: the chosen hazard and the scratch-off's prizes take effect.</summary>
        void BeginAnteEffects()
        {
            CurrentHazard = _nextHazard;
            _nextHazard = Hazard.None;
            _tacticalThisAnte = _tacticalNextAnte;
            _tacticalNextAnte = false;
            if (CurrentHazard == Hazard.ShortFuse)
                Run.Director.AdjustSeconds(HazardRules.AnteSeconds(CurrentHazard, Run.Balance) - Run.Director.SecondsLeft);
            if (_voucherNextAnte)
            {
                _voucherNextAnte = false;
                if (RunEffects.Player != null) RunEffects.Player.Heal(RunEffects.Player.Max);
                DeepBreathFx breath = DeepBreathFx.Instance;
                if (breath != null && breath.Breath != null) breath.Breath.Earn(1f);
            }
            _breakNote = null;
            PublishBuild();
            if (CurrentHazard != Hazard.None)
                Note("ANTE " + Run.Director.Ante + ": " + HazardRules.Name(CurrentHazard).ToUpperInvariant() + ", POINTS x" + HazardRules.Multiplier(CurrentHazard, Run.Balance).ToString("0.00", Inv));
        }

        // ------------------------------------------------------------------ the break's pages

        static string N(int v) => v.ToString("N0", Inv);

        void ShowBreak()
        {
            if (Menu == null || Run == null) return;
            AnteBalance b = Run.Balance;
            int ante = Run.Director.Ante;
            int next = b.EndlessTargetFor(ante + 1);   // the same as TargetFor for the listed antes; past them for Keep going

            var body = new StringBuilder();
            body.Append("Banked ").Append(N(Run.Ledger.AnteEarned)).Append(" of ").Append(N(Run.Director.Target))
                .Append(".   Run score ").Append(N(Run.Ledger.LifetimeRunScore)).Append(".\n");
            body.Append("To spend: ").Append(N(Run.Ledger.SpendablePoints)).Append("  (spending never lowers your score)\n");
            string sets = SetsLine();
            if (sets.Length > 0) body.Append(sets).Append("\n");
            if (!string.IsNullOrEmpty(_breakNote)) body.Append(_breakNote).Append("\n");
            if (Run.Director.ClearedInOvertime) body.Append("Made it in overtime. Close one!\n");
            body.Append("Ante ").Append(ante + 1).Append(" needs ").Append(N(next)).Append(" in ")
                .Append(Clock(HazardRules.AnteSeconds(_nextHazard, b))).Append(". What you cleared stays cleared.\n");
            body.Append(PressureLine(ante + 1));

            var choices = new List<PauseMenu.MenuChoice>();
            if (Run.Director.PerkDraftPending)
            {
                int offers = Run.PerkOffers().Count;
                choices.Add(new PauseMenu.MenuChoice("Pick a perk  (" + offers + " on offer)", ShowPerks, offers > 0));
            }
            else choices.Add(new PauseMenu.MenuChoice("Perk: " + LastPerkName(), null, false));
            choices.Add(new PauseMenu.MenuChoice("Shop  (" + N(Run.Ledger.SpendablePoints) + " to spend)", ShowShop));
            choices.Add(Run.Director.ScratchPending
                ? new PauseMenu.MenuChoice("Scratch the free ticket\n" + ScratchOddsLine(b), Scratch)
                : new PauseMenu.MenuChoice("Ticket scratched", null, false));
            choices.Add(new PauseMenu.MenuChoice("Raise the stakes: " + HazardRules.Name(_nextHazard)
                + (_nextHazard == Hazard.None ? "" : "  (points x" + HazardRules.Multiplier(_nextHazard, b).ToString("0.00", Inv) + ")"), ShowStakes));
            choices.Add(new PauseMenu.MenuChoice("Start ante " + (ante + 1), StartPressed));
            choices.Add(new PauseMenu.MenuChoice("Restart or quit", ShowLeave));
            Menu.ShowChoices("ANTE " + ante + " CLEARED", body.ToString(), choices, null, 190f, _keepSlot);
            _keepSlot = -1;
        }

        /// <summary>"Ante 4: enemies 14% sharper, 1 in 12 plain enemies come as heavies" (the tougher antes, said out loud).</summary>
        string PressureLine(int ante)
        {
            EnemyPressure p = EnemyPressure.For(ante, _nextHazard, Run.Balance);
            int sharper = Mathf.RoundToInt((1f - p.Sharpness) * 100f);
            string line = "Ante " + ante + ": enemies " + (sharper > 0 ? sharper + "% sharper" : "as in ante 1");
            if (p.HeavyShare > 0f) line += ", " + Mathf.RoundToInt(p.HeavyShare * 100f) + "% of plain enemies come as heavies";
            return line + ".";
        }

        /// <summary>Starting with a pick or the ticket unused gives them up, so it asks first.</summary>
        void StartPressed()
        {
            bool perk = Run.Director.PerkDraftPending && Run.PerkOffers().Count > 0, ticket = Run.Director.ScratchPending;
            if (!perk && !ticket) { Menu.Close(); StartNextAnte(); return; }
            string left = perk && ticket ? "a perk pick and the free ticket" : perk ? "a perk pick" : "the free ticket";
            var choices = new List<PauseMenu.MenuChoice>
            {
                new PauseMenu.MenuChoice("Back to the break", ShowBreak),
                new PauseMenu.MenuChoice("Start anyway, giving them up", () => { Menu.Close(); StartNextAnte(); }),
            };
            Menu.ShowChoices("START ANTE " + (Run.Director.Ante + 1) + "?", "You still have " + left + ". Starting gives " + (perk && ticket ? "them" : "it") + " up.",
                choices, ShowBreak, 60f);
        }

        int _keepSlot = -1;

        void ShowLeave()
        {
            var choices = new List<PauseMenu.MenuChoice>
            {
                new PauseMenu.MenuChoice("Back to the break", ShowBreak),
                new PauseMenu.MenuChoice("Restart with the same seed (" + GameSession.Seed + ")", () => Menu.RequestRestart(true)),
                new PauseMenu.MenuChoice("Restart with a new seed", () => Menu.RequestRestart(false)),
                new PauseMenu.MenuChoice("Quit to the main menu", () => Menu.RequestQuitToMenu()),
            };
            Menu.ShowChoices("RESTART OR QUIT?", "The current run ends. Its records are kept.", choices, ShowBreak, 40f);
        }

        string LastPerkName()
        {
            IReadOnlyList<PerkDefinition> owned = Run.Perks.Owned;
            return _skippedPerk ? "skipped" : owned.Count > 0 ? owned[owned.Count - 1].displayName : "none";
        }

        bool _skippedPerk;

        /// <summary>"GUNNER 2/3 · BRAWLER SET ON": the families the player has started.</summary>
        string SetsLine()
        {
            var parts = new List<string>();
            for (int i = 0; i < PerkFamilies.All.Length; i++)
            {
                PerkFamily f = PerkFamilies.All[i];
                if (Run.Perks.FamilyCount(f) > 0) parts.Add(PerkFamilies.Progress(Run.Perks, f, Run.Balance));
            }
            return parts.Count == 0 ? string.Empty : "Your build: " + string.Join("   ", parts);
        }

        void ShowPerks()
        {
            List<PerkDefinition> offers = Run.PerkOffers();
            AnteBalance b = Run.Balance;
            var choices = new List<PauseMenu.MenuChoice>();
            for (int i = 0; i < offers.Count; i++)
            {
                PerkDefinition perk = offers[i];
                string family = perk.family == PerkFamily.None ? "WILD CARD" : FamilyAfter(perk.family);
                string label = perk.displayName.ToUpperInvariant() + "   " + family + "   " + perk.rarity.ToString().ToUpperInvariant()
                               + "\n" + perk.Describe(b);
                choices.Add(new PauseMenu.MenuChoice(label, () => TakePerk(perk)));
            }
            choices.Add(new PauseMenu.MenuChoice("Skip all three: +" + N(b.perkSkipCash) + " to spend", SkipPerks));
            choices.Add(new PauseMenu.MenuChoice("Back", ShowBreak));
            string body = "Take one. Three of one family turns on its set bonus.\n" + SetBonusesLine(offers);
            Menu.ShowChoices("PICK A PERK", body, choices, ShowBreak, 110f, -1, true);
        }

        /// <summary>"GUNNER 2/3": the family count once this card is taken.</summary>
        string FamilyAfter(PerkFamily family)
        {
            int have = Run.Perks.FamilyCount(family) + 1, need = PerkFamilies.SetSize(Run.Balance);
            string name = family.ToString().ToUpperInvariant();
            return have >= need ? name + " " + Mathf.Min(have, need) + "/" + need + " SET!" : name + " " + have + "/" + need;
        }

        /// <summary>What each offered family's set gives, so the long-term pull is visible next to the short-term one.</summary>
        string SetBonusesLine(List<PerkDefinition> offers)
        {
            var seen = new HashSet<PerkFamily>();
            var parts = new List<string>();
            for (int i = 0; i < offers.Count; i++)
            {
                PerkFamily f = offers[i].family;
                if (f == PerkFamily.None || !seen.Add(f)) continue;
                parts.Add(f + " set: " + PerkFamilies.Describe(f, Run.Balance));
            }
            return string.Join("\n", parts);
        }

        void TakePerk(PerkDefinition perk)
        {
            if (!Run.TakePerk(perk)) { ShowBreak(); return; }
            _skippedPerk = false;
            Audio.AudioDirector.PlayLocal("reward.tier2");
            _breakNote = perk.displayName + ": " + perk.Describe(Run.Balance);
            if (perk.family != PerkFamily.None && Run.Perks.FamilyCount(perk.family) == PerkFamilies.SetSize(Run.Balance))
            {
                Audio.AudioDirector.PlayLocal("reward.tier4");
                _breakNote = perk.family.ToString().ToUpperInvariant() + " SET ON: " + PerkFamilies.Describe(perk.family, Run.Balance);
            }
            PublishBuild();
            _keepSlot = 0;
            ShowBreak();
        }

        void SkipPerks()
        {
            if (!Run.Director.PerkDraftPending) { ShowBreak(); return; }
            Run.Director.MarkPerkDraftTaken();
            Run.Ledger.AddBankOnly(Run.Balance.perkSkipCash);
            _skippedPerk = true;
            Audio.AudioDirector.PlayLocal("reward.points.rack_end");
            _breakNote = "Skipped the perks: +" + N(Run.Balance.perkSkipCash) + " to spend.";
            ShowBreak();
        }

        static readonly PlayerAttribute[] Attributes = { PlayerAttribute.Damage, PlayerAttribute.Movement, PlayerAttribute.Oxygen, PlayerAttribute.Defense };

        void ShowShop() => ShowShop(-1);

        void ShowShop(int keepSlot)
        {
            AnteBalance b = Run.Balance;
            var choices = new List<PauseMenu.MenuChoice>();
            for (int i = 0; i < Attributes.Length; i++)
            {
                PlayerAttribute a = Attributes[i];
                int level = Run.Attributes.Level(a), price = Run.Attributes.NextPrice(a);
                string what = AttributeWords(a, b);
                string label = price < 0
                    ? AttributeName(a) + "   level " + level + " (top)\n" + what
                    : AttributeName(a) + "   level " + level + " > " + (level + 1) + "   " + N(price) + "\n" + what;
                bool can = price >= 0 && Run.Ledger.SpendablePoints >= price;
                int slot = i;
                choices.Add(new PauseMenu.MenuChoice(label, () => Buy(a, slot), can));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", () => { _keepSlot = 1; ShowBreak(); }));
            Menu.ShowChoices("SHOP", "To spend: " + N(Run.Ledger.SpendablePoints) + ". These last for the whole run.", choices, ShowBreak, 40f, keepSlot);
        }

        static string AttributeName(PlayerAttribute a)
        {
            switch (a)
            {
                case PlayerAttribute.Damage: return "DAMAGE";
                case PlayerAttribute.Movement: return "SPEED";
                case PlayerAttribute.Oxygen: return "OXYGEN";
                default: return "DEFENCE";
            }
        }

        static string AttributeWords(PlayerAttribute a, AnteBalance b)
        {
            switch (a)
            {
                case PlayerAttribute.Damage: return "+" + Pct(b.damagePerLevel) + " damage a level";
                case PlayerAttribute.Movement: return "+" + Pct(b.movementPerLevel) + " walking speed a level";
                case PlayerAttribute.Oxygen: return "+" + Pct(b.oxygenPerLevel) + " suit air a level";
                default: return Pct(b.defensePerLevel) + " less damage taken a level";
            }
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        void Buy(PlayerAttribute a, int slot)
        {
            if (Run.TryBuyAttribute(a))
            {
                Audio.AudioDirector.PlayLocal("reward.tier1");
                PublishBuild();
            }
            ShowShop(slot);
        }

        void Scratch()
        {
            ScratchOutcome? got = Run.UseScratchOff();
            if (got == null) { ShowBreak(); return; }
            AnteBalance b = Run.Balance;
            switch (got.Value)
            {
                case ScratchOutcome.SmallCash:
                case ScratchOutcome.BigCash:
                    _breakNote = "Ticket: +" + N(ScratchOff.CashFor(b, got.Value)) + " to spend.";
                    Audio.AudioDirector.PlayLocal("reward.points.rack_end");
                    break;
                case ScratchOutcome.RepairRefillVoucher:
                    _voucherNextAnte = true;
                    _breakNote = "Ticket: refill voucher. The next ante starts with full health and Focus.";
                    Audio.AudioDirector.PlayLocal("reward.tier2");
                    break;
                case ScratchOutcome.TemporaryTacticalPerk:
                    _tacticalNextAnte = true;
                    _breakNote = "Ticket: tactical boost. +10% damage for the next ante.";
                    Audio.AudioDirector.PlayLocal("reward.tier3");
                    break;
                default:
                    _breakNote = "Ticket: a cosmetic token (kept for the locker when it exists).";
                    Audio.AudioDirector.PlayLocal("reward.tier4");
                    break;
            }
            _keepSlot = 2;
            ShowBreak();
        }

        static string ScratchOddsLine(AnteBalance b)
        {
            return Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.SmallCash)) + "% +" + b.scratchSmallCash
                   + ", " + Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.BigCash)) + "% +" + b.scratchBigCash
                   + ", " + Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.RepairRefillVoucher)) + "% refill"
                   + ", " + Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.TemporaryTacticalPerk)) + "% boost"
                   + ", " + ScratchOff.OddsPercent(b, ScratchOutcome.CosmeticToken).ToString("0.#", Inv) + "% token";
        }

        void ShowStakes()
        {
            AnteBalance b = Run.Balance;
            var choices = new List<PauseMenu.MenuChoice>
            {
                new PauseMenu.MenuChoice((_nextHazard == Hazard.None ? "CHOSEN:  " : "") + "NO HAZARD\nplay the ante as it comes, points x1.00", () => PickHazard(Hazard.None)),
            };
            for (int i = 0; i < HazardRules.Offered.Length; i++)
            {
                Hazard h = HazardRules.Offered[i];
                string mark = _nextHazard == h ? "CHOSEN:  " : "";
                choices.Add(new PauseMenu.MenuChoice(mark + HazardRules.Name(h).ToUpperInvariant() + "   points x" + HazardRules.Multiplier(h, b).ToString("0.00", Inv)
                    + "\n" + HazardRules.Describe(h, b), () => PickHazard(h)));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", ShowBreak));
            Menu.ShowChoices("RAISE THE STAKES", "One hazard for the next ante, chosen now. Every point that ante pays more.\n"
                + PressureLine(Run.Director.Ante + 1), choices, ShowBreak, 60f);
        }

        void PickHazard(Hazard h)
        {
            _nextHazard = h;
            _keepSlot = 3;
            Audio.AudioDirector.PlayLocal(h == Hazard.None ? "ui.select" : "reward.multiplier");
            ShowBreak();
        }

        // ------------------------------------------------------------------ the run recap and records

        /// <summary>
        /// The recap under the run-over page: how far, how close, the build and records. Records are saved once a
        /// run. Never invents anything: every number is from the run's own ledger.
        /// </summary>
        public string RecapAndRecord()
        {
            if (Run == null) return null;
            int ante = Run.Director.Ante, score = Run.Ledger.LifetimeRunScore;
            int target = Run.Director.Target, banked = Run.Ledger.AnteEarned;
            int cleared = Run.Director.Phase == AntePhase.Victory ? ante : ante - 1;

            Save.SaveData save = Save.SaveSystem.Data;
            bool newAnte = false, newScore = false;
            if (!_recorded && save != null)
            {
                _recorded = true;
                newAnte = ante > save.bestAnte && save.runsEnded > 0;
                newScore = score > save.bestRunScore && save.runsEnded > 0;
                save.bestAnte = Mathf.Max(save.bestAnte, ante);
                save.bestRunScore = Mathf.Max(save.bestRunScore, score);
                save.antesCleared += Mathf.Max(0, cleared);
                save.runsEnded++;
                Save.SaveSystem.Save();
            }

            var s = new StringBuilder();
            s.Append("Reached ante ").Append(ante);
            if (Run.Director.Phase != AntePhase.Victory && banked < target)
                s.Append(", ").Append(N(target - banked)).Append(" short of ").Append(N(target));
            s.Append(".   Run score ").Append(N(score)).Append(".\n");
            if (save != null)
                s.Append("Best ante ").Append(save.bestAnte).Append("   Best score ").Append(N(save.bestRunScore))
                 .Append("   Runs ").Append(save.runsEnded).Append("\n");
            if (Run.Perks.Owned.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < Run.Perks.Owned.Count; i++) names.Add(Run.Perks.Owned[i].displayName);
                s.Append("Build: ").Append(string.Join(", ", names)).Append("\n");
            }
            if (newAnte) s.Append("NEW RECORD: your highest ante!\n");
            else if (newScore) s.Append("NEW RECORD: your best run score!\n");
            return s.ToString().TrimEnd('\n');
        }
    }
}
