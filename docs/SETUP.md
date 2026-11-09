# Setup: from nothing to playing the test arena

About 30-45 minutes the first time (mostly downloads).

## A. Install the tools (once)
1. Go to **unity.com/download**, download **Unity Hub**, install it and open it.
2. Sign in (or create a free Unity account). When asked about a licence, choose **Personal** (free).
3. In Unity Hub click **Installs** (left side) → **Install Editor**.
4. Under **Unity 6** pick the newest version labelled **LTS** → **Install**.
5. On the modules screen tick:
   - **Linux Build Support (Mono)** (for the Steam Deck build later)
   - **Microsoft Visual Studio Community** (Windows) so you can open scripts
   Then click **Install** / **Continue** and wait.
6. Go to **desktop.github.com**, install **GitHub Desktop**, open it and sign in with your GitHub account (hmblasko).

## B. Get the project
1. In GitHub Desktop: **File → Clone repository → GitHub.com** tab.
2. Pick **hmblasko/Orbital-ante**. Under *Local path* choose a folder you can find again (for example `Documents\Unity\Orbital-ante`). Click **Clone**.
3. At the top, click **Current branch** and choose the branch named in the pull request
   (for Milestones 0-4: `unity/m0-m1-arena-mannequin`). Once that pull request is merged you can stay on `main`.

## C. Open it in Unity
1. In Unity Hub click **Projects** → the arrow next to **Add** → **Add project from disk**.
2. Select the cloned **Orbital-ante** folder (the one that contains `Assets`, `Packages` and `ProjectSettings`) → **Add Project**.
3. If Hub shows a warning next to the project about the editor version, click the version number and pick
   the Unity 6 LTS you installed. If a box says the project will be upgraded, click **Continue**.
4. Click the project to open it. The first open takes several minutes while Unity downloads packages.
5. If Unity asks **"Enable the new Input System backends? Unity will restart"**, click **Yes**.
6. When the **Orbital Ante setup** box appears, click **Run setup**.
   (If it doesn't appear: top menu bar → **Orbital Ante → Run Project Setup**.)
7. If it asks to restart, click **Restart**.

## D. Play
1. Top menu → **Orbital Ante → Open Test Arena**.
2. Click the **Game** tab. In the dropdown that says *Free Aspect*, click **+**, set *Type* to **Fixed Resolution**,
   Width **1280**, Height **800**, label it "Steam Deck", click **OK** and select it.
3. Press the **Play** button (triangle, top middle). Click inside the Game view so it captures the mouse.
4. Press **Esc** (or **Start** on a controller) for the pause menu. Press **Play** again to stop.

## E. Run the automated tests
1. Top menu → **Window → General → Test Runner**.
2. Click the **EditMode** tab → **Run All**. Every test should get a green tick. Send me any red ones.

## F. Save the generated files back to GitHub (important, once)
Setup creates the scenes, the render pipeline asset and the tunable config files. Commit them so we stay in sync:
1. Switch to GitHub Desktop. The left panel lists changed files.
2. In *Summary* (bottom left) type `Run project setup`, click **Commit to unity/m0-m1-arena-mannequin**.
3. Click **Push origin** (top).

## If something goes wrong
- **Red errors:** top menu → **Window → General → Console**. Click an error, copy the text at the bottom and send it to me.
- **Everything is pink/magenta:** the render pipeline didn't get set. Run **Orbital Ante → Run Project Setup** again.
- **Controller does nothing:** make sure the Game view has focus (click it), and that Unity restarted after the Input System prompt.
- **Mouse is stuck to the window:** press Esc to open the pause menu, which frees the cursor.
