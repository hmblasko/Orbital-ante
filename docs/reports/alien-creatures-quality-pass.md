# Alien creatures: quality pass (26 Nov 2026)

**Not run in Unity.** Everything below was judged in offline pictures drawn from the game's own model files and animators.

## Why
Matthew, 26 Nov 2026: "I really just want the alien assets not to look blocky and cheaply made in blender". The reference-image route (for Tripo) was dropped at his word the same day.

## What changed
- **Blind stalker (olm)** and **Ambusher (bobbit worm)**: the body was a row of like pieces drawn at different sizes, which showed as steps and rings. Each is now one continuous outline cut at its joints: every length has its own piece (`StalkerLayout.PieceOf`, `AmbusherLayout.PieceOf`), drawn at its modelled size, ending exactly as wide as the next begins, with the skin's pattern running across the joints. The Stalker has bushy gills, fleshed legs and toes; the Ambusher has a real mouth, hooked teeth, curled feelers and feet on every length.
- **Roller (isopod)**: plates, brow, head, eyes, tail and legs are finer and smooth, so no flat facets; whip feelers.
- **Screamer (cicada)**: finer fore body, head, eyes and wing edge; abdomen rings; swollen thighs with thorns, jointed feet with claws.
- **Strider, Sprinter, Cave hunter**: looked at again and left as they are. They are armoured animals whose bodies really are jointed plates, and they already read that way.
- Animators, so pieces meet at their joints in motion: `StalkerAnimator` (lengths laid joint to joint, head turns about the neck, tail's raise eased) and `AmbusherAnimator` (lengths laid joint to joint).
- Gibbing and limb loss are unchanged: same pieces come off, same health, same kill rules.
- Tools: `oa._halved` and `FAR_HALVE` (a far copy can halve a regular net), `-- --far-only`, the `rest` attribute for patterns that cross pieces (`mats.rest_place`).

Before and after pictures: `docs/art/previews/creatures/quality-pass/`.

## Cost (arithmetic, not measured)
| Creature | Full, as drawn | Far copy | Was (full / far) |
|---|---|---|---|
| Blind stalker | 15,868 | 4,631 | 7,208 / 1,894 |
| Ambusher (every length out) | 15,742 | 6,395 | 8,008 / 4,228 |
| Roller | 15,428 | 3,821 | 6,228 / 3,746 |
| Screamer | 11,192 | 3,596 | 4,378 / 1,488 |

All are inside the 16,000 allowance for an enemy body; the Stalker has 132 to spare.

## Constitution
Principle 1 (fun core first) and 3 (learnable challenge): nothing about how they fight changed, and the tells (gill kick, jaws flying wide, raised abdomen) are the same shapes in the same places.

## Tested
- Rule tests: 2,731 passed, 1 skipped (`dotnet test tools/rules-tests`), including each model file against its layout (joint rings match, far copies within their caps).
- Runtime compile check against reference assemblies: no errors.
- Stills of every rebuilt creature, and the Stalker mid-thrash from above and close up: no seam at the joints.

## Agent review
- **unity-reviewer**: the Ambusher's cut wounds were buried in the new rounded ends (fixed: `WoundOutBack`, `WoundOutFront`); a missing model piece left a body hittable and not drawn (now warns); no per-frame allocations, shared meshes safe, hitboxes never depended on mesh scale. Asked to check in Unity: the seam behind the Ambusher's head at full strike and in a hold.
- **game-designer**: tells and kill zones held. The Stalker was still laid by its middles, so a thrash would part the skin (fixed: joint to joint, checked in a still). Open: thrown Ambusher lengths other than the cut one show their modelled ends, not wounds (as before the pass); the gill kick may be harder to see on finer gills while it listens.
- **deck-performance**: within budget at arena counts (about 109,000 triangles all in full, 180,000 cap unchanged); draw calls unchanged; memory is the only new cost. Before creatures spawn in crowds outside the arena the Ambusher's and Cave hunter's far copies should come down to about 4,800.

## Still unverified
- Everything in Unity: look under the game's shaders, frame time on the Deck, far-copy swaps.
- The Ambusher's new wound offsets (not drawn offline).
- The gill kick's visibility at range.
