using System.Text;
using OrbitalAnte.Combat;
using OrbitalAnte.Ship;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.UI;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Fills the pause menu's Run Stats page (<see cref="StatsSheet"/>) from the running game: what used to crowd
    /// the screen (the F3 readout, the pull, the per cents of every hurt part, the gun ladder's words) plus a few
    /// numbers that never had a place (time this run, kills, time played). Called only when the page opens.
    /// </summary>
    public static class RunStatsReport
    {
        public static StatsSheet Build(double runSeconds, Space.SolarSystemWorld space, Arena.TestArena arena,
            DeepBreathFx focus, Hud hud)
        {
            var sheet = new StatsSheet();
            Player.PlayerWeapons weapons = space != null && space.Astronaut != null ? space.Astronaut.Weapons
                : arena != null ? arena.Weapons : null;

            AddRun(sheet.Section("THIS RUN"), runSeconds, weapons, focus);
            if (space != null)
            {
                if (space.Ship != null) AddShip(sheet.Section("SHIP"), space);
                AddSuitAndFlight(sheet.Section("SUIT AND FLIGHT"), space);
            }
            else if (arena != null && arena.Health != null)
            {
                sheet.Section("YOU")
                    .Add("Health", StatsFormat.Percent(arena.Health.Fraction), VisorReadout.SuitTone(arena.Health.Fraction) == GaugeTone.Alarm ? StatTone.Bad : StatTone.Plain)
                    .Add("Plates", VisorReadout.PlatesLeft(arena.Health.Fraction) + " of " + VisorReadout.Plates);
            }

            AddPerformance(sheet);
            sheet.Technical = hud != null ? hud.DebugText() : string.Empty;
            return sheet;
        }

        static void AddRun(StatSection s, double runSeconds, Player.PlayerWeapons weapons, DeepBreathFx focus)
        {
            s.Add("Time this run", StatsFormat.Clock(runSeconds));
            GunLadder ladder = GunLadderFx.Active;
            if (ladder != null)
            {
                s.Add("Kills", StatsFormat.Count(ladder.TotalKills));
                string gun = GunName(ladder.WeaponId);
                s.Add("Gun", (ladder.Rung + 1) + " of " + ladder.RungCount + "  " + gun);
                if (ladder.AtTop) s.Add("Next gun", "last gun", StatTone.Quiet);
                else
                {
                    int left = ladder.KillsLeft;
                    s.Add("Next gun", left + (left == 1 ? " kill to " : " kills to ") + GunName(ladder.NextWeaponId), left == 1 ? StatTone.Warn : StatTone.Plain);
                }
            }
            else if (weapons != null && weapons.Current != null)
            {
                s.Add("Gun", GunLadder.ShortName(weapons.Current.Data.DisplayName));
            }

            if (weapons != null && weapons.Carried.Count > 0)
            {
                // The first by name and a count of the rest, so the value fits its column. The switch key is on the HUD.
                Weapon first = weapons.Carried[0];
                string name = first != null && first.Data != null ? GunLadder.ShortName(first.Data.DisplayName) : "gun";
                int more = weapons.Carried.Count - 1;
                s.Add("Guns carried", more > 0 ? name + " +" + more : name);
            }

            if (focus != null && focus.Breath != null) s.Add("Focus", StatsFormat.Percent(focus.Breath.Meter));
            s.Add("Seed", GameSession.Seed.ToString());
            s.Add("Time played", StatsFormat.Played(Save.SaveSystem.Data.secondsPlayed + runSeconds), StatTone.Quiet);
        }

        static string GunName(string id)
        {
            WeaponData data = Content.ContentDatabase.Get<WeaponData>(id);
            return data != null ? GunLadder.ShortName(data.DisplayName) : id;
        }

        static void AddShip(StatSection s, Space.SolarSystemWorld space)
        {
            Ship.Ship ship = space.Ship;
            Cockpit.ShipSupplies supplies = space.Dash != null ? space.Dash.Supplies : null;
            if (supplies != null)
            {
                s.Add("Fuel", StatsFormat.Percent(supplies.Fuel), supplies.FuelOut ? StatTone.Bad : supplies.FuelLow ? StatTone.Warn : StatTone.Plain);
                s.Add("Air", StatsFormat.Percent(supplies.Oxygen), supplies.OxygenLow ? StatTone.Warn : StatTone.Plain);
            }
            ShipModel model = ship.Model;
            s.Add("Thrust", StatsFormat.Percent(model.ThrustFactor), model.ThrustFactor < 1f ? StatTone.Warn : StatTone.Plain);
            s.Add("Turning", StatsFormat.Percent(model.TurnFactor), model.TurnFactor < 1f ? StatTone.Warn : StatTone.Plain);
            int hurt = 0;
            for (int i = 0; i < model.Count; i++)
            {
                PartState state = model.State(i);
                if (state == PartState.Sound && !model.NeedsRepair(i)) continue;
                s.Rows.Add(HudRules.ShipPartStat(model.Parts[i].Name, state, model.Fraction(i), model.TearProgress(i)));
                hurt++;
            }
            if (!model.CabinIntact) s.Add("Cabin", "OPEN TO SPACE", StatTone.Bad);
            else if (hurt == 0) s.Add("Damage", "none", StatTone.Quiet);
        }

        static void AddSuitAndFlight(StatSection s, Space.SolarSystemWorld space)
        {
            Astronaut a = space.Astronaut;
            if (a != null && a.SuitTanks != null)
            {
                SuitSupplies tanks = a.SuitTanks;
                SuitConfig dials = a.SuitDials;
                string air = StatsFormat.Percent(tanks.Air);
                if (dials != null && !tanks.AirOut)
                {
                    int seconds = VisorReadout.AirSeconds(tanks.Air, dials.rules.airMinutes);
                    if (seconds >= 0) air += "  " + StatsFormat.Clock(seconds) + " left";
                }
                s.Add("Suit air", air, tanks.AirOut ? StatTone.Bad : tanks.AirLow ? StatTone.Warn : StatTone.Plain);
                s.Add("Jet fuel", StatsFormat.Percent(tanks.Fuel), tanks.FuelLow ? StatTone.Warn : StatTone.Plain);
            }
            if (a != null && a.Armour != null)
                s.Add("Armour", VisorReadout.PlatesLeft(a.Armour.Fraction) + " of " + VisorReadout.Plates + " plates");

            FlightHud flight = space.Hud;
            bool seated = a != null && a.Mode == AstronautMode.Seated;
            if (flight != null && seated)
            {
                s.Add("Near", flight.NearWords);
                if (flight.HeightWords.Length > 0) s.Add("Height", flight.HeightWords);
                s.Add("Speed", flight.SpeedWords);
                s.Add("Pull", flight.Pull.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s²");
                Navigation.OrbitReading orbit = flight.Orbit;
                if (orbit.Kind != Navigation.OrbitKind.None)
                    s.Add("Orbit", StatsFormat.Speed(orbit.SideSpeed) + " of " + StatsFormat.Speed(orbit.CircleSpeed), HudRules.OrbitTone(orbit.Kind));
            }
            else if (a != null)
            {
                SuitInputs suit = a.SuitState;
                s.Add("Where", a.Mode == AstronautMode.Outside ? "outside" : a.Mode == AstronautMode.Driving ? "driving the rover" : "aboard");
                if (a.Mode == AstronautMode.Outside)
                {
                    s.Add("Speed", StatsFormat.Speed(suit.Speed));
                    s.Add("Ship", StatsFormat.Distance(suit.ShipDistance) + (suit.Tethered ? " (tethered)" : string.Empty));
                }
            }
        }

        static void AddPerformance(StatsSheet sheet)
        {
            Perf.PerfMonitor perf = Perf.PerfMonitor.Instance;
            var words = System.Globalization.CultureInfo.InvariantCulture;
            if (perf != null && perf.Stats.Count > 0)
            {
                float average = perf.Stats.Average;
                sheet.Performance.Add(new StatCell(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, average)).ToString(words), "fps"));
                sheet.Performance.Add(new StatCell((average * 1000f).ToString("0.0", words), "ms average"));
                sheet.Performance.Add(new StatCell((perf.Stats.Percentile(0.99f) * 1000f).ToString("0.0", words), "ms 1% worst"));
                Perf.PerfVerdict verdict = perf.Verdict;
                string v = verdict == Perf.PerfVerdict.WithinBudget ? "OK" : verdict == Perf.PerfVerdict.Tight ? "TIGHT"
                    : verdict == Perf.PerfVerdict.Over ? "OVER" : "MEASURING";
                StatTone tone = verdict == Perf.PerfVerdict.Over ? StatTone.Bad : verdict == Perf.PerfVerdict.Tight ? StatTone.Warn : StatTone.Plain;
                sheet.Performance.Add(new StatCell(v, "Deck budget " + perf.Budget.frameBudgetMs.ToString("0.0", words) + " ms", tone));
            }
            else
            {
                sheet.Performance.Add(new StatCell("-", "not measured yet", StatTone.Quiet));
            }

            var line = new StringBuilder();
            var gibs = Dismemberment.GibManager.Instance;
            var gore = Gore.GoreFx.Instance;
            if (gibs != null && gore != null)
            {
                line.Append("gibs ").Append(gibs.ActiveCount).Append('/').Append(gibs.Config.maxActiveGibs)
                    .Append("   blood ").Append(gore.LiveDroplets).Append('/').Append(gore.Config.maxBloodParticles)
                    .Append("   splats ").Append(gore.Decals.LiveCount).Append('/').Append(gore.Config.maxDecals);
            }
            if (Look.PostFx.Instance != null) line.Append(line.Length > 0 ? "   " : "").Append("picture finish ").Append(Look.PostFx.Instance.Status);
            sheet.PerformanceLine = line.ToString();
        }
    }
}
