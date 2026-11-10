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
3. To try a milestone before it is merged: click **Current Branch** at the top, and pick the branch named in the pull request (Milestones 1 and 2 are both on `claude/unity-milestone-1`).

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
3. Click **Enter the test arena**. Click once inside the Game view if the mouse is not captured.
4. Press **Play** again to stop.

Tip: for a Steam Deck-shaped view, in the **Game** tab click the resolution dropdown (it says "Free Aspect"), click **+**, and add a fixed resolution of 1280 x 800.

### Controls

| Action | Keyboard and mouse | Controller |
|---|---|---|
| Move | W A S D | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | A / Cross |
| Sprint | Left Shift | Click left stick |
| Fire | Left mouse button | Right trigger |
| Respawn dummies | T | D-pad up |
| Pause menu | Esc | Start / Menu |
| Debug readout on/off | F3 | (none) |

Everything in the first six rows can be changed in **Pause > Controls**.

## E. Run the automated tests

1. Menu: **Window > General > Test Runner**.
2. Click the **EditMode** tab, then **Run All**. These check the dismemberment rules (19 tests).
3. Click the **PlayMode** tab, then **Run All**. These build a real mannequin and shoot bits off it, then check the gib pool, blood, splats and hitstop (19 tests). The editor will flicker into Play mode for about 30 seconds.
4. Green ticks mean pass. If anything is red, click it, copy the text at the bottom, and send it to me.

## F. Tuning without code

In the **Project** window open `Assets/OrbitalAnte/Resources/Config`.

- **MannequinConfig**: hit points per body part, movement speeds for each damage state, how hard gibs launch, colours.
- **GoreConfig**: everything from Milestone 2. Most loose chunks at once, how long they last, bounciness, blood amount and colour, splat size and count, hitstop lengths, camera shake strength, headshot slow-mo, sound volumes.
- **PlayerConfig**: move speed, jump, mouse and stick sensitivity, field of view, test rifle damage and fire rate.

Click one, change numbers in the **Inspector**, press Play. Changes are kept.

## G. Blood decals (only if the top-left readout says "splats ... (flat)")

Blood splats are meant to use URP Decal Projectors, which wrap over walls and corners. The setup tool switches these on for you. If the readout at the top left of the screen ends in **(flat)** instead of **(projectors)**, the automatic step did not work and the game is using simple flat splats instead. The game is fully playable like that. To switch projectors on by hand:

1. Menu: **Orbital Ante > Set Up Project**. Press Play and check the readout again. If it now says **(projectors)**, stop here.
2. In the **Project** window open `Assets/Settings` and click **OA_UniversalRenderer**.
3. In the **Inspector**, click **Add Renderer Feature** at the bottom and choose **Decal**.
4. In the **Project** window open `Assets/OrbitalAnte/Resources/Config` and click **GoreConfig**.
5. In the **Inspector**, tick **Decal Projectors Ready**.
6. Press Play. Then tell me the automatic step failed, and paste any yellow "blood decals" line from the Console so I can fix it.

To compare the two looks at any time, set **Decal Mode** on GoreConfig to **Mesh Only** or **Projector Only**.

## If something goes wrong

- **Red errors in the Console and Play is greyed out or refuses to start**: copy the first red line and send it to me. One typo-level error stops everything, and I could not compile inside Unity itself.
- **Everything is bright pink**: run **Orbital Ante > Set Up Project** once more, then press Play.
- **Keyboard and controller do nothing**: menu **Edit > Project Settings > Player > Other Settings > Active Input Handling**, choose **Input System Package (New)**, and let Unity restart.
- **Package errors on first open** (something like "cannot find package version"): menu **Window > Package Manager**, select the package named in the error, and click **Update** or **Install**. Then tell me which one so I can fix the version number.
