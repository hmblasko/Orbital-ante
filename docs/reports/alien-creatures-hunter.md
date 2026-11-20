# Alien creatures: the Cave hunter (20 Nov 2026)

The fourth of Matthew's nine (his go-ahead of 19 Nov 2026: "Continue to build these, spider crab looks good"): the **Cave hunter** (placeholder name; `Hunter` in code), from the giant centipede. Like the Roller it is not a walker, so it has its own rules.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A centipede six metres long and knee high: a flat rust-red head with two heavy black-pointed fangs and two long feelers, sixteen dark mahogany segments each with a pair of amber legs, and two long thorny legs trailing behind. Pictures: `docs/art/previews/creatures/hunter_*.jpg`.

- **It comes in weaving.** 5.6 m/s: a little quicker than you walk (5), slower than you sprint (8). Its body follows its head, so every turn runs down its length.
- **It rears, then strikes down.** Within reach (2.2 m when whole) it lifts the front third of its body until its head is above yours. That takes 0.75 s. For the first two thirds of that it still follows you; then it draws back to full height and its fangs fly wide open, and from that moment it is aimed at where you stood. The head comes down in a fifth of a second. 22 damage. A step back or aside after the fangs open is a miss (rule test).
- **It pays for a strike.** Hit or miss, its head lies on the ground for 0.8 s, then it runs a loop out and round for about two seconds before it comes in again.
- **It can be shot in two.** Any segment behind the first three that gives way cuts it there: everything from the cut back comes away in pieces with the legs still on, and the head end fights on, shorter. Every segment cut away shortens its reach (2.2 m down to 1 m) and lowers its rear, but it turns quicker. The tail end is cheap to cut (one rifle hit for the last segment), the front dear (three).
- **A cut stops a strike.** Cut while it is reared, it thrashes on the spot for 0.9 s and the strike never comes. It cannot be held still that way: a second cut inside the next few seconds shortens it but does not stop it.
- **What comes off:** thirty-two legs (20 each: one rifle hit), and up to thirteen segments. Each leg lost slows it a little; with none it still wriggles at half pace.
- **It dies two ways:** the soft fore body, the three segments behind the head (150 between them; it bleeds), or the armoured head (220; it knocks, no blood). Cutting never kills. Dead, it comes apart at every joint.

## How it moves (no animation clips)

The head is moved over the ground and every joint of the body is drawn along behind the one in front, over the ground it really covered. To strike, the front is lifted joint by joint in a curve, which pulls the head back as well as up; the strike lays it flat again while the whole animal lunges. Legs step in a wave that runs up the body, opposite sides out of step; lifted off the ground they spread and grasp. The feelers sweep, the fangs chew, the two tail legs trail and sway. Clips: a run with a turn each way, the whole strike, and being shot apart, in the project files.

## What changed

- `Scripts/Runtime/Creatures/`: `HunterLayout`, `HunterBody`, `HunterAnimator`, `HunterBrain` (plain C#), `Hunter` (Unity side); `CreatureConfig.hunter`, `arenaHunters`.
- `tools/blender/build_creatures.py`: the Cave hunter, textured at 2048, and its far copy. `tools/blender/creature_dump`: its scenes (`HunterDump.cs`).
- `Resources/Models/Hunter.bytes`, `HunterFar.bytes`, `Textures/Hunter_*.png`.
- The combat arena starts with one Cave hunter two thirds of the way down, on the left.

## Constitution

- **1, fun core:** a fourth verb: cut it down to size. The Strider is aimed at, the Sprinter moved round, the Roller dodged and punished.
- **2, agency:** kill it through the fore body, trim it from the tail so it has to come closer, or save a cut for the moment it rears.
- **3, learnable challenge:** the rear is the warning and the fangs flying wide are the moment it takes aim; nothing is random and nothing has hidden health (a cut is one to three rifle hits, by where).
- **5, legible risk:** armour knocks and flesh bleeds; every segment cut away is visibly less reach.
- **8, varied intensity:** rush, strike, a still head, a loop away.

## Tested

- 31 rule tests in `HunterRulesTests`: what a cut takes and that it never kills; the two ways it dies; reach and rear going with length; the body following its head at a fixed spacing through hard turns; the rear's height and the strike laying it flat; the lock and the step aside; the still head and the loop; a cut stopping a strike but not twice running; someone circling inside its reach still being reared at; never backing up; legs reaching the ground; the left fang as the right one rolled over; the model against the layout. Whole suite: 2,624 passed, 1 skipped.
- The Unity-side code compiles against Unity reference assemblies.
- The model: 2,244 triangles in the file, 13,370 as drawn (a segment is one mesh drawn sixteen times, a leg thirty-two), FARDRAWN as the far copy; seen from inside 0.00%.

## Agent review

- **unity-reviewer** found no certain defects. Two likely ones, both fixed: a lunge on a long frame could step through thin cover (the whole step is now swept, not just where it ends), and hitboxes trailed the drawn body while it ran, so thin legs would be missed (they are now brought up to date every frame the player can see it).
- **game-designer** found: the armoured head was the quicker kill, which reads the wrong way round (swapped: fore body 150, head 220); reach fell in hidden steps (now a little for every segment); cut right down it lifted too little to show a warning (it always lifts at least two segments); nothing marked the moment it takes aim (it now draws back and its fangs fly wide); it could never catch a walking player (pace 4.6 to 5.6); a leg took two hits where the note said one (now 20); shooting the tail was wasted and the deepest cut was always best (segments now run from one hit at the tail to three at the front); walking round it inside its reach beat its turn (it now swings its head round much faster at close quarters; rule test); cut after cut could hold it still for good (not twice running); blocked by an obstacle it never reared (it does now). It suggested a shorter one should turn quicker as a real trade-off; that is in, and is Matthew's to overrule.
- **deck-performance** found no garbage per frame. It asked that legs riding on a loose segment have their colliders switched off before they are moved (done), and gave the budget line now in `docs/performance_budget.md`. Left as they are until measured: loose segments use the same careful collision as other loose pieces, and it is posed every frame whether or not it is in view.

## Not verified, and not built

- Not run in Unity: whether it appears, how the body following its head looks at speed, whether the hitboxes sit on it while reared, whether 0.75 s is the right warning, frame cost (55 draw calls and 49 moving hitboxes for one animal).
- **Hanging from a cave roof and dropping** (the brief's idea for it) is not built: creatures have no ceilings yet, and it is in the arena only.
- Level ground only. Melee and the rover cannot hurt it. No sounds of its own: the rear borrows an enemy's warning sound, which is the wrong sound and should be replaced before it is judged by ear.
- A cut-off length comes away as separate segments at once; it does not thrash as one piece first.
- Its legs are one curved piece each. In a crowd of thirty-two that reads well; one alone, close up, is plain.
- The bite only hurts: no venom, no knock back.

## Seeing it on the Deck

1. In Desktop Mode open a terminal in `~/Orbital-ante` and run `git fetch`, `git checkout claude/playtest`, `git pull` (after PR 70 is merged there).
2. Open the project in Unity and wait for it to import.
3. Press Play, choose **Combat test arena**.
4. The Cave hunter is two thirds of the way down the arena on the left. Press **H** (D-pad down) to start the fight.
5. Try: stand still and take one strike; then step back as its fangs fly open and shoot the fore body while its head is down; shoot it in half as it rears; trim it from the tail and see how close it has to come.
6. If nothing appears, look in the Console for a line starting "Orbital Ante: the Cave hunter's model" and send it to me.
