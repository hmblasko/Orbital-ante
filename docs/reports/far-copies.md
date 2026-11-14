# Far copies: simpler enemies and guns a long way off

Status: **built, not run in Unity.** Everything below was checked outside the engine: 1,958 rule tests pass (5 skipped as before), the game code compiles against reference assemblies, and the pictures were drawn in Blender from the same shapes the game files are written from. They are not Unity screenshots. Nobody has seen a swap happen in the game, and no frame time has been measured.

Pictures:
- `docs/art/previews/far_copies.jpg`: the four enemy kinds from 5 m, 9 m and 24 m at the Deck's resolution and the game's width of view. In each pair the full bodies are above and the far copies below.
- `docs/art/previews/far_copies_close.jpg`: each kind from under 3 m, much closer than the game means to show a far copy. In each pair the far copy is on the left and the full body on the right.
- `docs/art/previews/far_copies_guns.jpg`: the five guns, full above, far below.

## What Matthew asked for (14 Nov 2026)

"Numerous enemies everywhere... fighting near constantly", on a Steam Deck.

An enemy body is 12,500 to 14,850 triangles and its gun 3,900 to 7,700, so the game holds itself to 12 enemies at once (`liveCap`). The budget said that number could not rise until enemies far away were drawn more cheaply. This is that piece. It does not raise the number: that is for the core build to do once this has been seen working and measured on the Deck.

## What changed

**A far copy of every enemy body and every gun.** Nine new model files beside the full ones (`EnemyFar`, `EnemyRunnerFar`, `EnemyBruiserFar`, `EnemyMarksmanFar`, `RifleFar`, `PistolFar`, `SmgFar`, `ShotgunFar`, `SniperFar`). Each has the same pieces by the same names and the same sockets, so the game can point a piece at one or the other.

| | Full | Far copy |
|---|---|---|
| Trooper | 14,848 triangles, 9,872 corners | 4,473 triangles, 4,601 corners |
| Runner | 12,520 / 8,394 | 4,469 / 4,137 |
| Bruiser | 13,508 / 9,120 | 4,476 / 4,394 |
| Marksman | 14,020 / 9,766 | 4,462 / 4,657 |
| Guns | 3,868 to 7,696 triangles | 1,525 to 1,993 |

**How a far copy is made** (`oa.thin` in `tools/blender/oa_blender.py`; the same script that builds a model writes its far copy, so the two cannot drift):
1. Fittings a few centimetres across (bolts, studs) are left out.
2. Small flat shapes (plates, straps, buckles) are kept exactly as they are.
3. A few named shapes that cross each other at a shallow angle are kept whole too (the trooper's helmet, visor and visor frame; the Marksman's chest plate), and one that barely shows is left out (the Runner's vest, all but covered by its jacket).
4. Everything else, the rounded shapes that hold most of the triangles, is thinned by one share, and its paint is looked up again from the full model.
5. Faces nobody can ever see are then taken out: the chest under a plate, and the patches where a thinned shape pokes through the one on top of it. This is worked out piece by piece, because the pieces move against each other in the game, and a face is only taken out if something still covers the place it leaves.
6. Parts that shine (`...Glow`) are copied exactly.
7. The script then counts how often the far copy can be seen from inside (through a hole) from all round. All nine score 0.00%, the same as their full models.

**The first version was not good enough, and the pictures are why it changed.** The first far copies were 2,950 triangles and looked the same as the full ones from 18 m. A reviewer pointed out that the game can show a far copy much closer than that, so they were drawn again from 5 m and 9 m. From there they looked cheap: creased leg plates, a crumpled visor, and patches of the wrong colour where a shape underneath poked through the one on top. Steps 2, 3 and 5 above are the fixes, and the far bodies went from 2,950 to about 4,470 triangles to pay for them. Three other fixes were tried and thrown away because the pictures showed they were worse: painting each corner with whatever colour the full model shows there smeared pads down the limbs; sinking covered corners crumpled lenses; and taking hidden faces out before thinning instead of after left holes you could see into, which is why step 7 exists.

**Choosing which copy to draw** (`Look/DetailRule.cs`, plain rule; `Look/ModelDetail.cs`, the Unity side):
- A body goes over to its far copy beyond 24 m and a gun beyond 9 m. A bigger body changes further off in proportion (Runner 20 m, Bruiser 31 m, Marksman 29 m), so each kind changes at the same size on screen: about 36 pixels tall.
- It changes back a little nearer than it changed over (12% either way), so one standing at the edge does not flicker.
- 24 m, not the first version's 18 m, because the trooper stands and fights at 18 m. It would have stood on the line, changing whenever the player stepped forward or back.
- At most 12 bodies are full at once. The places go to the nearest, and to bodies in front of the player before ones behind. Twelve is how many enemies there are today, all drawn in full, so **nothing close up looks any simpler than it did before this change.** The limit only starts to matter when the count is raised.
- A gun in somebody's hands is never drawn finer than the body holding it.
- A limb that has been shot off goes on being watched by itself from where it lies, so one shot off far away is full again when you walk up to it. A dead body takes none of the 12 places.
- Everything is looked at together five times a second, not every frame.
- Hitboxes are never touched. Neither are the parts that shine, which carry the tells (the Marksman's lens).

**Dials** on the Look asset, under "Far copies": `Far Copies` (the master switch), `Full Bodies` (12) and `Far Copy Distance` (1 means 24 m and 9 m).

**F3** has a new line, for example `Far copies: bodies 5 of 12 (full 7/12, nearest far one 26 m)   guns 6 of 13   loose pieces 0 of 9`.

## Why, and which principles it serves

- **1, fun core gameplay first** and Part B section 9 ("Enemy count: substantially high"): this is what stands between the game and more enemies on a Deck.
- **3, learnable challenge, no hidden rules** and **5, legible risk and reward**: the swap must not change anything the player reads or aims at. Outlines are held to within 3.5 cm by a rule test, hitboxes are fitted to both copies by a rule test, limbs still come off as the same 14 pieces, and the shining tells are the very same meshes.
- **The art direction** (no cheap look): the far copies were judged from 5 m, not only from where they switch, and rebuilt until they held.

## Constitution rule 10

- **Problem:** each enemy with its gun is about 20,000 triangles, so only 12 can be present, and Matthew asked for many.
- **Hypothesis:** beyond 24 m a body of about 4,500 triangles cannot be told from the full one on the Deck's screen, and 12 full bodies are enough for the ones being looked at.
- **Smallest test:** the pictures in this report, then the Deck steps below.
- **Acceptance:** Matthew cannot say when a body changes; limbs, heads and disarms behave the same at range; the Marksman's glint and line show before every shot; the Budget line on F3 is no worse than before.
- **Playtest protocol:** the numbered steps under "On the Deck".

## What was tested, and how

- **Rule tests (1,958 pass, 5 skipped, outside Unity):**
  - `DetailRuleTests` (11): the two ranges, no flicker at the edge, only the nearest few full, guns not counted, two bodies at the same distance do not swap, a body behind the player gives way to one in view, the trooper does not stand on the line, odd input breaks nothing.
  - `TheFarCopy_IsTheSameModel_InAFractionOfTheTriangles` (9 models): triangle caps (4,800 a body, 2,200 a gun), under 0.4 or 0.45 of the full model's triangles and 0.62 of its corners, same sockets, same piece names, shining pieces identical, outlines within 3.5 cm, still painted.
  - `EnemyModelFile_ItsHitboxesFitIt` now runs on the four far bodies as well as the four full ones.
- **Files:** all nine read back by a separate script: no broken numbers, no stray colours. The build's own count of holes is 0.00% for all nine.
- **Compile:** the runtime code compiles against reference assemblies.
- **Pictures:** every version of the far copies was drawn and looked at from 5 m, 9 m and 24 m and from under 3 m.

## What is still unverified

- **Nothing has been run in Unity.** Whether the swap can be seen happening, on any kind, at any distance.
- **The new PlayMode test** `FarAway_TheBodyWearsItsFarCopy_AndNothingYouShootAtChanges` has not been compiled or run: it needs the engine.
- **Frame time and Batches.** Every cost in `docs/performance_budget.md` is counted from files or guessed.
- **Draw calls do not fall.** A far body is still 14 draw calls and a far gun 2 or 3. At 30 enemies that is about 490, a third of the whole budget, and the reviewer expects draw calls, not triangles, to be the first limit. This change does nothing about that.
- **Close up, a far copy is still simpler.** From under 3 m you can find the differences (see the close picture): the trooper's chest panel loses its three small lamps and its aerial loses the ball on top, the Bruiser's porthole loses its rivets, and the Marksman's white chest plate shows a few thin grey slivers where the suit underneath pokes through. None of these can be made out in the 5 m picture. The game only shows a far copy that close if more than 12 enemies stand close in view.
- **The pictures are Blender's renderer, not the game's shader**, so they judge shape and paint, not the finished frame.

## Agent review

Three reviews were run on the first version, before the rebuild described above.

**unity-reviewer** found nothing that would throw on Play. Its findings:
- Dead bodies kept their places among the full ones for 25 to 30 s, pushing living enemies to far copies. **Fixed:** a body with nothing left under it leaves the list.
- Shot-off limbs and bodies were frozen on whichever copy they wore. **Fixed:** loose pieces are watched by themselves.
- The list of cameras had a fixed size and would have thrown if more than 16 were ever enabled. **Fixed**, the same way `PostFx` guards it.
- No PlayMode test. **Added**, not run.
- Checked and found sound: start-up order with domain reload off, restart, the player's own gun (it cannot show a far copy), measuring from the camera that is on screen when the dash takes over the view, the arena's shadow copies (swapped too, which is right), hitboxes (plain shapes, untouched).

**game-designer** found nothing lost that the player must read, and nothing from Phase 2. Its findings:
- The same dead-body fault. **Fixed.**
- The limit showed far copies at distances nobody had looked at them from; the picture was drawn at 60 degrees of view, not the game's 80. **Acted on:** new pictures at 5 m, 9 m and 24 m at 80 degrees, which is what led to the rebuild; bodies in view now get the full places first; the limit went from 8 to 12 so today's fights look exactly as they did.
- The trooper stood on the 18 m line. **Fixed:** 24 m, with a rule test.
- Guns were not counted. **Fixed:** a held gun follows its holder.
- The hitbox test did not cover the far copies. **Fixed.**
- Wording that claimed more than was known ("too far away for the difference to show", "as the Deck shows them", a pixel count worked out at the wrong width of view). **Corrected.**
- Not done: a pause of about 2 s before a body may change again. Left until the swap has been seen; the 12% slack may be enough.

**deck-performance** measured the rule outside Unity (under 6 microseconds for 61 things, no garbage) and counted every file. Its findings:
- Draw calls do not fall and are the first limit at 30 enemies. **Not fixed here**; written into the budget and into "still unverified" above. Its suggestion to try first, if a second drawing pass turns out to be running: set the blood decals' technique to Screen Space.
- Full guns were not limited. **Fixed** for held guns; guns lying loose are still not counted (12 at most).
- A camp woken 150 m away was drawn in full for up to a fifth of a second. **Fixed:** everything is looked at again on the frame something new is made.
- `Attach` asked each piece what it wore once per piece of the model. **Fixed.**
- The working lists were re-made at 33 and 65 things. **Fixed:** they start at 128.
- Passed to the core build, not changed here: the spawner's preload skips the rifle (it is read when the player's own is made, so nothing breaks today).
- Its advice was to leave 18 m alone until measured. Not taken, for the designer's reason above; the cost is that more bodies are full on an ordinary frame. The worst frame is unchanged, because the limit sets it.

## On the Deck

1. Get the build the usual way (`git fetch`, `git checkout claude/playtest`, `git pull`) once the core build has merged this, open the project and press **Play**.
2. Press **F3**. Find the line that starts `Far copies`.
3. Land near a camp and walk slowly towards one ordinary enemy from far off. When the first number on that line drops, that was the swap. **Did you see the enemy change?** Step back and forward across that spot five times.
4. Do the same with the small quick one and the big one. Watch the gun as well as the body.
5. Stand 30 m or more from the tall caped one. Before every shot, do you see the glint on its gun and the line?
6. From beyond 25 m shoot an arm off, then a leg, then the head. Did each take the same number of hits as it does up close?
7. Walk up to a limb you shot off from far away. Does it look any cheaper than one shot off close?
8. Write down the `ms avg`, `ms worst` and Budget lines from F3 facing a camp and facing away from it.
9. To see the limit at work on purpose: stop Play, click **Assets > OrbitalAnte > Resources > Config > LookConfig** in the Project window, and in the Inspector set **Full Bodies** to 4. Press Play, pull a camp close, and say whether you can pick out the simpler bodies and whether anyone changes while you watch. Set it back to 12 afterwards.
10. If any swap is easy to see, raise **Far Copy Distance** on the same asset to 1.5 and try again. Tell me the number at which you stop noticing.

To run the tests instead: `tools/check-in-unity.sh`, then read `unity/Logs/oa-check/summary.txt`. The new PlayMode test is `FarAway_TheBodyWearsItsFarCopy_AndNothingYouShootAtChanges`.
