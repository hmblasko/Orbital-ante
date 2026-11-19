# More landscape, standout landforms and more bases (19 Nov 2026)

Matthew, 15 Nov 2026: "Add more interesting landscape to the planets." and "we need more bases throughout and oxygen refills to be in bases. Also need so significantly more landscape."

Nothing here has been run in Unity. The pictures in `docs/art/previews/landforms_*.jpg` are from the offline viewer `tools/terrain_view`, not Unity screenshots: no trees, rocks, bases, sky effects or close-up ground shader.

## What changed

- **Four new kinds of landform** in the shape of the ground itself (`PlanetTerrain.Standouts`): winding flat-floored canyons, long toothed ridges, fields of rock towers (buttes with a fallen-rock apron, a cliff band and a broken top) and dune fields. Each keeps to its own country, so a world has canyon land, tower land and plain land rather than everything everywhere.
- **Each world has its own signature.** Mars: canyons 50 m deep and wide enough to fly, towers, low dunes. Earth: buttes, ridges, 18 m canyons. The Moon: narrow rilles. Mercury: scarps. Venus: canyons and ridges. Io: ridges and low towers. Europa: ridges only. Enceladus: cracks only. Titan: 6 m dunes and shallow canyons. Titania: a 14 m canyon. Callisto: knobs. Inside Jupiter, Saturn and Uranus: tall towers, ridges and canyons. Neptune is unchanged.
- **Twice the seeded landforms** of every world's list (craters, volcanoes, faults, wind-cut rock, ice forms); it was one and a half times. Cave networks, landmarks, seas and woods are not multiplied.
- **About twice the bases.** One per 42,000 square metres (was 90,000), 2 to 50 a world (was 1 to 24), at least 130 m apart (was 180). Found over eight seeds: Earth 28 to 36 (was 15 to 18), Mars 31 to 36 (was 15 to 17), Venus 50 (was 24), the Moon 6 to 7 (was 3), Mercury 19 (was 9), Titan 6 to 11 (was 4 to 5), Europa 4 (was 2), Enceladus 2 (was 1). Every base still has its guards.
- **The search for base sites runs on a background thread**, so more bases do not cost a hitch on first approach. Enemy camps wait for it, because guards are part of the camps.
- `PlanetProfiles.Version` is 5, so the Planets asset on Matthew's machine takes the new numbers.
- The cave light box budget is a quarter larger (1.25 MB): a ridge or tower over a way in makes the box a little taller.
- `tools/terrain_view` has `find <body>` (where each form shows most) and `bases` prints search times.

## Why, and which principles

Discovery that matters (4) and meaningful replayability (6): landing anywhere should show something worth walking or flying to, and worlds should be told apart by their ground. Fun core gameplay (1) and varied intensity (8): more bases mean more to fight and loot, and the spacing was kept above the longest enemy reach (110 m) so one base's marksman does not reach the next.

## Tested

- 2,537 rule tests pass in the cloud (`dotnet test tools/rules-tests`), six of them new: canyons only cut down and never deeper than their dial; ridges, towers and dunes only add ground and stay under their dials; forms are gathered in country; every world stays inside its stated bounds and far ground leaves towers out; bases are throughout every world.
- Compile check against reference assemblies: no new errors.
- Height cost, quickest of five on a cloud machine, millionths of a second, before then after: Earth 3.7 then 3.6, Mars 3.1 then 4.9, Moon 3.0 then 3.0, Titan 3.5 then 3.8. Mars's rise is mostly its doubled craters and other stamped landforms.
- Level ground (slope under 0.12): Earth 29%, Mars 25% by the viewer's count. The rule test's own sample puts Mars near 20%, so its floor was lowered from 22% to 18%.

## Agent review

- **unity-reviewer:** a failed or cancelled base search would have left a world with no enemies for the run (fixed: a failure answers "no bases", a cancelled search starts again); a first attempt at clamping the cave light box would have drawn a hard light line (removed, budget raised instead); three hard detail-level switches in towers and ridges would have cracked between tiles (now fades); the launch site was recomputed on every poll (moved).
- **game-designer:** the first numbers (60 bases, 110 m apart) would have kept the enemy cap full everywhere with two garrisons at once as the normal case, and worlds read alike. Density was eased and each world given a signature. Its two questions for Matthew are below.
- **deck-performance:** the search could be starved by ground tiles and hold enemies back (priority changed to rank as a tile 300 m off, patient); avoidable noise lookups in `Standouts` (country lookups now lazy, towers rejected before their outline noise). Not done: a batched `AlsoKeepClear` (a guessed 2 to 6 ms once per world) and a nearest-only `Cells`.

## Not verified

- Nothing was seen or timed in Unity or on the Deck. Deck figures above are guesses.
- Whether ground arrives fast enough on Mars when landing (press F3, time from 300 m up until the tile count stops rising).
- Whether enemies and guards appear within a couple of seconds of a world's ground, including when flying low and fast.
- Seams or popping at tower fields and ridges between near and far ground.
- How the forms look with the close-up ground shader, scenery and bases on them.
- Whether fights are too constant with twice the bases. No playtest has been done.
- Oxygen refill stations are in the enterable-bases work (PR 53), which is still open and not in `claude/playtest`.

## Questions for Matthew

- Should some bases stand empty or lightly held, to give lulls between fights? (The designer suggests about one in three.)
- Is one signature landform per world right, or should every world have every form?
