# Milestone 7 report - Ship design

Status: **built, not yet run in Unity.** Same position as Milestones 1 to 6. The ship's rules (walking about the cabin, damage and mending, glass, the hatch and lift, the seat belt, every shape, the baked light) are plain maths and are tested here: 873 rule tests run and pass outside Unity. Everything you would see, hear or feel compiles but has never been run. The glass shader has been checked for syntax only.

Pull request #4 (Milestones 1 to 6) was merged, so this is a new pull request on a new branch, `claude/milestone-7-ship`.

The picture `milestone-7-ship-previews.png` shows the cabin and the outside of the ship. It was drawn here by a small test program from the game's real meshes and the real baked light. It is **not a Unity screenshot**: in Unity the shading, glass and colours will differ a little.

## What changed

**The starter ship.** A lander in the style of the Apollo Lunar Module: a rounded white cabin on an eight-sided gold-and-black descent stage, four splayed legs with dished footpads and a ladder, four thruster clusters on outriggers, a steerable dish, a docking ring on the roof. It replaces the stand-in test craft; it flies exactly as that did (flight is Milestone 10).

**A cabin you walk about in.** One round, bowl-shaped room about 5 m across with a domed ceiling. The pilot's seat faces a curved desk of screens and switches under two big angled front windows; there is a window overhead for docking, one round window each side, a bank of lockers at the back, and the lift well in the middle of the floor with a walkway all the way round it. It has its own gravity: you stand on its floor whichever way the ship is pointing, in orbit or tumbling.

**No glitches, by construction.** Inside, you are not a physics object. You are carried in the ship's own space by simple rules (walls, desk, lockers, seat and lever post to walk round, a ceiling to bump your head on). Nothing the ship does can shake you through a wall.

**Baked interior light.** The cabin is lit by a ring lamp in the ceiling and six small wall lamps. The game's own path tracer follows that light round the room once (768 paths per corner, bounced light included) and the result is stored with the ship. In play it costs nothing: no lights, no shadows. See "Baked lighting" below.

**Belly entry.** A blue ring glows under the ship. Stand under it with the hatch open and the gravity lift draws you to the middle and up the shaft into the cabin, and the hatch closes behind you. To leave, open the hatch and step onto it. You can open or close the hatch at any time, in flight too, with one button from anywhere near the ship or with the lever in the cabin.

**The seat.** Look at the seat and press the button: you sit, glance down while the strap comes across your lap, it clicks, the seat hisses, and you have the controls. The ship only answers a buckled-in pilot. One button unbuckles.

**Damage, repair and glass.** The ship is 21 parts: cabin hull, descent stage, 4 legs, engines, 4 thruster clusters, dish, 5 windows, hatch. Each goes sound, damaged, broken. Damaged engines and thrusters still work, more weakly; a broken dish takes the map away; a broken hatch will not move. Hard landings hurt whatever hit the ground. Legs, engines and thrusters can be **torn off for good** by one huge blow or by being battered after they break, and fly off as wreckage. Windows crack in stages where they were hit and then shatter (about 8 rifle rounds, from inside or outside). Anything not torn off is mended by looking at it from close by and holding the button for 1 to 2 seconds.

**Small things.** A short readout only when there is something to say: what the button in front of you does, a repair bar, a list of hurt parts, and one notice when something breaks, falls off or is mended. 15 new sounds, all made in code.

## Why, and which principles it serves

- **Fun core first (1)** and the refinement bar "a cosy little spacecraft you want to protect": warm lamps, a seat with a view, lockers, and parts that visibly suffer and can be put right by you.
- **Agency and trade-offs (2).** A hard landing costs you a leg or thrusters you then fly without, or you get out and mend it. Broken is recoverable; torn off is not.
- **Learnable challenge, no hidden rules (3).** Every refusal says why in words (hatch broken, map needs the dish, lift has nothing below to stand on). Damage numbers are plain and in one asset.
- **Legible risk (5).** Cracks show where and how badly a window is hurt before it goes. The parts list shows percentages.
- **Cognitive load (7).** One "use" button for seat, lever and mending; the flying instruments hide when you stand up.

## Choices you should know about

1. **Four small engines round the lift shaft**, not the Lunar Module's single engine. A belly entry needs the middle of the belly.
2. **One round room.** The prompt asks for "curved halls"; here the walkway round the lift well is the curved hall. A second room would make the ship much bigger than a lander.
3. **Inside, you move by rules, not physics** (above). A side effect: bullets do not leave marks on the cabin walls, and gibs will not bounce around the cabin until Milestone 12 decides how.
4. **The lift will not lower you into empty space.** With no ground within about 4 m below, it holds you and says so. There is no jetpack until Milestone 11.
5. **A test pistol is in your hand on foot**, so glass can be shot. Test only; untick **Test Pistol** on the ship asset to remove it.
6. **A hole in the cabin has no consequence yet.** The readout says "Cabin open to the outside". Oxygen arrives in Milestone 11.
7. **Crashes cannot kill you yet.** "An unbuckled player can die in a bad crash" belongs to Milestone 10.
8. **The map only opens from the seat, and needs the dish.**
9. **Half-done repairs slip back** if you let go, so mending under fire is a real choice.
10. **Sunlight through the windows is not baked** (the Sun moves). The cabin is lit by its own lamps.

## Baked lighting: a change from what I told you

In the Milestone 6 report I said I would use Unity's lightmapper and that it would be "one menu click for you". I did it a different way that needs **no click at all**: the game has its own small path tracer, and I ran the bake here and committed the result (`Resources/Ship/LanderBake`, 31 KB). Reasons: I cannot run Unity's lightmapper from here, so you would have had to bake and I could not check it; and this way the previews attached are the real baked light. It is still baked path-traced lighting, which is what I understood you to ask for. If the cabin shape ever changes without a re-bake, the game notices, shows flat light for a few seconds and re-bakes in the background, and a test fails to tell me.

I am still working from my reading of your request (baked light for fixed places, no live ray tracing). Please correct me if you meant something else.

## What was tested

- **873 rule tests pass** (`dotnet test`, outside Unity); 140 are new for the ship. They check, among other things: 20,000 random steps never leave the cabin's floor plan or put your head through the ceiling; you slide along walls; the lift never drops you going up, keeps your shoulders clear of the shaft, closes the hatch behind you, and will not snatch you straight back after setting you down; the belt takes its stated time and clicks once; damage goes sound, damaged, broken; heavy damage tears parts off and those cannot be mended; the cabin, windows and hatch never fall off; glass takes between 4 and 14 rifle rounds (8 as set); repairs take at most 2.2 seconds; the cabin wall faces into the room and leaves every window open; every outside part sits where the layout says and faces outward; the bake gives the same result every time and a bake for a different cabin is refused.
- **The whole project compiles** against Unity reference assemblies, including 73 PlayMode tests (10 new for the ship). PlayMode tests **have not been run**.
- **Glass shader**: syntax check only.
- **Previews** of the cabin (4 views) and outside (4 views) were rendered from the real meshes and bake, and looked at. Two bugs were found this way and fixed (a floor facing down, blotchy light).

## Quality gate

Your visual quality gate (asked for on 12 November, in a separate pull request) says nothing visual passes without a Unity capture scored by the independent critic. There is no Unity capture of the ship, so **the ship's visual targets are BLOCKED, not passed.** I have not scored my own work. A ship capture test will be added to that system once its pull request is merged.

## Agent review

All three reviewers were stopped by the account's usage limit on the first attempt, so the first commit went in unreviewed. They ran when the limit reset and their findings were applied in a second commit on the same pull request. None of them ran anything in Unity.

**game-designer.** Applied:
- **Losing a part for good came too close to ordinary damage.** By its sums a leg was damaged at about 6.5 m/s, broken at 9.1 and gone for the run at 10.4. Now a safe touchdown is 4 m/s (was 3) and a single blow must be 2.5 times a part's health to tear it off (was 1.5), so a leg breaks at about 10 m/s and is lost at about 13.6. A test checks there is at least 3 m/s between the two.
- **Only the speed straight into the ground counts** as a crash. Skidding sideways no longer does, and one knock felt through four footpads is counted once.
- **The dish can break but never be torn off**, so the map cannot be lost for a whole run.
- **Outside parts could be mended through the cabin wall.** From inside you can now mend only windows, the hatch and the cabin wall. From outside the reach is 7 m, because the dish and thrusters are too high to reach from the ground and there is no jetpack until Milestone 11. This is a stopgap.
- **Lost engines could strand you** (30% thrust is less than Earth's pull). The floor is now 40%. Whether that lifts off from every world still needs checking against the tuned gravities.
- **No hidden damage:** the list shows anything below 100%, and a broken part that is close to coming off says "BROKEN, COMING LOOSE". In a crash the worst news stays on screen instead of being overwritten.

Not applied, for you to decide: whether a broken hatch may refuse to move (the prompt says "any time"); a climbable ladder; operable lockers or a lamp switch for cosiness; some sign of a breached cabin before oxygen exists; and a broken descent stage or broken legs having an effect beyond looks. It also noted that when crashes can kill (Milestone 10) the player aboard will need a rule-based shove, since they are fixed to the ship.

**deck-performance.** Nothing looked like a threat to the frame budget in steady play. Applied: crack lines on glass were being rebuilt on every hit (thousands of triangles; guessed 3 to 8 ms each on the Deck) and are now rebuilt only when something changed; on-screen prompt words were being rebuilt every frame and are now kept for a second; the fallback light bake uses one thread, not two; torn-off parts are removed once 400 m from you and the ship; sounds, belt meshes and materials are freed on restart. Not applied until measured: building sounds and meshes on background threads at run start (guessed 100 to 250 ms once per run on the Deck), halving the crack triangles, fewer engine colliders. Its figures replace mine in `docs/performance_budget.md`.

**unity-reviewer.** Real bugs found and fixed:
- **Shots fired in the cabin passed through the wall** and could damage your own legs or dish (a solid collision shape cannot be hit from inside). A shot from on board that misses the glass now counts as hitting the cabin wall.
- **The baked cabin was being lit a second time** by the planet's sky light, lamp and fog, so it would have brightened by day and gone murky in fog. Baked surfaces now show only their baked light.
- **The lift could set you down at a stale height** if the ship settled during the ride. The ground is measured again at the bottom, and if it has gone you are taken back up.
- **An unmanned ship was being steadied** as if a pilot were holding it, so it would have tipped over in slow motion. Now only a buckled-in pilot steadies it.
- **A torn-off part started inside the ship** and would have been fired out like a cannon shot. It now ignores the ship's own shapes.
- **Turning on foot outside would have stuttered** (applied at the physics rate). It is applied to the view at once.
- A cracked window near the seat could swallow the buckle-in button; the repair tick bent the pitch of other sounds; one of my own PlayMode tests would have failed for the wrong reason.

Not fixed, to check in the editor: shots from outside through a window that has already shattered still stop at the cabin's collision shape; and the baked-light file's fingerprint might not survive the move from the .NET I used to Unity's own, in which case the Console will warn and the cabin will re-bake itself (a test catches this).

The technical-artist agent built the outside parts and the furniture and found two bugs in my mesh helpers (a lathe and a ball that faced inwards), both fixed and now covered by tests. Its notes on fit are under "Still unverified".

## Still unverified

Everything that needs the engine. In particular:

- Whether the cabin feels the right size, and cosy. This is the refinement bar and only you can judge it.
- Shading: the ship's meshes rely on Unity working out smooth and sharp edges; cushions may look lumpy, foil panels may look noisy.
- Whether the view stays steady while the ship turns fast (the camera rides on the ship).
- The glass: see-through, tint, glint, cracks, and whether it shows pink (then tick **Simple Shaders** on the Planets asset).
- The belt animation: strap placement in view, and a possible gap behind your head as you sit.
- Stepping out of the lift: that the suit does not snag on the belly or legs.
- Crash damage amounts: sensible on paper, never felt.
- The ceiling lamp against the overhead window, the dish mast meeting the hull, thruster nozzles showing in the corners of the front windows.
- Sounds: all 15 are untested by ear.
- Frame cost. The figures in the budget document are estimates.
- No reference photographs were pulled for this ship; shapes are from general knowledge of the Lunar Module. The prompt says to pull real reference first, so the look should be checked against photographs before sign-off.

## Decisions for you

None blocks anything. I have gone with the recommended option each time.

1. **Four engines round a central lift** instead of one engine. Recommended: keep.
2. **One round cabin.** Recommended: keep for the starter ship; bigger ships get more rooms.
3. **Glass strength**: about 8 rifle rounds. Recommended: keep, tune after shooting it.
4. **Torn-off parts are gone for the run**, at about 13.6 m/s for a leg. Recommended: keep; it is what the prompt says.
5. **Map needs the dish and the seat**; the dish can break but not be lost. Recommended: keep.
6. **A broken hatch will not move** until mended, although the prompt says it opens "any time". Recommended: keep.
7. **Mending from 7 m away when outside**, until the jetpack exists. Recommended: keep as a stopgap.
8. **Baked lighting done by the game's own tracer** (above). Recommended: keep.
9. **Still open from before:** your reading of "bake in render and lighting"; the Milestone 5 and 6 decisions; faction names; ante timer; first-playable weapons; skateboard.

## Playtest checklist (when you first open Unity)

1. Run **Orbital Ante > Set Up Project** once more (it creates the Ship asset and the stand-in glass).
2. Choose **Fly the solar system**. You should be in the seat, looking out of two big windows at the planet. Is the desk readable and out of the way?
3. Press **F3**. No yellow warning about the ship's baked light in the Console?
4. Fly a little. Then press **B** to unbuckle. Do the flying instruments go away? Does the ship stop answering?
5. Walk right round the cabin. Does it feel like a real little spacecraft? Too big, too small? Can you get stuck anywhere? Jump under the low edge of the roof.
6. Spin the ship first (buckled), then unbuckle while it spins. Do you stay standing on the floor with a steady view?
7. Look at the seat, press **E**. Does the belt sequence read well? Is the click and hiss satisfying?
8. Land somewhere gently (under 4 m/s). Any damage? Then land hard. What broke? Does the list at top right say so?
9. Press **G**. Do the floor doors open? Step on. Does the lift set you down under the ship?
10. Walk away, come back, stand under the blue ring. Does it take you up and close behind you?
11. Outside, look at a damaged part and hold **E**. Is 1 to 2 seconds right? Is the sound satisfying?
12. Shoot a window from inside, then from outside. Count the shots. Do cracks appear where you hit? Mend it.
13. Shoot a leg until it comes off. Does it fly away? Does the ship sag?
14. Break the dish and try the map (**M**).
15. In orbit, unbuckle, open the hatch and step on it. It should hold you and say why.
16. **Pause > Restart** and check step 2 again.
17. Run the tests: EditMode should show 873 passing, PlayMode 73.

## Assets I will need

Nothing is required for this milestone; the ship is built in code. To move past the stand-in look, in order of value:

1. **NASA 3D Resources "Apollo Lunar Module"** model (free download from the NASA 3D Resources site), dropped in `Assets/ThirdParty/NASA/`. I will use it as the reference for proportions and, simplified, for the outside.
2. A **concept turnaround** (front, side, back, top) of the starter ship in the game's style from ChatGPT, if you want a look that departs from the real lander.
3. For Milestone 8: the **NASA launch pad** model (Launch Complex 39 or the mobile launcher) from the same site.
