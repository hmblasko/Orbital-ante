# Hit markers and the hit ding (16 Nov 2026)

Matthew asked: "When hitting enemy players, you should have visual hit markers just like encounters trike, and it should make a super satisfying dinging noise (not annoying) like in team Fortress 2".

## What was wrong before

- The hit marker (four small ticks) lived with the test arena's crosshair, which is hidden in the solar system, so in the real game there was no hit marker at all.
- Every bullet that hit anything that can be hit rang the hit sound, including the ship's own cabin wall, a gun lying on the ground and loose limbs.
- The hit sound was a 70 ms tick, not a ding.

## What changed

- **Marker.** A small X of four thin ticks round the crosshair, each over a soft dark edge so it reads on bright sand or sky. It pops out about a third bigger (half again for kills), settles in 0.06 s, holds, then fades. What the hit was shows in its size and colour:
  - body: white, 0.22 s;
  - head (not yet a kill; a head takes a few rifle hits): gold, 0.28 s;
  - limb off: a bigger white X, 0.32 s;
  - disarm (the gun in their hands): cyan, 0.32 s;
  - kill: a big red X, 0.5 s;
  - headshot kill: the red X with a second gold X outside it, 0.65 s.
  A smaller hit never cuts a kill's X short; a burst of equal hits keeps the X up. It has its own canvas, so it shows outside the ship and in the arena alike.
- **Ding.** Our own small struck bell, made from sine partials in `tools/audio/sfx/combat.py` (`_ding`), not taken from any game. A clear note on D6 (the music's home note), with an octave and two bell-like partials that die first, so it ends on a pure soft tone; a 1.5 ms rise, so it is never a click; heard down by 40 dB in about a third of a second, so automatic fire gives a run of dings, not a wash. Almost nothing above 4 kHz.
  - head: the same bell a fifth up (A6);
  - disarm: a fourth up (G6);
  - kill: "ding-ding", D6 then A6 with a soft low thump;
  - headshot kill: D6 and A6 together, then D7, with a longer ring.
  - Hits closer together than a quarter of a second play quieter (down to 60%), so an SMG burst does not nag; kills always play at full.
- Only hits on living enemies (and a gun in their hands) give a marker or a ding. The music's intensity still hears every hit as before.

## Files

- New: `Scripts/Runtime/UI/HitConfirm.cs` (plain C# rules), `Scripts/Runtime/UI/HitMarker.cs` (the drawing), `Tests/EditMode/HitConfirmTests.cs` (7 tests).
- Changed: `Core/Hit.cs` (`HitOutcome.Head`), `Dismemberment/Mannequin.cs` (sets it), `UI/Hud.cs` (old ticks removed, marker created, hidden with the HUD), `Audio/GameAudioHooks.cs` (ding through the rules), `tools/audio/sfx/combat.py` and the three re-rendered files `hit_marker.wav`, `hit_kill.wav`, `hit_headshot.wav`, the audio manifest, `CLAUDE.md`.
- Offline previews in the project folder `orbital-ante/hud/`: `hit_markers_preview.png` (drawn by `hitmarker_preview.py` with the same numbers; not a Unity screenshot) and `hit_dings_demo.ogg` (the real rendered files played in sequences: three rifle hits; two head hits and a headshot kill; an SMG burst ending in a kill; a disarm; two hits and a kill).

## Constitution

Principle 1 (fun core gameplay): every landed shot gives an instant, pleasant confirmation. Principle 7 (cognitive load): one place to look (the crosshair) and one sound, varied by colour and pitch, not by new symbols. Principle 3 (learnable challenge): the head and the kill are told apart, so the player learns the head takes a few hits. Principle 5 (legible risk/reward): shooting the gun out of a hand is confirmed in its own colour.

## Tested

- `HitConfirmTests`, 7 tests, pass with `dotnet test tools/rules-tests` (only living enemies confirm; each kind looks and sounds different from the body; bigger hits show longer and bigger; the pop settles and the fade never brightens; a smaller hit never cuts a kill short; the burst volume and kills at full). Whole suite: 2,236 passed, 1 skipped.
- All runtime scripts compile against the reference assemblies (only the known Unity 6 name and IHittable clash errors).
- Sound measured, not heard: the three files peak at -4 dBFS; the hit ding's spectral centre is about 1.75 kHz with under 1% of its energy above 4 kHz; loudness after the event's volume is within about 1.5 dB of the old sounds (hit ding about -28 LUFS against the old tick's -28).

## Not verified (needs Unity on the Deck)

- Nothing here has been run in Unity. Whether the X is the right size on the Deck's screen, whether it reads against snow and bright sky, and whether the ding is satisfying rather than annoying after ten minutes of fighting, are all for Matthew to judge.
- The ding has not been listened to by anyone: it was designed and measured, not heard.
- The solar game's crosshair is the ship HUD's aim dot; the X is centred on the screen like it, but has not been seen together with it.

Playtest protocol (principle 10): problem, hits on enemies were not confirmed in the real game and the sound was a tick; hypothesis, a short popping X plus a soft bell, coloured and pitched by what the hit did, makes every hit feel landed and tells head from body without words; smallest test, outside on Mars, fight three troopers with the rifle and then the SMG; acceptance, every hit is noticed, head hits and kills are told apart without looking at the enemy, and after five minutes the ding is still pleasant (if it is not, turn its event volume down or shorten its decay in `_ding`).

## Agent review

Not run: the specialist agents run on Fable 5.2, whose usage limit resets on 19 Nov 2026. Checked by hand for the usual Unity traps instead: the marker subscribes in `OnEnable` and unsubscribes in `OnDisable`; alpha goes on the canvas renderers (no layout rebuild); nothing allocates per frame; layout changes only when the kind of hit changes; times use unscaled time, so hitstop and slow motion do not freeze the marker.
