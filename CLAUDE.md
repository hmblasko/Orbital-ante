# ORBITAL ANTE - MASTER BUILD PROMPT (v1.0, 30 Oct 2026)

Paste this whole file to the coding agent, or save it in the project root as CLAUDE.md so it is re-read every session. It is the single source of truth. Nothing in it overrides anything else; if two lines ever seem to conflict, use the Priority Order in section 0.3 and ask me.

---

## 0. READ THIS FIRST

### 0.1 What you are building
Orbital Ante (working title) is a single-player, first-person space-exploration roguelite for Steam Deck, built in Godot 4.x with all game code in Rust (godot-rust / gdext).

Pitch: Outer Wilds' living, physics-driven solar system and cockpit flight + Balatro's escalating antes and build-crafting + TF2's goofy style, readable silhouettes and gibs + Lethal Company's dread, with real NASA hardware as ships and vehicles. The player is a freelance space bounty hunter and scavenger who belongs to no faction.

### 0.2 Your role and standing rules
You are the lead programmer and technical designer. I direct the design; you implement it.
1. I am still learning Godot and Rust. Whenever I must do something, give numbered click-by-click editor steps (panel, node, property) and exact terminal/cargo commands, with which file each piece of code goes in. Explain any Rust concept (ownership, traits, Gd<T>) in one plain sentence the first time it appears.
2. Work phase by phase (section 16). Finish a phase, tell me exactly how to test it on PC and Steam Deck, then stop and wait for my go-ahead.
3. Never invent official names or anything not decided in this file. Use the working names and ask me.
4. Never invent playtest results, frame times or scores. Report only what you measured or what I tell you.
5. Data-driven: weapons, enemies, items, rarities, perks, planets, points and timings live in RON data files so I can tune without code. No magic numbers in code.
6. One master run seed; each system derives its own RNG from it, so runs are reproducible and shareable.
7. Modular scenes: one scene per ship, vehicle, enemy, weapon and planet feature. Rust-registered singletons only for GameState, RunSeed, Audio, Points, Notifications and SaveData.
8. Reuse assets in res://assets/ (Tripo models, NASA 3D Resources). If something is missing, list it and stub it with a clearly labelled placeholder.
9. Every mechanic you build must name which design pillar (section 1) and which fun rule (section 12) it serves. If it serves none, flag it for cutting.

### 0.3 Priority order when rules conflict
1. Ethics and legal (section 13, licensing in 15)
2. Stability: no crashes, no save loss
3. Steam Deck performance budgets
4. Core feel and the fun rubric (sections 10, 12)
5. Visual fidelity and the asset quality bar (section 11)
6. Content quantity

### 0.4 Your first task (Phase 0, step 1)
1. Check the latest stable Godot 4.x and the matching gdext release; confirm they are compatible and tell me the versions.
2. Give me step-by-step setup: Rust toolchain, Godot, gdext project template, Git + Git LFS, and a Steam Deck test build route.
3. Create the repo layout from section 14.
4. Build a test scene: a first-person capsule walking on a small sphere with point gravity, controller input, and a frame-time overlay.
5. Stop, give me test steps, and wait.

### 0.5 Report format at the end of every phase
- What changed (files and features)
- Why (pillar and fun rule served)
- How to test it (PC and Steam Deck), as numbered steps
- Measured results (frame times etc.) or "not measured"
- What is unverified or still placeholder
- Verification report summary (section 19.6): gates passed/failed/not run, open bugs, flagged screenshots, fun proxies
- Questions for me (max 5)

### 0.6 Glossary
- Ante: one timed round with a points target. A run is antes 1-8, then optional Endless.
- Ante Score: points earned this ante; never spent; checked against the target.
- Wallet: spendable points (upgrades, rerolls, casino, emergency lander).
- Diplomacy: goodwill with a faction, earned by delivering its bounties alive.
- Hostility: how angry a faction is with you; raises their aggression later.
- Signal Log: Outer Wilds-style log of radio rumours and half-seen landmarks.
- Sidegrade: an unlockable ship part or option that trades one strength for another; never a straight upgrade.
- Synergy tag: a label on perks/weapons/parts (Salvage, Stealth, Heat, Speed, Diplomacy, Chaos, Scavenger) that stacks into bonuses.
- Locker: storage at the home base; permanent loot only counts once delivered there.

### 0.7 Confirmed defaults
- Targets: Steam Deck / Linux first; Windows build second (same pipeline).
- Release: a paid Steam release, so licensing and store standards are strict from day one.
- Team: solo developer (me) directing AI agents; no other artists or programmers.
- Title: "Orbital Ante" (working title until launch).

### 0.8 Decided vs still mine to approve
Decided (section 17 has the details): faction working names, ship guns, the 3 extra space guns, the final perk list and synergy bonuses, starting points values, the bounty roster, achievements. Treat these as final for building; I may still rename or retune.
Still mine to approve before 1.0: the final faction names and emblems, and the final game title. Use the working names until then and never invent new ones.

### 0.9 Out of scope (do not build unless I ask)
Multiplayer, story/cutscenes, skateboard/skatepark, real-money anything, mod support, VR, mobile, procedural planets beyond the fixed roster, character creator.

---

## 1. DESIGN PILLARS
1. A living clockwork solar system: everything orbits, rotates and keeps moving whether you watch or not.
2. Goofy but deadly: TF2 humour and style on top of real danger, real physics and real dread.
3. Every run counts and teaches: each ante shows something new; each death teaches something.
4. Everything is physical: ships, enemies, loot and the player obey the same physics and can collide in funny ways.
5. Honest rewards: excitement comes from anticipation and craft, never from manipulation.

---

## 2. CORE LOOP AND RUN STRUCTURE

Core loop: launch from the home base on Earth -> explore -> earn points (travel, discovery, combat, repairs, salvage, bounties, contracts) -> hit the ante target before the timer runs out -> upgrade draft -> next, harder ante.

Run shape (Balatro model):
- Each ante lasts 12 real minutes. First discovery of each landmark, cave or ruin adds 20 seconds. Stopwatch items add 60 seconds.
- Ante targets rise steeply early and more gently later (Balatro-shaped curve); exact values and the maths behind them are in section 18.
- Ante 8 is the finale: every hostile faction allies to hunt you. Clearing it wins the run; Endless mode continues after.
- A full winning run takes about 90-120 minutes.
- Difficulty target: a typical first-time player dies around ante 3-4; experienced players win ante 8 about 1 run in 4.
- The whole solar system is open from ante 1. Antes add threats, never locked doors.

Ante cadence (each ante introduces a new pattern):
| Ante | New pattern |
|---|---|
| 1 | Moon, lander, rover, Faction A tier 1, oxygen basics |
| 2 | Bounties and rope gun, Faction A tier 2, damaged satellites |
| 3 | Robots on rovers; the Watcher first appears (harmless) |
| 4 | Faction B, ship patrols with light guns, casino appears |
| 5 | Aliens; the Watcher becomes lethal |
| 6 | Faction C, tier 3 heavies, factions retake planets, the Mimic |
| 7 | Satellite signals trigger ambushes, stronger weather, the Hull Crawler |
| 8 | Finale: hostile factions ally to hunt you |

Escalation as antes rise: more enemies (counts should be high), factions retake planets and return stronger, new enemy bases spawn, horror hunts more often, the ISS is more often enemy-held.

Death and loss:
- Death ends the run.
- Kept: unlocks, sidegrades, playable characters, notebook entries, loot already in the locker.
- Lost: Wallet, Ante Score, upgrades, carried items, Diplomacy, Hostility.
- Ship destroyed far from base: survive in the suit and steal any reachable ship, or call one emergency lander per ante (costs 25% of the Wallet).
- Run-end summary: new discoveries, new notebook cards, unlock progress. In the first ~10 runs nearly every run should reveal something new.

A typical ante, minute by minute: heavy smoky takeoff -> radio static hints at a base on the Moon's far side -> land in a crater, drive a rover over hills -> sneak in, take out guards, grab an oxygen tank and a chest -> spot a wanted poster, tie up the target, haul them aboard -> detour to repair a damaged satellite while its signal draws a patrol -> limp home with a cracked window as the timer runs low -> coins rack up -> upgrade draft and scratch-off.

Intensity curve: alternate peaks and valleys - quiet exploration, tense base raid, calm travel, rare horror spike, frantic return. Music follows intensity.

---

## 3. POINTS, ECONOMY AND PROGRESSION

Two tracks: every point earned adds to both Ante Score (never spent) and Wallet (spent). Spending never reduces ante progress. Points display with commas and a slot-machine count-up.

Points sources (all values in data; reward should match time spent):
- Discovery: first visit to bodies and landmarks - modest, delivered as a surprise, never a posted price list.
- Combat: kills, limb shots, disarms, base clears; bonus for all bases on a planet.
- Ground satellite dishes switched on; bonus for all dishes on a planet.
- Damaged orbital satellites repaired (multi-part; the restored signal may attract enemies).
- Salvage hauled to base by grapple/crane ships.
- Bounties and Diplomacy, contracts, bonus missions, racetrack orbital-speed achievement.

Contract board (start of each ante): 3 optional contracts with different risk, reward and faction consequences; take up to 2. Examples: take out a faction leader, steal a spacecraft, fix a specific satellite on a specific planet, find a specific item.

Faction Hostility: attacking a faction raises its Hostility; angry factions attack more later. Alive bounty deliveries and stealth pay off through Diplomacy, so killing everything is never automatically best.

End-of-ante upgrade screen (Select/Back), Fallout-style and very easy to read:
- Attributes bought with Wallet points: Damage, Speed/Jump, Oxygen Capacity, Defense.
- Upgrade draft: 3 random perks offered; pick 1. Rerolls cost Wallet points and get pricier with each reroll in the same ante (section 17.5).
- One free scratch-off ticket per ante (works like a chest).

Synergy tags: every perk, weapon, ship component and suit mod has 1-2 tags (Salvage, Stealth, Heat, Speed, Diplomacy, Chaos, Scavenger). 2 of a tag = small bonus; 4 = run-defining bonus. Support at least 6 viable builds (e.g. silent bounty hunter, demolition salvager, speed racer, diplomat).

Perks are trade-offs, never flat buffs. The final list of 14 perks, their tags and the tag bonuses are in section 17.4; starting costs and point values are in section 17.5.

Meta-progression (Balatro-style unlocks): completing goals unlocks base ship models and swappable ship components (sidegrades only). Completing a historical figure's bounty unlocks them as a playable character.

Leaderboard: local first (Steam later) for highest ante and highest points, tagged with the seed.

---

## 4. SOLAR SYSTEM, PLANETS AND PROCEDURAL GENERATION

Living system (Outer Wilds model): planets orbit the sun, moons orbit planets, satellites orbit planets and moons, asteroids drift, every body rotates. The map shows live positions. Real inertia everywhere.

Scale: rocky planets 400-900 m radius, moons 150-400 m, gas giants 1.5-2.5 km, whole system about 25 km across. Crossing the system at cruise takes about 4 minutes; Earth to Moon about 30 seconds. Use a floating origin (re-centre the world on the player). At this scale standard single precision plus a floating origin is enough, so do NOT require a custom double-precision engine build.

Gravity: each body's gravity is based on its real gravity but kept playable (Outer Wilds-like). Ships and stations have internal gravity fields.

Fixed vs seeded:
- Fixed every run: the sun, the body roster, orbits, faction home bodies.
- Seeded: each body's starting position along its orbit, landmark and feature placement, enemy bases, oxygen tanks, chests, damaged satellites, rare paintings, bounty targets, Earth's ancient humans, casino location.

Surfaces: hilly and fun to drive, never flat plains. Preset feature sets placed procedurally: caves, flyable canyons, large mountains, abandoned ruins, research labs, shipwreck sites, temple-like structures, mesas, hoodoos, cliffs, trees, planet-specific rock sets, varied craters (more on moons), bodies of water of varied shape, menacing geography. Small/medium/large faction bases scale enemies, vehicles and weapons with size.

Body roster and hooks:
| Body | Hook |
|---|---|
| Sun | Lethal heat close in; slingshot route for speedruns |
| Mercury | Scorching day side, frozen night side; outrun the terminator line |
| Venus | Crushing acid storms that corrode ships; Faction A home |
| Earth | Player's home base and launch pad; seeded ancient humans with spears |
| Moon | Crater driving, far-side outposts; vertical-slice location |
| Mars + Phobos | Dust storms, giant canyon run, rover-friendly; Faction B home |
| Asteroid belt | Moving seeded rocks that damage ship and player, salvage, hidden chests, the racetrack |
| Jupiter + Europa + Io | Gas giant interior surface; Europa ice over an ocean (Faction C home); Io volcanoes |
| Saturn + rings + Titan | Ring-ice slalom; Titan methane lakes and orange haze |
| Uranus | Sideways-spinning gas giant with odd day cycles |
| Neptune | Underwater ocean world |

Gas giants: fly into the atmosphere; landable surfaces appear inside; the sky changes to the planet's gases.

Look and weather: colours from real images and NASA planet textures, never bland. Subtle atmosphere tints (orange haze on Mars, light blue on Earth; none on moons or asteroids). Weather per planet: storms, rain, sandstorms, wind, lightning in different colours.

Orbital points of interest: enterable ISS (sometimes enemy-held), large roaming satellites, enterable satellites (size permitting) with loot, the racetrack.

---

## 5. SHIPS, FLIGHT, COCKPIT, VEHICLES AND CRASHES

Ships: NASA spacecraft are the default ships. Starter ship based on the Apollo Lunar Module. Each faction has small/medium/large ships at about 1x/4x/8x the Outer Wilds ship's length. Ships carry light defensive guns (enough for fighters and drones); medium and large ships add a turret or two. Some ships have a grapple/crane for salvage. Escape pods only on the largest ships.

Flight model (shared by every spacecraft): Outer Wilds-style, flown entirely in first person: 6-axis thrust, real inertia, match-velocity lock, autopilot to a marked target, target locking. Flight assist on by default.

Interiors: rounded, bowl-like shapes with curved halls, interesting glass windows, good lighting, correctly scaled to the player, no glitches, own gravity field. The starter ship has wall spots for public-domain paintings.

Entry: Outer Wilds-style blue ring hatch with a gravity lift that pulls the player in; the hatch closes behind them and can be opened/closed from inside, even in flight. Starter lander uses belly entry (hatch underneath). Large ships and stations have Battlefront-style hangars to fly or drive into.

Cockpit and dash:
- Outer Wilds-like layout (keep direction display, velocity, fuel, oxygen) but whimsical TF2-cartoony, inspired by real spacecraft cockpits, dynamic, easy to read, never in the way.
- Every gauge looks different (fuel as a bar, oxygen as something else); a small 3D ship model shows thrust direction.
- Build 3 switchable dash variants for me to choose from, including one black-screen green-text version. Show screenshots.
- Buckle-in seat with first-person seatbelt animation and a simple hydraulic sound; an unbuckled player can die in a bad crash.
- Cameras: landing camera plus two "selfie stick" cameras on poles; a cockpit screen fills the view and switches between them.

Takeoff: intense like a real launch - smoke, debris, heat shimmer, light, heavy takeoff rumble layered under the existing flight sound. Home base launch pad uses the NASA launch pad model I uploaded; no flags.

Damage, crashes and repair:
- Physics-driven crashes like BeamNG.drive / Outer Wilds: bouncing, tumbling, sometimes breaking apart; Outer Wilds-style impact sounds. No crash music, no cheesy landing sound, no third-person camera on crash or death.
- Repairable until heavy damage makes parts fall off permanently. The player can leave a moving vehicle to repair mid-flight.
- Glass can be shot and broken from inside or outside; fairly strong; repairable.
- Repairs are multi-part and quick, with one highly satisfying repair sound shared by ships and satellites.

Other vehicles: NASA rovers (incl. Mars rovers) and landers drivable in first person where sensible. Dune-buggy rovers get rocket boosts. Hostile robots drive their own rovers. Vehicles break into parts and crash spectacularly.

---

## 6. PLAYER, SUIT, MOVEMENT, CONTROLS AND HUD

Camera: always first person - walking, flying, driving, crashing, dying.

Player model: the NASA spacesuit asset (no NASA logos). Unlocked historical figures are alternate playable characters.

Suit:
- Required in space; without it the player starts dying. Suit oxygen refills from the ship's supply.
- Jetpack with satisfying thrust sound and Outer Wilds-style thruster light showing direction; can match velocity with ships and planets.
- Safety tether keeps the player from floating away but does not reel them in.

Oxygen tanks: seeded in naturally explored spots (enemy ships, satellites, outposts, points of interest, some asteroids). Use on the spot, or haul to the ship for more value. Neither trivial nor tedious.

Handheld radio: static and tones hint at nearby bases, structures, planets, satellites and landmarks; clearer as you approach. Feeds the Signal Log.

Stealth: crouch and sneak; ships and engines draw attention; a spotted player is searched for, then enemies give up.

Locker: permanent loot (art, rare cosmetics) only counts once carried to the locker at the home base.

Controls (Steam Deck default, fully remappable; mouse and keyboard also supported):
| Input | On foot | Ship |
|---|---|---|
| Left stick | Move | Thrust forward/back/strafe |
| Right stick / right trackpad | Look | Pitch/yaw |
| A | Jump / jetpack up | Thrust up |
| B | Crouch / jetpack down | Thrust down |
| LB / RB | Melee / rope gun | Roll left / right |
| LT / RT | Aim / fire | Match velocity / autopilot |
| X | Interact / repair | Unbuckle |
| Y | Swap weapon | Cameras screen |
| D-pad | Radio, tether, oxygen tank, map | Lock target, map |
| Select | Upgrade screen (between antes) | - |

HUD: diegetic and minimal. Oxygen, jetpack fuel and ammo on the helmet visor edge. Ante goal, timer and progress always one glance away. Map opened from the cockpit or a wrist screen, showing live orbits. No map icons for undiscovered places.

Onboarding: ante 1 teaches by environment - short walk to the lander, buckle-in prompt, nearby Moon landing. No text walls. Skippable. Every new mechanic gets a safe first encounter before a dangerous one.

---

## 7. COMBAT, WEAPONS, ENEMIES AND FACTIONS

Loadout: one gun + one melee secondary + the rope gun (exempt). Ammo is limited and refills only from fallen enemies using the same gun. Keep the weapon count small; each weapon has unique sound, feel, damage and properties.

Guns (ridiculous spoofs; original names and designs, no real brands or other games' names):
- Gravity gun spoof: grab and fling props, debris and ragdolls (can fling enemies into space).
- Uzi spoof: spray-and-pray, comically fast.
- AK-47 spoof: reliable all-rounder.
- Desert Eagle spoof: huge kick, huge damage.
- AWM sniper spoof: long-range limb popping.
- Silenced PPK spoof: quiet stealth pistol.
- Laser cowboy revolver: fan-the-hammer laser shots.
- Ray gun spoof: glowing bolts, splash damage.
- Double-barrel laser shotgun: close-range gib machine.
- Throwable rock: infinite, pathetic, funny.
- Plus 3 silly space guns (section 17.2).

Melee (rare, found): baseball bat, mailbox, butterfly knife, laser sword that actually cuts, other goofy items. Some gib; some launch targets into absurd ragdolls.

Hit reactions (TF2 gibs):
- Limbs shoot off easily and satisfyingly. Shooting a weapon or arm disarms; shooting a leg drops the enemy.
- Ragdolls on death; vehicles, robots and satellites break into natural components with real physics.
- Hit feedback: satisfying ping + hit marker + brief hit-stop (40-60 ms) on gibs and big impacts.
- Gore toggle turns gibs into sparks and bolts.

Factions (three; working names in section 17.1):
- Faction A: Soviet-spacesuit-inspired; home Venus.
- Faction B: Chinese-astronaut-inspired; home Mars.
- Faction C: JAXA (Japan)-inspired (HTV/Kounotori, Hayabusa, H-II rockets, Japanese suits), tidy white-and-red high-tech look; home Europa.
- Each: three enemy tiers clearly different in size and power (tier 1 rushes; tier 2 uses cover and flanks; tier 3 is a slow heavy with a special weapon), distinct weapons and abilities, small/medium/large ships.
- Silhouettes as readable as TF2 classes, with real dimension - never Roblox-boxy (the historical-figure bounty models are the quality bar). Faction colour coding like TF2 teams. Invented emblems only.

Unaligned threats: aliens (modified open-source models, unique properties), robots with distinct looks and fighting styles, robots driving rovers, Earth-only ancient humans with spears.

Enemy AI states: Patrol -> Suspicious -> Alert -> Search (last known position, ~45 s) -> Return. Each state has a clear audio and visual tell. In zero-g enemies use jetpacks. Distant AI runs simplified logic.

---

## 8. HORROR ENTITIES

Light horror, distinct from aliens. Three types, never more than one active, hunting only occasionally and more often at higher antes. Each has its own unsettling music cue and a visible or audible warning before it can harm you. All are very hard to kill; escape is the main counterplay.

| Entity | Arrives | Behaviour | How to survive |
|---|---|---|---|
| The Watcher | Ante 3 (lethal from ante 5) | Stares from a distance; each time you look away it appears closer | Keep it in view while backing off, or break line of sight and leave |
| The Mimic | Ante 6 | Fakes radio signals and satellite distress calls to lure you somewhere isolated | Learn its tell (a subtle wrong note on the radio) and leave |
| The Hull Crawler | Ante 7 | Gets aboard ships in deep space and hunts in the cabin | Stay buckled and burn hard, or eject through the hatch |

---

## 9. BOUNTIES, DISCOVERY, LOOT, CASINO AND COLLECTIONS

Bounties (Star Wars: Bounty Hunter PS2-style):
- Found on good-looking wanted posters, each posted by a specific faction; accepting adds a waypoint.
- Targets include deceased historical figures. They can pilot ships and hang out with other factions, never the one hunting them.
- Rope gun: 3 shots per run; tied targets can be carried and dropped (and drift off in orbit).
- Deliver alive: full points + Diplomacy. Kill: fewer points, no Diplomacy. Knocked-out figures can still be gibbed.

Discovery: the main reward is the discovery itself - hidden places, strange events, secrets, shortcuts, rare paintings. Signal Log records leads from the radio and half-seen landmarks.

Chests (seeded, Fortnite-style): one random item, from a rock to art, a weapon or a permanent/Unusual cosmetic.

Rarity (TF2-style, odds shown in-game): Common 55%, Uncommon 25%, Rare 13%, Legendary 6%, Unusual 1% (particle effects such as fire). Cosmetics for clothing and weapons.

Paintings: public-domain masterpieces unlock for the starter ship's walls; ultra-rare paintings spawn in some seeds. Purely cosmetic.

Space casino: random location per seed; blackjack and slots only; wager Wallet points or items. Rules in section 13.

Collections notebook (Paper Mario-style, main menu): logs found items and defeated enemies, with front pages for rare art and cosmetics. Entries are goofy trading cards with cartoon art, holographic sheen and reflections. No completion percentage by default.

---

## 10. UI, AUDIO, MUSIC AND GAME FEEL

UI: whimsical, slightly goofy, TF2-like; chunky readable fonts; controller-first; legible on the Deck's 7-inch screen.
Notifications (top right): max 3-4 on screen, colour/icon-coded by type, slightly larger goofy text, never spammy.
Visual language: interactables get a soft warm outline when looked at; hostile reads red-orange; friendly/neutral reads blue-teal; rarity colours only for rarity, faction colours only for factions.

Game feel:
- Input-to-response under 100 ms on Steam Deck, ideally under 67 ms.
- Every action stacks polish: sound, light, particles, controller rumble, subtle toggleable camera kick.
- Physics stays self-consistent so players can learn and predict it.
- Never make the controls the challenge; difficulty comes from situations and enemies.

Reward feedback (honest craft):
- Anticipation before reveals (~1 second reel spin, scratch, chest creak).
- Fanfare scales with true value: short chime for small gains; full coin rack-up, count-up and flash for big ones; unique fanfare for Unusuals. A common drop never sounds rare.
- Distinct sounds for art, cosmetics, ammo and new weapons; rumble on coin rack-ups.

Sound: keep the current ship-flying sound; add jetpack thrust, heavy takeoff rumble, Outer Wilds-style crash impacts, seatbelt hydraulic click, one shared repair sound. Remove all cheesy landing, crash and novelty sounds.

### 10.1 Music: space house driven by an adaptive intensity mixer

Style: professional-grade, AI-generated space house. Upbeat electronic built on funky bass guitar (house, rally house, modern disco) wrapped in a space vibe: wide analog pads, shimmering arps, filtered radio chatter textures, sub bass that feels like engine rumble. Feel references (for my ears only, never named in AI prompts): "Musty (Disco Lines Remix)" by Shoreline Mafia for the bass groove, "1997" by Beave. Instrumental only (vocal chops allowed, no lyrics). It must keep players engaged without ever becoming annoying or repetitive.

The intensity mixer (core system, Rust module `audio::music`):
- An intensity value from 0.0 to 1.0 is computed every frame and smoothed (rises over ~2 s, falls over ~8 s, with hysteresis so it never flickers).
- Inputs: ante baseline (ante 1 = 0.15 rising to 0.45 at ante 8), enemies in Alert or Search state nearby, damage taken recently, ship speed and boosting, ante timer under 2 minutes, oxygen below 25%, crash in progress, finale ante. Weights live in a RON file.
- Vertical layering: every track ships as 5 synchronized stems that fade in by intensity band:
  | Band | Intensity | Layers playing |
  |---|---|---|
  | Drift | 0.00-0.20 | Space pads + sub drone |
  | Cruise | 0.20-0.40 | + kick and funky bass guitar |
  | Groove | 0.40-0.60 | + hats, percussion, chords |
  | Heat | 0.60-0.80 | + lead synths and arps |
  | Peak | 0.80-1.00 | + full drop, extra percussion, risers |
- Layer changes happen only on bar boundaries (4/4 at 122 BPM), with 1-bar crossfades; drops land on the downbeat after a 2-4 bar riser.
- Horizontal DJ mixing: all tracks are 122 BPM in harmonically compatible keys (Camelot wheel neighbours). Track changes happen every 32-64 bars with a 16-bar DJ-style transition: low-cut swap of the bass, filter sweep, matched downbeats. Music never stops abruptly and never restarts a track from the top mid-play.
- Track pools by ante: antes 1-2 deep, spacious space house; antes 3-5 rally/tech house with more drive; antes 6-7 peak-time energy; ante 8 a dedicated finale set. Higher antes raise the baseline, so the same situation feels more intense later in a run.
- Breathing room: after about 90 seconds at Heat or Peak with no new threats, force a breakdown to Groove or lower, so intensity never becomes numbing. Quiet exploration may drop to Drift.
- Overrides: horror entities duck the house music to near-silence and bring in their own unsettling cue; the casino uses a lounge-house variant; the upgrade screen uses a laid-back loop; the main menu uses a signature theme.
- Effects: low oxygen applies a low-pass "muffled helmet" filter; big crashes briefly duck music under impact sounds; radio chatter and barks duck music by about 6 dB.
- Ship radio (diegetic DJ deck in the cockpit): skip track, favourite a track (favourites appear more often), switch between "Mix" (auto) and "Chill" (caps intensity at Groove). Never pauses the adaptive system entirely unless the player turns music off.
- Debug overlay (developer toggle): live intensity graph, current band, active stems, current track and next transition point.

Implementation: Godot 4's AudioStreamSynchronized (stems) and AudioStreamInteractive (transitions), driven from Rust; a dedicated Music bus with sidechain-style ducking from SFX and Voice buses; all thresholds, weights and track lists in data.

Music production pipeline (AI-generated, professional grade):
1. Generate with a commercially licensed AI music tool on a paid plan that grants commercial use for game distribution; re-check the tool's current terms before generating, and log plan, date and download for every track.
2. Prompt with genre, mood, instruments, 122 BPM and key; never name real artists or songs.
3. Export stems if the tool supports it; otherwise separate stems with a stem-separation tool, then clean them up in a DAW.
4. Human curation and editing: cut to 32-bar loopable sections with clean loop points, fix artefacts and muddy low end, tune to the 5-layer structure.
5. Master every stem set to a consistent loudness (around -16 LUFS integrated for the full mix), no clipping, matching tonal balance across tracks.
6. Quality gate: no audible AI artefacts, no gibberish vocals, grooves lock to the grid, transitions sound like a real DJ blend. Reject anything that doesn't pass.
7. Target library at 1.0: about 24 tracks (6 per ante pool) plus finale, menu, casino, upgrade-screen and horror cues, each with 5 stems.

Disclosure: AI-generated music is player-facing content, so it must be declared in Steam's AI content disclosure. Note that AI-generated tracks may have limited copyright protection.

---

## 11. ART DIRECTION, QUALITY BAR AND ASSET PIPELINE

Target look: Xbox 360-era fidelity, Source 2-quality lighting, TF2 style - a TF2 remaster on Source 2 at Xbox 360-class budgets. Stylised, painterly, readable. Not photoreal, not low-poly PS2, never boxy.

TF2 rules: exaggerated readable silhouettes; hand-painted-looking textures with soft gradients; warm/cool separation; moderate saturation; half-Lambert shading (shadows never pure black) plus soft rim light; faction colour coding.

Rendering: stylised PBR, baked lightmaps plus real-time key lights, soft shadows, ambient occlusion, bloom, volumetric fog for atmospheres and gas giants, good emissive lighting in ships.

Budgets (Steam Deck):
| Asset | Triangles | Textures |
|---|---|---|
| Player / enemy | 8k-15k | 1024 albedo + normal |
| Small ship / rover | 15k-30k | 1024-2048 |
| Large ship / station exterior | 40k-80k with LODs | 2048 atlas |
| Props, rocks, debris | 300-3k | 512 shared atlases |
Three LODs on everything large, impostors for distant ships and asteroids, occlusion culling inside stations.

Quality bar - hard rejects:
- Visible default primitives (plain cubes, cylinders, spheres) in final art.
- Hard 90-degree edges with no bevel; flat-shaded faceting on curved surfaces.
- Flat single colours with no detail, wear or variation; stretched or blurry UVs.
- Proportions that don't match the reference; floating or interpenetrating parts.
- Characters with block limbs, no hands or unreadable faces/visors.
Required: bevels, smooth curved forms, baked normal maps, panel lines, rivets, decals and trim sheets, material variety (metal, fabric, glass, rubber) with grime and wear, a silhouette that reads at distance.

Reference research before every asset (requires web search):
1. Gather 8-12 real reference images per asset (NASA Image and Video Library, NASA 3D Resources, ESA, JAXA, Wikimedia Commons, USGS planetary maps, public photos of Soviet/Russian/Chinese/Japanese suits and craft).
2. For style, study Valve's paper "Illustrative Rendering in Team Fortress 2" (Mitchell, Francke & Eng, 2007) and TF2 art.
3. Write board notes: proportions, signature details, materials, colours, what to exaggerate.
4. Log every image's URL and licence. Only public-domain or CC0/CC-BY images may go into textures (NASA, Poly Haven, ambientCG). Copyrighted images are reference only, never traced.
5. Feed the board into the ChatGPT concept-art prompt and the Tripo generation.
6. After import, render a side-by-side screenshot of asset vs reference, check against the quality bar, fix, and show me.

Pipeline: ChatGPT concept-art project (turnarounds) -> Tripo 3D -> decimate/retopologise to budget -> split destructibles into natural components with separate meshes and colliders -> TF2-palette texture pass -> Godot import with step-by-step instructions -> asset checklist of anything missing.

---

## 12. WHAT MAKES IT FUN - RULES AND PLAYTEST RUBRIC

1. Learning (Koster): every ante teaches a new pattern; mastery is felt, then challenged.
2. Interesting decisions (Sid Meier): real trade-offs, no dominant strategy (contracts, draft, Hostility).
3. Competence, autonomy, relatedness (Ryan, Rigby & Przybylski): controls learnable in 5 minutes; at least 3 viable ways to hit each ante target; factions remember you.
4. Curiosity (Outer Wilds): knowledge and discovery are their own reward; extrinsic points stay modest (Deci, Koestner & Ryan).
5. Flow (Csikszentmihalyi; Sweetser & Wyeth GameFlow): clear goals, instant feedback, rising challenge, no immersion breaks.
6. Aesthetics first (MDA: Hunicke, LeBlanc & Zubek): Discovery > Challenge > Sensation > Fantasy > Expression.
7. Four kinds of fun (Lazzaro): every ante touches at least three - hard (combat, landings, finale), easy (exploring, ragdolls, radio mysteries, casino), serious (collecting, restoring satellites), people (leaderboard, shareable seeds, screenshot-worthy crashes).
8. Fair failure (Juul): first-person death recap naming the cause in one line; no unseen one-shots; randomness never kills without counterplay.
9. Every run counts (Supergiant, Hades): run-end summary and steady unlock pacing.
10. Game feel (Swink): see section 10.
11. Emergent stories: systems collide under shared physics; prefer systemic interactions over scripted events.

Playtest rubric (I fill it in; never invent scores). Score 1-7:
| Measure | Question |
|---|---|
| Competence | Did I feel capable and improving? |
| Autonomy | Did I choose how to play this ante? |
| Presence | Did I feel really out there? |
| Clarity | Did I always know my goal? |
| Control frustration | Did the controls get in my way? (lower is better) |
| Pull | Did I want another run straight away? |
Also record time to first fun moment (target under 2 minutes) and every point of confusion.

---

## 13. ETHICS GUARDRAILS (non-negotiable, enforced in code)

- No real money anywhere; no buying spins, rerolls or items.
- No compulsion loops: no FOMO timers, daily-login pressure, streaks or grind.
- Loss aversion only makes the ship feel precious; it never punishes.
- No completion meters by default; cosmetics are purely cosmetic.
- Casino:
  - No losses disguised as wins: any spin or hand returning less than the bet plays a neutral/loss sound; results always show the net (e.g. "Net: -50").
  - No engineered near-misses: fair seeded RNG with no weighting toward "almost jackpot" displays. (Skill near-misses in combat and landings are fine and should be shown clearly.)
  - No machine zone: the ante timer keeps running in the casino; each spin takes 3 seconds; max 10 spins and 10 hands per visit.
  - Odds shown in-game; slot house edge fixed at about 5%; standard blackjack rules.
  - Wagers use Wallet points or items only, never Ante Score.
- All random odds (rarity, chests, scratch-offs, casino) are seeded, fair and documented in-game.
- Cultural respect: factions are inspired by real space programs' hardware and suit design, never by caricatures of people. No ethnic or national stereotypes in looks, voices, accents, names or jokes; the humour comes from situations, physics and gear, the same way for every faction.
- Historical figures: only long-deceased public figures (died before 1950), chosen for links to astronomy, physics and exploration. No dictators, perpetrators of atrocities, religious figures or anyone whose depiction could read as mocking a tragedy. Cartoonish TF2-style treatment only; no realistic gore on real people's likenesses (gore toggle still applies).

---

## 14. TECHNICAL ARCHITECTURE

- Engine: latest stable Godot 4.x + matching gdext release; Jolt physics; floating origin.
- Language: Rust for all game systems; GDScript only for thin UI and animation glue where Rust adds nothing.
- Repo: Git with Git LFS for models, textures and audio. Commit at the end of every phase with a plain-English message.
- Rust crate modules: orbits, flight, player, combat, ai, procgen, points, items, horror, audio, save, ui_bridge. Data in data/ as RON files.
- Tests: Rust unit tests; determinism tests (same seed = identical placements, loot rolls and orbits) run headless before every commit; scripted test scenes for gameplay.
- Audio: separate buses for Music, SFX, Voice, UI and Horror; music as synchronized stems (section 10.1).
- Saving: autosave at ante end and when entering a ship; save-and-quit anytime; versioned saves with migration; keep the last 3 autosaves; Steam Deck suspend/resume safe.
- Performance caps: max 40 active AI, 150 live physics debris pieces, 60 gib pieces; gibs fade after 20 s.
- Text: English, all strings in a table for later translation.
- Steamworks (achievements, leaderboard, cloud saves) after the vertical slice.

---

## 15. PRODUCTION READINESS

Definition of Done (every feature): works with controller only and with mouse and keyboard; holds the frame budget on Deck in its worst scene; final-quality sound, VFX and UI (or a tracked placeholder ticket); values in data; has a test, a bot that exercises it and screenshot baselines (section 19); saves/loads correctly mid-feature; works with every other system.

Performance budgets (Steam Deck, 1280x800):
| Budget | Target |
|---|---|
| Frame time | 16.6 ms (60 fps); floor 25 ms (40 fps) |
| Physics step | <= 4 ms |
| AI | <= 2 ms |
| Draw calls | <= 1,500 in busiest scene |
| RAM / VRAM | <= 6 GB / <= 3 GB |
| Cold start to menu | <= 15 s |
| Loading during play | none (stream in background) |
| Shader stutter | none (pre-warm behind start-up screen) |

Steam Deck Verified: Deck glyphs and default layout, text at least 9 px at 1280x800 plus UI scale option, no external launcher, native Linux build, on-screen keyboard for text entry, sensible first-launch defaults, suspend/resume safe.

Stability: crash handler writes a local log plus the seed; no telemetry without opt-in. Bug severity: S1 crash/save loss, S2 broken feature, S3 visual/audio issue, S4 polish.

Build pipeline: GitHub Actions builds Linux and Windows on every push, runs all tests, attaches builds to tagged releases. Semantic versioning; version and seed shown on the pause screen.

Comfort and accessibility: FOV slider, camera shake toggle, crash-tumble reduction, colourblind-safe palettes (shape plus colour), subtitles for radio, horror and barks, assist mode (slower oxygen drain, longer ante timer, leaderboard disabled), gore toggle.

Legal and licensing (asset licence ledger):
- Log every asset, texture, sound and track with source and licence.
- Never use the NASA insignia ("meatball"), logotype ("worm") or seal; avoid Roscosmos, CNSA and JAXA logos too. Invent emblems.
- Parody weapons: original names and designs only.
- Historical bounty figures: deceased public figures only.
- Music: AI-generated on a paid plan with commercial rights (logged per track), or original/licensed for commercial game use.
- Steam AI disclosure: declare all player-facing AI-generated content (music, Tripo models, concept-derived art) in the Steam content survey.

Release checklist (1.0): full options menu (graphics presets Deck/Low/Medium/High, per-bus audio, remapping, comfort, gore); pause menu; credits and licence notices; audio mixed to about -16 LUFS integrated with no clipping; ~30 achievements rewarding variety and skill, never grind; IARC rating on Steam; store capsule, 5+ screenshots and a 60-90 s gameplay trailer; external playtests each milestone (5-8 new players, watched silently, scored with the rubric; fix the top 3 issues before moving on).

---

## 16. BUILD PHASES

Stop after each phase, report (section 0.5) and wait.

0. Vertical slice first: the Moon and Earth orbit, starter lander, one rover, Faction A tiers 1-2, three guns (AK spoof, double-barrel laser shotgun, rock), baseball bat, oxygen, one base, two dishes, one damaged satellite, antes 1-2, upgrade draft, and a first version of the intensity music mixer with 2 placeholder tracks. It must score well on the rubric and pass the asset quality bar before phase 5 or later starts. Phases 1-4 and 6-7 are first built only to slice scope, then expanded.
1. Foundation: project setup, Deck build route, input map, seed system, save system, data files, CI, and the verification layer skeleton (section 19): smoke test, input replay recording, screenshot harness, bug-filing script and report dashboard. Bot profiles are added as each system they need comes online.
2. Player and suit: first-person controller, suit, oxygen, jetpack, tether, inertia, planet gravity.
3. Solar system: bodies, orbits, rotation, floating origin, live map, atmospheres.
4. Starter lander: belly-entry gravity ring, interior, seat, 3 dash variants, flight model, autopilot, lock, cameras, takeoff, crashes, repair.
5. Planet surfaces: terrain, procedural features, craters, caves, weather, Neptune ocean, gas giant interiors.
6. Combat: guns, melee, rope gun, limb damage, gibs, ragdolls, AI states.
7. Ante loop: points maths (section 18: multiplier, diminishing returns, indexed prices, interest, cash-out, pace meter) and the balance simulator, timer, contracts, notifications, upgrade draft, synergy tags, scratch-off, run-end summary, leaderboard.
8. Content pass: all factions and tiers, aliens, robots, ancient humans, bases, bounties, Diplomacy/Hostility, ISS, satellites, dishes, salvage, chests, casino, racetrack.
9. Horror, audio and music: three horror entities, feedback sounds, the adaptive intensity music mixer (section 10.1) with debug overlay and ship radio. A first version of the mixer with 2 placeholder tracks is built during the vertical slice so music can be judged early.
10. Meta and polish: unlocks, sidegrades, playable historical figures, locker, notebook trading cards, Signal Log, achievements, accessibility, performance pass, release checklist.

Acceptance for every phase: runs on Steam Deck within budget in the target scene; fully playable with a controller; no new errors in the log; tunable values in data; a written test script for me; and every section 19 gate passed (or explicitly listed as not run, with the reason).

---

## 17. DECIDED CONTENT AND STARTING NUMBERS

All numbers are starting values in RON data; tune them from playtests and report what you changed.

### 17.1 Factions (working names; I approve final names and emblems)
| Faction | Working name | Inspiration | Home | Look | Signature ability |
|---|---|---|---|---|---|
| A | The Venera Collective | Soviet suits and craft (Sokol, Orlan, Venera, Soyuz) | Venus | Heavy, rounded, armoured; olive and rust | Tier 3 heavies deploy acid-proof shield walls |
| B | The Tiangong Syndicate | Chinese suits and craft (Feitian, Shenzhou, Zhurong) | Mars | Sleek, angular, practical; red and gold trim | Tier 2 drop dust-storm smoke and flank inside it |
| C | The Hayabusa Company | JAXA suits and craft (HTV/Kounotori, Hayabusa, H-II) | Europa | Tidy, modular, high-tech; white and red | Tier 2 launch sample-grabber drones that steal your loot |
Emblems are invented (no real agency logos).

### 17.2 Extra space guns (all Chaos-tagged)
- Black Hole Popper: lobs a mini singularity that sucks in ragdolls, debris and loose loot for 3 seconds, then pops them outward.
- Freeze Extinguisher: a CO2 extinguisher that briefly freezes enemies solid (frozen enemies shatter into gibs) and doubles as a zero-g thruster when fired backwards.
- Plasma Leaf Blower: a cone of force that blasts enemies, debris and tied targets away; flings enemies off ledges or into space.

### 17.3 Ship guns
| Ship size | Gun | Role |
|---|---|---|
| Small (incl. starter lander) | Pulse cannon | Short bursts that overheat; enough to scare off fighters and drones |
| Medium | Flak turret (auto-aim, manned from a seat) | Area denial against small craft and incoming debris |
| Large | Harpoon cannon | Spears enemy ships or wrecks and tows them; doubles as salvage gear |

### 17.4 Final perk list and synergy bonuses
| Perk | Tags | Effect |
|---|---|---|
| Deep Breath | Scavenger | +40% suit oxygen, -20% jetpack fuel |
| Ghost | Stealth | Near-silent crouching; your ship's engine is louder |
| Lucky Charm | Scavenger | Chests roll twice and you pick; no scratch-off tickets |
| Signal Jammer | Stealth, Salvage | Repaired satellites never draw enemies but pay 30% less |
| Pack Mule | Salvage, Diplomacy | Carry two targets or tanks; move slower while carrying |
| Glass Cannon | Heat | +30% damage dealt, +30% damage taken |
| Second Wind | Heat | Once per ante, survive a lethal hit with 1 HP |
| Hot Rod | Speed, Chaos | Rover boosts recharge 2x faster; rovers take more crash damage |
| Diplomat | Diplomacy | Alive deliveries give +50% Diplomacy; kills raise Hostility 2x faster |
| Overclocked | Speed, Heat | Faster ship acceleration, higher fuel use |
| Junk Magnet | Salvage | Small salvage drifts toward you; your ship takes +10% damage |
| Ragdoll Physicist | Chaos | Flung ragdolls damage enemies they hit; gravity-gun ammo drains 2x faster |
| Silver Tongue | Diplomacy | Hostility cools over time; bounty payouts -10% |
| Afterburner | Speed | Jetpack dash; oxygen drains 2x while dashing |

| Tag | 2 held | 4 held |
|---|---|---|
| Salvage | +25% salvage value | Crane/harpoon reach doubled; wrecks auto-tow to your ship |
| Stealth | Enemies take 30% longer to spot you | First shot from stealth always gibs |
| Heat | +10% damage below half health | Kills below 25% health restore 10 health |
| Speed | +10% move and thrust | High-speed ramming damages enemies, not you |
| Diplomacy | +25% Diplomacy gains | One chosen faction will not shoot first this ante |
| Chaos | Ragdolls bounce 50% further | Gibs have a 10% chance to drop ammo for your current gun |
| Scavenger | +25% ammo from pickups | 10% of chests roll one rarity tier higher (shown in the odds screen) |

### 17.5 Starting points values
Ante targets (Ante Score): 1,000 / 1,700 / 2,800 / 4,400 / 6,500 / 9,000 / 11,800 / 14,500 (derivation in section 18).
Check: ante 1 should be reachable in about 9 of its 12 minutes by an average player using any one of the 3 main routes.
The point values below are base values before the multiplier and diminishing returns in section 18.

| Source | Points |
|---|---|
| Kill: tier 1 / tier 2 / tier 3 | 25 / 60 / 150 |
| Limb shot / disarm | +5 / +15 |
| Base clear: small / medium / large | 150 / 400 / 900 |
| All bases on a planet | +500 |
| Dish switched on / all dishes on a planet | 120 / +400 |
| Satellite repaired | 300 |
| Discovery (first visit; varies by landmark) | 30-80 |
| Salvage per piece (by size) | 50-400 |
| Bounty alive (by target tier) | 600-1,500 |
| Bounty dead | 40% of the alive value, no Diplomacy |
| Contracts | 300-1,200 |
| Racetrack orbital-speed lap | 250 |
Scaling: enemy and contract values rise 15% per ante so later antes stay reachable.

Wallet costs are indexed to the current ante target T (section 18.5): attribute levels 1-5 cost 0.30T / 0.45T / 0.60T / 0.80T / 1.00T; draft reroll 0.10T (+0.05T for each further reroll in the same ante); emergency lander 25% of the Wallet; casino minimum bet 0.02T.

### 17.6 Bounty roster (historical figures)
Starter roster of 8, each with a TF2-style exaggerated look and one quirk that changes how you catch them:
| Figure | Quirk |
|---|---|
| Galileo Galilei | Spots you from far away with his telescope |
| Isaac Newton | Throws apples that knock your aim off |
| Johannes Kepler | Flies elliptical evasive orbits in his ship |
| Tycho Brahe | Charges at you, golden nose gleaming; very tough |
| Nicolaus Copernicus | Hides at the system's centre, near the Sun |
| Nikola Tesla | Electric arcs disable your jetpack briefly (use his name only, no brand imagery) |
| Jules Verne | Rides a cannon-launched capsule between moons |
| Edmond Halley | Appears only on a long looping orbit, like his comet; track him by radio and intercept |
Completing each bounty unlocks that figure as a playable character with a cosmetic-only difference.

### 17.7 Achievements (about 30, variety and skill, never grind)
Examples to set the tone:
- Escape Velocity: reach orbital speed on the racetrack.
- Gentle Hands: deliver 5 bounties alive in one run.
- Newton Was Right: fling an enemy into orbit with the gravity gun.
- Buckle Up: survive a crash that destroys half your ship.
- Don't Look Away: escape the Watcher without breaking line of sight.
- Full House: win a blackjack hand while the Mimic is on the radio.
- Tourist: visit every body in one run.
- Clean Hands: clear ante 1 without killing anyone.
- Synergy: hold 4 perks with the same tag.
- Ante Up: win a run.
No achievement requires more than one run's worth of repetition.

---

## 18. POINTS AND ANTE MATHEMATICS

Goal: the system must be fair (every seed winnable, every rule visible), challenging (targets outpace an average player's growth), and interesting (choices change the maths, not just the totals). All constants live in data; the formulas live in the Rust `points` module with unit tests.

### 18.1 Two curves, one gap
- Challenge curve: the ante target T(n).
- Power curve: how fast the player earns points per minute, P(n).
- Fun lives in the gap. Early on P grows faster than T (learning, first upgrades) so the player feels strong; from ante 4 onward T grows a little faster than an average player's P, so only good routes, builds and play close the gap. That is the wall a typical first run hits at ante 3-4.

Player power is modelled as P(n) = B(n) x M(n) x E(n):
- B(n), base value of actions: rises 15% per ante, so B(8) is about 2.66 x B(1).
- M(n), the score multiplier (18.3): about 1.2 early; an average build reaches about 2.3 by ante 8, a strong build about 3.2 (cap 3.5).
- E(n), efficiency (skill, attributes, route knowledge, faster travel): about 1.0 to 1.6 for an average player, up to about 1.9 for an expert.
- Estimated growth from ante 1 to ante 8: average player about 8x; expert about 13-14x.

### 18.2 The target curve
T(n) = 1,000 / 1,700 / 2,800 / 4,400 / 6,500 / 9,000 / 11,800 / 14,500.
- Growth ratios fall from 1.70x to 1.23x per ante, the same shape as Balatro's base requirements (300, 800, 2,000 ... 50,000), which also rise steeply early and flatten late.
- Total growth is 14.5x: just above expert power (~13.5x), well above average (~8x). So experts win sometimes (target: about 1 run in 4) and average players need better builds and knowledge, which is the improvement loop.
- Endless (ante 9+): T grows 1.6x per ante. Numbers over 100,000 display compactly (e.g. 160k, 1.2M).
- These are model estimates. The balance simulation (18.8) must confirm or correct them before playtests.

### 18.3 Score multiplier: Ante Score = base points x multiplier
Additive points alone cannot keep up with a rising target, which is why Balatro multiplies chips by mult. Here the multiplier rewards variety and momentum, never repetition:
- Variety: count the distinct point categories used this ante (Combat, Discovery, Repair, Salvage, Bounty, Contract, Stunt). Multiplier part = 1 + 0.2 x (distinct - 1), max 2.2.
- Chain: actions within 20 seconds of each other build a chain worth +0.05 per link (max +0.5). The chain breaks after 20 seconds idle, never because you took damage.
- Perks and synergy tags can add up to +0.8: each tag held at 4 adds +0.2 on top of its section 17.4 bonus.
- Total multiplier = variety + chain + perks, capped at 3.5. Shown on the HUD as "x2.4".
- Effect: exploring is worth more than its small raw points, because a Discovery adds a whole category to the multiplier. Curiosity matters mathematically without big extrinsic payouts.

### 18.4 Diminishing returns on repetition
- The k-th repeat of the same source type within one ante pays base x 0.85^k, with a floor of 40% (e.g. the 6th tier-1 kill pays about 44%).
- This removes dominant farming strategies and makes switching activities the efficient choice.
- First discoveries, bounties and contracts are naturally one-off and are exempt.

### 18.5 Wallet economy: sources, sinks and feedback loops
- Source: every point of Ante Score also adds to the Wallet (1:1), so the player always earns what they see.
- Sinks: attributes, draft rerolls, emergency lander, casino house edge.
- Prices are indexed to the current target T (section 17.5), so the economy never inflates as numbers grow; an upgrade costs about the same share of an ante at ante 2 or ante 7.
- Payback rule: each attribute level must raise efficiency enough to repay its price within about 1 ante. If it doesn't, cut the price or buff the effect.
- Interest (Balatro-style saving vs spending choice): at ante end, unspent Wallet earns 10%, capped at 0.1T.
- Feedback loops: upgrades -> more points -> more upgrades is a positive loop that could snowball. It is checked by three negative loops: rising targets, diminishing returns, and faction Hostility (killing-heavy play makes later antes harder). Interest is capped so hoarding never beats playing.

### 18.6 Push your luck: cash out early
- Once Ante Score reaches the target, the player may Cash Out from any ship's cockpit. Each remaining second converts to Wallet at 0.002T per second (capped at 0.6T).
- Or keep playing: overshoot points still count for the leaderboard and Wallet, but you risk dying.
- This is a clear, honest risk-reward choice with no hidden odds.

### 18.7 Fairness rules
- Supply guarantee: the generator ensures every ante's reachable point supply is at least 2.5x the target within the time available. Seeds that fail are regenerated. This is checked by the determinism tests.
- No hidden dynamic difficulty adjustment. Hidden rubber-banding can produce implausible situations, and players have been documented learning to play badly on purpose to game it. Difficulty follows fixed, visible rules; help comes only through the visible assist mode.
- Pace meter: the HUD shows Ahead / On pace / Behind, using current score plus the last 2 minutes' rate times the remaining time. Risk stays legible.
- Risk-reward: a riskier option (contract risk level 1-3, alive bounty, cash-out decision) must pay 1.3-1.6x the expected value of the safe option, with visibly higher variance.
- Casino: negative expected value (about -5%) by design; it is a fun sink, never an income strategy. Odds are shown.
- Readable numbers: base values are round (5, 25, 60, 150 ...); the count-up animation shows base x multiplier on big gains so players learn the formula.

### 18.8 Balance simulation (required before playtests)
- Build a headless Monte Carlo simulator in Rust that plays 10,000 runs per player profile: Novice, Average and Expert, defined by actions per minute, route efficiency, mistake rate and build quality.
- Output: distribution of ante reached, time-to-target per ante, multiplier distribution, Wallet over time, and how often each perk and route is chosen.
- Tune constants until: Novice median death at ante 3-4; Expert win rate about 25%; no single route or perk in more than 40% of winning Expert runs (no dominant strategy); every one of the 3 main routes can clear ante 1 in about 9 minutes for Average.
- Label all simulator numbers as simulated. Real playtest scores (section 12) override them.

### 18.9 Sources for this section
Balatro's blind and ante requirements; Schreiber & Romero, Game Balance (2021), on cost curves and progression; Adams & Dormans, Game Mechanics: Advanced Game Design (2012), on internal economies, sources, drains and feedback loops; Hunicke, The Case for Dynamic Difficulty Adjustment in Games (2005), and the documented problems with hidden DDA; Sid Meier on interesting decisions.

---

## 19. PLAYTHROUGH AND VERIFICATION LAYER

The game is checked constantly, by machines every day and by humans every week. Nothing is "done" until it passes this layer. Never report a check as passed if it was not run; say "not run" and why.

### 19.1 Cadence ("test a lot")
| When | What runs |
|---|---|
| Every commit | Rust unit tests, determinism tests, data-file validation, smoke test (boot to menu, load the slice, 60 s autoplay bot, quit cleanly) |
| Every feature finished | Feature test scene, screenshot regression for that feature, 50-seed bot run, verification report |
| Nightly (CI) | 200 seeds x 5 bot profiles playing full antes; full screenshot regression; physics and perf capture; balance simulator (section 18.8) |
| End of every phase | Everything above + AI visual review + fun-proxy dashboard + my Steam Deck session with the rubric |
| Before each milestone | External playtest: 5-8 new players (section 15) |
Regressions block merging: a failing check means fix first, then continue.

### 19.2 Autoplay bots (automated playthroughs)
Rust behaviour-tree bots drive the real player controller through the same input layer as a human, on many seeds, at accelerated time where physics allows (otherwise real time).
| Profile | Plays like | Finds |
|---|---|---|
| Explorer | Visits every body and landmark, drives every rover | Unreachable areas, falling through terrain, missing collision, empty regions |
| Fighter | Seeks and fights every enemy type with every weapon | Combat bugs, gib/ragdoll explosions, AI getting stuck, unfair deaths |
| Bounty Hunter | Takes contracts and bounties, ropes and carries targets | Objective soft-locks, carry/tow bugs, posting/delivery failures |
| Speedrunner | Flies fast, crashes hard, cashes out early | Tunnelling at speed, crash physics blow-ups, floating-origin jitter |
| Chaos Monkey | Random and adversarial inputs: spam buttons, open menus mid-action, save/quit at odd moments | Crashes, UI dead-ends, save corruption, input locks |

Every bot run records: seed, an input replay file (deterministic, so any bug can be reproduced exactly), deaths and their causes, points per minute, multiplier, frame times, errors and warnings in the log.

Automatic detectors during bot runs:
- Stuck: no meaningful position or objective progress for 30 s.
- Soft-lock: an objective or exit becomes unreachable.
- Out of world: an object falls below terrain or passes a kill boundary.
- Physics explosion: any body exceeds a sane velocity or spins wildly; NaN positions.
- Unfair death: damage from a source that was off-screen and silent for the previous 2 s (section 12, rule 8).
- Economy anomalies: points rate or Wallet outside 3 standard deviations of the simulator's prediction.

### 19.3 Graphical verification
- Screenshot regression: about 150 fixed camera viewpoints (cockpit, each ship interior, each body surface, menus, upgrade screen, casino, notebook) rendered with a fixed seed and time. Compare with approved baselines using a perceptual difference score (SSIM); a drop below 0.98 is flagged for review. I approve new baselines; you never approve your own.
- Automatic image checks on every screenshot and on frames sampled every 10 s of bot runs:
  - Missing texture colours (magenta/black), fully black or white frames.
  - Text overflow or overlap, and text smaller than 9 px at 1280x800.
  - Flicker between consecutive frames (z-fighting, shadow acne).
  - Large unlit or overblown areas (light leaks, bloom blow-outs).
  - LOD pops: sudden silhouette changes between frames as distance changes.
- AI visual review: you look at the flagged frames plus a random sample of 30 frames per run, and check them against the asset quality bar (section 11) and this list: clipping, interpenetration, floating objects, stretched UVs, boxy or Roblox-like shapes, wrong scale, unreadable UI, broken effects. Log each finding with screenshot, location and seed, labelled "AI review finding - needs my confirmation".
- Rendering on CI: Godot's headless mode does not render, so screenshot tests run with real rendering on a GPU runner, or with a software Vulkan driver (Mesa lavapipe) if no GPU is available (slower, acceptable for nightly).
- Steam Deck capture: during my weekly Deck session, collect MangoHud frame-time logs and 3 short recordings of the worst scenes.

### 19.4 Bug pipeline
- Every crash, detector hit and confirmed visual finding becomes a GitHub Issue automatically: severity (S1-S4), seed, replay file, screenshot or clip, log excerpt, build version.
- Duplicates are merged by stack trace or detector signature.
- S1 (crash, save loss, soft-lock) stops feature work until fixed. S2 must be fixed before its phase ends.
- Every fixed bug gets a regression test (a replay or test scene) so it can never silently return.

### 19.5 Is it fun? Verification
Bots cannot feel fun, so fun is checked two ways and the human result always wins.

Fun proxies from bot telemetry and recordings (warning lights, not verdicts):
| Proxy | Healthy range |
|---|---|
| Time to first rewarding moment from launch | under 2 minutes |
| Dead time (no meaningful event, choice or discovery) | no stretch over 60 s outside deliberate quiet moments |
| Distinct activity categories per ante | at least 4 for Explorer and Bounty Hunter bots |
| Intensity curve (from the music mixer) | at least 2 peaks and 2 valleys per ante; never above Heat for more than 2 minutes |
| Dominant strategy | no route or perk in more than 40% of winning runs |
| Unfair deaths | under 5% of all deaths |
| Repeat-death frustration | the same death cause 3 times in a row on one seed is flagged for review |
| Target clearance | each of the 3 main routes clears ante 1 in about 9 minutes for the Average bot |

AI fun review: after each phase, write a short critique of 3 recorded bot playthroughs against the section 12 rules (learning, interesting decisions, curiosity, fair failure, feel, emergent moments): what is working, the 3 biggest fun risks, and the smallest test to fix each. Label it "AI heuristic review, not player data".

Human verification (the deciding vote): my rubric scores after every phase, external playtests before milestones. Gate to finish a phase: average of 5/7 or higher on Competence, Clarity and Pull, Control Frustration 3/7 or lower, and no S1 bugs.

### 19.6 Verification report (every feature, nightly and phase end)
Publish as a short HTML dashboard in reports/ and summarise it in the phase report (section 0.5):
- Pass/fail for every gate, with "not run" where applicable.
- New, fixed and open bugs by severity, each with seed and replay link.
- Flagged screenshots with before/after images.
- Frame-time charts per scene versus budget.
- Fun-proxy table and trend versus the last report.
- Balance simulator results versus targets (labelled simulated).
- What you could not verify and what you need from me (e.g. a Deck session).

---

## 20. KEY SOURCES (for your design reasoning)
Koster, A Theory of Fun; Sid Meier, "Interesting Decisions" (GDC 2012); Ryan, Rigby & Przybylski (2006); Przybylski et al. (2014); Deci, Koestner & Ryan (1999); Sweetser & Wyeth, GameFlow (2005); Hunicke, LeBlanc & Zubek, MDA (2004); Lazzaro, Four Keys to Fun; Juul, The Art of Failure (2013); Swink, Game Feel (2008); Hodent, The Gamer's Brain (2017); Kasavin on Hades; Mobius Digital on Outer Wilds; Mitchell, Francke & Eng, Illustrative Rendering in TF2 (2007); Dixon et al. on losses disguised as wins (2010) and slot sound (2013); Clark et al. on near-misses (2009); Schull, Addiction by Design (2012).
