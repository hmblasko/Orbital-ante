# Enemy kinds: three more bodies, the way each moves, and their guns

Status: **built, not run in Unity.** Everything below was checked outside the engine: the rule tests pass, and the pictures were drawn in Blender from the game's real model files, posed by the game's real animation code. They are not Unity screenshots, and nobody has seen any of this move.

Pictures:
- `docs/art/previews/enemy_kinds.jpg`: the four kinds standing (top) and running (bottom), at the sizes the game draws them. Left to right: Bruiser, Marksman, Trooper, Runner.
- `docs/art/previews/enemy_kinds_outline.jpg`: the same as black shapes, to judge the outlines alone.
- `docs/art/previews/guns_enemy_kinds.jpg`: the Smg, the Shotgun and the Sniper.

## What Matthew asked for (14 Nov 2026)

"I want several different types of enemies, each with different attributes and weapons... numerous enemies everywhere... fighting near constantly."

This thread made the look: the bodies, how each one carries itself, and the guns. The core build thread owns what they do: attributes, behaviour, where they spawn and how many. The names are placeholders and belong to no faction.

## What changed

**Three more bodies** (`tools/blender/build_enemy_types.py`), each in the same 14 breakable pieces on the same joints as the trooper, so limbs come off, stumps show and everything else in the dismemberment system works unchanged.

- **Runner** (drawn at 0.85 size): thin limbs, a navy suit with yellow gloves and boots, a small round helmet that is mostly one big eye, a yellow crest swept back over it and a strap across the chest. Carries the Smg.
- **Bruiser** (1.3): a barrel chest with a dark round port in it, a bright slit to see through, red plates over a dark suit, a helmet sunk between heavy shoulder plates, thick forearms and tanks on its back. Carries the Shotgun.
- **Marksman** (1.2): tall and narrow in grey-blue, a white dome of a helmet with one telescope eye, a pleated blue cape that fans out wider than its arms, and a tall aerial with a pennant. Carries the Sniper.

**The way each one moves** (`BodyStyle` in `BodyAnimator.cs`). There are still no stored animations: feet are placed on the ground from the real speed and the legs bent to reach them. A kind now changes eleven numbers of that: how far it leans, how it holds its arms, how wide it stands, how long it strides, how much it bobs and sways. The Runner leans in low and pumps its arms; the Bruiser hunches, holds its arms out from its sides and rolls its weight from foot to foot; the Marksman stands narrow, upright and almost still. The trooper's numbers change nothing, and a test holds it to that.

**Quick legs for a quick body.** The Runner moves 1.5 times as fast as the trooper on legs 0.85 times as long. Left alone, its strides came out longer than its legs could reach. A body that is fast for its size now takes quicker steps and spends less of each step on the ground (`Pace`), so its feet still do not slide.

**Hitboxes that fit** (`BodyFit.cs`). Each kind has a row saying how much thicker or thinner each part is than the trooper's. A rule test measures every model against its own row and fails if a model outgrows its hitboxes or the hitboxes outgrow the model.

**Three guns** (`tools/blender/build_weapons.py`):
- **Smg** (the Uzi spoof): a stubby box with a wire stock folded along its sides and a magazine up through the grip that is far too long for it. 4,124 triangles.
- **Shotgun** (the double-barrel laser shotgun): two fat barrels side by side, wound with copper coils, wooden furniture, and a green power cell under the breech. A charge window each side and the bottom of each barrel glow green. 5,668 triangles.
- **Sniper** (the AWM spoof): a very long fluted barrel, a green stock with a hole for the thumb, a bipod folded under, and an enormous scope whose front glass is red. 6,140 triangles.

These three are from the gun list in section 9 of the build prompt. The enemies carry them now; whether and when the player's own roster gets them is the core build's and Matthew's call.

**Parts that shine.** A model piece whose name ends in `Glow` is now drawn brighter than white, so it blooms, and is handed back to the game by name. The Sniper's `LensGlow` is there so the Marksman's shot can be announced by a glint the player can see from the dangerous end.

## Why, and which principles it serves

- Section 10 asks for enemies "distinguished by size and silhouette like TF2 classes, unmistakable at a glance, never boxy". The outline picture is the check for that.
- Principle 3 (learnable challenge) and principle 5 (legible risk): each body says how it will fight before it does. The thin quick one sprays up close, the wide slow one hurts at arm's length, the tall still one hurts from far away and shows a red lens first.
- Principle 1 (fun core gameplay first): near-constant fighting only stays interesting if the next enemy is not the last one again.
- More enemy kinds and more guns are Phase 2 work in the build order (item 13). They are here now because Matthew asked for them on 14 Nov, and what he says in conversation wins over the Phase 1 rule. Nothing else from Phase 2 came with them: no factions, no tiers, no loot, no abilities. The three sizes are kinds of raider, not a faction's three tiers.
- Problem: every enemy was the same body with the same rifle. Hypothesis: three outlines, three ways of moving and three guns let a player name the threat within a second of seeing it. Smallest test: stand 30 m from one of each and say which is which, then again at 80 m. Acceptance: Matthew names all four without the colours helping (try it at dusk). Not yet tested.

## What was tested

- The rule tests pass outside Unity (`dotnet test`); the count is in the pull request. New in this work: each enemy file has the 14 pieces and the stump, every piece sits on its own joint and stays inside its triangle budget; each kind's hitboxes fit its model; the trooper style changes nothing; no kind's feet slide at a walk or a sprint; no kind squats when it runs; each kind stands differently from the others; the style's limits hold; each gun has its sockets where the script says, its length, its two required pieces and its triangle budget; what shines is small, on the gun, and (for the Sniper) faces forward above the barrel.
- The game code compiles against Unity reference assemblies.
- Every model was looked at from several sides after every change, and all four kinds were posed standing and running by the game's own animation code and looked at together.

## What is not verified

- **Nothing here has been run in Unity.** Nobody has seen these bodies move, lose a limb, hold these guns or fire them.
- How the guns sit in the hands. The grip, support hand and muzzle are where the rifle's are by the same rules, but hands on a new gun need eyes.
- Whether the Marksman's lens can be seen at the range it shoots from.
- The cape is part of the torso piece and does not swing. Whether that looks stiff at a run needs the engine.
- Frame cost is counted from the files, not measured.

## Agent review

**game-designer.** Applied:
- The Marksman and the trooper were the weakest pair from the front (both mid-sized, round helmet, one aerial). The Marksman's cape was in fact fanned the wrong way, narrowing towards the hem. It now fans out past the arms in seven pleats, so the figure is a tall A from the front. Its aerial and pennant are half as thick again, so they do not vanish at range.
- The Bruiser's chest had a glowing port, which says "shoot here" on the part where shots do least (only the head ends an enemy). The port is now dark and the helmet slit is the brightest thing on it.
- The Runner had two little jets on its back, promising a boost it does not have. They are gone.
- The Shotgun passed for a rifle from the side, where only one barrel shows. The barrels are a quarter fatter with bigger coils and mouths, on a hinge block wider than the breech.
- The Runner's head hitbox was the narrowest part of a long helmet: thickness 0.80 raised to 0.88 (the designer asked for 0.95; a new test holds every head's hitbox to within a quarter of the helmet's width, and 0.88 is as far as that goes).
- The Bruiser's helmet sits 5 cm lower than the others', and its head hitbox did not. `BodyFit.HeadRise` now says where each helmet's middle is.
- Movement numbers: the Runner's head bobs less (0.8, was 1.2), so a sprinting head is a fair target; the Bruiser sways less (1.5, was 1.9); the Marksman fidgets a little more at rest (0.4, was 0.2), so it does not look frozen.
- This report now says plainly that kinds and guns are Phase 2 item 13, brought forward by Matthew's request.

Passed to the core build, with the designer's reasons: the Bruiser's head takes six rifle shots against the trooper's three, which is close to the "inflated health" the constitution rules out; recommended is to keep its toughness on the torso and make the head 1.5 times, four shots.

**unity-reviewer.** The shining pieces are safe: nothing in pick-up, dropping, the magazine, indoor lighting or scene restarts is disturbed, and the animator makes no garbage and cannot produce a broken pose from any size or speed. Applied: a body lying on its chest rested at the trooper's chest depth, so a crawling Bruiser would have sunk about 7 cm into the ground; a style now carries the chest's depth (`BodyStyle.Girth`). Passed to the core build, which owns the code: guns are held at fixed distances in metres on a body that is drawn bigger or smaller, so the Bruiser's gun is likely to sit inside its chest plate and the Runner's arm to be nearly locked; a gun held by a scaled body changes size the moment it is dropped; wound caps are not fitted to each kind's thickness.

**deck-performance.** Counted at the spawner's cap of 12 enemies with their guns: about 237,000 triangles and 196 draw calls per pass, which is on the budget's limit, not over it. The limit must not rise until far copies of the bodies and guns exist; an enemy 50 m away is 17 pixels tall and still draws every triangle. The shining pieces cost four draw calls and no extra state change. Applied: the budget rows (the three bodies' real counts, the guns, 13 guns at once, the cost of reading a model the first time). Passed to the core build: a kind's body and gun are read on the frame its first member stands up, guessed at 5 to 15 ms once per kind on the Deck; reading them all when the spawner is built removes it. To look at first in Unity: whether a depth pass doubles those triangles (Stats window facing three camps, then facing away).

## Try it on the Deck

1. Open a terminal in `~/Orbital-ante` and run `git fetch`, then `git checkout claude/playtest`, then `git pull`.
2. Open the project in Unity and wait for it to finish importing.
3. Press Play, fly or walk to where enemies are (the core build's notes say where the camps are for your seed).
4. From a distance, before anyone shoots: can you tell the four apart by shape alone?
5. Watch each one walk and run. Tell me if any feet slide, any knees snap, or the small quick one looks like it is skating.
6. Shoot an arm and a leg off each kind. The stumps and the flying pieces should match the body they came from.
7. Find the tall one with the cape. Before it fires, the front of its sight should shine red. Tell me if you can see it.
8. Pick up each dropped gun and look at it in your hands. Tell me if a hand is in the wrong place or the gun looks cheap from the player's side.
