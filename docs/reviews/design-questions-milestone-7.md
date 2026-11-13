# Milestone 7 — questions for the lead designer

Source read: PR #6, `claude/milestone-7-ship` at `d277c7d5a3948d12b03e6e05ec353fd7002fde20`, including `CLAUDE.md` Part B, `docs/reports/milestone-7.md`, the ship rules/runtime code and its existing tests. That milestone is not on the inspected `main` snapshot (`96264e13187659d90dba198703bba188721f4e23`); this document records questions about the pending ship work, not a claim that main already contains it.

Fable 5.2 leads the look, feel, models and design when available; the current Claude Opus 5.5 session can check technical findings and run the real checks. These are questions and proposed observations for Fable and the player, not instructions, new feature requests or tuning decisions. Claude should check source findings and run the relevant tests before any merge. I have not run Unity, viewed a Unity capture, heard these sounds or measured a frame time. No result, score or visual-gate verdict is recorded here.

## 1. Does the room make the ship feel worth protecting?

**Question for Fable:** Does the single bowl room and walkway around its central well satisfy the milestone's “cosy little spacecraft you want to protect” bar, including the prompt's curved-hall intent, or does the player read it as an empty circular test room?

**Why ask:** The report deliberately interprets the walkway as the curved hall; `InteriorWalker` constrains the cabin to one floor plan with a seat and lever obstacle. This can be a sensible compact interpretation, but shape correctness and a collision-free path cannot establish the feeling of being in a home-like craft. The report also explicitly lists interior scale and cosiness as unseen in Unity.

**Principles:** 1, fun core first; 7, cognitive load; 10, evidence-based iteration.

**Smallest playtest:** Start the pending ship build, unbuckle and ask a fresh player to walk around the well, find the seat and identify the exit without an explanation. Give them two minutes. Ask “Which place in this room feels useful or comfortable?” and “Would you want to return here?” Record their words and any stuck location. Do not add furniture or pick a different layout from this question alone.

## 2. Can the player predict who has flight control?

**Question for Fable:** Is the boundary between buckling, buckled flight and standing obvious from the first-person belt, hiss/click, controls and disappearing flight instruments, including when the ship is spinning?

**Why ask:** `SeatBelt` enables flight only once buckling completes. `Astronaut.UpdateSeated` switches input maps at the click and on release, while `Ship.Update` sets `Flight.Unmanned` from belt state. Standing aboard is rule-based motion fixed to the ship; crash injury and a rule-based shove are expressly deferred. The intended control boundary should remain learnable before those later systems arrive.

**Principles:** 2, agency; 3, learnable challenge; 7, cognitive load.

**Smallest playtest:** With a player who has not read the report, buckle and release once at rest, then repeat after a gentle spin in a safe location. Ask before each transition whether thrust will respond and where their feet will end up. Note mistaken predictions, horizon discomfort and whether the belt cue is visible. This asks whether the present cues work; it does not choose new timing or damping values.

## 3. Does the belly-entry loop explain itself without trapping the player?

**Question for Fable:** Can the player understand the blue ring, hatch, automatic boarding/closure and the requirement to step away before boarding again as one coherent loop?

**Why ask:** `HatchLift.CanPickUp` suppresses immediate reboarding after a downward ride until the player steps outside the ring region. `Astronaut.UpdateAboard` starts descent from the open well. A player who stands still after getting out may reasonably expect the ring to take them up again; that expectation needs observation rather than assuming the hidden anti-recapture latch is obvious.

**Principles:** 3, learnable challenge; 5, legible risk; 7, cognitive load.

**Smallest playtest:** On level ground, ask the player to leave and re-enter twice, using their normal Deck controls and without telling them to step away. Allow two minutes. Ask them to explain why the lift did or did not take them. Record any confusion and whether the prompts resolve it. Keep the belly entry and existing mechanics intact while assessing this.

## 4. Is the orbit exit refusal understandable and consistent with “any time”?

**Question for Fable:** Does the current no-ground refusal communicate a temporary Phase 1 safety boundary, and how should the player interpret a broken hatch compared with the build prompt's “open/close it any time, even in flight” wording?

**Why ask:** The report deliberately prevents lowering the player into empty space before the jetpack milestone. `Ship.FindLiftBottom` must find ground within the lift's reach, while `ShipModel.HatchWorks` disables hatch requests when Broken. The report asks the owner to decide the broken-hatch interpretation; this document does not resolve it. The follow-up code now also gates door advancement when the hatch breaks mid-movement; source review identified this change, but it has not been exercised here. Include both halfway-open and halfway-closed failures in the player observation rather than assuming the patch establishes the intended experience.

**Principles:** 2, agency; 3, learnable challenge; 5, legible risk.

**Smallest playtest:** In orbit, open the hatch and approach the well; on the ground, repeat after a controlled broken-hatch setup supplied by Claude. Ask the player what each refusal means and how they would recover. Capture exact prompt text and their prediction. Ask Fable to decide whether the observed behavior matches the intended exception; do not implement an escape mechanic or alter the damage rule here.

## 5. Can the player tell recoverable damage from permanent loss before committing?

**Question for Fable:** Do cracks, sag, notices and the parts list let a player distinguish scratched, Damaged, Broken, “coming loose” and Lost without needing to memorize percentages?

**Why ask:** `ShipModel` makes Damaged/Broken repairable and Lost permanent; the HUD exposes health and tear progress. The report acknowledges that crash severity has never been felt. Health thresholds alone cannot establish whether the remaining safe margin is perceptible, particularly when several components change in one collision. Also, the report's “21 parts” description differs from the constructor's 18 health units: four visible engine bells share one `engines` unit. A player may infer independent bells unless the feedback makes the grouping clear.

**Principles:** 2, meaningful trade-offs; 3, learnable challenge; 5, legible risk.

**Smallest playtest:** Have Claude prepare separate existing states for one leg: damaged, broken, close to loss and lost. Show them in order without narrating the state, and ask “Can you repair this, and what do you think another hit will do?” Repeat only the engine grouping with the four visible bells. Record predictions and the cues cited. Do not prescribe a new health value or score the quality gate.

## 6. Does one interaction button stay unambiguous during repairs?

**Question for Fable:** Is the distinction between a tap to use the seat/hatch and a hold to mend reliable when several damaged surfaces or controls occupy the same view?

**Why ask:** Inside, `Astronaut.UpdateAboard` prioritizes the lever and seat ahead of mending. Outside, it tries mending before the exterior hatch button. `Ship.FindRepairTarget` ranks in-range anchors by aim angle; the outside reach is intentionally longer as a pre-jetpack stopgap. Repairs partially decay when released. The intended simplicity of one button may still create surprising priorities or unnoticed loss of progress.

**Principles:** 2, agency; 5, legible risk; 7, cognitive load.

**Smallest playtest:** With a damaged front window near the seat, ask the player to buckle in and then mend it. Outside, damage a component near the hatch control and ask them to open the hatch, select the desired repair, interrupt once, then resume. Record unwanted actions, target switching, progress-bar interpretation and whether the repair sound tells them the same thing as the bar. Fable decides whether the existing priorities are acceptable; this question does not invent another button or retune repair durations.

## 7. Does breakable glass behave as the visible opening suggests?

**Question for Fable:** Does the glass's staged cracking give enough warning and make a broken pane read as a real opening, while preserving a readable pilot view?

**Why ask:** `ShipModel.CrackStage` and `Ship.RefreshPart` produce crack/frost stages, hide the pane and disable its own collider when broken. The milestone report already notes that outside shots through a missing pane can still hit the solid cabin collision shape; that is a known runtime limitation for Claude to check, not a visual result. The latest follow-up adds a rule-based cabin-exit boundary to the shot-resolution path, including directions that miss every physics collider. Its new rule/PlayMode cases have not been executed here. Include adjacent solid-hull and opening shots in the actual check so visual boundaries and hit feedback are assessed together.

**Principles:** 1, fun core; 3, learnable challenge; 5, legible risk.

**Smallest playtest:** With the existing test pistol, use the same front pane from inside and outside, recording hits as it cracks and disappears. Compare a shot at the opening with a shot at the adjacent solid hull and ask the player to predict which target behind it can be reached. Keep source-bug reproduction separate from Fable's judgement of crack readability; no new penetration mechanic is requested.

## 8. Are map loss and its recovery legible without becoming a chore?

**Question for Fable:** When the dish breaks, does the seat-only map refusal explain what happened and let the player recover confidently, rather than feeling like the navigation UI simply stopped working?

**Why ask:** The dish is repairable and cannot be permanently torn off. `SolarSystemWorld.Map.CanOpen` requires both a buckled seat and a working dish; `WhyNot` reports the seat or dish refusal. The report retains outside repair reach as a temporary way to mend high parts before the jetpack exists. Whether looking up and mending from that range feels coherent is a play question.

**Principles:** 2, agency; 3, learnable challenge; 7, cognitive load; 8, varied intensity.

**Smallest playtest:** On safe ground, have Claude prepare a broken dish, then ask an otherwise uninformed player to open the map and recover it using only the visible prompts. Record the route, first repair target and any stalled step for up to three minutes. Do not add map alternatives or select a new reach value here.

## 9. Do damage feedback and presentation remain useful on the actual Deck?

**Question for Fable:** In one busy landing or repair moment, do the highest-priority notice, small damage list, glass view and sounds communicate the next useful action without drowning the scene or stuttering?

**Why ask:** `ShipHud` keeps the worst recent damage notice and refreshes the damage list periodically. Cracks rebuild only when the displayed stage/hit count changes, but those changes can still generate thousands of triangles during a combat event. The budget and report contain estimates, not measured Deck costs; the report also states that no one has heard the 15 sounds in the game. Neither source review nor exported previews can decide clarity, comfort or frame pacing.

**Principles:** 1, fun core; 5, legible risk; 7, cognitive load; 10, evidence-based iteration.

**Smallest playtest:** Record one controlled hard landing followed by a window hit and a repair at native 1280×800. Ask immediately what broke, what can be fixed and what they would do next. Claude should record actual CPU/GPU whole-frame and relevant rebuild costs on Deck, along with the active build/seed and image settings, separately from the player's answers. Keep results blank until this is performed; only the independent Critic decides visual-gate scores/verdicts.

## Review handoff

For each question, keep a capture/build SHA, seed, setup and the player's own words with the observation. Mark it “not run” until someone actually performs it. Fable and the owner resolve the questions; Claude checks code findings and runs the appropriate tests before merge. No Phase 2 work, new loot/factions, cockpit redesign, mechanics change, asset change or tuning choice is proposed by this document.
