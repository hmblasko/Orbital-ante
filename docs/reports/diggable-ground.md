# Diggable ground (17 Nov 2026)

Matthew asked: "Maybe have the ground of the planet be solid and give the player the ability to shovel holes and dig in the ground. Also have the ability to shoot holes in the ground and the ground gets dug up, including when enemies shoot at you. Thinking it would be cool to be able to cave around your enemies."

**Nothing here has been run in Unity.** The rules were tested in the cloud and the Unity code was compile-checked against reference assemblies. The pictures are offline renders of the game's own meshes, not Unity screenshots.

## What changed

- **Solid, diggable ground on every planet and moon.** Ground stays the same height-field tiles until something digs it. Round each hole the game then builds "dug ground": boxes 6.4 m across in which the ground is a true solid, so holes, overhangs and tunnels are possible. Where those boxes stand, the ordinary tile and any cave network wall leave their faces out, so there is no seam. Dug ground lies 6 cm under the old surface, so where both are drawn the old surface wins.
- **The shovel.** It is always carried on the suit, separate from the gun and the melee slot.
  - Controls: hold **B**, the **middle mouse button**, or the **left bumper** (on foot; while floating free the bumper still rolls).
  - Each bite takes a scoop about 1.4 m across in front of you. Dig down for a foxhole, or straight ahead into a hill for a tunnel.
  - Digging forward takes a second scoop at your feet, so the tunnel is tall enough to walk along and its floor stays level with your boots.
  - Speed: about 1.3 m of tunnel a second, slower than walking.
  - Rock, trees, bases and the ship cannot be dug, and the blade rings off them.
  - When the ground will not give, you hear a dull thud and the suit display says why: as deep as it goes (24 m), water below, ground too loose (beside an old single cave), or ground packed hard (the body's cap is reached).
- **Bullet holes.** Every bullet that hits the ground takes out a small scoop, from 0.2 m for a pistol to 0.5 m for heavy rounds. Enemy bullets dig too, so cover wears away under fire. Two settings switch this off: "Bullets Dig" and "Enemy Bullets Dig" on the Planets asset.
- **Blasts.** `GroundDamage.Blast(point, radius)` makes a crater. Nothing in the game explodes on the ground yet, so this is ready for the meteor strikes (PR 58) to call.
- **Enemies and holes:**
  - They hear digging from a bit further than footsteps (1.4 times the walking range), so digging is loud work.
  - Down a roofed tunnel you are seen from half as far, never less than 0.35 of the normal range, as in a dark cave.
  - Craters count as cover, because the cover finder looks rather than reading a list.
  - A hunted player who has dug in is hunted through the tunnel's mouth. Enemies go there, shoot down the tunnel, and go in after you once they reach it.
- **Tunnels join cave networks.** Dig into a lava tube on the Moon and the two open into each other.
- **Light.** Each corner of dug ground stores how much sky it can see, so tunnels darken as they go in.
- **Holes last for the run**, kept per body. When you fly away and come back they are still there.

## Down to the core (added the same day)

Matthew: "You should be able to dig anywhere in the down to the core."

- **No depth limit.** The 24 m limit is gone. You can dig anywhere until you are 1.5 m from the world's centre; the shovel then says "THE CORE: NOWHERE DEEPER TO DIG". A rule test digs a shaft straight down into Earth (500 m radius in the game) with the shovel: 640 bites, 475 boxes, and it stops at the core and nowhere else.
- **Below the sea's level, under dry land.** Before, nothing on Earth could be dug below the sea's level, even far inland. Now that is allowed under dry land. Under the sea bed or a shore a scoop keeps 1.5 m of rock over it, so the sea never comes in; the shovel says "CAN'T DIG: THE SEA WOULD COME IN". A tunnel below the sea's level counts as air, not water, and the sea's surface is hidden while you are down there, so it does not hang across the shaft as a ceiling of water.
- **The safety net moves down.** Each world has an invisible solid ball under its lowest ground to catch anything that falls through. It now shrinks to stay under the deepest hole, so it never blocks a shaft.
- **Gravity.** Inside a world the pull already fades in a straight line to nothing at the centre (unchanged), so at the core you float.
- **Light.** Daylight down a hole fades out between 8 m and 48 m under the ground; deeper, only your suit lamp lights the walls.
- **Seeing deep holes.** Boxes well under the ground are drawn whenever you are within 320 m, whatever the ground tiles above are doing. Boxes are only built within 320 m of you, so a world dug full of shafts does not build them all at once.
- **Enemies** look further for the mouth of a tunnel the deeper you are (30 m plus one and a half times your depth). Which hole is a mouth is worked out once per hole and kept.
- **Box cap** raised from 420 to 1500 per world; bullets still stop at 250.

Not verified (needs Unity): walking, standing and floating near the core (which way is up flips as you pass the centre; the suit should go to floating as the pull fades), how long the boxes of a deep shaft take to appear on the Deck, whether anything that checks "under the ground" elsewhere in the game still treats a deep shaft as being inside rock.

## Why (constitution)

- Rule 1 (fun core gameplay) and rule 2 (agency): digging in and caving round enemies is a new way to fight, and the player chooses when to use it.
- Rule 3 (learnable challenge, never hidden rules): a refused bite says why, and enemies come for a dug-in player instead of standing on the roof.
- Rule 8 (varied intensity): a firefight now scars the ground, and cover wears away.

## Agent review

Three reviews ran on the first version: Unity, design and Deck performance. Everything below was fixed before pushing.

### Unity review

- **Invisible lid over a hole.** A tile that stopped being solid kept its old collider shape. When it became solid again, that shape came back as an invisible lid over the hole. *Fixed:* the old shape is dropped from the collider.
- **Gap for up to a quarter second.** A box far off was switched off whole, collider included. A tile could then come in with its hole already cut and no floor under it. *Fixed:* only the drawing is switched now, and the collider stays on.
- **Safety net turned off near holes.** The net that stops things sinking into the ground was off within about 4 m of any bullet hole. *Fixed:* it is now off only right inside dug air.
- **A crashed job stopped digging for good.** *Fixed:* a box that fails is left empty and logged.
- **Loose things not woken.** Loose things are now woken over the changed box and over rebuilt tiles. The wake check only looks at layers that hold loose things.
- **Collider cooking.** Colliders are now cooked with mesh cleaning.
- **Lag on a reshaped box, not fixed.** A reshaped box can be drawn a frame or two before its new collider is cooked. While that lasts the old floor is still there, so it acts as a lid and you cannot fall through. The code comment now says so.

### Design review

- **Burrowing beat every fight.** *Fixed:* enemies now hunt a dug-in player through the tunnel's mouth, as described above.
- **Digging was too fast**, at 2.2 m/s. *Fixed:* it is now about 1.3 m/s (the swing takes 0.71 s, and each bite reaches 0.20 m past the blade).
- **Tunnels drifted upward.** *Fixed:* the lower scoop is now placed from the feet. A rule test digs 15 bites and checks the floor stays within 0.2 m.
- **Refusals sounded like rock.** *Fixed:* a refusal now has its own thud and its reason on the suit display.
- **Bullets could use up the whole cap.** *Fixed:* bullets now stop at 250 of the 420 boxes, so the shovel always has room.
- **Digging was too quiet.** *Fixed:* it is now heard at 1.4 times the walking range, up from 1.1.

### Deck performance review

- **Dig work crowded out the ground you land on.** *Fixed:*
  - beyond 15 m, dig work queues behind ordinary ground tiles and never takes the last free core;
  - rebuilt tiles and cave walls follow the same rule.
- **Too many tiles rebuilt.** *Fixed:*
  - only the finest tiles that actually touch a changed box are rebuilt;
  - a tile whose shape does not change keeps its mesh;
  - the cover is remade at most every 0.2 s.
- **Collider cooking.** *Fixed:* one shared budget of 2 cooks a frame now covers dug boxes and rebuilt tiles.
- **Allocations.** *Fixed:*
  - the per-frame closure is gone;
  - the cave network list is cached.
- **Drawing check.** *Fixed:* it now checks 40 boxes a frame in turn, instead of every box in one frame.
- **Not done:** per-thread scratch buffers in the box builder. Each box built still makes garbage, estimated at a few hundred KB. This is the first thing to look at if the Profiler shows garbage-collection hitches.

## Tests

- Rule tests in the cloud (`dotnet test tools/rules-tests`): 23 digging tests pass. Earlier, before the review fixes, the full suite passed (2,579 passed, 1 skipped). They cover:
  - the box lattice and the cover;
  - the caps, including the bullets' share;
  - the depth and sea limits, and the reason given for each refusal;
  - hole sizes;
  - dug ground meeting the tile within 2 cm;
  - sealed meshes facing the air across box edges;
  - tiles leaving out only covered squares;
  - darkness down a tunnel;
  - digging into the Moon's cave network;
  - box build time;
  - the shovel's rhythm and speed;
  - level tunnel floors;
  - finding a tunnel's mouth.
- Unity code compile-checked against Unity reference assemblies. The only errors are the expected Unity 6 renames, which those older reference assemblies lack.
- Offline pictures of Mars (seed 4242), from the game's own meshes with a simple light: `dig_air.png` (a blast crater, a foxhole and a patch of 60 bullet holes, from above) and `dig_ground.png` (the same from standing height).

## Still unverified (needs Unity on the Deck)

- Everything at run time: seams and flicker where dug ground meets the tiles, the shader change (`_VertexSky`), collider timing, frame times and garbage collection on the Deck.
- Rocks, trees and bullet decals sitting over a new hole stay floating; they are not moved or removed.
- Enemies in a cave network walk on the network's own shape, so they do not follow dug tunnels inside a network. On open ground they reach a tunnel's mouth, but whether they walk well inside a narrow tunnel is untested.
- Under the "simple shaders" fallback, tunnels are not darkened.
- From far away, holes are not drawn. The coarse ground covers them; only the finest tiles show dug ground.
- The shovel is a primitive stand-in (a wooden handle and a steel blade) until Fable makes a model.
- Digging sounds reuse "impact.dirt" (a lower, quieter play for a refusal). There is no shovel sound of its own yet.
- Whether bullet holes wearing away cover is fun or unfair needs a playtest.

## Try it on the Deck

1. Open Konsole in Desktop Mode and go to the repo: `cd ~/Orbital-ante`
2. `git fetch`
3. `git checkout claude/playtest`
4. `git pull`
5. Open the project in Unity Hub and press Play. Start a run, leave the ship and walk off the launch pad onto the ground.
6. Hold the **left bumper** (or **B** / **middle mouse** on a keyboard) while looking at the ground. You should dig a hole and be able to stand in it.
7. Look straight ahead at a hillside and hold it. You should tunnel in, with the floor level with your feet.
8. Shoot the ground. Each shot should leave a small hole.
9. Find enemies and hide in a hole or tunnel. Note whether they come to the tunnel mouth, and whether their bullets chew the ground round you.
10. Press F3 and read the planet line: "dug N scoops, M boxes (R refused)". Note anything that looks wrong: gaps, flicker, falling through, a stutter while firing.
