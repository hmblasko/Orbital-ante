# Envelope view (offline)

Draws the prize envelope with no Unity, from the same plain C# the game uses (`Scripts/Runtime/Items/EnvelopeArt.cs` paints the pieces, `EnvelopeScene.cs` says where everything goes at each moment).

```
dotnet run -c Release -- pieces <pieces folder>                       # every piece as a PNG, with timings
dotnet run -c Release -- show <show folder> reveal Legendary 30 10.5 HelmetCrown
dotnet run -c Release -- show <show folder> reveal Celestial 30 10.5 PaintMidnightFoil Aurora
dotnet run -c Release -- show <show folder> runstart Rare 30 6
python3 compose.py <pieces folder> <show folder> <frames folder>      # frame_0000.png ... at 1280 x 800
ffmpeg -framerate 30 -i <frames folder>/frame_%04d.png -c:v libx264 -pix_fmt yuv420p envelope.mp4
```

`show` writes `frames.json` (the draw list for every frame) and `Card.png`. `compose.py` needs Pillow and draws the words with the game's own B612 font.

These are pictures of the real layout, timing and paint, but they are not Unity screenshots: Unity blends in linear colour and draws its own text, so colours and letter spacing can differ a little.
