# Milestone 9: Cockpit view

13 Nov 2026. Branch `claude/milestone-9-cockpit` (built on Milestone 8).

**Nothing in this milestone has been run in Unity.** The rules were tested outside the engine, the code was compiled against stand-in Unity libraries, and the dash pictures in `docs/art/previews/` are drawn by a small offline program from the same drawing lists the game uses. They are not screenshots of the game.

## What changed

**A dash on the desk in front of the pilot's seat.** It shows, left to right:
- **Air** as a round dial with a needle.
- **Fuel** as an upright bar.
- **The direction ball**: a dot showing which way you are really moving compared with where the nose points (hollow ring = moving backward), in the spirit of Outer Wilds.
- **Speed**, and what it is measured against ("SPEED vs MOON"). Near the ground, **height** and **climb or sink**.
- **A small 3D lander** (drawn as a wire model in perspective) with a jet for each direction you are pushing, including when the ship is braking for you.
- **A small live camera screen**.
- One line for the single most important notice (fuel out, fuel low, air low, refilling, engines building, matching speed).

**Four designs to choose from**, switchable in flight with **N** (controller: click the right stick):
1. Flight deck: cream panel, round instruments, screws, a "PROPERTY OF NOBODY" placard.
2. Arcade: chunky colour blocks.
3. Glass: thin bright lines on dark glass.
4. Green screen: black screen, green text, warnings in inverse video.

All four show the same facts in the same places on purpose, so choosing one is a choice of look, not of what you can read.

**Three ship cameras.** A landing camera under the belly looking straight down, and one on a pole each side looking back at the ship. Press **C** (D-pad down) and your head tips down to the screen, which then fills the view; **V** (D-pad right) changes camera; **C** again looks back up. A hard knock brings your eyes straight back up. You never leave first person: these are the ship's own cameras, with real hardware on the hull (made in Blender by the look thread: two braced booms and a belly camera, each with a small red record lamp).

**Fuel and air, just enough to give the gauges something true to show.**
- A full tank is 7 minutes of full push. Boost costs 3.5 times as much for 2.5 times the push. Braking with match-velocity costs fuel too.
- An empty tank leaves a weak reserve push (30%) and never takes your steering, so you cannot be left helpless.
- The pad refills in 6 seconds. Standing still anywhere else refills slowly (45 seconds), so you cannot be stranded.
- Air only runs down if a window is broken (about 7 minutes with one window out). A sealed cabin keeps its air for now, because there is nothing you could do about it until the suit exists in Milestone 11.

## Why, and which principles it serves

- **Fun core first (1) and cognitive load (7):** one notice at a time, by urgency; numbers are eased and rounded so they do not flicker; every gauge has its own shape, so you know which is which without reading.
- **Learnable challenge, no hidden rules (3):** the speed says what it is measured against; the dash shows the key for looking down; an empty tank says exactly what to do.
- **Legible risk and reward (5):** boost visibly drains the bar faster than it pays back.
- **Agency (2):** fuel makes "burn hard or coast" a real choice without ever taking control away.
- **Ethical design (9):** no timers that punish you for waiting; refilling is free and always possible.

## Choices you should know about

1. **Which dash is the default.** I recommend 1, Flight deck. It is the easiest to read and the most "cosy little spacecraft". The decision card in the thread sets it.
2. **Looking down is a button**, not "look at the screen with the stick". On a Deck a button is quicker and cannot happen by accident. Say if you want the gaze version as well.
3. **The small 3D ship is a wire model drawn on the dash**, not a separate lit model. It reads clearly at that size and costs almost nothing.
4. **Tank size (7 minutes) is a first guess** and needs a real flight to judge. It is one number on the Cockpit asset.
5. **The old stand-in screens on the desk are still there** beside the new panel. I have asked the look thread for a proper housing for the dash; until then the panel sits on the desk on its own.

## What was tested

- **1,035 rule tests pass** outside Unity (5 skipped, as before). New for this milestone: fuel and air sums, refilling, the reserve push, reading rounding and easing, notice priority, every design staying on its panel and under its triangle and word caps, every design showing the same facts, no word smaller than 14 units, a warning blink changing at most 3 words, the picture fingerprint, the camera ring, the look-down timing.
- **The game code and all PlayMode tests compile** against stand-in Unity libraries. (The one known gap there is unrelated: the stand-in editor library has no `AssetPostprocessor`, which the audio import rule uses.)
- **The four designs were drawn offline** in four situations (on the pad, climbing, cruising, in trouble) and looked at. Fixes made from that: stacked height label, larger text, warning banner clear of the fuel percent.

## Still unverified (needs Unity on your machine)

- Whether the panel sits well on the desk, and is readable on the Deck's screen from the seat. This is the biggest unknown.
- The 3 new PlayMode tests (81 in all) have not been run.
- The camera pictures: framing from the poles, whether the boom is out of shot, the landing view.
- The cost of the little camera screen on a Deck (see "Cockpit" in `docs/performance_budget.md` for what to read off).
- Whether 7 minutes of fuel feels right.
- No gameplay footage: the build prompt asks for screenshots or gameplay of each design. The offline pictures stand in until the game is run.

## Agent review

**game-designer.** Applied: braking now costs fuel and shows a jet; an empty tank no longer weakens steering; slow refill anywhere when landed; boost made dearer than its gain; fuel warnings rank above air; sealed cabin keeps its air until the suit exists; the dash shows the look-down key and what speed is measured against; a knock returns your eyes to the windows. Flagged, not changed: the four designs share one layout (deliberate, choice 1 above); the look-down is a button (choice 2).

**unity-reviewer.** Applied: the dry-tank test waits for the reading and checks the right dial; desk panel enlarged (0.84 m to 1.1 m wide) and lowered, minimum text size 14, letter sharpness lowered so text does not sparkle; camera feed depth 24 bits; an unused dial removed; per-paint garbage removed; config statics reset between runs. Open: a modelled housing for the panel (asked for).

**deck-performance.** Applied: the little screen is drawn without bloom, grade or shadows and only sees 30 km; it only runs while seated, every 6th frame, at 240 x 150; words keep their place so a blink changes 1 to 3 words instead of 13 to 16; the mesh is only rebuilt when the picture changes; the dash is on its own layer so the ship's cameras never film it. Not done: preparing the cameras at load (a one-off hitch the first time each is used). Its estimates table is in `docs/performance_budget.md`.

## Codex (ChatGPT) work checked

None new in this milestone. A Codex review of this pull request is requested in a comment; what it finds, and what is used or rejected, will be added here.
