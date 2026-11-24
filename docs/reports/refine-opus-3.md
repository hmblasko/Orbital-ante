# Refinement pass 3 (24 Nov 2026)

Matthew, 24 Nov 2026: "Think deeply about how else you can refine the game and implement the improvements."

Built on `claude/playtest-refine` (PR 83, which folds PRs 78 to 82 into `claude/playtest`), so it merges after PR 83. **Nothing here was run in Unity or on the Deck.** No playtest results are claimed; the checklist at the end is how to get them.

## How the pass was chosen

I read the project notes for every earlier refinement (passes 1 and 2, the handmade-feel pass, the fun and playable-loop work, playtest pass 1) so nothing was redone, then walked the code for gaps against Matthew's standing direction (near-constant combat, stealth as a real choice, Outer Wilds zero-G, real inertia, overtime) and the open items those passes left. Five changes came out of it. Asset and model work was left alone (Fable's).

Things checked and left alone:

- **Hitstop by event size** (listed as not built in the fun report): it already scales, limb 50 ms, torso 70 ms, kill 80 ms, melee its own.
- **Hit reactions** (an idea from pass 1): enemies already flinch from every hit (`BodyAnimator.Flinch`), harder for a severed part.
- **Always Included Shaders before the first Deck build** (flagged in playtest pass 1): on a closer read this is mostly covered. `ProjectSetup` saves `OA_Lit`, `OA_Unlit`, `OA_VertexLit`, `OA_VertexUnlit`, `OA_Glass` and `OA_Decal` in `Resources/Materials`, and the game loads those before it ever calls `Shader.Find`, so the shaders are in the build. `Shader.Find` is only the fallback. Adding URP Lit to Always Included would compile every variant and make builds very slow, so I did not.

## What changed

### 1. Restless camps: a long quiet ends in a fight

**Problem.** Camps stand still until you come within 150 m. Land between camps, or clear the ones near the ship, and you can walk for minutes with nobody to fight. That works against "near-constant combat".

**Change** (`Enemies/LullRules.cs`, `EnemySpawner.Restless`, `MannequinCombat.SendToLook`; dials on the EnemyConfig asset under Lull):

- After **35 s** with no enemy aware of you while you are out on foot, the nearest squad still standing within 240 m is sent toward you. The quiet gets 6% shorter each ante and never drops below 20 s.
- They get a **place**, never a live track: a spot up to 15 m from where you were. They run until about 50 m from it, then come on low and careful, the same as a squad that heard a shot.
- If nobody is standing near, one ordinary camp may wake from up to 200 m instead of 150 m. It is left alone for **12 s** first, so you can see it and choose to sneak round it or take it on. It is the only camp woken for that lull.
- **Hiding stops the clock.** Crouched, prone, sneaking or in a dark cave, the clock stands still (it does not reset). Driving stops it too. Going aboard resets it.
- **Guards keep their posts.** Base and cave guards are never called, so bases stay full.

Constitution 1 (fun core) and 8 (varied intensity: the 35 s lull is kept on purpose). Stealth stays a real choice (2).

### 2. Guns push you back in zero-G

**Problem.** A melee hit already pushed you back while floating free, but a gun did not, so shooting in zero-G felt like standing on a floor. Part B says "real space inertia throughout".

**Change** (`Player/ZeroGKick.cs`, `PlayerWeapons.PushShooter`, `Astronaut`; dials on the PlayerConfig asset under Zero G Kick):

- Only while floating free, each shot pushes the suit straight back along the line of fire. The push is the gun's own kick times 0.08 m/s per degree, at most 0.4 m/s a shot.
  - Sniper: about 0.28 m/s a shot.
  - Shotgun: about 0.24.
  - Pistol: about 0.12.
  - Held AK rifle or SMG: about 0.6 m/s for each second of fire.
- Shooting never adds more than 3 m/s of drift. Match Velocity takes the push off like any other drift. The view is never turned. On the ground and aboard nothing changes.

Constitution 1 and Part B section 4. Milestone 11: zero-G should feel "free, precise and a little scary".

### 3. The stakes pay only while the clock runs

**Problem.** Overtime made a hazard close to free. Short Fuse's lost minute cost almost nothing once a missed clock just meant overtime, and every overtime point still paid x1.20. The fun pass's own review left this open.

**Change** (`HazardRules.PaysNow`, `AnteLoop`):

- A hazard is a bet on the clock. Once the ante is in overtime its bonus stops and points pay x1.00. The hazard itself stays to the end of the ante.
- The words say so in three places:
  - The stakes page: "Its points bonus lasts while the ante clock runs. In overtime the hazard stays, but points pay x1.00", and each row reads "points x1.20 on the clock".
  - The ante-start note: "... POINTS x1.20 UNTIL THE CLOCK RUNS OUT".
  - The overtime note: "SHORT FUSE STILL ON, BONUS OVER".
- `tools/run-sim` follows the rule. In the simulator the difference is small, because its made-up player scores little in overtime: Short Fuse's mean run score was 48,508 in the earlier 1,000-run table and is 47,520 now, against 48,438 with no hazard (300 runs; made-up players, not a playtest).

Constitution 2 (meaningful trade-offs) and 5 (legible risk and reward).

### 4. Leaving the game pauses it

**Problem.** Alt-tabbing, opening the Steam overlay, or the Deck going to sleep left the fight running, so you could come back dead.

**Change** (`PauseMenu.OnApplicationFocus`, `OnApplicationPause`): in a built game, losing focus opens the pause menu (and frees the mouse). It never opens over a page that already pauses (the break, star chart, death page) and never in the editor, where clicking the Inspector would pause every time. In Gaming Mode the Steam button may not take focus from the game; that needs checking on the Deck.

Constitution 9 (no lost progress from a moment away).

### 5. Plain pause pages draw at half rate

**Problem.** The world under the pause menu is frozen but was still drawn 60 times a second, which costs the Deck's fan and battery.

**Change** (`PerformanceBudget.halfRateWhenPaused`, `PauseMenu.Show`, `PerfMonitor`): on the plain pause pages (menu, controls, feel, audio, restart, quit, confirm) only every second frame is drawn (`OnDemandRendering`). Input is still read every frame, so menus answer as fast as before. The pages meant to look good (the ante break and its choices, the star chart, the death recap) keep full rate. The frame readout leaves out the 2 frames after a pause. Untick Half Rate When Paused on the PerformanceBudget asset to compare.

## Agent review

Three agents read the change before commit (opus, since Fable is limited until 26 Nov). What they found, and what was done about it:

**unity-reviewer**
- **Hiding check.** The first version used the visibility number, so standing still counted as hiding and sneaking did not. It now uses `Astronaut.Hiding`, which is the stance, the sneak flag and cave darkness.
- **Waking chain.** A lull could wake camp after camp: a woken squad could measure past the call reach, so the next think woke another. Now the lull's camp is remembered and called whatever its distance, and nothing else wakes meanwhile. The call reach was also raised from 210 m to 240 m, and a rule test holds it.
- **Cave squads.** A cave squad could be sent to the surface. Cave and base guards are now never called.
- **Running too long.** `_sentUntil` outlived the place it was sent to, so a later ordinary sound made the enemy run. It is now cleared with the place.
- **Nothing else.** No compile or crash problems were found. The focus-loss pause cannot cover the break, star chart, death page or rebinding.

**game-designer**
- **Hiding check.** It found the same hiding problem.
- **Wake and charge.** A camp woken after a long quiet charged at once, before you could see it. It now gets the 12 s grace.
- **Lull too urgent.** A lull squad ran all the way, while overtime hunters walk. It now runs only until it is near, then comes on carefully, and goes to a spot near you rather than your exact place.
- **Wording.** The stakes text was unclear. It was rewritten as above.
- **Zero-G numbers.** The push was too gentle (and my brief had the rifle's kick wrong: it is 0.75, not 1.5). It went from 0.06 to 0.08 per degree, capped at 0.4.
- **Half rate.** The star chart and break should keep full rate. They do.
- **Guards.** Base guards should not be pulled away. They are not.
- **Left open.** A tell when a squad is sent (a distant radio call or flare), and whether overtime hunters should run. See below.

**deck-performance**
- **Pause rate method.** `vSyncCount = 2` may be ignored under gamescope and slows menu input. It was replaced with `OnDemandRendering`, which keeps input at full rate.
- **Rover case.** In the rover a far camp could wake and be put back to sleep at once. The clock now stands still while driving.
- **Per-frame cost.** Restless and the zero-G push add no per-frame allocation. Lines were added to `docs/performance_budget.md`.

## Tested

- Runtime compile check against Unity reference assemblies: clean, apart from the two known `excludeLayers` lines that exist in Unity 6 but not in the 2021 reference.
- Rule tests: 2,688 passed, 1 skipped (the cabin bake check, as before). 13 are new, in `RefinePassThreeTests`: the quiet clock, the grace, hiding, the ante shortening, the reach relations, the zero-G push and its drift cap, and hazards paying only on the clock.
- Run simulator rerun for the stakes table (numbers above).

## Not verified (needs Unity)

Everything about feel: how often a squad shows up, whether 35 s is right, whether the zero-G push feels good or annoying, whether the half rate is noticeable, and whether focus loss fires on the Deck.

## Open for Matthew

- Should a squad sent by a long quiet give a **tell**? For example a distant radio call from its direction, or a Scout's flare. That would let players learn the rule that standing in the open draws patrols. It needs a new sound.
- Should **overtime hunters run** to you (they walk now), so overtime is clearly sharper than a lull?

## Deck checklist (about 15 minutes)

Get the build first:

1. Open Konsole in Desktop Mode.
2. `cd ~/Orbital-ante`
3. `git fetch`
4. `git checkout claude/playtest`
5. `git pull`
6. Open the project in Unity Hub and press Play.

Then:

1. Land on a world and clear the enemies near the ship, or land well away from any. Stand or walk upright in the open. Within about a minute, does a squad come looking (running, then careful)?
2. Do the same but crouched or prone the whole time. Nobody should be sent.
3. Fly out until you float free (no "Gravity has you"), then fire the sniper once. Do you drift back? Hold the rifle's trigger. Does Match Velocity stop the drift?
4. At an ante break, take Short Fuse. Let the clock run out. Does the overtime note say "SHORT FUSE STILL ON, BONUS OVER", and do "+points" lines stop showing the bonus?
5. Press Esc. The menu should feel the same. Open Controls and move around in it: does anything feel slower?
6. In a built game (not the editor), alt-tab or press the Steam button mid-fight. Does the pause menu open?
