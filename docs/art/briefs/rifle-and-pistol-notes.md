# Rifle and test pistol — modeling critique checklist

For Fable, supporting the art lead rather than replacing their design. Source read: `claude/look-pass-1`, commit `45cf8948974623d8d8165a4cf9c0377e2cb619f2` (PR #8; refreshed after the gun commits landed during this review). I inspected the committed Blender previews `rifle_side.jpg`, `rifle_held.jpg`, `pistol_side.jpg` and `guns_sheet.jpg`. Those images are not Unity captures; nothing has been run in Unity or measured on Deck here.

## Intent and constraints

Make tools a suited explorer could hold, repair and accidentally drop: a solid working core, generous hand clearances, softly caught edges and a few deliberate repairs. The rifle's role is a recognizable curved-magazine automatic rifle with an original space-tool construction. Recognition comes from mass and function, never a reproduction of a real receiver, trademarked profile, logos or another game's detailing. The pistol is the small test sidearm, not a new permanent second-gun slot: the final loadout remains one gun plus melee, with the rope tool exempt.

Use the stated Blender Python → vertex-colour mesh → `.bytes` pipeline. No textures, UVs, decals, normal maps, photographic grain or shader trick is required by this brief. Colour boundaries, wear and seams must survive export as vertices and faces. The refreshed source includes `tools/blender/build_weapons.py`, `oa_blender.py`, `ModelFile` and `ModelLibrary`. The `OAM1` file carries named parts, positions, split normals, RGB8 sRGB colour and named position sockets; the loader creates one renderer per part. Build order is assigned prefab, then model file, then primitive fallback. Do not alter the pipeline or runtime as part of these notes.

## First critique of the committed Blender previews

These observations concern the supplied Blender images only, whose lighting is softer than the game shader and whose crops are not the player camera:

1. **Keep:** the continuous magazine curve, rounded stock heel, handguard-to-receiver size change and clearly separated pistol slide/frame. These are useful primary shapes. The rifle held preview still lacks a glove and a crosshair; it cannot establish player-view fit.
2. **Prioritize originality:** the rifle currently follows a familiar terrestrial rifle profile very closely. Replace the two stacked orange handguard tubes with a single shaped insulating palm shell around the existing support point, alter the stock neck/cheek contour, and reshape the receiver lid as a broad service cover. Keep the curved magazine and barrel function. Do not merely add a science-fiction ornament to a copied construction layout.
3. **Add construction hierarchy:** the large receiver side and pistol slide look smooth and uninterrupted despite the bevels. Use one real inset cover with a broad lip and a few quiet vertex-colour patches. The pistol's dark side “port” looks applied to the surface in the preview; recess a real opening/seat if it is meant to be a mechanism, or make it an explicitly raised service cover. No dark sticker pretending to be depth.
4. **Reduce toy cues:** orange grips, yellow wraps, pale rivets and orange sights compete. Keep one warm grip family; mute the wraps toward ochre and reduce pale fasteners to a few structural locations. Preserve a small sight cue only if it remains below the aim exclusion zone. The broad light pistol slide needs a cool/warm panel variation more than extra surface strips.
5. **Fit the glove before more detail:** the rifle script's grip width is 0.040 m and pistol frame grip 0.030 m with added panels; the guard bottom/front pieces leave a shallow aperture. Compare the posed glove to the clear opening, rather than calling either “space-glove ready” from an empty guard render. The proposed dimensions below deliberately enlarge that interface.
6. **Check the export appearance, not just Cycles:** in these previews the painted wear is subtle; do not respond by turning up automatic curvature wear on every edge. Put a few authored handling patches on top of the existing colour recipe and judge them with the shared look. The report estimates about 7,800 rifle / 3,900 pistol triangles; the lower ceilings below are proposed reduction targets, not the current counts or proof that the existing models are too costly.

## Read the silhouette before adding parts

- Rifle: three masses, in order: compact rounded receiver; lighter, narrower forward barrel/guard; broad butt with a short shoulder neck. Give the curved magazine one continuous sweep with a distinct foot, rather than two boxes making an elbow. Leave daylight between magazine and firing grip.
- Pistol: a stout short upper assembly over a swept, bulb-ended grip; an open glove-sized trigger bow; one forward step at the muzzle. Avoid a cuboid slide balanced on a cuboid handle. Make its muzzle/front cap and rear shoulder visibly different shapes.
- Distinguish them by length, stock and magazine silhouette in black, without coloured panels or glow. Build both sides: a dropped item and an enemy-held weapon expose surfaces hidden in first person.
- Inspect at **1280×800, native scale**, not a zoomed Blender viewport. At first-person size the silhouette, hand placement, muzzle and one mechanism should read first. Permit only 2–3 secondary features in the upper visible quarter.
- At **20 m**, demand class recognition, not readable screws. With the current default vertical FOV of 80°, a broadside 1 m object subtends about 24 px at 800 px height (`800/(2*tan(40°))*1/20`); a 0.25 m pistol is only about 6 px. This is a projection estimate; foreshortening can shrink it further. Test broadside, three-quarter and end-on. A pistol cannot honestly show its fine details there: judge its compact hand extension against the rifle's long stock/barrel and hanging magazine. If confusion persists, enlarge the defining masses before adding detail.

## Proportions and glove fit

These are starting art dimensions in metres, not a request to move existing sockets or change animations. Keep the firing grip centre at the origin. Fit the actual glove proxy first; the proposed clearances must be revised if that proxy is larger.

| Region | Rifle proposal | Test pistol proposal |
|---|---|---|
| Overall length | 0.95–1.05; preserve the current approximately 1 m envelope initially | 0.24–0.28, versus the narrower approximately 0.20 m placeholder upper assembly |
| Main body | 0.075–0.095 wide, 0.09–0.12 high | upper 0.045–0.055 wide, 0.05–0.065 high |
| Firing grip | oval 0.045–0.055 across × 0.060–0.070 fore/aft; 0.12–0.14 usable length | same glove interface, 0.11–0.13 usable length; do not miniaturize the hand |
| Trigger bow | clear opening at least 0.040 high × 0.055 fore/aft; frame 0.009–0.012 thick | same minimum opening; fingers must not merge into a solid block |
| Support surface | palm landing approximately 0.08 wide × 0.13 long, clear of hot barrel | cup the firing glove below/back; no implausible second grip at the muzzle |
| Controls | one 0.022–0.030 lever/paddle and a broad latch, reachable around a glove | one short rear paddle and a broad magazine heel |

Keep about 0.010–0.015 visible clearance around the posed trigger finger, and 0.015 between a wrist cuff and projecting machinery. These are modeling clearances, not ergonomic certification. Show a 1.8 m suited-person scale reference, consistent with `ShipLayout.PersonHeight`. Do not bulk the weapon by widening every part equally: the guard, grip and receiver need clear size changes. Widening the pistol requires a camera/near-plane check; it is not automatically compatible with the present view pose.

## Socket and motion contract (source facts)

`unity/Assets/OrbitalAnte/Scripts/Runtime/Weapons/WeaponModelFactory.cs` defines grip-origin weapon space: **+Z out of the barrel, +Y top, +X right**. The following are both the primitive fallback sockets and the positions exported by the new gun script, in metres in game space. Preserve them for the first integration comparison; any deliberate relocation needs the implementer's explicit animation handoff.

| Socket | Rifle XYZ | Pistol XYZ |
|---|---|---|
| `Grip` | (0, 0, 0) | (0, 0, 0) |
| `SupportHand` | (0, 0.010, 0.370) | (0, −0.030, 0) |
| `Muzzle` | (0, 0.045, 0.690) | (0, 0.062, 0.155) |
| `Magazine` | (0, 0.010, 0.150) | (0, −0.070, −0.022) |

- Bullet/flash origin must sit just outside the bore at the muzzle socket. Do not add a longer cap past that point without coordinating the socket. Keep the bore readable as a recessed dark polygon with a bevelled lip, not a black circle painted on a box.
- Keep the visible magazine as its own renderable child. Reload hides `MagazineMesh`; baking the magazine into the receiver would leave it visible. Give the socket a clean insertion seat and a visible magazine lip; do not leave an infinite black cavity when hidden.
- Prefab path caveat: the factory looks for the **first descendant name containing** `muzzle`, `support` or `magazine`. Avoid decorative objects whose names accidentally win that search. It does not discover a named grip; the grip is recentered from bounds or `WeaponData` settings. Custom socket vectors are bounds-centre offsets, not plain grip-relative coordinates. Do not paste this table into those offsets unchanged. This bounds-centre behavior belongs only to the prefab path. The `.bytes` path uses exact `Muzzle`, `SupportHand`, `Magazine` sockets from `OAM1`, assumes grip-origin coordinates and reparents the exact `Magazine` part with a compensating negative socket translation. The script exports those three sockets; `Grip` is created at zero by the factory. Do not apply prefab recentering to this path.
- `PlayerWeapons` currently rests the rifle at (0.19, −0.21, 0.34) and pistol at (0.17, −0.17, 0.44), relative to the holder. Recoil moves back 0.045 m and rotates −3.5° about X; reload adds (−0.03, −0.09, 0) and up to +22° X/−28° Z. Check the full sweep, not just rest. Magazine is hidden from reload progress 0.22 until 0.70. No new animation is implied by the model.

## Crosshair and frame protection

For screenshot critique, reserve a **96 px radius circle around (640,400)** at 1280×800 as a proposed combat visibility gate: no persistent solid gun, glove, tall sight, cable loop or saturated accent inside it at rest or recoil. A flash may cross briefly but must not erase the target; record that separately. Check reload occlusion as an animation event rather than demanding an empty centre throughout reload. This screen-space gate is a proposal, not existing game code or an iron-sight mode.

Keep the highest rear receiver shoulder and sight low and to the side of the aim ray. Avoid a carry handle or luminous rod rising toward screen centre. Rifle support wrist must not form a diagonal screen-wide bar. Check top edge of the grip and stock against the near plane at maximum kick and during the reload roll. Aim at a small target, a dark moving character, the horizon and the ship's hatch button; the object being acted on must remain identifiable.

## Geometry that earns its cost

- Put broad 0.004–0.008 m bevels on receiver corners and stock edges; pistol 0.002–0.004 m. One or two bevel segments first; use a third only where the first-person curve visibly steps. A bevel must change the outline or catch a light band at actual image size.
- Separate major plane normals, but keep curves sharing vertices along their circumference. The new `oa_blender` exporter carries split loop normals and `ModelLibrary.ToMesh` uses them directly; keep those normal splits intentional. The older procedural ship mesh path recalculates normals, so do not transfer that assumption between paths. Inspect the exported geometry and normals without regenerating assets in this review.
- Spend cross-section sides on the barrel lip, visible grip bulb and upper shoulder; hide faceting on undersides, never across the nearest rounded silhouette. Start cylinders at 12–16 sides, 20–24 only on large close circular ends. Inspect, do not apply one side count everywhere.
- Recess one large service panel by 0.002–0.004 m with an actual rim. Use 0.006–0.010 m broad seams in visible regions. Place 3–5 large captive fasteners at load-bearing corners, not an evenly tiled screw field. Avoid coplanar patch faces and hairline gaps.
- Give a selected seam a slightly uneven course and one replacement panel a different shape. Keep functional alignments straight. Handmade means plausible construction decisions, not randomized vertices, bent bores or wobbly sockets.
- Proposed LOD0 ceilings: rifle **6,000 triangles**, pistol **3,000**, including the detachable magazine; aim below those. Optional world-model targets 1,500/700 and distant 400/200 are future authoring targets, not a claim LOD switching exists. Keep one opaque vertex-colour material and one extra renderer for the magazine where the importer permits. Export counts after triangulation and colour/normal splits; avoid hundreds of independent detail renderers.

## Vertex colour recipe

Hex values are **sRGB paint swatches**, not already-linear floats and not fixed shading. The inspected exporter converts Blender linear attribute values back to sRGB and quantizes RGB8; `ModelLibrary.ToMesh` converts those sRGB values to linear when the project is linear. Preserve that contract and do not preconvert the hex swatches a second time. `oa_blender.paint` currently writes a POINT attribute, so hard colour boundaries need split vertices or separate source objects before export. Review swatches with neutral lighting before the warm look grade. Use geometry for depth and vertex colours for material regions; never prepaint the Sun's direction.

| Role | Rifle | Pistol | Placement |
|---|---|---|---|
| Primary enamel | `#59696B` | `#657879` | approximately 55–65% of visible body |
| Warm grip/stock composite | `#9A6243` | `#785343` | 20–25%; broad uninterrupted grip faces |
| Dark iron | `#343B43` | `#38414B` | barrel, latches and mechanism; no pure black mass |
| Replacement panel | `#B9AE8D` | `#B5AF9A` | one 5–10% patch, with a bevelled perimeter |
| Small safety accent | `#D98B51` | `#D29559` | one paddle or ring, under 3% area, below the crosshair |
| Warm exposed edge | `#C6B694` | `#C7C2AA` | selective contact wear, not every edge |
| Recess/old grime | `#454447` | `#41474B` | under overlaps and at grip junctions, broad and sparse |
| Cool paint variation | `#506166` | `#5D7078` | neighbouring vertices across a large panel |

Choose one main colour, one warm material and one dark mechanism before expanding the palette. Keep wear to about **2–4% visible area**: rubbed heel of the magazine, stock shoulder contact, grip palm contact, latch travel. One broad 0.015–0.030 m chipped patch with two smaller chips is better than edge glitter. Add a thin ring of lighter vertices only at selected exposed bevels. Split vertices at hard material boundaries so orange accents do not interpolate across an entire triangle. Use extra subdivisions only where a visible broad colour patch needs them; smooth low-amplitude shifts within a panel, no random colour per triangle. Never bake black ambient-occlusion stripes around every seam; they make shadow sides dirty and cannot respond to light.

## Avoid the PS2 read

Reject primitive silhouettes, pointed six-sided close barrels, uniformly sharp boxes, featureless flat side plates, detached-looking handles, baked-in highlights and unbroken black undersides. Also reject the opposite failure: a dense forest of tiny bevels, thin rails and noisy chips that sparkle at Deck size. A continuous bevel, intentional material blocks and a functional negative space matter more than total polygons. Lighting/post effects cannot repair a box silhouette; extra geometry cannot repair a material path that discards vertex colour. No glow added just to make the asset look newer.

## Screenshot pass/fail sheet

Request a fixed-seed set at native 1280×800: first-person rest, maximum kick, mid-reload (magazine hidden), one-hand state, broadside dropped and held at 20 m. Repeat the rest view in neutral daylight, warm low Sun, blue night with the suit lamp, and against pale sky. Include a grayscale and a black silhouette crop. These are requested captures, not completed checks.

For each image answer **yes/no**; record the screenshot name, failure and next geometry/colour edit:

1. Can a new viewer distinguish rifle from pistol by shape alone, including the tiny 20 m silhouette at a plausible viewing angle?
2. Does a space glove fit without touching the bow, receiver or magazine, with a believable support wrist?
3. Is the 96 px aim circle clear at rest and kick, and does the target remain legible during the flash/reload?
4. Does the closest curved edge look continuous, with a visible bevel rather than a hard cube or tiny shimmering strip?
5. Do primary body, grip and mechanism remain distinct in grayscale and on the night side, without white clipping in sunlight?
6. Do colour variation and chips explain handling/construction rather than form random facets or oily all-over dirt?
7. Does hiding the magazine leave only its seat, and do muzzle effects begin beyond the visible bore?
8. Are there zero near-plane cuts, exposed hollow backs, coplanar flicker and accidental centre-screen projections across the pose sweep?
9. Does the export keep correct scale, handedness, parts and vertex-colour boundaries, with counts inside the proposed budget?

Any **no** blocks visual sign-off; Deck frame-time acceptance additionally requires real measurement. These notes serve readable risk, low cognitive load and evidence-based iteration; they do not certify appearance or performance.
