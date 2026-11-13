# Launch and audio integration — review handoff and playtest cards

For Fable 5.2, who retains design, asset, sound, feel and tuning authority. This is a source-only handoff and a proposed check packet. Nothing here is a completed playtest, listening result, passing-test claim or quality-gate verdict. No Unity execution, C# compilation or Deck measurement was performed.

## Source boundary and intended use

- Audio baseline: `main` at `224642ed5bf6da5553334c02b0f7236864cd8b64`, especially `Scripts/Runtime/Audio/` and `docs/audio/implementation.md` / `docs/reports/audio-slice.md`.
- Launch baseline: PR #23, `claude/milestone-8-launch` at `5556189d89284d7b4d5caa4f385e69fb78ed2235`, especially `Scripts/Runtime/Launch/`, `Space/TestCraft.cs`, `Space/SolarSystemWorld.cs`, `Ship/Ship.cs` and `Ship/Astronaut.cs`. All script paths in this document are under `unity/Assets/OrbitalAnte/`.
- These are **separate snapshots**. The inspected launch tree has no Audio runtime folder; main has the audio slice and the older test craft. No combined tree was built or merged during this review. The three integration risks below are conditional on retaining both cited behaviors when Fable combines the work.
- This adds a focused checklist; it does not replace `docs/SETUP.md` or the broader Phase 1 checklist. The earlier setup-rewrite PR #21 was based on an older source snapshot. Preserve Fable's newer ship, launch and audio instructions when preparing any replacement.

## Three integration risks to check before judging the mix

| Risk | Source evidence | Proposed technical check; decision owner |
|---|---|---|
| **The old-engine mute can target another ship sound.** | Main `Audio/GameAudioHooks.cs:159–165` calls `craft.GetComponent<AudioSource>()`. Launch `Ship/Ship.cs:100–106` builds the ship's tick/voice/lift sources before attaching TestCraft; those sources (`:310–327`), the private TestCraft rumble (`Space/TestCraft.cs:132–138`) and launch sources (`Launch/LaunchFx.cs:81–86`) share the same object. | Ask Fable to identify the intended placeholder by explicit ownership during integration. A generic component lookup cannot express that ownership. Record the clip, mute state and owner of every source before/after the hook. Preserve repair, interaction and lift audio; do not solve this by muting every source. Principles 3/5/10: consistent feedback, legible risk, evidence-based iteration. |
| **The listener can remain classified as seated after the player leaves.** | Main `Audio/GameAudioHooks.cs:168–170` always supplies Air=1/Cockpit and starts the cabin loop for a solar world; `:183–204` samples music context at the craft. Launch `Ship/Astronaut.cs:166–228` supports Aboard, Lift and Outside, moving the actual listener. | Record listener position/mode, ListenerSpace and cabin-loop target through the complete entry/exit loop. Fable defines the intended cabin/open-hatch/suit treatment and which environment supplies context; the integration must use the actual listener's state. No oxygen or new acoustic mechanic is proposed. Principles 3/5/7/10. |
| **Launch audio can bypass the Effects volume setting.** | Main AudioPrefs supplies per-bus gain; `Audio/AudioDirector.cs:265–267,433–434` applies it to produced events/loops. Launch `Launch/LaunchFx.cs:295–300,422–427` plays procedural sources with only Model/LaunchConfig levels. | Hold Master fixed and compare Effects full/zero for ignition, roar, crackle and release. Fable chooses ownership/layering of produced events and procedural fallback; retain the selected sound recipe while honoring the intended user control. Master already acts globally through AudioListener.volume, so this is a separate per-bus question. Principles 2/7/10: agency over settings and predictable controls. |

The launch report already notes that its roar layers over the old hum. That artistic layering question is distinct from accidentally muting the wrong source or bypassing a user setting.

## One launch-rule edge case for Fable's technical review

`LaunchModel.Tick` raises ignition's wanted Burn toward 0.8 independently of `up` (`LaunchModel.cs:162–167`), then switches to `up * 0.8` for unboosted Running (`:169–171`). `TestCraft.Update` normalizes the combined thrust vector, so holding up and a sideways direction can supply approximately `up=1/sqrt(2)` (`Space/TestCraft.cs:166–168`). In that case the running target is about 0.566, below the late ignition target. Burn can fall at release despite the documented “step up, never a step down” property. The existing continuity test uses up=1 only.

Proposed smallest rule check: hold a constant normalized diagonal up input through ignition/release, record Burn immediately before/after `JustReleased`, and compare to the existing full-up case. Include a partial test input and a boost change for context. Ask Fable to decide how the intended release-continuity rule applies to those inputs before changing implementation; no replacement timing, force or sound level is prescribed. Principles 1/3/10: convincing release, consistent rules and focused evidence. This is source reasoning, not a test result.

## Prerequisites — leave the combined-build checks blocked until supplied

1. Fable prepares a review build containing the audio and launch changes together, reconciles their overlapping bootstrap/input changes, and supplies its branch and exact SHA. Neither cited snapshot alone establishes this integration. Do not merge branches as a side effect of running this checklist.
2. Record source SHA, seed, platform, resolution, volume settings, shader fallback, output device and whether the test uses the editor or a Linux player. Use native 1280×800 for Deck observations.
3. For an editor check, open the supplied project's `unity` folder, wait for import and use **Orbital Ante > Set Up Project** if the reviewed setup requires it. Open **Window > General > Console**; if there is a red error, record the first one and stop that card.
4. If present in that integrated build, use **Orbital Ante > Audio > Check Audio Files**. Record its actual output; the existence of a source fallback does not establish that import/playback works.
5. Start **Fly the solar system**. Confirm the supplied start is the pad, with the pilot buckled into the lander. An orbit/old-craft start is BLOCKED for the pad cards. Open Pause with **Start / Menu** (**Esc** on keyboard), then read **Controls** for current bindings. Defaults from the launch code are thrust-up **Right Trigger** (**Space** on keyboard), unbuckle **Y / Triangle** (**B** on keyboard), Use **Y / Triangle** (**E** on keyboard) on foot, and hatch **D-pad left** (**G** on keyboard); the active input map changes their meaning. Use displayed bindings if they differ.
6. With a keyboard attached, F9 opens the audio readout in the audio slice and F3 is the game debug readout. If those are unavailable on the chosen Deck setup, have Fable collect the corresponding values; do not invent an unimplemented controller shortcut.

Each card below is a separate short session after loading. Leave `Yes / No / BLOCKED`, notes and clip names blank until actually performed. Preserve existing settings so they can be restored. Changing a player volume setting during a check is not approval to retune the game's mix.

## Card A — source ownership and volume controls (about five minutes)

1. Start seated on the pad. Have Fable inspect the AudioSources attached to the ship, recording owner/clip, mute state, playing state and output routing before/after GameAudioHooks first sees the craft.
2. Perform one ignition and early cancellation. Separately exercise the lift and one repair/use sound from a supplied safe setup. Do not damage the ship blindly to manufacture a repeatable state.
3. Open **Pause > Audio**. Note **Master volume** and **Effects**. In the current audio UI these are buttons that cycle through displayed steps 0–10, advancing one step per press; they are not draggable sliders. Cycle Effects to its displayed step 0 (mute) while keeping Master unchanged, then resume and repeat ignition. Restore the starting values afterward.

- [ ] Is only the intended superseded placeholder stopped, with interaction/repair/lift sources intact? ___
- [ ] Does Effects control the intended launch and ordinary sound effects consistently? ___
- [ ] After restoring settings, does the intended audio return? ___

Record perceived balance as the player's words for Fable. A source list cannot establish whether layering sounds good.

## Card B — listener follows the player (about five minutes)

1. From the safe pad start, unbuckle, walk aboard, open the hatch and ride down. Move away from the ship, then return under the ring and rebuckle.
2. Record listener mode/location, ListenerSpace, active cabin/engine loop targets and music context at each transition. Give Fable a short clip with the transitions marked.
3. A cave, underwater or airless-world comparison requires a suitable supplied start. Mark it BLOCKED if absent; no jetpack, teleport menu or suit feature is assumed.

- [ ] Do the sound state and actual listener location agree throughout the entry/exit loop? ___
- [ ] Does cabin sound follow Fable's intended policy outside the hull and during the lift transition? ___
- [ ] Does environment/context selection use the intended location when the player and ship separate? ___

## Card C — launch cancellation, diagonal input and pause (about six minutes)

1. At the parked start, hold up briefly and release before lift-off. Repeat once, then perform a complete straight-up launch from a fresh start.
2. From the same fresh start, compare up plus a sideways direction held through release. Record the Burn/engine state and visible/audio release; distinguish exhaust feedback from actual acceleration. Fable judges the intended feel.
3. Repeat ignition with **Pause** during build-up. Release the trigger while paused, wait briefly, then resume. Repeat once near release if safe. Restore the initial state between cases.

- [ ] Does cancellation stop the accepted launch without an unexpected later release? ___
- [ ] Does diagonal input preserve the release behavior Fable intends? ___
- [ ] Does resume honor the current input without leftover thrust, duplicate ignition or a mismatched sound event? ___

`GameSession.SetPaused` sets AudioListener.pause. LaunchFx's zero-delta return alone is not evidence of a pause-sound defect. Music intentionally continues/ducks in the audio slice; Fable decides the intended transition, and the check records what actually occurs.

## Card D — world changes, origin shifts and cleanup (about six minutes)

1. Start a launch, then use **Pause > Restart > Restart with the same seed**. Repeat once after lift-off. Record active source/loop counts and source ownership after destruction has completed; do not expect caches to return to zero.
2. Use **Pause > Quit > Quit to main menu**, confirm, then start **Combat test arena**. Check which world loops/context remain. Return to the solar scenario and repeat one launch.
3. Have Fable provide a safe floating-origin crossing with a long positional sound already playing. Record source/listener relative position and listen for displacement across that crossing. This is an already documented open audio issue, not a newly discovered defect or a completed reproduction.

- [ ] Are engine/cabin loops and one-shot tails handled according to Fable's intended transition policy? ___
- [ ] Do repeated restarts avoid accumulating orphan launch ground effects, sources or owned generated assets? ___
- [ ] Does a positional sound remain at the intended relative location across an origin shift? ___

LaunchFx has explicit cleanup for its generated clips/meshes and separate ground effects in OnDestroy. Source ownership is a useful starting point, but it does not establish the engine result. AudioDirector survives world replacement; whether a particular tail/music phrase should persist is a Fable decision rather than a leak claim.

## Return packet

Give Fable the build SHA/seed, completed cards, exact controls/settings, short clips and any first Console error. For profiling, add whole-frame CPU/GPU, Audio CPU, Playing Sources, Total Audio Memory and native resource counts across the relevant transitions where those tools are available. All new timings must come from the device/build; this document supplies none.

Fable checks the findings, runs the applicable rule/PlayMode cases and owns any creative response. The Critic evaluates actual captures. No game code, assets, config values, standing rules or Phase 2 systems are changed by this document.
