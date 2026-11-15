# Phase 1 playtest checklist — draft for the player

No results are recorded here. This is a proposed checklist for Fable/Claude to review, not a quality-gate verdict. Only the Critic scores real Unity captures; only Matthew signs off Phase 1. No Unity session or Steam Deck run was performed when writing it.

Sources: `CLAUDE.md` Part B section 15 and `docs/reports/milestone-1.md` through `milestone-6.md` on main `96264e13187659d90dba198703bba188721f4e23`. Ship checks also reference the pending Milestone 7 report/code on PR #6 at `b54972b2f8f9fd27402206252087dd6de841e246`. Main currently has no ship cabin/lift/seat, LookConfig, launch base, final cockpit, navigation autopilot, jetpack or proper enemy-art milestone. Do not interpret their planned cards as available controls.

Each card is a separate **2–9 minute play session**, after opening the reviewed build and loading its scene. The full checklist is not a ten-minute test. Stop a card at ten minutes; record incomplete access as **BLOCKED**, not yes or no. Later milestones need their actual build and setup steps supplied by Claude before their cards can be completed. This does not waive any refinement bar.

## Before the first card (once per reviewed build)

1. Ask Claude for the Linux Steam Deck build made from the source revision under review. Write down its branch/commit, build date and game seed. Do not assume main includes an open PR.
2. Launch that build on the Steam Deck. Keep the game at **1280×800**. Use the same display settings for comparison captures; record any reduced render scale or simple-shader fallback. For an editor preview only, the existing setup guide explains the Game view resolution dropdown; an editor-shaped image is not Deck evidence.
3. At the title screen, select **Combat test arena** or **Fly the solar system** as the card says. Use the controller first. If the controller cannot select a button, record that as a problem before using another input to continue.
4. Press **Start / Menu** to open Pause. Open **Controls**, choose **Controller**, and note the actual bindings. Instructions below assume defaults from `docs/SETUP.md`. If rebinding changed them, use the displayed binding instead. Keyboard equivalents are provided for clarity, not a requirement to connect a keyboard.
5. Record any Console/build error available to you and the readout's frame-time/budget information. Do not turn a green budget word into a quality verdict. Ask Claude to collect missing CPU/GPU data later; the performance target is a 16.67 ms frame at 60 fps, not a claim the build achieves it.
6. Use **Pause > Restart > Restart with the same seed** to repeat a card. To change scenario, use **Pause > Quit > Quit to main menu**, confirm **Yes**, then choose the scenario. Record controller actions that fail, misleading labels or lost selection.

For every card leave answers blank until you play: `Yes / No / BLOCKED`, with an optional timestamp or screenshot filename. Write what happened, not what you think the code does. A “No” is useful evidence for refinement, not permission to change the design or tuning.

## 1. Breakable characters — Milestone 1 (6 minutes, arena)

Principles: 1 fun core, 3 learnable challenge, 5 legible risk.

1. Choose **Combat test arena**. Enemies initially hold fire; use D-pad down (**H**) to return them to hold-fire if needed.
2. Aim at a dummy's hand, then forearm, then a fresh dummy's upper arm. Use short bursts and look at which pieces leave together. D-pad up (**T**) respawns the dummies.
3. On fresh dummies remove one leg, both legs, then arms as needed. Separately destroy the torso while leaving the head, then destroy the head.

- [ ] Can you tell which part you hit and which attached pieces left with it? ___
- [ ] Does loss of one leg, both legs, arms and torso produce distinct, understandable movement? ___
- [ ] Is a head-only living character distinguishable from a dead one? ___
- [ ] Are the limb changes quick and amusing rather than confusing or exhausting? ___

## 2. Gib feedback and comfort — Milestone 2 (5 minutes, arena)

Principles: 1 fun core, 7 cognitive load, 8 varied intensity.

1. Stay in the arena with enemies holding fire. Remove several limbs and watch the chunks land. Shoot a lower part still attached to a loose chain.
2. Keep the view still briefly to watch blood, splats, sound and chunks settling. Watch an early chunk long enough to see its eventual removal rather than spawning unlimited new ones.
3. Open **Pause > Feel** and try disabling then restoring hitstop, camera shake or headshot slow motion using the labels shown. Resume and compare the same shot.

- [ ] Do the sound, hit reaction and visible separation agree about the moment a limb comes off? ___
- [ ] Can a loose limb chain split further without an obvious visual glitch? ___
- [ ] Do chunks and effects clear rather than accumulating forever? ___
- [ ] Can you reduce uncomfortable effects and still understand the hit? ___
- [ ] Does repeated gibbing remain readable without an obvious pause or burst of frame stutter? ___

## 3. Weapons and disarming — Milestone 3 (7 minutes, arena)

Principles: 2 agency, 3 learnable challenge, 5 legible risk.

1. With the starting rifle, compare short taps with a held burst. Reload with X / Square (**R**) and observe the magazine/counter. Stop firing and watch recoil settle.
2. Approach the pistol near the start, use Y / Triangle (**E**) to pick it up, and check what happened to the rifle. Fire/reload the pistol, then swap back.
3. Shoot a held enemy weapon or gun hand. Watch the dropped gun and whether a remaining hand can recover/use it. For the player, note any one-hand/no-hand state encountered; do not invent a menu option or damage shortcut to force it.
4. Toggle enemy fire with D-pad down (**H**) for a short fight, then restore hold-fire.

- [ ] Are taps, long bursts and reloads clear and controllable on Deck inputs? ___
- [ ] Do shots and muzzle effects visibly start at the gun's bore? ___
- [ ] Does swapping leave you with one gun, with the other visibly dropped? ___
- [ ] Does a shot weapon/hand cause a readable disarm? ___
- [ ] When encountered, are one-hand penalties and no-hand refusal understandable? Mark BLOCKED if not encountered. ___
- [ ] Can you react to the first enemy shot rather than taking unexplained instant damage? ___

## 4. Foundation, save and controls — Milestone 4 (8 minutes)

Principles: 2 agency, 6 replayability, 7 cognitive load.

1. In the arena pause and note the seed. Choose **Restart > Restart with the same seed**; note starting positions. Repeat once. Then choose **Restart with a new seed** and record that seed separately.
2. Open **Controls > Controller**. Change one nonessential action to an unused button, resume briefly and try it. Return to Controls and use **Reset to defaults** for that device tab when done.
3. Try binding an action to a button already in use. Read the conflict choices and cancel. Keep a note of any changed binding, then restore defaults.
4. Quit to main menu, then quit/relaunch the build normally. Look for the most-recent-seed replay option and reopen the run. If you intentionally kept one binding changed, check it now, then restore it.

- [ ] Does same-seed restart reproduce the starting arrangement? This does not require identical physics after different player actions. ___
- [ ] Is a different seed clearly identified without claiming every visible item must move? ___
- [ ] Can you navigate, rebind, cancel a conflict and reset using the controller? ___
- [ ] Are saved replay choice and intentionally kept settings available after relaunch? ___
- [ ] Do pause, resume, restart and quit avoid accidental firing or lost menu selection? ___

## 5. Living solar system — Milestone 5 (7 minutes, solar system)

Principles: 3 learnable challenge, 4 discovery, 7 cognitive load.

1. Choose **Fly the solar system**. Open the live map with D-pad up (**M**). Look at the controls shown there; if an action has no usable controller binding, record it rather than silently switching inputs.
2. Close the map. Aim at a nearby body's label and observe the target brackets and closing-speed readout. Thrust sideways with the left stick, release, and watch the drift direction.
3. Aim at that body and hold A / Cross (**F**) to match velocity. Reopen the map briefly and compare motion. No long interplanetary transfer is needed for this card.

- [ ] Can you identify your target and approach/closing motion without guessing what a number means? ___
- [ ] Do release and counter-thrust convey inertia instead of unexplained braking? ___
- [ ] Does velocity matching make the relative motion easier to read? ___
- [ ] Does the map show a moving system and remain usable with Deck controls? ___
- [ ] Does the visible scale encourage exploring rather than feeling empty or unreadably tiny? ___

## 6. Planet recognition and landing — Milestone 6 (9 minutes per accessible body)

Principles: 4 discovery, 6 replayability, 7 cognitive load.

1. Start **Fly the solar system**. For the first card use the start near Earth. Use a provided save/build starting position for other bodies if Claude supplies one; there is no assumed teleport menu. Do not spend the entire card crossing the system. Mark an inaccessible body BLOCKED and ask Claude for a reproducible start in a later build.
2. Approach slowly, using the target closing-speed readout and match-velocity control. Observe ground detail appearing. Make one gentle landing and look around; do not force a high-speed collision to see a cave.
3. Record the body's identity before comparing its surface to a second accessible body. On a world with weather, observe any weather you actually encounter; on a gas giant, test the cloud crossing only from a suitable nearby start. On Neptune, test water only if reachable.

- [ ] Does the body look distinct from the other sampled world without reading its name? ___
- [ ] Is there a landform, shore or cave you want to approach after landing? ___
- [ ] Does approaching/landing avoid obvious ground gaps, fall-through, delayed-solid-ground surprises or a sustained freeze? ___
- [ ] Where encountered, are cloud, wind, low visibility and liquid effects signalled clearly? ___
- [ ] On an airless world, is the lack of an atmospheric sky apparent? ___
- [ ] For a suitable gas-giant/Neptune start, can you understand what is below the clouds or water and reach the intended ground/liquid? Otherwise BLOCKED. ___

Repeat as short separate cards across the available worlds; two worlds are a sample, not sign-off for every planet/moon.

## 7. A cosy, protectable ship — Milestone 7 (two cards, 8–9 minutes each; pending PR #6)

Principles: 2 agency, 3 learnable challenge, 5 legible risk, 7 cognitive load.

**A: cabin and entry, from a reviewed ship build**

1. Choose **Fly the solar system**. You should start seated in the lander on the ship branch. If you instead see the old test craft, stop this card as BLOCKED.
2. Open **Pause > Controls** and read bindings for Unbuckle, Use and Hatch. The ship report gives keyboard defaults **B**, **E**, **G**; use the actual displayed controller bindings. No invented controller shortcut is assumed here.
3. Unbuckle. Walk around the seat, lift well and lockers, and jump under the low roof. Look at the seat and Use it to rebuckle. Observe strap/sound and flight-control availability.
4. After a gentle landing, open the hatch and step onto the lift. Walk away below, then return under the ring. Separately, from orbit, try the hatch/lift only in the reviewed safe test conditions; the current milestone deliberately holds exit without nearby ground until jetpack work.

- [ ] Does the cabin feel correctly scaled, comfortable and worth protecting? ___
- [ ] Can you walk/jump without snagging, cutting through walls or losing a stable first-person view? ___
- [ ] Are buckle state, flight-control ownership and belt sound/animation understandable? ___
- [ ] Does belly exit/re-entry keep you clear of feet/engines and close the hatch behind you? ___
- [ ] Is an orbit exit refusal explained rather than looking like a broken button? ___

**B: damage and repair, from the same ship build**

1. Restart the same seed. Compare a gentle landing with one deliberately harder landing where you can safely continue. Record what you did; do not use assumed speed thresholds as a pass result.
2. Use the temporary test pistol, if enabled in the reviewed ship build, to damage a window from inside and outside. Count shots, observe the crack stages, then look at the damaged part and hold Use to repair.
3. Damage an eligible leg/engine/thruster until it is lost; distinguish its state from repairable Broken. Damage the dish and try the map. Restart afterward rather than expecting a lost part to return through repair.

- [ ] Can you tell Sound, Damaged, Broken and Lost apart from what is shown? ___
- [ ] Are crack position/progression, window break and repair readable from both sides? ___
- [ ] Is the difference between a repairable failure and permanent loss clear before repeated damage? ___
- [ ] Can you identify and reach a damaged part to repair without guessing or aiming through a wall? ___
- [ ] Is the map's dish/seat requirement explained when it refuses? ___

## 8–12. Later Phase 1 cards — setup blocked until those builds exist

The following are draft questions tied to the existing refinement bars, not new features. Claude must add exact build-specific setup and displayed controller bindings when each milestone lands. Do not substitute today's test craft for a final cockpit/jetpack/launch implementation or mark these yes from source code.

| Milestone / planned duration | Numbered setup to be completed in its real build | Yes/no questions (leave blank) | Principles |
|---|---|---|---|
| **8: Launch, 6 min** | 1. Open the supplied launch-base start. 2. Use its documented takeoff input and make one launch. 3. Restart the same start and repeat. Exact base/build/binding: BLOCKED. | Is the start understandable? Do smoke, sound, light and motion make launch exciting without hiding direction or causing discomfort? Is repeating it still enjoyable? ___ | 1 fun, 8 intensity |
| **9: Cockpit, 6 min per designer-supplied variant** | 1. Load the specified cockpit variant. 2. Fly a short familiar route. 3. Use the documented landing/selfie-screen controls. Variant/build/bindings: BLOCKED. Fable supplies variants; this checklist does not design them. | Can you read velocity/fuel/oxygen and thrust direction at a glance? Is the outside view clear? Can you enter/leave camera screens and know which view is active? ___ | 2 agency, 7 load |
| **10: Navigation/crashes, 9 min** | 1. Load a supplied short route near a moving landing target. 2. Try documented target-lock/autopilot/match modes. 3. Attempt a gentle landing, then the supplied crash setup seated/unbuckled. Route/build/bindings: BLOCKED. | Can you learn why a landing succeeds/fails? Do mode changes preserve agency and show their state? Do impacts, lost parts and unbuckled danger make sense while staying first person? ___ | 2 agency, 3 challenge, 5 risk |
| **11: Jetpack/repair, 8 min** | 1. Load a safe moving-ship/suit start. 2. Exit using documented suit/tether/match controls. 3. Approach and repair the supplied damaged part, then return. Build/start/bindings: BLOCKED. | Can you control drift precisely? Are oxygen/fuel/tether limits readable? Does a mid-flight repair feel free and a little scary without unexplained loss of control? ___ | 2 agency, 3 challenge, 5 risk |
| **12: Combat polish, 7 min per environment** | 1. Load the supplied planet/low-G/zero-G/interior arena with the actual enemy art. 2. Repeat limb shot, disarm and leg shot. 3. Repeat one short fight. Each build/start/binding: BLOCKED. | Do hits/disarms read instantly on the real model? Are gibs convincing under that environment's gravity? Is the fight funny, readable and responsive rather than cluttered? ___ | 1 fun, 3 challenge, 7 load |

## Return packet and sign-off

1. For each card, return your yes/no/BLOCKED answers, the build SHA/seed and one sentence describing each No. Include a short clip or named image where useful. Preserve the actual observation; don't fill in a cause you didn't see.
2. Ask Claude to attach the real Unity checks and Deck whole-frame/per-pass measurements needed by the code and visual gate. Estimates and previous milestone test counts are not new results.
3. Have Fable review look/feel/design questions and the Critic review real captures. This document assigns no scores and declares no visual pass.
4. Phase 1 ends only after all areas are available/refined, the playable build and completed packet are reviewed, and Matthew explicitly approves. Do not begin factions, ante progression, loot or casino work because a checklist file exists.
