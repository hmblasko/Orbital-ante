# Handing refinement work to ChatGPT

Matthew asked on 12 Nov 2026 for a way to hand refinement tasks to ChatGPT and bring its thoughts or edits back, while Fable 5.2 keeps the heavy lifting and Opus and ChatGPT time is used efficiently.

Claude cannot call ChatGPT, so Matthew is the courier. Claude packs one self-contained **packet**, Matthew pastes it into ChatGPT, copies the reply back, and Claude checks it with a tool before anything is used. A round trip should take Matthew about two minutes.

**ChatGPT's output is never treated as verified.** Its findings are leads that Claude confirms against the code or the real captures. Its edits are checked, applied on a branch, reviewed and tested like any other change, and are never merged on its word.

## Who does what

| Work | Who | Why |
|---|---|---|
| Blender assets, look and feel passes, cockpit designs, flight and crash feel, design calls | **Fable 5.2** | The most important work (Matthew's rule). Never sent to ChatGPT. |
| Level 2 Critic reviews (the quality gate's scores and verdicts) | **Fable 5.2** (`orbital-critic`) | The gate's verdict must come from the Critic that looked at the captures. |
| Prep, wiring, tests, docs, checking and applying ChatGPT replies | **Opus 5.5** | Mechanical work Claude can verify by running tests. |
| Second-opinion code review of a pull request's diff before merge | **ChatGPT** | Cheap extra eyes. One packet per pull request. |
| Pre-screen of capture pictures before a Fable Critic run (Level 1 glance) | **ChatGPT** | Catches obvious breakage (clipping, pink shaders, wrong scale) before a Fable review is spent. Advisory only. |
| Small isolated edits: a guard clause, a rule test, a plain C# helper | **ChatGPT** | Only in files Claude names, only as search/replace edits. |
| Wording of docs, reports and Matthew's step lists | **ChatGPT** | Low risk, easy to check. |

ChatGPT is never sent: anything Fable owns (above), the rubric, the gate config or reports, `CLAUDE.md`, `.claude/`, Unity scene, prefab, material or `.meta` files, binary assets. The tool refuses these even if a packet tried to allow them.

**When a handoff is worth it.** Only when the task fits in one packet (under about 15,000 tokens; the tool warns at 60,000 characters) and can be judged from what is in it. If Claude can do a task in one short pass, handing it off costs Matthew more time than it saves. Claude batches: usually one code review packet per pull request and one picture packet per capture run.

## One-time setup (Matthew, about 3 minutes)

1. Open https://chatgpt.com and sign in.
2. In the left sidebar, click **Projects**, then **New project**. Name it `Orbital Ante reviewer`.
3. Open the project, click its settings (the **...** next to the project name, or the **Instructions** card), and choose **Instructions**.
4. Open `tools/chatgpt/chatgpt-project-instructions.md` from the repo (or the copy Claude attaches), copy everything below the line, paste it into the Instructions box, and save.
5. Use a reasoning model for code review packets if your plan has one; any model is fine for docs.

## Each handoff (Matthew, about 2 minutes)

1. Claude attaches a packet to its reply: a file called `packet.md`, plus any pictures to attach. In a cloud thread they are in the project files under `orbital-ante/chatgpt/<packet id>/`; in a local Claude Code session they are in `handoff/<packet id>/` in the repo folder.
2. Open `packet.md`, select all (Ctrl+A), and copy (Ctrl+C).
3. In ChatGPT, open the **Orbital Ante reviewer** project and start a **new chat** (one chat per packet keeps replies clean).
4. Paste the packet into the message box. If the packet lists pictures, drag every file from the packet's `attach` folder into the message box too.
5. Send, and wait for the full answer.
6. Under ChatGPT's reply, click the **Copy** button (the two overlapping squares). Do not select the text by hand: that drops the ``` fences the tool reads.
7. Paste it into the Claude thread (or local Claude Code session). Nothing else is needed; the packet id is inside the reply.
8. If Claude answers with a **Problems** list, paste that list into the same ChatGPT chat, then copy the new reply back as in step 6. If the second try also fails, Claude does the task itself rather than asking a third time.

Packets do not block anything. Claude keeps working while one is out, so do them whenever it suits you, or tell Claude to skip one.

## What Claude does (both ends)

The procedure is in the `chatgpt-handoff` skill (`.claude/skills/chatgpt-handoff/SKILL.md`). In short:

1. **Pack:** `python3 tools/chatgpt/oa_handoff.py pack --task <review-code|second-opinion|small-edit|docs> --title "..." --ask "..." [--diff main...HEAD] [--file path[:start-end]] [--allow-edit path] [--image path]`.
2. **Intake:** save the pasted reply to a file and run `python3 tools/chatgpt/oa_handoff.py intake <packet folder> reply.md`. It checks the packet id and task, the shape of every finding, that edits touch only the allowed files, and that every SEARCH matches exactly once. It downgrades any "verified" claim to "suspected", writes `intake.md` and `findings.json`, and never commits. Edits are a dry run until `--apply`.
3. **Verify:** confirm or reject each finding by reading the code (and writing a failing test for a real bug), or, for pictures, by looking at the same capture. Apply edits with `--apply` on a `claude/...` branch, review the diff, run the tests, and commit with `Suggested-by: ChatGPT (packet H-...)` in the message.
4. **Report:** say which findings were confirmed, which rejected and why, and what ran.

## Formats

Every reply is one JSON envelope: `packet`, `task`, `summary`, `findings`, `questions`, `notChecked` (plus `advisoryScores` on picture packets). Each finding uses the same field names as an issue in the quality gate's `Verification/Config/issues.schema.json` (`id`, `category`, `severity` P0-P3, `kind`, `title`, `description`, `evidence`, `recommendedFixes`, `acceptanceCriteria`, `confidence`), so a finding the Critic has confirmed in the captures can be lifted into its `issues.json`. ChatGPT may not use `verified_defect`, and its advisory scores never count toward the gate.

Edits come as search/replace blocks rather than unified diffs, because chat models get diff line numbers wrong far more often than they misquote text:

```
FILE: unity/Assets/OrbitalAnte/Scripts/Runtime/Ship/HatchRules.cs
<<<<<<< SEARCH
exact old lines
=======
new lines
>>>>>>> REPLACE
```

## Could this be automatic?

Not today. No ChatGPT connector exists in the Claude connector directory (searched 12 Nov 2026), and cloud sessions have no OpenAI access. Two options if the copy and paste gets tiresome:

- **OpenAI API key.** Add an `OPENAI_API_KEY` secret to the Claude environment and allow `api.openai.com` in its network settings, and `oa_handoff.py` could gain a `send` step that posts the packet and runs intake on the answer. API use is billed per token, separately from a ChatGPT subscription.
- **Codex CLI on Matthew's computer.** A local Claude Code session could run OpenAI's command-line tool with the packet. Also billed through OpenAI.

Either can be added later without changing the packet or reply formats.

## Tests

`python3 -m unittest discover -s tools/chatgpt/tests` (standard library only, no Unity needed).
