# Player screen: "Hull stencil" (26 Nov 2026)

Matthew asked: "create an interesting player screen that the player sees when playing that shows the appropriate information for the game in interesting and distinct aesthetic with interesting and easy to read font."

## What was shown first

Three offline mock-ups at the Deck's 1280 x 800, over an offline terrain picture (not game footage), each on Mars and on the Moon. They are in the project folder `orbital-ante/hud/player-hud/`:

1. **House table** (Bungee + Barlow Semi Condensed): a casino table. Chip clock, suit plates as a hand of cards, tasks as tickets.
2. **Hull stencil** (Saira Stencil One + Overpass Mono): painted, bolted spacecraft hardware. Flip clock, stencil numbers, hazard stripes. Recommended.
3. **Visor glass** (Oxanium): no panels; air, ammo and suit as arcs round the crosshair.

Matthew chose **2, Hull stencil** on the card. This report is what was built from it.

## What is on screen now (on foot)

Four bolted plates, one in each corner, and one strip:

- **Top left, the ante.** "ANTE 03", the clock on three flip tiles, the points banked this ante as a big stencil number over a bar of 20 blocks, "NEED 20,000" under it, and the streak ("x4") on the right. The pale tick across the bar is where a steady pace would be by now. The digits turn orange in the last 30 seconds and in overtime (the name then reads "ANTE 03 OVERTIME" and the tiles count up). The number and the bar turn green once the target is met. Under the plate: the pace words when behind, or "PERK READY" with its key, then the "+points" lines.
- **Top right, the task.** What is left to do on the world you are at ("MARS: 1 MAST · 2 CRATES"). Hidden aboard, where the ship's damage list has that corner.
- **Lower left, the suit.** Air as a tank that drains, beside its per cent and the time left; "SUIT PLATES" as ten chevrons that go dark one at a time (the last one still on is orange while it is being worn through) with "06 OF 10" under them; jet fuel as a row of ten blocks. Speed, tether and stance are lines just above the plate.
- **Lower right, the gun.** The gun's name, the rounds in the magazine as a big number ("05 /08") and as blocks (one a round up to ten rounds, ten blocks for a bigger magazine), "NEXT SNIPER" over one pip per kill it takes, and the Focus meter. The number and blocks turn orange at a quarter of a magazine or less and while reloading (the name then reads RELOADING). The guns you carry and the switch key are a line above the plate.
- **Low in the middle, the warning strip.** The suit's one notice. An alarm (air low, no air, jet fuel out) is orange between hazard stripes; anything else is plain cream on a plain strip.
- **The aiming mark** is four corner brackets round a small orange square, with a dark edge so it reads over sand, sky and space.

Prompts under the crosshair, tips, the melee line and the rare-gun warning are in the mono face; banners ("NEW GUN", ship notices, the ante's notes, TAKEDOWN) are in the stencil face.

## What moved or went

- The ante readout moved from the top right to the top left. The top right was carrying the ante, the task line, the "+points" lines and (near the ship) the damage list; now it has the task plate only.
- The two visor arcs (air lower left, suit lower right) are replaced by the suit plate. Their numbers and rules (`VisorReadout`) are unchanged: when a plate goes dark, when air is an alarm, what the lines say.
- The Focus meter and the gun ladder's line moved from the low middle into the gun plate. The Focus meter now always shows while the plate does (it used to hide when full).
- **New on screen:** the gun's name and rounds in the solar game. Until now they showed only in the practice range.
- In the practice range, the old ammo text is gone (the gun plate replaces it), and health is the suit plate's chevrons on a small plate lower left (27 Nov).

## Not changed

- The seat view and the ship's flight readout. Matthew said on 17 Nov that he did not want the ship HUD taken away, and its redesign has its own open card (PR 72).
- The hit marker, the hurt arcs and the edge glow.
- What anything means: no rule, number or threshold changed.

## Files

- New: `Scripts/Runtime/UI/HullKit.cs` (the look: plates, blocks, flip tiles, pictures made in code, the fonts), `Scripts/Runtime/UI/HullReadout.cs` (plain C#: blocks lit, clock tiles, magazine blocks, pips), `Tests/EditMode/HullReadoutTests.cs`, `Resources/Fonts/SairaStencilOne-Regular.ttf`, `OverpassMono-SemiBold.ttf`, `OverpassMono-Bold.ttf` and their licence texts.
- Changed: `Tasks/AnteLoop.cs` (ante plate), `Tasks/RunGuideFx.cs` (task plate), `Spacewalk/SuitHud.cs` (suit plate, warning strip), `Combat/GunLadderFx.cs` (gun plate), `Combat/DeepBreathFx.cs` (Focus bar in the plate), `Ship/ShipHud.cs` (aiming mark, prompt), `Melee/MeleeHud.cs`, `UI/Hud.cs` (practice range).

## Fonts

- **Saira Stencil One** (Omnibus-Type), SIL Open Font Licence 1.1, file unchanged from Google Fonts.
- **Overpass Mono** (Red Hat / Delve Fonts), SIL Open Font Licence 1.1. Google Fonts ships it as one variable file; Unity's text cannot pick a weight from that, so the SemiBold and Bold weights were cut from it with fontTools. The licence has no reserved font name, so the cut files keep the name.

Both are logged in `docs/asset_licence_ledger.md`. B612 stays the lettering of the menus and the seat view.

## Constitution

- **7, manage cognitive load:** each corner answers one question (how is the ante going, what is there to do here, how am I, what am I holding), and the top right no longer stacks four things.
- **5, legible risk and reward:** one colour, orange, means "act on this" everywhere on the screen; green means the target is met.
- **3, learnable challenge:** the magazine and the kills to the next gun are now shown, not just known.

## Tested

- All game code compiles against Unity reference assemblies in the cloud (the only errors are the two known `excludeLayers` lines that the older reference lacks).
- `dotnet test tools/rules-tests`: 2,710 passed, 0 failed, 1 skipped (the cabin bake check, which skips in the cloud as before). 12 new rule tests in `HullReadoutTests`.

## Agent review

Not done. The unity-reviewer and deck-performance agents were started on this change and both stopped on the account's usage limit before reporting. The points they were asked about were checked by hand instead: every colour set on a renderer is set again each frame the plate shows, and nothing per frame builds a string.

## Not verified

- **Nothing here has been run in Unity.** Placement, sizes and colours come from the mock-up's measurements; they have not been seen on the Deck. The likeliest things to need a nudge: how the stencil digits sit in the flip tiles, the tank's rounded ends, the hazard stripes' scale, and text that runs long (a long world name on the task plate shrinks to fit rather than being cut).
- Whether the new fonts import cleanly. If a plate's text shows in the old lettering, the font file did not import (`HullKit` falls back to B612).
- Frame cost on the Deck.

## Open

- PR 89 (bonus missions) and PR 91 (the "seen" warning arc and clock cues) are not in the playtest branch yet. Both touch the same files; whichever lands second needs a small merge. The task plate has room for a second "BONUS" row when PR 89 lands.
- Done 27 Nov: the practice range's health is now the suit plate's ten chevrons on a small plate lower left, above the help line (`UI/Hud.cs`), so no arc is left on the on-foot screen.
