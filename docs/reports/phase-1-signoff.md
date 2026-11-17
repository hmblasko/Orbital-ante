# Phase 1 sign-off: playtest checklist

16 Nov 2026. For Matthew, on the Steam Deck.

This is the gate at the end of Phase 1 (build prompt section 15). Nothing in Phase 2 starts until you say Phase 1 is good. For each line, play it and mark **Good**, **Not yet**, or a note in your own words. Nobody else fills these in. Anything marked "Not yet" gets refined before Phase 2.

Nothing below has been played by Claude: there is no Unity in the cloud. The automatic checks are step 3 of "Getting the build".

## Getting the build

1. Open Konsole in Desktop Mode.
2. Type `cd ~/Orbital-ante`, then `git fetch`, `git checkout claude/playtest` and `git pull`.
3. Run `tools/check-in-unity.sh`. It sets the project up and runs every automatic test, and takes a while. When it finishes, open `unity/Logs/oa-check/summary.txt` and send me the counts, or the whole file if anything failed.
4. Open the project in Unity Hub, open the solar system scene and press Play.
5. To measure speed, press F3 in game. The top line says whether the frame time is OK.

I can't make a finished Linux build from the cloud. When you'd like one, I'll write the steps for File > Build Profiles.

## 1. Solar system (Milestone 5)

- [ ] From orbit, the planets and moons visibly move along their orbits and turn. The map shows them moving.
- [ ] Fly from Earth to the Moon. It feels grand but never drags.
- [ ] Gravity gets stronger as you get closer to a world, and leaving a world's pull is never a jolt.
- [ ] Start a new run with a new seed: the planets are in different places along their orbits.

## 2. Planets (Milestone 6)

- [ ] Land on Earth, the Moon and Mars. Each one can be recognised at a glance.
- [ ] Mars has an orange haze, Earth a light blue one, and the Moon none.
- [ ] The ground is hilly and worn, with valleys, crags and craters, not flat plains. The colours look natural.
- [ ] Rocks, trees and other features are scattered differently on different worlds.
- [ ] A gas giant can be flown into: the sky changes inside and there is a surface to land on.
- [ ] Neptune is an ocean to explore.
- [ ] Find a cave network (karst on Earth, lava tubes on the Moon or Mars, ice caves on Ganymede). It has several rooms, passages you can walk, and a shaft only the jetpack can climb.
- [ ] Weather shows on the worlds that have it.
- [ ] Landing anywhere gives you something you want to go and look at.

## 3. Ship design (Milestone 7)

- [ ] Walk round the cabin. It feels cosy, like a real little spacecraft: warm lamps, a dusk-blue dome and space hardware. You never snag on anything or fall through.
- [ ] Walk under the ship: the blue ring and lift pull you in, and the hatch closes behind you.
- [ ] Look at the hatch inside and press the button: it opens and closes, in flight too.
- [ ] Sit down: you see the seatbelt buckle in first person and hear the hydraulic sound.
- [ ] Shoot the glass. It cracks and breaks after several hits, not one. Mend it.
- [ ] Crash hard enough that a part tears off. It stays off. Smaller damage can be mended.

## 4. Launch (Milestone 8)

- [ ] A run starts seated on the pad at the launch base, with no flags anywhere.
- [ ] Take off: the engines build up, and you get smoke, grit, glow, shake and a heavy roar.
- [ ] Do it ten times. It still gives a little rush.

## 5. Cockpit view (Milestone 9)

- [ ] On the Flight deck dash, mid-flight, you can read direction, speed, fuel and air at a glance.
- [ ] The dash is never in the way of the front window.
- [ ] The console's own fuel tube, air needle, lamps and throttle lever move with what is happening.
- [ ] Look down at the screen: it takes over the view, and you can switch between the landing camera and the two pole cameras.
- [ ] The cockpit looks great.

## 6. Ship navigation (Milestone 10)

- [ ] Flying has real inertia: let go and you keep drifting.
- [ ] Lock a moon, use the autopilot, and match velocity. Then unbuckle and walk the cabin while it flies.
- [ ] Land on a spinning moon. It feels like an achievement, not a fight with the controls.
- [ ] Every push and turn shows flame from the right thrusters.
- [ ] Crash: the ship bounces and sometimes breaks. Crash unbuckled: you can die, in first person, with the cause in plain words.
- [ ] Floodlights come on by themselves in the dark. L or Select switches them.

## 7. Jetpack navigation (Milestone 11)

- [ ] Step out of the ship in orbit. Floating feels free, precise and a little scary.
- [ ] Each jet direction lights its own nozzles, and you hear a soft gas rush.
- [ ] Match Velocity (F or left trigger) stops you against the ship or the ground, and says which.
- [ ] The tether stops you at 40 m and never reels you in.
- [ ] Leave a moving ship, fly to a damaged part and mend it.
- [ ] Crouch (tap Jets Down), lie prone (hold it) and sneak (Left Alt): enemies that haven't seen you notice you later.

## 8. Combat and gibbing (Milestones 1 to 3 and 12)

- [ ] **Arena.** Rifle at 10 m: an arm comes off in 1 to 3 hits, and a thigh takes the shin and foot with it. You get a pop, a hit marker with a ding, a short hitstop, a small shake and a burst of blood.
- [ ] **Head only.** Shoot off everything but the head. It is still alive, rolls slowly and cannot shoot. Only destroying the head kills.
- [ ] **Losing legs.** With one leg gone the enemy crawls, pushing with the leg that is left. With both legs gone it crawls on its arms. With no arms and no legs it wriggles.
- [ ] **Disarming.** Shoot the gun hand, then a forearm, then the gun itself. Each time the gun drops, the enemy looks startled and scrambles for it. With one hand its aim is visibly worse. With no hands it cannot fire.
- [ ] **Re-splitting.** Shoot a loose leg in the air and again on the ground. It splits again and bleeds.
- [ ] **Earth.** Fight a camp. Limbs bounce and settle on slopes, and blood marks rock and base walls. Nothing sinks into the ground or hangs in the air.
- [ ] **Moon.** Limbs arc long and slow, and nothing disappears in mid-flight.
- [ ] **Orbit.** On a spacewalk by the ship, drop your gun. It drifts along with the ship. Blood floats as a cloud instead of raining toward the planet.
- [ ] **Moving ship.** Shoot the hull from outside, then fly off. The bullet holes stay on the hull.
- [ ] **Re-centre.** Fight, travel more than 2 km and come back. Dropped guns and limbs are where they fell.
- [ ] **Caves.** Limbs and guns rest on the cave floor. In the dark you are seen from closer.
- [ ] **Big fight.** Gib a whole base garrison. The frame rate holds, the oldest pieces clear first, and the "rescues" count on F3 does not climb.
- [ ] **Gun ladder.** Make 22 kills. Each step hands you the next gun with a banner, Switch Gun (1 or D-pad up) cycles your guns, and picking up a gun from the ground swaps only the one in your hand.
- [ ] **Wildcard.** Find it. The warning says it can blow up, one hit kills, and a blow-up ends the run in first person.
- [ ] **Death.** Die outside and aboard. Your body bursts, the view stays in the head, and the death page names the cause.
- [ ] **Reading a fight.** From 20 to 30 m, without pausing, you can name a limb pop, a disarm or a leg drop the moment it happens.
- [ ] **Laughs.** Did each fight give you a moment worth laughing about? Yes or no, per fight.

## 9. Everywhere

- [ ] The game never goes to third person, not even when crashing or dying.
- [ ] The pause menu (Esc or Start) works with the Deck's controls: rebinding, restart with the same or a new seed, and quit.
- [ ] Rebound controls are still there after quitting and starting again.
- [ ] The frame rate on the Deck is steady in flight, on the ground and in a big fight.

## Things that differ from the build prompt, all at your request

Please confirm each one stays, or tell me to change it:

1. You can carry the guns the ladder has given you and switch between them. The prompt says "one gun at a time". You asked for carrying on 16 Nov.
2. Some Phase 2 items were built early at your request: crouching, prone and sneaking; guards at bases; the gun ladder; and the rare Wildcard gun.
3. With one leg gone an enemy crawls instead of hopping. You asked for this on 16 Nov.
4. The look follows "more realistic, not cartoony" from 14 Nov, which takes the place of the prompt's "TF2 / Gang Beasts" look. The humour stays in the gibs, sounds and UI.

## Still to do in Phase 1

- Stop limbs coming up the lift into the cabin.
- A metal sound for hull hits.
- Timings on the Deck. `docs/performance_budget.md` says what to read off in the Profiler.
