# Milestone 11: Jetpack navigation

14 Nov 2026. Branch `claude/milestone-11-jetpack` (built on Milestone 10).

**Nothing in this milestone has been run in Unity.** The rules were tested outside the engine and the game code and PlayMode tests were compiled against stand-in Unity libraries. Whether floating feels "free, precise and a little scary" can only be judged by playing it.

## What changed

**The suit has jets.** Off the ground, the stick (or W A S D) pushes you forward, back, left and right; hold Jump for up and Left Ctrl (B / Circle) for down. Let go and you keep exactly the speed you had: nothing slows you but your own jets. The jets lift you on the Moon and Mars. On Earth they only soften a fall. Hold Left Shift (left stick pressed in) for gentle jets when you need small, careful moves.

**Floating free.** High above a body, or where almost nothing pulls, there is no "down": you can look any way up and roll over with Z and C (the bumpers). Near the ground you are stood head-up again. The change keeps the way you are looking exactly, and a short notice tells you it has happened.

**Match Velocity for the suit** (hold F / left trigger). It stops you against the ship if the ship is within 60 m or you are looking at it, otherwise against the ground below. What it picked is fixed when you press and shown on screen, and your speed reading is always measured against the same thing ("3.2 m/s vs SHIP", "vs MOON GROUND").

**A safety tether.** 40 m of line from the ship's belly to your harness. It stops you at its length and does nothing else: it never reels you in and never pulls while slack. It clips on by itself when you leave a ship that is not standing on the ground. X (D-pad right) takes it off or puts it on, and you can only clip on within 40 m. You see it as a bright orange line that hangs when slack and goes straight when it holds you.

**Leaving a moving ship to mend it.** With no ground below, the lift now lowers you clear of the belly and lets go; you keep the ship's speed. Mending from outside now needs you within 3.5 m (it was 7 m, because there were no jets), so you fly up to the dish or a thruster. While you hold the mend button the jets hold you beside that part of the ship by themselves, even if the ship is tumbling. Fly back under the blue ring to go aboard.

**Jets you can see and hear.** Six pairs of small pale-blue jets round the backpack, each lit for the direction it pushes: going backward lights the pair low in front of you, going up lights the pair by your hips. The rim of the visor glows on the side a jet is burning. The sound is a soft rush of gas from inside the helmet, with a "pfft" at the start of each push.

**Suit air and jet fuel, just enough to test.**
- Air: 8 minutes outside. At nothing you have 14 seconds, counted down on screen, then it is fatal.
- Jet fuel: 80 seconds of full push. Drifting costs nothing.
- With the jet tank dry the jets burn your air instead (a full air tank is 40 seconds of push), so you can always move, at a price. The suit says so the moment the tank runs dry, before you pay.
- Both fill in 4 seconds once you are aboard. The air comes out of the ship's tank (a full suit is 6% of it). If the ship has lost its air you breathe from the suit aboard too.

**The suit can be hurt.** Hitting something at more than 9 m/s winds you (you lose the controls for a moment). More than 26 m/s kills, with the cause in plain words. The speed reading turns red above half that. Coming up short on the tether at speed can wind you but never kills.

**A small readout, bottom left, only outside:** air as a ring, jets as a bar (the same shapes as the ship's dash), speed and what it is measured against, the tether, and one notice at a time.

## Why, and which principles it serves

- **Fun core first (1):** real inertia with a reliable way to stop is the whole pleasure of a spacewalk. The match button and the tether are the two things that make drifting safe enough to enjoy.
- **Agency and trade-offs (2):** tether on is safe but short; tether off is free and can lose you. Burning air to move is always possible and always costs.
- **Learnable challenge (3) and legible risk (5):** every limit is on screen before it bites: what speed is measured against, a red speed before a fatal one, the air countdown, "JETS NOW BURN YOUR AIR" before the first push on air.
- **Cognitive load (7):** one notice at a time, most urgent first; the readout is only there outside.
- **Varied intensity (8):** quiet drifting, then the small fear of the line running out.

## Choices you should know about

1. **Jets burn air when the jet tank is dry.** Not in the build prompt. It means you are never unable to move. Say if you would rather an empty tank simply left you drifting.
2. **Suit knocks can wind or kill you.** The prompt does not ask for this; it is what makes floating "a little scary". Fatal speed (26 m/s) is a first guess.
3. **The tether clips on by itself** when you leave a ship in flight. The design review would rather it were your choice, with a prompt. I kept automatic, because forgetting it once on a first spacewalk loses the run.
4. **The tether stops you dead** (and winds you if you were fast). It does not stretch or swing.
5. **Mending from outside now needs 3.5 m**, down from 7 m. This changes Milestone 7 behaviour on purpose.
6. **Jet fuel refills free aboard.** It does not draw on the ship's fuel yet.
7. **Four safety nets at once** (auto-clip, auto-hold while mending, air as fuel, a 14 second grace). The design review warns this may be too gentle to be scary. Your play will tell.
8. **Your saved key bindings from earlier builds are not checked against the new keys** (X, F, Z, C, Left Ctrl on foot). If something does not respond, use Controls > Reset to defaults.

## What was tested

- **1,077 rule tests pass** outside Unity (5 skipped, as before). New for this milestone: tank times, air as fuel, the grace countdown, filling aboard and what it costs the ship, the jets and which nozzle each push lights, the puff, velocity matching never overshooting, what it picks, the tether at four speeds (stops at its length, leaves you still, keeps sideways motion, never pulls on someone coming home), the drawn line, standing or floating without flicker, knocks, and the suit's words.
- **The game code and all PlayMode tests compile** against stand-in Unity libraries. (The one known gap is unrelated: the stand-in editor library has no `AssetPostprocessor`.)
- A real fault was found by the rule tests while building: the first tether pulled you back in after a fast drift. It now only ever removes speed away from the ship.

## Still unverified (needs Unity on your machine)

- **How it feels**, above all: the jets' strength, whether floating free is comfortable to look around in, the change between floating and standing.
- The 5 new PlayMode tests (91 in all) have not been run.
- Whether the view shakes when you hang on a taut tether while pushing outward.
- Walking after a jump on high ground and on the small moons (Phobos, Deimos).
- Leaving the lift from a ship moving low over a planet: you should stay with it.
- The jets, the visor glow and the tether line: none has been seen on screen.
- The sounds.
- The cost on a Deck (see "Spacewalk" in `docs/performance_budget.md`).

## Agent review

**game-designer.** Applied: off the ground nothing but the jets changes your speed (an earlier version let you "lean" in mid-hop, which broke real inertia) and the jets answer at once; a notice when you start or stop floating; the air cost of an empty jet tank is said before it is paid; the tether winds you if it stops you fast; Match Velocity's target is fixed while held; a gentle-jets button; mending holds you against the part beside you, not the ship's middle; air-as-fuel made dearer (75 to 40 seconds) and fatal speed raised (22 to 26 m/s). Flagged for you, not changed: choices 1 to 7 above.

**unity-reviewer.** Applied: the puff and the tether's "taut" are latched so they are not played twice or missed between physics steps and frames; the tether only moves you for a real overrun, so the view should not shake on a taut line; the real ground is asked of the planets (not the body's nominal radius), and touching ground that pulls always stands you up; standing up waits if it would put you through the hull; a winding blow now costs the controls; the fuel bar's corners no longer stretch; tether distances all measured from the same point. Noted: old saved bindings (choice 8).

**deck-performance.** Applied: gauges and colours no longer rebuild the readout every frame; the visor bands are shallower (150 to 90 px); the readout's words are only rebuilt when they change. Not done: building the suit's sounds off the main thread (a one-off at run start). Its table is in `docs/performance_budget.md`.
