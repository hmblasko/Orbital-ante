# HUD clean-up and Run Stats (17 Nov 2026)

Matthew asked: "the current version of the game has a bunch of stats on the screen and white text. I'd like you to move some of the stats to maybe a menu on the pause screen and then have some stats be in [the] HUD ... determine what makes the most sense ... do some research before to identify what makes a HUD aesthetically pleasing."

Nothing here has been run in Unity. The pictures are offline mockups drawn with the game's font and colours (`tools/hud_mockups/hud_cleanup_mockups.py`), not screenshots.

## What was on screen

In the solar game, with nothing pressed:
- The F3 debug readout was **on by default**: six or more lines of cream text in the top left (frame times, seed, gib and blood counts, the Deck budget, picture finish, far copies, which body the world is anchored to, ground tiles, ship hatch and belt, thrust and turning, the rover).
- In the seat, a block of six lines in the lower left (speed, pull, the orbit aid's two lines, the lock line, wind), the world's name and height as one orange line at the top, and the whole list of flying keys along the bottom, always.
- The ship's hurt parts as a list of "Name: 54%" lines in the top right.
- On foot, the gun ladder as a sentence above the Focus meter ("GUN 2 OF 6   3 KILLS TO SHOTGUN   +1 GUN (1 / D-Pad Up: SWITCH)"), and no ammo readout at all outside the arena.
- In the arena, its list of keys along the bottom whenever the debug readout was on.

## What the research says

Short version of what makes a HUD look good and work:
1. **Show only what is acted on in the next few seconds; the rest on demand.** Every guide agrees: put critical information where the eye already is, push the rest to the edges or a menu, and show rarely needed things only when relevant ([Returnal's UX team](https://blog.playstation.com/?p=348960); [Bugnet: readable HUD](https://bugnet.io/blog/how-to-design-a-readable-hud)). Three or four things at once is a good ceiling ([The Diegetic Dilemma](https://indieklem.substack.com/p/19-the-diegetic-dilemma-benefits)).
2. **Placement beats decoration.** In a controlled study, an ammo count placed near the line of sight cut wasted shots by about 35% and was the favourite of 70% of players; the authors put that down to where it sat, not its style ([Peacocke, Teather, Carette, MacKenzie, IEEE GEM 2015](https://www.yorku.ca/mack/ieee_gem2015.html)).
3. **Grouping and hierarchy.** Size, position and contrast tell the eye what matters: one big number per group, small muted words under it ([Bugnet](https://bugnet.io/blog/how-to-design-a-readable-hud)). Metroid Prime keeps to core information in its visor and brings boss information in only when relevant ([UI breakdown: Metroid Prime](https://medium.com/the-space-ape-games-experience/ui-breakdown-metroid-prime-c22cdfbce0bc)).
4. **A restricted palette with colour meaning something.** Returnal uses one muted main colour and colour-codes health only ([Returnal](https://blog.playstation.com/?p=348960)). Here: cream for values, muted for words, amber for "look at this", red only when something is wrong, blue for air and the locked target.
5. **Readable on the Deck.** Valve's Deck Verified rule is no character under 9 px at 1280x800, with 12 px recommended ([Steamworks: Deck compatibility](https://partner.steamgames.com/doc/steamdeck/compat?l=german)). The smallest HUD text now is 12 px; most labels are 13 to 15.
6. **Diegetic where it fits, not everywhere.** The suit's visor arcs (chosen 15 Nov) and the cockpit's own instruments already carry air and fuel in the world; the rest stays as a light overlay with a soft halo, never boxes of text.

## The split

**Stays on the HUD** (each only while it applies):

| Place | Seat | On foot |
|---|---|---|
| Top middle | the world's name (small amber), height (big), "above the ground" (small) | |
| Lower left | SPEED, big number, what it is against; climb or sink rate below 2 km (red with a "COMING DOWN TOO FAST" notice at the same moment as the dash's warning); the orbit aid as a bar with a notch where a circle's speed is, only while orbiting is what you are doing; one quiet line for wind, thick air or water | air arc, jets bar, speed, tether (unchanged) |
| Lower right | LOCKED / AIMED / NEAREST tag, the target's name, its distance (big), closing speed (red when you must brake) | suit plates (moved 80 px in), then the gun: name, +N carried, ladder pips and the next gun, magazine (big) over its size |
| Above the bottom middle | one notice, most urgent first (unchanged) | Focus meter, one notice |
| Top right | the ship's hurt parts as bars for 6 s after any change, then only what no longer works plus "N parts worn"; a "Thrust 60%  Turning 92%" line while damage costs anything | same |

Colours: labels are muted, values cream, amber means "act soon", red "act now", blue is air and the locked target. Notices that ask for an action name its key ("hold Match Velocity (F)"), so nothing depends on the faded key list.

**Moved to the pause menu's new Run Stats page:** the whole F3 readout (off by default now, small on a dark plate when on; toggle with F3 or the button on Run Stats; remembered), the pull in m/s², hurt parts' per cents, ship fuel and air in numbers, thrust and turning per cents, the suit's air time and jet fuel in numbers, guns carried and the Switch Gun keys, time this run, kills, seed, time played, and a "Technical details" view with every debug line.

**Fades:** the list of flying keys and the arena's list of keys show for 12 s, then fade over 1.5 s (`HudRules.HelpAlpha`). The full list is always in Controls.

## What changed in the code

- `UI/HudRules.cs` (plain C#, tested): `StatsFormat` (commas, per cents that never say 100% early, run clock, time played, speed, distance split into number and unit), `HudRules` (key-list fade, when the orbit aid shows and where on its bar, ladder pips or words, a ship part's bar or word), `StatsSheet` (three columns, rows cut to fit with "and N more").
- `UI/StatsPage.cs`: the Run Stats page; `Core/RunStatsReport.cs` fills it; `PauseMenu` gains the Run Stats button after Resume; `GameBootstrap` keeps the run's time.
- `UI/Hud.cs`: performance overlay off by default and restyled; the gun group in the lower right for both worlds, with the ladder as pips; `DebugText()` for the overlay and Technical details.
- `UI/FlightHud.cs`: the seat view rebuilt as the four groups above.
- `Ship/ShipHud.cs`: hurt parts as pooled rows with bars.
- `Combat/GunLadderFx.cs`: its line above Focus is gone (now in the gun group); banner and rare-gun note unchanged.
- `Melee/MeleeHud.cs`: the held item's line and heat bar moved to the lower right above the gun, out of the suit notice's place.
- `Spacewalk/SuitHud.cs`: the suit plates sit 380 in from the right (as in the arena) to make room for the gun.
- `Core/FeelSettings.cs`: `PerformanceOverlay`.

Constitution: 7 (manage cognitive load) first; 5 (legible risk/reward) and 3 (no hidden rules), since nothing was removed from the game, only moved to a page that is one button away, and every hurt part still shows on screen.

## Agent review

Two reviews ran before committing (Opus, as the Fable agents are rate limited until 19 Nov).
- Design (game-designer role): the split is right; seven changes were taken: sink rate and a landing warning on the HUD, keys in notices, the thrust and turning cost in the ship panel, amber kept for warnings only, "+2 guns [key]" instead of a bare "+2", the ship panel shrinking after 6 s, and the performance numbers moved behind Technical details. Not taken: hiding the on-foot speed and tether line on the ground (unchanged suit readout from 15 Nov; worth a look after a playtest), bringing the key list back after 20 s idle, low ship fuel and air in the seat's notice (the console's lamps and dash already say it).
- Code and Deck cost (unity-reviewer and deck-performance roles): no null references or dead ends found. Fixed: the arena's prompt and aim text no longer build words in the solar game, the drift marker moved to the markers' canvas so the readouts are not re-batched every frame, the melee line lifted above the suit arc, Run Stats rows that pushed Seed off the page, values overlapping their labels, long gun names wrapping, the overlay button going stale after F3, a width measured every refresh in the ship panel. Left: about 80 short strings a second from the seat view's words (small).

## Tested

- Rule tests: `HudRulesTests` (number words, fade, orbit aid, ladder, ship parts, sheet columns). Full `dotnet test tools/rules-tests` results are in the PR.
- All runtime scripts compile against the UnityEngine 2021.3 reference assemblies (only the known IHittable name clash with that library).

## Unverified (needs Unity)

- Everything's look and position on the real screen, especially the seat view's lower corners over the console, and the target group's number and unit lining up.
- Run Stats page: controller navigation between its three buttons placed without a layout group; the numbers it shows in every situation (seated, aboard, outside, rover, arena).
- That turning the overlay off by default does not hide anything a playtest needs: performance numbers are now in Run Stats, and F3 still shows the overlay.
