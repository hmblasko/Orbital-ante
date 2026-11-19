using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The contract between the world and the point system. A world system (enemies, landmarks, bounties,
    /// satellites...) reports one of these when something scoreable is COMPLETED. It never works out points
    /// itself. Systems that do not exist yet (Milestones 13, 15 and 16) only need to fill this in.
    ///
    /// Things that are deliberately not events: hits, limb pops, disarms, time survived, distance flown, scans.
    /// </summary>
    public struct ScoreEvent
    {
        /// <summary>
        /// One id per scoreable thing for the whole run, the same after a save and load. Build it from seeded
        /// world ids, never from a counter: "enemy:mars.base2.guard7", "landmark:mars.canyon", "landing:europa",
        /// "bounty:poster12" (the same id whether the target comes back dead or alive, so it pays once).
        /// </summary>
        public string claimId;
        public ScoreEventKind kind;
        /// <summary>Optional finer label that perks can look for ("wreck").</summary>
        public string tag;
        /// <summary>For missions: what this mission is worth, kept inside the allowed range.</summary>
        public int missionBasePoints;
        /// <summary>For capped kinds: what the cap is counted against (the base id for a retake mission).</summary>
        public string capGroup;
        /// <summary>The game checked a real feat of skill (set by the reporting system, never by the player).</summary>
        public bool verifiedSkill;
        /// <summary>The player chose a real, visible risk beforehand.</summary>
        public bool acceptedRisk;
        /// <summary>True for anything that came back after being cleared (respawned enemies, a base retaken without a mission). These never score.</summary>
        public bool isRespawn;
        /// <summary>For bounties: which faction posted it ("A", "B", "C").</summary>
        public string postingFaction;
    }

    public enum ScoreOutcome
    {
        Awarded = 0,
        /// <summary>This thing has already paid out this run.</summary>
        AlreadyClaimed = 1,
        /// <summary>Respawned or otherwise not a first-time, completed event.</summary>
        NotEligible = 2,
        /// <summary>The finite allowance for this kind of event is used up.</summary>
        CapReached = 3,
        /// <summary>No point value has been set for this kind yet.</summary>
        NoValueSet = 4,
        /// <summary>No claim id: the event cannot be told apart from a repeat, so it is refused.</summary>
        MissingClaimId = 5,
        /// <summary>No ante is being played (between antes, or the run is over).</summary>
        AnteNotRunning = 6,
    }

    public struct ScoreResult
    {
        public ScoreOutcome outcome;
        public int points;
        public int basePoints;
        public ScoreCategory category;
        /// <summary>True only for a bounty delivered alive. The diplomacy system (Milestone 16) acts on it.</summary>
        public bool grantsGoodwill;
        public string goodwillFaction;
        public bool chainBonusUsed;

        public bool Awarded => outcome == ScoreOutcome.Awarded;
    }

    /// <summary>One line of the score log shown in the developer balance panel and sent to telemetry.</summary>
    public struct ScoreLogEntry
    {
        public int ante;
        public float runSeconds;
        public ScoreEvent scoreEvent;
        public ScoreResult result;
    }

    /// <summary>
    /// Turns reported events into points: refuses repeats and farms, applies the formula, pays the ledger and
    /// keeps a log. Plain C#, no Unity types.
    /// </summary>
    public sealed class ScoreService
    {
        public const int LogCapacity = 256;

        readonly AnteBalance _balance;
        readonly RunLedger _ledger;
        readonly ClaimRegistry _claims;
        readonly PerkLoadout _perks;
        readonly List<ScoreLogEntry> _log = new List<ScoreLogEntry>();

        // "Change of Pace" perk bookkeeping.
        bool _hasLast;
        ScoreCategory _lastCategory;
        float _lastSeconds;
        int _chainUsesThisAnte;

        public ScoreService(AnteBalance balance, RunLedger ledger, ClaimRegistry claims, PerkLoadout perks)
        {
            _balance = balance;
            _ledger = ledger;
            _claims = claims;
            _perks = perks;
        }

        /// <summary>The most recent submissions, refused ones included, oldest first.</summary>
        public IReadOnlyList<ScoreLogEntry> Log => _log;

        /// <summary>Raised for every submission, refused ones included (the HUD shows only the awarded ones).</summary>
        public event Action<ScoreLogEntry> Submitted;

        public int ChainUsesThisAnte => _chainUsesThisAnte;

        /// <param name="ante">The ante being played (1 is the first).</param>
        /// <param name="anteRunning">False between antes and after the run ends: nothing scores then.</param>
        /// <param name="runSeconds">Play time so far, not counting pauses.</param>
        public ScoreResult Submit(ScoreEvent scoreEvent, int ante, bool anteRunning, float runSeconds)
        {
            ScoreResult result = Decide(scoreEvent, ante, anteRunning, runSeconds);
            var entry = new ScoreLogEntry { ante = ante, runSeconds = runSeconds, scoreEvent = scoreEvent, result = result };
            if (_log.Count >= LogCapacity) _log.RemoveAt(0);
            _log.Add(entry);
            Submitted?.Invoke(entry);
            return result;
        }

        ScoreResult Decide(ScoreEvent scoreEvent, int ante, bool anteRunning, float runSeconds)
        {
            var result = new ScoreResult { category = AnteBalance.CategoryOf(scoreEvent.kind) };

            if (!anteRunning) { result.outcome = ScoreOutcome.AnteNotRunning; return result; }
            if (string.IsNullOrEmpty(scoreEvent.claimId)) { result.outcome = ScoreOutcome.MissingClaimId; return result; }
            if (scoreEvent.isRespawn) { result.outcome = ScoreOutcome.NotEligible; return result; }
            if (_claims.IsClaimed(scoreEvent.claimId)) { result.outcome = ScoreOutcome.AlreadyClaimed; return result; }

            int basePoints = BasePointsFor(scoreEvent);
            result.basePoints = basePoints;
            if (basePoints <= 0) { result.outcome = ScoreOutcome.NoValueSet; return result; }

            string capKey = CapKey(scoreEvent, ante, out int capLimit);
            if (capKey != null && _claims.Count(capKey) >= capLimit) { result.outcome = ScoreOutcome.CapReached; return result; }

            float perkBonus = _perks == null ? 0f : _perks.ScoreBonusFor(scoreEvent.kind, scoreEvent.tag);
            float chain = _perks == null ? 0f : _perks.ChainBonus();
            bool useChain = chain > 0f && _hasLast && _lastCategory != result.category
                            && runSeconds - _lastSeconds <= _balance.chainWindowSeconds
                            && _chainUsesThisAnte < _balance.chainUsesPerAnte;
            if (useChain) perkBonus += chain;

            float skill = scoreEvent.verifiedSkill ? _balance.skillVerified : 1f;
            float risk = scoreEvent.acceptedRisk ? _balance.riskAccepted : 1f;
            int points = ScoreMath.Award(_balance, basePoints, ante, perkBonus, skill, risk);

            _claims.TryClaim(scoreEvent.claimId);
            if (capKey != null) _claims.Increment(capKey);
            if (useChain) _chainUsesThisAnte++;
            _hasLast = true;
            _lastCategory = result.category;
            _lastSeconds = runSeconds;
            _ledger.AddScore(points);

            result.outcome = ScoreOutcome.Awarded;
            result.points = points;
            result.chainBonusUsed = useChain;
            if (scoreEvent.kind == ScoreEventKind.BountyAlive)
            {
                result.grantsGoodwill = true;
                result.goodwillFaction = scoreEvent.postingFaction;
            }
            return result;
        }

        int BasePointsFor(ScoreEvent scoreEvent)
        {
            if (scoreEvent.kind == ScoreEventKind.OptionalMission || scoreEvent.kind == ScoreEventKind.BaseRetakeMission)
            {
                return Math.Max(_balance.optionalMissionMin, Math.Min(_balance.optionalMissionMax, scoreEvent.missionBasePoints));
            }
            return _balance.BasePoints(scoreEvent.kind);
        }

        string CapKey(ScoreEvent scoreEvent, int ante, out int limit)
        {
            limit = int.MaxValue;
            switch (scoreEvent.kind)
            {
                case ScoreEventKind.UnderThreatRecovery:
                    limit = _balance.underThreatRecoveriesPerAnte;
                    return "cap:recovery:ante" + ante;
                case ScoreEventKind.BaseRetakeMission:
                    limit = _balance.retakeMissionsPerBasePerRun;
                    return "cap:retake:" + (scoreEvent.capGroup ?? "");
                default:
                    return null;
            }
        }

        /// <summary>Called when a new ante starts.</summary>
        public void StartNewAnte()
        {
            _chainUsesThisAnte = 0;
            _hasLast = false;
        }

        public void ExportChain(out bool hasLast, out int lastCategory, out float lastSeconds, out int chainUses)
        {
            hasLast = _hasLast;
            lastCategory = (int)_lastCategory;
            lastSeconds = _lastSeconds;
            chainUses = _chainUsesThisAnte;
        }

        public void ImportChain(bool hasLast, int lastCategory, float lastSeconds, int chainUses)
        {
            _hasLast = hasLast;
            _lastCategory = (ScoreCategory)lastCategory;
            _lastSeconds = lastSeconds;
            _chainUsesThisAnte = Math.Max(0, chainUses);
        }
    }
}
