# Refining the game with Claude Code on your own computer

Cloud sessions have no Unity, so they cannot run the game or its PlayMode tests. Claude Code on your own computer can: it runs Unity in the background, reads what failed, fixes it and runs again. This works well on Opus 5.5 for fixing and tidying, and keeps Fable 5.2 credits for the big jobs (see the end of this page).

## A. Install Claude Code (once)

1. Install **Node.js** (the LTS version) from https://nodejs.org if you do not have it. Accept the defaults.
2. Open a terminal:
   - Windows: press Start, type **PowerShell**, open it.
   - Mac: press Cmd+Space, type **Terminal**, open it.
3. Type `npm install -g @anthropic-ai/claude-code` and press Enter. Wait for it to finish.
4. Type `claude` and press Enter. Follow the sign-in steps in the browser with your Claude account, then type `/exit` to close it.

(If the Claude Desktop app is installed, its **Code** tab can do the same as the terminal. These steps use the terminal because it is the same on every computer.)

## B. Get the version you want to refine (each time)

1. In **GitHub Desktop**, open `Orbital-ante`, click **Fetch origin**, then **Current Branch**, and pick the branch (the newest work is `claude/milestone-7-ship`).
2. Close Unity if it is open.

## C. Start a refining session

1. In GitHub Desktop choose **Repository > Open in Command Prompt** (Mac: **Open in Terminal**). A terminal opens in the repo folder.
2. Type `claude` and press Enter.
3. Type `/model` and press Enter, then choose **Opus 5.5** from the list (or type `/model claude-opus-5-5`).
4. Type this and press Enter:

   `Use the unity-refine skill. Run the checks in Unity, fix whatever fails, and report the real results.`

5. Claude asks before it runs commands or edits files. Choose **Yes** (or "Yes, and don't ask again" for the check script) when it wants to run `tools/check-in-unity`.
6. When it is done it shows the pass/fail counts from `unity/Logs/oa-check/summary.txt`, commits on a `claude/...` branch and offers to open a pull request.

The first run takes longer (Unity imports everything). After that a full check takes a few minutes.

## D. Run the checks yourself (no Claude needed)

1. Close Unity.
2. Windows: in File Explorer, open the repo's `tools` folder, right-click `check-in-unity.ps1`, choose **Run with PowerShell**. Mac: in the repo terminal type `bash tools/check-in-unity.sh`.
3. The window ends with `Overall: PASS` or `Overall: FAIL`. The full results are in `unity/Logs/oa-check/summary.txt`.
4. If it says it cannot find Unity, tell it where Unity is: in Unity Hub click **Installs**, click the cog next to Unity 6, **Show in Explorer** (or Finder), and copy the path of `Unity.exe`. Then run `$env:UNITY_EDITOR="C:\path\to\Unity.exe"` in PowerShell before step 2 (Mac: `export UNITY_EDITOR="/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity"`).

Sending `summary.txt` (or pasting it) to the project thread lets the cloud session work from real results too.

## E. Let the project thread drive your computer (optional)

Instead of typing in a terminal, you can let the project thread run Claude Code on your computer for you:

1. In the repo terminal (step C1) type `claude remote-control` and press Enter, and leave the window open.
2. Tell the project thread you have done it. It can then start a session on your machine that runs the checks and fixes, and reports back in the thread.

## F. Run the quality review (Builder and Critic)

This takes real pictures of the game in Unity, has an independent critic score them against the fixed rubric, and has a builder fix what the critic found. The critic and the builder run on Fable 5.2 by themselves; the session that runs them can be Opus 5.5.

1. Do section B so you are on the branch you want reviewed (normally `claude/playtest`). Close Unity.
2. Open a terminal in the repo folder (step C1; on the Steam Deck, in Desktop Mode open **Konsole** and type `cd ~/Orbital-ante`).
3. Type `claude` and press Enter. Type `/model claude-opus-5-5` and press Enter.
4. Type this and press Enter (change `ship` to what you want looked at: `mannequin`, `weapons`, `combat-arena`, `planets`, `planet-surface`, `menus`, or leave the name out for everything):

   `Use the quality-gate skill. Run a level 2 review of the ship and tell me the real verdict.`

5. Choose **Yes** when it asks to run `tools/quality-capture`. Unity runs without its window for several minutes; the screen may flicker. Leave the machine alone until it finishes.
6. It then starts the critic. When that is done you get, for each thing reviewed: PASS, FAIL or BLOCKED, the score, and the main problems in plain words. The pictures are in `Verification/Reports/` in a folder named with the date.
7. If the verdict is FAIL and you want it improved, type: `Hand the issues to the builder and review again.` After three failed tries on one thing it stops and asks you.
8. The first time, expect the capture itself to need a fix or two (it has never run in Unity before). Claude fixes the capture code and tries again; that is normal and it will say so.

To only take the pictures, with no Claude: `bash tools/quality-capture.sh` (Windows: right-click `tools\quality-capture.ps1`, **Run with PowerShell**). Then in GitHub Desktop commit the new files under `Verification/Reports` and push, and the project thread can review them from the cloud.

## What stays with Fable 5.2

These need the strongest judgement, so they wait for Fable 5.2 credits:
1. All models and assets, built in Blender (ship, launch pad, NASA-style hardware, characters). Code-built shapes are stand-ins until then.
2. Feel passes: the launch (smoke, debris, light, shake, heat shimmer, sound), flight handling and crash physics (Milestone 10), jetpack feel (Milestone 11).
3. The cockpit dash design iterations (Milestone 9).
4. Design decisions and the critic review of how things look and play.

Opus 5.5 is a good fit for: running the checks and fixing what fails, wiring systems whose design is already set, writing tests, performance tidying, and keeping docs and setup steps right.
