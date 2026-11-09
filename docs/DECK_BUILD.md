# Making a Steam Deck build and running it

I have not been able to run these steps myself (no Unity or Deck on the cloud machine). If a step does not
match what you see, tell me what was on screen.

## A. One-time: Linux build support
1. Close Unity.
2. Open **Unity Hub** > **Installs**.
3. Click the **cog** next to your Unity 6 version > **Add modules**.
4. Tick **Linux Build Support (Mono)** > **Install**. Wait for it to finish.

## B. Build
1. Open the project in Unity.
2. Top menu > **Orbital Ante > Run Project Setup** (safe to run again; it only adds what is missing).
3. Top menu > **Orbital Ante > Build > Steam Deck (playtest, with profiler)**.
4. Wait. When it finishes a box shows the folder and a file window opens on it. The build is the whole
   folder `Builds/SteamDeck` inside the project (it is not uploaded to GitHub).

## C. Copy it to the Deck
The simplest way is a USB stick.
1. Copy the whole **SteamDeck** folder onto a USB stick.
2. On the Deck: hold the **power button** > **Switch to Desktop**.
3. Plug in the USB stick (through a USB-C hub or dock). Open the **Dolphin** file manager (folder icon).
4. Drag the **SteamDeck** folder from the stick into **Home > Documents**.
5. Open the folder, right-click **OrbitalAnte.x86_64** > **Properties** > **Permissions** tab >
   tick **Is executable** > **OK**.

## D. Add it to Steam and play
1. Still in Desktop mode, open **Steam**.
2. Bottom left: **Add a Game > Add a Non-Steam Game... > Browse**.
3. Go to **Documents/SteamDeck**, set the file type box to **All Files**, pick **OrbitalAnte.x86_64** >
   **Open** > **Add Selected Programs**.
4. Double-click **Return to Gaming Mode** on the desktop.
5. In your library open the **Non-Steam** tab and start **OrbitalAnte.x86_64**.

## E. What to check
1. Press **D-pad down** to show the frame-time overlay.
2. Go through the checklist in the newest file in `docs/milestones/` and write the overlay numbers into it.
3. If something is slow, the log has a line starting `[OA] Over the frame budget`. The log is at
   `~/.config/unity3d/Orbital Ante/Orbital Ante/Player.log` (in Dolphin turn on **Show Hidden Files** in the
   top-right menu to see `.config`). Send me that file.
