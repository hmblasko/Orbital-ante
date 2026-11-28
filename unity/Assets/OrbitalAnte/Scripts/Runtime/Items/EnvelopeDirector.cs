using System;
using System.Collections;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Save;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// Runs the envelope side of the game (lives on the game's root object): seals a new envelope when a run starts
    /// and shows it, loses it when the run ends early, unlocks and reveals it when Ante 9 is cleared, and opens kept
    /// envelopes from the collection page. Everything it decides is in the plain C# rules
    /// (<see cref="EnvelopeBook"/>, <see cref="RunEnvelope"/>, <see cref="PrizeRoll"/>).
    ///
    /// The ante loop is not in the game yet (Milestone 14). It will call <see cref="AnteCleared"/> (or hand its
    /// director to <see cref="RunEnvelope.Follow"/> on <see cref="Current"/>). Until then, in the editor and
    /// development builds, F8 during a run pretends Ante 9 was cleared, and its prize is marked as a test item.
    /// </summary>
    public sealed class EnvelopeDirector : MonoBehaviour
    {
        public static EnvelopeDirector Instance { get; private set; }

        EnvelopeScreen _screen;
        RunEnvelope _run;

        /// <summary>This run's envelope, or null outside a run.</summary>
        public RunEnvelope Current => _run;
        public bool Showing => _screen != null && _screen.Showing;

        public static void ResetStatics()
        {
            Instance = null;
            EnvelopeTextures.ResetStatics();
            EnvelopeConfig.ResetStatics();
        }

        /// <summary>Test envelopes and the F8 shortcut exist only where Matthew tests: the editor and development builds.</summary>
        public static bool TestsAllowed => Application.isEditor || Debug.isDebugBuild;

        public void Build(Transform uiRoot)
        {
            Instance = this;
            var go = new GameObject("EnvelopeScreenController");
            go.transform.SetParent(uiRoot, false);
            _screen = go.AddComponent<EnvelopeScreen>();
            _screen.Build(uiRoot);
            StartCoroutine(PaintAhead());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Paints the envelope's pieces one a frame while the title screen shows, so no run waits for them.</summary>
        IEnumerator PaintAhead()
        {
            yield return null;
            foreach (string name in EnvelopeArt.Pieces().Keys)
            {
                EnvelopeTextures.Get(name);
                yield return null;
            }
        }

        static EnvelopeBook Book()
        {
            var book = new EnvelopeBook(SaveSystem.Data);
            book.EnsureSalt(GameSession.NewRandomSeed() + 1);
            return book;
        }

        // ------------------------------------------------------------------ the run

        /// <summary>A run is starting: number its envelope and show it. Call before the run's first save.</summary>
        public void RunStarted() => RunStarted(false);

        /// <param name="restartedFromARun">The run was restarted from the death page or the pause menu: its
        /// envelope is sealed as always but not shown again (<see cref="RunEnvelope.ShowsAtStart"/>).</param>
        public void RunStarted(bool restartedFromARun)
        {
            _screen.Close();
            _run = RunEnvelope.Seal(Book());
            if (!RunEnvelope.ShowsAtStart(EnvelopeConfig.Load().showAtRunStart, restartedFromARun)) return;
            _screen.UseCard(null);
            _screen.Show(new EnvelopeShow
            {
                mode = EnvelopeMode.RunStart,
                serial = _run.Serial,
                antesCleared = _run.AntesCleared,
                oddsLine = EnvelopeScene.OddsLine(EnvelopeConfig.Load().odds),
                prompt = "Press " + SouthButton() + ", Enter or click to begin",
            }, null);
        }

        /// <summary>The run is being torn down (death, restart, quitting). A sealed envelope is lost.</summary>
        public void RunEnded()
        {
            _run?.OnRunEnded();
            _run = null;
            _screen.Close();
        }

        /// <summary>
        /// For the ante loop: an ante was cleared. Clearing Ante 9 unlocks this run's envelope, keeps it in the save
        /// and opens it at once. <paramref name="whenDone"/> runs once the player closes the reveal (or at once if
        /// nothing was shown).
        /// </summary>
        public void AnteCleared(int ante, Action whenDone = null, bool fromTest = false)
        {
            if (_run == null) { whenDone?.Invoke(); return; }
            EnvelopeBook book = Book();
            if (!_run.OnAnteCleared(ante, book, fromTest)) { whenDone?.Invoke(); return; }
            SaveSystem.Save();
            EnvelopeTicket ticket = FindTicket(book, _run.Serial);
            if (ticket == null || !Open(ticket, whenDone)) whenDone?.Invoke();
        }

        void Update()
        {
            if (!TestsAllowed || _run == null || Showing || GameSession.Paused || GameSession.Mode != GameMode.Playing) return;
            Keyboard k = Keyboard.current;
            if (k != null && k.f8Key.wasPressedThisFrame) AnteCleared(RunEnvelope.UnlockAnte, null, fromTest: true);
        }

        // ------------------------------------------------------------------ opening

        /// <summary>Opens a kept envelope: the prize goes into the collection and is saved first, then the reveal plays.</summary>
        public bool Open(EnvelopeTicket ticket, Action whenDone)
        {
            EnvelopeBook book = Book();
            List<ItemSpec> catalog = Catalog();
            List<ItemEffect> effects = ItemCatalog.CreateEffects();
            Prize prize = book.Open(ticket, EnvelopeConfig.Load().odds, catalog, effects);
            if (!prize.IsValid) return false;
            ItemSpec item = catalog.Find(i => i.id == prize.itemId);
            // A prize goes to work by itself where its place is empty or holds a look earned by a goal (never an
            // upgrade: each has a cost), saved with it. What held its place is named either way.
            string held = Locker.InPlaceOf(SaveSystem.Data, item, LockerLook.Find);
            bool placed = Locker.AutoUse(SaveSystem.Data, item, prize.effectId);
            bool inUse = placed || Locker.InUse(SaveSystem.Data, item, prize.effectId);
            SaveSystem.Save();
            string inPlaceOf = placed ? (string.IsNullOrEmpty(held) ? null : held) : inUse ? null : Locker.InPlaceOf(SaveSystem.Data, item, LockerLook.Find);
            // In the middle of a run (the reveal at ante 9) the look goes on there and then: the lander, the suit and
            // the gun in hand are told to read the Locker again.
            if (placed && GameSession.Mode == GameMode.Playing) LockerLook.RefreshSoon();

            ItemEffect effect = effects.Find(e => e.id == prize.effectId);
            OwnedItem owned = book.Inventory.Find(prize.itemId, prize.effectId);
            _screen.UseCard(EnvelopeTextures.Card("Card", item, prize.rarity, effect?.colour));
            _screen.Show(ShowFor(item, prize, effect, owned, ticket, inUse, inPlaceOf), whenDone);
            return true;
        }

        public static EnvelopeShow ShowFor(ItemSpec item, Prize prize, ItemEffect effect, OwnedItem owned, EnvelopeTicket ticket,
            bool inUse = false, string inPlaceOf = null)
        {
            string weaponName = null;
            if (item.category == ItemCategory.WeaponSkin && !string.IsNullOrEmpty(item.weaponId))
            {
                var gun = Content.ContentDatabase.Get<Weapons.WeaponData>(item.weaponId);
                if (gun != null) weaponName = gun.DisplayName;
            }
            return new EnvelopeShow
            {
                mode = EnvelopeMode.Reveal,
                serial = ticket.serial,
                antesCleared = RunEnvelope.UnlockAnte,
                prompt = "Press " + SouthButton() + ", Enter or click to continue",
                rarity = prize.rarity,
                cardTexture = "Card",
                itemName = item.displayName,
                kindLine = ItemLook.KindLine(item, prize.rarity, weaponName),
                blurb = item.blurb,
                upLine = item.category == ItemCategory.Upgrade ? ItemLook.UpgradeUpLine(item) : "",
                downLine = item.category == ItemCategory.Upgrade ? ItemLook.UpgradeDownLine(item) : "",
                effectName = effect != null ? effect.displayName : "",
                effectColour = effect?.colour,
                ownedCount = owned != null ? owned.count : 1,
                fromTest = ticket.fromTest,
                useLine = Locker.RevealWords(item, inUse, weaponName, inPlaceOf),
                inUse = inUse && Locker.Usable(item),
                effectNote = effect != null ? Locker.EffectWhere(item) : "",
                effectId = effect != null ? effect.id : "",
            };
        }

        /// <summary>Keeps a test envelope (editor and development builds), to try the reveal before the ante loop exists.</summary>
        public void WinTestEnvelope()
        {
            if (!TestsAllowed) return;
            Book().KeepTestEnvelope();
            SaveSystem.Save();
        }

        /// <summary>
        /// Puts the next Celestial effect to use on test items (an every-gun skin, a ship paint, a suit piece), so each
        /// effect can be checked in a run (editor and development builds; <see cref="Locker.TryNextEffect"/>). Returns the
        /// effect's name, or "".
        /// </summary>
        public string TryNextEffect()
        {
            if (!TestsAllowed) return "";
            string id = Locker.TryNextEffect(SaveSystem.Data, Catalog(), ItemCatalog.CreateEffects());
            if (id.Length == 0) return "";
            SaveSystem.Save();
            ItemEffect effect = CelestialLooks.Effect(id);
            return effect != null ? effect.displayName : id;
        }

        public int ClearTests()
        {
            int gone = Book().ClearTests();
            // Test prizes that were in use come out of use with them.
            Locker.Tidy(SaveSystem.Data, LockerLook.Find);
            SaveSystem.Save();
            return gone;
        }

        /// <summary>Every prize item, from the item assets (built-in ones where an asset is missing).</summary>
        public static List<ItemSpec> Catalog()
        {
            var list = new List<ItemSpec>();
            foreach (ItemData data in Content.ContentDatabase.All<ItemData>()) list.Add(data.ToSpec());
            return list;
        }

        static EnvelopeTicket FindTicket(EnvelopeBook book, int serial)
        {
            foreach (EnvelopeTicket t in book.Unopened) if (t.serial == serial) return t;
            return null;
        }

        static string SouthButton()
        {
            return Glyphs.ForGamepadPath("<Gamepad>/buttonSouth", Glyphs.CurrentFamily).Text;
        }
    }
}
