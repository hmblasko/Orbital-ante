# Look pass 3: the cabin as a place of refuge

Status: **built, not run in Unity.** Everything below was checked outside the engine: 1,051 rule tests pass, and the pictures were drawn from the game's real cabin shapes and its real baked light. They are not Unity screenshots.

Pictures: `docs/art/previews/cabin_cosy.jpg` (now) and `docs/art/previews/cabin_before.jpg` (before).

## What Matthew asked for

"Can you revamp the interior. Think cozy but space vibes. The ship is the place of refuge like in resident evil doors or in outer wilds ship. Cozy lighting, but still space vibes" (13 Nov 2026).

## What changed

**Light.** The cabin was lit evenly and brightly by one white ring in the ceiling. It is now lit low and warm, in pools:
- the ceiling ring is dimmer and warmer, and only fills in;
- the four wall lamps are frosted globes on brass arms, so they glow from wherever you stand (the old ones showed a grey hood from eye height);
- a string of 63 small warm bulbs loops round the dome above the wall lamps, stopping well short of the front window on both sides;
- a storm lantern stands on the lockers.
The blue of the lift well and its ring is unchanged, so the cool "space" light now stands out against the warm room.

**Colour.** Oat-coloured wall panels, a dusk-blue dome, a walnut desk top, warmer deck plates, a brown seat shell. The window surrounds are pale so the windows read against the dark dome.

**Things from home** (all in `ShipProps.Comforts()`): a woven rug between the lift well and the lockers; on the locker tops a plant, a stack of books, the lantern, a small radio, a flask and a mug, held in by a stretchy cord; a pin board of postcards under the right-hand wall lamp; a rolled sleeping bag strapped to the left wall; a mug and a cactus at the far ends of the desk; glow-in-the-dark stars stuck to the back and sides of the dome.

**Dash housing.** The housing round the cockpit screen (built earlier today) is now lit by the cabin's own lamps: the bake tool copies it out of the Lander model, lights it with the room, and stores it as `Models/LanderCabin`. The desk's switches and hand controllers moved out from under it.

**Tools.** `tools/bake` bakes the cabin (about 25 seconds) and `tools/blender/preview_cabin.py` draws pictures of the result, so the cabin can be judged without Unity.

## Why, and which principles it serves

- Milestone 7's bar is "a real, cosy little spacecraft you want to protect". The old cabin was clean but clinical. (Principle 1, fun core; principle 8, varied intensity: a calm room makes the dangerous outside read as dangerous.)
- Nothing new can be used, picked up or unlocked, so no Phase 2 feature has crept in. The wall either side of the front window is left bare for the paintings of section 12.
- Nothing soft stands where you walk, and nothing stands up into the front window; rule tests hold both. (Principle 7, cognitive load; and Matthew's "one large window".)
- Problem: the cabin does not feel like a refuge. Hypothesis: low warm light in pools, plus a few lived-in things, does more than any amount of extra hardware. Smallest test: sit in the seat, then stand at the hatch and turn round once. Acceptance: Matthew says it feels like somewhere he wants to come back to, and he can still read the dash, find the lever and see out. Not yet tested.

## What was tested

- 1,051 rule tests pass outside Unity (`dotnet test`). New ones: the comforts never stand where you walk or in the front window; the string of lights glows, keeps to its height and leaves the window clear; the lantern, rug and stars are there; the baked light file fits the cabin; the lit housing matches the Lander model's housing; a slow frame rate loses no animation time (see below).
- The pictures were looked at after every change. Three things were found and fixed that way: grey lamp hoods, blotchy wall light, and coloured speckle from very small lamps.

## What is not verified

- **Nothing here has been run in Unity.** The pictures leave out bloom, the glass, the dash screen and the view outside.
- The wall light is stored at the corners of fairly large wall panels, so the pools of light are soft and a little uneven. If it looks patchy in the game, the fix is finer wall panels (about an hour).
- The lamps cannot bloom: baked colours stop at white. If they look flat in the game, the lamp shapes can be drawn with the brighter material the entry ring uses.
- The housing is modelled for the dash's default size and lean. Changing `deskWidth` or `deskLean` on the `CockpitConfig` asset would put the screen through the bezel.
- Frame cost is estimated, not measured.

## Agent review

**unity-reviewer.** Applied: the plain housing (used when the lit one is missing) was going to be lit by the Sun through the hull, and now gets the even indoor light; the lit housing is no longer used while the cabin is showing stand-in light. Noted, not changed: the dash dials above; the fallback bake is slower than before (see budget); lamps cannot bloom. Checked and fine: ownership of the new material, the layer, colours in linear and gamma projects, the two new pieces needing no special handling.

**game-designer.** Verdict: meets the request and is a clear step up, with wall light quality the main risk. Applied: smoother, less contrasty lamp balance; pale window surrounds; stars kept to the back and sides; the string of lights starts further from the window and is warm colours only (red and teal read as Christmas); a cord along the shelf so loose things look secured in a ship that crashes; wall kept free for the paintings. Not done, on its advice: anything interactive, a working radio, more props. Its open question for Matthew: keep the ceiling stars?

**deck-performance.** Nothing threatens the frame. Two more draw calls (13 cabin pieces), about 0.2 MB more on the graphics card, 10 to 20 ms more at the start of a run (a guess). The fallback bake, which only runs if the shipped file is stale, is about half as slow again. Its figures are in `docs/performance_budget.md`. Its three checks for Matthew are in the steps below.

## Also in this change: one Codex finding fixed

Codex reviewed the look work on PR #8 and raised two things.
- **Used:** the enemy's animation threw away time on frames longer than 50 ms, so at a low frame rate the stride fell behind the ground covered and the feet would skate. Long frames are now taken in several short steps. A rule test compares 10 and 60 frames a second.
- **Rejected:** "the camera hardware and dash housing are never drawn". True of that branch alone; the cockpit milestone draws the cameras, and this change draws the housing.

## For Matthew: how to look at it

1. In Desktop Mode open Konsole and run `cd ~/Orbital-ante && git pull`.
2. Open the project in Unity and wait for it to finish importing.
3. Press Play. You start in the seat. Look at the dash, then unbuckle and stand up.
4. Walk to the middle of the cabin and turn right round once.
5. Tell me: does it feel like a refuge; is anything too dark to use; do the walls look patchy; do you want the ceiling stars kept.
6. Optional, for the frame cost: in the Game view click **Stats** and tell me the **Batches** and **SetPass calls** numbers while seated.
