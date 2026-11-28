# The Locker: prizes that do something (Ralph loop pass 4, 28 Nov 2026)

Matthew asked for a fun unlockables system. Clearing Ante 9 opens a prize envelope (Matthew, 16 Nov 2026: "an item
system like tf2"), but until this pass a prize was only listed in the collection. The upgrades, paints, skins and
suit pieces inside changed nothing in the game, so the envelope's promise was empty.

**Nothing here has been run in Unity.** The pictures below come from the offline envelope tool, not from Unity.

## What changed

| | |
|---|---|
| Problem | A prize did nothing. Part B section 5 asks for unlocks that change later runs, and section 4 says they must be sidegrades. |
| Hypothesis | If every prize but a painting goes to work in one place (the Locker), shows where it went the moment it is revealed, and can be put to use or taken off in one press on the Collection page, then a prize is worth wanting. If every upgrade writes its cost beside its gain wherever it shows, choosing one is a real choice and never a must-take. |
| Principle | Agency and meaningful trade-offs (2): the player chooses what is in use, and each upgrade costs something. Legible risk and reward (5): the cost is written wherever the upgrade is named. Discovery that matters (4) and replayability (6): a prize changes later runs. Ethics (9): no meters, the odds stay public, nothing is lost by waiting. |
| Smallest test | 34 rule tests (`LockerTests` 17, `PrizeLookTests` 17): the Locker's places and rules, the save's move to layout 6, the Collection's order and words, every upgrade's gain and cost on two different numbers, the ship paints, the skin patterns held to the shader, and the suit's looks held to the plate's contrast. |
| Acceptance (offline, met) | Every prize but a painting has a place. A won cosmetic goes to work by itself only where its place is empty; an upgrade never does. Every upgrade moves exactly the two numbers it names. The unpainted lander is exactly as before. Every suit piece keeps the plate's words at 7:1 contrast or better. |
| Acceptance (Deck, not yet run) | Below. |
| Playtest protocol | At the end of this report. |

## The Locker

`Items/Locker.cs` (plain C#) keeps one item in use in each place:

| Place | What it changes | When it is read |
|---|---|---|
| Upgrade | Two of the run's numbers, the gain and the cost (below) | At the start of a run |
| Ship paint | The lander's outside hull | When the lander is built (a run's start) |
| Gun skin | The player's guns. One skin for every gun, and one of a gun's own per gun (a gun's own skin wins on that gun) | Whenever a gun comes into the player's hands, in the practice range too |
| Helmet | The shade at the edge of the view outside the ship | When the suit's display is built (a run's start) |
| Visor | A gentle tint over the world outside the ship | The same |
| Suit | The suit plate's face and edge on the screen | The same |
| Jetpack | The jets' flames, the visor rim's glow and the jet blocks on the plate | The same |
| Badge | A patch sewn on the suit plate | The same |

- **What goes to work by itself.** A won cosmetic, skin or paint goes into use by itself only when its place is empty (`Locker.AutoUse`). An upgrade never does, because each one has a cost and is the player's choice.
- **Saved** in `SaveData.locker` (layout 6; a layout 5 save gains an empty Locker and keeps every prize). The Collection page tidies the Locker when it opens: anything no longer owned, no longer in the catalogue or no longer fitting its place is taken out of use (`Locker.Tidy`).
- **Paintings** have no place yet: they wait for the frames on the cabin wall (Part B section 12), which need the pictures themselves (public-domain paintings Matthew downloads).
- **Celestial effects** are kept with the item they came on, but are not drawn yet.

## Upgrades are sidegrades

An upgrade multiplies two of the run's numbers (`Items/PrizeLooks.cs`, `UpgradeEffects`), on top of the perks, the shop
and the hazard (`RunMods.From`). The effect is clamped between 0.5 and 2 times, whatever an asset says.

| Upgrade | Gain | Cost |
|---|---|---|
| Big Lungs | +12% suit air | -5% running speed |
| Light Frame | +8% running speed | -10% armour |
| Hot Loads | +10% gun damage | -12% reload speed (every reload takes 1/0.88 as long) |
| Spare Jet Tank | +20% jet fuel | -10% ship fuel |
| Deep Focus | +20% Focus | -15% melee damage |
| Heavy Hands | +25% melee damage | -8% gun damage |

- **The cost is written wherever the upgrade is named:** on the envelope's reveal (the gain in green, the cost in orange), on the Collection page ("+12% suit air and -5% running speed in every run while it is in use. One upgrade at a time."), and at the ante break and on the field UPGRADES page under the run's sets ("Locker upgrade: Big Lungs (+12% suit air, -5% running speed)").
- **New numbers the run can change:** jet fuel (`SuitSupplies.FuelScale`), ship fuel (`ShipSupplies.FuelScale`) and reload time (`FirearmState.StartReload`'s scale, clamped to 0.5 to 2), all through `RunEffects`.
- **Only in a solar-system run.** `RunEffects` is neutral outside one, so the practice range and the tests are untouched.
- **An old bug fixed on the way:** the suit plate's air time ignored a bigger tank bought in the shop. It now counts it.

## How prizes look

- **Ship paint** (`Ship/HullPaint.cs`, `ShipShell.Hull`): the paint on the light panels, a darker shade on the grey ones, the accent where the gold panels were. Racing Green adds a cream band round the foot of the hull; Two-Tone Cruiser paints the roof in teal and the walls in cream. The dark panels, the skirt into the base and the bands round the windows stay dark, so the windows and the shape still read. The Blender-made fittings (base, legs, engines, thrusters, dish) and the cabin inside (whose light is baked) are not painted.
- **Gun skins** (`OA_Model.shader`, keyword `_OA_SKIN`; `PlanetMaterials.SkinnedModel`; `ModelLibrary.Wear`): seven patterns, each in the item's own two colours. The pictures' own wear, grime and edges show through the paint.
  - Paint: one colour on every painted part.
  - Stripes (Hazard Stripes): diagonal bands 2.5 cm wide, evened out to their average from 4 to 10 m away so they never shimmer.
  - Grain (Station Wagon Wood): grain lines along the gun, with a finer grain close up.
  - Pearl (Mother of Pearl): pale, with a colour that moves as the gun turns.
  - Flake (Candy Apple): deep colour with fine flake, and the bare metal under it tinted.
  - Stars (Star Chart): night blue with gold points.
  - Gilt (Gilded Long Shot): gold leaf over every part, bare metal included.
  - Only the player's guns wear skins. Enemies' guns never do.
- **Suit pieces** (`PrizeLooks.Suit`, `SuitHud`, `SuitFx`, `PostFx.VisorTint`):
  - A helmet shades the edge of the view in its own colour, darkened. The Fishbowl Dome is clear and shades it less.
  - A visor tints the world outside by at most 12%, faded in over 0.35 s, so the world's colours still mean what they mean. The screen's plates and arcs are never tinted (they are drawn after the picture's effects).
  - A suit paints the plate's face (kept dark) and edge.
  - A jetpack colours the flames, the visor rim's glow and the jet blocks. The colour is always pale toward white-hot, never a deep red or orange that could read as damage.
  - A badge is sewn on the suit plate.

## The reveal and the Collection page

- **The reveal** says where the prize went to work ("In use on your lander."), or how to put it to use ("Every upgrade has a cost, so it waits for you: put it to use from Collection on the title screen."). Mid-run (a test envelope) it says "from your next run", because the lander, the suit and the numbers were built at the run's start. The line sits under "Added to your collection." and wraps to two lines at most; see it offline with `tools/envelope_view` (`show <folder> reveal Exotic 30 10.5 UpgradeHeavyHands`), which now draws it.

- **The Collection page** (`UI/CollectionPage.cs`, order and words from `Items/CollectionRows.cs`):
  - A summary line: how many items, how many in use, how many envelopes to open.
  - Six rows a page, grouped by place in the Locker's order, rarest first, newest first. A coloured stripe gives each row's rarity and IN USE is written in green on the right.
  - Choosing a row puts it to use or takes it off, and the details under the rows say what it does and what choosing it will do ("Choose it to put it to use, in place of Sunset Visor.").
  - Previous and next page buttons when there are more than six.

## The specialist reviews and what was done

To be written after the reviews.

## Tests

- **`LockerTests` (17 tests):**
  - every prize but a painting has a place, and only an owned prize with the effect it was won with can be used;
  - putting a prize to use takes off whatever was in its place, and nothing else; one upgrade at a time;
  - a gun's own skin and the skin for every gun are kept apart;
  - a won prize goes to work by itself only where its place is empty, and never as an upgrade;
  - choosing puts on and takes off; the tidy takes out of use what is no longer owned, known or fitting;
  - a layout 5 save is brought up to date with nothing in use and its prizes kept, and a loadout with holes is mended;
  - the reveal's, the Collection's and the upgrade's words;
  - the Collection's groups, order, rarity of a line with an effect, unknown items left out, pages of six, and details.
- **`PrizeLookTests` (17 tests):**
  - every upgrade is a sidegrade with its gain and cost on two different numbers, and moves only those two, each by its own amount;
  - Hot Loads makes every reload longer by its cost; the run's build takes the Locker upgrade on top of its perks;
  - no asset can push an upgrade past its limits;
  - a bigger jet tank lasts longer and a smaller ship tank shorter; a reload scale changes only how long the reload takes;
  - the unpainted hull is the lander's own white, grey and gold; every paint repaints it and leaves the dark panels dark; the paints' styles are the ones their words promise;
  - every skin pattern is one the model shader draws, by the same number, kept in a build, and declared in the one material buffer every variant shares; every skin has a look;
  - with nothing worn the suit looks as it always did (held to the HUD's own numbers); every suit keeps the plate's words easy to read (7:1, and 4.5:1 for the dim labels); no jetpack gives a flame that reads as damage; a visor tint is gentle and a helmet's edge stays dark;
  - the reveal draws its use line between the "owned" line and the prompt.
- **Changed:** `UnlockTests` no longer pins the save layout to 5.
- **Offline shader check:** every OA_Model variant (54) passes glslang against stub URP includes. That is a syntax check, not Unity's compiler.

## Still unverified

- Nothing here has run in Unity.
- How each skin looks on the real gun models in sunlight, shade and caves, and whether any pattern shimmers on the Deck's screen.
- Whether the plate colours, the badge and the visor tint read as the suit's own and not as a warning.
- Whether the hull paints look painted, not flat, under the Sun and the floodlights.
- Known limits:
  - a prize revealed mid-run shows from the next run;
  - Celestial effects are not drawn;
  - paintings have no frames yet;
  - prizes come only from envelopes, which open after Ante 9.

## Acceptance on the Deck

- Each kind of prize, put to use, is seen where the Collection says it goes:
  - the hull on the pad in the paint's colours, with the windows still dark-banded;
  - the held gun in its skin, from the first frame it is in the hand;
  - the plate, flames, rim and edge shade outside the ship.
- With Spare Jet Tank in use, the jets push about 96 s from full to empty instead of 80 s; with Hot Loads, the rifle's reload is about an eighth longer.
- Every suit plate's words are read at arm's length in handheld mode, in sunlight on the Moon and in a dark cave.
- No tester calls a jet flame or a visor tint a warning.
- No visible hitch the first time a skinned gun comes into the hand.
- Failure looks like: a tester cannot say what choosing a row does; picks an upgrade without seeing its cost; reads a tinted world as danger; sees a pattern crawl or shimmer.

## Playtest protocol

**Setup**
- 3 to 5 testers, handheld.
- One fresh profile, and one with test envelopes opened from the Collection page until it owns one of each kind.

**Steps.** After each step, ask the tester what they think will happen, then what did.
1. On the fresh profile, open a test envelope. Ask where the prize went.
2. On the Collection page, put an upgrade to use. Ask what it costs. Start a run and find the "Locker upgrade" line on the UPGRADES page.
3. Launch, land on the Moon, step out, and jet about in each suit look.
4. Fire and reload each skinned gun in sunlight and in a cave.
5. Ask which prize they would want next, and why.
