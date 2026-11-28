# A playtest log for the Deck (Ralph loop pass 9, 28 Nov 2026)

Matthew asked for a game that is "easily understandable". Only people playing it can say whether it is. Every pass of
the Ralph loop so far has ended in a playtest protocol that says what to count: each creature's warning, whether the
player moved on its mark and whether it hurt (pass 2); what the music did (pass 3); the unlock moments (passes 1 and
5); where creatures were hit (pass 7); and every animal met on the worlds and how its fight ended (pass 8). The last
two said it outright: "The game has no playtest log yet. Until it does, record the screen and count from the video."
The earlier ones left the counting to whoever watched. Counting a dozen warnings from a video is slow and easy to get
wrong.

This pass adds the log. The editor and development builds keep, for each run, the facts those protocols ask for, and
write them to a plain text file when the run ends. A release build keeps nothing and writes nothing. **Nothing here
has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | The playtests the loop is waiting for asked testers (or Matthew) to count things the game already knows: a warning's mark, whether the player stepped aside on it, whether its strike hurt, which piece a shot hit, whether a kill paid, how long the music sat in each band. Counting them by eye from a recording is slow, misses what happens off screen, and would make the first Deck session's numbers less trustworthy than the code that produced them. |
| Hypothesis | If every run in a development build writes what happened (counts and times, never a judgment), then a Deck session needs only the testers' own answers to the protocols' questions, and the counts come from the file. A later Claude can read the file and count the protocols' rows without guessing. |
| Principle | Evidence-based iteration (10): every protocol's counts come from the game itself. The reporting rule (never invent playtest results): the file says what happened and nothing about whether it was fun. Ethical design (9): a release build has no recorder and writes nothing, so nothing is kept about players. |
| Smallest test | `PlaytestLogTests` (47 rule tests) and the cloud compile check. |
| Acceptance (offline, met) | `dotnet test tools/rules-tests`: 3,269 run, 3,268 passed, 1 skipped (the cabin bake check, as in every cloud run). The runtime compiles apart from the two known `excludeLayers` errors in `PlayerBurstFx`. |
| Acceptance (Deck, not yet run) | See "Checking the log against a recording". |

## How to get the file (Matthew)

1. Play a run in the Unity editor (press Play) or in a development build (File > Build Profiles, tick **Development
   Build**, then Build). A normal build keeps no log.
2. Play as usual. The log is written by itself when the run ends: you die, the run-over page shows, you restart, you go
   back to the title screen, or you quit (in the editor, stopping Play counts). Pausing (Esc, or Start on the Deck)
   also writes what there is so far, at most once every 30 seconds, so a crash or the Deck going to sleep loses little.
   On a keyboard, **F7** writes it at any moment.
3. Find it:
   - In the editor: `~/Orbital-ante/unity/Logs/Playtest/latest.txt` is always the newest run. Each run also has its own
     file beside it, named by when it began, for example `run_2026-11-28_100215_solar-system_seed12345.txt`. The 40
     newest runs are kept and older ones are deleted. The `Logs` folder is never committed.
   - In a development build: the same files in the game's data folder, under `Playtest`. On SteamOS that is usually
     `~/.config/unity3d/DefaultCompany/Orbital Ante/Playtest/` (the middle part is the company name in Player
     Settings).
   - Either way, **F3** (the debug readout) shows the exact path on its last lines, with how many events the run's log
     holds so far. Unity's own log (the Console in the editor) also says where each file was written.
4. To copy it off the Deck in Desktop Mode:
   1. Open **Dolphin** (the file manager) from the taskbar.
   2. Press **Ctrl+L**, type `~/.config/unity3d/DefaultCompany/Orbital Ante/Playtest/` and press Enter. (If the folder
      is not there, read the path off F3 instead.)
   3. Right-click `latest.txt` and choose **Copy**, then paste it somewhere easy such as **Desktop**.
   4. Attach the file in the project chat (or open it and paste the text). It is plain text, about 100 KB to 1 MB for
      a run.

## What the file holds

The run first: where, the seed, when it started, the build, the kit and the upgrade in use, which run of the profile it
is, how many unlocks it holds and which looks are in use, whether new-player tips were on, how often and for how long
the game was paused, how long the run lasted and how it ended, with the death page's own words. Then one section for
each protocol, then every warning one row each, then every event in order. A section with nothing in it is left out.
Counts only, never percentages.

**Two clocks.** Each line's time is minutes and seconds of real time from the run's start, pauses included, so it
lines up with a screen recording started with the run. How long things took (a kill from the first hit, a dodge) and
the windows the log uses (a strike's grace, a fight's hold) are game seconds, which stop for a pause and slow with
Focus and a hitstop, as the creatures' and the readout's own clocks do. So Focus never stretches a time to kill, and a
pause never closes a window. The file says which is which wherever a game time is printed ("+1.4 s", "of game time").

| Section | For | What it counts |
|---|---|---|
| ANTES | the loop | when each ante began and its target; when ante 1's clock really started (it waits on the pad); overtime; when the target was met and cleared, with the seconds left on the clock each time; the perks taken |
| POINTS | pass 3 | every award by what paid it (the "+points" label; a creature's kill under "creature" and its kind), refusals by why, and the notes the readout really showed beside those it was offered |
| UNLOCKS | passes 1 and 5 | each goal met (in a fight or at the ante break) and when its moment was shown, with how long it waited |
| CREATURE WARNINGS | pass 2 | per kind: warnings, marked (it took aim, its mark sounded), hurt you, missed (it struck and missed), stopped (it took aim and never struck), killed during it, called off before its mark, still under way; hurts from a creature with no warning of its kind under way |
| THE DODGE | pass 2 | per kind, for marked warnings: whether the player was already moving across as it took aim, began moving across on its mark (after the mark, before the strike), only after the strike, or not at all; where it was at its mark (ahead, to the side, behind), whether it was inside the screen's edges, whether the player turned to face it, and shots fired while it warned or struck |
| HITS ON CREATURES | pass 7 | per kind: hits on armour that held, on the weak place, weapons shot off, plain hits; kills, kills through the weak place, aim lines shown; and where the aim line lay when it showed (over the creature, over its weak place) |
| CREATURES MET | pass 8 | a count table per kind (met, in view, after you, shot, killed by you, killed you, put away, still there), the kills on the worlds that paid nothing, then one entry per animal (or pack): its world and ante, when it woke and how far off, when it first came into view and from how far, when it was first after the player, whether people or the creature came first in its fight, its warnings, the hurt it did, its hits by zone, how it ended (killed by you through which piece, how long after the first hit and what the kill paid; killed you; put away when you left it; still there), how often a walker's feet slipped, and the frames around its wake |
| PEOPLE AND GUNS | pass 3, the loop | hits on people (heads, limbs off, guns shot out of hands, kills, kills through the head), times an enemy picked the player out (and how many while hidden), restless squads sent, alarms raised, and per gun the shots, hits and kills |
| YOUR HURT AND DEATHS | passes 2 and 8 | the player's armour lost by cause (the death page's words; shots and anything else together) and every death; in the practice range it says knock-downs, because nobody dies there |
| MUSIC | pass 3 | time in each band (paused time not counted), how often the band changed, how often it thinned on the last plates, the most layers playing at once, which layers came in and went, whether fight music or "someone after you" was on, the pieces in order, and the bar each fight came in on |
| EVERY WARNING | pass 2 | one row a warning: the creature, when it began, and the game seconds after that to its mark, its strike and its end, how it ended, the dodge, where it was at its mark and the shots |
| EVERY EVENT | all | every line above in the order it happened, with its time (at most 6,000 lines a run). A creature's first hit is listed, and every later hit that took a piece off, found its weak place or killed it; hits that only struck it are counted under HITS ON CREATURES |

### The dodge

Pass 2's protocol asks whether players "move on the mark". The log checks it every frame while a warning is open (four
times a second would be too coarse: a Sprinter's bite lands 0.39 s after its mark). It takes how the player moved
against the creature (the eye's place minus the creature's, in the space the creature stands in, so a re-centre of the
world and the world's own turning cancel out), along the ground (what is along "up" is left out: away from the world's
middle on a world, straight up in the practice range, nothing for a swimmer, where across the water counts every way),
and only what is across the line between the two. It smooths that over 0.08 s so a footstep's bob or one long frame is
not a move (`DodgeRules`). Over 2 m/s across counts as moving out of its way. That is a first guess: walking is 5 m/s,
a crouch 2.75, sneaking 2.25 and prone 1.1, so a deliberate step aside from a walk or a crouch passes it, and standing
still, turning on the spot and crawling prone do not. The first frame over it is kept, against the mark and the strike.

### When a creature counts as seen

"In view" is the first time the creature was inside the screen's edges, under 400 m off, and on a world not hidden by
the ground. The line from the eye to it is checked every 2 m (at most 64 points) against the ground really built
(`PlanetSurface.GroundRadius`), with half a metre of give (`SightLine`). That catches the horizon and hills: the
design review measured that a Strider's top drops below a standing eye's horizon at about 49 m on the Moon, and
animals wake 70 to 110 m off. Rocks, trees, buildings, night, fog and water are not checked, so "in view" still means
"could have been seen", not "was seen". Swimmers and the practice range are not checked against the ground.

### How a hurt is put down to a creature

A creature's strike that hurts the player names itself in the death page's words (`StrikeWords`), and those words name
the kind (`StrikeWords.KindOf`). The hurt goes to the newest warning of that kind that has given its mark and is still
under way, or ended within a game second (`HurtGrace`): a strike always comes after its mark, so a fresh wind-up begun
the frame the last strike landed never takes that strike's hurt. Only the two that hold on (the Ambusher and the
Drifting maw) go on biting after their warning has ended: their later bites go to the creature whose marked warning
ended last, within 15 game seconds (`HoldGrace`). Anything else is counted as a hurt with no warning under way. A
warning that winds up again straight after a strike, or after letting go of its aim, is a new warning, as the visor
tells it (`WindupChange.Began`). A creature killed during its warning stops thinking, so the log ends that warning
itself, as "killed during it". A creature put away mid-warning, or a pack whose last fish has died, has its warning
closed then too.

### What a kill paid, and which notes showed

A creature's kill is matched to the ante's own answer by its claim (`TaskIds.Creature`, `TaskIds.LandCreature`), heard
before or after the kill, so the file says what the ante really paid or why it refused, never what the log expected.
A kill with no claim is one the practice range gave no credit for. A kill the creature could not answer ("it could not
answer you there") is told from one the ante refused for another reason.

The readout's notes are counted twice: when they are offered (`AnteLoop.Note`) and when one really shows
(`NoteBoard.Showings`), because a plain note cannot write over an important one for 3 s and most offers never show.

### Packs and the practice range

A Pack biter pack is one entry. Each fish knows its shoal (`Biter.Shoal`); the fish's warnings, hits, bites and kill
are its own and add to the pack's; when its last fish dies the shoal says so (`BiterShoal.Gone`) and the pack ends as
"all killed". In the practice range every creature is met as it is laid out (`PlaytestHooks.LaidOut`, called by
`TestArena.SpawnCreatures`), so its "met" and "in view" times are real; when the range is laid out again the old ones
are put away as "cleared".

### What the log cannot tell

It says what happened, not what the player understood or felt, so the protocols' questions to testers stay as they
are. Some of its counts are also cruder than they look:

- "In view" (above) is "could have been seen": things on the ground, night and water are not checked.
- "Ahead", "to the side" and "behind" are the angle between where the player looked and the creature at the mark.
  They do not say whether the player saw or heard the warning.
- "Moved across on its mark" is a speed threshold, a first guess. It does not say the player moved because of the mark.
- "After you" is the creature's own notice (`AwareOfPlayer`, which needs the means to hurt), read four times a second,
  for the walkers, the Roller, the Screamer, the Drifting maw, a Pack biter pack, the Ambusher (rearing, striking,
  holding or tunnelling after footsteps) and the Blind stalker (stalking, rushing, gaping or lunging). The Cave hunter
  is not read: it only becomes "in a fight" by warning or being hit.
- "People came first" means an armed enemy had the player in sight in the 6 seconds before the creature's fight began
  (`ReadoutFocus.FightHold`); "an enemy saw you during its fight" means while it had been after them within that time.
  Neither says one came because of the other.
- Frame times in a development build are slower and less even than in a release build. The frames around a wake (the
  wake frame itself, the longest in the half second after, and the middle frame of the second before) are a sign to
  look into, not a measurement for the budget. A wake with a pause or a write of this log in that half second is "not
  timed", and the first wake of each kind in a session is marked, because it draws that kind for the first time.

## How it is built

Plain C# (rule-tested, no Unity types):

- `Playtest/PlaytestRules.cs`: `Moment` (the two clocks), `DodgeRules` (speed across the line, smoothing, the first
  guess), `SightLine` (ground between the eye and a thing on a world) and `FrameTimes` (the last 64 frames, for the
  baseline beside a wake).
- `Playtest/PlaytestLog.cs`: the run's records (`AnimalRecord`, `WarningRecord`, `AnteRecord`, tallies) and the event
  lines. Bounded: at most 6,000 lines, 400 creatures, 2,000 warnings and 60 names in each tally (the rest as "other");
  the events that come many times a second (hits, a warning's stages, hurts) check the cap before their words are
  built, and what is dropped is counted and said in the file.
- `Playtest/PlaytestReport.cs`: the text, section by section. It reads the same each time it is written.
- `Playtest/PlaytestFiles.cs`: the file names, `latest.txt` (a copy of the run's file) and keeping the 40 newest runs.
- `Creatures/StrikeWords.KindOf` and `WindupChange.Was`, `Now` and `Changed` (`Creatures/Windups.cs`).

Unity side, `Playtest/PlaytestRecorder.cs`:

- `PlaytestHooks`: the static calls the game makes. `Log` is null in a release build (there is no recorder), outside a
  run and once a run has ended, and every hook returns at once when it is. A write is wrapped in the profiler marker
  `OA.Playtest.Write`.
- `PlaytestRecorder` (on the game's root, added in `GameBootstrap.Awake` only when `Application.isEditor ||
  Debug.isDebugBuild`): hears the player's hits (`CombatEvents.PlayerHitSomething`), sightings
  (`CombatWorld.PlayerSpotted`), restless squads (`CombatWorld.SquadSent`), alarms (`CombatWorld.Alarm`), pauses
  (`GameSession.PauseChanged`) and hurts (`PlayerHealth.Damaged` with `LastCause`, counted as armour really lost);
  every frame watches the antes and each open warning's dodge, and times a fresh wake; four times a second reads the
  watched creatures' notice and whether they are in view, the aim line's place (five rays along the line while it
  shows), the music (`AudioDirector`: band, piece, layers, thinning, fight music, someone after you, and the bar a
  fight came in on) and whether an armed enemy saw the player (`CombatWorld.PlayerLastSeenAt`); F7 and pausing write.
  A death the burst has not yet handed to the death page is still logged as a death if the run is left during it.

The hooks, one line each: `GameBootstrap` (a run begins and ends; a death with the page's words; quitting),
`PauseMenu.ShowRunOver`, `AnteLoop.Report`, `Note` and `ShowNote`, the unlock moments (`AnteLoop.Unlocks.cs`),
`HitMarker.Learn` (the aim line, with the creature it named; `HitMarker.AimLineSpan` says where it lies on screen),
`WindupTell.Tell` (every creature's warning stages), `LandCreatures` and `SeaCreatures` (wakes and put-aways),
`TestArena.SpawnCreatures` (the practice range laid out), `PlayerWeapons` (a shot fired) and `Hud` (the F3 lines).
`Walker.FootSlips` reads its animator's slips, `Biter.Shoal` names a fish's pack and `NoteBoard.Showings` counts the
notes shown.

What it costs is in `docs/performance_budget.md`, "Playtest log": a release build pays one null check per hook; a
development build pays a few hundred nanoseconds a frame for each warning under way and up to a few milliseconds when
the file is written, on a pause, a death or a restart.

## Tests

`PlaytestLogTests` (47):

- Words: every strike's words name a kind the log knows, every kind has strike words, and words that are not a strike
  name nothing; every kind is named for its animal; a `WindupChange` says what it was and is; facing words; the clock
  is minutes, seconds and tenths, never rounded up.
- Warnings: one that hurts (with where the creature was at its mark), one called off before its mark, one that strikes
  and misses, one that takes aim and never strikes, one aimed from the start and one ended by a kill each end the
  right way; a wind-up straight after a strike is a new warning and the strike's hurt stays with the first; one that
  lets go of its aim and winds up again is counted as the visor tells it; a hurt just after a strike counts for it, a
  hold's later bites go to the creature and later ones to nobody, and only the kinds that hold on bite after their
  warning; a strike is only put down to a warning of its own kind; a shot's hurt is never a creature's; the graces are
  game time, so a pause or Focus never stretches them; a warning put away, or whose pack is gone, is closed and its
  objects let go.
- The dodge: the speed across the line leaves out what is along it and what is up; it is smoothed, so a footstep or
  one long frame is not a move; the four outcomes (already moving, on its mark, only after the strike, not at all);
  where it was at its mark and the shots during it are kept.
- Creatures: hits counted by where they landed, with the kill's piece and pay; a creature killed once; each kind
  numbered in the order met; a pack's fish add to the pack, which is met with its first fish, in the practice range
  too, and a fish's bite and kill are the pack's; an animal woken, seen, after you and put away; whether people or the
  creature came first; an enemy's sight joins only a fight under way; a death by a strike marks the creature and a
  shot marks none; the aim line marks the creature it named and keeps where it lay; a creature's pay is the ante's own
  answer, heard before or after its kill; the sight line is hidden by the curve of a world and by a hill (on a world
  of 1,000 m, clear at 80 m and hidden at 150 m); the frame times give the middle of the last second or so; a wake
  that could not be timed says so.
- The run: points tallied by what paid and refusals by why; the note board counts what it shows, not what it is
  offered; the antes say when each began, met its target, went into overtime and was cleared; pauses are counted and
  timed on the real clock; the music's bands without paused time, and without counting a second twice after an early
  write; its fight entries and layers are kept; a run ends once; the caps, with what was dropped counted.
- The file: the report has a section for each protocol and counts, never percentages; an empty run still writes; it
  reads the same each time; a run's file is named by when it began; writing keeps the run and the latest copy and
  trims the oldest runs (and nothing else).

## Reviews

Three reviews read the change before it was committed (Unity, design, Steam Deck cost). None of them ran Unity. Every
finding below was applied unless it says otherwise.

**Unity review** (the unity-reviewer agent):

1. Pack biters were never grouped: the log looked for a fish's shoal among its parents, but the fish stand beside it.
   **Applied:** each fish keeps its shoal (`Biter.Shoal`), and a pack whose last fish dies ends from `BiterShoal.Gone`,
   because it destroys itself without being put away.
2. A death could be logged as "left the run": the death page comes 2.6 s after the burst, and a pause and Restart in
   that time ended the run first. **Applied:** a run left while the player is dead is logged as a death, with the
   words "before the death page came up".
3. "An aim line showed in its fight" missed the usual case, because the marker hears a hit before the recorder does.
   **Applied:** the aim line's hook is given the creature it named.
4. "Missed" also counted strikes that never came, and a creature put away mid-warning stayed "under way".
   **Applied:** a strike is needed for "missed" ("stopped" counts the rest), and putting away closes the warning.
5. F10 is the menu key on Windows in the editor. **Applied:** F7.
6. The log held every creature it met for the whole run. **Applied:** one put away is let go; a killed one is kept
   to the end (at most 400).
7. No row in the budget. **Applied:** see `docs/performance_budget.md`.

**Design review** (the game-designer agent):

1. The dodge itself was not counted, and a poll four times a second is too coarse for it. **Applied:** "The dodge",
   every frame, with where it was at its mark, whether it was on the screen, turning to face it and shots.
2. "In view" counted animals over the horizon. **Applied:** the sight line (above).
3. "A camp joined" only meant "an enemy saw you", in either order. **Applied:** whether people or the creature came
   first is kept, and the words say only what was seen.
4. "Left alone" mixed animals never met, ones walked round and ones that killed the player unshot. **Applied:** the
   count table per kind.
5. One clock made Focus stretch the time to kill and shrink the fight's hold, and the ante times did not match the
   ante clock. **Applied:** the two clocks, the paused total, the seconds left at each target and clear, and when ante
   1's clock really started.
6. Practice range creatures were met late, at their first warning or hit. **Applied:** `PlaytestHooks.LaidOut`.
7. "Pays toward the ante" was the log's prediction, and notes were counted when offered, not shown. **Applied:** pay
   from the ante's own answer; notes shown and offered counted apart.
8. The music logged only the band asked for, ahead of what is heard. **Applied:** the layers by name, fight music,
   someone after you, each fight's entry bar, and alarms.
9. The hold's grace applied to every kind, so a Roller's butt after a warning could count for it. **Applied:** only
   the Ambusher and the Drifting maw.
10. A wake's frame had nothing to be told against. **Applied:** the middle frame of the second before.
11. Smaller ones, all applied: the run's number, unlocks held and looks in use in the header; perks at each ante;
    creature kills under their own POINTS key; hurt counted as armour really lost; knock-downs in the practice range;
    a legend for the facing columns; long animal lines split; one row a warning; these numbered steps for the file.
    The review's open question from pass 7, whether the aim line covers the piece it names, is answered by the five
    rays along the line.

Its re-score of line 2 (easily understandable): 7 before, 8 on paper with findings 1 and 2 applied. The next biggest
gap that can be closed in the cloud: creatures and the camps noticing each other (gap 12).

**Steam Deck cost review** (the deck-performance agent; its numbers are an off-engine bench in .NET 8 on a 2.1 GHz
Xeon, guessed at 2 to 4 times slower on the Deck):

1. A wake's longest frame counted pauses and this log's own writes. **Applied:** a wake with either in its half second
   is "not timed".
2. The longest frame is an upper bound (a camp waking, a tile built, a first draw of a kind all land in the same half
   second). **Applied:** the wake frame's own time beside it, and the first wake of each kind marked.
3. Building the report took up to 19 ms and 3.2 MB at the caps, half of it walking every warning for every animal.
   **Applied:** one pass over the warnings; `latest.txt` is a copy of the run's file, not a second encoding; the
   profiler marker `OA.Playtest.Write`. Measured again after the changes (the report now has more in it): 1.7 ms and
   1.0 MB a typical run, 7 to 15 ms and 7 MB at the caps.
4. Each creature hit made 0.7 KB of garbage, about 10 to 20 KB a second firing full-auto into a lid, and a full log
   still built the words it dropped. **Applied:** hits, a warning's stages and hurts check the cap before their words
   are built, and a hit that only struck (after the first on that animal) is counted with no line: 0.07 microseconds
   and no garbage, measured again.
5. Put-away creatures were kept in memory to the run's end. **Applied** (as Unity finding 6).
6. Two hooks in `AnteLoop` worked out their arguments before the null check. **Applied:** guarded like the rest.
7. In play otherwise fine: a few reads a frame, the polls under 0.05 ms, about 1.3 MB at the caps.

## Checking the log against a recording

Not run: nothing here is a playtest result.

The log has to be checked once against what really happened before its counts are trusted:

- **Setup:** one run on the Deck in a development build, recorded (Steam's recording or OBS), started at the same
  moment as the run. Land on the Moon and on Mars; fight at least one animal on each, let at least one warning hit,
  and step aside on at least one mark. Pause once in a fight and use Focus once. Then one session in the practice
  range with the Strider, the Roller and a Pack biter pack.
- **Compare:** every warning in the file shows its mark in the video at that time and its strike hurting (or not) as
  the file says; every step aside the file calls "on its mark" is a step aside in the video after the mark; every hit
  on a creature in the file has a marker of the same kind on screen; every "in view" is about when the animal first
  shows; the music changes about when the file says; the paused time matches the pause.
- **Acceptance:** every warning, hit and death in the video is in the file at the right real time (within half a
  second), and nothing in the file did not happen. If a dodge the video shows plainly is "not across" in the file (or
  standing still is "on its mark"), the 2 m/s guess is wrong and is the first thing to change. Any other disagreement
  is a bug in the log to fix before the first counted playtest.

From then on the earlier passes' protocols are counted from the file, and their "record the screen and count from the
video" becomes a check, not the count.

## Still unverified

- Nothing here has been run in Unity: that the recorder is added in development builds and not in release builds,
  that every hook fires where it should, the file paths on the Deck, F7, and the costs (estimated in the budget, not
  measured in Unity).
- Whether the log's counts match a recording (above), and whether 2 m/s is the right line for a dodge.
- "In view" does not check rocks, trees, buildings, night or water; the Cave hunter's notice is not read.
- Not built: a log of the seat (flight, landings and crashes are only in the points and the music), the suit's air and
  jet fuel over time, the restless camps' quiet clock, and the envelope's reveal. Each can be added as a hook when a
  protocol asks for it.
