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

        // ------------------------------------------------------------------ the build, published

        /// <summary>Folds perks, attributes, hazard and boosts into the numbers the game reads.</summary>
        void PublishBuild()
        {
            if (Run == null) { RunEffects.Clear(); return; }
            RunEffects.Publish(RunMods.From(Run.Perks, Run.Attributes, CurrentHazard, Run.Balance, _tacticalThisAnte));
            Enemies.EnemySpawner.Pressure = EnemyPressure.For(Run.Director.Ante, CurrentHazard, Run.Balance);
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
            if (CurrentHazard != Hazard.None) e.acceptedRisk = true;
        }

        void AfterReport(ScoreEvent e, ScoreResult result, string label)
        {
            if (!result.Awarded || Run == null) return;
            RunMods mods = RunEffects.Mods;
            float back = 0f;
            if (mods.LandingTimeBack > 0f && (e.kind == ScoreEventKind.NewPlanet || e.kind == ScoreEventKind.NewMoon || e.kind == ScoreEventKind.DifficultLanding))
                back += mods.LandingTimeBack;
            if (mods.TimeOnLongShot > 0f && label != null && label.Contains("LONG SHOT")) back += mods.TimeOnLongShot;
            if (back > 0f)
            {
                Run.Director.AdjustSeconds(back);
                Pop("+" + Mathf.RoundToInt(back) + " s ON THE CLOCK");
            }
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
                Note("ANTE " + Run.Director.Ante + ": " + HazardRules.Name(CurrentHazard).ToUpperInvariant() + ", POINTS x" + Run.Balance.riskAccepted.ToString("0.00", Inv));
        }

        // ------------------------------------------------------------------ the break's pages

        static string N(int v) => v.ToString("N0", Inv);

        void ShowBreak()
        {
            if (Menu == null || Run == null) return;
            AnteBalance b = Run.Balance;
            int ante = Run.Director.Ante;
            int next = b.TargetFor(ante + 1);

            var body = new StringBuilder();
            body.Append("Banked ").Append(N(Run.Ledger.AnteEarned)).Append(" of ").Append(N(Run.Director.Target))
                .Append(".   Run score ").Append(N(Run.Ledger.LifetimeRunScore)).Append(".\n");
            body.Append("To spend: ").Append(N(Run.Ledger.SpendablePoints)).Append("\n");
            string sets = SetsLine();
            if (sets.Length > 0) body.Append(sets).Append("\n");
            if (!string.IsNullOrEmpty(_breakNote)) body.Append(_breakNote).Append("\n");
            body.Append("Ante ").Append(ante + 1).Append(" needs ").Append(N(next)).Append(" in ")
                .Append(Clock(HazardRules.AnteSeconds(_nextHazard, b))).Append(". What you cleared stays cleared.");

            var choices = new List<PauseMenu.MenuChoice>();
            if (Run.Director.PerkDraftPending)
            {
                int offers = Run.PerkOffers().Count;
                choices.Add(new PauseMenu.MenuChoice("Pick a perk  (" + offers + " on offer)", ShowPerks, offers > 0));
            }
            else choices.Add(new PauseMenu.MenuChoice("Perk: " + LastPerkName(), null, false));
            choices.Add(new PauseMenu.MenuChoice("Shop  (" + N(Run.Ledger.SpendablePoints) + " to spend)", ShowShop));
            choices.Add(Run.Director.ScratchPending
                ? new PauseMenu.MenuChoice("Scratch the free ticket", Scratch)
                : new PauseMenu.MenuChoice("Ticket scratched", null, false));
            choices.Add(new PauseMenu.MenuChoice("Raise the stakes: " + HazardRules.Name(_nextHazard)
                + (_nextHazard == Hazard.None ? "" : "  (points x" + b.riskAccepted.ToString("0.00", Inv) + ")"), ShowStakes));
            choices.Add(new PauseMenu.MenuChoice("Start ante " + (ante + 1), () => { Menu.Close(); StartNextAnte(); }));
            choices.Add(new PauseMenu.MenuChoice("Restart or quit", () => Menu.ShowQuitPage()));
            Menu.ShowChoices("ANTE " + ante + " CLEARED", body.ToString(), choices, null, 150f);
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
            Menu.ShowChoices("PICK A PERK", body, choices, ShowBreak, 110f);
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

        void ShowShop()
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
                choices.Add(new PauseMenu.MenuChoice(label, () => Buy(a), can));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", ShowBreak));
            Menu.ShowChoices("SHOP", "To spend: " + N(Run.Ledger.SpendablePoints) + ". These last for the whole run.", choices, ShowBreak, 40f);
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

        void Buy(PlayerAttribute a)
        {
            if (Run.TryBuyAttribute(a))
            {
                Audio.AudioDirector.PlayLocal("reward.tier1");
                PublishBuild();
            }
            ShowShop();
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
            _breakNote += "  Odds: " + ScratchOddsLine(b);
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
                new PauseMenu.MenuChoice((_nextHazard == Hazard.None ? "> " : "") + "NO HAZARD\nplay the ante as it comes, points x1.00", () => PickHazard(Hazard.None)),
            };
            for (int i = 0; i < HazardRules.Offered.Length; i++)
            {
                Hazard h = HazardRules.Offered[i];
                string mark = _nextHazard == h ? "> " : "";
                choices.Add(new PauseMenu.MenuChoice(mark + HazardRules.Name(h).ToUpperInvariant() + "   points x" + b.riskAccepted.ToString("0.00", Inv)
                    + "\n" + HazardRules.Describe(h, b), () => PickHazard(h)));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", ShowBreak));
            Menu.ShowChoices("RAISE THE STAKES", "One hazard for the next ante, chosen now. Every point that ante pays x"
                + b.riskAccepted.ToString("0.00", Inv) + ".", choices, ShowBreak, 50f);
        }

        void PickHazard(Hazard h)
        {
            _nextHazard = h;
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
