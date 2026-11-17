# Brief for Fable: Faction A suits and the melee models

Milestone 13 put Faction A and five melee weapons into the game as rules, with stand-in looks. This brief lists the models that would replace the stand-ins. Blender scripts write the models (`tools/blender/`), the same way as every model the game already has. Follow `docs/art_direction.md`, especially "Reading a fight", and log each model in `docs/asset_licence_ledger.md`.

Faction A is a placeholder. Never give it a name, an emblem, a flag or lettering.

## 1. Faction A: three suits

**Reference.** Soviet and Russian pressure suits: the Sokol (soft, laced, worn for launch) and the Orlan (a hard torso entered through a door in the back, with its pack built into that door). Pull real photographs first. The look must be original and must not copy any one suit badge for badge.

**Why it matters now.** In the game the three tiers wear the unaligned kinds' bodies: the Scout wears the Runner's, the Line the Trooper's and the Heavy the Bruiser's. So a Faction A camp cannot yet be told from an unaligned one by shape. Only its behaviour shows it. Until these suits exist, that is the main readability gap in Milestone 13.

**Shared rules for all three:**
- Build on the same skeleton (`BodySkeleton`, the helpers in `build_enemy.py`) at the same size. The game scales the body: Scout 0.85, Line 1.0, Heavy 1.3. Never model a tier larger or smaller.
- One piece per `BodyPartId`, plus `Stump`. Every requirement in "Reading a fight" applies: dark gloves and boots, a dark ring at each joint, a mark on the back of the helmet, nothing behind the head.
- A faction-wide band colour sits on the shoulders and helmet ring. This is the use the art direction holds band colour back for. It must be one colour for all of Faction A and must differ from every unaligned kind. Shape tells the tier; colour tells the side.
- Each tier needs its far copy (`XFar.bytes`) at 0.00% seen-from-inside, and an updated `BodyFit` row if its hitboxes change.
- Triangle cap: 16,000 per body (see CLAUDE.md, Models).

| Tier | File | Silhouette goal | What must read | Helmet |
|---|---|---|---|---|
| Scout | `EnemyScoutA` | Soft Sokol-like suit, slim, laced, no hard torso | The **free hand** must be clearly visible when the arm goes up. The flare is fired from it, and shooting it off stops the flare. | Small, rounded, wide visor |
| Line | `EnemyLineA` | Orlan-like hard torso, a squared pack on the back door | The rifle outline against the chest | Domed, with a sun-visor ridge |
| Heavy | `EnemyHeavyA` | Orlan torso with added plates; very broad shoulders; the gun is braced at the hip | **Legs**: taking a leg is a counter, so the legs must not hide under armour skirts | Squat, with a narrow slot visor |

**After the models exist (Opus can do this part):**
1. Set `model` on the three `EnemyRoster` rows.
2. Add `BodyStyle.For` entries and `BodyFit` rows.
3. Raise `EnemyRoster.Version`.

## 2. Faction A: three guns

These currently borrow existing models: `SmgA` uses the Smg, `RifleA` the Rifle, `HeavyGunA` the Shotgun. Each new model needs the usual sockets (`Muzzle`, `SupportHand`, `Magazine`, plus a separate `Magazine` piece) and a far copy.

- **SmgA** (Scout): a short carbine with a folding stock.
- **RifleA** (Line): a long rifle with a drum or long magazine. The game gives it 40 rounds.
- **HeavyGunA** (Heavy): a rotary gun. It **must** have a piece named `CellGlow`. The game scales that piece up and brighter through the 1.2 s spin-up, and that growing glow is the tell. Keep the piece on the side the player sees when the Heavy faces them. A barrel cluster in a separate piece named `Barrels` would let the game spin it later (not wired yet).

## 3. Melee weapons: five models

The game currently uses stand-in shapes. Each model is one file, built in a new script `tools/blender/build_melee.py`, held as `Resources/Models/Melee*.bytes`. Each needs the sockets `Grip` (where the hand holds it) and `Tip` (the far end, where the swing reaches), as the stand-ins have; see `docs/reports/melee.md`.

| Weapon | Real reference | Must read |
|---|---|---|
| Laser sword | An original hilt (a flashlight or torch body is a good start); the blade is a glow piece | Blade in a piece ending `Glow`, so it blooms. The hilt must read as a grip in first person. |
| Baseball bat | A real wooden or aluminium bat, with a strip of tape on the handle | Dents and scuffs near the barrel, where the sweet spot is |
| Mailbox | A US-style curbside mailbox torn off its post, the post stub still on it | Heavy and absurd. The flag is a separate piece so it can flap later. |
| Butterfly knife | A balisong with two handle halves | Small but readable on the Deck's screen; nothing thinner than 12 mm across |
| Frying pan | A cast-iron skillet | The flat underside faces the target at the hit |

All five need a first-person view (held low on the right) and a world copy for when they lie on the ground at a base's loot point. They are kept at or under 2,000 triangles and have far copies, like the guns.

## Order of work

1. Faction A Scout, then Heavy, then Line. The Scout's raised hand and the Heavy's legs carry gameplay. The Line is closest to the Trooper.
2. HeavyGunA, because its `CellGlow` is a tell.
3. Melee: laser sword, bat, mailbox, knife, pan (this is the research ranking).
