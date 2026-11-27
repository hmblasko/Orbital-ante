# The sky bake

Makes the two files the game's real sky is read from (Matthew, 26 Nov 2026: space should "look more like real space and have interesting galaxy structures"; he chose "Real sky plus showpieces"):

- `unity/Assets/OrbitalAnte/Resources/Sky/DeepSpace.png`: the picture wrapped round the sky behind the stars (4096 x 2048).
- `unity/Assets/OrbitalAnte/Resources/Sky/Stars.bytes`: the stars the game draws as points.

```
pip install numpy scipy pillow
python3 milkyway.py 2048        # once, about 10 minutes: the Milky Way, kept in cache/ (not committed)
python3 bake_sky.py             # about 1 minute: writes both game files
python3 preview_sky.py out      # pictures of the result at the Deck's screen size
```

The bake gives the same files every time. Never edit the two game files by hand: change the scripts and run them again.

## What is real, modelled and invented

| | |
|---|---|
| Real | The 9,096 stars of the Bright Star Catalogue (`bsc5_stars.csv`) at their true places, brightness and colour. The places and sizes of the Large and Small Magellanic Clouds, Andromeda, Triangulum and nine bright nebulae (`REAL` in `bake_sky.py`). Where the Milky Way lies, and its tilt against the planets' plane. |
| Modelled | The Milky Way's light and dust: `milkyway.py` marches a ray for every dot of the sky from the Sun's place through a model galaxy (old disc, young blue arms, yellow bar and bulge, clumpy dust that dims blue most). It has the real one's character, not its exact clouds. The 18,000 stars past the catalogue's reach (magnitude 6.6 to 8.2) and the 70,000 fainter dots in the picture: scattered by a seed, thicker where the Milky Way is brighter. |
| Invented, on purpose | `SHOWPIECES` in `bake_sky.py`: a grand spiral galaxy, an edge-on galaxy, a tilted barred spiral, a large glowing nebula, a blue dust nebula, and a faint field of far galaxies. None is in the real sky. They sit away from the Milky Way so they stand against black. To add, move or remove one, edit that list (galactic longitude, latitude, radius in degrees, painter). |

## The game's sky frame

The game's Y is the north pole of the planets' plane (the ecliptic) and X points at the March equinox, so the planets move through the constellations they really move through. `OA_Sky.shader` looks the picture up with `u = atan2(z, x) / 2pi + 0.5`, `v = acos(y) / pi`, and `v = 0` is the bottom row of the PNG. Unity is left-handed, so the bake swaps the sky's Y and Z; the rule test `TheSkyIsNotMirrored_SiriusIsLeftOfOrionWithNorthUp` fails if a change ever flips the sky.

## Star catalogue

`bsc5_stars.csv` is the Yale Bright Star Catalogue, 5th revised edition (Hoffleit and Warren 1991), public domain, reduced to five columns (HR number, right ascension and declination in degrees for J2000, visual magnitude, B-V colour) from the JSON copy at github.com/frostoven/BSC5P-JSON. Fourteen of its 9,110 entries are not stars (novae and clusters with no magnitude) and are left out.

## Previews are not screenshots

`preview_sky.py` reads the same two files as the game and keeps a copy of `StarLook`'s sums (in `StarCatalogue.cs`); if those change, change the copy. Its bloom and tone curve only imitate the game's. Its pictures are for judging the bake, never evidence of how the game looks or runs.
