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
            RunEffects.Publish(RunMods.From(Run.Perks, Run.Attributes, CurrentHazard, Run.Balance, _tacticalThisAnte, LockerUpgrade));
            // In overtime the enemies are as sharp as under Hunted. Not its extra heavies: an enemy keeps the kind it
            // first woke as for the whole run, so heavies woken in overtime would stay heavies in every later ante.
            EnemyPressure pressure = EnemyPressure.For(Run.Director.Ante, CurrentHazard, Run.Balance);
            if (Run.Director.InOvertime && CurrentHazard != Hazard.Hunted)
                pressure.Sharpness = Mathf.Max(0.4f, pressure.Sharpness * Run.Balance.huntedSharpness);
            Enemies.EnemySpawner.Pressure = pressure;
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
            ResetContracts();
        }

        /// <summary>Called by <see cref="Report"/>: the chosen risk, and time back from perks.</summary>
        void BeforeReport(ref ScoreEvent e)
        {
            // The stakes pay only while the clock runs: in overtime the bet is lost (HazardRules.PaysNow).
            if (!HazardRules.PaysNow(CurrentHazard, Run.Director.InOvertime)) return;
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
            // No clock in overtime (or once it has run out): say nothing rather than promise time that never comes.
            if (Run.Director.WaitingForPlayer) return;
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
            BeginAnteContract();
            PublishBuild();
            if (CurrentHazard != Hazard.None)
                Note("ANTE " + Run.Director.Ante + ": " + HazardRules.Name(CurrentHazard).ToUpperInvariant() + ", POINTS x" + HazardRules.Multiplier(CurrentHazard, Run.Balance).ToString("0.00", Inv) + " UNTIL THE CLOCK RUNS OUT");
        }

        // ------------------------------------------------------------------ the break's pages

        static string N(int v) => v.ToString("N0", Inv);

        void ShowBreak()
        {
            if (Menu == null || Run == null) return;
            _fieldPage = false;
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
            if (!string.IsNullOrEmpty(UnlockBreakLines)) body.Append(UnlockBreakLines).Append("\n");
            if (Run.Director.ClearedInOvertime) body.Append("Made it in overtime. Close one!\n");
            if (!string.IsNullOrEmpty(_contractResult)) body.Append(_contractResult).Append("\n");
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
                ? new PauseMenu.MenuChoice("Scratch the free ticket\n" + ScratchOddsLine(b, Run), Scratch)
                : new PauseMenu.MenuChoice("Ticket scratched", null, false));
            choices.Add(new PauseMenu.MenuChoice("Raise the stakes: " + HazardRules.Name(_nextHazard)
                + (_nextHazard == Hazard.None ? "" : "  (points x" + HazardRules.Multiplier(_nextHazard, b).ToString("0.00", Inv) + ")"), ShowStakes));
            if (TryContractRow(out PauseMenu.MenuChoice contract)) choices.Add(contract);
            choices.Add(new PauseMenu.MenuChoice("Start ante " + (ante + 1), StartPressed));
            choices.Add(new PauseMenu.MenuChoice("Restart or quit", ShowLeave));
            // Seven rows at full height no longer fit a 1280 x 800 screen (agent review, 25 Nov 2026): shorter rows then.
            // The body grows to its words (a goal unlocked at the clear adds a line); the menu shrinks the rows to fit.
            bool full = choices.Count >= 7;
            string words = body.ToString().TrimEnd('\n');
            Menu.ShowChoices("ANTE " + ante + " CLEARED", words, choices, null, full ? 176f : 190f,
                _keepSlot, false, full ? 56f : PauseMenu.ChoiceButtonHeight);
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
            // The first antes of a kit run have no pick: the kit's perks were those picks, and one more is its price.
            if (Run.Director.PickGivenAhead && !_skippedPerk && !Run.Director.PerkTakenEarly) return KitPickWords();
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
            string build = parts.Count == 0 ? string.Empty : "Your build: " + string.Join("   ", parts);
            // The Locker's upgrade is part of the build all run (Ralph loop pass 4): its gain and its cost, in words.
            if (LockerUpgrade == null) return build;
            // "Locker" is kept off the screen until the launch base has a real one (Part B section 8).
            string locker = "Upgrade in use: " + Items.Locker.UpgradeWords(LockerUpgrade) + ". Change it in Collection.";
            return build.Length == 0 ? locker : build + "\n" + locker;
        }

        /// <summary>The break's perk pick, on the star chart (Matthew chose option A, 21 Nov 2026).</summary>
        void ShowPerks() => ShowPerks(0);

        void ShowPerks(int selected)
        {
            if (_chartView == null || Run == null) return;
            StarChartPage page = ChartPage(selected);
            page.title = "PICK A PERK";
            page.backLabel = "Back to the break";
            page.footer = PressureLine(Run.Director.Ante + 1);
            page.take = perk => { _chartView.Hide(); TakePerk(perk); };
            page.skip = () => { _chartView.Hide(); SkipPerks(); };
            page.reroll = () => { if (Run.RerollPerks()) Rerolled(false); };
            page.back = () => { _chartView.Hide(); ShowBreak(); };
            _chartView.Show(page);
        }

        /// <summary>
        /// The same pick in the field (21 Nov 2026, Matthew: apply the rewarded upgrades from a Select menu during
        /// the game): open once the ante's target is met, on foot, paused while it is up. Later keeps it for the break.
        /// </summary>
        void ShowFieldPerks() => ShowFieldPerks(0);

        void ShowFieldPerks(int selected)
        {
            if (_chartView == null || Run == null || !Run.Director.PerkEarnedEarly) return;
            StarChartPage page = ChartPage(selected);
            page.title = "PERK EARNED";
            page.subtitle = "Target met: this ante's pick is yours now. Take it, or keep it for the break.";
            page.backLabel = "Later";
            page.footer = "The clock is stopped while this is open. The free ticket waits for the break.";
            page.take = perk =>
            {
                _chartView.Hide();
                if (Run.TakePerk(perk))
                {
                    _skippedPerk = false;
                    AnnouncePerk(perk);
                    PublishBuild();
                    _fieldNote = _breakNote;
                    _breakNote = null;
                }
                ShowFieldUpgrades(1);
            };
            page.skip = () =>
            {
                _chartView.Hide();
                int cash = Run.SkipCash;
                if (Run.Director.PerkEarnedEarly && Run.SkipPerks())
                {
                    _skippedPerk = true;
                    Audio.AudioDirector.PlayLocal("reward.points.rack_end");
                    _fieldNote = "Skipped the perks: +" + N(cash) + " to spend.";
                }
                ShowFieldUpgrades(1);
            };
            page.reroll = () => { if (Run.RerollPerks()) Rerolled(true); };
            page.back = () => { _chartView.Hide(); ShowFieldUpgrades(0); };
            _chartView.Show(page);
        }

        string _fieldNote;

        /// <summary>
        /// Upgrades out on a world (21 Nov 2026; Matthew said yes to the shop on Select as well as the perk): one
        /// short page, paused while it is open. The perk row opens the star chart once the target is met; the shop
        /// is the break's own shop. The free ticket and Raise the stakes stay at the break, so it still matters.
        /// </summary>
        void ShowFieldUpgrades() => ShowFieldUpgrades(-1);

        void ShowFieldUpgrades(int keepSlot)
        {
            if (Menu == null || Run == null || !Run.Director.IsRunning || OrbitalAnte.Ship.Astronaut.PlayerDown) { Menu?.Close(); return; }
            AnteDirector d = Run.Director;
            var body = new StringBuilder();
            body.Append("To spend: ").Append(N(Run.Ledger.SpendablePoints)).Append("  (spending never lowers your score)\n");
            string sets = SetsLine();
            if (sets.Length > 0) body.Append(sets).Append("\n");
            if (!string.IsNullOrEmpty(_fieldNote)) body.Append(_fieldNote).Append("\n");
            body.Append("The clock is stopped while this is open. The free ticket waits for the break.");
            _fieldNote = null;

            var choices = new List<PauseMenu.MenuChoice>();
            if (d.PerkEarnedEarly)
            {
                int offers = Run.PerkOffers().Count;
                choices.Add(new PauseMenu.MenuChoice("Pick a perk  (" + offers + " on offer)", () => ShowFieldPerks(0), offers > 0));
            }
            else if (d.PerkTakenEarly) choices.Add(new PauseMenu.MenuChoice("Perk: " + LastPerkName(), null, false));
            else if (d.PickGivenAhead) choices.Add(new PauseMenu.MenuChoice("Perk: " + KitPickWords(), null, false));
            else if (d.IsLastAnte) choices.Add(new PauseMenu.MenuChoice("Perk: none on the last ante", null, false));
            else choices.Add(new PauseMenu.MenuChoice("Perk: yours when you meet the target  (" + N(Mathf.Max(0, d.Target - Run.Ledger.AnteEarned)) + " to go)", null, false));
            choices.Add(new PauseMenu.MenuChoice("Shop  (" + N(Run.Ledger.SpendablePoints) + " to spend)", () => ShowShop(-1, () => ShowFieldUpgrades(1))));
            choices.Add(new PauseMenu.MenuChoice("Back to the game", () => Menu.Close()));
            Menu.ShowChoices("UPGRADES", body.ToString(), choices, () => Menu.Close(), 120f, keepSlot);
            OpenedFieldPage();
        }

        bool _fieldPage;
        int _fieldPageFrame = -1;

        /// <summary>
        /// A field page is up: pausing turned the Gameplay map off, so Upgrades alone is switched back on, and the
        /// press that opened (or came back to) the page must not also close it.
        /// </summary>
        void OpenedFieldPage()
        {
            _fieldPage = true;
            _fieldPageFrame = Time.frameCount;
            if (GameInput.Upgrades != null) GameInput.Upgrades.Enable();
        }

        void Rerolled(bool field)
        {
            Audio.AudioDirector.PlayLocal("reward.points.rack_end", 0.6f);
            int keep = Mathf.Max(0, _chartView.SelectedIndex);
            if (field) ShowFieldPerks(keep); else ShowPerks(keep);
        }

        /// <summary>What every perk page shares: the offers, the build so far, the wallet, the reroll.</summary>
        StarChartPage ChartPage(int selected)
        {
            int price = Run.RerollPrice;
            List<PerkDefinition> offers = Run.PerkOffers();
            return new StarChartPage
            {
                loadout = Run.Perks,
                offers = offers,
                // A perk unlocked since the last draft is on a card: it says NEW (AnteLoop.Unlocks.cs).
                newPerkId = ShowingNewPerk(offers),
                balance = Run.Balance,
                wallet = Run.Ledger.SpendablePoints,
                rerollPrice = price,
                canReroll = Run.Balance.perkRerollPrice > 0 && Run.Ledger.SpendablePoints >= price,
                skipCash = Run.SkipCash,
                selected = selected,
            };
        }

        /// <summary>The sound and the line for a perk taken: a bigger one when it completes a set.</summary>
        void AnnouncePerk(PerkDefinition perk)
        {
            Audio.AudioDirector.PlayLocal("reward.tier2");
            _breakNote = perk.displayName + ": " + perk.Describe(Run.Balance);
            if (perk.family != PerkFamily.None && Run.Perks.FamilyCount(perk.family) == PerkFamilies.SetSize(Run.Balance))
            {
                Audio.AudioDirector.PlayLocal("reward.tier4");
                _breakNote = perk.family.ToString().ToUpperInvariant() + " SET ON: " + PerkFamilies.Describe(perk.family, Run.Balance);
            }
        }

        void TakePerk(PerkDefinition perk)
        {
            if (!Run.TakePerk(perk)) { ShowBreak(); return; }
            _skippedPerk = false;
            AnnouncePerk(perk);
            PublishBuild();
            _keepSlot = 0;
            ShowBreak();
        }

        void SkipPerks()
        {
            int cash = Run.SkipCash;
            if (!Run.Director.PerkDraftPending || !Run.SkipPerks()) { ShowBreak(); return; }
            _skippedPerk = true;
            Audio.AudioDirector.PlayLocal("reward.points.rack_end");
            _breakNote = "Skipped the perks: +" + N(cash) + " to spend.";
            ShowBreak();
        }

        static readonly PlayerAttribute[] Attributes = { PlayerAttribute.Damage, PlayerAttribute.Movement, PlayerAttribute.Oxygen, PlayerAttribute.Defense };

        void ShowShop() => ShowShop(-1, () => { _keepSlot = 1; ShowBreak(); });

        /// <summary>The shop, at the break or in the field; <paramref name="back"/> is where Back and B go.</summary>
        void ShowShop(int keepSlot, System.Action back)
        {
            AnteBalance b = Run.Balance;
            var choices = new List<PauseMenu.MenuChoice>();
            for (int i = 0; i < Attributes.Length; i++)
            {
                PlayerAttribute a = Attributes[i];
                int level = Run.Attributes.Level(a);
                var quote = Run.Attributes.Quote(a, Run.Ledger);
                string heading = AttributeName(a) + "   level " + level;
                string detail = AttributeWords(a, b);
                if (quote.Price < 0) heading += " (top)";
                else
                {
                    heading += " > " + (level + 1) + "   " + N(quote.Price) + " pts";
                    if (!Run.Director.IsLastAnte && quote.PriceNextAnte >= 0 && quote.PriceNextAnte != quote.Price)
                        heading += " (" + N(quote.PriceNextAnte) + " next ante)";
                    detail += quote.CanBuy ? "   /   " + N(quote.WalletAfter) + " left after buying"
                                          : "   /   " + N(quote.PointsNeeded) + " more needed";
                }
                int slot = i;
                choices.Add(new PauseMenu.MenuChoice(heading + "\n" + detail, () => Buy(a, slot, back), quote.CanBuy));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", back));
            Menu.ShowChoices("SHOP", "To spend: " + N(Run.Ledger.SpendablePoints) + ". Upgrades last for the run. Spending keeps your score.",
                choices, back, 40f, keepSlot);
            if (_fieldPage) OpenedFieldPage();
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
                case PlayerAttribute.Oxygen: return "suit air lasts " + Pct(b.oxygenPerLevel) + " longer a level";
                default: return Pct(b.defensePerLevel) + " less damage taken a level";
            }
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        void Buy(PlayerAttribute a, int slot, System.Action back)
        {
            if (Run.TryBuyAttribute(a))
            {
                Audio.AudioDirector.PlayLocal("reward.tier1");
                PublishBuild();
            }
            ShowShop(slot, back);
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
                    _breakNote = "Ticket: +" + N(Run.ScratchCash(got.Value)) + " to spend.";
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
                    _breakNote = "Ticket: a cosmetic token (nothing uses tokens yet).";
                    Audio.AudioDirector.PlayLocal("reward.tier4");
                    break;
            }
            _keepSlot = 2;
            ShowBreak();
        }

        static string ScratchOddsLine(AnteBalance b, AnteRun run)
        {
            return Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.SmallCash)) + "% +" + N(run.ScratchCash(ScratchOutcome.SmallCash))
                   + ", " + Mathf.RoundToInt(ScratchOff.OddsPercent(b, ScratchOutcome.BigCash)) + "% +" + N(run.ScratchCash(ScratchOutcome.BigCash))
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
                choices.Add(new PauseMenu.MenuChoice(mark + HazardRules.Name(h).ToUpperInvariant() + "   points x" + HazardRules.Multiplier(h, b).ToString("0.00", Inv) + " on the clock"
                    + "\n" + HazardRules.Describe(h, b), () => PickHazard(h)));
            }
            choices.Add(new PauseMenu.MenuChoice("Back", ShowBreak));
            Menu.ShowChoices("RAISE THE STAKES", "One hazard for the next ante, chosen now. Its points bonus lasts while the ante clock runs. In overtime the hazard stays, but points pay x1.00.\n"
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
            // At the break (restart or quit from it) the ante shown is the one just cleared.
            int cleared = Run.Director.Phase == AntePhase.Victory || Run.Director.Phase == AntePhase.Intermission ? ante : ante - 1;

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
            // Gain framed: what was cleared first, then the distance to the next (plain numbers, never dressed up as "so close").
            s.Append("Cleared ").Append(Mathf.Max(0, cleared)).Append(cleared == 1 ? " ante" : " antes").Append(", reached ante ").Append(ante);
            // With nothing banked the distance is the whole target, which says nothing and reads as a scolding.
            if (Run.Director.Phase != AntePhase.Victory && banked < target && banked > 0)
                s.Append(", ").Append(N(target - banked)).Append(" short of ").Append(N(target));
            s.Append(".   Run score ").Append(N(score)).Append(".\n");
            if (save != null)
                s.Append("Best ante ").Append(save.bestAnte).Append("   Best score ").Append(N(save.bestRunScore))
                 .Append("   Runs ").Append(save.runsEnded).Append("\n");
            if (Run.Perks.Owned.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < Run.Perks.Owned.Count; i++) names.Add(Run.Perks.Owned[i].displayName);
                // A kit run says so: Try again starts with the same kit (it is changed at New run).
                StartingKit kit = StartingKits.Find(Run.KitId);
                s.Append("Build: ").Append(string.Join(", ", names));
                if (kit != null && kit.perkIds.Length > 0) s.Append(" (started with the ").Append(kit.displayName).Append(')');
                s.Append("\n");
            }
            string story = _recap != null ? _recap.Lines() : string.Empty;
            if (story.Length > 0) s.Append(story).Append("\n");
            if (newAnte) s.Append("NEW RECORD: your highest ante!\n");
            else if (newScore) s.Append("NEW RECORD: your best run score!\n");
            // This run's unlocks, the next two to earn, then the next mark (Ante/Unlocks.cs, UnlockRecap).
            s.Append(UnlockRecapLines(cleared));
            return s.ToString();
        }
    }
}
