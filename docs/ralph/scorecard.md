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

## Pass 2 (27 Nov 2026): after creature tells

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 7 | = | Unchanged | Creature kills still score nothing, so a fight in the seas does not count toward the ante |
| 2 | Easily understandable | 7 | +1 | Each of the nine creatures warns in sounds of its own: a wind-up, then one hard mark played the frame it takes aim, when its amber arc on the visor snaps to half its width (`Creatures/Windups.cs`, held to each brain and to the sound files by `WindupTests` and `tools/audio/qa.py`); one rule for every creature (move on the mark); the arc counts down while it winds up and dims once a strike can no longer reach you, so a dodge that worked is seen to work; every arc on the visor, damage, seen and creature, follows its danger as the player turns, so a warning behind them is found by turning toward it | Where to shoot a creature is taught only by knocks, blood and the hit marker; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | Twenty new creature sounds made from code, none of them like the clock or a reward (measured, not listened to); two older ones now carry on the Deck's speakers | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 6 | = | A creature fight can now be read and dodged on the mark, from any side | Unchanged: creature fights pay nothing, land creatures only in the practice range, the music never hears of the player being hurt or hunted |
| 5 | Fun unlockables | 6 | = | Unchanged | Envelope prizes still do nothing in the game; ten unlocks is a small catalogue |
| 6 | All-round fun | 6 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Pass 3 (28 Nov 2026): after creature kills pay and the danger music

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | +1 | Every fight now counts toward the ante: a creature kill pays on the camps' tier table with the same feats, streak, bonus missions and unlocks, once a run, and only from within its reach, with a note saying so (`TaskDirector.OnCreatureHit`, `CreatureTiers`, `TaskIds.Creature`, `ICreature.InReach`, held by `CreatureKillTests`); the gaps listed at pass 0 (nothing beyond one run, creature fights off the goal) are both closed | Nothing left that can be closed without the Deck: whether the goal reads at a glance in a fight is a playtest question |
| 2 | Easily understandable | 7 | = | The music now says where the player stands: calm; looked for (tension: heard, alarmed, searched for after a fight, a swimmer coming); a fight (the readout's six seconds after an armed enemy's sighting, or a creature's warning, held between its strikes); and on the last plates it thins so the suit's pulse is heard (`Audio/MusicDanger.cs`, `MusicBrain.Thin`, `MusicDangerTests`) | Where to shoot a creature is taught only by knocks, blood and the hit marker; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | The adaptive music Part B section 14 asks for now follows antes and danger, not only gunfire, never rests while the player is hunted, and a fight back soon opens on a later bar instead of the same riser (measured against the music's own model, never listened to) | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | +1 | A sea fight is now a way to score, not a detour, and only by getting in the water with them (no free points from a hover); the music rises when you are found, holds tension while anyone comes to look, and drops to tension when they have lost you | Land creatures live only in the practice range; nothing seen in Unity |
| 5 | Fun unlockables | 6 | = | Creature kills count toward the kill unlocks | Envelope prizes still do nothing in the game; ten unlocks is a small catalogue |
| 6 | All-round fun | 6 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Gaps, biggest first

1. ~~**Unlockables: goal unlocks.**~~ Done in pass 1.
2. ~~**Understandable: creature tells.**~~ Done in pass 2.
3. ~~**Goals and gameplay: creature kills pay; the music hears being hurt and hunted.**~~ Done in pass 3.
4. **Unlockables: envelope prizes do something** (ship paint, gun skins, upgrades as sidegrades), and more unlocks.
5. **Gameplay: land creatures on the real worlds** (walkers need ground following first).
6. **Aesthetics: the guns** (blocked on Matthew's downloads) and the seat view (PR 72's card).
7. **Gameplay: shots and the sea's surface.** Bullets pass through the sea both ways; stopping them at the surface
   would make "get in the water with them" a rule you can see, but lets a swimmer dive out of gunfire (pass 3's design
   review). A design question to settle before the seas get more creatures.
