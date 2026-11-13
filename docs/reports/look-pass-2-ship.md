# Look pass 2 report, part 2 - the outside of the ship

Status: **built, not yet run in Unity.** The new parts are real models (made in Blender here, pictures below). The code that loads them compiles against Unity reference assemblies and its rules are tested outside Unity (900 rule tests run, 900 pass; 8 are new in this part). Nobody has seen the ship in the game with these parts yet.

The pictures in `docs/art/previews/` (`lander_before_after`, `lander_a`, `lander_b`, `lander_c`) are **Blender's renders, not screenshots of the game**. The "before" half is the ship exactly as the game built it until now. The cabin dome in the "after" half is the game's own dome with the new round shading.

## Why

On 13 Nov 2026 Matthew said the game must not look PS2, "including the guns and ship". Rendering the ship as it stood showed where that came from: a hard eight-sided base of flat chequered squares, thin legs, small plain engine bells, and a dome shaded as flat facets.

## What changed

**The base** is now a dark frame with rolled rims, quilted gold foil panels (each a slightly different size and gently crumpled), and dark padded blankets on the four sides the legs hang from. The four free sides each carry something: a cream equipment locker with an orange stripe and a handle (nose), two strapped silver tanks (right), a finned radiator (tail), a round bolted service hatch (left).

**The legs** are fatter: a gold-wrapped upper strut with two dark straps, ball joints at shoulder, knee and ankle, a collar the bare piston slides out of, thicker side struts, a thicker rolled footpad. The ladder on the front-right leg is wider with fatter rails.

**The engines** have a mounting collar, a stiffening band and a thicker lip, with a dull orange glow painted inside.

**The thrusters** are cream blocks with an orange stripe and four fatter nozzles. **The dish** has a thicker rim, an orange ring on its face and an orange tip on its feed.

**The cabin dome shades round.** Its panels used to be lit as separate flat facets. Corners that sit in the same place now share their facing, so the dome reads as one curved shell, while the window frames and the skirt stay crisp. The dome's shape, panels and colours are unchanged, so the inside and the baked cabin light are untouched.

**Nothing about how the ship works changed.** Every new piece sits where the old one sat. The 18 breakable parts (the four engine bells count as one), their colliders, repair points, tear-off and wreckage, the belly entry and lift, the glass and the cabin are as they were. If the model file is missing or damaged, the ship builds the old shapes.

All colour is stored at the corners of the models (worn edges, grime in creases, patchy paint). No texture files.

## Which principles it serves
- **Milestone 7's bar:** "a real, cosy little spacecraft you want to protect". The ship is the player's home and the largest made object on screen.
- **Discovery that matters (4) and legible risk (5):** each side of the base is now different, so you can tell which way the ship faces from outside, and a missing leg or thruster is easier to spot.
- **Evidence-based iteration (10):** problem: the ship's outside reads as PS2. Hypothesis: the flat chequered base, thin legs and faceted dome are the cause. Smallest test: replace the bolt-on parts and shade the dome round, changing no mechanics. Acceptance: Matthew no longer calls the ship PS2 from outside; the readout still says OK on the Deck; legs still rest on the ground and tear off as before. Protocol: the checklist below.

## Choices you should know about
1. **The dome was not remodelled.** It has to fit the cabin, windows and baked light exactly, so it stays code-built. Painted wear and panel lines on the dome are the next step.
2. **The fittings on the base (locker, tanks, radiator, hatch) are decoration.** They do nothing yet. They could become real later (storage, oxygen), but that is Phase 2.
3. **The ladder is still decoration** (entry is by the belly lift). If it invites climbing, say so and I will make it read less like a route.
4. **About three times the triangles** of the old outside (about 32,000 against about 11,000). Estimated to be well inside the budget for one ship; unmeasured.

## What was tested, and how
- **Rule tests: 900 run, 900 pass**, with `dotnet test` outside Unity. New: the model's attachment points equal the game's layout numbers (leg roots, footpads, engines, thrusters, dish), so the Blender script and the game cannot drift apart unnoticed; all 11 pieces exist; footpads sit exactly on the ground and are as wide as their colliders; nothing dips below the ground; every piece covers about the same space as the shape it replaces; engine bells end where the flame starts; the triangle cap; round shading joins panels on a curve, keeps real creases, faces outward on the real hull and never turns a corner more than 45 degrees.
- **The game's scripts compile** against Unity reference assemblies.
- **Inside-out faces** were checked in Blender by a preview material that shows any back-facing surface in magenta; none remain on the new parts.
- **The models were built and rendered in Blender 4.0** from `tools/blender/build_ship.py`.

## What is unverified
- **Everything in the game:** how the parts look under the game's light, whether they line up with the dome and the colliders by eye, and whether tearing a leg off still looks right.
- **Frame time on the Deck.** Estimates only (`docs/performance_budget.md`, "Ship outside").
- **PlayMode tests** have not been run with this change. No PlayMode test covers the new fittings yet.
- **The visual quality gate:** still BLOCKED until a capture runs in Unity.

## Also fixed in this part (found by the outside review of pass 1, confirmed in the code)
- **The ship stayed sunlit on a planet's night side.** Panels that happened to face the Sun were lit through the planet. Sunlight now fades out for everything once the Sun is below the horizon where you stand. Not fixed: a ship in orbit inside a planet's shadow is still lit.
- The tip on the bloom threshold dial described the wrong units; it now says to tune by eye.
- Changing a Look dial during Play does nothing (the finish is set up once at the start). The steps in the performance notes now say to stop and press Play again.

## Agent review

Three reviews read the work before it was committed. None of them ran anything in Unity.

**Unity reviewer.** Found no blocking problem. Checked that nothing reads or destroys the shared meshes (ship clean-up, tear-off, wreckage, damage tint, colliders), that the model library is only reset at start-up, that all 11 pieces are in the file under the names the ship asks for with every triangle facing the right way, and that the round shading is correct on the real hull (it ran it: under 4 ms, once). No PlayMode test looks at ship meshes. Fixed from its findings: a stale comment about who owns wreckage meshes. **Left open:** there is no PlayMode test of the new fittings (one should check a torn-off leg still draws after the ship is gone); the round shading could in principle leave a faint seam where three very different surfaces meet (not seen on this hull); the fittings have no simpler far-away copy.

**Deck performance.** Judged it no threat to the frame budget as far as code shows. Counted the file: 28,940 triangles before the last design changes, and timed reading it on a desktop outside Unity (4 ms warm, 19 ms cold; guessed 20 to 80 ms on a Deck, once per launch). Draw calls are unchanged. Fixed from its findings: the cap in the rule test was tightened from 40,000 to 34,000 triangles; its rows are in `docs/performance_budget.md`. **Left as follow-ups:** read the model at start-up rather than when the ship is built; a far-away copy; the cheapest trim if needed is one bevel step fewer on the foil panels.

**Game designer.** Judged it mostly on direction: clearly Lunar Module-derived, chunky, hand-made, nothing copied. Fixed from its findings: the gold was shifted from lemon toward amber; the dark blankets on the leg sides were warmed and given seams so they stop reading as open black holes; the ladder's orange rung and the hatch's orange lever were made plain, so orange is decoration only and does not double as a warning colour. **For your eyes:** the dome now looks plainer than the base (next step); the ladder, locker handle and hatch lever look usable but do nothing; the tanks and locker stand up to about 0.4 m proud of the colliders, so you and bullets can pass through their outer edges.

## How to see it on the Steam Deck

1. Close Unity if it is open.
2. Open a terminal (Konsole) and type these lines, pressing Enter after each:
   1. `cd ~/Orbital-ante`
   2. `git fetch origin`
   3. `git checkout claude/look-pass-1`
   4. `git pull`
3. Open the project in Unity Hub as usual and wait for it to take in the new files.
4. Look at the Console tab. If there is any **red** line, stop and send me the first red line.
5. Press Play, start a run in the solar system, and leave the ship by the belly lift.

### What to look at
1. Walk round the ship. Is the base gold quilted foil with a locker, tanks, a radiator and a hatch, or still flat chequered squares? If squares, send me any yellow Console line that mentions "model".
2. Do the footpads sit on the ground, and do the legs meet the base without gaps?
3. Does the dome look round, with crisp window frames?
4. Crash gently until a leg or thruster comes off. Does it tear off and tumble as before?
5. Is the blue entry ring still easy to see under the base?
6. Frames per second and the Budget line, standing outside looking at the ship.
7. The big one: does the ship still feel PS2 from outside? What would you change first?

## What comes next
1. Painted wear and panel lines on the dome.
2. Your eyes on the guns and the ship, and changes from what you say.
3. ChatGPT's confirmed notes and its ship brief, when they arrive.
4. Then rocks and landmarks, gloves and tools.
