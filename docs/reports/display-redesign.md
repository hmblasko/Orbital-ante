# Seat display redesign (20 Nov 2026)

Matthew asked: "fix the stats appearing on the screen like they do, it needs to be more interesting. the screen display needs to be super interesting. do deep research on what makes a display interesting in a game and then implement. i don't want anything too complicated, fairly simple but that looks sharp and not like AI made it. don't use generic founts, make sure it is easily visible."

Research note and mockups (offline drawings at 1280x800, not game footage) are in the project files under `orbital-ante/hud/redesign/`, made by `tools/hud_mockups/hud_redesign_mockups.py`. Two options went to Matthew: A, flight tapes, and B, a dash strip with warning lamps. B was recommended and is what this branch builds.

## What changed

- **The seat readout is now a dash strip** (`FlightHud`): five cells along the dash, SPEED, HEIGHT, PULL, ORBIT and WIND. Each has a big number, a small unit and one short note (for example "VS MARS GROUND", "NEEDS 3.2 KM/S", "THICK AIR SLOWS YOU"). Each cell always sits in the same place. Speed and height roll toward the true value instead of jumping. The orbit cell has a small bar: it fills to your speed across the world, and its notch marks the speed a circle at this height needs. The low and high points show once you are in orbit.
- **Lamps instead of sentences:** BOOST, MATCH, LOCK, AUTO, the orbit's path (IN ORBIT, FALLING, LEAVING or TUGGED), PULL and BRAKE. A lamp flashes for 0.6 s as it comes on, then stays lit. Colour only means something: amber is watch this, red is act now, blue is your target, green is an orbit reached.
- **One advice line** above the lamps, most urgent first: autopilot words, why the autopilot let go, brake now, pull stronger than the thrusters, matching velocity, nearest the nose.
- **Top middle:** the world's name, big, with the height in words under it.
- **The aimed body's marker** carries its distance and closing speed.
- **The key list** is one line of caps. It fades after 10 s in the seat and comes back each time you sit down or close the map.
- **Ship damage** (`ShipHud`), top right: still lists every hurt part, now on a dark plate in the new lettering, coloured by how bad it is. The ship notice moved down to sit under the world's name.
- **The F3 debug readout is off by default** (`Hud`). F3 still shows it. The arena's key line no longer hides with it.
- **Fonts:** Saira Condensed Bold for numbers and Chivo Mono SemiBold for labels (`UiKit.NumberFont`, `UiKit.LabelFont`, `Resources/Fonts`, SIL OFL, both logged in the licence ledger). B612 is still the font everywhere else and the fallback.
- **Rules:** `UI/DashStrip.cs` (plain C#) handles number and unit splitting, the lamp flash, the orbit bar, the key fade and the number roll. It is tested in `DashStripTests`.

Constitution: 7 (manage cognitive load: glanceable numbers in fixed places, states as lamps) and 5 (legible risk: red only for act-now).

## Tested

- `DashStripTests` pass with `dotnet test tools/rules-tests`. The full rules suite was also run (see the PR).
- The runtime scripts compile against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam. The only errors are the known IHittable name clash from that library.

## Not verified (needs Unity on the Deck)

- Nothing has been run in Unity. Still unchecked: how the strip, lamps and fonts look, whether the TTFs import as fonts through `Resources.Load<Font>` (as B612 does), and where the unit lands after each number (it uses `Text.preferredWidth`).
- Whether the strip covers anything you need on the console from the seat. It sits 76 to 172 px up from the bottom of the screen, over the dash.
- The ante timer, score and streak are not drawn yet. The mockups leave room for them top left, and the ante tasks work will add them.

Playtest protocol (principle 10). Problem: the readout was six lines of sentences you had to read. Hypothesis: big numbers in fixed places, plus lamps that flash on, are read at a glance. Smallest test: from the pad, fly up, lock Phobos (or the Moon), let the autopilot run, then fly by hand toward a moon fast enough that BRAKE lights. Acceptance: speed, height and the BRAKE lamp can be read on the Deck without looking away from the window, nothing overlaps the crosshair or the notice, and no text is too small to read.

## Agent review

An Opus reviewer read the change, following the unity-reviewer and deck-performance briefs. These were fixed:

- **Target numbers no longer vanish.** The target's distance and closing speed could disappear when its marker was hidden (the body too big, off screen, or out of the marked set). They now move to a fixed blue line above the advice.
- **The damage plate fits its text.** It is wider (360 px, 14 px text) and its height comes from the text's own height, so a long line like "FRONT RIGHT THRUSTERS BROKEN, LOOSE" no longer runs off the screen.
- **Units no longer jiggle.** Numbers are right-aligned in a fixed box and the unit has a fixed place, sitting on the baseline. Before, rolling digits nudged the unit sideways.
- **No strings built every frame.** Rolling numbers only build text when the shown digits change (`DashStrip.SpeedKey` / `DistanceKey`, tested). Key names are looked up every 2 s and reused by the advice line.
- **Nothing reads falling or heavy while landed.** On the ground the orbit cell says LANDED, and the orbit and pull lamps stay dark.
- **"Matching velocity" comes before "brake now"** in the advice line again, as in the old display.
- **Pull and thrust numbers** use the invariant culture.
- **The key line** fades every frame and is switched off once it has faded.
- **The drift marker** moved to the markers' canvas, so its movement does not rebuild the readings' canvas.

Not done:

- The strip has no nested canvas of its own yet. Rolling numbers and flashing lamps still rebuild the FlightHUD canvas while they change. Check `Canvas.BuildBatch` in the Profiler.
- If `CockpitConfig.placement` is set to the on-screen dash rather than the default OnDesk, that dash covers the strip. With OnDesk, the strip may sit over the 3D desk panel when you glance down. Both need a look in Unity.
