# Setup, step by step

Everything here is done once, except "Play" and "Run the tests".

## A. Install Unity (once)

1. Go to https://unity.com/download and install **Unity Hub**. Open it and sign in with a free Unity account. The Personal licence is fine.
2. In Unity Hub click **Installs** (left side), then **Install Editor**.
3. Pick the **Unity 6** version marked **LTS** and click **Install**.
4. On the "Add modules" screen tick **Linux Build Support (IL2CPP)** and **Linux Build Support (Mono)**. These make the Steam Deck build later. Click **Continue** / **Install** and wait.

## B. Get the project (once)

1. Install **GitHub Desktop** from https://desktop.github.com if you do not already have the repo on your computer.
2. In GitHub Desktop choose **File > Clone repository**, pick `hmblasko/Orbital-ante`, choose a folder, click **Clone**.
3. To try a milestone before it is merged: click **Current Branch** at the top, and pick the branch named in the pull request (Milestone 7, the ship, is on `claude/milestone-7-ship`).

No GitHub Desktop? Download a zip instead: https://github.com/hmblasko/Orbital-ante/archive/refs/heads/claude/milestone-7-ship.zip . Right-click it, **Extract All**, and use the extracted folder in step C. (A zip does not update itself: download it again for newer work.)

## C. Open it in Unity (first time takes a few minutes)

1. In Unity Hub click **Projects**, then **Add** (or **Add project from disk**).
2. Choose the **`unity`** folder inside the repo (not the repo folder itself) and confirm.
3. Click the project in the list to open it.
4. If Hub says the editor version is missing or different, choose your installed **Unity 6 LTS** version and click **Open with ...** / **Continue**. This is expected: the project was written without knowing your exact version number.
5. Wait while Unity imports. The first import can take several minutes.
6. If a box appears about the **new Input System** asking to enable the backends or restart, click **Yes** / **Restart now**. Unity closes and reopens by itself. This happens once.
7. When the editor is open, look at the bottom-left status bar or the **Console** tab (menu: **Window > General > Console**). You should see: `Orbital Ante: project is set up.`
   - If you do not, use the menu at the very top: **Orbital Ante > Set Up Project**.

The setup tool creates the render pipeline settings, the tunable config files and the test scene for you. Nothing needs wiring by hand.

## D. Play

1. Menu: **Orbital Ante > Open Test Arena** (or double-click `Assets/Scenes/TestArena` in the Project window).
2. Press the **Play** button (the triangle at the top centre).
3. Click **Fly the solar system** (the newest work) or **Combat test arena**. Click once inside the Game view if the mouse is not captured.
4. Press **Play** again to stop.

Tip: for a Steam Deck-shaped view, in the **Game** tab click the resolution dropdown (it says "Free Aspect"), click **+**, and add a fixed resolution of 1280 x 800.

### Flying controls (solar system)

| Action | Keyboard and mouse | Controller |
|---|---|---|
| Thrust forward, back, sideways | W A S D | Left stick |
| Steer | Mouse | Right stick |
| Thrust up / down | Space / Left Ctrl | Right trigger / Left trigger |
| Roll left / right | Q / E | Left bumper / Right bumper |
| Boost (hold) | Left Shift | Click left stick |
| Look down at the camera screen (and back up) | C | D-pad down |
| Next ship camera | V | D-pad right |
| Next dash design (for choosing; all four are in the build) | N | Click right stick |
| Match velocity with what you aim at (hold) | F | A / Cross |
| Map | M | D-pad up |
| Pause menu | Esc | Start / Menu |

All but the last can be changed in **Pause > Controls** while flying (the page shows whichever set you are using).

How to fly it, in five lines:
1. You start buckled into the lander on the launch pad on Earth. **Hold Space** (controller: the "up" button shown in **Pause > Controls**). The engines take about a second to build up, then the ship lifts. Keep holding to climb; add **Boost** to climb hard. Tapping does nothing but cough: hold it. Nothing slows you down in space: to stop, you thrust the other way.
2. Point at a planet's name and its label gets [ brackets ]. The bottom-left readout then shows how fast you are closing on it.
3. Thrust toward it (hold Boost for long trips). The green ring on screen shows which way you are really drifting.
4. When the screen says **BRAKE NOW**, keep aiming at the planet and hold **F**. That matches your speed with it, however fast it is moving.
5. Ease down with small pushes. Hold **F** near the ground to hover. **M** opens the live map; in it, W / S zoom and F changes what it is centred on.

### In and around the ship (solar system)

You start strapped into the pilot's seat. The flying controls above only work while you are buckled in.

| What | Keyboard | Controller |
|---|---|---|
| Unbuckle and stand up | B | Y / Triangle |
| Walk, look, jump | W A S D, mouse, Space | Left stick, right stick, A / Cross |
| Buckle in | Look at the seat from close by, press E | Look at the seat, press Y / Triangle |
| Open or close the belly hatch | G (anywhere near the ship, even in flight), or E on the lever | D-pad left, or Y / Triangle on the lever |
| Ride the lift down | Walk onto the open hatch | The same |
| Ride the lift up | Stand under the blue ring with the hatch open | The same |
| Mend a damaged part | Look at it from close by and hold E | Hold Y / Triangle |
| Shoot (test pistol, on foot only) | Left mouse | Right trigger |

The lift will not lower you unless there is ground within about 4 m below the ship, because there is no jetpack until Milestone 11. Land first.

### On-foot controls (combat arena)

| Action | Keyboard and mouse | Controller |
|---|---|---|
| Move | W A S D | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | A / Cross |
| Sprint | Left Shift | Click left stick |
| Fire | Left mouse button | Right trigger |
| Reload | R | X / Square |
| Pick up / swap weapon | E | Y / Triangle |
| Respawn dummies | T | D-pad up |
| Enemies hold fire on/off | H | D-pad down |
| Pause menu | Esc | Start / Menu |
| Debug readout on/off | F3 | (none) |

Everything except the last two rows can be changed in **Pause > Controls**.

The arena starts with the enemies **holding fire**, so you can look around. Press **H** (or D-pad down) to start the fight, and again to calm things down.

## E. Run the automated tests

1. Menu: **Window > General > Test Runner**.
2. Click the **EditMode** tab, then **Run All**. These check the dismemberment rules, the gun rules, seeded random numbers, the save file, the frame-time sums, and the solar system's orbits, gravity and point-of-view sums, the placed rock and tree shapes, and the planet rules: the formation index, ground, caves, tiles, air, cloud decks, seas and weather, and the ship's rules: walking about the cabin, damage and mending, glass, the hatch and lift, the seat belt, the shapes and the baked light, and the launch: where the pad goes, the engine build-up and the pad's shapes, and the cockpit: fuel and air, the dash readings, the four dash designs, the cameras and the look-down view (1,035 tests; they take a few seconds).
3. Click the **PlayMode** tab, then **Run All**. These build a real mannequin and shoot bits off it, then check the gib pool, blood, splats, hitstop, weapons, disarming, saving, the content database, the solar system and the planets: ground appearing, landing on it, gas giants, Neptune's sea and storms, and the ship: built whole, baked light fits, the seat, damage, glass, the hatch, shots fired inside the cabin and the map, and the launch: resting on the pad, the build-up, smoke and the service arm, and the cockpit: the dash and its cameras, an empty tank, and looking down at the screen (81 tests). The editor will flicker into Play mode for a few minutes.
4. Green ticks mean pass. If anything is red, click it, copy the text at the bottom, and send it to me.
5. Or run everything without opening the editor: close Unity, then right-click `tools/check-in-unity.ps1` > **Run with PowerShell** (Mac: `bash tools/check-in-unity.sh`). It ends with `Overall: PASS` or `FAIL` and saves `unity/Logs/oa-check/summary.txt`, which you can send me. Claude Code on your own computer can run this loop and fix what fails: see `docs/CLAUDE_CODE_LOCAL.md`.

## F. Tuning without code

In the **Project** window open `Assets/OrbitalAnte/Resources/Config`.

- **MannequinConfig**: hit points per body part, movement speeds for each damage state, how hard gibs launch, colours.
- **GoreConfig**: everything from Milestone 2. Most loose chunks at once, how long they last, bounciness, blood amount and colour, splat size and count, hitstop lengths, camera shake strength, headshot slow-mo, sound volumes.
- **PlayerConfig**: move speed, jump, mouse and stick sensitivity, field of view, health and pickup reach.
- **PerformanceBudget**: the Steam Deck frame-time targets the top-left readout checks against, and the V Sync switch. Explained in `docs/performance_budget.md`.

Click one, change numbers in the **Inspector**, press Play. Changes are kept.

Guns have their own folder: `Assets/OrbitalAnte/Resources/Weapons`. **Rifle** and **Pistol** each hold damage per body part, fire rate, spread, recoil, magazine size, reload time and the one-handed penalties. How enemies fight (reaction time, aim error, burst length, the one-handed reload toggle) is under **Combat** on MannequinConfig.

The solar system has its own asset: `Assets/OrbitalAnte/Resources/SolarSystem/SolarSystem`. Open **Bodies** to change any world's radius, surface gravity, orbit distance, day length, tilt and colours, and **Asteroid Belt** for the rocks. Orbit speed is not listed because it follows from the Sun's gravity and the distance. **TestCraftConfig** (in `Resources/Config`) holds the test craft's thrust, boost, turn speeds and which planet you start at. After changing the solar system, run **Orbital Ante > Check Content**: it tells you if, say, a moon now orbits inside its planet.

Planets have their own asset too: `Assets/OrbitalAnte/Resources/Planets/Planets`.

- **Settings** at the top: how fine the ground is (**Finest Cell**, **Split Distance**), how many rocks may exist (**Feature Cap**), how much water holds the ship up (**Buoyancy**), how hard storm wind pushes (**Wind Push**), the lamp, weather amounts, and the **Simple Shaders** box (see "If something goes wrong").
- **Planets**: one entry per body. Each has the heights of its continents, hills and mountains, its colours, its sea, its sky and haze colours, how far you can see, its weather, and the list of **Formations** it has. Formation ids are listed with descriptions in `docs/formation_index.md`. To give Mars more hoodoos, find the `hoodoo_field` line under Mars and raise its **Count**.
- After changing it, run **Orbital Ante > Check Content**. It tells you if a formation id is misspelt.

To add a gun: select one in that folder, press **Ctrl+D** to duplicate it, rename the copy (the file name is the gun's id), and change its numbers. Then menu **Orbital Ante > Check Content** tells you in plain words if any value is wrong.

The ship has its own asset: `Assets/OrbitalAnte/Resources/Config/ShipConfig`. It holds the ship's weight, how much damage each part takes, how fast repairs go, walking speed in the cabin, the seat belt and hatch timings, and a **Test Pistol** tick box (untick it to walk about unarmed).

### Saves and seeds

- After your first run the main menu shows **Play seed N again**. The same seed gives the same starting layout.
- The save file is small and readable. Menu **Orbital Ante > Show Save Folder** opens where it lives (`orbital_ante_save.json`, plus a `.bak` backup). Deleting both resets it.

## G. Blood decals (only if the top-left readout says "splats ... (flat)")

Blood splats are meant to use URP Decal Projectors, which wrap over walls and corners. The setup tool switches these on for you. If the readout at the top left of the screen ends in **(flat)** instead of **(projectors)**, the automatic step did not work and the game is using simple flat splats instead. The game is fully playable like that. To switch projectors on by hand:

1. Menu: **Orbital Ante > Set Up Project**. Press Play and check the readout again. If it now says **(projectors)**, stop here.
2. In the **Project** window open `Assets/Settings` and click **OA_UniversalRenderer**.
3. In the **Inspector**, click **Add Renderer Feature** at the bottom and choose **Decal**.
4. In the **Project** window open `Assets/OrbitalAnte/Resources/Config` and click **GoreConfig**.
5. In the **Inspector**, tick **Decal Projectors Ready**.
6. Press Play. Then tell me the automatic step failed, and paste any yellow "blood decals" line from the Console so I can fix it.

To compare the two looks at any time, set **Decal Mode** on GoreConfig to **Mesh Only** or **Projector Only**.

## H. Using a real gun model instead of the placeholder (optional)

The guns are placeholder shapes until you add models. To swap one in:

1. Download a CC0 pack, for example the Quaternius "Ultimate Gun Pack". Unzip it.
2. In Unity's **Project** window, open `Assets/ThirdParty/Weapons`.
3. Drag the model files you want (usually `.fbx`) from your file explorer into that folder. Wait for the import.
4. In the **Project** window open `Assets/OrbitalAnte/Resources/Weapons` and click **Rifle** (or **Pistol**).
5. In the **Inspector**, find **Model Prefab** under **Look**. Drag the imported model from `Assets/ThirdParty/Weapons` onto that box.
6. Set **Model Length** to the real length in metres (about `0.87` for the rifle, `0.2` for the pistol).
7. Press Play. If the gun points sideways, up or backwards, stop, change **Model Rotation** by 90 on one axis (try Y first), and press Play again.
8. The hand, muzzle and magazine positions are estimated from the model's size. If the muzzle flash or the hands are in the wrong place, tell me which model you used and I will set them exactly.
9. Add a line for the pack to `docs/asset_licence_ledger.md` (name, author, licence, link), or tell me and I will.

## If something goes wrong

- **Red errors in the Console and Play is greyed out or refuses to start**: copy the first red line and send it to me. One typo-level error stops everything, and I could not compile inside Unity itself.
- **Everything is bright pink**: run **Orbital Ante > Set Up Project** once more, then press Play.
- **Planets (and only planets) are bright pink, or the ground is black**: one of the game's own planet shaders did not compile on your machine. Please copy the red shader error from the Console and send it to me. To keep playing meanwhile: in the Project window open `Assets/OrbitalAnte/Resources/Planets`, click **Planets**, and in the Inspector tick **Settings > Simple Shaders**. Press Play again. Skies and far-off planets look plainer, but everything works. The debug readout (F3) then says SIMPLE SHADERS.
- **Keyboard and controller do nothing**: menu **Edit > Project Settings > Player > Other Settings > Active Input Handling**, choose **Input System Package (New)**, and let Unity restart.
- **Package errors on first open** (something like "cannot find package version"): menu **Window > Package Manager**, select the package named in the error, and click **Update** or **Install**. Then tell me which one so I can fix the version number.
