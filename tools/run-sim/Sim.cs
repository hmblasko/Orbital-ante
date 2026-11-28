using System;
using System.Collections.Generic;
using System.Linq;
using OrbitalAnte.Ante;

namespace OrbitalAnte.RunSim
{
    /// <summary>
    /// A made-up player. Every number here is a guess, chosen so a first-time player tends to die around ante 3-4
    /// as Part B section 5 asks. Change them when the Deck playtest gives real ones (kills a minute, deaths per ante).
    /// </summary>
    public sealed class Profile
    {
        public string Name;
        /// <summary>Kills a minute while fighting, at ante 1 with the first gun.</summary>
        public float KillsPerMinute;
        /// <summary>Share of each minute spent fighting (the rest is flying, walking, looking).</summary>
        public float FightingShare;
        /// <summary>Chance a kill has a style feat (ONE SHOT, LONG SHOT, MELEE, ...).</summary>
        public float FeatChance;
        public float MeleeShare, LongShotShare, LowStanceShare;
        /// <summary>Chance a second of fighting starts a stretch under fire, and the damage a second while under fire.</summary>
        public float UnderFireChance, DamagePerSecond;
        /// <summary>Mean seconds a stretch under fire lasts before the player breaks it off or wins it.</summary>
        public float UnderFireSeconds;
        /// <summary>New places a minute (first landings) and hard landings an ante, other one-off scores an ante.</summary>
        public float FirstLandingsPerAnte, HardLandingsPerAnte, BaseClearsPerAnte, SalvagePerAnte;

        public static readonly Profile New = new Profile
        {
            Name = "First-time", KillsPerMinute = 3.0f, FightingShare = 0.55f, FeatChance = 0.15f,
            MeleeShare = 0.08f, LongShotShare = 0.08f, LowStanceShare = 0.1f,
            UnderFireChance = 0.16f, DamagePerSecond = 9f, UnderFireSeconds = 4.0f,
            FirstLandingsPerAnte = 0.9f, HardLandingsPerAnte = 0.6f, BaseClearsPerAnte = 0.25f, SalvagePerAnte = 0.2f,
        };
        public static readonly Profile Average = new Profile
        {
            Name = "Average", KillsPerMinute = 4.2f, FightingShare = 0.6f, FeatChance = 0.3f,
            MeleeShare = 0.12f, LongShotShare = 0.15f, LowStanceShare = 0.2f,
            UnderFireChance = 0.14f, DamagePerSecond = 8f, UnderFireSeconds = 3.6f,
            FirstLandingsPerAnte = 1.0f, HardLandingsPerAnte = 1.0f, BaseClearsPerAnte = 0.5f, SalvagePerAnte = 0.4f,
        };
        public static readonly Profile Skilled = new Profile
        {
            Name = "Skilled", KillsPerMinute = 5.6f, FightingShare = 0.65f, FeatChance = 0.5f,
            MeleeShare = 0.15f, LongShotShare = 0.25f, LowStanceShare = 0.3f,
            UnderFireChance = 0.12f, DamagePerSecond = 7f, UnderFireSeconds = 3.2f,
            FirstLandingsPerAnte = 1.2f, HardLandingsPerAnte = 1.5f, BaseClearsPerAnte = 0.9f, SalvagePerAnte = 0.6f,
        };
        public static readonly Profile[] All = { New, Average, Skilled };
    }

    public enum DraftPolicy { Skip, Random, StayInFamily }

    public sealed class Policy
    {
        public bool Overtime;
        public DraftPolicy Draft = DraftPolicy.StayInFamily;
        /// <summary>Take this perk at the first break (and skip later ones), to measure what one perk is worth.</summary>
        public string Forced;
        public Hazard AlwaysHazard = Hazard.None;
        /// <summary>Overtime: the ante goes on, the run is hunted, until the target is met or the player dies.</summary>
        public float OvertimeDangerScale = 1.4f;
        /// <summary>Safety stop: an overtime this long counts as the run ending (the model player gives up).</summary>
        public float OvertimeLimit = 900f;
        /// <summary>Take the pick from the star chart the moment the target is met (21 Nov 2026), not at the break.</summary>
        public bool FieldPick;
        /// <summary>At the break, buy new offers once when none fits the build and the wallet holds twice the price.</summary>
        public bool Reroll;
        /// <summary>Start the run with this starting kit (Ralph loop pass 1, 27 Nov 2026), or null for Standard.</summary>
        public string Kit;
    }

    public sealed class AnteRow
    {
        public int Entered, Cleared, OnClock, Overtime, Died;
        public float OvertimeSeconds;
        public long Wallet;
    }

    public sealed class Stats
    {
        public int Runs, Reach4, ClearAll, EndedByClock, EndedByDeath, NearMiss, SetsDone, FieldPicks, Rerolls;
        public List<int> AnteReached = new List<int>();
        public List<float> Gaps = new List<float>();
        public double ScoreSum, ClearedSum;
        public AnteRow[] Antes = Enumerable.Range(0, 11).Select(_ => new AnteRow()).ToArray();

        public int MedianAnte => AnteReached.Count == 0 ? 0 : AnteReached.OrderBy(x => x).ElementAt(AnteReached.Count / 2);
        public float MeanGap => Gaps.Count == 0 ? 0 : Gaps.Average();
        public float P90Gap => Gaps.Count == 0 ? 0 : Gaps.OrderBy(x => x).ElementAt((int)(Gaps.Count * 0.9));
        public float MeanScore => (float)(ScoreSum / Math.Max(1, Runs));
        public float MeanCleared => (float)(ClearedSum / Math.Max(1, Runs));
        public string Pct(int n) => Sim.P(n, Runs);
    }

    public static class Sim
    {
        public static string P(int n, int of) => of == 0 ? "-" : $"{100.0 * n / of:0}%";

        /// <summary>One scale on every profile's danger, set by <see cref="Calibrate"/>.</summary>
        public static float Danger = 1f;

        /// <summary>
        /// Finds the danger scale at which half of first-time players reach ante 4 with no perks bought on purpose
        /// (Part B section 5: first-time players "should typically die around ante 3-4"). Everything else is then
        /// read against that one fixed point.
        /// </summary>
        public static float Calibrate(int runs, Policy policy)
        {
            float lo = 0.05f, hi = 3f;
            for (int i = 0; i < 18; i++)
            {
                Danger = (lo + hi) / 2f;
                var st = Many(Profile.New, runs, policy);
                if ((float)st.Reach4 / st.Runs > 0.5f) lo = Danger; else hi = Danger;
            }
            Danger = (lo + hi) / 2f;
            return Danger;
        }

        public static Stats Many(Profile p, int runs, Policy policy)
        {
            var s = new Stats();
            for (int i = 0; i < runs; i++) One(p, 1000 + i, policy, s);
            return s;
        }

        static readonly int[] LadderSteps = { 4, 5, 6, 7 };

        static void One(Profile p, int seed, Policy policy, Stats s)
        {
            var balance = new AnteBalance { missedTarget = policy.Overtime ? MissedTarget.Overtime : MissedTarget.EndRun };
            var run = new AnteRun(balance, seed, RoguelitePerks.Create());
            if (policy.Kit != null) run.StartWithKit(StartingKits.Find(policy.Kit));
            var rng = new Random(seed * 7919 + 13);
            int claim = 0, kills = 0, ladderStep = 0, ladderCredit = 0;
            float health = 100f, sinceHit = 99f, underFire = 0f, lastPop = 0f, clock = 0f;
            bool dead = false, endedByClock = false;
            int stalled = 0;
            int reached = 1, cleared = 0;
            Hazard hazard = Hazard.None;
            RunMods mods = RunMods.From(run.Perks, run.Attributes, hazard, balance);

            void Pop() { if (lastPop > 0f || clock > 0f) s.Gaps.Add(clock - lastPop); lastPop = clock; }

            ScoreResult Report(ScoreEventKind kind, string tag, int feats)
            {
                var e = new ScoreEvent { claimId = "sim:" + (claim++), kind = kind, tag = tag, skillFeats = feats, verifiedSkill = feats > 0 };
                if (HazardRules.PaysNow(hazard, run.Director.InOvertime)) { e.acceptedRisk = true; e.riskMultiplier = HazardRules.Multiplier(hazard, balance); }
                var r = run.Report(e);
                if (r.Awarded) Pop();
                return r;
            }

            while (true)
            {
                int ante = run.Director.Ante;
                reached = ante;
                AnteRow row = s.Antes[ante];
                row.Entered++;
                if (hazard == Hazard.ShortFuse) run.Director.AdjustSeconds(-balance.shortFuseSeconds);
                EnemyPressure pressure = EnemyPressure.For(ante, hazard, balance);

                // One-off scores this ante, spread over its five minutes.
                var oneOffs = new List<(float at, ScoreEventKind kind, string tag)>();
                void Plan(float mean, ScoreEventKind kind, string tag)
                {
                    int n = Poisson(rng, mean);
                    for (int i = 0; i < n; i++) oneOffs.Add(((float)rng.NextDouble() * 280f + 10f, kind, tag));
                }
                Plan(p.FirstLandingsPerAnte * mods.Move, rng.NextDouble() < 0.4 ? ScoreEventKind.NewMoon : ScoreEventKind.NewPlanet, null);
                Plan(p.HardLandingsPerAnte, ScoreEventKind.DifficultLanding, null);
                Plan(p.BaseClearsPerAnte, ScoreEventKind.OptionalMission, "base");
                Plan(p.SalvagePerAnte * mods.Move, ScoreEventKind.SalvageDelivered, null);
                oneOffs.Sort((a, b) => a.at.CompareTo(b.at));
                int nextOneOff = 0;

                float t = 0f, overtime = 0f;
                bool inOvertime = false;
                while (true)
                {
                    t += 1f; clock += 1f;
                    run.Tick(1f, false);
                    bool fighting = rng.NextDouble() < p.FightingShare;

                    // Kills: slower against heavies and the sharper enemies of later antes; quicker with a better gun.
                    float heavySlow = 1f + 0.8f * pressure.HeavyShare;
                    float sharpSlow = 0.75f + 0.25f * pressure.Sharpness;
                    float gun = (float)Math.Pow(mods.GunShot(rng.NextDouble() < p.LowStanceShare, health / (100f * mods.MaxHealth)), 0.7) * (1f + 0.08f * ladderStep);
                    float rate = fighting ? p.KillsPerMinute / 60f * gun * sharpSlow / heavySlow : 0f;
                    if (rng.NextDouble() < rate)
                    {
                        kills++; ladderCredit++;
                        bool melee = rng.NextDouble() < p.MeleeShare, longShot = rng.NextDouble() < p.LongShotShare;
                        int feats = rng.NextDouble() < p.FeatChance ? 1 : 0;
                        if (longShot || melee) feats = Math.Max(feats, 1);
                        double roll = rng.NextDouble();
                        ScoreEventKind kind = roll < pressure.HeavyShare ? ScoreEventKind.EnemyTier3
                            : roll < 0.72 ? ScoreEventKind.EnemyTier1 : roll < 0.92 ? ScoreEventKind.EnemyTier2 : ScoreEventKind.EnemyTier3;
                        var r = Report(kind, melee ? "melee" : null, feats);
                        if (inOvertime) { }
                        float maxHp = 100f * mods.MaxHealth;
                        health = Math.Min(maxHp, health + mods.HealFor(melee, false, health / maxHp));
                        if (r.Awarded && longShot && mods.TimeOnLongShot > 0f) run.Director.AdjustSeconds(mods.TimeOnLongShot);
                        if (mods.LadderDouble(kills)) ladderCredit++;
                        while (ladderStep < LadderSteps.Length && ladderCredit >= LadderSteps[ladderStep]) { ladderCredit -= LadderSteps[ladderStep]; ladderStep++; }
                    }

                    // Being shot: stretches under fire, worse against sharper enemies, heavies and in overtime.
                    if (underFire <= 0f && fighting && rng.NextDouble() < p.UnderFireChance)
                        underFire = (float)(-Math.Log(1.0 - rng.NextDouble()) * p.UnderFireSeconds / mods.Move);   // quicker feet break line of sight sooner (a guess)
                    if (underFire > 0f)
                    {
                        underFire -= 1f;
                        float danger = (1f / Math.Max(0.4f, pressure.Sharpness)) * (1f + 0.5f * pressure.HeavyShare) * (inOvertime ? policy.OvertimeDangerScale : 1f);
                        health -= mods.Taken(p.DamagePerSecond * danger * Danger, 20f);
                        sinceHit = 0f;
                    }
                    else sinceHit += 1f;
                    if (sinceHit >= 4f && !mods.NoRegen) health = Math.Min(100f * mods.MaxHealth, health + 18f * mods.Regen);
                    if (health <= 0f) { dead = true; break; }

                    while (nextOneOff < oneOffs.Count && oneOffs[nextOneOff].at <= t)
                    {
                        var o = oneOffs[nextOneOff++];
                        var r = Report(o.kind, o.tag, o.kind == ScoreEventKind.DifficultLanding ? 1 : 0);
                        if (r.Awarded && mods.LandingTimeBack > 0f && (o.kind == ScoreEventKind.NewMoon || o.kind == ScoreEventKind.NewPlanet || o.kind == ScoreEventKind.DifficultLanding))
                            run.Director.AdjustSeconds(mods.LandingTimeBack);
                    }

                    if (policy.FieldPick && policy.Forced == null && run.Director.PerkEarnedEarly)
                    {
                        PerkDefinition early = Choose(run, run.PerkOffers(), policy, rng);
                        if (early != null && run.TakePerk(early))
                        {
                            s.FieldPicks++;
                            float hpShare = health / (100f * mods.MaxHealth);
                            mods = RunMods.From(run.Perks, run.Attributes, hazard, balance);
                            health = hpShare * 100f * mods.MaxHealth;
                        }
                    }
                    if (run.Director.Phase == AntePhase.Intermission || run.Director.Phase == AntePhase.Victory) break;
                    if (run.Director.Phase == AntePhase.Failed) { endedByClock = true; break; }
                    if (run.Director.InOvertime && !inOvertime)
                    {
                        // The game's overtime (21 Nov 2026): the ante runs on, hunted, until the target is met.
                        inOvertime = true;
                        pressure = EnemyPressure.For(ante, Hazard.Hunted, balance);
                    }
                    if (inOvertime) overtime += 1f;
                    if (overtime > policy.OvertimeLimit) { endedByClock = true; stalled++; break; }
                }

                float share = run.Director.Target > 0 ? (float)run.Ledger.AnteEarned / run.Director.Target : 1f;
                if (dead || endedByClock)
                {
                    if (dead) row.Died++;
                    if (share >= 0.85f && share < 1f) s.NearMiss++;
                    break;
                }
                if (inOvertime) { row.Overtime++; row.OvertimeSeconds += overtime; } else row.OnClock++;
                row.Cleared++;
                cleared++;
                if (run.Director.Phase != AntePhase.Intermission) { /* overtime clear: treated as cleared */ }
                if (run.Director.IsLastAnte) break;

                // The break: perk, shop, scratch, stakes.
                if (run.Director.PerkDraftPending)
                {
                    var offers = run.PerkOffers();
                    PerkDefinition pick = null;
                    if (policy.Forced != null)
                    {
                        if (!run.Perks.Has(policy.Forced)) pick = run.Catalog.First(x => x.id == policy.Forced);
                        if (pick != null) { run.Perks.Add(pick); pick = null; }
                    }
                    else
                    {
                        if (policy.Reroll && run.Perks.Owned.Count > 0 && !offers.Any(o => o.family != PerkFamily.None && run.Perks.FamilyCount(o.family) > 0)
                            && run.Ledger.SpendablePoints >= 2 * run.RerollPrice && run.RerollPerks())
                        {
                            s.Rerolls++;
                            offers = run.PerkOffers();
                        }
                        pick = Choose(run, offers, policy, rng);
                    }
                    if (pick != null && !run.TakePerk(pick)) run.Perks.Add(pick);
                }
                if (run.Director.ScratchPending) run.UseScratchOff();
                row.Wallet += run.Ledger.SpendablePoints;
                // Buy the cheapest affordable level, defence and damage first.
                for (int guard = 0; guard < 8; guard++)
                {
                    var choice = new[] { PlayerAttribute.Defense, PlayerAttribute.Damage, PlayerAttribute.Movement, PlayerAttribute.Oxygen }
                        .Where(a => !run.Attributes.IsMaxed(a) && run.Ledger.CanAfford(run.Attributes.NextPrice(a)))
                        .OrderBy(a => run.Attributes.NextPrice(a)).ToList();
                    if (choice.Count == 0 || !run.TryBuyAttribute(choice[0])) break;
                }
                hazard = policy.AlwaysHazard;
                if (!run.BeginNextAnte())
                {
                    // An overtime clear is outside these rules: start the next ante by hand.
                    break;
                }
                mods = RunMods.From(run.Perks, run.Attributes, hazard, balance);
                health = 100f * mods.MaxHealth;
            }

            s.Runs++;
            s.AnteReached.Add(reached);
            if (reached >= 4) s.Reach4++;
            if (cleared >= 9) s.ClearAll++;
            if (dead) s.EndedByDeath++; else if (endedByClock) s.EndedByClock++;
            if (PerkFamilies.All.Any(f => PerkFamilies.Active(run.Perks, f, balance))) s.SetsDone++;
            s.ScoreSum += run.Ledger.LifetimeRunScore;
            s.ClearedSum += cleared;
        }

        static PerkDefinition Choose(AnteRun run, List<PerkDefinition> offers, Policy policy, Random rng)
        {
            if (offers == null || offers.Count == 0) return null;
            if (policy.Draft == DraftPolicy.Random) return offers[rng.Next(offers.Count)];
            if (policy.Draft == DraftPolicy.StayInFamily)
                return offers.OrderByDescending(o => run.Perks.Owned.Count(h => h.family == o.family && o.family != PerkFamily.None)).ThenBy(_ => rng.Next()).First();
            return null;
        }

        static int Poisson(Random rng, float mean)
        {
            double l = Math.Exp(-mean), pr = 1.0; int k = 0;
            do { k++; pr *= rng.NextDouble(); } while (pr > l);
            return k - 1;
        }
    }
}
