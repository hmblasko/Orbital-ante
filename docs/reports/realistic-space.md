# Realistic look, stage 1: the real sky (26 Nov 2026)

Matthew asked for the game to "look realistic, including the space atmosphere (needs to look more like real space and have interesting galaxy structures)", and then for all parts of the game, the screen included, to stop looking machine-made. He chose **"Real sky plus showpieces"** and approved the five-stage plan. This is stage 1.

**Nothing here has been run in Unity.** The pictures in `docs/art/previews/sky_*.jpg` are drawn offline from the same two files the game reads. They are not screenshots.

## What changed

### 1. The stars are the real ones
**Problem.** The sky was 1,680 square white stars scattered at random in three layers. Even, same-sized, colourless dots are what a generated starfield looks like; a real night has a huge spread of brightness, a few coloured giants and patterns people know.

**Change.** `Resources/Sky/Stars.bytes` holds the 9,096 stars of the Yale Bright Star Catalogue (public domain) at their true places, brightness and colour, then about 18,000 fainter ones scattered by a seed, thicker along the Milky Way. `StarCatalogue` (plain C#) reads it, `SpaceMeshes.RealStars` makes one mesh and `OA_Stars.shader` draws each star as a soft round dot sized in screen dots, so no star is ever a square. `StarLook` turns a star's magnitude into size and brightness. Orion, the Plough, the Southern Cross and the rest are simply there, and because the sky's frame is the game's (Y is the north of the planets' plane) the planets move through the constellations they really move through.

### 2. Deep space is a picture of the real sky
**Problem.** The backdrop was a painted band of colour: soft, purple-blue and without structure.

**Change.** `Resources/Sky/DeepSpace.png` (4096 x 2048) is baked by `tools/sky`: a Milky Way made by marching a ray for every dot of the sky through a model galaxy (old disc, blue arms, yellow bulge, clumpy dust that reddens what is behind it), the Magellanic Clouds, Andromeda, Triangulum and nine real nebulae at their true places, and 70,000 dots too faint to draw as stars. `SkyDome` hands it to `OA_Sky.shader`.

### 3. Showpieces
Six invented things that are not in the real sky, placed away from the Milky Way so they stand against black: a grand spiral galaxy, an edge-on galaxy, a tilted barred spiral, a large glowing nebula, a blue dust nebula and a faint field of far galaxies. They are listed in `SHOWPIECES` in `tools/sky/bake_sky.py`; moving, dimming or removing one is a one-line change and a re-bake.

### 4. The Sun hides the stars near it
Stars and deep space fade toward the Sun (`OA_SunGlare`, dial `Sun Hides Stars` on the Look asset, 0.75), as they do for any eye or camera, and behind a daytime sky as before.

### 5. The old sky is kept
`Real Sky` unticked on the Look asset, a missing file or a missing shader brings back the painted band and the square stars. That is also the quick before and after.

**Serves** constitution 1 (fun core: flying between worlds is the game, so what fills the window matters), 4 (discovery: there are things in the sky worth turning the ship to look at) and 7 (cognitive load: planets stay the brightest points, so the sky never hides where to go).

## What was tested, and how

- Rule tests: `dotnet test tools/rules-tests`, 2,707 passed, 1 skipped (as before this work), 0 failed. Nine new ones in `RealSkyTests`: the file reads back, broken files are refused, brighter stars are never drawn smaller or dimmer, Sirius, Betelgeuse (orange), Rigel and Polaris are where they should be, the sky is not mirrored, the zodiac lies on the planets' plane, no invented star is brighter than magnitude 6.5, and the picture is the right size.
- The runtime C# compiles against Unity reference assemblies (an older Unity's, so the usual handful of Unity 6 name errors in untouched files are the only ones). This caught one real mistake in the new code, which is fixed.
- Both shaders' vertex and fragment programs pass a shader compiler offline with Unity's include stubbed out.
- Offline previews at the Deck's 1280 x 800 (`python3 tools/sky/preview_sky.py <folder>`).

## Not verified

- That the two shaders compile in Unity and on the Deck's Vulkan driver.
- How the sky looks in the game: brightness against bloom and tone mapping, whether edge smoothing (FXAA) dims the faintest stars, whether the picture's seam or poles show.
- Frame time and memory. The budget line is a guess: 0.1 to 0.4 ms a frame more than the old sky, about 12.5 MB on the graphics card, a one-off 15 to 60 ms when the first run of a session starts.
- That Unity imports the picture at full size as BC7. Project setup now forces this (`SkyTextureImportRules.Ensure`), but that is editor code, which cannot be compiled in the cloud.
- The editor script and the PlayMode side were only read, not compiled.

## Agent review

- **Game designer.** No conflict with the build prompt. Two risks: bright stars could be mistaken for far planets, and showpieces could read as places to fly to. Applied: a star's size and brightness are capped under a far planet's (`StarLook.LargestHalfSize` 4, `BrightestPeak` 4), and `docs/art_direction.md` now says planets outshine stars and showpieces are scenery. Left to Matthew: whether the showpieces should be dimmer (suggested about 60 per cent).
- **Deck performance.** Applied: the star mesh is built once a session and reused (a restart made about 5 MB of garbage before), and the file's cap is 40,000 stars. Noted: the cockpit's small camera feeds stop short of the stars, as they always have, so they show the picture but no point stars. First import of the picture on the Deck may take 20 to 90 seconds, once.
- **Unity reviewer.** No blocker. Applied: the import rule could be left at Unity's defaults if the picture arrived before the editor scripts compiled, so project setup now checks and re-imports; the stars no longer write alpha; the shared-shader reset includes the star shader. Checks handed to Matthew below.

## Checking it on the Deck

1. In a terminal in `~/Orbital-ante`: `git fetch`, then `git checkout claude/playtest`, then `git pull` (after this PR is merged).
2. Open the project in Unity and wait. The first time, a bar saying it is importing `DeepSpace` may sit for up to a minute or two.
3. In the Project window open `Assets/OrbitalAnte/Resources/Sky` and click `DeepSpace`. At the bottom of the Inspector the preview should say **4096x2048** and **BC7**. If it says 2048x1024 or DXT, use the menu `Orbital Ante > Set Up Project` once and look again.
4. Press Play, start a new run and fly up off the pad until the sky goes black.

Then, yes or no:

1. Are the stars round dots of different sizes, with no squares?
2. Can you find the Milky Way as a band with dark lanes in it, and do the stars thicken along it (the band and the stars line up)?
3. Can you find Orion, and is his belt the right way round (Betelgeuse orange at one shoulder, Sirius below and to the left of the belt with north up)?
4. Can you find at least one spiral galaxy and one glowing nebula away from the Milky Way?
5. Looking toward the Sun, do the stars near it fade out?
6. Is every planet you can see still easier to pick out than any star?
7. Is there any seam, smear at the poles, blockiness or banding in the dark sky?
8. With F3 open, does the frame time stay in budget while looking at the thickest part of the Milky Way?

To compare with the old sky: stop Play, click `Assets/OrbitalAnte/Resources/Config/LookConfig`, untick **Real Sky**, press Play again. Tick it again afterwards.

If the Console shows a red line naming `OA_Stars` or `OA_Sky`, send the first one.

## Next

Stage 2 is the planets seen from space (air glowing at the rim, clouds, a night side), with previews before anything is built.
