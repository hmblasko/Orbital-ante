using System;
using System.Collections.Generic;
using OrbitalAnte.Ante;
using OrbitalAnte.Save;

namespace OrbitalAnte.Items
{
    /// <summary>Where one run's envelope stands.</summary>
    public enum EnvelopeState
    {
        /// <summary>Shown at the start of the run; cannot be opened.</summary>
        Sealed = 0,
        /// <summary>Ante 9 is cleared: the envelope is the player's and is kept in the save until opened.</summary>
        Unlocked = 1,
        /// <summary>The run ended before Ante 9 was cleared. Nothing is kept.</summary>
        Lost = 2,
    }

    /// <summary>An envelope that has been won but not opened yet. Kept in the save, so a crash cannot lose it.</summary>
    [Serializable]
    public sealed class EnvelopeTicket
    {
        public int serial;
        public bool fromTest;
    }

    /// <summary>
    /// The save's side of envelopes and the collection: numbering envelopes, keeping won ones, and opening them.
    /// Works on <see cref="SaveData"/> directly; the caller writes the save afterwards.
    /// </summary>
    public sealed class EnvelopeBook
    {
        readonly SaveData _data;

        public EnvelopeBook(SaveData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (_data.items == null) _data.items = new List<OwnedItem>();
            if (_data.unopenedEnvelopes == null) _data.unopenedEnvelopes = new List<EnvelopeTicket>();
        }

        public SaveData Data => _data;
        public ItemInventory Inventory => new ItemInventory(_data.items);
        public IReadOnlyList<EnvelopeTicket> Unopened => _data.unopenedEnvelopes;

        /// <summary>
        /// Gives the save its own salt the first time, so two players' envelopes differ. The salt is the one
        /// number allowed to come from the clock (like a run seed); everything after it is seeded.
        /// </summary>
        public void EnsureSalt(int fromClock)
        {
            if (_data.profileSalt != 0) return;
            _data.profileSalt = fromClock != 0 ? fromClock : 0x5EA1ED;
        }

        /// <summary>Numbers a new envelope for a run that is starting. Numbers are never reused.</summary>
        public int SealNext()
        {
            _data.envelopesSealed = Math.Max(0, _data.envelopesSealed) + 1;
            return _data.envelopesSealed;
        }

        public bool HasTicket(int serial)
        {
            for (int i = 0; i < _data.unopenedEnvelopes.Count; i++)
                if (_data.unopenedEnvelopes[i].serial == serial) return true;
            return false;
        }

        /// <summary>Keeps a won envelope. Asking twice for the same number keeps it once.</summary>
        public EnvelopeTicket Keep(int serial, bool fromTest)
        {
            for (int i = 0; i < _data.unopenedEnvelopes.Count; i++)
                if (_data.unopenedEnvelopes[i].serial == serial) return _data.unopenedEnvelopes[i];
            var ticket = new EnvelopeTicket { serial = serial, fromTest = fromTest };
            _data.unopenedEnvelopes.Add(ticket);
            return ticket;
        }

        /// <summary>A test envelope (editor and development builds): numbered like any other, marked as a test.</summary>
        public EnvelopeTicket KeepTestEnvelope() => Keep(SealNext(), true);

        /// <summary>
        /// The prize inside a kept envelope, without opening it. It is fixed by the salt, the envelope's number and
        /// what the player owns, so asking again gives the same answer.
        /// </summary>
        public Prize Peek(EnvelopeTicket ticket, PrizeOdds odds, IReadOnlyList<ItemSpec> catalog, IReadOnlyList<ItemEffect> effects)
        {
            if (ticket == null) return default;
            return PrizeRoll.Roll(PrizeRoll.StreamFor(_data.profileSalt, ticket.serial), odds ?? new PrizeOdds(), catalog, effects, Inventory);
        }

        /// <summary>
        /// Opens a kept envelope: rolls the prize, puts it in the collection and takes the envelope away, all at
        /// once, before any animation plays, so quitting halfway through the reveal still keeps the prize.
        /// Returns an invalid prize if the ticket is not kept or the catalog is empty.
        /// </summary>
        public Prize Open(EnvelopeTicket ticket, PrizeOdds odds, IReadOnlyList<ItemSpec> catalog, IReadOnlyList<ItemEffect> effects)
        {
            if (ticket == null || !_data.unopenedEnvelopes.Contains(ticket)) return default;
            Prize prize = Peek(ticket, odds, catalog, effects);
            if (!prize.IsValid) return prize;
            Inventory.Add(prize, ticket.serial, ticket.fromTest);
            _data.unopenedEnvelopes.Remove(ticket);
            _data.envelopesOpened++;
            return prize;
        }

        /// <summary>Clears test items and test envelopes. Returns how many collection lines went.</summary>
        public int ClearTests()
        {
            _data.unopenedEnvelopes.RemoveAll(t => t.fromTest);
            return Inventory.RemoveTestItems();
        }
    }

    /// <summary>
    /// One run's envelope. Sealed when the run starts; unlocked when Ante 9 is cleared; lost if the run ends first.
    /// The ante loop (Milestone 14) calls <see cref="OnAnteCleared"/>, or hands its director to <see cref="Follow"/>.
    /// </summary>
    public sealed class RunEnvelope
    {
        /// <summary>Matthew, 16 Nov 2026: "The envelope cannot be opened unless they beat Ante 9."</summary>
        public const int UnlockAnte = 9;

        public int Serial { get; }
        public EnvelopeState State { get; private set; }
        /// <summary>The highest ante cleared this run, for the nine stars around the seal.</summary>
        public int AntesCleared { get; private set; }

        /// <summary>Raised once, when the envelope unlocks.</summary>
        public event Action<RunEnvelope> Unlocked;

        RunEnvelope(int serial)
        {
            Serial = serial;
            State = EnvelopeState.Sealed;
        }

        /// <summary>Seals a new envelope for a run that is starting.</summary>
        public static RunEnvelope Seal(EnvelopeBook book) => new RunEnvelope(book.SealNext());

        /// <summary>
        /// An ante was cleared. Clearing Ante 9 (or beyond, if more antes are ever added) unlocks the envelope and
        /// keeps it in the book. Returns true the one time it unlocks. <paramref name="fromTest"/> marks it as a test
        /// envelope (the F8 shortcut in development builds, before the ante loop exists).
        /// </summary>
        public bool OnAnteCleared(int ante, EnvelopeBook book, bool fromTest = false)
        {
            if (ante > AntesCleared) AntesCleared = Math.Min(ante, UnlockAnte);
            if (State != EnvelopeState.Sealed || ante < UnlockAnte) return false;
            State = EnvelopeState.Unlocked;
            book?.Keep(Serial, fromTest);
            Unlocked?.Invoke(this);
            return true;
        }

        /// <summary>The run ended (death, quitting, a restart). A sealed envelope is lost; a won one stays won.</summary>
        public void OnRunEnded()
        {
            if (State == EnvelopeState.Sealed) State = EnvelopeState.Lost;
        }

        /// <summary>
        /// Follows an ante director: an ante counts as cleared when the run moves to the upgrade screen after it, or
        /// to victory after the last one. Returns the action to remove with <c>director.Changed -= ...</c>.
        /// </summary>
        public Action Follow(AnteDirector director, EnvelopeBook book)
        {
            Action handler = () =>
            {
                if (director.Phase == AntePhase.Intermission || director.Phase == AntePhase.Victory) OnAnteCleared(director.Ante, book);
            };
            director.Changed += handler;
            return handler;
        }
    }
}
