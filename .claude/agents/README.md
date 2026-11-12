# Game-building agents

Six specialist agents that Claude can hand work to while building Orbital Ante. Each runs on Fable 5.2 and starts by reading `CLAUDE.md`, so they all work to the same build prompt and the same rules.

| Agent | What it is for |
|---|---|
| `unity-reviewer` | Reads engine-facing code that was written without the editor and finds what will break on the first press of Play. Read-only. |
| `gameplay-engineer` | Builds one scoped feature or fix the way this repository does it: rules in plain C#, tunables in data, tests, compile check. |
| `game-designer` | Checks designs and reports against the build prompt and the constitution, and proposes starting numbers with reasoning. Read-only. |
| `qa-tester` | Writes and runs tests, hunts edge cases, and writes the playtest checklist for a milestone. |
| `technical-artist` | Builds looks in code (meshes, textures, materials, effects) and writes exact asset requests. |
| `deck-performance` | Audits code for Steam Deck cost before anything is measured. Read-only. |

## Quality gate agents

Added 12 Nov 2026 with the visual quality gate (`Verification/README.md`).

| Agent | What it is for |
|---|---|
| `orbital-builder` | Takes the Critic's ranked issues for one target, chooses and implements the fixes, hands back for review. Never scores its own work. |
| `orbital-critic` | Independent review of the real game from captured Unity evidence: scorecard, ranked issues, PASS / FAIL / BLOCKED. Never edits game code or assets. |

The main session is the Quality Orchestrator: it decides when work is ready, starts the Critic, checks the Critic's file with the gate tool, hands issues to the Builder, and keeps the history. It never changes a score.

How they are used: during a milestone the main session builds the core, then sends the result to the reviewers (usually `unity-reviewer`, `game-designer` and `deck-performance` side by side), fixes what they find, and has `qa-tester` extend the tests. Their findings are summarised in the milestone report under "Agent review".

What they cannot do: none of them can run Unity. They reduce the number of problems waiting behind the first press of Play; they do not replace it.

To change how an agent behaves, edit its file here. To ask for one by name, say for example "have the unity-reviewer look at the ship code".
