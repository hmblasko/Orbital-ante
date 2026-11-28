namespace OrbitalAnte.Core
{
    /// <summary>
    /// What kind of place on a creature a hit landed on (Ralph loop pass 7, 28 Nov 2026: teach where to shoot each
    /// creature). Each creature says it for every hit (<c>Creatures.WhereToShoot</c>), and the hit marker and its sound
    /// say it back (<c>UI.HitConfirm</c>): armour that held gets a small grey X and a dull knock instead of the ding;
    /// the one place it dies from quickest gets the gold X and the higher ding a person's head gets; a claw, jaw or drum
    /// shot off gets the disarm's. People leave it <see cref="Plain"/>: their head says <c>HitOutcome.Head</c>, and a gun
    /// shot out of their hands says <c>HitOutcome.Disarmed</c>.
    /// </summary>
    public enum HitZone : byte
    {
        /// <summary>Anywhere else: a leg, a fin, a wing, a length of tail; and every hit on a person.</summary>
        Plain = 0,
        /// <summary>Armour that held: a lid, a skull, a plate. It knocks, does not bleed and is the slow way to kill it.</summary>
        Armour = 1,
        /// <summary>The one place it dies from quickest: a belly, an abdomen, a throat. A creature's head, as the marker sees it.</summary>
        Vital = 2,
        /// <summary>A piece it fights with came off: a claw, a jaw, a drum. Its strike is weaker or gone, like a disarm.</summary>
        WeaponOff = 3,
    }

    /// <summary>Rules about hit zones that more than one system asks.</summary>
    public static class HitZones
    {
        /// <summary>
        /// A kill through the place that kills quickest: a person's head still on its body, or a creature's weak place
        /// (Ralph loop pass 7). It gets the headshot kill's marker and chime (<c>UI.HitConfirm</c>) and, when it is
        /// switched on, the headshot slow-mo (<c>Core.TimeFx</c>), so the best way to kill each creature feels the best.
        /// </summary>
        public static bool BestKill(bool killed, bool headshotKill, HitZone zone) => killed && (headshotKill || zone == HitZone.Vital);
    }
}
