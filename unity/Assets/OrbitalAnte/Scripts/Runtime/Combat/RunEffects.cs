using System;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The one place game systems ask what the run's build does (20 Nov 2026, roguelite fun pass): gun and melee
    /// damage, damage taken, walking speed, Focus gain, the long-shot distance. The ante loop publishes a new
    /// <see cref="RunMods"/> whenever the build changes; outside a solar-system run everything is neutral, so the
    /// arena and tests are untouched. Damage taken is only changed for the run's own player.
    /// </summary>
    public static class RunEffects
    {
        public static RunMods Mods { get; private set; } = RunMods.Neutral;

        /// <summary>The run's player (the suit's armour), or null.</summary>
        public static PlayerHealth Player { get; private set; }

        static Func<bool> _lowStance;
        static Spacewalk.SuitSupplies _suit;
        static Cockpit.ShipSupplies _ship;

        /// <summary>Raised after <see cref="Mods"/> changes.</summary>
        public static event Action Changed;

        public static void Publish(RunMods mods)
        {
            Mods = mods ?? RunMods.Neutral;
            ApplyToPlayer();
            Changed?.Invoke();
        }

        /// <summary>The solar-system run's player has been built (and its ship's tanks, which a Locker upgrade can change).</summary>
        public static void Attach(PlayerHealth armour, Func<bool> lowStance, Spacewalk.SuitSupplies suit = null, Cockpit.ShipSupplies ship = null)
        {
            Player = armour;
            _lowStance = lowStance;
            _suit = suit;
            _ship = ship;
            ApplyToPlayer();
        }

        public static void Clear()
        {
            if (Player != null) Player.SetBuild(1f, 1f, false);
            if (_suit != null) { _suit.AirScale = 1f; _suit.FuelScale = 1f; }
            if (_ship != null) _ship.FuelScale = 1f;
            Player = null;
            _lowStance = null;
            _suit = null;
            _ship = null;
            Mods = RunMods.Neutral;
            Changed?.Invoke();
        }

        public static void ResetStatics()
        {
            Player = null;
            _lowStance = null;
            _suit = null;
            _ship = null;
            Mods = RunMods.Neutral;
            Changed = null;
        }

        static void ApplyToPlayer()
        {
            if (Player != null) Player.SetBuild(Mods.MaxHealth, Mods.Regen, Mods.NoRegen);
            if (_suit != null)
            {
                _suit.AirScale = Mods.Oxygen;
                _suit.FuelScale = Mods.JetFuel;
            }
            if (_ship != null) _ship.FuelScale = Mods.ShipFuel;
        }

        /// <summary>How many times longer the player's reloads take (a Locker upgrade's; 1 outside a run).</summary>
        public static float ReloadTime => Mods.ReloadTime;

        public static float HealthShare => Player != null ? Player.Fraction : 1f;

        /// <summary>Multiplier for one of the player's gun shots.</summary>
        public static float GunShot()
        {
            if (Mods == RunMods.Neutral) return 1f;
            bool low = false;
            try { low = _lowStance != null && _lowStance(); }
            catch (Exception) { low = false; }
            return Mods.GunShot(low, HealthShare);
        }

        /// <summary>Multiplier for one of the player's melee blows.</summary>
        public static float MeleeBlow() => Mods == RunMods.Neutral ? 1f : Mods.MeleeBlow(HealthShare);

        /// <summary>What an enemy shot that would do <paramref name="damage"/> does to <paramref name="target"/>.</summary>
        public static float Taken(PlayerHealth target, float damage, float distance)
        {
            if (target == null || target != Player || Mods == RunMods.Neutral) return damage;
            return Mods.Taken(damage, distance);
        }
    }
}
