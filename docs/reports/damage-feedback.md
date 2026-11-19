# Showing the player when they are hurt (15 Nov 2026)

Matthew: "The player should have some indication of when they take damage." (The death gibbing he asked for in the same message is the build thread's.)

## Before

In the combat arena a hit gave a faint full-screen red pulse. Outside the ship in the solar system a hit showed nothing at all except the suit's per cent going down, and the hurt sound (`player.hurt`, three recordings already in the game) was never played anywhere.

## What changed

All four cues point the same way, and follow the visor style of `docs/reports/visor-hud.md`:

- **Direction arc**: a short red arc on a ring round the crosshair, on the side the shot came from, read like a radar seen from above: top is in front of you, bottom behind, left and right as they are. Full for a third of a second, gone after 1.1 s. Up to four at once; a second hit from about the same way refreshes the first instead of stacking.
- **Edge glow**: a red glow along the matching edge of the view (shared between two edges for a diagonal), from 35% strength for a graze to full for a 25-point hit, fading over 0.6 s. It replaces the arena's full-screen pulse.
- **Visor cracks**: one crack in the glass for each suit plate lost (up to six), around the edges of the view, never over the crosshair or the gauges. They close up again as the suit recovers, so the glass always agrees with the plate gauge.
- **Hurt sound**: `player.hurt`, panned toward the shot, at most once every 0.18 s so a burst of fire is not a burst of grunts.

In the solar system it shows only while you are outside (the only place the suit can be shot) and not while looking at a cockpit screen. The arena keeps its steady red tint while knocked down.

Preview (offline mockup, not game footage): project folder `orbital-ante/hud/hurt_feedback_preview.png`.

## Files

New: `UI/DamageCues.cs` (plain C# rules), `UI/DamageFeedback.cs` (drawing and sound), `Tests/EditMode/DamageCuesTests.cs`. Changed: `UI/Hud.cs` (creates it, `WatchHurt`, arena pulse removed), `Core/GameBootstrap.cs` (wires the arena player and the suit outside).

## Constitution

Principle 3 (learnable challenge): you always know you were hit and from where, so you can learn which cover works. Principle 5 (legible risk): the cracks give a lasting sense of how hurt you are that matches the plate gauge exactly. Principle 7 (cognitive load): one red colour and one direction for every cue; they fade by themselves.

## Tested

- `DamageCuesTests`, 7 tests: the arc points front/back/left/right correctly and handles shots from straight above or below and bad input; markers hold then fade and never brighten; the glow scales with damage and fades; edge shares split a diagonal; cracks match the plates lost exactly at every per cent; the sound pans toward the shot and is not repeated within 0.18 s. Whole suite: 2,197 passed, 1 skipped.
- Runtime scripts compile against the reference assemblies (only the known library clash errors).

## Not verified

- Nothing has been run in Unity: how strong the glow and cracks look on the Deck, whether the arc direction feels right in a fight, how the sound sits in the mix.
- Agent review: the unity-reviewer and game-designer agents could not run (their model's usage limit is reached until 19 Nov 2026). Checked by hand instead: the solar "visible" check holds the astronaut only until the world is torn down, when `WatchHurt(null)` drops it; events are unsubscribed on change and on destroy; a missing audio director is safe.
- Cost: up to four arcs, two edge bands (each a 150 px strip) and six crack images, all switched off when not needed; no per-frame allocations or text.

Playtest protocol: problem, outside the ship you could be shot without knowing it or from where; hypothesis, a direction arc, an edge glow and cracks make every hit noticed and placed; smallest test, stand on Mars with enemies firing from behind, then from the left; acceptance, you can turn to face the shooter within a second from the arc alone, and the number of cracks always matches the plates lost.
