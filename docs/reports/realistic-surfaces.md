# Realistic look, stage 4: the last stand-in shapes, and ground that is less machine-perfect

27 Nov 2026. Branch `claude/realistic-surfaces`, into `claude/playtest`. **Nothing here has been run in Unity.**

Matthew asked for a game that looks realistic and not made by a machine, "including what the player sees". On 27 Nov he said to make the calls myself and keep building, so this stage was chosen and built without a mock-up card: it replaces things that were plainly unfinished, and every one keeps its old shape as the fallback.

## What changed

**1. Real models for the last plain boxes and cylinders.** Nine things in the open were still drawn as Unity's own cube, ball and cylinder. They now come from two models built by `tools/blender/build_props.py` (picture: `docs/art/previews/props_sheet.jpg`, a Blender render, not a Unity screenshot):

| Thing | What it is now | Started from |
|---|---|---|
| Signal mast (ante task) | A poured footing with a relay cabinet, a flanged steel pole with step bolts, feeder cables, two panel aerials and a drum dish | A rural radio relay site |
| Its power cells | A worn yellow finned canister clamped to the pole, with a lit window and a lit band right round. Shot out, it becomes a burst, scorched canister with no light | A pole-mounted battery pack |
| Salvage crate | An olive ribbed transit case with latches, handles, a strap, and lit strips on all four sides and the lid | A military transit case |
| Supply drop pod | A white panelled capsule on four crush legs, a scorched heat shield, cut parachute risers, marker lamps and a beacon | A re-entry capsule |
| Cave work lamp | A tripod site light with a cable to a small framed generator | A builder's site light |
| Laser sword | A machined grip with a knurled sleeve, guard and slotted emitter; the blade still glows | A torch body |
| Baseball bat | An ash bat with real taper, grain and grip tape | A wooden bat |
| Mailbox | A rural post box on its split post, dented tin, the signal arm up | A rural post box |
| Butterfly knife | Two skeleton handles, pivots, a clip-point blade | A balisong |
| Frying pan | A cast iron skillet with a wrapped handle | A skillet |

What is solid has not changed: the old shapes are still the colliders, so nothing about hitting a cell, ramming a mast, picking up a crate or swinging a weapon is different. If a model file is missing the old shapes show again.

**2. Ground that is less machine-perfect from space.** From orbit the first things that looked generated were perfect ring craters, lines that ran like hoops round the globe, and branches that drew a cross. Now a crater's outline has lobes and scallops, its rim stands higher on one side and has slumped on another, and its rings and terraces come and go. No curved landform is longer than 1.7 radiuses of its world; each bends twice; ridge pairs and scarps run in broken stretches; branches each leave from their own stretch of the main line. The change is modest in the before-and-after globes I drew (the big shapes stay where they were): it removes tells, it does not redesign the land. **The ground of every seed moves a little** (crater rims, where long lines run), so camps and bases found by searching may sit in slightly different places than before.

## Why (constitution)

- 3 Learnable challenge and 5 Legible risk and reward: the thing to shoot and the thing to take must still be findable. The design review found my first cell showed its light from one side only and the crate's strips were too thin; both were fixed (a lit band right round the cell, a wider window with one guard bar, strips on all four sides and the lid of the crate).
- 4 Discovery that matters: a mast, a pod or a lamp in a cave now looks like something someone put there.
- Art direction (14 Nov, "more realistic", not "blocky"): each starts from a named real thing, with wear, dust and soft contact shadow painted in.

## What was tested, and how

- `dotnet test tools/rules-tests`: 2,757 pass, 1 skipped (the usual cabin bake check). New: `PropsModelTests` (2 tests) hold both model files to the sizes, pieces, sockets and triangle counts the game expects.
- The runtime code compiles against reference assemblies (only the known Unity 6 name differences).
- Blender previews of every model, looked at after each change. The thinned models print "seen from inside 0.00%".
- Before-and-after globes of the Moon, Earth and Mars from `tools/terrain_view` (not Unity screenshots).

## Not verified (needs the Deck)

1. Any of it in Unity: the models' look under the game's own light, the swap to the burnt cell, the mast falling with its cells, the crate in the hands.
2. Frame time. Counted, not measured: a mast near the player is about 6,600 triangles and 8 draw calls (it was about 210 and 6); a mast further than 175 m is still the old plain shapes.
3. The switch between plain shapes and the model at 150 m: it may pop.
4. A fallen mast may pass through its taller footing and cabinet (the old footing was a low block).
5. The sizes of the lit pieces at 30 to 60 m.

## On the Deck

1. `git fetch`, `git checkout claude/playtest`, `git pull` (after this is merged).
2. Start a run, fly to a world whose task line names a mast. Walk up to it: does it change from a plain pole to the real mast without a jarring pop? Can you tell which cells are live from every side?
3. Shoot a cell out. Does it go dark and look burst? Shoot the rest: does the mast fall with its cells?
4. Pick up a crate and carry it. Does it look right in your hands, with no stray shadow?
5. Find a cave with a lamp (Earth, the Moon, Mars). Does the lamp look like a site light, dim in the dark?
6. Practice range: look at the five melee weapons lying ahead of the start, pick each up and swing it.
7. From orbit, look at the Moon and Mars: do the craters and long lines look natural?

## Agent review

- **Unity reviewer.** One real defect: the sword's glowing blade would have cast a shadow, even in the hand (fixed: glow pieces never cast). A carried crate would have thrown a shadow from in front of the eye (fixed: no shadow while carried). The models were read mid-play on first use (fixed: read when the solar world starts). Noted and left until seen: the fallen mast may clip its footing. Checked and sound: the falling pivot's child order, crate and pod pieces following their parents, melee collider bounds, indoor materials, no shared mesh or material destroyed, the cave lamp reading the cave's light.
- **Deck performance.** Every mast on a world stands at once (worlds are smaller than the 900 m build range), so two dozen masts at my first triangle counts would have been about 345,000 triangles. Applied both of its fixes: every piece thinned to about a third (a mast 13,800 to 6,600), and a mast wears the model only within 150 m. Budget rows "Task views" and "Melee items in the world" rewritten; "Sun shadows" no longer lists task views under never.
- **Game designer.** Nothing against the build prompt. Applied: the cell is lit from every side and its window is larger; the crate is lit on all four sides and the lid. Rule recorded: orange light for things to shoot, cyan for things to take. Tone: a real mailbox and a real skillet are funnier played straight.

## Not done in this stage

- The painted cloud pictures (brush-stroke swirls on Earth, scratch-like wisps on Mars) are unchanged.
- The arena's own test props and the mannequin fallback are still primitives; they only show when a model is missing or in the practice range.
- Guns are another thread's work (the realistic gun replacement).
