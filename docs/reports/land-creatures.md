# Land creatures on the worlds (Ralph loop pass 8, 28 Nov 2026)

Matthew asked for "enjoyable gameplay". Out on the worlds every fight on the ground was a camp's. The four creatures
that walk and fly (the Strider, the Sprinter, the Roller and the Screamer) lived only in the practice range. So a
player who learned their warnings (pass 2) and their weak places (pass 7) never met them in a run, and their kills
could never pay (pass 3). The brief (`docs/briefs/alien-creatures.md`) says where each animal lives. This pass puts
those four on the ground of the worlds: each kind of world has its own animals, more of them come at each ante, and
they keep to a round, hilly world. **Nothing here has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | The worlds' ground held only the camps' soldiers. Four of the nine creatures, each with its own warnings, weak places and sounds, were met only in the practice range, where nothing pays. The camps were the only reason to stop on a world's ground. |
| Hypothesis | If each world has its own animals on its ground (a few on a small moon, up to thirty haunts on a large planet, more of them lived in at each ante), then a landing becomes a choice between a camp's fight and an animal's, and a world is remembered by what lives there. What the player learned of a kind's warning and weak place then pays in the run. |
| Principle | Fun core gameplay first (1). Discovery that matters (4): each kind of world has its own animals. Varied intensity (8): one animal with one warning between the camps' squads. Meaningful replayability (6): seeded per world. Learnable challenge (3): a later ante brings more animals, never more health. Legible risk and reward (5): a kill pays only when the animal could answer the player. Part B section 10's aliens ("several types ... each with unique properties, satisfying to kill") and section 5's "more and tougher enemies" as the ante rises. |
| Smallest test | The rule tests below (`LandRulesTests`, 16 tests; `CreatureFightTests`, 3; one new in `RollerRulesTests`; `CreatureKillTests` widened). |
| Acceptance (offline, met) | All of the tests below pass. `dotnet test tools/rules-tests`: 3,222 run, 3,221 passed, 1 skipped (the cabin bake check, as before). The runtime compiles apart from the two known `excludeLayers` errors. |
| Acceptance (Deck, not yet run) | See "Playtest protocol". |

What the tests hold:

- Haunts are only on ground that fits. They are well apart and clear of the pad and the camps. The same seed gives the
  same haunts and another seed gives others.
- The count follows the world's size. A world too small or with no animals has none, and a small world full of camps
  still has room.
- About half the haunts are lived in at ante 1, three quarters at ante 2 and all from ante 3. A haunt lived in at one
  ante is lived in at every later one.
- A haunt's kind follows its world's weights and is never one the world does not have. Each kind of world is led by its
  own animal, the Roller is on every world, and the Screamer lives only under air.
- Each kind is drawn with its own model and paid as that creature. A land creature's claim is never a sea creature's.
- A step steeper than a kind takes is a wall, up or down. On a slope it stands by the ground, but never leans further
  than it can.
- Walking on a slope, a foot that is down is on the ground and the leg as drawn reaches it. This is tested for both
  walkers, on gentle and on their steepest slopes, at a walk and at full pace. In the practice range the walk is as it
  was.
- A creature walking in, or following someone who backs away, can always answer them. Kept below a ledge, it stops
  being able to answer, and can again once it gains ground. Its own opening never takes its pay away.
- A creature's warning, or a creature after the player, is a fight for the whole game, and the fight ends by itself.
  Everything that waits out a fight asks the same test.
- A creature's kill says how it happened in plain words, never with its placeholder name.
- Ground the Roller cannot roll up ends its roll like any other opening. Only something solid is a crash.

## Which animals live where

From the brief: the Strider (the spider crab) lives on open ground, the Sprinter (the camel spider) in deserts, the
Roller (the isopod) anywhere, and the Screamer (the cicada) among trees and cliffs. The Screamer flies and screams
through the air, so it lives only on a world with air (`LandHabitats`).

Each kind of world is led by its own animal, so what lives where can be learned at a glance:

- the hot, dry worlds by the Sprinter;
- the airless, cold moons by the Strider;
- the worlds under air by the Screamer.

The Roller is on every one, and is never more than a third of the haunts. The numbers are weights: of every six haunts
on Earth, about one is a Strider's, two a Roller's and three a Screamer's.

| World | Strider | Sprinter | Roller | Screamer | Haunts asked for |
|---|---|---|---|---|---|
| Mercury, Io | 1 | 3 | 1 | | 10, 3 |
| Venus | | 3 | 2 | 1 | 30 |
| Mars | 1 | 3 | 1 | 1 | 19 |
| Moon | 3 | | 1 | | 4 |
| Europa, Ganymede, Callisto, Titania, Triton | 2 | | 1 | | 2, 6, 5, 1, 3 |
| Earth | 1 | | 2 | 3 | 30 |
| Titan, and the ground inside Jupiter, Saturn and Uranus | | | 1 | 2 | 6, 30, 29, 16 |

"Haunts asked for" is one for every 80,000 square metres of the whole world, from 1 to 30. A place is thrown away if
any of these is true:

- it is in the sea, or less than a metre above it;
- it is in a pond, or within 6 m of one;
- the ground within 4 m is steeper than 0.35;
- the ground 15 m off is more than 5 m above or below it (there must be room to fight round it: a Strider wanders
  22 m, and a Roller charges from 5 to 22 m);
- it is within 6 m of a cave;
- it is nearer than 140 m to another haunt, 80 m to a camp or a base, or 250 m to the launch pad.

So a world with much sea (Earth) or many camps has fewer haunts than it asks for. There are none on the Sun, the
asteroids, or the three moons under 80 m in radius (Phobos, Deimos and Enceladus: too small for a 7 m crab). There are
none on Neptune either: its ground is the bottom of its ocean, and its sea has the swimmers.

Each haunt has its own lot, drawn from its seed. At ante 1 the haunts whose lot is under a half are lived in, at
ante 2 three quarters, and every one from ante 3. One lived in at an ante is lived in at every later one, so a world
fills up as the run goes on and nothing that was there goes away. An animal is never tougher at a later ante.

## Waking and putting away

- The nearest haunt that is lived in is woken once the player is within 110 m of it, at most one every half second,
  never more than two land creatures at once, and only once the ground under the player is showing.
- It is never woken nearer the player than 70 m, so none is seen to appear beside them. An ante that starts beside a
  haunt it has just filled, or a landing on top of one, leaves it until the player has gone.
- It is put away beyond 180 m, or as soon as the player leaves its world. One that was killed stays dead for the rest
  of the run. One that was put away comes back whole the next time.
- It is stood up at its haunt, or at the nearest spot round it (rings at 5 and 10 m) where nothing stands where its
  body would be and the ground is at least a metre above the water. It faces a seeded way along the ground.
- The haunts are worked out on a background thread the first time the player is near a world. A world's terrain never
  changes once built, so its rules are safe there. This runs after the world's camps are known, so the haunts keep
  clear of them. Until the search is back there are simply no land creatures on that world.

## Keeping to a round, hilly world

The four were written for the practice range, where the ground is flat, "up" is +Y and a step of more than 30 cm is a
wall. Out on a world (`Creatures/CreatureGround.cs`) the rules are these.

**What they stand on.**
- The world's ground as it is really built: the triangles of the tile showing there, which are also what the player
  collides with (`PlanetSurface.GroundRadius`).
- It is found without colliders, so an animal keeps to the ground wherever it is awake (colliders are only built within
  90 m of the player and the ship).
- "Up" is away from the world's middle.
- Every place an animal remembers (its home, where the player was) is kept in the world's own space, because the world
  can be moved and turned under the player at any moment.

**What refuses a step.**
- The ground climbs or drops more steeply than the kind walks. This is rise over run against the world's up, measured
  over the half metre ahead (`SlopeRun`), never over one frame's few centimetres: where tiles of different detail meet,
  or where a tile is built again finer, the ground moves by centimetres, which over a frame would read as a cliff. The
  Strider takes 0.6, the Sprinter and the Screamer 0.7, and the Roller 0.6 on foot and 0.45 as a ball.
- It would take the animal further into the water, the sea or a pond. A step out of the water is always allowed, so
  one that finds itself at the edge walks out.
- It would step into a cave's mouth: the built ground spans the hole cut for it.

**What stops a step (a wall).**
- Something stands on the ground there higher than the kind steps over: 30 cm (a shin) for the three small kinds, and
  1.2 m for the Strider, whose body rides 2 m up on long legs. Rocks, trees, crates and a base's floor all count.
- The probe for walls ahead is measured from the ground there, so a hillside is never taken for a wall.

**How it stands.**
- The walkers stay upright to the world, and their feet follow the ground (`WalkerAnimInput.Ground`, fed by
  `GroundUnder`). A planted foot is put back on the ground each frame, a swinging foot leaves from and lands on the
  ground, and the body's springs follow the feet, so it lies with a slope. The animator is given the move the walker
  really made, so its feet do not slide through a refused step. See "Walkers' legs on slopes".
- The Roller lies along the ground under it, flat or as a ball, at most 34 degrees from upright. This is eased in, so a
  bump under one end is not a jerk.
- The Screamer on foot lies along the ground at most 20 degrees. In the air it stays upright to the world, eased, so
  taking off from a hillside is not a snap. Its root is eased onto the ground or the water's top below it at
  7 metres a second, so flying over a cliff is not a jump. Where the ground rises faster than that (a cliff band, a
  canyon wall), its root climbs as far as it must for its body never to be drawn inside the ground, half a metre clear.

**In the practice range nothing changes.** It keeps the flat footing and the old rules line for line (a raycast from
above, a step of more than 30 cm a wall), which the rule tests and the existing tests hold.

## Walkers' legs on slopes

The walkers' legs were drawn for flat ground. Once their feet followed a slope, the leg as drawn could fall short of
the foot it stood on. Measured offline with the game's own animator (a probe test, not kept), walking straight before
this change: a foot that was down was drawn more than 3 cm from where it stood on 7,831 of about 55,000 frames for the
Sprinter and 552 for the Strider, by up to 60 cm.

On a world (when the animator is given the ground) the walk now keeps every foot within reach:

- A foot steps once its leg is stretched past 90% of its reach (`ReachStep`), whatever the gait would have done.
- A foot is put down within 86% of its reach (`ReachPlace`).
- The body comes down until every foot that is down is within 88% of its reach (`ReachBend`), never by more than 60% of
  the way to its belly. So on a steep slope a walker walks crouched.
- If a foot is still out of reach, it slips along the ground toward its leg until it is 3 cm inside its reach
  (`SlipShort`). This is the last resort and is counted (`Slips`).

The same probe on the final animator, over eight slopes from 0.7 down to 0.7 up, four speeds each, six seconds a run:

| | Frames a foot is drawn more than 3 cm off | Slips (worst run) | Body over the ground |
|---|---|---|---|
| Strider, straight | 0 of 55,488 | 3.4% of foot frames (0.7 up at 3.4 m/s) | 1.44 to 2.45 m |
| Strider, turning | 0 of 52,040 | 5.9% (0.7 up at 3.4 m/s) | 0.95 to 2.28 m |
| Sprinter, straight | 0 of 53,546 | 16.7% (0.7 up at full pace, 6.2 m/s) | 0.34 to 0.61 m |
| Sprinter, turning | 48 of 52,274, at most 16 cm | 21.8% (0.7 down at full pace) | 0.33 to 0.75 m |

On gentle ground (0.3) a slip is rare (the rule test holds it under 5%). A slip shows as a foot sliding a little on
the steepest ground at full pace, which is a question for the Deck. In the practice range the animator is given no
ground and the walk is exactly as it was (rule test: no slips).

## Paying, warning and the music

- **Credit.** Every animal on a world is given its credit (`CreatureCredit`): its kind, its world and a claim per haunt
  (`creature:Mars.land4`). It is paid once a run on the killing hit, like a camp's kill: the Strider as a camp's heavy,
  the others as a marksman. It earns the same feats, ON A ROLL, bonus missions and unlocks (pass 3).
- **Only a kill it could answer pays** (`LandReach`). This is the land version of the swimmers' rule. A land animal
  after the player can no longer answer them once it has been kept from them for 4 seconds: the player is out of its
  reach, its steps were refused (too steep, a wall, the water) and it gained no ground. That is the case on a ledge or a
  tower it cannot climb, across a drop, at the sea's edge or behind a rock. It can answer again the moment it gains
  half a metre on them in a second, or they come within its reach. Kiting is answering: an animal that follows the
  player gains ground however fast they back away. A second in which no step was refused and no ground was gained
  neither counts nor clears (on its back, out of breath, warning, dragging itself), so its own opening never takes its
  pay away. A kill it could not answer pays nothing and the note says why: KILLS PAY ONLY WHEN IT CAN GET AT YOU (the
  swimmers keep theirs, `CreatureTiers.NoPayNote`).
- **Warnings.** Its warnings (pass 2), the marks of where a hit landed and the aim line (pass 7) are the same as in the
  practice range: they come from its own brain and body, not from where it lives.
- **Music.** The music hears a land animal as being hunted when it sees the player, or was hurt and still knows where
  they are, like a swimmer that has noticed them (`LandCreatures.AnyAware`, beside `SeaCreatures.AnyAware` in
  `GameAudioHooks`). Its warnings count as a fight, as before. An animal with nothing left to hurt the player with (a
  walker with no claws or jaws, a Roller with too few plates to roll, a Screamer with no drums) is not after them, so
  the music lets go as it makes off.
- **A creature's fight is a fight for the whole game** (`ReadoutFocus.InFight` with the creature stamps). A creature's
  warning holds it as a sighting does, and a creature after the player holds it every frame it is. So in a creature's
  fight the readout keeps to what a fight needs, no tip starts, an unlock's banner and its save wait, the suit's beads
  go dark, and the restless camps' quiet clock does not run, so no squad is sent with a radio call in the middle of
  one. Before this pass only the beads knew about creatures.
- **Death words.** A creature's strike that ends the run says what it was on the death page (`StrikeWords`, through
  `PlayerHealth.LastCause`), never its placeholder name: "Caught in a giant claw.", "Bitten in a lunge.", "Run down by
  a rolling shell.", "Butted by a shell.", "Screamed down." The other five creatures got their own words too ("Struck
  down by a rearing head.", "Dragged toward a burrow.", "Caught by something that heard you.", "Drawn into a giant
  mouth.", "Bitten by a shoal."). Before, every creature's kill said "Shot down outside the ship."
- **The Roller's crash.** Ground too steep to roll up, or the sea's edge, now ends its roll like any other opening
  (1.5 s on its back), and it falls open back the way it came so it is not left hanging over the lip. Only rolling into
  something solid that stands up is a crash (3.6 s on its back).

## How it is built

- `Creatures/LandRules.cs` (plain C#):
  - `LandKind`.
  - `LandFieldRules`: the dials, on the `CreatureConfig` asset as `land`.
  - `LandHaunt`.
  - `LandMix`: a world's weights, and the pick from a haunt's own seed.
  - `LandHabitats`: which animals live on which world, the model each is drawn with, which fly, and `Leader`.
  - `LandField`: how many haunts, where, and which are lived in at an ante.
  - `StepResult`: taken, a wall, or the ground.
  - `LandStep`: too steep, the plane under a body, the lean.
  - `LandReach`: whether an animal after the player could still answer them.
- `Creatures/CreatureGround.cs`:
  - `LandFooting`: a `Footing` on a world's built ground: up, the ground under a point, the water's edge (the sea and
    the ponds), a cave's mouth, a step, places held in the world's space.
  - `CreatureGround`: the rules the four share. Where to probe for a wall, a step, keeping on the ground and upright, a
    flier's height, and the same old rules in the practice range.
  - `GroundUnder`: the ground's height in walker space, for the animator.
- `Creatures/LandCreatures.cs`, on the solar world (`SolarSystemWorld.Land`):
  - finds each world's haunts on a background job (`PlanetJobs`, ranked as a ground tile 400 m off and never on the
    last free core, so the ground under the player comes first);
  - wakes and puts away, remembers the dead and gives credit;
  - reads the four models while the run is got ready;
  - stamps `CombatWorld.CreatureAwareAt` every frame one is after the player (as `SeaCreatures` now does).
- `Creatures/StrikeWords.cs` (plain C#), and `PlayerHealth.LastCause`, read by `Astronaut` for the death page.
- `Walker`, `Roller`, `Screamer`:
  - a `Footing`, the steepest ground they take and what they step over;
  - `AwareOfPlayer`, which needs the means to hurt;
  - `InReach` from their `LandReach`;
  - their home (and the Screamer's memory of the player) kept in their parent's space.
  - `WalkerAnimator` reads the ground under planted and swinging feet and keeps them within reach when it is given the
    ground.
  - The Sprinter syncs its hitboxes only while the player sees it or is within 40 m.
  - `RollerSenses.Stopped` is the ground ending a roll, which is not a crash.
- `Tasks/ReadoutFocus.CreatureFight` and `InFight` with three times; `CombatWorld.CreatureWarnedAt` and
  `CreatureAwareAt`; `AnteLoop.InFight`, `EnemySpawner.Restless` and `SuitHud` ask it.
- `CreatureTiers.KeptAway`, `NoPayNote`; `TaskIds.LandCreature`; `GameAudioHooks` hears `LandCreatures.AnyAware`.
- `Planets/BuiltGround`: the plane cache has 1,024 places (it had 256), because two land creatures now read the ground
  under themselves and their feet every frame beside the player's feet.

## Reviews

Three reviews read the change before it was committed (design, Unity, Steam Deck cost). None of them ran Unity.

**Design review** (the game-designer agent; its measurements were a copy of the placement rules in Python over 20
seeds a world, geometry only):

1. A creature's fight did not count as a fight for the rest of the game: tips could start, an unlock's banner and its
   save could come in the middle of a strike, and the restless camps' clock counted the fight as quiet and sent a squad
   with a radio call. **Applied:** one test everywhere (see "Paying, warning and the music").
2. A creature's kill said "Shot down outside the ship." on the death page. **Applied:** `StrikeWords` for all ten
   strikes.
3. Kills the creature could never answer paid full price: from a butte top or a canyon rim the player could shoot one
   held below. **Applied:** `LandReach` and its note. **Not applied:** the review also asked for the animal to back off
   toward home when held; it now stands at the foot of the ledge and pays nothing, and backing off is left open.
4. The Roller's long crash fired on a threshold the player cannot see (ground steeper than 0.45). **Applied:** the
   ground only ends a roll.
5. Camps will join most creature fights, and placement cannot prevent it: 83 to 100% of haunts, by world, have a camp
   within 120 m, which is how far enemies hear a shot. A larger `campClear` would empty the worlds (at 100 m Mars keeps
   4 of its 19 haunts). **Kept at 80 m**, and said plainly here: in a run, a fight with an animal will often bring a
   camp's squad to the sound. Today the two ignore each other, and enemy fire passes through creatures. That is the
   next gap (see the scorecard, gap 12).
6. The music stayed tense over a harmless animal making off. **Applied:** `AwareOfPlayer` needs the means to hurt.
7. Players could not tell the world mixes apart: Callisto (2, 0, 2, 0) and Titania (1, 0, 1, 0) were the same mix in
   all but the numbers, the test counted the numbers, and a moon's few haunts cannot show 3:1 from 2:1. **Applied:**
   each kind of world is led by its own animal, the Roller at most a third everywhere, tested on proportions.
8. Numbers. Kept: one haunt per 80,000 square metres, 140 m apart, 250 m from the pad, waking at 110 m and putting away
   at 180 m (on foot nobody sees one appear: a Strider's top drops below a standing eye's horizon at about 49 m on the
   Moon, 76 m on Mars and 90 m on Earth), two at once, ante shares 0.5, 0.75 and 1. **Applied:** never woken within
   70 m; a haunt needs room to fight (the 15 m ring); the Strider steps over 1.2 m; the slope test runs at each kind's
   own steepest slope.
9. Follow-ups: findings 1 to 4 as one pass (done here), and creatures and the camps noticing each other (gap 12).

**Unity review** (the unity-reviewer agent):

1. A step's slope was judged over one frame's run, so tile seams and a tile rebuilt finer read as cliffs: a Strider
   would stutter at a seam and a rolling Roller would "crash" in open ground. **Applied:** `SlopeRun`.
2. A flying Screamer sank into steep ground (its root rose at most 7 m a second). **Applied:** its root climbs with the
   ground as far as it must.
3. Animals walked on air over cave mouths. **Applied:** `CaveMargin`.
4. Feet slid on refused steps. **Applied:** the walker's animator is given the move it really made.
5. Take-off snapped upright. **Applied:** eased.
6. Ponds were ignored. **Applied:** a pond is water for haunts, steps and fliers.
7. A ball crashed at a downhill lip. **Applied** with design finding 4.
8. No budget row. **Applied:** "Land creatures" in `docs/performance_budget.md`.

The review also checked by reading that the practice range is the same line for line, that the lifecycle (footing,
credit and steepness set straight after `Create`, before the first `Update`) is sound, that places are kept in the
right space, that the background job reads only terrain that never changes, and that music and credit are wired.

**Deck cost review** (the deck-performance agent; estimates from the code and a model of the plane cache's hash):

1. Plane-cache misses were the largest cost: two walkers add about 25 triangles to a 256-place cache, guessed 0.1 to
   0.3 ms a frame. **Applied:** 1,024 places (the slot is picked from the hash's top 10 bits), guessed 0.5 to 1 miss a
   frame, about 40 KB a world.
2. A tile rebuilt finer under a walker misses its triangles at once. Partly helped by the larger cache.
3. The wake frame builds about 40 objects at once, guessed 2 to 4 ms. **Left** for the Deck: if it measures over 3 ms,
   build it over several frames, as the biter shoal does.
4. The Sprinter flushed the whole scene's moved transforms every frame, even far off. **Applied:** only while the
   player sees it or is within 40 m.
5. Loose pieces each get a `GravityBody` and sit outside the gib caps. **Left:** cap them on worlds if they prove dear.
6. The four models' pictures add about 30 MB of texture memory, read once a session. Recorded in the budget.
7. to 9. The haunt search, allocations (none a frame) and physics queries are fine.

Deferred from the Deck review, to be measured first: reading a planted foot's ground once as it lands, the lean four
times a second instead of every frame, building a wake over several frames, and capping loose pieces.

## Playtest protocol

Not run: nothing here is a playtest result.

- **Testers:** five or more, some who have met the creatures in the practice range and some who have not.
- **Setup:** runs from the start, on the Deck. Land on the Moon, Mars and Earth in each run, and on one gas giant's
  ground if the run gets that far.
- **Record:**
  - every land animal met: world, kind, ante, how far off it was first seen, and whether the player chose to fight it
    or went round it;
  - every fight: time, hits by zone, how it ended, and whether a camp's squad joined it;
  - any animal seen standing in the air, sunk into the ground, stuck on a slope or a rock, walking into the water,
    turning to face the wrong way, or with a foot visibly sliding;
  - every time the note KILLS PAY ONLY WHEN IT CAN GET AT YOU shows, and whether the player understood why;
  - every death to an animal, and whether the death page's words told the player what killed them;
  - the frame time while one is woken.
  Since pass 9 a development build writes these counts to its playtest log (`docs/reports/playtest-log.md`); record
  the screen too until the log has been checked against a recording.
- **After each landing, ask:** what lives on this world? Did you go after it or keep away, and why? Did it feel like
  part of the world or dropped in?
- **Report counts,** not percentages.

Acceptance on the Deck (first guesses, to be revised by the first session):

- No animal is seen floating, sunk, sliding down a slope it cannot climb, or walking into the water.
- No hitch is felt when one wakes (frame time within the budget's 3 ms for the wake).
- Testers name an animal of at least two of the worlds they landed on.
- Some fights with animals are chosen and some avoided: neither is always the answer.
- Every tester who sees the kept-away note can say why the kill did not pay.

## Still unverified

- Nothing here has been run in Unity. The animals' footing on real tiles, their lean, the walkers' feet on slopes and
  the Screamer's flight over a world are read from the code and the rule tests.
- Whether a foot slipping on the steepest ground at full pace, and a Strider walking crouched on a steep slope, look
  right.
- Whether the walls a step meets on a world (rocks, trees, a base's floor) are the ones that should stop it, and
  whether an animal can be trapped among them.
- Whether the mix of animals per world, the number of haunts and the ante's shares make the worlds feel alive without
  crowding the camps.
- Not built:
  - the Ambusher, the Cave hunter and the Blind stalker on the worlds (they need soft ground and caves, which their
    brief asks for);
  - creatures and the camps noticing each other (gap 12);
  - an animal held below a ledge backing off toward home;
  - melee and the rover hurting the walkers;
  - an animal that was put away comes back whole;
  - a Screamer that lands on water stays on the water's top until it flies again.
