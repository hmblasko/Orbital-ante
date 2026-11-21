using System;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The perks that answer kills (20 Nov 2026): heals, rounds back in the magazine, ladder kills that count
    /// twice, Focus from melee kills. Lives on the game root next to Focus and the gun ladder, and reads the
    /// same hit events they do.
    /// </summary>
    public sealed class RunPerksFx : MonoBehaviour
    {
        int _kills;

        /// <summary>The weapons of the run's player, to put rounds back (set by the game root).</summary>
        [NonSerialized] public PlayerWeapons Weapons;

        /// <summary>A short line for the readout when a perk does something worth seeing ("+15 HEALTH").</summary>
        public static event Action<string> Note;

        public static void ResetStatics() => Note = null;

        void OnEnable() => CombatEvents.PlayerHitSomething += OnPlayerHit;
        void OnDisable() => CombatEvents.PlayerHitSomething -= OnPlayerHit;

        public void ResetForNewRun() => _kills = 0;

        void OnPlayerHit(HitOutcome outcome)
        {
            RunMods mods = RunEffects.Mods;
            if (mods == RunMods.Neutral || !outcome.Enemy || !outcome.Killed) return;
            _kills++;
            bool melee = outcome.Source == HitSource.Melee, splat = outcome.Source == HitSource.Rover;

            PlayerHealth player = RunEffects.Player;
            float heal = mods.HealFor(melee, splat, RunEffects.HealthShare);
            if (player != null && heal > 0f && player.Alive && player.Current < player.Max)
            {
                player.Heal(heal);
                Note?.Invoke("+" + Mathf.RoundToInt(heal) + " HEALTH");
            }

            if (mods.LadderDouble(_kills))
            {
                GunLadderFx ladder = GunLadderFx.Instance;
                if (ladder != null && ladder.CreditKills(1)) Note?.Invoke("LADDER KILL x2");
            }

            if (!melee && !splat && mods.RoundsOnKill > 0 && Weapons != null && Weapons.Current != null && Weapons.Current.State != null)
                Weapons.Current.State.AddRounds(mods.RoundsOnKill);

            if (melee && mods.FocusOnMeleeKill > 0f)
            {
                DeepBreathFx breath = DeepBreathFx.Instance;
                if (breath != null && breath.Breath != null) breath.Breath.Earn(mods.FocusOnMeleeKill);
            }
        }
    }
}
