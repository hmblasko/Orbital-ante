# Starter lander exterior — art lead handoff

Source: `claude/look-pass-1` at `45cf8948974623d8d8165a4cf9c0377e2cb619f2` (refreshed during review; ship layout/damage source unchanged) (PR #8), especially `ShipLayout.cs`, `ShipModel.cs`, `Ship.cs`, `ShipShell.cs` and `ShipProps.cs` under `unity/Assets/OrbitalAnte/Scripts/Runtime/Ship/`. Proposals below are for Fable's modeling decisions. No Blender render, Unity view or Deck measurement was available; all visual gates remain open.

## Read this count before splitting the model

The requested **21 parts** are **21 physical design units, but only 18 damage parts in this source**: cabin + base + four legs + four engine bells + four thruster clusters + dish + five windows + hatch. `ShipModel` groups the four bells into **one** `engines` health/detachment unit. Do not silently invent three new engine damage parts. The table below covers all 21 physical units and maps each to its actual runtime ID/index.

Only legs, the grouped engines and thruster clusters can become Lost: **nine detachable assemblies**. The dish explicitly has `CanFallOff=false`; the class summary suggesting otherwise is stale. Windows break out but never become Lost wreckage. Cabin, base and hatch stay attached. Any wish for 21 independently destructible parts is a separate gameplay/code decision, outside this documentation pass.

## Shape language and originality

Retain the lunar-lander engineering idea: a bowl cabin over a squat octagonal equipment raft, four outboard feet, independent shoulder jets and a roof dish. Make its identity a **soft ceramic-looking upper bowl, folded ochre lower panels, blunt tool-like leg collars and one asymmetrical repaired equipment bay**. The bowl feels inhabited; the base feels packed with practical machinery. Preserve mass transitions and useful negative spaces rather than adding greebles everywhere.

Use real lander engineering as structural research, not an instruction to reproduce a historical vehicle's exact panel map or any game's ship. No mission insignia, flags, logos, real brand names, invented faction names or copied cockpit/entry dressing. The player is unaligned. Dish offset, two front windows, round side ports and belly access already provide enough character; add controlled asymmetry to finish and repairs, not to landing geometry or thrust alignment.

Primary forms must read in a black silhouette at 1280×800: dome, raft, four feet, dish and shoulder clusters. Secondary forms should explain how they are joined. Tertiary colour chips are close-view rewards. Do not force every surface into a flat polygon merely to advertise low triangle count, and do not smooth away the base's deliberate eight-sided structure.

## Dimension lock — metres, source facts

Origin `(0,0,0)` is the middle of the cabin floor; **+Y up, +Z nose, +X pilot's right**. Blender export must explicitly map its axes to this convention, apply scale once, and preserve handedness. Geometry must fit existing collision/walking volumes; this brief does not authorize changing either.

| Region | Exact source dimensions / authoring consequence |
|---|---|
| Cabin bowl | Inside widest radius **2.5**; floor radius **2.15**; cove height **0.5**; wall turns into roof at Y **1.2**; inside ceiling peak **2.6**; hull thickness **0.14**. The outer widest diameter is **5.28**, roof cap top **2.74**. Use the profile functions, not a new sphere stretched to the same bounds. |
| Cabin profile grid | **64 columns × 20 rows**, **5.625°** per column. `InnerProfile`, `OuterProfile`, `OuterPoint` and `SurfacePoint` define the actual surface and window positions; thickness is along the profile normal. |
| Base | Octagon **inradius 2.3**, across flats **4.6**; circumradius `2.3/cos(22.5°)` ≈ **2.489502**, across opposite corners ≈ **4.979004**. Top Y **−0.05**, belly **−1.7**, height **1.65**. Do not mistake the inradius for the corner radius. |
| Landing stance | Four axes at **45°,135°,225°,315°**. Foot centres at radius **4.3**, bottom Y **−3.8**, footpad radius **0.45**. Roots at radius **2.4**, Y **−0.55**. X/Z pad bounds ≈ **±3.490559** (span **6.981118**); maximum opposite diagonal pad span **9.5**. These are different measurements. |
| Main engines | Four cardinal mounts at radius **1.5**, Y **−1.7**; exits Y **−2.35**, radius **0.4**; bell depth **0.65**. Ground-to-exit clearance **1.45**. |
| Shoulder thrusters | Four cluster centres at radius **3.05**, Y **1.15**, same diagonal angles as legs. Coordinates `(±2.156676,1.15,±2.156676)` with appropriate corner signs. |
| Dish | Centre **(−1.2,3.35,−1.3)**, radius **0.62**; mast foot **(−0.95,2.4,−1.05)**. Current bowl axis normalized `(−0.25,0.8,−0.55)`, depth **0.16**, thickness **0.015** (`ShipProps`). Do not treat centre height as overall ship height. |
| Belly passage | Clear shaft radius **0.8** (diameter **1.6**), lift catch radius **0.6**; belly-to-ground clearance **2.1**. Ring centreline radius **0.98**. `ShipProps` ring tube radius **0.07**, centre **(0,−1.74,0)**: inner/outer radii **0.91/1.05**. |
| Entry control | Outside hatch button **(1.35,−1.75,0)**. Cabin lever **(1.05,1.0,−0.55)**. Preserve both reach and sightline from below. |
| Human / cabin protection | Person radius **0.3**, height **1.8**, eye **1.62**, walk radius **1.95**; seated eye **(0,1.18,1.28)**. Seat **(0,0,1.2)**; desk Z **1.72**, lockers Z **−1.62**. No exterior panel or frame may intrude into these volumes or sightlines. |

### Five window holes: preserve the actual patches

Window columns measure angle from the nose toward +X. The following are exact `WindowSpec` grid values, not freely rearrangeable decoration. Frame/reveal is cabin geometry; pane is a separate damage unit. Keep its opening and curvature from `ShipShell`.

| ID | Start column / width | Start row / height | Shape |
|---|---|---|---|
| `window_front_left` | 57 / 6 | 5 / 6 | rounded lander triangle, mirrored |
| `window_front_right` | 1 / 6 | 5 / 6 | rounded lander triangle |
| `window_overhead` | 61 / 6, wraps through 0 | 13 / 4 | rounded box |
| `window_right` | 13 / 6 | 5 / 5 | round |
| `window_left` | 45 / 6 | 5 / 5 | round |

## All 21 physical units: appearance, joins, break behavior and pivot

Coordinates below are ship-space **stored rotation centres from `Ship.cs`**, not a claim each mesh already has that transform origin. Let `R(i)=ShipLayout.LegRoot(i)`, `T(i)=ShipLayout.Thruster(i)`, and `G(id)=ShipShell.GlassBox(spec).centre`. These getters are authoritative; rounded coordinates only aid drawing. Damage pivots are not proposed deployment hinges.

| Unit | Runtime ID (index) | Look and joining geometry | Break / retained interface | Current pivot |
|---|---|---|---|---|
| 1. Cabin | `cabin` (0) | Warm bowl with broad inset panels, deep window reveals, dark compression skirt seating into raft; one repaired rear equipment panel. | Cannot detach; Broken loses seal, current exterior stays intact and darkens. Do not promise a new fracture animation. Keep window frames with cabin. | `(0,1.2,0)` |
| 2. Base | `base` (1) | Ochre eight-sided raft; folded corner guards and 0.08 m top/bottom chamfer already establish depth. Open central shaft; panel seams stop at load collars. | Cannot detach; stays and darkens. Base-only break does not set `CabinIntact=false`. Cosmetic panels do not acquire independent health. | `(0,−0.9,0)` |
| 3. Front-right leg | `leg_0` (2) | Broad root collar, shaped main tube, knee, two braces, dished pad; preserve existing ladder as part of this assembly. | Broken sags 7°; Lost takes strut, braces, pad and ladder together. Give brace ends caps; no geometry strip should remain bridging the gap. | `R(0)` ≈ `(1.697056,−0.55,1.697056)` |
| 4. Back-right leg | `leg_1` (3) | Same load path; one broad warm maintenance patch on outer brace, no extra functional protrusion. | Same 7° sag / whole-leg loss; expose a retained base flange and capped detached ends. | `R(1)` ≈ `(1.697056,−0.55,−1.697056)` |
| 5. Back-left leg | `leg_2` (4) | Same structural dimensions; slightly different colour blocks on collar/pad, not a differently placed foot. | Same sag/loss. Split along physical root and brace attachments, not arbitrarily halfway down a tube. | `R(2)` ≈ `(−1.697056,−0.55,−1.697056)` |
| 6. Front-left leg | `leg_3` (5) | Same load path; cleanest front-facing pad lip to balance ladder on the opposite side. | Same sag/loss; root socket should read as an empty mounting cup after loss. | `R(3)` ≈ `(−1.697056,−0.55,1.697056)` |
| 7. Forward engine bell | `engines` (6), subunit 0 | Mount `(0,−1.7,1.5)`, broad collar, short dark throat, tapered bell and warm ceramic lip. Keep inner wall visible from below. | All four bells belong to one health/loss group; Broken assembly sags 4°. No independently falling forward bell. | Group `(0,−1.7,0)`; authoring mount is not a separate runtime pivot. |
| 8. Right engine bell | `engines` (6), subunit 1 | Mount `(1.5,−1.7,0)`, same bell; route visible service line outward around entry control, never across shaft. | Lost with other three; retained sockets on base, detached collars capped. | Same group pivot. |
| 9. Aft engine bell | `engines` (6), subunit 2 | Mount `(0,−1.7,−1.5)`; one replacement outer jacket colour, identical exit dimensions. | Same grouped loss. Preserve four separated bells rather than adding a central nozzle. | Same group pivot. |
| 10. Left engine bell | `engines` (6), subunit 3 | Mount `(−1.5,−1.7,0)`; restrained exterior ribs, inside lip bevel continuous at near view. | Same grouped loss. Do not add a support crossbar through the lift to visually explain grouping. | Same group pivot. |
| 11. Front-right cluster | `thruster_0` (7) | Squat rounded valve block with four blunt nozzle cups and a readable load-bearing stalk. | Broken sags 22°; whole stalk/block/nozzle unit can detach. Leave hull flange with cabin; cap stalk at seam. | `0.88*T(0)` ≈ `(1.897875,1.012,1.897875)` |
| 12. Back-right cluster | `thruster_1` (8) | Same mechanism; outer block gets one broad ochre panel, not four glowing nozzle ornaments. | Same sag/loss; no cable joining this to another health unit. | `0.88*T(1)` ≈ `(1.897875,1.012,−1.897875)` |
| 13. Back-left cluster | `thruster_2` (9) | Same family; cool valve cap against warm stalk collar. | Same sag/loss; enclosed backs on all nozzles for tumbling wreckage. | `0.88*T(2)` ≈ `(−1.897875,1.012,−1.897875)` |
| 14. Front-left cluster | `thruster_3` (10) | Same silhouette; keep it distinct from the dish at three-quarter view. | Same sag/loss; avoid beams obstructing side port. | `0.88*T(3)` ≈ `(−1.897875,1.012,1.897875)` |
| 15. Dish assembly | `dish` (11) | Shallow cream bowl, rolled lip, stout off-centre mast, broad feed horn and three practical feed struts; no satellite-brand silhouette detail. | **Retained and repairable**, never Lost and currently no sag. Broken map goes dark until repair. A bent-rim design is a future state-art proposal only. | Mast foot `(−0.95,2.4,−1.05)` |
| 16. Front-left pane | `window_front_left` (12) | Mirrored rounded triangle; broad cream reveal with dark gasket. No copied window trim pattern. | Crack stages 1–3/frost; at stage 4 pane hides and glass collider disables. Frame stays, no pane wreckage or hinge. | `G(id)`; never substitute the front-face midpoint. |
| 17. Front-right pane | `window_front_right` (13) | Partner triangle, consistent gasket width and pilot sightline. | Same break behavior. Both must be readable from seated eye with no heavy crossbar added. | `G(id)` for this spec. |
| 18. Overhead pane | `window_overhead` (14) | Rounded rectangle following dome; generous rim with only one shallow latch-like visual. | Same crack/hide behavior; no roof door implied. | `G(id)` for this spec. |
| 19. Right porthole | `window_right` (15) | Thick circular rolled rim and few broad fastener pads, no radial screw confetti. | Same crack/hide behavior; keep outline separate from nearby jet cluster. | `G(id)` for this spec. |
| 20. Left porthole | `window_left` (16) | Same circular construction; repaired adjacent hull panel belongs to cabin, not this pane. | Same crack/hide behavior. | `G(id)` for this spec. |
| 21. Hatch assembly | `hatch` (17) | Two semicircular floor leaves with thick undersides; blue/cyan belly ring remains the navigational cue. A broad seam expresses split doors. | Never detaches. Broken stops normal operation; current motion combines a downward move reaching **0.075 m** by openness 0.25 with an eased slide reaching **±0.86 X** at full opening (the motions overlap). Keep the sweep clear, no hinge animation. | Damage part `(0,0,0)`; doors slide, no hinge pivot. |

### Join and damage handoff rules

Current `NewPart` roots are identity transforms, meshes/colliders use full ship-space coordinates, and sag uses `position = pivot − rotation*pivot`. Do **not** recenter meshes at the table's pivots and also apply that mechanism. The new `OAM1` loader builds flat named parts in common model space; it carries named position sockets but no hierarchy, pivot rotation or independent damage mapping. Ship integration must map those part names to the existing IDs and stored rotation centres exactly once. Do not treat the socket list as automatically wiring ship damage. The existing `LanderBake.bytes` uses a separate `LBK1` interior-light format, not `OAM1` geometry; leave the cabin/bake untouched.

Thruster stalk skin joins come from `SurfacePoint(i*16+8, 5.86, 0.7)`, while their sag pivot is an approximation inside that join. Build a broad collar that tolerates the existing 22° sag; do not rely on a tight ball joint at a newly invented pivot. Leg knees are 55% along root-to-ankle; their braces return near base Y **−1.48**. Each detachable assembly needs visible terminal caps and a retained mounting surface. Decorative hoses must belong entirely to one group or terminate at the seam; they cannot remain stretched between separated groups.

Damage facts: Damaged below **0.66** health, Broken at zero. Eligible groups become Lost on one blow at least **2.5×** max health or accumulated overkill at least **1×** max health; the initial breaking blow adds half its spare damage to overkill. Lost is permanent; Damaged/Broken repairs. Wreckage inherits point velocity, gets an outward shove and spin; there is no authored, direction-specific fracture sequence. Engines still have **0.4** thrust factor when Lost, so avoid writing art notes that promise total loss of propulsion. Actual repair/spark anchors also live in `Ship.cs`; keep new panels from burying them.

## Belly entry must stay usable

Keep the entire shaft radius 0.8 unobstructed and the hatch-leaf swept volumes clear. Do not add a ladder, central engine, diagonal beam, decorative hanging cable or new door across the lift. The existing leg ladder is not a substitute for belly entry. A suited 1.8 m person must see the ring and reach `(1.35,−1.75,0)` from underneath.

There is only **0.05 m nominal radial clearance** between the ring's outer radius 1.05 and the closest engine lip at `1.5−0.4=1.10`. That is a tight reserved gap, not spare space for added ribs. Put engine reinforcement **outboard**. Keep ring and button framing simple against the dark underside; warm paint may surround the ring but should not compete with its cool cue. Ring/beam are visuals, not additional health parts. Vertex colour alone cannot create HDR emission: preserve the existing emissive material route rather than claiming a bright hex swatch will bloom.

## Panels and practical detail

- Bowl: 6–8 broad curved colour/panel regions fitted around the five fixed holes, with shallow overlap lips and a few recessed service hatches. Do not follow every 64-column grid edge with a black line. At the skirt, 0.025–0.045 m bevel bands read as load-bearing folded edges.
- Base: 2–3 panel shapes repeated with variation on eight sides. Use a long folded plate, a shorter access lid and a chamfered corner guard. Start seams 0.012–0.020 m wide and recess 0.008–0.015 m, stopping before passage and engine mounts. One panel has a larger replacement patch; keep the rest calmer.
- Legs: enlarge visible root collars and foot lips within existing collision envelope before adding hydraulic rods. Main collars can catch a 0.015–0.025 m bevel; brace edges 0.006–0.012. Give pads shallow radial depressions with broad, irregular paint sectors, not a sawtooth edge.
- Engines: 16–24 sided lips as a starting point; 12–16 on smaller throats. Spend triangles on bell curvature and inner/outer rim thickness, not tiny cooling fins. Three or four broad jacket bands convey fabrication better than dozens of rings.
- Dish: enough radial segments for its 1.24 m diameter to read round from ground level (start at 32), a thick rim and sparse struts; no razor-thin rim disappearing against sky. Use smooth shared-vertex curves, deliberate splits at rolled edges. The new `ModelLibrary` preserves exported split normals; the older procedural `ShipMeshes` path recalculates them. State which path a replacement exterior uses rather than assuming identical results.
- Handmade variation: offset a panel seam by a few centimetres, vary warm/cool colour on replacement sheets, and soften selected corners. Keep foot positions, engine axes, window apertures and pivots exact. No random dents along every outline.
- Near detail should survive a first-person walk-under at native resolution. At 20 m the raft folds, leg collars and window rims should remain; at 80 m the silhouette and palette blocks carry the ship. Do not expect subpixel bolts to communicate quality.

## Vertex-colour palette and wear

All hex values are **sRGB authoring swatches**; use one conversion to linear on the receiving mesh path. `ShipMeshes.ToMesh` converts unbaked paint in a linear project; baked cabin data is already linear. The new `oa_blender` → `ModelLibrary` path explicitly exports sRGB RGB8 and converts it to linear in a linear project; keep this contract when used for the ship. Vertex colour is the only surface-paint source; no texture, UV, normal-map or decal work is requested.

| Material/role | Base | Variation / wear | Placement |
|---|---|---|---|
| Cabin enamel | `#D7C9AA` | cool `#BBC1B4`, warm `#E2C59E` | 55–65% of upper shell, broad vertex gradients within panels |
| Raft insulation-like folded panels | `#B88A48` | `#9D713E`, `#C7A366` | warm lower mass; geometry carries folded depth, no metallic glitter required |
| Painted steel collars | `#586B6C` | `#718080` | legs, structural rims and jet blocks |
| Dark mechanism/gaskets | `#353E47` | `#475059` | recessed areas, bell throats, joints; avoid pure black undersides |
| Replacement sheet | `#A3A88B` | `#BBC0A0` | one rear bay or one base panel, not on every face |
| Contact wear | `#C8B697` | warm exposed edge `#D8CBAF` | pad rims, ladder contact, service latches and handling points |
| Heat staining | `#75675D` | `#55545A` | broad lower bell zones, local to heat source |
| Entry cue paint | `#72BDC9` | `#A5D8DC` | ring framing/button; preserve runtime glow separately |
| Small service accent | `#CC7950` | `#D9976D` | reachable handles, less than 3% of exterior area |

Target approximately 65% calm paint, 25% secondary construction, 8% mechanism and 2% accents at a typical three-quarter view; this is composition guidance, not a mandatory per-mesh formula. Wear **3–5%** of visible painted area, concentrated on feet, access edges and working latches. Put broad chips into bevel/patch topology; avoid randomly bright vertices. Keep seams lighter than black, no universal fake AO outline, no prepainted directional highlights. Duplicate vertices for abrupt material boundaries, share them for smooth tonal drift. Retain enough mid-value variation to read on the night side without making the whole hull emissive.

## Proposed triangle and renderer budget

The existing budget estimates **about 11,000 exterior triangles / about 15 exterior draws**, not a measured Deck allowance. Use **11,000 exterior triangles as the initial ceiling**, allocated after triangulation as follows. Counts include undersides, caps, bevels and repair join surfaces; count exported vertex splits separately. These are proposed limits, not counts of a completed asset.

| Exterior allocation | Triangle ceiling |
|---|---:|
| Cabin shell, reveals, exterior trim | 3,200 |
| Base and shaft exterior | 1,500 |
| Four legs incl. pads/braces/ladder | 2,400 (600 average each) |
| Four grouped bells | 1,200 (300 each) |
| Four thruster clusters | 1,000 (250 each) |
| Dish/mast/feed assembly | 600 |
| Five panes | 500 (100 average) |
| Ring/button/exterior hatch finish allowance | 600 |
| **Total** | **11,000** |

The budget accounting assigns panes and entry visuals to this exterior total; preserve existing ownership at import to avoid double counting hatch leaves. Interior is separately estimated at **11,758 triangles / 11 pieces**; crack geometry can add roughly **24,000 triangles** across five badly damaged panes. Do not spend those triangles again on cosmetic scratches. Wreckage uses the detached groups, not duplicate intact/broken assemblies drawn together. Aim to preserve about 15 exterior draws by batching within damage ownership and material limits; do not merge detachable groups or panes merely to reach a draw number. Extra transparent layers and overdraw cost matter as well as triangles.

Optional authoring LOD targets, only if the receiving pipeline later supports them: 4,000 exterior triangles for a medium-distance version, 1,500 for far view; never simplify the belly opening or fixed window landmarks first. No LOD implementation or profiling is included. Any higher near-detail tier needs the art lead and a measured Deck budget review rather than treating unused global triangle allowance as free GPU time.

## PS2-look / screenshot gate

Ask for native 1280×800 captures: ground-level front/side/rear three-quarter, walk-under entry, seated view through front panes, black silhouette at 20/80 m, night with lamp, low Sun, and matching views with one leg, engines group and one cluster lost. Also ask for all five window crack stages and open/closed hatch. No capture has been made here.

- **Pass:** silhouette reads as one inhabited vessel on four weight-bearing feet. **Fail:** sphere on a box, pencil legs, floating collars or a dish represented by a flat coin.
- **Pass:** bevels make broad soft transitions; curves remain continuous in the closest view. **Fail:** hard box corners, faceted bowl silhouette or uniform full-surface triangulated colour noise.
- **Pass:** windows have thickness and reveal, while pilot view remains clear. **Fail:** dark flat stickers, fake painted reflections or new opaque frames crossing the sightline.
- **Pass:** three main palette masses and local wear read under daylight and night lighting. **Fail:** black belly, uniformly gold plastic, white clipping, brown mud everywhere or cyan/amber accent clutter.
- **Pass:** retained flanges, capped wreckage and exposed mounting cups explain each separation. **Fail:** stretched hoses, missing mesh backs, a leg taking cabin panels with it, or single bells shown falling separately.
- **Pass:** hatch leaves slide clear; ring and button remain visible and reachable, every engine stays outside the reserved gap. **Fail:** entry blocked by a decorative central component or flashing geometry in the 0.05 m gap.
- **Pass:** local material shading and contact grounding support the forms. **Fail:** trying to hide missing contact shadows with painted black halos or bloom. Real lighting/contact depth is a renderer follow-up, not something this geometry brief can certify.

Record each failure against a named capture and one smallest edit. Visual sign-off and 60 fps acceptance require Unity/Deck evidence later; polygon totals alone do not establish either.

## Two concept-turnaround prompts

**Prompt 1 — construction and silhouette:**

> Create an original orthographic concept turnaround sheet for a small warm, hand-crafted space lander. Front, right side, rear, top and underside, identical scale and aligned floor datum, plus one small suited-human scale figure. Cabin is a soft cream bowl with 2.5 m inner radius, 0.14 m shell, 2.6 m inner peak above floor; a 4.6 m across-flats ochre octagonal equipment base beneath it from Y −0.05 to −1.7 m. Four splayed legs at 45/135/225/315 degrees, pad centres radius 4.3 m, pad bottoms Y −3.8 m, pads radius 0.45 m. Two rounded triangular front panes, two circular side ports, one rounded overhead pane. Four short engine bells at cardinal radius 1.5 m, exits Y −2.35 m and radius 0.4 m. Clear central belly lift shaft diameter 1.6 m, two sliding floor hatch leaves and cool blue entry ring below; no central engine. Four squat shoulder jet clusters at diagonal radius 3.05 m and Y 1.15 m. Small offset dish centre (−1.2,3.35,−1.3) m with radius 0.62 m. Broad rolled edges, sparse practical seams, shaped collars, one repaired rear panel, calm cream/ochre/slate-teal palette. Render surfaces as broad vertex-colour-like patches with simple form shading, no texture maps. No logos, brands, flags, named factions, copied fictional vehicle, decorative central beam, tiny greeble field or photoreal material noise. Use no game imagery as a design template. Treat this image as a mood/proportion proposal; technical dimensions are supplied separately for modeling.

**Prompt 2 — joins and entry clarity:**

> Draw a second original concept sheet of the same lander in neutral soft light: a matched front and side turnaround, large underside view of open belly entry, one low three-quarter intact view and a separate assembly diagram showing natural separation seams. Keep cream bowl, ochre eight-sided raft, four broad padded legs, four separate cardinal engine bells around the unobstructed 1.6 m shaft, four diagonal shoulder jet clusters and a retained offset dish. The four bells form ONE service/damage assembly despite their physical spacing; show them grouped with a diagram bracket, never a crossbeam through the entry. Show one detached full leg including braces and foot, and one detached shoulder jet with its stalk; capped ends on both wreckage and retained mounting collars. Keep cabin/base/dish/hatch attached, window frames retained around missing glass. Two floor leaves lower up to 0.075 m while sliding apart to ±0.86 m; entry ring centreline radius 0.98 m stays visible, nearest bell lip has only 0.05 m nominal clearance to ring outer edge. Use broad seams, sparse warm edge chips and slate mechanism recesses, with no black AO outlines, emissive hull, copied ship features, logos or faction names. Palette: cream #D7C9AA, ochre #B88A48, slate-teal #586B6C, dark #353E47, entry cyan #72BDC9. Plain light background and consistent orthographic scale; give no invented dimensions. This is a concept aid, not an engineering drawing or proof of animation compatibility.
