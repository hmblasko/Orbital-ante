# Ralph loop scorecard

Scores out of 10, read from the code, rule tests and offline pictures (see `README.md` for what each number means).
None of them is a playtest result. Bar: 8 on every line.

## Pass 0 (27 Nov 2026): where the game starts

| # | Criterion | Score | Evidence | Biggest gaps |
|---|---|---|---|---|
| 1 | Clear goal | 7 | First-run goal card and How to play say the goal in one screen (`Tasks/RunGuideRules.cs`); the ante plate shows clock, target, pace tick and NEED; one tip at a time; the world's task line; the map marks tasks left and the bonus mission; the recap ends on "Next mark" | Nothing to aim at beyond one run except records (no unlock goals); creature kills score nothing (`TaskDirector.OnPlayerHit` scores only `Mannequin` kills), so a fight in the seas does not count toward the goal |
| 2 | Easily understandable | 6 | Threat arcs and stings (round 1), readout focus in fights (round 2), hit markers and dings, death cause in plain words, controls page names both uses of a button, perk cards split gains and costs | Six creatures' warnings (Strider, Sprinter, Roller, Cave hunter, Drifting maw, Pack biter) fall back to the Faction A Scout's flare sound, and the maw's draw-in to the Heavy's spin-up: one sound, many meanings. The visor's threat and damage arcs stay at the screen angle they fired at, so turning toward the danger leaves them pointing the old way (`UI/DamageFeedback.cs`). A creature winding up out of view shows nothing on the visor |
| 3 | Strong aesthetics | 6 | Real sky (Bright Star Catalogue), planets from space with air, clouds and night side, Sun shadows, worn realistic ground, textured Tripo enemies, nine textured creatures, props and melee models, the Hull stencil plates and menus, deep house music | The five guns are still script-built (the realistic roster waits on Matthew downloading the CC0 packs); seat view (PR 72) unmerged; cloud picture flaws; none of the realism passes has been seen in Unity |
| 4 | Enjoyable gameplay | 6 | Gibs and disarms, cover and flanking, Faction A abilities, five melee weapons, rover splats, Focus, the gun ladder, restless camps, overtime hunts, nine creatures with tells | Land creatures live only in the practice range; creature fights pay nothing; in the real game the music never hears of the player being hurt or hunted (`GameAudioHooks` watches no health in space, threat fixed at 22 on foot), so it rises only with nearby enemy gunfire |
| 5 | Fun unlockables | 2 | Prize envelopes exist, but open only after ante 9 (`RunEnvelope.UnlockAnte`) while a first-time player is meant to die around ante 3 or 4 (Part B section 5); the paints, skins, upgrades and artworks inside are stored and never used by the game; there are no goal unlocks at all (Part B section 5 asks for Balatro-style ones); records are the only lasting progress | No goal unlocks; nothing to unlock in view; envelope prizes do nothing |
| 6 | All-round fun | 5 | On paper the loop holds: an ante to clear, tasks and fights to score from, build choices at the break, overtime instead of a hard fail, endless antes after the last | Moves with the other five, and with a playtest |

## Pass 1 (27 Nov 2026): after goal unlocks

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 7 | = | Now also a goal beyond the run: the title screen names the next unlock, the recap names the next two, and every goal says how to meet it with the player's own keys (`Ante/Unlocks.cs`) | Creature kills still score nothing, so a fight in the seas does not count toward the ante |
| 2 | Easily understandable | 6 | = | The five ways to score are named by what really pays them (`Ante/ScoreWays.cs`, held to the code by a rule test); a kit's price is written wherever it applies | Unchanged: six creatures share one warning sound, the visor arcs do not follow the danger as you turn, an unseen creature's wind-up shows nothing |
| 3 | Strong aesthetics | 6 | = | One new sound made for the unlock, a stencil banner | Unchanged |
| 4 | Enjoyable gameplay | 6 | = | Kits give a run a different opening | Unchanged: land creatures only in the practice range, the music never hears of being hurt or hunted |
| 5 | Fun unlockables | 6 | +4 | Ten one-run goals, easiest first, the first ones within a new player's reach (clear ante 2, a crouched kill, a rover splat); one always in view (title screen, recap, Unlocks page); each changes later runs (six perks out of the drafts until earned, four kits that are sidegrades); getting one is a moment (banner, note, its own sound, the new perk shown once marked NEW); saved the frame after | Envelope prizes still do nothing in the game; ten unlocks is a small catalogue; no ship parts (none exist yet); nothing seen in Unity |
| 6 | All-round fun | 6 | +1 | A run now ends looking at the next unlock as well as the next mark | Moves with the other five, and with a playtest |

## Gaps, biggest first

1. ~~**Unlockables: goal unlocks.**~~ Done in pass 1.
2. **Understandable: creature tells.** Each creature's own warning sound with its aim click; the visor arcs follow
   the danger as you turn; a creature's wind-up shows on the visor like a gunman's.
3. **Goals and gameplay: creature kills pay; the music hears being hurt and hunted.**
4. **Unlockables: envelope prizes do something** (ship paint, gun skins, upgrades as sidegrades), and more unlocks.
5. **Gameplay: land creatures on the real worlds** (walkers need ground following first).
6. **Aesthetics: the guns** (blocked on Matthew's downloads) and the seat view (PR 72's card).
