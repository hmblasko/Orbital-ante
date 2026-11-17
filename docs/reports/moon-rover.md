# Moon rover (16 Nov 2026)

Matthew asked: "I would love a moon rover vehicle that can run enemies over and splat them and/or their limbs. The rover should be really fast and should be aesthetically pleasing. Do deep research to find the right one or example one in creating it. I'll defer to you on what makes the most sense."

Rovers are a Phase 2 item in Part B (section 15, item 15). This was built because Matthew asked for it, as with stances and the gun ladder.

## Research and the choice

### Free models

Every candidate was checked on its own page or through Sketchfab's public API. The repo is public, so the licence has to allow the source files to be shared.

| Model | Licence | Triangles | Verdict |
|---|---|---|---|
| Hyena Recon Transport (Michael Wright, Sketchfab) | CC BY 4.0 | 26.9k, textured | The best free fit, but it is a six-wheel sci-fi scout. Its seat, interior and separate wheels are unconfirmed. |
| Mondfahrzeug / Lunar Rover (Deutsches Museum Digital) | CC BY-SA 4.0 | 464k, textured | An accurate Apollo rover from laser scans. Ten times too heavy for the Deck. Useful as reference. |
| Lunar Rover (SixSong) | CC BY 4.0 | 8.4k | Origin unknown, no description. |
| Sci-Fi Buggy (TiyaMakes) | CC BY 4.0 | 162k | Too heavy, and it reads as an Earth buggy. |
| D Raider | CC BY 4.0 | 56k | Made from someone else's concept art, so the licence chain is risky. |
| Kastle Lunar Buggy, tashtego buggies, Lunokhod-2 | CC BY-NC | | Not allowed (non-commercial). |
| Kenney and Quaternius space kits | CC0 | | Cartoony, and they have no rover. |
| NASA 3D Resources | Free of copyright in the US, logos excluded | | The current catalogue has no Apollo rover. Curiosity and Perseverance are robots with no seat. |

**Choice: an original model in Blender.** No free model was safe, Deck-sized, realistic and built for first person all at once. The game's models are also coloured per corner and read by its own loader, so a textured model would need a texture pipeline first. That is the same open question as the textured guns. Building our own also gives the parts the game must move: four wheels and four suspension arms, each built round its own pivot.

### What it is based on

- **The Apollo Lunar Roving Vehicle:**
  - Open twin seats, with the driver on the left.
  - A T-handle hand controller between the seats.
  - A motor in every hub.
  - Steering at both ends ("double Ackermann").
  - Woven wire-mesh wheels with titanium chevrons over half the tread.
  - Fenders over the wheels, a dish on a mast, and foil-wrapped boxes.
  - Dimensions for scale: 3.1 m long, 2.3 m wheelbase, 0.82 m wheels.
- **The Artemis terrain vehicle concepts** (Lunar Outpost, Astrolab, Intuitive Machines): a roll cage, long-travel suspension and a sportier stance.
- **Part B section 8** ("dune-buggy rovers with rocket boosts"): two boost nozzles at the back.

### How the real one handled, and what that meant for the game

The Apollo crews describe the rover in these words:
- "Bucking bronco".
- "Like steering a boat".
- All four wheels off the ground during the Grand Prix run.
- The tail stepping out at 10 to 12 km/h.
- Braking that took about twice the Earth distance.

At real lunar gravity a fast car only skates and floats. Players say the same about Starfield's rover on low-gravity worlds. Games that feel good do three things:
- Keep grip from a stronger "grip gravity" than the world's own.
- Add downforce with speed.
- Keep the long airtime and give the stick control in the air.

The rover does all three. Its tail can still step out at speed, but it is a slide you can catch, as in the Apollo accounts.

## What changed

- **Model** (`tools/blender/build_rover.py` writes `Resources/Models/Rover.bytes` and `RoverDavit.bytes`):
  - About 20k triangles for the body, plus four wheels of 6.8k each and four arms of 0.9k each, about 51k drawn in all.
  - Moon dust low down, crowd shadow, and finishes on bare metal, foil and paint.
  - No logos, flags or markings.
- **Carried on the lander.** The rover hangs from a davit behind the lander's base, nose out, tyres about 2 m off the ground, between the back legs and clear of the rear engine's flame.
  - Land, walk round the back, look at it and press Interact. It is winched down onto the ground found under the hook. If there is none within 6 m (a steep slope), it stays up and says so.
  - Drive it back to within 3.5 m of the davit, get out, and hold Interact beside it. It is winched back up.
  - If the lander lifts off while the rover is being lowered, the winch snatches it back up.
  - A rover left on a world stays where you parked it.
- **Driving** (`Rover/MoonRover.cs`, rules in `Rover/RoverRules.cs`, dials on the new `RoverConfig` asset):
  - Top speed is 122 km/h, or 173 km/h with the boost lit. The real rover's record was 18 km/h.
  - Steering narrows with speed. At a crawl the back wheels steer too, as on the real rover.
  - With two or more wheels down it is held to the ground by at least 7 m/s² plus speed downforce. In the air it is pulled down at least 3.8 m/s² (1.7 times the world's pull if that is more), the stick tips it, and it levels itself toward the ground. It is held only while it is the right way up, so it never sticks to a cave wall.
  - The boost only pushes along the ground. Pointing the nose up in the air does not launch it.
  - Handbrake for skid turns.
  - A Reload press puts it back on its wheels when it is on its back.
- **Running people over.** Each physics step, any hitbox in the rover's path takes a hit based on closing speed:
  - Under 22 km/h: a bump. They flinch and are shoved aside, and nothing comes off.
  - From 22 km/h: every part it touches takes damage, and legs come off first because of the bumper's height.
  - From 47 km/h: splat. The torso bursts, every limb flies and the head goes with it.
  - The pieces leave a little faster than the rover and slightly upward, so the bumper does not ram them twice. They go through the same gib, gore, decal and sound systems as a gunshot.
  - Loose limbs and bodies split further when driven over.
  - Each hit counts as yours. It rings the hit marker, counts toward the gun ladder and refills Focus.
- **People are run over, never driven into.** The rover's colliders are on a new physics layer, **Vehicle** (13), which does not touch characters' bodies. It still hits the ground, the lander, you on foot, gibs and dropped guns.
- **The driver:**
  - Driving is a new player mode. In it you breathe the suit's air and see the air and armour gauges.
  - Enemies see you and shoot at a stand-in hitbox in the seat. It sits on a new layer, **Seat** (14), which collides with nothing. The open frame does not stop bullets.
  - A big crash hurts you, and a wall at full speed kills you ("Crashed the rover at N km/h.").
  - You cannot climb out above 14 km/h.
  - No gun while driving: the rover is the weapon.
  - The view widens a little with speed. It follows the "camera shake" comfort switch.
- **Sound:** the motor whine is a stand-in made in code (a hum, a gear whine and a little grit, pitched with speed). The audio pipeline should replace it.
- **HUD:** speed in km/h low in the middle, with the boost tank as a bar under it.

Constitution:
- 1 (fun core: a fast way to cross the hilly ground the worlds were built for, and a new way to fight).
- 2 (agency: leave the lander behind, or bring it).
- 5 (legible risk and reward: the faster you hit, the more comes off, and the speed is on screen).
- 8 (varied intensity: drives between fights).

## Controls

| | Keyboard and mouse | Controller (Deck) |
|---|---|---|
| Winch down / get in / get out | E | Y |
| Winch back up (beside it, parked under the davit) | hold E | hold Y |
| Accelerate | W | Left stick up, or R2 |
| Brake, then reverse | S or F | Left stick down, or L2 |
| Steer | A / D | Left stick |
| Boost | Space or Shift | A, or L3 |
| Handbrake | Ctrl | B |
| Put it back on its wheels | R | X |
| Look round | Mouse | Right stick |

The controls reuse the on-foot buttons, so nothing new appears on the rebinding page and saved rebinds still work. Rebinding Jump also moves boost.

## Tested here

- `dotnet test tools/rules-tests`: 2,451 passed, 1 skipped. That includes 10 new rover tests:
  - steering
  - top speeds
  - the hold toward the ground on the Moon and on Earth
  - springs holding the drawn ride height
  - grip that peaks then slides
  - bump, limbs and splat thresholds against the enemies' limb hit points
  - the boost tank
  - crash hurt
  - the rover hanging clear of the ground, the legs and the engine flame
  - the model file against the layout
- All runtime scripts compile against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam. The only errors are the known IHittable name clash with that library.
- Pictures from Blender (`docs/art/previews/rover_*.jpg`). They are not Unity screenshots.

## Agent review

A reviewer read the rover code for Unity 6, design and Deck faults before it was committed. It found the following, and all of it was fixed:
- The driver's stand-in hitbox had its own kinematic body on the Player layer, so it could push the lander. It is now a plain collider on the new Seat layer, which collides with nothing.
- Boost worked in the air, so the rover could be flown like a rocket. Now only the push along the ground counts off the wheels.
- The hold toward the ground worked at any angle, so the rover could drive up a cave wall and stick there. It now fades out past about 60 degrees from upright.
- Physics read the drawn pose, which can lag one physics step behind (0.7 m at top speed). It now reads the body's own pose.
- Getting out of a tipped rover used the rover's up. It now uses the world's up and drops you beside it.
- Airtime was too long at the Moon's pull: a 3 m/s pop at speed meant 3 s in the air. Now the rover is pulled down at least 3.8 m/s² in the air, the hold lingers 0.12 s over small crests, and the stick works with one wheel down.
- At low speed the rover went straight through people. They are now shoved aside with a flinch, and loose limbs on the gib layer are found and split.
- The pieces of a splatted enemy spawned inside the bumper. They now leave faster than the rover, and slightly upward.
- Bumping the lander while parking could damage it. The rover's knock is now scaled by its share of the two masses.
- Lowering on a slope could sink the rover. The ground is now found from the davit's hook, and the rover is not lowered if no ground is found.
- Winching up from 7 m away dragged it through the legs. The reach is now 3.5 m.
- Armour did not recover while driving, and death said "outside the ship". Both are fixed.
- Dying in the seat could park the rover and leave the view with nothing to follow. It no longer parks while it is the view's frame.
- Small Deck costs: the wheel angles now wrap round, the HUD writes only on change, and part lookups take the cheap path.
- Still to measure: at 34 to 48 m/s the rover needs ground colliders built faster than walking does (`MakeSolidAt` cooks meshes on the main thread). Watch the F3 rescues count and frame times.

## Not verified (needs Unity on the Deck)

Nothing here has been run in Unity. In particular:
- **Feel.** The handling numbers are reasoned, not played: speed, grip, the hold toward the ground, the slide, and jumps. The dials are all on `RoverConfig`.
- Whether the winch-down finds the ground under the davit on a slope, and whether the rover settles without a hop.
- The parked rover being fixed to the spinning world, and being woken when you get back in.
- How the run-over hits read at speed. The rover could hit a crowd in one frame and call hitstop several times.
- Enemies' bullets reaching the driver through the frame, which depends on how they aim at the seat.
- The cables' look, and the davit's place against the lander's back face.
- Frame time on the Deck (about 51k triangles, no far copy yet), and whether ground colliders keep up at 170 km/h.
- Lights: the headlamp lenses glow, but there is no beam yet, so night driving is dark apart from your suit lamp.

Playtest protocol (principle 10):
- **Problem:** the worlds are big and hilly, and walking is slow.
- **Hypothesis:** a fast rover that turns enemies into fireworks makes crossing them a highlight.
- **Smallest test:** one drive from the pad on Earth, then one on the Moon.
- **Acceptance:**
  - Over 100 km/h on flat ground.
  - A crater rim jump lands on its wheels most of the time.
  - Running into an enemy at speed always splats.
  - Getting in, out, down and up never leaves you stuck.

## How to test it (on the Deck)

1. In Konsole: `cd ~/Orbital-ante`, `git fetch`, `git checkout claude/playtest`, `git pull`.
2. Open the project in Unity Hub and wait for it to finish importing. Then run **Orbital Ante > Set Up Project** from the top menu. This names the new Seat layer and makes the RoverConfig asset.
3. Press Play. You start seated on the pad on Earth. Fly to the Moon and land, or land anywhere nearby on Earth first.
4. Unbuckle and go out through the hatch. Walk round to the back of the lander and look up at the rover hanging there. Press **E / Y** to winch it down.
5. Look at the rover and tap **E / Y** to get in.
6. Drive: **W or R2** to accelerate, **S or L2** to brake and reverse, **A/D or the left stick** to steer. **Space or A** boosts, **Ctrl or B** is the handbrake.
7. Jump a crater rim. Check that it comes back down in a sensible time and that the stick tips it in the air.
8. Roll it on purpose, then press **R / X** to put it back on its wheels.
9. Find enemies (the Moon's bases or the arena camps). Drive into one slowly, under 20 km/h: they should be shoved aside. Then at about 30 km/h: limbs should come off. Then over 50 km/h: splat. Watch the hit markers and the gun ladder count.
10. Stop under 14 km/h and press **E / Y** to get out.
11. Drive back to just behind the lander, get out, look at the rover and **hold E / Y**. It should winch up onto the davit.
12. Press **F3** and check that the "rescues" count stays at 0 while you drive fast.

Tell me what felt wrong. All the handling numbers are on Assets/OrbitalAnte/Resources/Config/RoverConfig.
