# Realistic look, stage 2: planets seen from space (26 Nov 2026)

Matthew looked at before and after mock-ups of seven worlds and chose **"Build all as shown"**, which includes Venus's ground being hidden by cloud until you fly down through it. This is that build. It sits on top of stage 1 (the real sky, PR 90).

**Nothing here has been run in Unity.** The pictures in `docs/art/previews/planet_*.jpg` are drawn offline with a copy of the shaders' sums and the game's own numbers: before on the left, after on the right. They are not screenshots.

## What changed

**Problem.** From space every world was a smooth ball lit like a toy: light wrapped a fifth of the way round onto the night side, the air was a wide soft glow far thicker than any real sky, the gas giants were two colours of sine-wave stripes, Saturn's rings were five solid washers, and nothing had clouds. Those are the tells of a quick procedural planet.

1. **A real night side and edge of night.** The far ball's night side is black (a faint fill is kept, dial `Planet Night Side` on the Look asset, 0.15). The edge of the night is hard on a world with no air and soft where there is air, and low sunlight turns red on its way through.
2. **Relief that catches the light.** The far ball is lit by the real ground's slopes, worked out from the same heights the land is built from, so mountains and crater rims throw shade near the edge of the night. No extra questions are asked of the ground: the heights are kept while the far picture is painted.
3. **Air as a thin shell.** The wide halo is replaced by a shell worked out for each line of sight: a narrow bright line at the sunlit limb, a planet that pales toward its edge, a red band where day meets night, a ring of light when the Sun is behind the planet. A gas giant's edge darkens instead.
4. **Clouds.** Earth has weather (with storm swirls) that drifts slowly and casts shadows on the far ball; Mars has thin wisps; Venus has an unbroken deck that hides its ground. A layer parts round ground as high as itself, so mountain ranges rise out of gaps. It fades out as you come down to it, because the sky seen from the ground is drawn separately.
5. **Seas, lights, aurora.** The Sun glints on Earth's sea and Titan's lakes. A world's bases show as small warm lights on its night side, once the game knows where they are (which happens as you approach the world). Earth has a thin polar glow on its night side.
6. **Gas giants.** Jupiter, Saturn, Uranus and Neptune wear painted cloud tops: belts that slide past each other and curl, storms, Neptune's dark spot. Painted once by `tools/planet_pictures` (they are the same every run), from noise, with no photograph used.
7. **Rings.** Saturn's rings are one see-through band of hundreds of ringlets with the real gaps. The planet's shadow lies across them, their shadow falls on the planet, and the side turned from the Sun is dim.

`Real Planets` unticked on the Look asset brings the earlier look back, and each piece falls back by itself if its shader or picture is missing.

**Serves** constitution 1 (fun core: the worlds are what you fly toward), 4 (discovery that matters: a world is worth looking at before you land, and its lit bases tell you something), 5 (legible risk and reward: lights on the night side are where the guards are) and 7 (cognitive load: each world is recognisable at a glance).

## Known limits (said plainly)

- **The realistic lighting is on the far ball only.** Inside about four radii of a world the real ground replaces the ball, and it is still lit as before: its night side is brighter and it shows no cloud shadows or base lights. Expect a visible change of lighting at that distance (about 1,800 m above Earth). Making the real ground match is stage 3 (light and shadow).
- **Night lights only appear for worlds you have come near**, because that is when the game works out where a world's bases are.
- **Venus's ground cannot be seen from orbit**, as chosen. Lock, autopilot and the orbit aid do not look at the ground, so they should be unaffected, but that has not been tried.
- Earth's far-map ground itself (looped rilles, perfect-ring craters, the cross-shaped range) is unchanged; relief shading makes it clearer. That belongs to stage 4.

## What was tested, and how

- Rule tests: `dotnet test tools/rules-tests`, 2,720 passed, 1 skipped, 0 failed. Thirteen new ones in `OrbitLookTests`: every world's air is a thin shell that has faded by its edge; airless worlds have a hard night edge; shells and cloud shadows fade before anything can pop; slopes, sea and height are stored correctly and the right way round; night lights land where their base is, even across the picture's seam; every gas giant and cloud layer has its picture; the shaders keep their switches in a build and use no reserved words.
- The runtime C# compiles against Unity reference assemblies (an older Unity's, so only the usual Unity 6 name errors in untouched files).
- Every new and changed shader's vertex and fragment programs pass a shader compiler offline, in each switch combination.
- Offline previews: `tools/planet_pictures/preview_planets.py`.

## Not verified

- That the four shaders compile in Unity and on the Deck. The review caught one mistake the offline check could not see (a variable named `real`, a word URP defines); it is fixed and the offline check now knows that word, but there may be others of the kind.
- How any of it looks in the game, including the clouds parting round mountains, the rings through the cockpit glass, and shimmer in the ringlets or the limb line.
- Frame time and memory (the budget line is a guess: up to about 1 ms more near Earth or Saturn, about 28 MB on the graphics card).
- The editor script that sets the pictures' import settings (it cannot be compiled in the cloud).
- Whether a 20 to 30 m gap between the cloud layer and the air shell flickers at long range (depth precision).

## Agent review

- **Unity reviewer.** One blocker, fixed: a shader variable named `real` would have stopped every planet's shader compiling. Also fixed: clouds sat below the mountains and would have been cut by them along hard lines (the layer now parts round high ground and floats a little higher); the ring strip now has smaller copies so ringlets do not shimmer; a possible bad number at the exact pole in the aurora; a stale comment. Not fixed, by design: the lighting change where the real ground takes over (stage 3).
- **Game designer.** No conflict with the build prompt. Applied: the night side keeps a faint fill on a dial, so a moon met from its dark side is not black on black; the aurora is thinner and half as bright. Venus's deck already hides its lights. Noted for later stages: the far-map ground's looped rilles and perfect craters, brush-stroke cloud swirls, scratch-like Mars wisps.
- **Deck performance.** Applied: no relief picture for Venus (never seen under its deck) or the gas giants; the relief picture goes to the graphics card a frame after the far picture, so one frame never carries both; the relief version of the shader is made ready at run start, not mid-flight; cheaper picture sampling. First thing to lower if memory is tight: relief at half width.

## Checking it on the Deck

1. In a terminal in `~/Orbital-ante`: `git fetch`, then `git checkout claude/playtest`, then `git pull` (after this PR is merged).
2. Open the project in Unity and wait for the new pictures to import (a minute or so, once).
3. In the Project window open `Assets/OrbitalAnte/Resources/Planets` and click `JupiterTops`. The preview at the bottom of the Inspector should say **2048x1024** and **BC7**. If not, use the menu `Orbital Ante > Set Up Project` once.
4. Press Play, start a new run, fly up to orbit. Look at the Console: if there is a red line naming `OA_Body`, `OA_Air`, `OA_Clouds` or `OA_Rings`, send the first one and stop here.

Then, yes or no:

1. From orbit, is each world you can see recognisable at a glance?
2. Is Earth's night side dark, with a thin bright line of air on the sunlit edge and no wide blue halo?
3. Do Earth's clouds drift, and do mountains rise out of gaps in them with no hard cut lines?
4. Flying down to Earth from far off, is there a jump in the lighting at about 1,800 m up? (Expected for now; say how bad.)
5. Approaching the Moon from its night side, can you see it before you are close?
6. Descending through Venus's cloud, is there no pop or flash, and do lock and autopilot still work?
7. Do Saturn's rings show fine ringlets, the planet's shadow across them, and no shimmer?
8. With F3 open, does the Budget word stay green about one Earth radius above Earth looking down, and just above Saturn's rings looking at the planet?

To compare with the old look: stop Play, click `Assets/OrbitalAnte/Resources/Config/LookConfig`, untick **Real Planets**, press Play again.

## Next

Stage 3, light and shadow: make the real ground match the far ball (night side, edge of night, cloud shadows), with previews first.
