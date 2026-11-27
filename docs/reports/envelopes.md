# Prize envelopes and items (16 Nov 2026)

Matthew asked for "an item system like tf2": groundwork now, items later (Fable 5.2). Every run shows an envelope at the start; it cannot be opened until Ante 9 is cleared; opening it reveals a prize of some rarity: cosmetics, weapon skins, in-game upgrades, ship paint or artwork. The envelope should be "super aesthetically pleasing".

**Nothing here has been run in Unity.** The rules are tested; the look was checked in offline pictures drawn from the same code (see "What was tested").

Preview pictures and videos: `/mnt/project-files/orbital-ante/envelope/` (run start picture and video, a reveal for Rare, Legendary and Celestial, and a sheet of the reveal's steps).

## What changed

**Items (plain C#, `Scripts/Runtime/Items/ItemRules.cs`, `ItemCatalog.cs`)**
- Six rarities (placeholder names): Common, Uncommon, Rare, Exotic, Legendary, and **Celestial**, the top tier like an Unusual: an ordinary cosmetic, weapon skin or ship paint that also carries a special effect (six placeholder effects: Solar Flare, Aurora, Comet Tail, Nebula Drift, Starfall, Event Horizon).
- Five categories: Cosmetic (with a slot: helmet, visor, suit, jetpack, badge), Weapon skin (one gun or any gun), Upgrade, Ship paint, Artwork.
- 31 placeholder items. Artworks are public-domain paintings only (Constable, Van Gogh, Hokusai, Friedrich). Every upgrade is a **sidegrade** with a downside (Part B section 4 says unlocks are never straight upgrades), for example "Big Lungs: +12% suit air, -5% running speed".
- Each item is also an `ItemData` asset in `Resources/Items`, made by the setup tool; the file name is the id the save keeps.

**The roll (`PrizeRoll`)**
- First the rarity, by public odds printed on the envelope (constitution rule 9): Common 40%, Uncommon 30%, Rare 18%, Exotic 8%, Legendary 3.5%, Celestial 0.5%. First guesses, generous because clearing all nine antes is rare. They are dials on `EnvelopeConfig`.
- Then one item of that rarity, every item equally likely, items the player does not own yet first. When everything of that rarity is owned, a duplicate comes and the count goes up. A rarity with no items gives way to the nearest one below.
- The prize comes from the save's own number plus the envelope's number, never the run seed: replaying a seed does not replay its prize, and reloading cannot re-roll one.

**Envelopes and the collection (`EnvelopeRules.cs`, save version 3)**
- A new envelope is numbered when each run starts (the "No. 0042" on it). Clearing Ante 9 unlocks it and keeps it in the save at once; ending the run earlier loses it.
- Opening puts the prize in the collection and saves **before** the reveal plays, so quitting halfway keeps the prize. A won envelope can also wait and be opened later from the title screen.
- The collection persists across runs. The save moves from version 2 to 3 by itself; nothing in an old save is lost.

**The screens**
- Run start: the sealed envelope rises in, a sheen passes over the foil, glints twinkle, and under it "Opens when you clear Ante 9. A run that ends sooner leaves it sealed." (gain framed, rule kept whole, since 21 Nov 2026) with the odds in each rarity's colour. Any of A, Enter, Space, E or a click closes it. The run's controls are off while it shows.
- Reveal: the nine stars on the seal light one by one, the envelope rattles (longer and harder for rarer prizes), the seal cracks with light in the prize's rarity colour, splits and falls away, the flap folds open, the card slides out, the envelope drops away, light rays and sparks burst, and the result is written beside the card. About 7 to 9 seconds; a press skips to the result.
- Title screen: a new **Collection** button lists what you own (rarest first), opens waiting envelopes, and in the editor and development builds offers test envelopes.

**The look** is painted in code (`EnvelopeArt`): midnight-blue paper with a woven grain, a double gold-foil border with art-deco corners, a gold orbit emblem and constellations, a star-chart lining, and an oxblood wax seal stamped with a ringed planet and nine stars (one per ante), the design picked out in gold. The prize card has a metal frame in the rarity's colour, a holographic sheen from Rare up, and a stand-in icon per category until Fable makes the real item art.

## Why
Part B section 12 (TF2-style rarity tiers, an Unusual-like top tier, paintings), section 5 (antes), and Matthew's message. Constitution: 5 legible risk and reward (the rule and the odds are on the envelope), 9 ethical design (public odds, no streaks, no paid anything, the prize is never lost to a crash), 6 meaningful replayability.

## How to try it (numbered steps)
1. On the Deck: `cd ~/Orbital-ante`, `git fetch`, `git checkout claude/playtest`, `git pull`.
2. Open the project in Unity Hub and wait for it to finish importing. The setup tool runs by itself and makes the new item assets and the EnvelopeConfig. If it does not, use the menu **Orbital Ante > Set Up Project**.
3. Press **Play**. On the title screen choose **Fly the solar system** (or the arena). The sealed envelope shows first. Press A (or Enter, or click) to start.
4. The ante loop is not in the game yet, so to see a reveal during a run press **F8** (keyboard). It pretends Ante 9 was cleared; the prize is marked as a test item.
5. Or on the title screen choose **Collection**, then **Win a test envelope (testing only)**, then **Open the envelope**.
6. **Clear test envelopes and items** on the same page removes everything won with tests.
7. To change the odds or turn off the run-start envelope: in the Project window open `Assets/OrbitalAnte/Resources/Config/EnvelopeConfig` and edit it in the Inspector.
8. To rename an item or change its rarity: open its asset in `Assets/OrbitalAnte/Resources/Items/` and edit **Spec** in the Inspector. Do not rename the asset file itself: the save uses the file name.

If something goes wrong, the first red line in the Console is what I need.

## What was tested
- 38 new rule tests (`EnvelopeRulesTests`, `EnvelopeLookTests`) and all 2,537 rule tests pass with `dotnet test tools/rules-tests`. They cover the odds (400,000 rolls match the printed percentages), the unowned-first rule, duplicates, Celestial always carrying an effect, the Ante 9 lock (and that failing Ante 9 does not open it), losing the envelope, save migration from versions 1 and 2, the flap covering the whole opening, the seal halves making the whole seal, and every frame of every rarity's reveal being well formed.
- The runtime code compiles against Unity reference assemblies (2021.3 modules; only the usual unrelated name clash).
- The pictures and videos were drawn offline by `tools/envelope_view` from the same `EnvelopeArt` and `EnvelopeScene` code. They are not Unity screenshots: Unity blends in linear colour and draws text itself, so colours and spacing can differ a little.

## Agent review
A unity-reviewer pass (reading only) found seven things; five were fixed before this went up:
- The 1280 x 800 stage now scales to fit any screen shape (on 21:9 the prompt fell off the bottom).
- Whatever follows a closed envelope starts one frame later, so the press that closes a reveal cannot also press "Open" on the collection page.
- The sheen is clipped with a stencil mask, which turns with the envelope and the card (RectMask2D only clips straight boxes).
- The world now waits while the envelope is on screen during a run (`TimeFlow.Held`, which leaves sound playing); before, enemies could act while your controls were off.
- The flash now draws in the list's order, and a per-frame dictionary is reused.
Left as is: a few small allocations per frame while the envelope shows, and textures painted at 1:1 look a little soft when scaled up on a 1080p monitor (fine on the Deck).

## Not verified / not done
- **Never run in Unity.** Look, timing on the Deck, input handling and the title-screen hitch while the pieces are painted (about 0.55 s in all under .NET, spread one piece a frame) are unverified.
- The ante loop (Milestone 14) is not built, so nothing real unlocks the envelope yet; F8 and test envelopes stand in. Milestone 14 calls `EnvelopeDirector.AnteCleared(ante)`.
- Items are stored but not used: no cosmetic is worn, no skin or paint is applied, upgrades change no numbers, paintings do not hang on the cabin wall. Each needs its own system and, for most, Fable's models.
- No envelope sounds exist yet; the reveal borrows the existing reward and menu sounds (`envelope.*` events are named in the code for the audio thread).
- Part B section 12 says permanent loot must be carried back to the locker at the starting base to keep it. Envelope prizes go straight into the collection here, because the envelope opens at the end of a run. Say if they should go through the locker instead.

## Choices for Matthew
- Rarity names, item names, effects and the odds (all placeholders).
- Whether an upgrade is fitted one at a time from the collection, and whether it lasts all run.
- Whether duplicates should be traded at the casino later (Part B section 12 mentions trading items).
