---
name: chatgpt-handoff
description: Hand token-saving Orbital Ante work to ChatGPT Codex (PR reviews, audits, rule-test drafts, checklists, doc rewrites) by posting @codex on GitHub, and check what comes back before it is used. Also covers the copy-and-paste packet fallback (replies that start with a JSON block naming a packet H-...).
---

# ChatGPT handoff

## Codex first (connected to the repo since 13 Nov 2026)

Matthew asked on 13 Nov 2026 for ChatGPT to take tasks that save Fable tokens where it can do them as well, with Fable staying in charge of design and asset creation. Codex reads `AGENTS.md` at the repo root on every task, so prompts can stay short.

**Send work to Codex** by posting a PR comment (`gh api repos/hmblasko/Orbital-ante/issues/<n>/comments -f body=...`), as part of the work Matthew asked for:
- Review: `@codex review <focus>. Comment only.` on your own PR, once it is ready for review. One per PR, plus one `@codex review the latest commits only` after a round of fixes.
- Task: `@codex <task>. Follow AGENTS.md: open one new PR into main, touch only <path>.` for audits, rule-test drafts, checklists or doc rewrites. Matthew can also start these from https://chatgpt.com/codex using the prompts in the "ChatGPT and Codex task menu" doc.
- Tested 13 Nov 2026: `@codex review` posted from a Claude session works (the bot is `chatgpt-codex-connector[bot]`). A non-review `@codex <task>` comment was answered "create an environment for this repo", so until Matthew sets one up at chatgpt.com/codex/cloud/settings/environments, ask him to start tasks from chatgpt.com/codex.
- Codex's earlier cloud-task output was posted under Matthew's GitHub login, so tell its comments apart by their content, not by who posted them.

**Good Codex work:** PR reviews; audits (seeds, Unity 6 traps, Deck cost, hard-coded feel numbers); EditMode rule-test drafts for plain C# rules; playtest checklists; design questions (never answers); turning Matthew's play notes into issues; rewording docs into numbered steps; critiquing Blender previews against a brief.
**Never Codex:** making models, animations or audio; tuning values; feel passes; cockpit or dash designs; design decisions; quality-gate scores; anything Phase 2.

**When it comes back, the owning thread checks it before use:**
- Review comments: check each against the code. Fix what is real in your own PR, and reply on the comment once with what you did or why it is wrong.
- Test-draft PRs: compile them (cloud recipe in project memory, `orbital-ante-verify-without-unity`), run `dotnet test`, and fix or drop tests that are wrong. A failing test that exposes a real bug becomes a bug fix with that test. Merge only with the results stated.
- Report and checklist PRs: read them against the code, mark which points hold, and turn real ones into work items. Do not treat its suggested numbers as decisions.
- Say in your report what Codex suggested, what you used and what you rejected.

## Copy-and-paste packets (fallback: pictures, or anything outside the repo)

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
