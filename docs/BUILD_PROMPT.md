# Build Prompt: Orbital Ante (working title)

> Matthew's master build prompt for the Unity rebuild, saved word for word on 9 Nov 2026.
> This is the source of truth. CLAUDE.md summarises the standing rules and points here.

You are the lead engineer and technical designer for **Orbital Ante**, a first-person space-exploration roguelite built for Steam. You are building a new version of the game from scratch. I direct the design; you implement it. Read this whole document before writing any code, then propose a milestone plan (see "Build order") and wait for my go-ahead on Milestone 1.

I'm still learning Unity. Whenever I have to do something myself (install, import, configure, click through the editor), give me straightforward, numbered, step-by-step instructions.

---

## 1. The game in one paragraph

You fly a small NASA-style rover spacecraft around a living, constantly-moving solar system modelled on our own (Outer Wilds-style physics and flight, beam ng level of destruction of ship), landing on planets, exploring bases, ruins, caves and wrecks, fighting three rival factions plus roaming aliens, robots and horror entities, collecting bounties, salvage, oxygen and loot, and banking points. Each run escalates through **antes** (Balatro-style): as the ante rises, enemies get tougher, factions retake ground and may ally to hunt you, and the stakes climb. Between antes you spend points on upgrades and draft perks. There is **no story** — it's a pure sandbox roguelite. Tone: EDM space music, TF2-style humour on top of convincing, grounded space hardware, with a thread of horror and existential unknown, while also being goofy and not overly technical looking or AI generated.

Core inspirations: Outer Wilds (flight, physics, moving solar system, cockpit, ship entry), TF2 (character silhouettes, gibs, rarity, UI tone), Balatro (antes, unlocks, perk drafts with synergies), Lethal Company (scary roaming threats), Star Wars: Bounty Hunter PS2 (bounties), BeamNG.drive (crash physics), Paper Mario (notebook).

---

## 2. Tech stack and platform

- **Engine:** **Unity** (current Unity 6 LTS) with **C#**, using the Universal Render Pipeline (URP).
- **Target:** Steam Deck (native Linux build, 1280×800, handheld controls first), with keyboard and mouse also fully supported. Must hold a stable framerate on Deck; profile and set budgets early.
- **Input:** Unity's **Input System** package with action maps for both keyboard/mouse and gamepad, so every action is rebindable (see the pause menu in section 13). **Select** opens the end-of-ante upgrade screen.
- **Quality bar:** production-ready and refined, not a prototype. Clean architecture, data-driven content (weapons, enemies, items, factions defined in data files), seeded determinism, save system.

---

## 3. Art direction

- Graphics can be **simple** — clean, readable, stylised shapes and colours are fine. This is **not** meant to be a PS2-style game, so no deliberately low-res textures, jittery low-poly look or retro filters. Like tf2 and or gang beasts.
- Simple doesn't mean cheap: characters and vehicles should still have clear, distinct silhouettes (never Roblox-like boxy), and colours/lighting should keep the world interesting rather than bland.
- Pull real reference images before designing any asset.
- **Asset pipeline:** concept turnarounds made in ChatGPT → 3D models generated in **Tripo AI** → imported into Unity by you. Rely primarily on **NASA 3D Resources** models (ISS, Apollo vehicles, satellites, asteroids, rovers, spacesuit, landing sites, launch pad), simplified to match the art style. For each system, tell me exactly which assets you need so I can generate them.
- Destructible objects (ships, satellites, rovers, robots) must split along their natural components and genuinely detach with physics.
- Where it makes sense, assets are enterable, operable and drivable in first person.

---

## 4. Non-negotiable design rules

- **Everything is first person**, including flying from the cockpit. No third-person camera ever — not when crashing, not when dying.
- **The player belongs to no faction.** Factions are traded with, hired by, fought or avoided. Ships are acquired, never issued.
- **Do not invent official faction names.** Use the placeholders below until I approve names.
- **Real space inertia throughout:** objects in motion stay in motion.
- Unlocks are **sidegrades, never straight upgrades** (ship components, base ship models).

### Game design constitution (applies to every decision)
1. Fun core gameplay first.
2. Agency and meaningful trade-offs.
3. Learnable challenge — never inflated health or hidden rules.
4. Discovery that matters.
5. Legible risk/reward.
6. Meaningful replayability.
7. Manage cognitive load.
8. Varied intensity.
9. Ethical design: no FOMO timers, no grind, no streaks/daily-login pressure, no opaque randomness.
10. Evidence-based iteration: for each change state the problem, hypothesis, smallest test, acceptance criteria and playtest protocol.

Grounding: Nguyen (*Games: Agency as Art*), Rigby & Ryan (Self-Determination Theory), Hodent, Koster, Schell's lenses, Csikszentmihalyi's flow, Madigan. Avoid value capture — no completion meters by default; paintings and cosmetics are purely cosmetic.

**Reporting rule:** never invent playtest results. After each piece of work, report what changed, why, which constitution principle it serves, what you tested, and what is still unverified.

---

## 5. Run structure and difficulty

- Runs progress through **antes**. An ante timer exists; items like a **stopwatch** push it back.
- First-time players should typically die around **ante 3–4**.
- As the ante rises: more and tougher enemies; factions **retake planets** and return stronger; new enemy bases can spawn; angered factions attack more often; hostile factions may **ally solely to hunt the player**; horror entities appear more often.
- **End-of-ante upgrade screen** (opened with Select), Fallout-style and very easy to follow:
  - Spend points on a few simple attributes: damage, speed/jump, oxygen capacity, defense.
  - **Balatro-style perk draft:** pick 1 of 3 perks, with synergy tags between perks.
  - One **scratch-off ticket** per ante that works like a chest.
- **Bonus missions** unlock after each ante for extra points (e.g. take out a faction leader, steal a spacecraft, fix a specific satellite on a specific planet, find a specific item).
- **Points** reward travel, exploration, fighting and weighing faction antagonism. Balance so rewards fairly match the time spent earning them.
- **Leaderboard** for highest ante and points.
- **Balatro-style goal unlocks** for base ship models and swappable ship components (sidegrades only).

---

## 6. Solar system and procedural generation

- Modelled on **our real solar system**. Sun, planets and moons are the same every run; orbits are fixed but each body's position along its orbit varies by seed.
- **Always moving, like Outer Wilds:** planets orbit, moons orbit planets, satellites orbit planets/moons, asteroids drift, bodies rotate. The map updates live.
- Each planet has its **own gravity**, based on real values but tuned to stay playable.
- Each faction has a **home planet**.
- Planet looks informed by real imagery (NASA textures as a base). Subtle atmospheric tints — orange haze on Mars, light blue on Earth; none on asteroids or moons.
- **Gas giants work like Outer Wilds:** fly into the atmosphere, surfaces inside appear and are landable, sky changes to the planet's gases once inside.
- **Neptune** is an underwater ocean world to explore.
- **Surfaces must be interesting, Outer Wilds-style** — each planet has its own unique things going on: caves, flyable canyons, large mountains, abandoned ruins, research labs, shipwreck sites, temple-like structures, menacing geography. Substantial **hilly, fun-to-drive terrain** on the Moon, Mars, etc. — not flat plains. Rover-drivable craters.
- **Weather:** storms, rain, sandstorms, wind, lightning in different colours.
- **Procedural placement:** a consistent library of preset feature assets (moon bases, mesas, hoodoos, rocks, trees, caves, cliffs) placed procedurally per seed; rock sets specific to each planet; bodies of water vary in shape (no cookie-cutter lakes); varied crater types, more common on moons; seeded enemy base locations (small/medium/large, scaling enemies, vehicles and weapons by size).
- create index off over 100 types of geologic formation, bodies of water, and other naturally occurring formations to then implement into the landscape of the planets.
- **Seeded asteroids** that can damage the ship or player.
- **Large roaming satellites.** An **enterable ISS** that sometimes has enemies depending on the ante.
- A **racetrack** with an achievement for reaching orbital speed.

---

## 7. Ships and flight

- **Flight model:** Outer Wilds-style, including autopilot and target-locking/velocity-matching.
- **NASA spacecraft are the default ships.** The **starter ship is based on the Apollo Lunar Module.** Each faction has ship tiers at roughly **1×, 4× and 8× the length of the Outer Wilds ship**. Ships carry light defensive guns.
- **Interiors:** correctly scaled to the player, non-glitchy, with their own gravity field. glass windows, good interior lighting (not boring).
- **Entry:** Outer Wilds-style **blue ring with a gravity lift** that pulls the player in; the hatch closes behind them. Player can open/close it any time, even in flight. The **starter lander uses belly (bottom) entry.**
- **Hangars** (Battlefront-style) on large ships and stations to fly or drive into.
- **Seat:** Outer Wilds-style buckle-in seat with a first-person seatbelt animation and a simple hydraulic sound. An unbuckled player can die in a bad enough crash.
- **Cockpit dash:** Outer Wilds-like direction display plus velocity, fuel and oxygen readouts. Cartoony yet technical and dynamic, inspired by real spacecraft cockpits, whimsical TF2 tone, easy to read and never in the way. Each gauge visually unique (e.g. fuel as a bar, oxygen something different). A small 3D model showing the ship's thrust direction. Build **several design iterations** for me to choose from, including one old-school black screen with green text.
- **Cameras:** a landing camera plus two "selfie stick" cameras on poles looking back at the ship. Looking down at a cockpit screen takes over the view; the player can switch cameras.
- **Takeoff** feels intense and immersive like a real launch: smoke, debris, heat, light, heavy thruster sound.
- **Crashes** are highly physics-based (BeamNG.drive / Outer Wilds): the ship bounces, sometimes breaks apart, with Outer Wilds-style crash audio. No cheesy crash music or sound effects.
- **Damage and repair:** damage is repairable until heavy damage makes parts fall off permanently. The player can exit a moving vehicle to repair parts mid-flight. **Ship glass** can be shot and broken from inside or outside — fairly strong (not one bullet, not unbreakable) — and is repairable.
- **Escape pods** only on the largest ships.
- **Salvage:** some ships carry salvage via grapple/crane, returned to base for points/unlocks.
- **Ships hold more oxygen than the suit;** the suit draws from the ship's supply.

---

## 8. Player, suit and traversal

- Player wears the **NASA spacesuit** asset. Being in space without the suit starts killing the player.
- **Jetpack** with Outer Wilds-style velocity matching, a satisfying sound and visible thruster light in each thrust direction.
- **Safety tether (space hook)** keeps the player from floating away without reeling them in.
- **Oxygen tanks** seeded in naturally explorable spots (enemy ships, satellites, outposts, planet points of interest, sometimes asteroids). Use on the spot to refill the suit, or haul back to the ship for a bigger yield. Balanced to be neither easy nor tedious.
- **Handheld radio receiver** whose noise hints at nearby bases, structures, planets, satellites and landmarks.
- **Stealth:** crouch, sneak up on enemies; ships draw attention; once spotted the player is hunted, within reason.
- **Vehicles:** Mars rovers rideable; dune-buggy rovers with rocket boosts; all driven in first person. Hostile robots drive their own rovers.
- **Starting base:** redone launch base built around the NASA launch pad model (no flags). The player has a **locker** here.
- *(Deferred: skateboard / skatepark — leave out for now.)*

---

## 9. Combat

- **One gun at a time** plus a **melee secondary slot**. The rope gun is exempt from the one-gun rule.
- unlimited ammo
- **Guns** are spoofs of famous video-game weapons, each with unique sound, feel, damage and properties. Keep the roster small: gravity gun, Uzi, AK-47, Desert Eagle, AWM sniper, silenced PPK, laser cowboy revolver, CoD-style ray gun, double-barrel laser shotgun, a throwable rock, and a few silly space-themed guns.
- **Melee weapons are rare and must be found:** baseball bat, mailbox, butterfly knife, a laser sword that cuts, other goofy items. Some gib, some send targets into absurd ragdolls.
- **Gibs and dismemberment like TF2:** extremities easily shot off with satisfying sounds. Shooting an enemy's weapon or arm **disarms** them; shooting off a leg **drops** them.
- **Hit feedback:** satisfying ping sound plus hit marker.
- **Destruction:** very satisfying — vehicles break into parts, epic crashes.
- **Enemy count:** substantially high; the world should feel populated and dangerous.

---

## 10. Factions and enemies

### Factions (placeholders — do not name them)
- **Faction A:** Soviet-spacesuit-inspired.
- **Faction B:** Chinese-astronaut-inspired.
- **Faction C:** JAXA/Japan-inspired.

Each faction:
- Built from real-world spacesuit references.
- **Three enemy tiers** of clearly different size and power, all ragdolling and losing limbs.
- Distinguished by **size and silhouette like TF2 classes** — unmistakable at a glance, never boxy. (Reference: the historical-figure bounty models from earlier builds worked well because they had dimension.)
- Distinct weapons plus special weapons and abilities, so fighting each feels unique.
- Ship tiers at 1×/4×/8×; a home planet; bases of three sizes.

### Unaligned threats
- **Aliens:** several types built from modified open-source aliens, each with unique properties, satisfying to kill.
- **Robots:** a few types with interesting aesthetics and unique fighting capabilities; some drive rovers.
- **Ancient humans with spears:** Earth only, seed-dependent.
- **Horror entities (2–3 types):** distinct from aliens. Lethal Company-like; Enderman-style staring from a distance then vanishing. Unsettling music on arrival. Can kill the player and are very hard to kill, so escape is the main mechanic. Hunt only occasionally, more often as the ante rises — never excessive.

---

## 11. Bounties and diplomacy

- Bounties found on **wanted posters** (make them look good) posted by specific factions; accepting adds a map waypoint.
- Targets can pilot spaceships and hang out with other factions (never the faction that wants them).
- **Tie-up string gun:** 3 shots per run. Tied targets can be carried and dropped.
- Delivering targets **alive** earns points and **Diplomacy** (goodwill) with the posting faction. Killing them gives fewer points and no Diplomacy.
- **Historical-figure bounty targets** can be gibbed after being knocked out; completing their bounties **unlocks them as playable characters**.

---

## 12. Exploration rewards and loot

- **Points** for visiting planets and landmarks.
- **Ground satellite dishes** (NASA dish assets): points for switching them on; bonus for all dishes on a planet. Bonus for clearing all bases on a planet.
- **Damaged satellites** (seeded): repairing them gives points but may switch on a signal that attracts enemies. Multiple parts to repair per satellite; repairs must be quick, not tedious. Some satellites (size permitting) can be entered for loot, e.g. after repair.
- **Chests** (Fortnite-style, seeded) with one random item: anything from a rock to art, weapons, or permanent/Unusual cosmetics.
- **TF2-style rarity tiers** for cosmetics on clothing and weapons, with an Unusual-like top tier featuring special effects (e.g. fire).
- **Paintings:** the starter ship has wall spots for famous historical paintings (public-domain sources) as unlocks; ultra-rare paintings spawn randomly in different seeds.
- **Permanent loot** (art, rare cosmetics) must be carried back to the player's locker at the starting base to keep it.
- **Casino:** randomly placed, with **only blackjack and slots**, for wagering points or trading items.

---

## 13. UI and collections

- Whimsical, somewhat goofy, space-themed, TF2-like. Never dull.
- **Notifications** (top-right): capped in number, not spammy, visually distinct by type, slightly larger goofy text.
- **Points:** displayed with commas and a slot-machine-style count-up animation.
- **Notebook** on the main menu (Paper Mario-style) logging found items and defeated enemies, with found-item notifications, plus front pages for rare artwork and rare cosmetics.
- **Collections** shown as goofy **trading cards** with cartoonish item art and holographic detail/reflection.

### Pause menu
Opened with Esc on keyboard or Start on controller; pauses the game (time scale and audio) and is fully navigable with keyboard, mouse or controller. Options:
- **Resume.**
- **Controls / button mapping:** separate tabs for **keyboard & mouse** and **controller**. Show every action with its current binding; select one and press a new key/button to rebind it (Input System interactive rebinding). Detect conflicts and let the player swap or cancel. "Reset to defaults" per device. Show controller glyphs matching the device (Steam Deck/Xbox/PlayStation). Save bindings so they persist between sessions.
- **Restart:** restart the current run (new run with the same seed, or a new seed — offer both) after a confirmation prompt.
- **Quit:** quit to main menu or quit to desktop, each with a confirmation prompt.

---

## 14. Audio

- **Music:** professional-grade, upbeat electronic built around funky bass guitar (house, rally house, modern disco). Bass references: "Musty (Disco Lines Remix)" by Shoreline Mafia and "1997" by Beave. Never annoying or repetitive.
- **Adaptive mixer:** music blends continuously like a club DJ at a steady tempo, never cutting off abruptly, and intensifies at higher antes and in intense moments. Horror entities bring their own unsettling layer.
- **Feedback sounds** grounded in behavioural psychology: hit ping + marker; casino-style coin rack-up with flashy count-up when points rise; distinct reward sounds for art, cosmetics, ammo and new weapons; one highly satisfying **repair sound** shared by ship and satellite repairs.
- Heavy takeoff sound; keep the ship-flying sound feel of earlier builds. **Remove** cheesy landing sounds, crash music and similar effects.

---

## 15. Build order

The build has two phases. **Phase 1 is the refined core** — the parts of the game I most want to get right: **planets, the solar system, the ship's design, launch, cockpit view, ship navigation, jetpack navigation, combat and gibbing.** Everything else waits for Phase 2.

**Phase 1 rule: refine before expanding.** Each Phase 1 milestone must feel finished and polished — not "working" or "good enough for now" — before moving on. Don't add Phase 2 features (factions, ante loop, loot, casino, etc.) while Phase 1 is in progress, beyond the bare minimum a Phase 1 system needs to be tested. If a Phase 1 system isn't fun or convincing yet, iterate on it rather than moving past it. At the end of Phase 1 there is a **sign-off gate**: stop, give me a playable build and a playtest checklist covering every Phase 1 area, and wait for my approval before starting Phase 2.

Start with the three combat milestones below, exactly as specified, in a simple test arena (flat ground, a few walls, a first-person shooter controller, keyboard/mouse and controller input, and the full pause menu from section 13 so playtests can rebind, restart and quit).

### Milestone 1 – Breakable playtest characters

Build a placeholder humanoid ("mannequin") from primitives, one mesh per body part, each parented to the matching bone of a Unity **Humanoid** rig so it animates normally. Body parts (14):
- Head, Torso
- L/R Upper Arm, L/R Forearm, L/R Hand
- L/R Thigh, L/R Shin (leg), L/R Foot

Each part has: its own collider (hitbox), its own HP, a parent part, and a stump-cap mesh that appears on the parent when the child is lost.

Rules:
- Limb HP is **low**. Limbs should come off in 1–3 rifle hits — "almost instant."
- Losing a part also detaches everything below it (thigh takes shin + foot; upper arm takes forearm + hand). The detached chain flies off as one gib that can still split further if shot in the air or on the ground.
- The torso can be destroyed. The **only** death condition is the head being destroyed. A character can be reduced to just a head and still be alive.
- Degraded movement states, driven by what's left:
  - One leg gone: limp/hop, slower, can't sprint.
  - Both legs gone: crawl using arms.
  - No legs and no arms: torso wriggle/inch forward.
  - Torso gone (head only): head rolls/hops slowly. Can't shoot.
- Detachment: unparent the mesh, add a `Rigidbody` + collider, and apply an exaggerated impulse along the bullet direction plus random spin.

### Milestone 2 – Gibs that feel amazing (TF2 energy)

- Over-the-top impulse and tumble; bouncy `PhysicMaterial` on gibs.
- Blood burst particles at the wound and a trailing spray on the gib.
- Blood decals (URP Decal Projector) on floors/walls where gibs land.
- Hitstop (40–80 ms) on the shooter when a limb pops; small camera shake.
- Distinct, punchy "pop" sound per limb removal (placeholder SFX fine).
- Gibs persist ~30 s then fade; pool them and cap active gibs for Steam Deck performance.
- Optional toggle: brief slow-mo on headshot kills.

### Milestone 3 – Weapons and disarming

- Hitscan rifle (the AK-47 spoof) first: full-auto, recoil, spread, reload, ammo, tracer, muzzle flash, impact effects. Also a pistol for testing. *(Note: the final game allows one gun plus melee — the pistol here is a test weapon; flag how it fits the final loadout rules.)*
- Weapon data in **ScriptableObjects**: damage per body part, fire rate, spread, recoil, magazine size, reload time, etc.
- Use CC0 models I'll place in `Assets/ThirdParty/Weapons/`. Candidates: Quaternius "Ultimate Gun Pack" (CC0), stein-indie "Free Classic Weapons Pack" (CC0, includes AK-47), 3dmodelscc0 "Guns & Explosives" (CC0). If no model is present yet, use a primitive placeholder with the same sockets (grip, support hand, muzzle, magazine) as empty child transforms.
- Disarming:
  - Losing the gun hand or forearm drops the weapon as a physics object.
  - Shooting the weapon itself knocks it out of the hands.
  - Dropped weapons can be picked up with any remaining hand.
  - One-handed firing: big spread/recoil penalty, slower reload (or no reload if the one hand is needed — make this a toggle).
  - No hands: can't fire. Can still move and take cover.

### Milestone 4 – Foundation
Seeded RNG, save system, data-driven content pipeline, Steam Deck performance budget. Keep this lean — just enough to support the rest of Phase 1.

### Milestone 5 – Solar system
- Sun, planets and moons modelled on the real solar system, always moving like Outer Wilds: orbits, moons around planets, rotation, drifting asteroids, live-updating map.
- Per-planet gravity (real-based, tuned to stay playable). Seeded orbit positions.
- Refinement bar: the system feels alive and readable from the cockpit — flying between bodies is a pleasure, scale feels grand but travel never drags.

### Milestone 6 – Planets
- Every planet and moon looks distinct and interesting, informed by real imagery: colours, textures, subtle atmospheric tints (orange on Mars, light blue on Earth; none on moons/asteroids).
- Gas giants enterable Outer Wilds-style, with landable surfaces inside and skies that change to the planet's gases. Neptune as an explorable ocean world.
- Hilly, fun-to-drive and fun-to-land-on terrain (no flat plains), varied craters, caves, canyons you can fly through, mountains, and procedurally placed planet-specific features (rocks, mesas, hoodoos, cliffs) per section 6. Weather on the planets that have it.
- Landmarks, bases and points of interest can be placeholders here; the **terrain, look and feel** of each body must be refined.
- Refinement bar: landing anywhere gives you something you want to explore; each world is recognisable at a glance.

### Milestone 7 – Ship design
- Starter ship based on the Apollo Lunar Module: rounded/bowl-like shapes, curved halls, interesting glass windows, good interior lighting, correctly scaled interior with its own gravity field, no glitches.
- Belly entry with the blue ring and gravity lift; hatch closes behind the player and can be opened/closed any time, even in flight.
- Buckle-in seat with first-person seatbelt animation and hydraulic sound.
- Damage, repair and breakable glass per section 7.
- Refinement bar: walking around inside feels like a real, cosy little spacecraft you want to protect.

### Milestone 8 – Launch
- Starting base built around the NASA launch pad model (no flags).
- Takeoff that feels intense and immersive like a real launch: smoke, debris, heat shimmer, light, camera shake, heavy thruster sound.
- Refinement bar: every launch gives a little rush, even the tenth time.

### Milestone 9 – Cockpit view
- First-person cockpit per section 7: Outer Wilds-like direction display, velocity, fuel and oxygen readouts, each gauge visually unique, a small 3D thrust-direction model, readable and never in the way.
- Build **several dash design iterations** (including the black-screen/green-text one) and show me screenshots/gameplay of each so I can pick.
- Landing camera plus two "selfie stick" cameras; looking down at the cockpit screen takes over the view and lets the player switch cameras.
- Refinement bar: I can read everything I need at a glance mid-flight, and the cockpit looks great.

### Milestone 10 – Ship navigation
- Outer Wilds-style flight with real inertia, autopilot, target locking and velocity matching, landing on moving bodies.
- Physics-based crashes (BeamNG.drive / Outer Wilds): bouncing, parts breaking off, Outer Wilds-style crash audio; an unbuckled player can die in a bad crash. No third-person view.
- Refinement bar: flying is skill-based, learnable and satisfying; landing on a spinning moon feels like an achievement, not a fight with the controls.

### Milestone 11 – Jetpack navigation
- Suit with jetpack (basic oxygen/fuel only, enough to test), Outer Wilds-style velocity matching with ships and bodies, real inertia, safety tether.
- Satisfying jetpack sound and visible thruster light in each thrust direction.
- Exiting a moving ship to repair it mid-flight.
- Refinement bar: floating around a ship or moon in zero-G feels free, precise and a little scary.

### Milestone 12 – Combat and gibbing polish pass
- Revisit Milestones 1–3 inside the real game: on planets, in low gravity, in zero-G and inside ships. Gibs should behave convincingly (and hilariously) under each planet's gravity and in space.
- Swap the mannequin for one proper enemy model built on the dismemberment system to prove it works with real art.
- Refinement bar: every fight produces a moment worth laughing about; limb pops, disarms and leg shots all read instantly.

### ⛔ Phase 1 sign-off gate
Stop here. Deliver a playable build plus a playtest checklist covering every Phase 1 area, and wait for my approval.

### Phase 2 (only after sign-off)
13. **First real faction** (Faction A, three tiers), more weapons and melee.
14. **Ante loop:** timer, scaling, upgrade screen, perk draft, scratch-off, bonus missions, points.
15. **Planet content:** landmarks, bases, rovers, oxygen tanks, dishes, satellites, radio receiver, stealth.
16. **Expansion:** Factions B and C, aliens, robots, horror entities, bounties/Diplomacy, ISS, hangars, chests, rarity, casino, notebook, trading cards, music system, leaderboard.

---

## 16. Open questions to ask me before or during the build

- Official faction names (do not invent).
- Exact ante timer length and what ends an ante.
- Which weapons and melee items ship in the first playable.
- Whether the skateboard returns later
