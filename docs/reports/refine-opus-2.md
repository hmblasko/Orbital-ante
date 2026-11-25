# Refinement pass 2 (Opus 5.5, 21 Nov 2026)

Matthew: "Continue to refine the game. Yes." He chose **controller aim assist** from the options offered.

## What changed

### Controller aim assist (constitution 1, 3, 7)
- **Problem:** on the Deck a thumb overshoots small targets that a mouse hand stops on. The game is played handheld first.
- **What it does:** while the crosshair passes over a living enemy's head or chest, the sticks turn the view more slowly (half speed right on it, easing in over about 0.06 s). It never turns the view by itself and never pulls toward anything, so every hit is still the player's aim. The mouse is never helped.
- **Where:** `Player/AimAssist.cs` (rules, plain C#), `Player/AimAssistProbe.cs` (finds the target, one line-of-sight ray), wired into `Astronaut` (aboard and outside, gun in hand) and the arena's `FpsController`. Dials on `PlayerConfig.aimAssist`; each player can switch it off in pause > Feel > "Aim assist (controller)".
- **Hypothesis:** stick players land more first shots on limbs and heads without the aim feeling taken over.
- **Smallest test:** the arena, controller, aim across a standing mannequin with it on and off.
- **Acceptance:** the crosshair is easier to stop on a head; it never drifts by itself; no change with a mouse.

### Bug fixes from a review of the recent merges (PRs 73, 75, 76, 77)
- A dead player's ante clock kept running and the ante could still be banked on the death page. It now stops.
- The field star chart (PERK EARNED) could open while seated in the lift or in the seat, because the check used `Ship.Boarded`. It now uses the player's real mode (`Astronaut.PlayerSeated`, `PlayerDown`).
- "PERK READY" and the "+points" lines no longer draw over the ship's own display while aboard.
- A note or a tip left over from the last run no longer shows at the start of a new one.
- Overtime's hunt alarm no longer takes away a base's QUIET clear (the player did not trip it): `TaskDirector.SystemAlarm`.

## Tested
- Compiled against Unity reference assemblies (only the two known `excludeLayers` lines fail there, as before).
- Rule tests: 2,669 passed, 1 skipped (`BakedLightFile_FitsTheCabin`, as before). New: `AimAssistTests` (6).
- Reviewed by the unity-reviewer agent; it found that the enemy's own held gun blocked the line-of-sight check, and that a released stick kept the slowdown for a moment. Both fixed.

## Not verified
- Nothing here has been run in Unity. How the assist feels on the Deck, and the HUD fixes on screen, are unseen.
- Still open: outside the ship, the "+points" lines can overlap the ship's damage list when standing near it. That list belongs to the display thread (PR 72).
