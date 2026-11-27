# The far planets' painted pictures

Makes the pictures the planets wear from space that do not come from their own ground (Matthew, 26 Nov 2026, "Build all as shown"). They are read from `unity/Assets/OrbitalAnte/Resources/Planets`:

| File | What it is |
|---|---|
| `JupiterTops.png`, `SaturnTops.png` (2048 x 1024), `UranusTops.png`, `NeptuneTops.png` (1024 x 512) | A gas giant's cloud tops: belts that slide past each other and curl at their edges, storms. |
| `EarthClouds.png` (2048 x 1024, with alpha) | Earth's weather. Alpha is how much cloud. |
| `MarsClouds.png` (1024 x 512, with alpha) | Thin wisps of ice cloud. |
| `VenusClouds.png` (1024 x 512) | An unbroken deck of swept cloud: Venus's ground cannot be seen from space. |
| `SaturnRings.png` (1024 x 4, with alpha) | The rings from inner edge (left) to outer. Colour is brightness; alpha is how much they hide. |

```
pip install numpy scipy pillow
python3 bake_planets.py                       # about a minute: writes every file above
```

The bake gives the same files every time. Never edit them by hand: change the script and run it again. Nothing is a photograph or anyone else's picture: every one is made from noise by the sums in `bake_planets.py`, shaped after what the real planets look like (it borrows the noise in `tools/sky/skylib.py`). The planets are the same every run, so their pictures are too; the rocky worlds' ground is still painted per seed by the game (`FarMap`).

Which body wears which picture, and how thick its air and clouds show, is `OrbitLook.cs`. What draws them: `OA_Body` (the far ball), `OA_Air`, `OA_Clouds`, `OA_Rings`. Import settings are set by `PlanetTextureImportRules` (an ordinary file each, not LFS).

## Previews

```
cd ../terrain_view
for b in Earth Mars Moon Venus; do dotnet run -c Release -- map $b 1024 out/map_$b.ppm 1 out/h_$b.ppm; done
cd ../planet_pictures
python3 preview_planets.py ../terrain_view/out previews
```

`preview_planets.py` draws each world as it was drawn before (left) and with the realistic look (right). It keeps a copy of the shaders' sums and of `OrbitLook`'s numbers; if those change, change the copy. Its pictures are **not Unity screenshots**: they are for judging the bake and the numbers, never evidence of how the game looks or runs. Its night lights are scattered at random; the game puts them where the bases are.
