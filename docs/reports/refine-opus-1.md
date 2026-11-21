# Refinement pass 1 (20 Nov 2026)

Matthew asked to keep refining the game. Two critic reviews read `claude/playtest` (integration bugs from the latest merges, and Steam Deck cost), then a Unity review read the fixes.

## What changed, and why

| Change | Problem | Constitution |
|---|---|---|
| Guns kick the view in the real game (`Player/ViewRecoil`, `PlayerWeapons.Recoil`, `Astronaut.ReadLookRaw`) | Recoil only worked in the test arena. In the ship and outside the view never moved when firing, so full-auto never climbed. | 1 Fun core, 3 Learnable challenge |
| Tripo soldier's head hitbox moved onto the helmet (`BodyFit.HeadRise` -0.035, new `BodyFit.HeadForward` 0.07) | The ball was still where the old helmet was: shots up to 8 cm behind the helmet and 7 cm above it counted as head hits, and shots on the visor missed. The head is the only way to kill an enemy. | 3 Learnable challenge, 5 Legible risk |
| Aim ray only when the F3 line asks (`PlayerWeapons.AimedAt`) | A full-range raycast every frame for a debug readout. | Deck budget |
| Ground collision shape built only when a tile turns solid (`PlanetSurface.FillColliderMesh`) | Every tile, near or far, built a second mesh on the main thread when it arrived. | Deck budget |
| Cover search sort and the sword's heat bar no longer make garbage or rebuild every frame | Small per-frame allocations and canvas rebuilds. | Deck budget |

## Tested
- `dotnet test tools/rules-tests`: 2,559 passed, 1 skipped (`BakedLightFile_FitsTheCabin`, skipped before this change too). New: `ViewRecoilTests` (5). `EnemyModelFile_ItsHitboxesFitIt` now also checks the head ball against the helmet's top and back (it fails with the old numbers).
- Runtime scripts compile against UnityEngine 2021.3 reference assemblies; the only errors are the two known `excludeLayers` gaps in that older library.

## Not verified (nothing here has been run in Unity)
- How the kick feels in each posture: aboard, upright outside, floating free. It uses each gun's existing recoil numbers from the arena.
- That headshots land where the helmet is on screen.
- That ground under your feet is still solid after a tile is rebuilt (Physics Debugger).

## Left for other threads
- The flight readout (`FlightHud`) rebuilds its text 8 times a second and moves a marker on the main canvas every frame. The display-redesign thread (PR 72) is rebuilding that screen, so the fix was passed to it.
- The soldier's torso is wider than its hitbox (x 0.28 m against 0.215 m). Torso hits do not kill, so it was left for the realistic-enemies thread.
