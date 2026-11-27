# Realistic look, stage 3: light and shadow (26 Nov 2026)

Matthew looked at before and after mock-ups of Earth, the Moon and Mars and chose **"Build as shown"**. This is that build. It sits on top of stages 1 and 2 (PR 90, PR 92).

**Nothing here has been run in Unity.** The pictures in `docs/art/previews/light_*.jpg` are drawn offline with stand-in shapes; left is the earlier light, right is the new. They are not screenshots.

## What changed

**Problem.** Nothing in the game cast a shadow, so rocks, enemies and the lander looked as if they floated, and sunlight wrapped a fifth of the way round onto shaded sides, which is an illustrator's trick. And the ground was lit for where you stood, not for where it was: from high up, the night half of a world was as bright as the day half, and it jumped when the real ground took over from the planet seen from space.

1. **The Sun casts shadows.** The ground itself (hills, crater rims), rocks and trees big enough to be solid, the lander, the launch base, base buildings, enemies, their guns, the rover and loose limbs all cast, out to 70 m from you, with a soft edge. Small plants, glowing parts, cave walls and the cabin do not.
2. **Plain sunlight.** Light falls only on what faces the Sun. Shade is lit by the sky, so it takes the sky's colour on Earth and Mars. On a world with no air the only light in a shadow is what the sunlit ground throws back, so Moon shadows are nearly black, as in the Apollo photographs.
3. **Low sunlight reddens, and night has an edge.** The same sums as the planets seen from space: hard on the Moon, soft where there is air.
4. **The ground is lit for its own place.** Ground over the horizon goes dark where it is night there. From high above, the night ground dims to match the far planet, so the handover no longer jumps (this was the known limit of stage 2).
5. **Cloud shadows on the ground** while the cloud layer is in view from above.

**Dials** (Look asset, under "Light and shadow"): `Real Light`, `Sun Shadows`, `Shadow Darkness`, `Shadow Distance`, `Ground Bounce`. Unticking `Real Light` brings back the earlier light exactly; unticking `Sun Shadows` keeps the new light with no cast shadows.

**Serves** constitution 1 (fun core: the fights and the landings are what you look at most), 5 (legible risk and reward: where the ground is and how far a thing stands off it reads from its shadow) and 7 (cognitive load: things sit on the ground, so distance is judged without thinking).

## Said plainly

- **Cast shadow does not hide you from enemies.** Caves do (that is an existing rule); a rock's shadow on the Moon now looks as dark and hides nothing, and enemies see into shade perfectly while you do not. Known mismatch. My recommendation is to make shadow hide you when stealth is next revisited, not in this change. Your call.
- **Enemies in Moon shade are the risk.** To keep them findable, the light the ground throws into shadows is set at 0.06 (the real figure for grey Moon dust) and does not fall away at a low Sun. If they are still too hard to see, raise `Ground Bounce` first; `Shadow Darkness` below 1 is the "lighter shade" option you turned down.
- **Shadows reach 70 m, not to the horizon of a mountain range.** On foot these small worlds' horizon is 20 to 40 m away, so that covers what you stand among. A far mountain does not shadow a far valley.
- **The practice range changes too**: its shadows now use these settings, and its enemies cast their own shadows.
- The lamp and floodlights still come on only when it is dark, not when you stand in a shadow by day.

## What was tested, and how

- Rule tests: `dotnet test tools/rules-tests`, 2,730 passed, 1 skipped, 0 failed. Ten new ones in `GroundLightTests`: sunlight only on what faces the Sun and never in shadow; the edge of night hard without air and soft with it; low sunlight reddens on every world with air; the ground where you stand gets exactly your fill light; far ground goes dark at its own night and bright at its own day; from above the air the ground is filled by its own sky; night ground keeps its light on foot and matches the far planet from high up; shadow settings stay crisp; the two shaders keep the pieces a build needs; the shader's sums are the rules' sums.
- The runtime C# compiles against Unity reference assemblies (only the usual Unity 6 name errors in untouched files).
- Every shader passes a shader compiler offline in each switch combination, with a stand-in for URP's own shadow code.
- The offline viewer was switched to the built sums and gives the same pictures as the approved mock-ups (slightly less warm on Earth at a low Sun).

## Not verified

- **That it compiles and draws in Unity at all.** The shaders now include URP's real shadow code, which the offline check can only stand in for. If the ground or the models turn magenta, untick nothing: send the first red Console line.
- That shadows appear on everything listed, with no striping on the ground or the hull (bias), and no gap under feet.
- Frame time. The guess is 0.8 to 1.6 ms of graphics time and up to 0.8 ms of processor time, the largest single new cost so far (budget row "Sun shadows").
- A one-off hitch the first time a shadow is drawn.
- Under water and under Venus's cloud; a low orbit across the edge of night; the handover at about 1,800 m above Earth.
- The editor step that saves the shadow settings (cannot be compiled in the cloud).

## Agent review

- **Unity reviewer.** Two blockers, fixed: almost nothing would have cast (every ground tile, rock, building and the ship is made with casting switched off, which the first version did not change), and enemies would each have gained 14 hidden shadow copies. Also fixed: a URP helper that not every version has (written out instead); the sea bed and ground under cloud lit brighter than the rocks on them; a ship in orbit turning red near the edge of night; the practice range's models lit by the wrong Sun; the shadow picture drawn all night for nothing. Not determined: bias quality, and whether coarse ground tiles misbehave at a low Sun.
- **Game designer.** No conflict with the build prompt. Applied: `Ground Bounce` 0.03 to 0.06 and held at a low Sun; the art direction now says the light band is unused under real light. Raised for Matthew: shadow does not hide you (above).
- **Deck performance.** Applied: 70 m not 120 m, sharper near picture, the cheapest soft edge, fewer shader versions, glow pieces and cave lamps never cast, enemies cast with their own meshes (which also fixes the full-body count being halved in the practice range), the little camera screen never redraws shadows. First thing to lower: `Shadow Distance` to 45 m.

## Checking it on the Deck

1. In a terminal in `~/Orbital-ante`: `git fetch`, then `git checkout claude/playtest`, then `git pull` (after this PR is merged).
2. Open the project in Unity. Use the menu `Orbital Ante > Set Up Project` once (it saves the shadow settings).
3. Press Play and start a new run. Look at the Console: if there is a red line naming `OrbitalAnte/Terrain` or `OrbitalAnte/Model`, or the ground is magenta, send the first red line and stop here.
4. Press F3. The line `Sun shadows:` should say `on`.

Then, yes or no:

1. On the pad, do the lander and the launch tower cast shadows on the ground?
2. On Earth, do rocks, trees and enemies cast shadows, with no stripes on the ground and no gap under their feet?
3. Moon, Sun high: can you find an enemy standing in a rock's shadow at 25 m within two seconds?
4. Moon, Sun low: can you still pick out its head from its shaded side?
5. Did you ever try to hide in a shadow and feel cheated that it did not work?
6. Landing into a crater's shadow: could you see the ground before touching it?
7. Coming down to Earth from far off, has the jump in lighting at about 1,800 m gone?
8. With F3 open in a fight near trees, does the Budget word stay green?

To compare: stop Play, click `Assets/OrbitalAnte/Resources/Config/LookConfig`, untick **Sun Shadows** (or **Real Light** for the earlier light), press Play again.

## Next

Stage 4, surfaces and stand-ins, with previews first.
