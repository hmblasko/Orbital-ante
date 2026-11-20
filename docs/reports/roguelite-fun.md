# Roguelite fun pass: the ante break, the run build and rising pressure (20 Nov 2026)

Matthew asked (20 Nov 2026): "do research on what makes a game fun including rouge like and implement the principals into the game in an intuitive way", and to keep getting ChatGPT to help.

Research and plan: Claude Doc "Roguelite fun: research and plan". This report covers what was built from it.

**Nothing here has been run in Unity.** It compiles against reference assemblies and the rule tests pass in the cloud. Feel, layout on the Deck's screen and frame cost are unverified.

## What changed, and the principle behind each piece

| Built | Principle from the research | Constitution |
|---|---|---|
| The break between antes is one page with six rows: Pick a perk, Shop, Scratch ticket, Raise the stakes, Start ante, Restart or quit. It shows banked points, run score, what you can spend ("spending never lowers your score"), your sets and the next target. | Fast, legible choices with a cost; low cognitive load | 2, 5, 7 |
| 23 perks that all do something in the game (gun and melee damage, damage taken, healing on kills, Focus, move speed, ladder speed, long-shot distance, time back), in five families: Gunner, Brawler, Ghost, Pilot, Daredevil, plus three wild ones. Every strong perk has its cost written on it (Glass Cannon, All In, Fleet Foot, Thick Skin...). | Builds that change how you play; trade-offs, not straight upgrades | 2, 6 |
| Family sets: hold 3 of a family and its set bonus turns on (shown as GUNNER 2/3, then SET ON). The draft offers families you already hold more often, so a set is reachable in a normal run. Skipping a pick pays 250 to spend. | Visible synergy; a plan you can steer | 2, 5, 6 |
| Raise the stakes: before an ante, choose Hunted (more heavies, enemies sharper), Thin Air (health does not come back by itself; kills, perks and the ship still heal) or Short Fuse (60 s less). Each says exactly what it does and its own points multiplier (x1.25, x1.10, x1.25, never over the existing risk cap). | Chosen risk for chosen reward | 5, 8 |
| Rising pressure each ante: enemies react and aim a little sooner (down to a floor), and from ante 4 a seeded share of plain enemies wake as their side's heavy tier. Never more health. The break says what the next ante brings. An enemy keeps its kind for the whole run. | Rising challenge that stays learnable | 3, 8 |
| Run recap on the death and run-over pages (antes cleared, score, best ante and best score), saved (save version 4). Quitting to desktop records the run too. | Death as progress; a reason to go again | 6 |

## For Matthew to decide (nothing is blocked)

1. **Select.** Part B says Select opens the end-of-ante upgrade screen, but Select is currently Floodlights. For now the break opens by itself when an ante is banked, and Select stays Floodlights. Say if you want Select moved.
2. **Targets.** The design review thought the ante targets may feel steep once perks and hazards are in play. They are left exactly as you chose on 20 Nov (about 1.6x ante 1 by ante 4). Worth watching in the playtest.

## Tests

- `dotnet test tools/rules-tests`: 2,605 passed, 1 skipped (the cabin bake check that needs Unity's files).
- New `Tests/EditMode/RogueliteFunTests.cs`: every perk does something, family counting and set bonuses, family weighting in the draft, hazard multipliers and the risk cap, stacking caps, enemy pressure per ante, the heavy wake rule is seeded and stable, `AnteDirector.AdjustSeconds`, save version 3 to 4.
- Compile check of the whole Runtime folder against reference assemblies: only the two known `excludeLayers` errors. The compile check used before missed every error inside method bodies (a clash in a reference library stopped the compiler before bodies). The corrected check found one real error that came from the ante tasks branch (`glow` in `TaskDirector`), now fixed there too.

## Agent review

- **Unity reviewer:** an unbraced declaration in `TaskDirector` (fixed), cursor jumping to the top after every buy (kept in place now), a held button firing the death page (held off), Back on the restart/quit page (now its own page), quit to desktop not recording the run (now does), an enemy changing kind between antes (now keeps it).
- **Game designer:** picks lost by pressing Start (asks first now), sets unreachable (held families weighted), pressure hidden (now on the break page), scratch odds shown before you scratch, one multiplier for all hazards (now each its own), cheap perks with no cost (costs added), skip reward raised to 250, Gunner and Pilot set bonuses changed so they don't overlap caps.
- **Steam Deck performance:** a string made per woken enemy (int version now), sharpened enemy settings leaked (destroyed with the spawner). Budget lines added to `docs/performance_budget.md`.

## Unverified (needs Unity)

Everything that is seen or felt: the break pages on the Deck's screen and with a controller, whether perks feel strong enough, enemy sharpness at high antes, the heavies' share, how hard Thin Air is without regeneration, the recap text, frame cost.

## Playtest on the Steam Deck (after this is merged into the testing branch)

1. In Desktop Mode open Konsole and type `cd ~/Orbital-ante`, then Enter.
2. Type `git fetch`, Enter; `git checkout claude/playtest`, Enter; `git pull`, Enter.
3. Open the project in Unity Hub and wait for it to finish compiling.
4. Press Play and start a solar-system run.
5. Bank ante 1 (meet the target). The break page should open by itself.
6. Choose **Pick a perk**, take one, and check the family line (for example GUNNER 1/3).
7. Choose **Shop**, buy something, and check the cursor stays on the row you bought.
8. Choose **Scratch ticket** and check the odds were shown before you scratched.
9. Choose **Raise the stakes**, pick Hunted, then **Start ante**. Check the points lines show the bigger multiplier.
10. Die on purpose. Check the death page shows the recap and your best ante.
11. Tell me what felt good, what felt off, and anything that didn't show up.
