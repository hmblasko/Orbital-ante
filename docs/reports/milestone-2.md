# Milestone 2 report - Gibs that feel amazing

Status: **built, not yet run in Unity.** Same position as Milestone 1: the code compiles against Unity reference assemblies and the rule tests pass, but nobody has pressed Play. This milestone is almost entirely about feel, which is exactly the part I cannot check from here. Every number below is a first guess to be tuned with you. Setup steps are in `docs/SETUP.md`.

This sits on the same branch as Milestone 1 (`claude/unity-milestone-1`), so there is one thing to open.

## What changed

- **Over-the-top launch and tumble.** Gibs launch harder than in Milestone 1 (9 m/s along the bullet, 4 m/s of lift, fast random spin). Loose parts get a **bouncy physics material** (bounciness 0.55, and bounce uses the higher of the two surfaces), so they ricochet off the floor and walls instead of thudding.
- **Blood burst at the wound.** A fan of chunky droplets, biased along the bullet. Bigger for a torso burst and a headshot.
- **Trailing spray.** Both sides of the break keep spraying for about a second and a half, tailing off: the loose chunk as it flies, and the stump it left on the body.
- **Blood decals where gibs land.** Each chunk can leave up to 3 splats when it hits the floor or a wall hard enough, with a small splash of droplets and a wet smack. Uses URP Decal Projectors, with a flat-splat fallback (see "Decals" below).
- **Hitstop** when you pop a limb: 50 ms for a limb, 70 ms for a torso, 80 ms for a kill (inside your 40-80 ms range). Overlapping pops do not add up.
- **Small camera shake**, rotation only, at most about 1.6 degrees, scaled by how big the moment was. It never moves the camera out of the head.
- **Pop sound per limb removal.** Five different placeholder sounds generated in code (hand/foot, forearm/shin, thigh/upper arm, torso, head), with a little random pitch so repeats do not sound identical. No audio files.
- **Gibs last 30 s, then fade.** They are **pooled** (24 containers made up front) and **capped at 40 live at once**; when a new one would go over, the oldest fades out quickly.
- **Optional headshot slow-mo.** 0.45 s at 30% speed when a head is destroyed while still on its body.
- **Pause > Feel** page: on/off toggles for hitstop, camera shake and headshot slow-mo, remembered between sessions.
- **Readout** (top left, F3): now also shows live gibs, blood droplets and splats against their caps, and whether splats are "(projectors)" or "(flat)".
- **GoreConfig** asset with every number above, next to the other config assets.
- **Tests**: 9 new PlayMode tests (19 in total). The 19 EditMode rule tests are unchanged.

## Why, and which constitution principle each serves

| Decision | Principle |
|---|---|
| Launch, bounce, blood, pop sound, hitstop and shake all fire on the same frame a limb comes off | 1 Fun core gameplay first |
| Feedback scales with the size of the moment (hand < shin < thigh < torso < kill) | 5 Legible risk/reward; 8 Varied intensity |
| Ordinary hits that remove nothing get no hitstop and no shake | 7 Manage cognitive load; 8 Varied intensity |
| Hitstop, shake and slow-mo can each be switched off by the player | 9 Ethical design (comfort and motion sensitivity) |
| Hard caps on gibs, droplets and splats, shown live on screen | Steam Deck budget (section 2); 10 Evidence-based iteration |
| All values in a `GoreConfig` asset | 10 Evidence-based iteration |

## Evidence-based iteration (principle 10)

- **Problem:** in Milestone 1 a limb coming off is correct but flat. No sound, no mess, no punch.
- **Hypothesis:** stacking six small pieces of feedback on the same frame makes a limb pop satisfying and readable without slowing the fight.
- **Smallest test:** the test arena, 8 mannequins, the test rifle.
- **Acceptance criteria:** you can tell a limb came off without looking at the HUD; a pop makes you want to do it again; nothing feels sluggish or nauseating after two minutes of shooting; the frame-time readout stays steady with gibs at the cap.
- **Playtest protocol:** the checklist at the bottom. Try each Feel toggle off and on so we learn which pieces are pulling their weight.

## Decisions you should know about

1. **The fade is a shrink, not a transparency fade.** Parts use cheap solid materials. A true fade needs transparent materials on every part, which costs more on the Deck and can sort badly. The chunk holds full size, then shrinks away over 1.5 s. If you want a real see-through fade, say so and I will add it.
2. **Decals have two modes.** URP Decal Projectors (what you asked for) need a "Decal" feature switched on in the renderer. The setup tool does that for you, but through a back door, because Unity does not expose it to scripts properly. If that step fails, the game uses flat splat shapes laid on the surface instead and the readout says "(flat)". `docs/SETUP.md` section G has the six manual clicks. Flat splats do not wrap around corners.
3. **Blood is chunky solid droplets**, not soft transparent sprites. It suits the TF2/Gang Beasts look and is cheap. Easy to change if it reads wrong.
4. **Hitstop slows the whole game** to 2% for those few hundredths of a second, including you. That is the usual way to do it and what "hitstop on the shooter" normally means in practice, but tell me if you meant something else, such as only the gun freezing.
5. **Slow-mo is on by default** so that you see it. The spec calls it optional; it is a toggle in Pause > Feel. It triggers only for a head destroyed on a body that still has its torso, not for finishing off a rolling head.
6. **Sounds are synthesised placeholders.** They are meant to be punchy and distinct, not final.
7. Projector decals will also paint onto mannequins and loose parts that are lying on a splat. I think that looks right for blood, but it is untested.

## What I tested

- **Rule tests, actually run** (`dotnet test`, outside Unity): 19 of 19 pass. Unchanged from Milestone 1, re-run to confirm nothing broke.
- **Compile check, actually run**: all runtime scripts, including every Milestone 2 file, compile against Unity engine modules (2021.3) plus the real Input System and Unity UI assemblies, with Unity 6-only names mapped back. The setup tool and all 19 PlayMode tests compile against hand-written stand-ins for the editor and test-runner APIs.
- **PlayMode tests, written but NOT run**: gib cap pushes out the oldest; lifetime, fade and pool reuse; parts are bouncy and actually bounce; a limb pop makes blood and two sprays that stop; splats are capped and clear; a gib hitting the floor leaves a splat; hits report how big they were; hitstop freezes then recovers, followed by slow-mo; hitstop can be switched off.

## What is NOT verified

1. **Nothing has been run in Unity, for either milestone.** A first-open compile error is still possible. If you see red in the Console, send me the first line.
2. **Whether any of it feels good.** Launch speeds, bounce, blood amount, hitstop length, shake strength and the sounds are reasoned guesses.
3. **Decal Projectors.** Both the automatic setup step and the projector code reach URP by name, and I have not seen either work. The flat fallback is there for that reason.
4. **The generated sounds.** I have never heard them. They may be too quiet, too harsh, or too alike.
5. **Blood droplets colliding with the world.** Standard Unity particle collision, but untested here.
6. **Steam Deck performance.** Not measured. The caps are sized conservatively (40 gibs, 500 droplets, 48 splats) and the readout is there so you can read me the numbers.
7. Whether the 9 new PlayMode tests pass. Some depend on physics timing and may need loosening.

## Playtest checklist

**Setup**
- [ ] No red errors in the Console after opening.
- [ ] EditMode tests: 19 green. PlayMode tests: 19 green. (Send me the text of any red one.)
- [ ] Top-left readout shows "gibs 0/40  blood 0/500  splats 0/48" and ends in "(projectors)" or "(flat)". Which one?

**The pop** (shoot limbs off for a couple of minutes)
- [ ] Limbs fly far and tumble. Over the top enough? Too much?
- [ ] Parts bounce off the floor and walls. Fun, or too rubbery?
- [ ] There is a blood burst at the wound on every pop.
- [ ] The flying chunk trails blood, and the stump on the body sprays for a moment.
- [ ] Splats appear where chunks land, on the floor and on walls. Do they look like blood or like stickers?
- [ ] You feel a tiny freeze on each pop. Punchy, or does it make shooting feel laggy?
- [ ] The camera kicks slightly. Noticeable? Too much?
- [ ] Each pop has a sound. Can you tell a hand from a thigh from a head by ear? Any that are unpleasant?
- [ ] Empty a torso: is the burst the biggest moment so far?
- [ ] Headshot a healthy mannequin: brief slow-mo. Cool, or in the way?

**Budget**
- [ ] Wait 30 s after a pop: the chunk shrinks away. Does the shrink look acceptable, or do you want a true fade?
- [ ] Shred all 8 mannequins as fast as you can, press T (D-pad up) and do it again. "gibs" never goes above 40. Did you notice old chunks vanishing early?
- [ ] What do the frame-time numbers say at the worst moment?
- [ ] T clears all gibs, blood and splats.

**Pause > Feel**
- [ ] Each of the three toggles works and shows ON/OFF.
- [ ] They are still set the way you left them after stopping and pressing Play again.
- [ ] With hitstop off, do pops feel worse? With shake off?
- [ ] Pausing in the middle of a hitstop or slow-mo, then resuming, leaves the game at normal speed.

**Overall**
- [ ] Did a pop make you laugh? Which one?
- [ ] Which single piece of feedback would you turn up, and which would you turn down?

## Assets needed from you

None. For Milestone 3, optionally drop a CC0 gun pack into `unity/Assets/ThirdParty/Weapons/` (for example the Quaternius "Ultimate Gun Pack"); without one I will use placeholder shapes with the same sockets. If you would rather have real pop and splat sounds than generated ones, CC0 recordings dropped into `unity/Assets/ThirdParty/Audio/` would replace them.

## Next

Milestone 3 (weapons and disarming). Two milestones are now stacked without a playtest, so the most useful thing you can do is open the project once and tell me what the Console says.
