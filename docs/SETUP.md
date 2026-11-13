# Setup, step by step

Everything here is done once, except "Play" and "Run the tests".

## A. Install Unity (once)

1. Go to https://unity.com/download and install **Unity Hub**.
2. Open Unity Hub and sign in with a free Unity account. The Personal licence is fine.
3. Click **Installs** on the left, then **Install Editor**.
4. Pick the **Unity 6** version marked **LTS** and click **Install**.
5. On the "Add modules" screen, tick both **Linux Build Support (IL2CPP)** and **Linux Build Support (Mono)**. These make the Steam Deck build later.
6. Click **Continue** / **Install** and wait for installation to finish.

## B. Get the project (once)

1. If the repo is not already on your computer, install **GitHub Desktop** from https://desktop.github.com before cloning.
2. In GitHub Desktop, choose **File > Clone repository**.
3. Pick `hmblasko/Orbital-ante`, choose a folder, then click **Clone**.
4. To try a milestone before it is merged, click **Current Branch** at the top. Pick the branch named in the pull request. Milestones 1 and 2 are both on `claude/unity-milestone-1`.

## C. Open it in Unity (first time takes a few minutes)

1. In Unity Hub, click **Projects**, then **Add** (or **Add project from disk**).
2. Choose the **`unity`** folder inside the repo and confirm. Choose that folder, not the repo folder itself.
3. Click the project in the list to open it.
4. If Hub says the editor version is missing or different, choose your installed **Unity 6 LTS** version and click **Open with ...** / **Continue**. This is expected: the project was written without knowing your exact version number.
5. Wait while Unity imports. The first import can take several minutes.
6. If a box about the **new Input System** asks to enable the backends or restart, click **Yes** / **Restart now**. Unity closes and reopens by itself. This happens once.
7. When the editor is open, look at the bottom-left status bar or open the **Console** tab with **Window > General > Console**.
8. Look for `Orbital Ante: project is set up.` If it is missing, use **Orbital Ante > Set Up Project** in the menu at the very top.

The setup tool creates the render pipeline settings, the tunable config files and the test scene for you. Nothing needs wiring by hand.

## D. Play

1. Choose **Orbital Ante > Open Test Arena**. You can also double-click `Assets/Scenes/TestArena` in the **Project** window.
2. Press the **Play** button: the triangle at the top centre.
3. Click **Fly the solar system** for the newest work, or **Combat test arena**.
4. If the mouse is not captured, click once inside the Game view.
5. Press **Play** again to stop.

For a Steam Deck-shaped view:

1. In the **Game** tab, click the resolution dropdown that says "Free Aspect".
2. Click **+**.
3. Add a fixed resolution of 1280 x 800.

### Flying controls (solar system)

| Action | Keyboard and mouse | Controller |
|---|---|---|
| Thrust forward, back, sideways | W A S D | Left stick |
| Steer | Mouse | Right stick |
| Thrust up / down | Space / Left Ctrl | Right trigger / Left trigger |
| Roll left / right | Q / E | Left bumper / Right bumper |
| Boost (hold) | Left Shift | Click left stick |
| Match velocity with what you aim at (hold) | F | A / Cross |
| Map | M | D-pad up |
| Pause menu | Esc | Start / Menu |

All but the last row can be changed in **Pause > Controls** while flying. The page shows whichever set you are using.

How to fly it, in five lines:

1. You start in orbit round Earth. Nothing slows you down in space: to stop, thrust the other way.
2. Point at a planet's name. Its label gets [ brackets ], and the bottom-left readout shows how fast you are closing on it.
3. Thrust toward it; hold Boost for long trips. The green ring on screen shows which way you are really drifting.
4. When the screen says **BRAKE NOW**, keep aiming at the planet and hold **F**. That matches your speed with it, however fast it is moving.
5. Ease down with small pushes. Hold **F** near the ground to hover. **M** opens the live map; in it, W / S zoom and F changes what it is centred on.

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

The arena starts with the enemies **holding fire**, so you can look around:

1. Press **H** (or D-pad down) to start the fight.
2. Press it again to calm things down.

## E. Run the automated tests

1. Open **Window > General > Test Runner**.
2. Click the **EditMode** tab, then **Run All**. These check the dismemberment rules, gun rules, seeded random numbers, save file, frame-time sums, and the solar system's orbits, gravity and point-of-view sums. They also check placed rock and tree shapes and the planet rules: formation index, ground, caves, tiles, air, cloud decks, seas and weather. There are 733 tests; they take a few seconds.
3. Click the **PlayMode** tab, then **Run All**. These build a real mannequin and shoot bits off it. They then check the gib pool, blood, splats, hitstop, weapons, disarming, saving, content database, solar system and planets: ground appearing, landing on it, gas giants, Neptune's sea and storms. There are 63 tests. The editor will flicker into Play mode for a few minutes.
4. Look at the results. Green ticks mean pass.
5. If anything is red, click it, copy the text at the bottom, and send it to me.

## F. Tuning without code

### Find the general settings

1. In the **Project** window, open `Assets/OrbitalAnte/Resources/Config`.
2. Click the config for the setting you want:

   - **MannequinConfig**: hit points per body part, movement speeds for each damage state, how hard gibs launch, colours.
   - **GoreConfig**: everything from Milestone 2. Most loose chunks at once, how long they last, bounciness, blood amount and colour, splat size and count, hitstop lengths, camera shake strength, headshot slow-mo, sound volumes.
   - **PlayerConfig**: move speed, jump, mouse and stick sensitivity, field of view, health and pickup reach.
   - **PerformanceBudget**: the Steam Deck frame-time targets the top-left readout checks against, and the V Sync switch. Explained in `docs/performance_budget.md`.

3. Change the numbers in the **Inspector**.
4. Press Play. Changes are kept.

### Find gun and enemy settings

1. In the **Project** window, open `Assets/OrbitalAnte/Resources/Weapons`.
2. Click **Rifle** or **Pistol**. Each holds damage per body part, fire rate, spread, recoil, magazine size, reload time and the one-handed penalties.
3. For how enemies fight, open **MannequinConfig** and find **Combat**. It contains reaction time, aim error, burst length and the one-handed reload toggle.

### Find solar-system and test-craft settings

1. In the **Project** window, open the solar system asset at `Assets/OrbitalAnte/Resources/SolarSystem/SolarSystem`.
2. Open **Bodies** to change any world's radius, surface gravity, orbit distance, day length, tilt and colours. Orbit speed is not listed because it follows from the Sun's gravity and the distance.
3. Open **Asteroid Belt** for the rocks.
4. For the test craft's thrust, boost, turn speeds and starting planet, open **TestCraftConfig** in `Resources/Config`.
5. After changing the solar system, run **Orbital Ante > Check Content**. It tells you if, for example, a moon now orbits inside its planet.

### Find planet settings

1. In the **Project** window, open the asset at `Assets/OrbitalAnte/Resources/Planets/Planets`.
2. Use **Settings** at the top for how fine the ground is (**Finest Cell**, **Split Distance**), how many rocks may exist (**Feature Cap**), how much water holds the ship up (**Buoyancy**), how hard storm wind pushes (**Wind Push**), the lamp, weather amounts, and the **Simple Shaders** box. See "If something goes wrong" for that box.
3. Open **Planets**. There is one entry per body. Each has the heights of its continents, hills and mountains, its colours, sea, sky and haze colours, how far you can see, weather, and the list of **Formations** it has.
4. Find formation ids and their descriptions in `docs/formation_index.md`. To give Mars more hoodoos, find the `hoodoo_field` line under Mars and raise its **Count**.
5. After changing the planet asset, run **Orbital Ante > Check Content**. It tells you if a formation id is misspelt.

### Add a gun

1. Select a gun in `Assets/OrbitalAnte/Resources/Weapons`.
2. Press **Ctrl+D** to duplicate it.
3. Rename the copy. The file name is the gun's id.
4. Change its numbers.
5. Run **Orbital Ante > Check Content**. It tells you in plain words if any value is wrong.

### Saves and seeds

After your first run, the main menu shows **Play seed N again**. The same seed gives the same starting layout.

To find the small, readable save file:

1. Use **Orbital Ante > Show Save Folder**.
2. The folder contains `orbital_ante_save.json`, plus a `.bak` backup. Deleting both resets it.

## G. Blood decals (only if the top-left readout says "splats ... (flat)")

Blood splats are meant to use URP Decal Projectors, which wrap over walls and corners. The setup tool switches these on for you.

If the top-left readout ends in **(flat)** instead of **(projectors)**, the automatic step did not work. The game is using simple flat splats instead and is fully playable like that.

To switch projectors on by hand:

1. Run **Orbital Ante > Set Up Project**. Press Play and check the readout again. If it now says **(projectors)**, stop here.
2. In the **Project** window, open `Assets/Settings` and click **OA_UniversalRenderer**.
3. In the **Inspector**, click **Add Renderer Feature** at the bottom and choose **Decal**.
4. In the **Project** window, open `Assets/OrbitalAnte/Resources/Config` and click **GoreConfig**.
5. In the **Inspector**, tick **Decal Projectors Ready**.
6. Press Play. Tell me the automatic step failed, and paste any yellow "blood decals" line from the Console so I can fix it.

To compare the two looks at any time:

1. Open **GoreConfig**.
2. Set **Decal Mode** to **Mesh Only** or **Projector Only**.

## H. Using a real gun model instead of the placeholder (optional)

The guns are placeholder shapes until you add models. To swap one in:

1. Download a CC0 pack, for example the Quaternius "Ultimate Gun Pack". Unzip it.
2. In Unity's **Project** window, open `Assets/ThirdParty/Weapons`.
3. Drag the model files you want, usually `.fbx`, from your file explorer into that folder. Wait for the import.
4. In the **Project** window, open `Assets/OrbitalAnte/Resources/Weapons` and click **Rifle** (or **Pistol**).
5. In the **Inspector**, find **Model Prefab** under **Look**. Drag the imported model from `Assets/ThirdParty/Weapons` onto that box.
6. Set **Model Length** to the real length in metres: about `0.87` for the rifle, `0.2` for the pistol.
7. Press Play. If the gun points sideways, up or backwards, stop, change **Model Rotation** by 90 on one axis (try Y first), and press Play again.
8. The hand, muzzle and magazine positions are estimated from the model's size. If the muzzle flash or hands are in the wrong place, tell me which model you used and I will set them exactly.
9. Add a line for the pack to `docs/asset_licence_ledger.md` (name, author, licence, link), or tell me and I will.

## If something goes wrong

### Red errors in the Console and Play is greyed out or refuses to start

1. Copy the first red line and send it to me.

One typo-level error stops everything, and I could not compile inside Unity itself.

### Everything is bright pink

1. Run **Orbital Ante > Set Up Project** once more.
2. Press Play.

### Planets (and only planets) are bright pink, or the ground is black

One of the game's own planet shaders did not compile on your machine.

1. Copy the red shader error from the Console and send it to me.
2. To keep playing meanwhile, open `Assets/OrbitalAnte/Resources/Planets` in the **Project** window.
3. Click **Planets**.
4. In the **Inspector**, tick **Settings > Simple Shaders**.
5. Press Play again. Skies and far-off planets look plainer, but everything works. The debug readout (F3) then says SIMPLE SHADERS.

### Keyboard and controller do nothing

1. Open **Edit > Project Settings > Player > Other Settings > Active Input Handling**.
2. Choose **Input System Package (New)**.
3. Let Unity restart.

### Package errors on first open

For an error such as "cannot find package version":

1. Open **Window > Package Manager**.
2. Select the package named in the error.
3. Click **Update** or **Install**.
4. Tell me which package it was so I can fix the version number.
