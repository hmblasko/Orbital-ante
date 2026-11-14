# Baking the cabin's light

`Program.cs` is a small console program. It follows the light from the lander cabin's lamps round the room
(the game's own `LightBake`) and writes the two files the game ships with:

- `unity/Assets/OrbitalAnte/Resources/Ship/LanderBake.bytes`: a colour for every corner of the cabin.
- `unity/Assets/OrbitalAnte/Resources/Models/LanderCabin.bytes`: the body of the cockpit console from the Console model, lit by the same lamps.

Run it again after changing any cabin shape, colour or lamp (`ShipProps`, `ShipShell`, `ShipAssembly`), and after
rebuilding the Console model (`tools/blender/build_console.py`): always bake after the last model build, because
Blender does not place every corner identically twice. Rule tests fail when either file is stale
(`BakedLightFile_FitsTheCabin`, `CabinFittingsFile_IsTheConsole_WithTheCabinsLightBakedIn`).

## How to run it

It needs a project file that compiles the game's runtime scripts outside Unity (see "Verifying without the
editor" in `CLAUDE.md`). With such a project referenced:

    dotnet run -c Release -- <path to this repository> --paths 768

About 25 seconds on four cores. Add `--dump <folder>` to also write the cabin with its baked colours, then make
pictures of it:

    blender -b -P tools/blender/preview_cabin.py -- <folder> <output folder>

The pictures show the stored colours with no extra lighting. They are not Unity screenshots: the game adds bloom,
the glass, the dash screen and the view out of the windows.
