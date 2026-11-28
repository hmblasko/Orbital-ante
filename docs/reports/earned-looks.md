# Earned looks (Ralph loop pass 5, 28 Nov 2026)

Pass 4 made the envelope's prizes do something: a gun skin, a ship paint or a suit piece now shows in the game. But
envelopes open only after ante 9 (Matthew's rule, 16 Nov 2026, kept), and a first-time player is expected to die
around ante 3 or 4 (Part B section 5). So for many runs a new player's Collection stayed empty and none of the looks
reached them. This pass gives eight goal unlocks a look instead of a perk or a kit, three of them early enough for a
first run, and keeps every one of them out of the envelopes so the odds printed on the envelope stay true. **Nothing
here has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | The Collection was empty for a new player for many runs: looks came only from envelopes, which open after ante 9. The goal unlocks gave only perks and kits, and the easiest of those wanted ante 2 cleared or a crouched kill. |
| Hypothesis | One-run goals that a first run tends to meet while playing (a disarm, a leg shot off, a landing on the Moon) put a look in the Collection in the first run or two. Each one shows on the next run without a press. That shows a new player that the Collection matters, and gives every run another thing to aim at. |
| Principle | Discovery that matters (4), meaningful replayability (6). Rule 9: one feat inside one run, nothing counted across runs, no meters, and the envelope's public odds unchanged. Cognitive load (7): the Unlocks page splits into two short lists, not one page too long to read. |
| Smallest test | Rule tests on the catalogue, the counting, the words, the save, the envelope roll and the Collection's rows (`UnlockTests`, `EarnedLookTests`). |
| Acceptance | Each new goal is met by its own feat and nothing else, once a run. Each look goes to work by itself only where its place is empty, and says so when it cannot. The envelope roll is the same with or without the earned looks for every envelope tested, and never gives one. Every earned look is given by exactly one goal. The words are true to what the code counts. |
| Playtest protocol | On a fresh profile, play two runs on the Deck. Note which looks came and in which run, whether the banner and its note were read in the moment, and whether the look was noticed on the next run (the skin on the first gun, the badge on the suit plate, the paint on the lander). Open the Unlocks page with a controller: check that both lists can be reached and that each fits the screen. Open the Collection: check that an earned look says Earned, in green. |

## The eighteen unlocks, easiest first

Looks are mixed into the order by how soon a new player is likely to meet them. The recap and the title screen name
the next ones in this order.

| Unlock | Gives | Goal (one run) |
|---|---|---|
| Butterfingers | Gun skin, every gun (stripes) | A disarm: the gun shot out of an enemy's hands, or the hand or arm holding it shot off |
| Leg Day | Badge on the suit plate | Shoot a leg off an enemy standing on two |
| Roll Pattern | Ship paint (black band on white) | Land on any world but Earth |
| Gunner kit | Kit | Clear ante 2 |
| Patient Hunter | Ghost perk | A crouched or prone kill |
| Tunnel Vision | Visor tint (pale blue) | A kill while Focus slows the world |
| Road Rage | Pilot perk | A rover splat |
| Hard Hat | Helmet (amber edge shade) | Carry a salvage crate back aboard the lander |
| Afterburner | Jetpack (violet flame) | An AIRBORNE kill (not from the rover) |
| Ghost kit | Kit | A LONG SHOT kill |
| Bounty Hunter | Suit (rust plate, brass edge) | Finish a bonus mission |
| Pilot kit | Kit | Land on three worlds in one ante |
| Change of Pace | Wild-card perk | Score four of the five ways in one ante |
| Sharkskin | Pistol skin (flake) | Kill a sea creature from in the water with it |
| Daredevil kit | Kit | A kill with armour under 30% |
| Trigger Discipline | Gunner perk | Climb the gun ladder to its last gun |
| Heavy Swing | Brawler perk | A melee kill |
| All In | Daredevil perk | Raise the stakes, then beat that ante's clock |

- **Why these three first.** A new player fights from the first minutes, and the game teaches that limbs come off
  (How to play: "shoot the gun or the arm to disarm, a leg to drop them"), so a disarm and a leg shot off come in the
  course of the first fights. The Moon is the nearest world and the first flight's obvious target. None of the three
  needs a mode, a key or a place the player has not yet met.
- **Every look has a place of its own** (rule test): two looks for one place would leave the second waiting in the
  Collection the first time it is earned. The eight places are the skin for every gun, the pistol's own skin, the
  badge, the ship paint, the visor, the helmet, the jetpack and the suit.
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
    director reads it on the hit that severed the arm.
  - Leg off: each hit record now keeps the legs the enemy stood on (`HitRecord.Legs`, `LegsStoodOn`); a hit that
    leaves one that stood on two with fewer, and does not kill it, is a leg off.
  - Focus kill: the kill's facts read `DeepBreathFx.Amount` against `TaskRules.FocusKillAt` (0.5, half way into the
    slow), so the last instant of a fading Focus does not count.
  - Sea kill: only a swimmer (`CreatureTiers.Swims`: the Drifting maw and the Pack biter) killed within its reach,
    which already means from in the water with it (pass 3); out of reach pays nothing and counts nothing.
  - Salvage home: the crate's delivery, after its own score. Bonus mission: the contract's completion, after its pay.
- **Granting** (`UnlockBook.Grant`): a look is added to the Collection (a real line: number 0, never a test item, so
  clearing test items never takes it) and put to use with `Locker.AutoUse`, which only fills an empty place. A look
  whose place is taken waits in the Collection, and its words say so.
- **It shows from the next run**, because the looks are read once a world (pass 4). Every note about a look says
  "from your next run".
- **The unlock is the record.** If an earned look's Collection line is ever missing, opening the Collection puts it
  back without putting it to use (`UnlockBook.KeepEarnedLooks`), so mending a save never chooses for the player.

## Kept out of the envelopes

- **The odds stay true** (rule 9). `PrizeRoll.InEnvelopes` leaves earned looks out of the item roll, the Celestial
  roll and the "is this rarity stocked" check, and the roll still draws the same numbers. A rule test rolls 3,000
  envelopes against three collections with the full catalogue and with the catalogue as it was before this pass: every
  prize is the same, and none is an earned look.
- **Earned, not a rarity.** An earned look is stored as Common (for sorting only) but is shown as "Earned" in a green
  of its own (`ItemLook.EarnedName`, `EarnedColour`, no rarity's colour): "Earned weapon skin (any gun)". So the
  TF2-style tiers of section 12 keep meaning how rare a prize is in an envelope. The Collection's details add "Earned
  for a disarm".
- **An empty Collection** now names the next look and how ("Nothing yet. Looks are earned by goals in a run. The
  next: Butterfingers, for a disarm"), then the envelope.

## On screen

- **The moment** is the goal unlocks' own (pass 1): the green stencil banner, "UNLOCKED: BUTTERFINGERS", the note
  "DISARMED · A GUN SKIN, ON EVERY GUN FROM YOUR NEXT RUN" (or "..., IN YOUR COLLECTION" when its place is taken),
  and `reward.unlock`. At a break: "UNLOCKED: Butterfingers, for a disarm. A gun skin, on every gun from your next
  run."
- **The Unlocks page** has two lists, perks and kits (ten) and looks (eight), because eighteen with their goals do not
  fit one 16:9 page. A button beside Back switches them and names the other list's new ones ("Looks (2 new)"). The
  page opens on a list with something new, the looks first, then on the list holding the next unlock. Each list's NEW
  marks go once that list has been shown. The title screen's Unlocks button counts both.
- **How to play** says a goal "opens a new perk, starting kit or look".
- **The goal words carry the player's keys**: the Focus key (`UnlockHints.focusKey`) and the Interact key.

## Tests

- `dotnet test tools/rules-tests`: 3,161 run, 3,160 passed, 1 skipped (the cabin bake check, as before). New:
  `EarnedLookTests` (7) and twelve in `UnlockTests` (the first three goals and their places, every look's place and
  its goal, each new goal met only by its feat, a rover jump, granting into an empty and a taken place, the words,
  the keys, mending, the two lists' NEW marks, the next look, and the home world named by the id the game uses). Four older tests follow the new order, and the three
  worlds test now uses the game's real body ids, which it had written in lower case.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.

## Still unverified

Nothing here has run in Unity. Not yet checked there: the arm-sever disarm reaching the director in the same frame
the gun is dropped, the eight looks on the real guns, suit plate and lander, the Unlocks page's two buttons with a
controller, and how soon a new player really meets the first three goals (a playtest question).
