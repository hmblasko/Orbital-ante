# Enemy cover and flanking

Matthew, 16 Nov 2026: "Make it so that enemies try to take cover. Also, make it so they try to flank."

Branch `claude/enemy-cover-flank`, into `claude/playtest`. **Not run in Unity.**

## What changed

**Cover.** An armed enemy in a fight now goes to a spot you cannot see and fights from it:

- **Finding cover.** It looks for cover by checking lines of sight, not from a list. Anything solid between you and the spot counts: rocks, trees, base buildings, the ship, the crest of a hill, a cave wall.
  - A spot only counts if the enemy has room to stand there, can walk straight to it, and has somewhere to shoot from.
  - Cover that gets shot away, or a tree that gets felled, stops counting. The enemy notices within half a second and moves.
- **Shooting from cover.** It takes turns hiding and looking out to shoot.
  - Behind low cover it stands up and shoots over the top, so its head shows.
  - Behind full cover it steps about 1.4 m out to the side.
  - It ducks back to reload, and it waits behind cover while the squad's turns to shoot are taken. So a squad fires in turns from behind cover.
  - Every time it looks out there is a short beat before its first shot. You never get an instant shot from cover.
- **Walking round cover works.** If you move 6 m or walk round its cover, it has to find new cover.
- **If you hide.** If nobody in the squad has seen you for 4 s, they leave cover and come looking, as before.

**Flanking.** In a camp or base squad, once the squad has had you in sight for 6 s:

- **Who goes.** One member is sent wide round your side, about 70 degrees round, heading for cover there. In a squad of four or more, two go, one each side.
- **On the way.** A flanker doesn't shoot while it moves. When it gets there, it waits its full reaction time plus 0.6 s before the first shot.
- **More waves.** A flanker you kill is replaced only after 8 s. A new wave can go 12 s after the last flank ended, so long fights don't stall.

**Kinds.**

| Kind | Takes cover | Flanks |
|---|---|---|
| Bruiser | Never: it still walks you down in the open | Never |
| Marksman | Yes, and stays out long enough to finish its aim line | Never. Tower lookouts keep their post. |
| Runner | Only once it is near you, and never closer than 6 m | Goes first |
| Trooper | Yes | Yes |

**Enemies who lost a leg** drag themselves to cover within 5 m, but only to a spot where they can still shoot from the ground. They are never sent round.

**Enemies with no gun** now hide behind the same kind of cover on the worlds. Before, they could only run away there.

## Why

Constitution 1 (fun core) and 8 (varied intensity): fights now have a rhythm. There are lulls while enemies hide, and pressure while someone comes round the side.

Constitution 3 (learnable challenge) and 5 (legible risk/reward): every rule can be seen and answered. Heads show over low cover. A step out from cover comes before the shot. Moving or walking round cover flushes enemies out. A flank is a timed wave, not a constant stream.

## How it fits what was there

- **Cover from the world itself.** Cover is found from the physics world, so it works with destructible bases (PR 53) once they land and with felled trees, with no extra hooks.
- **Caves.** In a cave, cover is only looked for on the same level, and only where the cave's own shape says a straight walk works.
  - A camp shares one cave footing. It now remembers 8 routes instead of one, so members going to different spots don't steer toward each other's next steps.
- **Sneaking and hearing are unchanged.** Cover and flanking only start once someone in the squad has seen you.
- **Disarming, the scramble for the gun, death and head-only all let go of cover.** A disarmed enemy also stops being a flanker.

## Tested (cloud, no Unity)

- `dotnet test tools/rules-tests`: 2,435 passed, 1 skipped (the baked-light file). 19 of them are new, in `Tests/EditMode/CoverAndFlankTests.cs`. They cover:
  - who is sent round, when, and how many
  - one flanker each side
  - snipers and Bruisers never sent
  - replacing a fallen flanker only after a pause
  - waves
  - a new fight starting afresh
  - no two members on one spot
  - the front ignoring flankers
  - cover scoring
  - the hide and look-out rhythm
- The game code compiles against Unity reference assemblies. The only errors are the 2 known name clashes from the reference library.
- Two agent reviews were done: a Unity and Deck review, and a game design review. Everything they found is fixed:
  - **Fairness.** The first-sight beat had been made shorter for every enemy; it is back to normal outside cover. Flank shots needed a longer first beat. The Marksman could duck in the middle of its aim line.
  - **Caves.** Cover could be picked through cave walls.
  - **Cost.** An unarmed enemy with no cover searched every think, and one search could cost too much on the Deck.
  - **Stuck states.** Turning Cover off mid-fight left hiding enemies frozen. Downed enemies could hide where they can't shoot. Cover claims were left behind. The cave route cache was shared by the whole camp.

## Not verified (needs Unity)

- How it all feels, and whether the numbers are right. All numbers are first guesses and are on the EnemyConfig asset under **Tactics**.
- That enemies walk to cover without snagging on rocks or base walls.
- Cover inside caves.
- Frame time on the Deck. The estimate is 0.1 to 0.3 ms for the one search allowed each frame.

## Not built

- A shout or other sound when a flank is sent. The game designer review asked for one so a flank can be heard coming, but there are no enemy voice events yet. This would go to the audio thread.
- Flanking in the test arena. The arena's mannequins have no squad, so they take cover but never flank.

## How to test (on the Deck)

1. In Desktop Mode, open Konsole and run:

   ```
   cd ~/Orbital-ante
   git fetch
   git checkout claude/playtest
   git pull
   ```

2. Open the project in Unity Hub. Wait for it to finish compiling, then press Play.
3. Land near a camp with rocks or trees around it, or near a base. Get out and walk toward it until they shoot.
4. **Cover.** Watch the enemies after the first shots. They should run to rocks, trees or buildings and stay behind them. Behind low cover they rise and shoot over it. Behind tall cover they step out to the side and back.
5. **Flanking.** Keep fighting from one place for about 10 seconds. One enemy should run wide round your left or right without shooting. In a big camp or base, one should go each way.
6. **Walking round cover.** Walk round an enemy's rock so you can see it. It should run to new cover.
7. **Killing a flanker.** Kill a flanker. Nobody should replace it for about 8 seconds.
8. **Bruisers and Marksmen.** A Bruiser should keep walking straight at you. A Marksman should hide, and it should never duck while its red aim line is showing.
9. **Comparing with the old way.** In the Project window, open `Assets/OrbitalAnte/Resources/Config/EnemyConfig`. Under **Tactics**, untick **Cover** or **Flank** and play again.
10. If anything looks wrong, tell me what you saw. If the Console shows a red line, send me the first one.
