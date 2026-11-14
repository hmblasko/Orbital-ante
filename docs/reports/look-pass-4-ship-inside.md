# Look pass 4: long legs, a real console, a working cabin

Status: **built, not run in Unity.** Everything below was checked outside the engine: 1,124 rule tests pass, and the pictures were drawn from the game's real shapes and its real baked light. They are not Unity screenshots.

Pictures:
- `docs/art/previews/lander_long_legs.jpg`: the lander on its longer legs.
- `docs/art/previews/dash-1-flight-deck.png`: the screen, four moments (on the pad, climbing, matching speed, low on fuel).
- `docs/art/previews/cabin_hardware.jpg`: the roof and orrery, the air rack, the suit stand and computer, and the view up from the seat.
- `docs/art/previews/cabin_to_lockers.jpg`: the back of the cabin.

## What Matthew asked for (14 Nov 2026)

1. "This ship's legs need to be significantly longer."
2. "I like the little screen on the dashboard, but it should have an interesting border around it and flow into the dash naturally like a dashboard in a spaceship would. Also get rid of the cheap wording around the dashboard and make the dashboard look more interesting."
3. "The stats need to be displayed better on the ship. I want them displayed on the dashboard and in an interesting way, like in Outer Wilds."
4. "Make the interior of the ship be more interesting generally and have more of a space theme instead of just a few lockers."
5. "I don't like the hatch lever. I really want just to be able to look at the hatch and press E to open."

## What changed

**Legs.** The lander stands 5.6 m off the ground (it was 2.6 m). The lift reaches the ground under them, and now rides it in 1.4 seconds: at the old speed the longer ride took over two.

**Console.** The screen sits in a modelled console (`tools/blender/build_console.py`): a hooded brass-edged bezel that grows out of the desk, with wings either side that carry a glass tube of fuel, a round air dial, five picture lamps (Lock, Auto, Match, Fuel, Air), switches and a throttle. The tube, the needle and the lamps are real moving parts driven by the ship (`ConsoleGauges`, `ConsoleInstruments`).

**Screen.** Seven words only: speed, its unit, what it is measured against, height, climb or sink, the camera's name, and one notice. Everything else is a picture. New in this pass: **fuel and air are on the screen itself**, either side of the direction ball. Fuel is a slim tube with a drop over it; air is a column of five bubbles that shrink away from the top. Both turn red and blink when low. (Why they had to be on the screen is under "Agent review".)

**Cabin.** `ShipProps.Hardware()`: six wooden ribs up the dome to a brass crown, a hanging orrery of the six nearest worlds under the ceiling lamp, a switch panel either side of the roof window, a rack of air bottles with a copper pipe and gauge, the spare suit pack and helmet on a stand, a valve pipe, an extinguisher. One locker bay is now the ship's computer: a green screen, tape reels, lamps and a paper slot.

**Hatch.** The lever and its post are gone from the cabin, the layout and the walking rules. Inside, you look at the hatch and press the button. Outside, you can now do the same from anywhere under the ship between its legs (the reach follows the legs).

**Things the longer legs would have broken, fixed:**
- Mending the thrusters from the ground. They are now 5.1 m above your eyes, and on Earth the suit's jets cannot lift you. The reach for mending from outside is never less than it takes to reach them (`Ship.MendReachOf`).
- The pole cameras aimed above the middle of the ship; they aim lower so the legs are in the picture.
- The launch's ground effect was measured from the engine bells, so a ship standing on the pad counted as 3 m up. It is measured from the feet.

## Why, and which principles it serves

- Milestone 9's bar is "I can read everything I need at a glance mid-flight, and the cockpit looks great". Fuel and air on real instruments is the look; fuel and air on the screen is the glance. (Principle 5, legible risk and reward; principle 7, cognitive load.)
- Milestone 7's bar is "a real, cosy little spacecraft you want to protect". The hardware gives the room a job without taking the warmth out of it. Nothing new can be used, picked up or unlocked, so no Phase 2 feature has crept in.
- Red is kept for real warnings. Scenery dials have dark needles and the pilot lights are amber, green and blue, so nothing decorative looks like an alarm. (Principle 3: no hidden rules, and no false ones.)
- Problem: the stats were words on a flat panel. Hypothesis: pictures that move, in a console that looks built, read faster and look better. Smallest test: fly one pad-to-orbit-to-Moon trip without reading a word. Acceptance: Matthew knows his fuel, air and speed at every moment of it. Not yet tested.

## What was tested

- 1,124 rule tests pass outside Unity (`dotnet test`). New or changed in this pass: the screen shows fuel and air as pictures that follow the tanks, never add or drop corners, and blink when low; the Air lamp burns steadily when the air is gone; the pole cameras aim below the cabin floor; no lever stands in the cabin and the seat is the only thing to walk round; the hardware leaves the front and roof windows clear from the seat, never stands where you walk, and none of its glows light the room; the baked light file and the lit console fit the cabin.
- The game code compiles against Unity reference assemblies.
- The pictures were looked at after every change.

## What is not verified

- **Nothing here has been run in Unity.** Not the lift ride, not the reach for the hatch or for mending, not the instruments moving, not the frame rate.
- A topple off the long legs drops the cabin about 3.9 m. What that does to the ship and the pilot needs the engine.
- Whether the bubbles read as "air" without a word. The dial on the console, the blue of the suit's air ring and the AIR LOW notice all say the same thing, but only a first-time player can answer this.
- Frame cost is counted from the files, not measured.

## Agent review

**unity-reviewer.** Applied: the instruments froze at their last reading when you left the cabin, and now follow the tanks with nobody aboard; the shining parts of the console were being lit by the sky through the hull, and now use an indoor material of their own, destroyed with the console; the thrusters could not be mended from the ground (above); a doubled comment. Confirmed with the designer's finding: on the chosen screen, fuel and air vanished if the Console model was missing. Checked and fine: lifetimes, garbage, missing sockets, pivots, layers, the hatch with one mending place, the lift's reach, Unity 6 names.

**game-designer.** Applied: fuel and air on the screen (the console's own tube and dial are 58 degrees off the view's centre, outside the Deck's picture from the seat); the Air lamp's steady "out" state; the fuel tube's red marks now start at a fifth, where FUEL LOW fires, not at an eighth; decorative needles and lights no longer red; the lift ride, the hatch reach, the pole camera aim and the launch ground effect (above). Left, with reasons: the screen does not show which key looks down at the camera (the other three styles do; a place for it is Matthew's call, below); when both tanks are low the notice says only FUEL LOW (the notice rules belong to the core build; the lamps and both screen gauges do show both). Left for Matthew: the console's throttle, key and guarded switch look usable and are not (below).

**deck-performance.** Nothing threatens the frame: about 75,000 triangles and 45 draw calls with the whole ship in view from aboard. Applied: no rod or wire in the cabin is thinner than 12 mm now (the orrery's arms, the moon's wire, the fairy-light wire, the lantern's handle, the radio aerial and the tape were finer and would have crawled on the Deck's screen); the budget rows. Left as follow-ups, written into the budget: the cabin and console are drawn whenever the ship is in view, from outside too (hide them beyond 30 m); the model library builds a console mesh that is not drawn (0.36 MB).

## For Matthew: three calls

1. **Should the throttle on the console move with your thrust?** It is scenery now. Recommended: yes. It is a small job and makes the console feel alive.
2. **Should the camera view carry the notice?** When a camera fills the view, a warning such as FUEL LOW is not shown. Recommended: yes, one line at the bottom.
3. **Keep the glow-in-the-dark stars on the ceiling?** Still open from the last pass.

## Try it on the Deck

1. Open a terminal in `~/Orbital-ante` and run `git fetch`, then `git checkout claude/playtest`, then `git pull`.
2. Open the project in Unity and wait for it to finish importing.
3. Press Play. You start seated on the pad.
4. Look straight ahead. Fuel is the orange tube left of the ball; air is the column of blue bubbles right of it.
5. Look left and right along the console: the big fuel tube and the air dial should agree with the screen.
6. Press F3. The Ship line should say "light baked". If it says anything else, tell me what.
7. Unbuckle, stand up and look round: ribs, the orrery overhead, the panels by the roof window, the air rack, the suit stand, the computer in the lockers.
8. Look down at the hatch and press E (Y on the controller). Ride the lift down. It should take about a second and a half.
9. On the ground, walk a few steps out from under the ship, look up at the belly and press E. The hatch should answer from anywhere between the legs.
10. Tell me if anything thin (the fairy-light wire, the orrery's arms) shimmers as you move your head.
