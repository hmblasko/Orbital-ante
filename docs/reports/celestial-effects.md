# Celestial effects (Ralph loop pass 6, 28 Nov 2026)

Part B section 12 asks for "TF2-style rarity tiers for cosmetics on clothing and weapons, with an Unusual-like top
tier featuring special effects (e.g. fire)". Since pass 4 the top tier, Celestial, has been rolled, saved and named,
and its prize has gone to work as an ordinary gun skin, ship paint or suit piece, but its effect was not drawn: the
words said "not shown yet". This pass draws each of the six effects where its item shows: as moving light on the gun
in the player's hands and on the lander's hull, and as beads of light round the suit's readout plate and round the
prize card on the envelope's reveal. An effect is never to be mistaken for damage, a warning or an aim tell.
**Nothing here has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | The rarest prize in the envelope (half a per cent of them) did no more than an ordinary one, and said so ("not shown yet"). Section 12's "Unusual-like top tier featuring special effects" was a name only. |
| Hypothesis | An effect drawn on what the player looks at most (the gun in hand, the lander, the suit's readout) makes the rarest prize feel rare and worth showing off. Kept to light on the item's own surface, steady, never past white, and pale and outside the readout plate, it cannot be read as any signal the game uses for danger. |
| Principle | Discovery that matters (4) and meaningful replayability (6): the envelope's top line now has something to show. Legible risk (5) and cognitive load (7): nothing an effect does looks like a hit, a warning, damage or an aim tell, and the plate's beads go dark in a fight and beside a warning. Rule 9 and section 4: purely cosmetic, and the envelope's printed odds are unchanged. |
| Smallest test | Rule tests on the sums (the C# twin of the shader), the shader's text, the plate's rules, the words, the damage fade, the bloom and the Locker (`CelestialLooksTests`, 24 tests), and offline pictures of every effect in every place drawn from the same sums. |
| Acceptance | Every effect is drawn by the shader with the same numbers as the C# the tests judge. No pixel is lifted past white, and the brightest an effect draws gives under 1% of its light to the bloom at the Look asset's default threshold. The light on the whole gun or hull changes from one second to the next by under a fifth of its average typically and never by half. No seam round the hull, and no cut where its side turns. A star never comes up in under 0.48 s. A comet runs from the muzzle back to the hand. The hull's effect fades with its damage and is gone from a broken panel. The plate's beads stay outside the plate, at most 0.7 alpha and 0.45 of that once settled, never lit from dark in under a quarter second, paler than the readout's orange, green and blue, and dark in a fight or beside a warning. An enemy's gun and a gun that can blow up never wear an effect. The words say where each effect is seen and fit the reveal's line. |
| Playtest protocol | In an editor or development build, open Collection on the title screen, press "Try the next effect" (it puts the next of the six on a test gun skin, ship paint and badge, in use) and start a new run. For each of the six, look at the gun in hand in daylight and in a dark cave; at the lander from outside at 5 to 30 m, from above on a spacewalk and on a pole camera's feed; and at the suit's plate, lower left, as you step outside. For each, note whether it read as a prize, or ever as damage, a warning, thrust or an aim tell, and whether it was ever in the way of reading the air, the armour or the speed. Look for flicker or crawling at 1280 x 800, and at the top of the lander's dome. Fight a camp with Solar Flare in hand and note whether a flame was ever taken for being hit; check the plate's beads go dark in the fight and come back after. Knock the lander about until a panel is damaged and one is broken: the effect should dim on the first and be gone from the second. Drop the gun near an enemy without one and let it pick the gun up: its gun must show no effect. Open a test envelope with a Celestial prize and watch the beads come up round the card. Then "Clear tests". Read the costs under "To read off in Unity" in the budget's "Celestial effects". |

## The six effects

| Effect | On the gun | On the lander's hull | Round the suit's plate |
|---|---|---|---|
| Solar Flare | Tongues of orange light rising up the paint, white-hot at their cores | Slow clouds in the same fire colours, never rising: rising orange flame on the lander is what its thrusters show, and orange in the world means "shoot here" | Flickering tongues travelling slowly round the edge |
| Aurora | Thin green curtains that sway and ripple, their fringes violet; a small item always shows some of it | The same, in folds round the cabin | Slow folds of light swaying round |
| Comet Tail | Bright heads with fading tails, in lanes, running from the muzzle back toward the hand, never toward the muzzle (which could read as a shot charging) | The same, in lanes running round the cabin | One head running slowly round the plate |
| Nebula Drift | Soft clouds of violet and pink drifting slowly | The same | Two-coloured clouds drifting round |
| Starfall | Four-pointed stars in about half the cells of every face, each lighting on its own slow beat and sliding down as it fades | The same, in cells running round the cabin | Some beads twinkle, each on its own slow beat |
| Event Horizon | Bands of darkness turning round the item, each with a bright rim on its leading edge and a fainter one behind; under a band at most 60% of the paint's light is taken away | The same bands, but only 20% darker under them, because a darkened patch on the lander is what damage looks like | Pairs of lights, a band's two rims, running round |

## Never mistaken for damage, a warning or an aim tell

- **Light on the item's own surface only.** No sparks, smoke or loose particles, which are what damage looks like.
- **Never past white.** The shader adds the effect after the lighting and the shine and caps it at linear 1. Unity
  takes the bloom's threshold dial as a gamma value, so the Look asset's default of 1.25 is about 1.67 in linear
  light, with a soft knee half as wide: by URP's own sums (`CelestialLooks.BloomShare`) a white pixel gives 0.8% of
  its light to the bloom, where a glow piece drawn at twice white gives a fifth and one at four times white over half.
  So nothing an effect draws glows like a muzzle flash, a shining aim tell or a lamp. At most 55% of its colour on a
  gun and 42% on the hull, where it covers far more of the screen. A rule test reads the threshold's default from
  `LookConfig` (the dial itself can still be turned down to 0.5, where a white pixel would bloom a little: it is
  Matthew's to tune).
- **Steady.** Nothing swells, pulses or grows, which is what the Heavy's spin-up and the Marksman's lens do. The light
  on the whole gun or hull, second by second over four minutes, changes typically by under a fifth of its average and
  never by half:

  | | Gun, typical | Gun, at worst | Hull, typical | Hull, at worst |
  |---|---|---|---|---|
  | Solar Flare | 0.15 | 0.45 | 0.03 | 0.06 |
  | Aurora | 0.06 | 0.17 | 0.05 | 0.09 |
  | Comet Tail | 0.13 | 0.25 | 0.04 | 0.12 |
  | Nebula Drift | 0.02 | 0.06 | 0.03 | 0.06 |
  | Event Horizon | 0.09 | 0.21 | 0.07 | 0.17 |

  Starfall is points of light that come and go one at a time, so it is held to its own test instead: a star takes at
  least 0.48 s to come up, and the stars of one item first light over more than a second and a half, never together.
  Flames first swelled and shrank together about once a second, because rising straight up through the noise's grid
  carried the whole gun through its cells in step; the noise is now read on a grid turned against the item
  (`CelestialLooks.TurnedNoise`, the shader's `OA_FxTurned`). The hull's clouds first showed the grid's blocks for
  the same reason and are now read the same way.
- **Never reacting.** It never changes with a shot, a hit, a reload or low armour. It runs on the world's clock
  (`_Time.y`), so like everything else in the world it slows with Focus and the headshot slow-motion, holds for a
  hitstop and stops with a pause. The plate's beads run on unscaled time, like the rest of the readout.
- **No blinks.** A star takes at least 0.48 s to come up (Starfall runs at half speed on the gun, so a rifle's many
  small cells never light several stars a second at the edge of the view); a comet is a head moving along the item,
  never a light that flashes in place.
- **The hull's trim stays plain.** Only the painted panels take the effect; the dark skirt, the window bands and the
  dark wall panels never do (`ShipShell.PaintedCorners`, carried in the mesh's colour alpha).
- **The hull's damage comes first.** The lander shows damage by darkening a part (`Ship` shades its renderers to 0.78
  when damaged and 0.5 when broken). The effect fades with that shade (`CelestialLooks.HullDamageShare`): all of it on
  a sound panel, about half on a damaged one, none on a broken one, so the effect never competes with the damage, a
  wrecked lander never shimmers, and a repair lights it again. Event Horizon's bands are only 20% darker on the hull.
- **No cuts and no sparkle on the hull.** Round the cabin the pattern runs on the turn round its upright axis, 24
  units round, so it meets itself behind with no seam; the hull has only two faces (the cap, where the paint faces within 45 degrees of straight up,
  and the side), so Starfall's cells are never cut along upright lines where the side turns; and nearer the axis, where a
  unit of the turn is smaller, the pattern fades sooner by the pixel's footprint, to nothing at the top of the dome
  (`CelestialLooks.RunScale`), so the dome's top never crawls.
- **Never on an enemy.** Only the player's guns wear skins. A gun an enemy picks up (the player's dropped one) goes
  back to its own paint (`MannequinCombat.TryPickUp`), so an enemy's gun never has moving light on it.
- **Never on a gun that can blow up.** The Wildcard can blow up in the player's hands, so any change in its look would
  read as its warning: it wears the skin's paint and pattern but never its effect (`PrizeLooks.OnGun`, asked by
  `LockerLook.SkinFor` with the gun's `backfireChance`).
- **The plate stays readable.** The beads sit 4 px outside the plate's edge (a pixel clear of it, and 7 px clear of
  the corners), never over a number; the speed, tether and stance lines above the plate moved up 8 px so the top row
  never touches them, and the notice strip moved 12 px right so its widest is clear of them
  (`HullReadout.NoticeShift`). At most 0.7 alpha; no bead lit from dark in under a quarter second; their colours are
  taken most of the way to the readout's cream and never more saturated than 0.38, under the readout's orange (0.90),
  green (0.46) and blue (0.44), so a bead is never read as "act on this" or "target met".
- **The plate is quiet when it matters.** The plate warns by changing in place (the air's alarm blinks, the jet blocks
  blink when low, the armour flashes on a hit), and moving light there all the time would teach the eye to ignore
  change there. So the beads go dark over 0.3 s while the plate blinks a warning, while the notice strip shows an
  alarm, and in a fight (the readout's own test, `AnteLoop.InFight`, or a creature's warning within the same 6 s),
  and come back over 1.5 s after (`CelestialPlate.Hushed`, `Shown`). As the plate comes up (stepping outside) they show
  off at full strength for 3 s, then settle over 1.5 s to 0.45 of it, where they stay (`CelestialPlate.Strength`).
  With several suit pieces carrying effects, the next one shows each time the plate comes up (`CelestialPlate.TurnFor`),
  so the plate's motion never changes while it is up.

## Where each effect shows

- **A gun skin's effect** is on every gun in the player's hands that wears the skin, except a gun that can blow up. A
  dropped gun keeps its look on the ground until an enemy picks it up.
- **A ship paint's effect** is on the lander's painted hull, from outside and on the pole cameras' feed wherever the
  hull is in view (from the seat the hull itself is out of view). It goes on with the paint (`Ship.Repaint`; the hull
  is built again only when the paint or its effect changes).
- **A suit piece's effect** (helmet, visor, suit, jetpack or badge) is round the suit's readout plate, the one place
  on screen the suit is.
- **On the reveal** the prize card wears the effect's own beads, with the plate's motion and colours, coming up over
  0.8 s (`EnvelopeScene.EffectBeads`), so a Celestial prize shows what it does the moment it is won.
- **Not drawn** under the "simple shaders" box or with Shine at 0 on the Look asset (the gun and hull keep their skin
  or paint and lose the effect; the plate's beads still show).
- **The words** say where an effect is seen: "Effect: Aurora (on every gun)" for a skin for every gun, "(on that gun)"
  for a gun's own skin (the line above it names the gun), "(on your lander)", "(round your suit's readout)", on the
  reveal and in the Collection. A line longer than 44 letters is drawn a size smaller on the reveal, so the longest
  ("Effect: Event Horizon (round your suit's readout)") stays on one line. "Not shown yet" is gone.

## How it is built

- **Plain C#:** `Items/CelestialLooks.cs`. `CelestialLooks` holds each effect's motion per place (`MotionFor`),
  colours, strength, speed, size, finest detail and dark share per place, the damage fade, the bloom's sums, and the
  sums the shader does (`Surface`, `Lift`, `TurnedNoise`, `Along`, `Face`, `RunScale`, the shader's own hash and
  noise), so the tests and the previews judge exactly what the GPU draws. `CelestialPlate` holds the beads: their path
  round any plate, their pale colours, their light, the flourish, the hush and the turns. `Locker.EffectWhere`,
  `EffectWords` and `EffectsInUse` give the words and what is worn.
- **The shader:** `OA_Model.shader` draws the effect behind `[branch] if (_FxSet.y > 0.0)`, a material value, never a
  keyword, so it adds no shader programs and no stall the first time a Celestial gun comes into the hand (a gun ladder
  step can do that mid-fight); its four values (`_FxColour`, `_FxSecond`, `_FxSet`, `_FxShape`) are in the shared
  material buffer. Each motion is a branch of its own. It works out how much of the effect a pixel takes first
  (painted corner, damage shade, the pixel's footprint) and returns at none, so trim, broken panels and far pixels pay
  almost nothing more. The numbers are the `OA_FX_<NAME> n` lines. Fine detail fades before it could crawl
  (`OA_SkinFine`, by the pixel's footprint).
- **The gun:** a skin's look carries its effect (`SkinLook.Effect`); `PlanetMaterials.SkinnedModel` sets it on the
  gun's material; `LockerLook.SkinFor(WeaponData)` leaves it off a gun that can blow up.
- **The hull:** `PlanetMaterials.CelestialHull` (one cached material per effect), `Ship.HullMesh`; the damage shade is
  the part's own `_BaseColor`, which the effect reads.
- **The plate:** `SuitHud.BuildBeads` and `LightBeads`: about 86 small images on a canvas of their own under the plate,
  coloured on their renderers each frame. No layout or mesh is rebuilt; only the beads' own canvas is batched again.
- **The reveal:** `EnvelopeScene.EffectBeads` (one soft dot each, `EnvelopeArt.Bead`), seen offline with
  `tools/envelope_view`, which now keeps what a group draws outside its bounds unless the group clips.
- **Testing in Unity:** the Collection's test row (editor and development builds) has "Try the next effect"
  (`Locker.TryNextEffect`), because Celestial is one envelope in two hundred.

## Reviews

Each finding was checked against the code before it was applied.

- **game-designer:** the plate's beads kept moving through the plate's own warnings, and their top row touched the
  speed line (now hushed beside a warning and in a fight, and the speed lines moved up 8 px); Solar Flare's rising
  orange tongues on the hull would read as the lander's thrust or fire (now slow clouds there, flames kept on the gun,
  fire being the prompt's own example); Event Horizon darkened the hull the way damage does (now 20% there, and every
  hull effect fades with the damage shade); the reveal drew the same orbiting sparks for every effect and the plate
  read as a faint dotted frame (now the effect's own beads round the card, and a 3 s flourish as the plate comes up,
  then an idle at 0.45); two comet heads at 150 px/s were chasing lights, the usual sign for "claim me" (now one head
  at 60 px/s); turns every 10 s changed the plate's motion even in a fight (now on each step outside); the words
  should name the gun (now "on every gun" or "on that gun": a gun's own name is too long for the line, and the line
  above it names the gun); the Wildcard wore the every-gun skin's effect (now never); Starfall lit several stars a
  second on a rifle (half speed on the gun); "never blooms" held only at the default threshold (a rule test holds the
  default). It found as built: the steadiness tests, nothing past white, enemy pickups stripped, comets running away
  from the muzzle, the trim plain, the colours under the readout's, the strengths, and no hidden rule for a glowing gun
  in a dark cave (the 0.35 visibility floor already counts the player's lamp).
- **unity-reviewer:** only the chain's first motion had `[branch]`, so a compiler could flatten the rest and a
  Celestial pixel pay for all six (now each has its own); no budget line (written below); Starfall's cells on the hull
  followed the normal's strongest axis and so cut stars in half along four upright lines and a ring on the dome (now
  two faces, and cells round the turn); a sparkle at the top of the dome, where 24 units of the turn meet in a point
  (now the pattern fades by the pixel's footprint toward the axis); the words said the light does not change with
  Focus, when `_Time.y` slows with it (corrected here and in the repo notes; a clock of its own would only be needed if
  Matthew wants the light to run on through slow-motion); the longest reveal line was 519 px in a 540 px box (now
  drawn smaller past 44 letters, and the longest line is tested). It checked as sound: the shared material buffer in
  every pass, no derivative or texture read inside the branches, the pattern in object space (it never swims as the
  ship moves or the origin re-centres), the colour alpha paired only with the model material, the damage tint
  surviving a repaint, the hull's mesh and materials freed once, the caches cleared between worlds, the indoor twins
  carrying the effect, the fallbacks, the beads' draw order, and every enemy pickup going through `TryPickUp`.
- **deck-performance** (an offline compile for the Deck's GPU, not Unity's compiler and not measured on a Deck): the
  hull's effect worked out its share after the whole sum, so trim, broken panels and far pixels paid for nothing (now
  the share comes first and returns at none, and the hull's turn is worked out only on the hull); Solar Flare adds
  about 390 steps a pixel on the gun and Aurora about 306 on the hull, guessed at 0.17 to 0.35 ms aiming down sights
  and 0.28 to 0.55 ms with the lander filling half the screen; the effect's code needs more registers in every model
  pixel, worn or not (64 instead of 56, measured again after the share change), guessed under 0.1 ms; and a model's
  pipeline now takes about 40 to 52 ms to build on a cold shader cache instead of 22 to 33. Kept as a material value,
  not made a keyword: a keyword would add twelve programs and a stall when a gun ladder step brings a Celestial gun
  into the hand mid-fight unless it is warmed, and nothing warms shaders yet. If the register cost measures over
  0.1 ms on the Deck, the budget says to make it a keyword and warm it. Warming the model shader at a run's start is
  left open (it was already owed for the shine). The bead comment that said the canvas costs no rebuild overstated it
  (corrected). Budget rows below are from this review.

## Still unverified

- Nothing has run in Unity: how the effects look on the real guns and hull under the game's light, whether any of
  them reads as a warning in a real fight, and their cost on the Deck (the budget's "To read off in Unity").
- The shader's costs come from Mesa's compiler with stand-in URP files, not Unity's; the Deck's real numbers may differ.
- `_Time.y` loses precision after many hours of play, as it does for every shader in the game that reads it.
- Celestial prizes are half a per cent of envelopes, which open only after ante 9 (Matthew's rule), so most players
  will see an effect rarely; the test button is for checking, not for players.
- The Collection's rows name an effect but do not show its beads.
- Paintings still wait for frames on the cabin wall and the five public-domain pictures.
