# Milestones 0 + 1: test arena, pause menu, breakable mannequin

## What's in it
**M0, test arena (setup for every later playtest)**
- URP project with Steam Deck defaults (1280x800, linear colour, vsync) and a one-click setup script.
- Input System actions built in code; every binding tagged Keyboard & mouse or Controller.
- First-person controller: walk, sprint, crouch (won't stand up under a ceiling), jump, mouse and stick look
  (stick has a squared response curve for fine aim). Walking into gibs shoves them.
- Arena: 60 m checker floor (2 m squares to judge distance), perimeter walls, interior walls, cover, a ramp, a ledge, steps.
- Pause menu (Esc / Start): pauses time and audio; Resume, Controls, Restart, Quit.
  - Controls: Keyboard & mouse tab and Controller tab, every action with its current binding.
    Select a row, press a new key/button. Conflicts ask "Swap them?" or Cancel. Reset to defaults per device
    (with confirmation). Glyph labels for Xbox, PlayStation and Steam Deck (auto-detected, button to switch).
    Bindings save between sessions.
  - Restart: same seed or new seed, after a confirmation. Quit: to main menu or to desktop, each confirmed.
- Frame-time overlay (F3 / D-pad down): fps, average, worst 1 %, max, main thread, draw calls, dummy and gib counts,
  coloured against the 16.7 ms and 25 ms budgets.
- Capped top-right notifications (3 max), crosshair, hit marker, placeholder ping and pop sounds.
- Select / Tab is reserved for the upgrade screen (shows a "Phase 2" note for now).

**M1, breakable mannequin**
- Placeholder humanoid built from primitives on a real Unity Humanoid rig (Avatar built in code), one mesh
  and one hitbox per part, 14 parts, each with its own HP and parent.
- Limb HP is low: hands, feet and forearms pop in 1 hit, upper arms, thighs and shins in 2, the head in 3,
  the torso in 5 (test rifle, 34 damage). All in `MannequinConfig`.
- Losing a part takes everything below it, as one jointed gib chain launched along the bullet with extra lift
  and random spin. Gibs can be shot again: a joint snaps and the chain splits; shooting the top piece of a
  chunk bursts it and frees what hangs off it.
- Stump caps appear on both sides of every cut.
- The head is the only death. Destroy the torso and the head lives on. On death the head pops off and the rest
  of the body collapses as a ragdoll, keeping its momentum.
- Degraded movement driven only by what's left: one leg = hop/limp (slower, no sprint), no legs = crawl with
  whatever arms remain, no limbs = caterpillar wriggle, head only = rolling hops. Hands also decide whether the
  character could shoot (used by Milestone 3).
- Dummy walk modes for testing (G / D-pad left): Stand, Wander, Approach you.
- Gib cap (120 bodies) and 30 s lifetime for the Deck budget. Fade, pooling and blood come in Milestone 2.

## Constitution principles served
- **1 Fun core first / 8 Varied intensity:** limbs pop almost instantly and the degraded states make every fight different.
- **3 Learnable challenge:** fixed per-part HP, no hidden rules; the only death is the head, always.
- **5 Legible risk/reward:** a leg shot visibly slows a target, an arm shot (M3) disarms it.
- **7 Cognitive load:** one action per row, consistent glyphs, confirmations before anything destructive.

## Playtest checklist (please fill in; I won't guess these)
Run each with keyboard & mouse, then again with a controller.

| # | Do this | Pass if | Result |
|---|---|---|---|
| 1 | Walk, sprint, crouch under the low wall, jump onto cover | Feels responsive, no snagging | |
| 2 | Shoot a dummy's hand, forearm, foot | Each comes off in 1 hit | |
| 3 | Shoot a thigh twice | Thigh + shin + foot leave as one floppy chain | |
| 4 | Shoot that flying chain | It splits; caps show on the cut ends | |
| 5 | Remove one leg, set walk mode to Approach | Dummy hops toward you, slower | |
| 6 | Remove both legs | It crawls with its arms | |
| 7 | Remove both arms too | It wriggles forward | |
| 8 | Shoot the torso 5 times | Body flies off, the head stays alive and hops | |
| 9 | Shoot the head 3 times | Dummy dies, head pops, body ragdolls | |
| 10 | Esc / Start mid-fight | Everything freezes, sound stops, menu works with mouse, keys and controller | |
| 11 | Rebind Jump to a key already used | Swap / Cancel prompt; Swap works | |
| 12 | Quit play, press Play again | Your rebinds are still there | |
| 13 | Reset to defaults (each tab) | Only that device's bindings reset | |
| 14 | Restart → Same seed | Dummies spawn in the same spots and colours | |
| 15 | Restart → New seed | Different spots, new seed number | |
| 16 | Quit → Main menu → Play | Arena loads again | |
| 17 | F3 overlay while making lots of gibs | Note avg / worst 1 % ms here: | |
| 18 | Is it fun to shoot? (1-7) | | |

## What I could NOT verify
- **Nothing here has been compiled or run yet.** The cloud machine has no Unity editor, so compile errors,
  feel, physics stability (joint jitter), performance and the look of the procedural gait are all unverified
  until you open it. Expect a round of fixes from your first error list.
- Steam Deck: not built or tested on Deck yet (that route comes with Milestone 4).
- The URP asset is created by script; if Unity's URP version changed that API, setup may log an error (send it).
- Glyphs are text labels, not button images.

## Assets I'll need from you later (not needed for M0/M1)
- Milestone 2: 4-6 short "pop" sounds, a hit ping, 2-3 blood decal textures (I can make placeholders first).
- Milestone 3: a CC0 AK-47-style model and a pistol in `Assets/ThirdParty/Weapons/` (candidates are in that folder's README).
- UI: a chunky, free-licence display font for the TF2-ish look (e.g. an OFL font you like), and controller glyph images.
