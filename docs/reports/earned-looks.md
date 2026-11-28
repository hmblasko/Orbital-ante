# Earned looks (Ralph loop pass 5, 28 Nov 2026)

Pass 4 made the envelope's prizes do something: a gun skin, a ship paint or a suit piece now shows in the game. But
envelopes open only after ante 9 (Matthew's rule, 16 Nov 2026, kept), and a first-time player is expected to die
around ante 3 or 4 (Part B section 5). So for many runs a new player's Collection stayed empty and none of the looks
reached them. This pass gives eight goal unlocks a look instead of a perk or a kit, three of them early enough for a
first run, puts each look on the moment it is earned, and keeps every one of them out of the envelopes so the odds
printed on the envelope stay true. **Nothing here has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | The Collection was empty for a new player for many runs: looks came only from envelopes, which open after ante 9. The goal unlocks gave only perks and kits, and the easiest of those wanted ante 2 cleared or a crouched kill. |
| Hypothesis | One-run goals that a first run tends to meet while playing (a landing on the Moon, a disarm, a leg shot off) put a look in the Collection in the first run or two. Each one goes on as its moment shows, with no press. That shows a new player that the Collection matters, and gives every run another thing to aim at. |
| Principle | Discovery that matters (4), meaningful replayability (6). Rule 9: one feat inside one run, nothing counted across runs, no meters, and the envelope's public odds unchanged. Cognitive load (7): the Unlocks page splits into two short lists, not one page too long to read. |
| Smallest test | Rule tests on the catalogue, the counting, the words, the save, the envelope roll, the Collection's rows and the places looks take (`UnlockTests`, `EarnedLookTests`, `LockerTests`). |
| Acceptance | Each new goal is met by its own feat and nothing else, once a run. Each look goes to work by itself only where its place is empty, and says so when it cannot. An envelope's prize takes the place of an earned look, never the other way round. The envelope roll is the same with or without the earned looks for every envelope tested, and never gives one. Every earned look is given by exactly one goal. The words are true to what the code counts. |
| Playtest protocol | On a fresh profile, play two runs on the Deck. Note which looks came and in which run, whether the banner and its note were read in the moment, and whether the look was seen going on (the paint on the lander, the skin on the gun in hand, the badge on the suit plate). Open the Unlocks page with a controller: check that both lists can be reached and that each fits the screen. Open the Collection: check that an earned look says "earned" on its row and has a cream stripe. Go to the practice range first on another fresh profile and shoot a limb off: check the note says goals count only in a run. |

## The eighteen unlocks, easiest first

Looks are mixed into the order by how soon a new player is likely to meet them. The recap, the title screen and an
empty Collection name the next ones in this order, a look with its kind beside its name ("Roll Pattern (ship paint),
for a landing away from Earth").

| Unlock | Gives | Goal (one run) |
|---|---|---|
| Roll Pattern | Ship paint (black band on white) | Land on any world but Earth |
| Butterfingers | Gun skin, every gun (butter-coloured enamel) | A disarm: the gun shot out of an enemy's hands, or the hand or arm holding it shot off |
| Leg Day | Badge on the suit's readout plate | Shoot a leg off an enemy standing on two |
| Gunner kit | Kit | Clear ante 2 |
| Patient Hunter | Ghost perk | A crouched or prone kill |
| Tunnel Vision | Visor tint (smoked glass) | A kill while Focus slows the world |
| Road Rage | Pilot perk | A rover splat |
| Hard Hat | Helmet shade (amber edge) | Carry a salvage crate back aboard the lander |
| Afterburner | Jet flame colour (violet) | An AIRBORNE kill (not from the rover) |
| Ghost kit | Kit | A LONG SHOT kill |
| Bounty Hunter | Readout plate colour (rust, brass edge) | Finish a bonus mission |
| Pilot kit | Kit | Land on three worlds in one ante |
| Change of Pace | Wild-card perk | Score four of the five ways in one ante |
| Daredevil kit | Kit | A kill with armour under 30% |
| Sharkskin | Pistol skin (flake) | Kill a sea creature from in the water with it |
| Trigger Discipline | Gunner perk | Climb the gun ladder to its last gun |
| Heavy Swing | Brawler perk | A melee kill |
| All In | Daredevil perk | Raise the stakes, then beat that ante's clock |

- **Why these three first.** The first tips after liftoff point at the map and the autopilot, and the Moon is the
  nearest world, so the first flight's landing is the easiest goal there is; it is also the one the practice range
  cannot meet, so a player who starts there still has the first look waiting in a run. A new player fights from the
  first minutes, and How to play teaches that limbs come off ("shoot the gun or the arm to disarm, a leg to drop
  them"), so a disarm and a leg shot off come in the course of the first fights. None of the three needs a mode, a
  key or a place the player has not yet met.
- **Every look has a place of its own** (rule test): two looks for one place would leave the second waiting in the
  Collection the first time it is earned. The eight places are the ship paint, the skin for every gun, the badge, the
  visor, the helmet, the jetpack, the suit's plate and the pistol's own skin.
- **What a look is called.** Its kind says what it changes, never a piece of gear: "a visor tint", "a helmet shade",
  "a jet flame colour", "a readout plate colour" (beside the upgrades, "a jetpack" or "a helmet" read as new kit). The
  suit and the badge show on the suit's readout plate (the player never sees the suit itself), so their words say
  "on your suit's readout".
- **New goals go at the end of `UnlockGoal`**, as the save keeps unlock ids and never goal numbers. The listing order
  is the catalogue's, not the enum's.

## How a look is earned and kept

- **Counting** (`UnlockTracker`, plain C#): each goal is one feat in one run, met once. `Disarmed`, `LegOff`,
  `SalvageHome` and `BonusMissionDone` are new; `Kill` takes `airborne`, `inFocus` and `seaCreature`; `Landed` meets
  the landing goal on any world but `UnlockTracker.HomeWorld` (`LaunchSite.BodyId`, "Earth", the world every run
  starts on). A rover jump is not an AIRBORNE kill for the goal (the rover's splat has its own).
- **Where the feats come from** (`TaskDirector`):
  - Disarm: a gun knocked out of an enemy's hands arrives as a hit with `Disarmed` set. An arm shot off takes the gun
    with it but arrives as a sever, so `MannequinCombat.DisarmedFrame` stamps the frame its gun left its hands and the
    director reads it on the hit that severed the arm (the body raises the part's loss before the hit is reported, so
    the frame is the same one).
  - Leg off: each hit record keeps the legs the enemy stood on (`HitRecord.Legs`, `LegsStoodOn`); a hit that leaves
    one that stood on two with fewer, and does not kill it, is a leg off. A torso blown off takes both legs with it but
    is not a leg shot off, so it never counts.
  - Focus kill: the kill's facts read `DeepBreathFx.Amount` against `TaskRules.FocusKillAt` (0.5, half way into the
    slow), so the last instant of a fading Focus does not count.
  - Sea kill: only a swimmer (`CreatureTiers.Swims`: the Drifting maw and the Pack biter) killed within its reach,
    which already means from in the water with it (pass 3); out of reach pays nothing and counts nothing. The goal's
    words say where they live: deep water, well out from the shore.
  - Salvage home: the crate's delivery, after its own score. Bonus mission: the contract's completion, after its pay.
- **Granting** (`UnlockBook.Grant`): a look is added to the Collection (a real line: number 0, never a test item, so
  clearing test items never takes it) and put to use with `Locker.AutoUse`, which only fills an empty place. A look
  whose place is taken waits in the Collection, and its words say so.
- **It goes on as its moment shows.** When the banner shows (or the break or victory page names it), the game reads
  the looks again (`LockerLook.Refresh`, which raises `LockerLook.Changed`): the lander's hull is painted again, the
  gun in hand wears its skin, the suit plate, visor tint, helmet shade, badge and jet flames take their colours. The
  words say "now on your lander", never "from your next run".
- **An envelope's prize takes an earned look's place** (`Locker.GivesWay`): the earned looks fill their places early,
  and without this the ante 9 reveal would go to work for nobody. The earned look stays in the Collection, and the
  reveal names it: "In use on your lander, in place of Roll Pattern (still in your Collection)." An earned look never
  takes the place of anything the player put to use or won.
- **The unlock is the record.** If an earned look's Collection line is ever missing, opening the Collection puts it
  back without putting it to use (`UnlockBook.KeepEarnedLooks`), so mending a save never chooses for the player.
- **The save waits out a fight.** A goal's save is written the frame after, unless an enemy has the player in sight;
  then at the fight's end, at a pause, or `UnlockMoment.FightWaitMost` (20 s) later at most (`UnlockMoment.SaveNow`),
  so a write is never felt mid-fight. Every way a run ends saves anyway.

## Kept out of the envelopes

- **The odds stay true** (rule 9). `PrizeRoll.InEnvelopes` leaves earned looks out of the item roll, the Celestial
  roll and the "is this rarity stocked" check, and the roll still draws the same numbers. A rule test rolls 3,000
  envelopes against three collections with the full catalogue and with the catalogue as it was before this pass: every
  prize is the same, and none is an earned look.
- **Earned, not a rarity.** An earned look is stored as Common (for sorting only) but is shown as "Earned", "Earned
  weapon skin (any gun)". Its stripe is the screen's cream paint, no rarity's colour and not the IN USE green, and its
  row says "· earned" in words too, so the colour is never the only sign. The TF2-style tiers of section 12 keep
  meaning how rare a prize is in an envelope. The Collection's details add "Earned for a disarm".
- **An empty Collection** names the next look and how ("Nothing yet. Looks are earned by goals in a run. The next:
  Roll Pattern (ship paint), for a landing away from Earth"), then the envelope.

## On screen

- **The moment** is the goal unlocks' own (pass 1): the green stencil banner, "UNLOCKED: ROLL PATTERN", the note
  "LANDED AWAY FROM EARTH · A SHIP PAINT, NOW ON YOUR LANDER" (or "..., IN YOUR COLLECTION" when its place is taken),
  and `reward.unlock`. At a break: "UNLOCKED: Butterfingers, for a disarm. A gun skin, now on every gun."
- **The Unlocks page** has two lists, perks and kits (ten) and looks (eight), because eighteen with their goals do not
  fit one 16:9 page. A button beside Back switches them and names the other list's new ones ("Looks (2 new)"). The
  page opens on a list with something new, the looks first, then on the list holding the next unlock. Each list's NEW
  marks go once that list has been shown. The title screen's Unlocks button counts both. Its looks list says the
  practice range does not count.
- **The practice range says so once.** Goals count only in a run, so the first disarm, limb or kill there in a
  session shows "PRACTICE. Unlock goals count only in a run, not here." for 4 s on the arena's banner, while a goal is
  still to be met and tips are on (`PracticeNote`).
- **How to play** says a goal "opens a new perk, starting kit or look".
- **The goal words carry the player's keys**: the Focus key (`UnlockHints.focusKey`) and the Interact key.
- **The two earliest looks are quiet.** Butterfingers is muted butter enamel, not stripes, because the first look
  most players earn goes on every gun; Tunnel Vision is smoked glass, a shade darker, never the blue the Focus rim
  means the slowed world by.

## Reviews

Each finding was checked before it was applied.

- **game-designer:** the looks showed only from the next run, so the moment promised something unseen (now they go
  on at once); the order put two fights before the one goal the practice range cannot meet (the landing now leads);
  "a jetpack", "a helmet" and "a suit" read as new gear beside the upgrades (kinds now say what they change); a look's
  name says nothing of what it is (its kind goes beside it on the recap and title screen); the earned green was the IN
  USE green (now cream, and "earned" in words); stripes on every gun as the first look looked like a toy (now muted
  enamel); the Tunnel Vision blue was the Focus rim's (now smoke grey); the sea goal's words did not say where sea
  creatures are; a new player who starts in the practice range would wonder where the unlocks went (the note). Its
  view: unlockables at 8 once the first three were done. Scored 7 here, because the Celestial tier's effects are still
  not drawn, which the cloud can still do (see the scorecard).
- **unity-reviewer:** an envelope prize won after the earned looks would never go to work, because every place was
  taken (now an earned look gives way, and the reveal names it); a torso blown off counted as a leg shot off (now it
  must still have a torso); the arm-sever disarm reaches the director in the same frame (checked: `Mannequin.ApplyHit`
  raises the part's loss before the hit); the Unlocks page kept a reference it no longer used (removed).
- **deck-performance:** a goal's save in the middle of a fight could be felt as a hitch on the Deck (now it waits, see
  above). Budget rows written.
- **The fixes, reviewed again.** Putting a look on at its moment and the practice note were built after the three
  reviews, so unity-reviewer and deck-performance read that code again:
  - deck-performance: every refresh built the hull again (2.0 ms and 1.2 MB measured off-engine on a cloud Xeon,
    guessed 2.5 to 6 ms on the Deck), whatever look changed; now only a new paint does (`LockerLook.ShipPaintId`).
    The moment can fall in a fight after its 20 s wait, and two looks at one break or a reveal beside its save
    refreshed twice in a frame; now `LockerLook.RefreshSoon` runs one refresh on the next frame that writes no save
    (`RefreshIfDue`, `SaveSystem.SavedFrame`). The suit plate, badge and jet material are touched only when they
    changed, and the practice note reads the save only for a hit that would have met a goal.
  - unity-reviewer: the retuned colours of Butterfingers and Tunnel Vision would never reach a machine whose item
    assets were made before them (an item's asset is made once); now an earned look's colours and blurb follow the
    catalogue like an upgrade's numbers (`ItemCatalog.FollowEarnedLook`, `ItemCatalog.Version` 3; 9999 keeps an
    asset's own). A practice note cut short by a restart or a quit came back on the next world; now every world
    clears it. Checked and found sound: every listener leaves on every way its object is destroyed, the hull's old
    mesh is freed once, the shade picture is freed only after nothing shows it, and the visor tint never outlives
    the suit.

## Tests

- `dotnet test tools/rules-tests`: 3,165 run, 3,164 passed, 1 skipped (the cabin bake check, as before). New:
  `EarnedLookTests` (8), fourteen in `UnlockTests` (the first three goals and their places, every look's place and its
  goal, each new goal met only by its feat, a rover jump, granting into an empty and a taken place, the words, the
  keys, mending, the two lists' NEW marks, the next look, the home world named by the id the game uses, the save
  waiting out a fight, the practice note) and one in `LockerTests` (an envelope prize takes an earned look's place, an
  earned look never takes a prize's). Four older tests follow the new order, the reveal's words lost "from your next
  run", and the three worlds test now uses the game's real body ids, which it had written in lower case.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.

## Still unverified

Nothing here has run in Unity. Not yet checked there: the arm-sever disarm reaching the director in the same frame
the gun is dropped, the looks going on at their moment (the lander's hull swapped, the gun in hand re-skinned, the suit
plate, badge, visor tint, helmet shade and jet flames recoloured), the eight looks on the real guns, suit plate and
lander, the Unlocks page's two buttons with a controller, the practice note on the arena banner, and how soon a new
player really meets the first three goals (a playtest question).
