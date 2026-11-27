# Alien creatures: the swimmers (Drifting maw and Pack biter)

27 Nov 2026, built on Opus 5.5 (Matthew: "Continue to build and refine using Opus 5.5"). These are the last two of Matthew's nine creatures from `docs/briefs/alien-creatures.md`. Nothing here has been run in Unity.

## What changed

- **Drifting maw** (from the megamouth shark). Its rules, the Unity class and its place in the seas were already on this branch. The model had never been committed. It is now rebuilt and baked: 16 pieces, 9,518 triangles, a far copy of 2,840 triangles, and 2048 pictures. Its back fin was 8 cm taller than its layout, which a rule test caught, so the fin tip is now set lower to allow for the sweep that lifts it.
- **Pack biter** (from the Goliath tigerfish), new. It has rules (`BiterLayout`, `BiterBody`, `BiterBrain`, `BiterPack`, `BiterAnimator`), Unity classes (`BiterShoal`, `Biter`), a textured model (12 pieces, 6,210 triangles, far copy 1,828, `fishscale` material), a dump scene for creature_dump, and 16 rule tests.
- Seas now hold both creatures. 60% of sea haunts are a pack of 3 to 5 fish (`SeaField.IsPack`, `PackSize`), and only one pack is awake at a time. The arena has one pack of 3 swimming in the air over the ground, as a stand-in for water.

## How the Pack biter fights

A pack rings a swimmer. Only one fish has the turn at a time. That fish warns for 0.9 s: its jaws flare, its silver flank rolls to the light and its fins sweep back. It aims at where the player is heading until 0.6 of the way through the warning, then darts straight through and peels away. Turning back after it locks on dodges the dart. Drifting at a steady pace does not.

Shooting a fish during its warning scatters it and passes the turn to another. Losing its tail means it cannot dart, losing its jaw makes it flee, and the last fish that can still bite makes off. It dies to its head (65 HP, two rifle hits) or its body (80 HP, three).

Constitution: 3 learnable challenge (one threat at a time, each warned), 5 legible risk/reward (the head is the weak spot, shooting the fish that is warning pays), 8 varied intensity (a pack's rhythm compared with the maw's slow pull).

## Agent review

- **game-designer**: The maw choked on a single throat shot, so its pull never got played; choke damage went from 70 to 150 (two shots). The Biter's tail at 35 and head at 100 sat right on the rifle's 34; they are now 60 and 65. A fish aimed at where the player was, so steady drifting dodged every dart; it now leads the player by 0.5 s. A fish could warn from too far away to reach; only fish near the ring now bid for the turn. Two packs could be awake at once; now only one can. Fish that could not bite still counted as the pack holding together; they no longer do. The visor threat arc for a fish that is warning was suggested and is not built yet.
- **unity-reviewer**: When the world was not ready the water's "up" fell back to +Y; it now falls back to none. The facing used `System.Random` in world space; it is now seeded and in the world's own space. Each fish synced physics on its own; there is now one sync per shoal. A dart could bite someone out of the water; it cannot now. Every head hit played the head ding; now only a killing one does. Each animal searched for the suit; that is now one shared, throttled search. An empty shoal never went away; it now destroys itself. A pack's sleep distance was measured from where it was made; it is now measured from its fish. Not fixed yet: the maw's hold sets the suit's velocity in `Update`, which may fight the suit's `FixedUpdate`.
- **deck-performance**: A whole pack was built in one frame; it is now one fish a frame. Fish kept full detail out to 24 m; that is now about 12 m. The budget rows were added. Worst case is one pack of 5 plus one maw: about 64 draw calls and 38,500 triangles. That is arithmetic, not a measurement.

## What was tested

- `dotnet test tools/rules-tests`: 2,768 passed, 1 skipped.
  - Biter tests cover: what comes off and what kills it; one striker at a time, with every fish getting a turn; a warning of at least half a second; a still swimmer and a steady drifter get bitten, while one who turns back at the lock gets bitten less; no interest in anyone out of the water; the tail, jaw and broken-pack rules; the layout matching the model file.
- The runtime compile check passes apart from the two known `excludeLayers` errors in PlayerBurstFx, which come from the old reference assemblies.
- Stills were rendered in Blender (swim and warning). These are Blender pictures, not Unity screenshots.

## Still unverified

All of the following waits on Unity:
- The look in the game.
- The feel of the dodge.
- Whether the warning reads on the Deck's screen.
- Frame times.
- The maw's hold against the suit.
- Their own sounds, which are not made yet; other sounds stand in.
