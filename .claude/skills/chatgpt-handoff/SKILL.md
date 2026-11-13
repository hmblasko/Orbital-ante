---
name: chatgpt-handoff
description: Hand a bounded Orbital Ante refinement task to ChatGPT through Matthew (code review of a diff, a pre-screen of capture pictures, a small edit, doc wording) and check the reply he pastes back. Use when a task fits one packet, or when Matthew pastes a reply that starts with a JSON block naming a packet H-....
---

# ChatGPT handoff

Matthew carries packets to ChatGPT and replies back; Claude cannot reach ChatGPT. Read `docs/chatgpt-handoff.md` for the routing table and Matthew's steps. ChatGPT is a cheap second opinion: **nothing it says is verified until you have checked it, and nothing it writes is merged until it has been reviewed and the tests have run.**

## Decide whether to hand off

Hand off only if all of these hold:
- The task is on ChatGPT's list: second-opinion review of a diff, pre-screen of real capture pictures, a small edit in files you name, or doc wording.
- It is not Fable's work (assets, feel passes, cockpit designs, design decisions, Level 2 Critic scores) and does not touch the never-edit list in `tools/chatgpt/oa_handoff.py` (`DENY`).
- It fits in one packet and can be judged from that packet alone.
- It saves more than it costs: Matthew's round trip is about two minutes. Batch: one review packet per pull request, one picture packet per capture run.

Never block on a packet. Carry on with other work while it is out.

## Pack

```
python3 tools/chatgpt/oa_handoff.py pack --task review-code --title "M7 hatch" \
    --ask "Look for logic bugs and missing tests in the hatch and lift rules" --diff main...HEAD
python3 tools/chatgpt/oa_handoff.py pack --task second-opinion --title "Lander captures" \
    --ask "Point out anything that looks broken or off-model" --image Verification/Reports/<id>/ship/Screenshots/a.jpg --notes cameras.txt
python3 tools/chatgpt/oa_handoff.py pack --task small-edit --title "Hatch guard" \
    --ask "Guard Progress() against OpenSeconds <= 0 and add an EditMode test" \
    --allow-edit unity/Assets/OrbitalAnte/Scripts/Runtime/Ship/HatchRules.cs --allow-edit unity/Assets/OrbitalAnte/Tests/EditMode/HatchRulesTests.cs
```

- Write a precise `--ask`: the outcome, the limits, and what not to change.
- Narrow big files with `--file path:start-end`. Keep the packet under about 60,000 characters.
- Pictures for `second-opinion` must be real Unity captures from `Verification/Reports/`, never previews or concept art. Pass the camera list from the capture manifest with `--notes`.
- In a cloud session, pass `--out /mnt/project-files/orbital-ante/chatgpt` so Matthew can open it, and attach `packet.md` (and each file in `attach/`) to your reply. Locally the default is `handoff/` (git-ignored).
- In the reply, tell Matthew in one line what the packet is for and point to the steps in `docs/chatgpt-handoff.md` the first time.

## Intake

1. Save Matthew's paste exactly as given to `<packet folder>/reply-raw.md` (do not tidy it), then run `python3 tools/chatgpt/oa_handoff.py intake <packet folder> <packet folder>/reply-raw.md`.
2. If it says **REJECTED**, send Matthew the Problems list to paste back into the same ChatGPT chat. After a second rejection, do the task yourself and say so.
3. **Findings:** check each one yourself. For code, read the lines and, for a real bug, write a failing test first. For pictures, open the same capture. Mark each confirmed or rejected with a one-line reason. Confirmed visual findings go to the Builder as leads; only the Critic, having seen them itself, may put them in `issues.json`. Advisory scores never go in the gate.
4. **Edits:** make sure you are on a `claude/...` branch, run intake again with `--apply`, read the whole `git diff`, and fix or revert anything wrong. Run the tests that cover it (`dotnet test` in the cloud for rules, `tools/check-in-unity` locally via the `unity-refine` skill). Engine-facing changes still go to `unity-reviewer`. Commit with `Suggested-by: ChatGPT (packet H-...), reviewed by Claude` in the message.
5. Report to Matthew: what was confirmed, what was rejected and why, what ran, and what is still unverified.

## Never

- Present a ChatGPT finding, score or edit as verified, or merge one untested.
- Send secrets, Matthew's personal details, or anything outside the repo in a packet.
- Let ChatGPT name factions or invent playtest results.
