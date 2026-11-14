# Pictures of the ground without the engine

`Program.cs` is a small console program that draws a world's ground from the game's own terrain rules
(`PlanetTerrain`, the same code the game runs), so the shape and colour of the land can be judged where there
is no Unity Editor. It also measures what the ground costs to work out.

    cd tools/terrain_view
    dotnet run -c Release -- shot Earth 28 90 20 90 -2 earth.ppm
    dotnet run -c Release -- globe Mars 30 30 mars.ppm
    dotnet run -c Release -- land Earth
    dotnet run -c Release -- stats Mars
    dotnet run -c Release -- cost
    dotnet run -c Release -- bench

- `shot <body> <latitude> <longitude> <metres above the ground> <heading> <pitch> <out.ppm> [seed] [sun height] [sun bearing]`:
  a view from a place. Heading is in degrees from north; pitch is measured from the horizon (these worlds are
  small, so the horizon lies well below level).
- `globe <body> <latitude> <longitude> <out.ppm> [seed]`: the whole body from space.
- `land <body>`: a map in letters of where the sea, lowland, hills and high ground are, for choosing a place.
- `stats <body>`: how high the ground goes, how much is level enough to land on and how much is cliff.
- `cost`: what one height question and one colour question cost, the quickest of five goes.
- `bench`: how long whole ground tiles take.

Pictures are written as `.ppm`; most picture programs open them (`python3 -c "from PIL import Image; Image.open('earth.ppm').save('earth.png')"`).

## What the pictures are, and are not

They are **not Unity screenshots**. The land's shape and its colours are the game's own rules, and the fine
relief the ground shader adds near the eye (grit, stones, hummocks) is copied sum for sum from `OA_Ground` in
`OA_Terrain.shader` (set `OA_RELIEF=0` to leave it out). The light is simpler than the game's (a plain sun, sky
light, a cast shadow and haze), there is no bloom or tone mapping, colour is worked out at every dot where the
game works it out at every corner of its ground tiles, and trees, rocks, water waves, bases and weather are not
drawn. Use them to compare one version of the ground with another, not to promise how the game looks.

If `OA_Ground` in the shader changes, change `Ground` in `Program.cs` to match.
