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
| Smallest test | 38 rule tests (`LockerTests` 17, `PrizeLookTests` 21): the Locker's places and rules, the save's move to layout 6, the Collection's order and words, every upgrade's gain and cost on two different numbers (never gun or melee damage), older item assets following retuned upgrades, the ship paints, the skin patterns held to the shader, and the suit's looks held to the plate's contrast. |
| Acceptance (offline, met) | Every prize but a painting has a place. A won cosmetic goes to work by itself only where its place is empty; an upgrade never does. Every upgrade moves exactly the two numbers it names, and none costs gun or melee damage. The unpainted lander is exactly as before. Every suit piece keeps the plate's words at 7:1 contrast or better. |
| Acceptance (Deck, not yet run) | Below. |
| Playtest protocol | At the end of this report. |

## The Locker

`Items/Locker.cs` (plain C#) keeps one item in use in each place:

| Place | What it changes | When it is read |
|---|---|---|
| Upgrade | Two of the run's numbers, the gain and the cost (below) | At the start of a run |
| Ship paint | The lander's outside hull | When the lander is built (a run's start) |
| Gun skin | The player's guns. One skin for every gun, and one of a gun's own per gun (a gun's own skin wins on that gun) | Once a world (a run, or the practice range), the first time a gun comes into the player's hands |
| Helmet | The shade at the edge of the view outside the ship | When the suit's display is built (a run's start) |
| Visor | A gentle tint over the world outside the ship | The same |
| Suit | The suit plate's face and edge on the screen | The same |
| Jetpack | The jets' flames, the visor rim's glow and the jet blocks on the plate | The same |
| Badge | A patch sewn on the suit plate | The same |

- **What goes to work by itself.** A won cosmetic, skin or paint goes into use by itself only when its place is empty (`Locker.AutoUse`). An upgrade never does, because each one has a cost and is the player's choice.
- **Saved** in `SaveData.locker` (layout 6; a layout 5 save gains an empty Locker and keeps every prize). The Collection page tidies the Locker when it opens: anything no longer owned, no longer in the catalogue or no longer fitting its place is taken out of use (`Locker.Tidy`).
- **Matthew: before switching branches.** Once this build has saved, a build from before this pass finds a newer save and refuses to write over it (`SaveSystem.CheckForNewerSave`, as for every layout change). Records, unlocks and envelopes from sessions on the older build are then not kept. Stay on `claude/playtest` once this is merged, or copy the save first.
- **The word "Locker" is never on screen.** Part B's locker is the one at the launch base, for permanent loot (section 8), and that does not exist yet. The pages say Collection and "in use"; the code keeps the name.
- **Paintings** have no place yet: they wait for the frames on the cabin wall (Part B section 12), which need the pictures themselves (public-domain paintings Matthew downloads).
- **Celestial effects** are kept with the item they came on, but are not drawn yet.

## Upgrades are sidegrades

An upgrade multiplies two of the run's numbers (`Items/PrizeLooks.cs`, `UpgradeEffects`), on top of the perks, the shop
and the hazard (`RunMods.From`). The effect is clamped between 0.5 and 2 times, whatever an asset says.

| Upgrade | Gain | Cost |
|---|---|---|
| Big Lungs | +25% suit air (and the jets burn air for longer once their own tank is dry) | -4% walking speed |
| Light Frame | +12% walking speed | -10% max armour |
| Hot Loads | +10% gun damage | -12% reload speed (every reload takes 1/0.88 as long) |
| Spare Jet Tank | +20% jet fuel | -10% ship fuel |
| Deep Focus | +20% Focus from kills and limbs | -5% walking speed |
| Heavy Hands | +45% melee damage | -15% Focus from kills and limbs |

- **Why these numbers** (from the design review): the first set had three weak picks (Big Lungs' +12% air was about 34 s on a 280 s tank; Light Frame's +8% speed against -10% armour; Heavy Hands' +25% melee for -8% gun damage, which matters every shot, while melee is paid only when a found weapon is held). No upgrade costs gun or melee damage now: gun damage sits just above the hits-to-break lines, so a few per cent less adds a hit to many limbs and heads, and melee damage is a cost only for a player who holds a melee weapon. A rule test holds this.
- **The words are the perks' words:** walking speed, max armour, Focus from kills and limbs (`ItemLook.StatName`, held to the perks' own lines by a test).
- **Older item assets follow:** the item assets are made once on Matthew's machine, so the first numbers would have stayed there. `ItemCatalog.Version` (now 2) and `ItemData.catalogVersion` work like the colour grade's version: an older asset takes the catalogue's gain, cost and blurb when it loads and keeps its name and colours; 9999 keeps an asset's own numbers.
- **The cost is written wherever the upgrade is named:** on the envelope's reveal (the gain in green, the cost in orange), on the Collection page ("+25% suit air and -4% walking speed in every run while it is in use. One upgrade at a time."), as a note when the run starts ("UPGRADE: BIG LUNGS (+25% SUIT AIR, -4% WALKING SPEED)", after a kit's note if there is one), and at the ante break and on the field UPGRADES page under the run's sets ("Upgrade in use: Big Lungs (+25% suit air, -4% walking speed). Change it in Collection.").
- **New numbers the run can change:** jet fuel (`SuitSupplies.FuelScale`), ship fuel (`ShipSupplies.FuelScale`) and reload time (`FirearmState.StartReload`'s scale, clamped to 0.5 to 2), all through `RunEffects`.
- **Only in a solar-system run.** `RunEffects` is neutral outside one, so the practice range and the tests are untouched.
- **An old bug fixed on the way:** the suit plate's air time ignored a bigger tank bought in the shop. It now counts it.

## How prizes look

- **Ship paint** (`Ship/HullPaint.cs`, `ShipShell.Hull`): the paint on the light panels, a darker shade on the grey ones, the accent where the gold panels were. Racing Green adds a cream band round the foot of the hull; Two-Tone Cruiser paints the roof in teal and the walls in cream. The dark panels, the skirt into the base and the bands round the windows stay dark, so the windows and the shape still read. The Blender-made fittings (base, legs, engines, thrusters, dish) and the cabin inside (whose light is baked) are not painted.
- **Gun skins** (`OA_Model.shader`, `OA_Skin`; `PlanetMaterials.SkinnedModel`; `ModelLibrary.Wear`): seven patterns, each in the item's own two colours. The pictures' own wear, grime and edges show through the paint. The skin is drawn behind a branch on a material value (`_SkinSet.y`), not a shader keyword, so it adds no shader programs and nothing to compile the first time a skinned gun is drawn. A pattern finer than a few pixels fades out by how far the gun moves across one pixel (`OA_SkinFine`), so the flake, the stars and the fine wood grain never crawl, at range or on a face seen edge-on.
  - Paint: one colour on every painted part.
  - Stripes (Hazard Stripes): diagonal bands 2.5 cm wide, evened out to their average from 4 to 10 m away so they never shimmer.
  - Grain (Station Wagon Wood): grain lines along the gun, with a finer grain close up (gone by 3 m).
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

- **The reveal** says where the prize went to work ("In use on your lander.", in the target-met green), what is already in its place ("Your Fishbowl Dome is in its place: swap this in from Collection on the title screen."), or how to put an upgrade to use ("Every upgrade has a cost, so it waits for you: put it to use from Collection on the title screen."). Mid-run (a test envelope) it says "from your next run", because the lander, the suit, the skins and the numbers were read at the run's start. The line sits under "Added to your collection." and wraps to two lines at most. A Celestial prize's effect says "(not shown yet)", because the effects are not drawn. See it offline with `tools/envelope_view` (`show <folder> reveal Exotic 30 10.5 UpgradeHeavyHands`), which now draws it as the game does.

- **The Collection page** (`UI/CollectionPage.cs`, order and words from `Items/CollectionRows.cs`):
  - A summary line: how many items, how many in use, how many envelopes to open.
  - Six rows a page, grouped by place in the Locker's order, rarest first, newest first. A coloured stripe gives each row's rarity and IN USE is written in green on the right.
  - Pressing a row puts it to use or takes it off, and the details under the rows say what it does and what pressing it will do ("Press to use it instead of Sunset Visor.", "In use. Press to take it off.").
  - A painting says KEPT in grey, and its details say it is kept for the cabin wall, with no frame to hang it in yet.
  - Previous and next page buttons when there are more than six. B, Esc or Backspace goes back to the title; after a stray click on empty space, the D-pad or stick chooses a button again.

## The specialist reviews and what was done

Each review ran on the first build of this pass; each finding was checked before it was applied.

- **game-designer** (unlockables 6 to 7 on its reading):
  - Three upgrades were weak or costly in the wrong place (above): retuned, with a rule test that no upgrade costs gun or melee damage, and older assets now follow the catalogue.
  - The stat words differed from the perks' ("running speed", "armour", "Focus"): now the perks' words.
  - "Locker" on screen promised Part B's base locker: kept off screen.
  - An upgrade's cost was seen only at the first break: it is now a note as the run starts.
  - "Choose" read as a menu word: now "Press". "Your X is in its place" on the reveal when a place was taken; KEPT for paintings; a Celestial effect marked "not shown yet".
  - Its biggest remaining gap is reach: prizes open only after ante 9, so a new player meets none for many runs. Goal unlocks that give a cosmetic would put the Collection within reach (next pass).
- **deck-performance:**
  - The skin keyword would have doubled the model shader's forward programs and stalled the first time a skinned gun was drawn: now a branch on a material value, no keyword.
  - Skins were looked up in the save on every gun put in the hand: now read once a world into a small table.
  - The visor tint found URP's colour filter by reflection every frame: the setter is now bound once.
  - Not changed: the Collection page saves on every press (title screen only; a menu frame can afford it, and a save that waits could be lost).
  - Budget rows written.
- **unity-reviewer:**
  - B and Esc did nothing on the Collection page, and a click on empty space left the D-pad with nothing to move: B now goes back, and the D-pad chooses again (not A, which would press the button it chose and open an envelope).
  - The flake and star patterns were hard-edged cells of 2.5 and 4.5 mm, faded only by distance, so they would likely crawl on the held gun: now faded by the pixel's footprint too, and the fine wood grain with them.
  - The save's layout 6 means older builds refuse to write the save (above).
  - Checked and fine: no save, missing item assets, the start-up order, the skin materials' life and the cabin's twins, the colour filter's override flag, the painted hull, the reveal's closing press, and the practice range staying neutral.

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
- **`PrizeLookTests` (21 tests):**
  - every upgrade is a sidegrade with its gain and cost on two different numbers, and moves only those two, each by its own amount; none costs gun or melee damage; its stats are named in the perks' words;
  - an older item asset takes the retuned gain, cost and blurb and keeps everything else; 9999, an up-to-date asset, a paint and an unknown upgrade are left alone;
  - Hot Loads makes every reload longer by its cost; the run's build takes the Locker upgrade on top of its perks;
  - no asset can push an upgrade past its limits;
  - a bigger jet tank lasts longer and a smaller ship tank shorter; a bigger air tank also feeds jets that burn air for longer; a reload scale changes only how long the reload takes;
  - the unpainted hull is the lander's own white, grey and gold; every paint repaints it and leaves the dark panels dark; the paints' styles are the ones their words promise;
  - every skin pattern is one the model shader draws, by the same number, behind the branch on `_SkinSet.y` with no keyword, and declared in the one material buffer every variant shares; every skin has a look;
  - with nothing worn the suit looks as it always did (held to the HUD's own numbers); every suit keeps the plate's words easy to read (7:1, and 4.5:1 for the dim labels); no jetpack gives a flame that reads as damage; a visor tint is gentle and a helmet's edge stays dark;
  - the reveal draws its use line between the "owned" line and the prompt, green only for a prize that went to work by itself, and a Celestial effect says it is not shown yet.
- **Changed:** `UnlockTests` no longer pins the save layout to 5; two older tests take the perks' new words ("max armour").
- **Offline shader check:** every OA_Model program (30, the same count as before the skins) passes glslang against stub URP includes. That is a syntax check, not Unity's compiler.
- **Whole suite:** 3,142 rule tests, 3,141 passing and 1 skipped (the cabin bake check, as before). The first full run after the reviews' fixes had one failure, a test still holding Heavy Hands' first numbers; it was fixed and re-run with every test that reads the changed files. Runtime compile check clean apart from the two known `excludeLayers` lines.

## Still unverified

- Nothing here has run in Unity.
- How each skin looks on the real gun models in sunlight, shade and caves, and whether any pattern shimmers on the Deck's screen.
- Whether the plate colours, the badge and the visor tint read as the suit's own and not as a warning.
- Whether the hull paints look painted, not flat, under the Sun and the floodlights.
- Known limits:
  - a prize revealed mid-run, or a change made in the Collection, shows from the next run;
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
2. On the Collection page, put an upgrade to use. Ask what it costs. Start a run and watch for the UPGRADE note, then find the "Upgrade in use" line on the UPGRADES page.
3. Launch, land on the Moon, step out, and jet about in each suit look.
4. Fire and reload each skinned gun in sunlight and in a cave.
5. Ask which prize they would want next, and why.
