# Milestone 1 report - Breakable playtest characters

Status: **built, not yet run in Unity.** I wrote this without access to a Unity Editor. The code compiles against Unity reference assemblies and the rule tests pass, but nobody has pressed Play yet. Everything about look and feel is unverified until you try it. Setup steps are in `docs/SETUP.md`.

## What changed

- **New Unity 6 project** in `unity/` (URP, Input System, C#). The old Godot + Rust build is preserved on the `godot-archive` branch and removed from this branch.
- **One-click setup** (`Orbital Ante > Set Up Project`, also runs by itself on first open): creates the URP settings, tunable config assets, layer names and the test scene.
- **Test arena**: 60 m flat floor with a 1 m checker, perimeter walls, two tall walls, two low walls and a crate. First-person controller, keyboard/mouse and controller.
- **Mannequin** built from primitives: 14 parts (head, torso, L/R upper arm, forearm, hand, thigh, shin, foot), one mesh per part, each parented to the matching bone of a Unity Humanoid skeleton with a generated Humanoid avatar.
  - Each part has its own hitbox, its own HP, a parent part, and wound caps: one on the parent where the child was, and one on the severed end.
  - Losing a part takes everything below it as one gib. A gib can be split again by shooting any part below its top part, in the air or on the ground.
  - The torso can be destroyed: each remaining limb chain bursts off separately and the head lives on. **Only destroying the head kills.**
  - Detached chunks get a Rigidbody, inherit the character's motion, and are launched along the bullet direction with extra lift and random spin.
- **Degraded movement**, chosen only from what is left: walk/sprint, one-legged hop (slower, no sprint), arm crawl (works with one arm, lopsided), torso wriggle, rolling/hopping head. All procedural.
- **Test rifle**: bare-minimum full-auto hitscan (34 damage, 10 shots/s, tracer, tiny kick) so the mannequins can be tested. Milestone 3 replaces it.
- **HUD**: crosshair, hit marker (white hit, orange limb off, red kill), frame-time readout, and the name and HP of the part under the crosshair. F3 hides the debug parts.
- **Pause menu** (Esc / Start), pauses time and audio, works with mouse, keyboard or controller:
  - Resume.
  - Controls: Keyboard & Mouse tab and Controller tab, press-to-rebind, conflict detection with Swap or Cancel, reset to defaults per device, bindings saved between sessions, button names and face-button colours that follow the controller (Steam Deck / Xbox / PlayStation).
  - Restart: same seed or new seed, or cancel.
  - Quit: to main menu or to desktop, each behind an "Are you sure?" with No as the default.
- **Minimal title screen** so "quit to main menu" has somewhere to go.
- **Tests**: 19 rule tests (EditMode) and 10 tests on the real mannequin object (PlayMode).

## Why, and which constitution principle each serves

| Decision | Principle |
|---|---|
| Limbs at 30-68 HP against a 34-damage rifle, so every limb comes off in 1-2 hits | 1 Fun core gameplay first; 3 Learnable challenge (no inflated health) |
| Only the head kills; a character can be whittled to a head | 2 Agency and trade-offs: disable, slow or kill are different choices |
| Movement state follows visibly from what is missing, with no hidden rules | 3 Learnable challenge; 5 Legible risk/reward |
| The part under the crosshair shows its name and HP | 5 Legible risk/reward; 10 Evidence-based iteration (you can check my numbers by eye) |
| All numbers in `MannequinConfig` / `PlayerConfig` assets | 10 Evidence-based iteration |
| Rules in a plain class (`BodyModel`) with tests | 10 Evidence-based iteration |
| Rebinding, conflicts, reset, saved bindings, pause is fixed so you can never lock yourself out | 7 Manage cognitive load; 9 Ethical design |
| Seed shown on the pause screen; same-seed restart reproduces the arena | 6 Meaningful replayability (groundwork) |

## What I tested

- **Rule tests, actually run** (with `dotnet test`, outside Unity): 19 of 19 pass. They cover chains detaching together, gibs splitting again, torso destruction, head-only survival, head as the only death, every movement state, and a 200-run random-shots check that parts always end up in exactly one connected chunk.
- **Compile check, actually run**: all runtime scripts compile against Unity engine modules (2021.3) plus the real Input System and Unity UI assemblies, with Unity 6-only names mapped back. The editor setup script and the PlayMode tests were compiled against hand-written stand-ins for the editor and test-runner APIs, which checks my code but not my memory of those APIs.

## What is NOT verified

1. **Nothing has been run in Unity.** No Play session, no PlayMode tests, no frame times. A first-open compile error is possible; if you see red in the Console, send me the first line.
2. **The package versions** in `Packages/manifest.json` are my best match for Unity 6 LTS. Unity normally corrects built-in package versions by itself.
3. **The URP setup step** creates the pipeline assets from script. It is written defensively, and the game falls back to the built-in renderer rather than turning pink, but I have not seen it work.
4. **The Humanoid avatar** is generated in code. A PlayMode test checks that Unity accepts it as valid.
5. **How the movement looks.** I chose joint angles by reasoning, not by eye. Expect some of it to look wrong (an arm bending backwards, a crawl that slides). This is the part I most expect to iterate on with you.
6. **Glyphs** are button names and coloured discs, not artwork. Steam Deck detection is untested on a real Deck.
7. **Steam Deck performance**: not measured. No Deck build has been made.

## Known placeholders and limits

- Dead bodies fall as one rigid chunk rather than a floppy ragdoll. A real ragdoll comes with the real enemy model in Milestone 12; say so if you want it earlier.
- Mannequins only wander. They do not fight back (they get weapons in Milestone 3).
- No sound, blood, decals, hitstop or camera shake yet. That is all of Milestone 2.
- Loose parts are simply deleted after 30 seconds. Pooling, the fade and the active-gib cap are Milestone 2.
- Humanoid animation clips are not used yet. The skeleton is ready for them, but motion is procedural for now.
- Select / View on the controller is deliberately unbound: it is reserved for the end-of-ante upgrade screen.

## Playtest checklist

Tick or note each. Please do not skip the "feel" lines; those are the ones I cannot check.

**Setup**
- [ ] Project opens with no red errors in the Console.
- [ ] Console shows "Orbital Ante: project is set up."
- [ ] EditMode tests: 19 green. PlayMode tests: 10 green.

**Arena and player**
- [ ] Title screen appears; "Enter the test arena" starts the game.
- [ ] Walking, sprinting, jumping and looking feel right on keyboard/mouse.
- [ ] Same on a controller. Is stick look speed comfortable?
- [ ] Frame time readout (top left): what does it say with 8 mannequins up?

**Dismemberment**
- [ ] Aim at a part: its name and HP show under the crosshair.
- [ ] Limbs come off in 1-2 rifle hits. Does that read as "almost instant"? Too easy?
- [ ] Shooting a thigh takes shin and foot with it as one piece. Upper arm takes forearm and hand.
- [ ] Shoot a loose leg at the shin: it splits again, in the air and on the ground.
- [ ] A red cap shows on both sides of every break.
- [ ] Gibs launch away from you with spin. Is it exaggerated enough, or too much?
- [ ] Empty the torso (about 8 hits): limbs burst off, the head survives and rolls away.
- [ ] Reduce one to just a head. It is still alive. Shoot the head: it dies.
- [ ] Headshot on a healthy one (3 hits): head pops off, body drops.

**Movement states** (watch one for a few seconds after each)
- [ ] Intact: walks, and sometimes sprints.
- [ ] One leg gone (or one foot): hops, slower, never sprints.
- [ ] Both legs gone: crawls on its arms. With one arm: lopsided crawl.
- [ ] No legs, no arms: torso wriggles forward.
- [ ] Head only: rolls and hops slowly.
- [ ] Which of these look right, and which look wrong or dull?

**Pause menu**
- [ ] Esc and Start both open it. The game and mannequins freeze.
- [ ] Every page works with mouse only, keyboard only (arrows + Enter, Backspace or Esc to go back) and controller only (stick/D-pad + A, B to go back).
- [ ] Controls: rebind Jump on the keyboard tab. It works in game.
- [ ] Rebind Fire to the key Jump uses: you are offered Swap or Cancel, and both do what they say.
- [ ] Controller tab shows names that match your controller. Rebind a button.
- [ ] Reset to defaults restores that tab only.
- [ ] Stop Play, press Play again: your bindings are still there.
- [ ] Restart with the same seed: mannequins start in the same places. New seed: different places.
- [ ] Quit to main menu and Quit to desktop both ask first. (In the editor, "desktop" just stops Play.)

**Overall**
- [ ] Was there a moment that made you laugh? What was it?
- [ ] What felt worst?

## Assets needed from you

None for Milestones 1 or 2. For Milestone 3, optionally drop a CC0 gun pack into `unity/Assets/ThirdParty/Weapons/` (for example the Quaternius "Ultimate Gun Pack"); without one I will use placeholder shapes with the same sockets.

## Next

Milestone 2 (gibs that feel amazing) builds directly on this. I would rather fix whatever the checklist turns up first, since every later combat milestone stands on these mannequins.
