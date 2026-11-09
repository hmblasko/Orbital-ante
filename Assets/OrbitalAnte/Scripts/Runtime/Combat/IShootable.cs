namespace OrbitalAnte.Combat
{
    /// <summary>Anything a bullet can hit and react to (body parts, weapons).</summary>
    public interface IShootable
    {
        HitResult TakeHit(in HitInfo hit);
    }

    /// <summary>Something that can hold a weapon and react when that weapon is shot.</summary>
    public interface IWeaponHolder
    {
        /// <summary>Called when the held weapon itself is hit. Return true if it was knocked away.</summary>
        bool OnWeaponShot(Weapon weapon, in HitInfo hit);
        /// <summary>How many hands can work the weapon right now (0, 1 or 2).</summary>
        int HandsOnWeapon { get; }
    }
}
