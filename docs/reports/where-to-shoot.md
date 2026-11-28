# Where to shoot a creature (Ralph loop pass 7, 28 Nov 2026)

Matthew asked for a game that is "easily understandable". Every one of the nine creatures has one weak place that kills
it quickest, armour that is the slow way, and pieces it fights with that can be shot off ("one weak place to find and
one strike to read", `Tasks.CreatureTiers`). Nothing said which was which except a dull knock and the blood, and the hit
bell rang the same on the Strider's lid as on its belly, so the game's reward went as much to the shots that did least.
The Pack biter's weak place, its head (65 hit points against its body's 80), even played the armour knock, which said
the opposite of the truth. **Nothing here has been run in Unity, and the new sound has not been listened to: its numbers
are measurements.**

## What changed

| | |
|---|---|
| Problem | A player could not tell a creature's armour from its weak place except by a knock and blood, and the hit bell rewarded both the same. |
| Hypothesis | If every hit on a creature says where it landed in the marks players already learn on people (gold and the bell a fifth up for the place that kills; cyan and the bell a fourth up for a weapon gone), and armour that held gets a small grey flat mark and a dry knock in place of the bell, players find each creature's weak place by shooting at it; one short line, the first time a kind's armour turns their shots, names it for anyone who does not. |
| Principle | Learnable challenge, never hidden rules (3); legible risk and reward (5); managing cognitive load (7): the line comes only to someone doing the less effective thing, at most twice a kind, and never once they have found the weak place; ethical design (9): no bestiary, no completion meter. The critique loop's sound rules: a reward is a bell in D, anything else is never one. |
| Smallest test | Rule tests (`WhereToShootTests`, 10 tests): every kind has exactly one weak place and it is that kind's quickest kill (from the kinds' own hit points); armour that comes off knocks while it holds and is a piece off once broken; the pieces a creature fights with are a weapon off when shot off, fins and wings never; the marker says each (a glance is smaller, thinner, grey, shorter, never pops and never rings the bell, and never cuts a real hit's marker short); every kind with armour has a line of at most 44 letters and the Pack biter has none; the line's memory (only on a glance, until found, at most twice, 40 s apart, kept across sessions, junk read safely). The hit marker's own 7 tests still pass unchanged, so people are marked exactly as before. |
| Acceptance (offline, met) | All of the above, and all 3,199 rule tests pass; the runtime compiles apart from the two known `excludeLayers` errors. The new sound measures 0.12 s, peak -5 dB, centred near 3 kHz with no energy under 500 Hz (the bell is a pure 1.2 kHz, the clock's tick two sine partials near 1.1 kHz). |
| Acceptance (Deck, not yet run) | See "Playtest protocol". |

## Each creature

| Creature | Armour: grey, a knock, no bell | Weak place: gold, the head's bell | Weapon off: cyan, the disarm's bell | The line, the first time its armour turns a shot |
|---|---|---|---|---|
| Strider | its lid (360) | the belly underneath (240) | each claw arm | ARMOUR · AIM UNDER IT, AT THE BELLY |
| Sprinter | its head in front (200) | the abdomen behind (150) | each jaw | ARMOUR · AIM FOR THE ABDOMEN BEHIND |
| Roller | its head (300); plates and tail fan until they break | the soft body under the plates (200) | the plate that leaves it too few to close into a ball | ARMOUR · SHOOT UNDERNEATH WHEN IT FLIPS |
| Cave hunter | its head (220) | the fore body, the three segments behind the head (150) | none: it strikes with its head | ARMOUR · AIM JUST BEHIND THE HEAD |
| Screamer | its fore body (300) | the abdomen (150) | each drum (its scream) | ARMOUR · AIM AT THE ABDOMEN OR SIDE DRUMS |
| Ambusher | its head (200); a jaw until it breaks | the body (90 a length) | each jaw | ARMOUR · AIM FOR THE BODY BEHIND THE HEAD |
| Blind stalker | its skull (280) | the trunk (200) | none: its jaw is not a piece of its own | ARMOUR · AIM FOR THE BODY, NOT THE HEAD |
| Drifting maw | its skull (420) | down its open throat (its body's 320 at 2.5 times: 128) | its jaw | ARMOUR · SHOOT DOWN ITS THROAT WHEN IT OPENS |
| Pack biter | none | its head (65) | its jaw | none: nothing turns a shot |

A creature killed through its weak place gets the headshot kill's marker (red with the gold ring outside) and its
three-bell chime; one killed through its armour, or by a one-hit-kill gun anywhere, the plain kill's two bells. So the
best way to kill each one is also the one that sounds best. A piece that comes off and does not take a weapon (a leg,
a fin, a wing, a tail, the Cave hunter's cut-off rear) is the "limb off" mark as before.

The weapon-off mark is the disarm's look and bell only. It never counts as the "disarm" unlock goal, which is still a
gun shot out of an enemy's hands (`HitOutcome.Disarmed` is untouched; the creature says `HitZone.WeaponOff`).

## The line under the crosshair

`ARMOUR` in the glance's grey, a dot, then where to aim in cream, Overpass Mono 15 px with an outline and a shadow,
78 px under the middle of the screen: inside the damage ring, below the melee TAKEDOWN. It holds 2.7 s and fades in
0.5 s, and its clock stands still while the game is paused or held for a hitstop. When (`Creatures/AimLines`):

- only after a shot glances off that kind's armour, so it answers what the player just did;
- never once the player has hit that kind's weak place, however they found it;
- at most twice a kind ever (a second chance for a line missed in the noise of a fight), and never within 40 s of the
  last line of any kind;
- never with the new-player tips switched off, and forgotten with them when the player asks for the tips again
  (title screen, Extras).

It is remembered in PlayerPrefs (`oa.aim.lines`, for example `Maw=2,Strider=found`), written in memory only, never
flushed mid-fight; Unity writes it on quit, and if the game crashes first the line just shows again. It works the same
in the practice range, where the land creatures live, and in a run.

## How it is built

- `Core/HitZone.cs`: `Plain`, `Armour`, `Vital`, `WeaponOff`, carried by `HitOutcome.Zone`. People leave it `Plain`.
- `Creatures/WhereToShoot.cs` (plain C#): one function per kind from the piece hit and whether it broke, the lines, and
  `AimLines`. The seven `XxxPiece` enums moved from the Unity classes into their plain `XxxLayout.cs` files so the rules
  can name them.
- Each creature's `TakeHit` sets the zone, and its armour knock (`impact.armor`, in the world) now plays exactly when
  the zone is `Armour`, so the knock and the grey mark can never disagree. A walker's claw or jaw counts only if it was
  still on (`Walker.TakesWeapon`); the Roller's charge counts as lost on the plate that takes it below
  `RollerBody.PlatesToRoll`.
- The Pack biter's head no longer plays the armour knock. It bleeds and rings gold.
- `UI/HitConfirm.cs`: `HitKind.Glance` (small, thin, grey, no pop, 0.16 s, below every real hit) and
  `Classify(..., zone)`. `UI/HitMarker.cs` draws it and the line; `Audio/GameAudioHooks.cs` plays the knock in place of
  the bell.
- `hit.glance` (`tools/audio/sfx/combat.py`): a tight crack of noise, a padded thud and a band of noise sliding from
  about 4 kHz to 1.6 kHz (the shot skating off), no partials that ring, 0.12 s, -13 dB, 2 voices, 50 ms cooldown. With
  it the creature's own knock (centred near 500 Hz) plays where the shot landed, so the two read as one knock and scrape.
- `ICreature.Kind` names the kind (its model's name), in the practice range as well as in a run.

## Reviews

(Filled in after the three reviews.)

## Playtest protocol

(Filled in after the design review.)

## Still unverified

- Nothing here has been run in Unity: the grey mark, the line's place and fade, and the sound mixed with the creature's
  knock are read from the code.
- The new sound has never been listened to.
- Whether players learn each creature's weak place from the marks alone, and whether the line helps or distracts in a
  fight, are playtest questions.
