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

## Pass 4 (28 Nov 2026): after the Locker (envelope prizes that do something)

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 2 | Easily understandable | 7 | = | Every prize says where it went or how to put it to use, an upgrade's cost is written wherever it is named (reveal, Collection, a note at the run's start, the break), and the stats use the perks' own words (`Items/Locker.cs`, `CollectionRows`, held by `LockerTests`) | Where to shoot a creature is taught only by knocks, blood and the hit marker; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | Seven gun skin patterns that keep the pictures' wear and fade before they could crawl, six hull paints that keep the lander's dark panels and window bands, suit looks held to the plate's contrast (`Items/PrizeLooks.cs`, `OA_Skin`, `HullPaint`, held by `PrizeLookTests`); the reveal checked offline with the game's own fonts | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | = | Six upgrades that change a run's shape, each a sidegrade with its cost on another number, none on gun or melee damage | Land creatures live only in the practice range; nothing seen in Unity |
| 5 | Fun unlockables | 7 | +1 | Envelope prizes now do something (the gap listed since pass 0): one in use per place (upgrade, ship paint, gun skins, helmet, visor, suit, jetpack, badge), a won cosmetic goes to work by itself where its place is empty, one press on the Collection page puts a prize to use or takes it off, and older item assets follow retuned upgrades (`Items/Locker.cs`, `PrizeLooks.cs`, `ItemCatalog.Version`) | Reach: prizes open only after ante 9, so a new player meets none for many runs (the envelope rule is Matthew's, 16 Nov 2026); ten goal unlocks is a small catalogue; Celestial effects are not drawn; paintings wait for frames and Matthew's downloads |
| 6 | All-round fun | 6 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Pass 5 (28 Nov 2026): after earned looks

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | = | An empty Collection now names the next look and how to earn it before the envelope; a look's kind is said beside its name wherever the next unlocks are named | Nothing left that can be closed without the Deck |
| 2 | Easily understandable | 7 | = | Every look says what it changes ("a visor tint", never "a visor"), where it went ("now on your lander"), and what an envelope prize took the place of; the practice range says once that goals count only in a run (`PracticeNote`) | Where to shoot a creature is taught only by knocks, blood and the hit marker; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | Eight looks made of numbers on the existing patterns and styles; the two earliest kept quiet (muted enamel on every gun, smoked glass on the visor) | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | = | The first looks are earned by the game's own best moments (a disarm, a leg shot off, the first landing away from Earth) | Land creatures live only in the practice range; nothing seen in Unity |
| 5 | Fun unlockables | 7 | = | Reach closed (the gap listed since pass 4): eighteen one-run goals, eight of them looks in places of their own, three within a first run's reach; each look goes on as its moment shows (`LockerLook.Refresh`), an envelope's prize takes an earned look's place, and the envelope's printed odds are unchanged (3,000 envelopes rolled both ways, `EarnedLookTests`) | Still short of 8: the Celestial tier's effects are not drawn (Part B section 12's "special effects (e.g. fire)"; the words say "not shown yet"), the one piece of this line the cloud can still close; paintings wait for frames and the five public-domain pictures |
| 6 | All-round fun | 7 | +1 | The reason for one more run now reaches a first-time player: a first run tends to end with a look on the lander or the guns and the next unlock named, where before a new player's Collection stayed empty for many runs | Moves with the other five, and with a playtest |

## Pass 6 (28 Nov 2026): after the Celestial effects are drawn

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 2 | Easily understandable | 7 | = | Every effect says where it is seen ("on every gun", "on that gun", "on your lander", "round your suit's readout"), and none can be read as a hit, a warning, damage or an aim tell: light on the item only, never past white, steady second to second, faded by the hull's damage, never on an enemy's gun or on the gun that can blow up, the plate's beads outside the plate, paler than every readout colour that means something and dark in a fight or beside a warning (held by `CelestialLooksTests`) | Where to shoot a creature is taught only by knocks, blood and the hit marker; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | Six moving effects made from code, judged offline from the shader's own sums: flames, curtains, comets, clouds, falling stars and turning dark bands on the gun and the hull (fire as slow clouds on the hull), and beads round the suit plate and round the prize card on the reveal | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | = | Unchanged | Land creatures live only in the practice range; nothing seen in Unity |
| 5 | Fun unlockables | 8 | +1 | The top tier is no longer a name only (the gap listed since pass 4): each of the six effects is drawn where its item shows, on the gun in hand, on the lander's hull or round the suit's readout (`Items/CelestialLooks.cs`, `OA_Fx` in `OA_Model.shader`, `SuitHud` beads), and the words say where; with pass 5's reach this is what the design review asked for 8 | Nothing left that can be closed without the Deck: paintings wait for frames and the five public-domain pictures, ship parts for the ship's parts (Part B's own unlock targets), and whether the unlocks feel worth chasing is a playtest question |
| 6 | All-round fun | 7 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Pass 7 (28 Nov 2026): after where to shoot a creature

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 2 | Easily understandable | 7 | = | Where to shoot a creature is taught (the gap listed since pass 2): every hit on one says where it landed in the marks players learn on people, armour that held a small grey X and a soft knock with no bell, the weak place the head's gold and bell (a kill through it the headshot kill's marker, chime and slow-mo), a claw, jaw or drum shot off the disarm's cyan; grey never marks a piece the player should shoot; the first time a kind's armour turns a shot, one line over the crosshair names the piece and where to aim, twice at most and never once it is found (`Creatures/WhereToShoot.cs`, `UI/HitConfirm.cs`, held by `WhereToShootTests`); the glance and the creatures' shell knock are held to every creature's mark by `tools/audio/qa.py` | The design review's last cloud item for 8: a development-only playtest log, so the protocols of passes 2 to 7 can be counted on the Deck (until then, from video); whether the line ever covers what it names; nothing seen or heard in Unity |
| 3 | Strong aesthetics | 6 | = | Two sounds remade from code: the glance, soft and dull, and the shell knock, which no longer rings at the ante clock's pitches (measured, not listened to) | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | = | Shooting a creature no longer stops time on every hit (only a piece off, a choke or a kill does, as for people), so full-auto fire at one stops stuttering; the best way to kill each creature now sounds and feels best | Land creatures live only in the practice range; nothing seen in Unity |
| 5 | Fun unlockables | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 6 | All-round fun | 7 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Pass 8 (28 Nov 2026): after land creatures on the worlds

| # | Criterion | Score | Change | Evidence | Biggest gaps |
|---|---|---|---|---|---|
| 1 | Clear goal | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 2 | Easily understandable | 7 | = | A creature's kill names its strike on the death page in plain words, never a placeholder name (`StrikeWords`); a creature's fight is a fight for the whole game, so no tip, unlock banner or restless squad's radio call comes in the middle of one (`ReadoutFocus.CreatureFight`, held by `CreatureFightTests`); a kill that does not pay says why (KILLS PAY ONLY WHEN IT CAN GET AT YOU); what lives where can be learned at a glance, each kind of world led by its own animal | Unchanged: a development-only playtest log (gap 9); nothing seen in Unity |
| 3 | Strong aesthetics | 6 | = | The walkers' legs reach the ground they stand on, on any slope (measured offline: no foot drawn more than 3 cm off walking straight); creatures lie with the ground and fly over cliffs without passing through them | Unchanged: the guns (Matthew's downloads), the seat view (PR 72), nothing of the realism passes seen in Unity |
| 4 | Enjoyable gameplay | 7 | = | The Strider, the Sprinter, the Roller and the Screamer live on the worlds' ground, each kind of world with its own animals, more of them each ante, their kills paying toward the ante when they could answer the player (`LandCreatures`, `LandField`, `LandReach`, held by `LandRulesTests`); the Roller's crash only against something solid | Creatures and the camps ignore each other in the fights the placement makes likely (gap 12); the three cave and burrow creatures are not on the worlds yet; nothing seen in Unity |
| 5 | Fun unlockables | 8 | = | Unchanged | Nothing left that can be closed without the Deck |
| 6 | All-round fun | 7 | = | Moves with the other five | Moves with the other five, and with a playtest |

## Gaps, biggest first

1. ~~**Unlockables: goal unlocks.**~~ Done in pass 1.
2. ~~**Understandable: creature tells.**~~ Done in pass 2.
3. ~~**Goals and gameplay: creature kills pay; the music hears being hurt and hunted.**~~ Done in pass 3.
4. ~~**Unlockables: envelope prizes do something.**~~ Done in pass 4 (the Locker).
5. ~~**Unlockables: reach.**~~ Done in pass 5 (earned looks).
6. ~~**Unlockables: Celestial effects drawn.**~~ Done in pass 6. Paintings framed on the cabin wall stay blocked on
   the five public-domain pictures.
7. ~~**Understandable: where to shoot a creature.**~~ Done in pass 7.
8. ~~**Gameplay: land creatures on the real worlds.**~~ Done in pass 8 (the Strider, the Sprinter, the Roller and the
   Screamer; the Ambusher, the Cave hunter and the Blind stalker wait for soft ground and caves).
9. **Understandable: a development-only playtest log.** One log the Deck playtests of passes 2 to 8 can be counted
   from (hits by zone, kills, aim lines shown, creatures' marks and whether they hurt, unlock moments, land creatures
   met and how each fight ended, kills that did not pay), kept in memory and written at a run's end, editor and
   development builds only. The design review's last cloud item for line 2.
10. **Aesthetics: the guns** (blocked on Matthew's downloads) and the seat view (PR 72's card).
11. **Gameplay: shots and the sea's surface.** Bullets pass through the sea both ways; stopping them at the surface
   would make "get in the water with them" a rule you can see, but lets a swimmer dive out of gunfire (pass 3's design
   review). A design question to settle before the seas get more creatures.
12. **Gameplay: creatures and the camps notice each other.** In a run most fights with a creature will bring a camp's
   squad to the sound (pass 8's design review: 83 to 100% of haunts, by world, have a camp within a shot's hearing),
   but the two sides ignore each other and enemy fire passes through creatures (`EnemyShootMask`). Start cheap: a claw,
   roll, scream or bite that lands on an enemy hurts and gibs it, and enemy fire stops at a creature. Milestone 12's
   "moment worth laughing about".
