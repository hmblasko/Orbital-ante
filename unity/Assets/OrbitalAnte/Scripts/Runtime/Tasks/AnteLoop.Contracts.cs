using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// Bonus missions (Part B section 5; Matthew, 25 Nov 2026: "clear and fun goals"). Rules in
    /// <see cref="ContractRules"/>. At the break the player may take one of two contracts for the next ante; it shows
    /// on the readout under the world line, its world is marked on the map, and it pays once, the moment it is done.
    /// An unfinished one just ends with the ante. <see cref="TaskDirector"/> reports kills, landings, masts and
    /// salvage through the static calls below.
    /// </summary>
    public sealed partial class AnteLoop
    {
        /// <summary>The bonus mission of the running ante, or null.</summary>
        public Contract ActiveContract { get; private set; }

        Contract _nextContract;
        List<Contract> _contractOffers;
        int _offersForAnte = -1;
        string _contractResult;
        Text _contractLine;
        string _shownContract;
        int _shownContractKey;

        ContractRules ContractDials => Config != null && Config.contracts != null ? Config.contracts : DefaultContracts;
        static readonly ContractRules DefaultContracts = new ContractRules();

        /// <summary>The body the running contract names, while it is not done (-1 otherwise): the map marks it.</summary>
        public static int ContractBody
        {
            get
            {
                AnteLoop loop = Current;
                Contract c = loop != null ? loop.ActiveContract : null;
                return c != null && !c.Paid && loop.Running ? c.Body : -1;
            }
        }

        void ResetContracts()
        {
            ActiveContract = null;
            _nextContract = null;
            _contractOffers = null;
            _offersForAnte = -1;
            _contractResult = null;
            _shownContract = null;
        }

        /// <summary>The ante has begun: the contract taken at the break is live.</summary>
        void BeginAnteContract()
        {
            ActiveContract = _nextContract;
            _nextContract = null;
            _contractOffers = null;
            _offersForAnte = -1;
            _contractResult = null;
            _shownContract = null;
        }

        /// <summary>The ante has ended (banked, won or lost): what became of its contract, said at the break.</summary>
        void EndAnteContract()
        {
            Contract c = ActiveContract;
            if (c != null && !c.Paid) _contractResult = "Bonus mission " + c.Title + " not finished: no loss, it just ends.";
            ActiveContract = null;
            _shownContract = null;
        }

        // ------------------------------------------------------------------ the break

        void EnsureOffers()
        {
            int ante = Run.Director.Ante + 1;
            if (_contractOffers != null && _offersForAnte == ante) return;
            _offersForAnte = ante;
            var nearby = new List<ContractWorld>(8);
            bool melee = false;
            TaskDirector tasks = TaskDirector.Active;
            if (tasks != null)
            {
                tasks.NearbyWorlds(nearby);
                melee = tasks.HoldsMelee;
            }
            _contractOffers = ContractBoard.Offer(ContractDials, ante, nearby, melee, GameSession.Stream("contracts." + ante));
        }

        /// <summary>What a contract pays at its ante's value, before perks and the stakes (said as "about").</summary>
        int ContractPreview(Contract c)
        {
            AnteBalance b = Run.Balance;
            int basePoints = Mathf.Clamp(c.BasePoints, b.optionalMissionMin, Mathf.Max(b.optionalMissionMin, b.optionalMissionMax));
            return ScoreMath.Award(b, basePoints, c.Ante, 0f, 1f, 1f);
        }

        /// <summary>The break's row; false when there is nothing to offer.</summary>
        bool TryContractRow(out PauseMenu.MenuChoice row)
        {
            row = default;
            if (!ContractDials.enabled) return false;
            EnsureOffers();
            if (_nextContract != null)
                row = new PauseMenu.MenuChoice("Bonus mission: " + _nextContract.Title + "  (+" + N(ContractPreview(_nextContract)) + ")", ShowContracts);
            else if (_contractOffers.Count > 0)
                row = new PauseMenu.MenuChoice("Pick a bonus mission  (" + _contractOffers.Count + " on offer)", ShowContracts);
            else return false;
            return true;
        }

        void ShowContracts()
        {
            if (Menu == null || Run == null) return;
            EnsureOffers();
            int ante = Run.Director.Ante + 1;
            var body = new StringBuilder();
            body.Append("One extra goal for ante ").Append(ante).Append(", paid the moment it's done.\n");
            body.Append("It lasts that ante, overtime included. Turning one down, or not finishing it, costs nothing.\n");
            body.Append("Points shown are before perks and the stakes.");
            var choices = new List<PauseMenu.MenuChoice>();
            for (int i = 0; i < _contractOffers.Count; i++)
            {
                Contract offer = _contractOffers[i];
                string mark = offer == _nextContract ? "> " : string.Empty;
                choices.Add(new PauseMenu.MenuChoice(mark + offer.Title + "   +" + N(ContractPreview(offer)) + "\n" + offer.Task, () =>
                {
                    _nextContract = offer;
                    Audio.AudioDirector.PlayLocal("reward.tier1", 0.7f);
                    _keepSlot = 4;
                    ShowBreak();
                }));
            }
            choices.Add(new PauseMenu.MenuChoice(_nextContract == null ? "> No bonus mission" : "No bonus mission", () => { _nextContract = null; _keepSlot = 4; ShowBreak(); }));
            choices.Add(new PauseMenu.MenuChoice("Back to the break", () => { _keepSlot = 4; ShowBreak(); }));
            Menu.ShowChoices("BONUS MISSION", body.ToString(), choices, () => { _keepSlot = 4; ShowBreak(); }, 76f);
        }

        // ------------------------------------------------------------------ what happened in the world

        static AnteLoop LiveContract(out Contract c)
        {
            AnteLoop loop = Current;
            c = loop != null && loop.Running ? loop.ActiveContract : null;
            return c != null && !c.Paid ? loop : null;
        }

        /// <summary>An enemy the player killed went down on <paramref name="body"/>.</summary>
        public static void ContractKill(int body, KillFacts facts)
        {
            AnteLoop loop = LiveContract(out Contract c);
            if (loop != null && c.Kill(body, facts)) loop.ContractMoved();
        }

        /// <summary>The ship touched down on <paramref name="body"/>.</summary>
        public static void ContractLanded(int body)
        {
            AnteLoop loop = LiveContract(out Contract c);
            if (loop != null && c.Landed(body)) loop.ContractMoved();
        }

        public static void ContractMastDown()
        {
            AnteLoop loop = LiveContract(out Contract c);
            if (loop != null && c.MastDown()) loop.ContractMoved();
        }

        public static void ContractCrateDelivered()
        {
            AnteLoop loop = LiveContract(out Contract c);
            if (loop != null && c.CrateDelivered()) loop.ContractMoved();
        }

        void ContractMoved()
        {
            Contract c = ActiveContract;
            if (c == null) return;
            _shownContract = null;
            if (!c.Complete)
            {
                Audio.AudioDirector.PlayLocal("reward.points.tick", 0.8f);
                return;
            }
            c.Paid = true;
            var e = new ScoreEvent { claimId = c.ClaimId, kind = ScoreEventKind.OptionalMission, missionBasePoints = c.BasePoints };
            ScoreResult paid = Report(e, "BONUS MISSION: " + c.Title);
            Audio.AudioDirector.PlayLocal("reward.tier2", 0.9f);
            Note("BONUS MISSION DONE: " + c.Title);
            if (paid.Awarded) _contractResult = "Bonus mission " + c.Title + " done: +" + N(paid.points) + ".";
        }

        // ------------------------------------------------------------------ readout

        void BuildContractHud()
        {
            // Top right, under the task plate (RunGuideFx, which ends at -86). It stays in a fight, when the
            // task plate goes, so it stands by itself with a halo and is not a row of that plate.
            _contractLine = HullKit.Mono(_canvas.transform, string.Empty, 15, HullKit.Paint, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_contractLine.transform, new Vector2(1f, 1f), new Vector2(-HullKit.Margin - 2f, -102f), new Vector2(520f, 20f));
            UiKit.Halo(_contractLine, true);
            _contractLine.gameObject.SetActive(false);
        }

        void UpdateContractHud()
        {
            if (_contractLine == null) return;
            Contract c = ActiveContract;
            // Aboard, the ship's damage list has this corner, as for the "+points" lines.
            bool on = c != null && Run.Director.IsRunning && !OrbitalAnte.Ship.Astronaut.PlayerInShip;
            if (_contractLine.gameObject.activeSelf != on) _contractLine.gameObject.SetActive(on);
            if (!on) return;
            // Words only when the count changes (no string built per frame).
            int key = c.Done * 2 + (c.Paid ? 1 : 0);
            if (_shownContract != null && key == _shownContractKey) return;
            _shownContractKey = key;
            // "~": the pop-up adds perks and the stakes to this.
            string text = "BONUS · " + c.Progress + (c.Paid ? string.Empty : "  ~+" + N(ContractPreview(c)));
            _shownContract = text;
            _contractLine.text = text;
            _contractLine.color = c.Paid ? Config.scoreColour : HullKit.Paint;
        }
    }
}
