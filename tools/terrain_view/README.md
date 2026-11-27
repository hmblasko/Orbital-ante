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

## Light and shadow trials

    OA_LIGHT=game OA_PROPS=1 OA_SS=2 dotnet run -c Release -- shot Earth -4 -156 1.9 90 5 before.ppm 1 11 205
    OA_LIGHT=new  OA_PROPS=1 OA_SS=2 dotnet run -c Release -- shot Earth -4 -156 1.9 90 5 after.ppm 1 11 205
    dotnet run -c Release -- flat Moon

- `OA_LIGHT=game` lights the ground with a copy of the game's earlier sum (nothing casts a shadow, light wraps
  round); `OA_LIGHT=new` uses the game's real light (`GroundLight`, the same sums as `OA_LightAt`) with cast
  shadows. With neither set the tool's own older, simpler light is used.
- `OA_PROPS=1` stands stand-in shapes in front of the eye (boulders, a figure, a lander) so something casts.
  They are balls and rods, not the game's models.
- `OA_SS=2` works the picture out at twice the size and shrinks it, which smooths it.
- `flat <body>` lists open, level places with hills in view: somewhere to stand.

The shadow's soft edge here comes from four looks toward the Sun; the game's comes from URP's shadow picture, so
edges, reach (70 m in the game) and crispness will differ.
