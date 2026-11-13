# Fun, motivation, and the first implementation

Prepared 13 November 2026 against `main` commit `224642ed5bf6da5553334c02b0f7236864cd8b64`.

This is new research for Matthew's request to study psychology and improve the game. It does not reconstruct or claim to be the earlier points-system research from another conversation. It is a targeted review of 14 publications, including primary experiments, meta-analyses and longitudinal observation, rather than an exhaustive systematic review. Methods, samples, original-source links and limits are in the two evidence files:

- [Motivation, choice, rewards, curiosity, and goals: seven sources](motivation-evidence.md).
- [Challenge, flow, feedback, responsiveness, choice, and wellbeing: seven sources](challenge-and-feedback-evidence.md).
- [Code changes, review findings, and remaining checks](../../reports/fun-control-hints.md).

Fable 5.2 retains authority over design, assets, feel, tuning and presentation. Codex supplies research, implementation and engineering review. The code in this change repairs existing instructions; larger experience changes below are proposals for Fable. No player outcomes have been observed for this change, and Unity/Steam Deck behavior remains unverified.

## What the research supports

People can pursue achievement because an activity lets them develop competence, make choices they endorse, answer a question they care about, or connect with others. These motives vary by person, situation and experience. A score, a win, a long session and enjoyment measure different things; none is a universal proxy for the others.

**Agency and competence.** Ryan, Rigby and Przybylski's game studies associate intuitive controls, competence and autonomy with enjoyment. Patall and colleagues' meta-analysis of choice experiments supports benefits from choice on average, with substantial context dependence. For this game, a useful hypothesis is that players should be able to choose an approach, act reliably, understand the result and form a better next attempt. This does not imply that more buttons, guaranteed success or more options always help.

**Challenge and achievement.** The flow meta-analysis supports a relationship between perceived challenge–skill fit and flow, with measurement limitations. Suspense experiments suggest close contests can be enjoyable in their studied setting; large educational-game experiments show that harder conditions do not universally maximize engagement or learning. Therefore, there is no researched universal success percentage or difficulty curve to import. Fable should define which skill a situation asks the player to learn, which mistake remains recoverable, and what information makes a failure intelligible.

**Curiosity and discovery.** The information-gap account suggests a recognizable unanswered question can invite exploration. Small laboratory studies associate curiosity with better learning, but do not demonstrate enjoyment in a space roguelite. A useful division is to make operational rules understandable while letting the world contain interesting unknowns. Hiding the key needed to interact creates a different uncertainty from wondering what is inside a visible wreck.

**Feedback and reinforcement.** Behavioral output can increase without improved intrinsic motivation: the image-tagging experiment with points, levels and leaderboards is a direct example. The reward meta-analysis distinguishes expected tangible incentives from informative positive feedback, but does not settle the value of an in-game scoring rule. Educational feedback research supports learning on average, not a claim that more praise, particles, popups or score always improve entertainment. Show the actual consequence of a player's action before adding another incentive to repeat it. This is an implementation hypothesis, not a proven intervention in Orbital Ante.

**Goals and progress.** Progress-monitoring interventions improve goal attainment on average in mostly non-game contexts. That supports examining useful immediate-goal information, such as motion relative to a destination. It does not justify a global completion meter, a checklist of obligations or social pressure. A player may freely choose a difficult goal and enjoy incomplete progress toward understanding it.

**Response quality.** A controlled FPS experiment found lower local latency improved performance and experience within its setup. Its mouse controls, experienced sample and high-refresh displays differ from the Deck. Source review cannot establish this game's responsiveness. Frame pacing and input correctness need investigation alongside difficulty; frustration caused by an unreliable response is not evidence that enemies are too hard.

**Voluntary immersion.** Time-played data alone are insufficient to evaluate experience or wellbeing. Assess enjoyment, perceived control, understanding and felt pressure separately. The project's existing rejection of FOMO, grind and streak pressure is a design commitment; no cited study alone proves every prohibited feature harmful or every permitted feature safe. Multiplayer is not prescribed by evidence about relatedness in an MMO sample.

## First application: instructions must agree with controls

The current code allows movement and arena-action rebinding, but several hints continue to advertise defaults:

| Existing failure | Implemented correction | Why this is a useful first application |
| --- | --- | --- |
| Arena help and the hold-fire banner still say `T`/`H` and default pad buttons after customization. | Describe the accepted effective bindings for the device in use. | A chosen control layout should remain usable without memorizing a contradictory instruction. |
| Flight help still says `W A S D` or left stick after thrust is rebound. | Describe all keyboard composite parts or the effective pad vector binding. | The player can apply the same flight rule with their chosen controls. |
| Map zoom still says `W / S` or left stick although it reads the rebindable thrust action's Y axis. | Describe only the keyboard Up/Down parts, or the bound vector control with an up/down direction cue. | Information about an action matches what actually moves the map. |
| The simple hint helper assumes pad binding index 1. | Select the device group and composite parts rather than an assumed array position. | A movement composite places the pad at index 5; a technically incorrect label undermines learning. |

The nearby pickup prompt also uses a cached active-device label. A shared plain-C# refresh gate limits device-only text rebuilds to four per second; accepted binding changes bypass that limit. A two-second periodic refresh catches layout changes. Flight remains on its existing 0.125-second text tick. These intervals control description work; they do not delay gameplay input.

This addresses a demonstrated source inconsistency and supports constitution principles 2, 3, 7 and 10. Whether it improves perceived competence or enjoyment is still a hypothesis. The patch adds no scoring, reward schedule, difficulty adjustment, content or art.

## Fable's next design pass

These are ordered candidates, not instructions to expand Phase 1. Start with observation of existing features, and choose one demonstrated problem at a time.

| Existing part of the game | Question for Fable | Codex's supporting work after Fable chooses the change |
| --- | --- | --- |
| Arena shooting, disarming and severing | Can a new player distinguish a hit, a disarm and a kill, and explain a different next shot? Do the cues remain legible with comfort effects disabled? | Trace state-to-feedback consistency, cover edge cases and preserve Fable's selected cues and timing. |
| Flight, braking and landing | Can the player predict drift versus facing and identify why a landing failed? Does information teach a skill without flying for them? | Check reference-frame units and boundary cases; implement Fable's selected explanation or instrumentation. |
| Exploration | Can a player identify an interesting question and a plausible way to answer it? Does the discovery change understanding or available choices? | Audit existing interaction and navigation information. Fable creates landmarks, silhouettes, clues and discoveries. |
| Existing same-seed restart and arena hold-fire controls | Do these let players practice intentionally without feeling obliged to repeat? Is the next attempt meaningfully different? | Keep reset behavior deterministic and controllable; repair inconsistent state without changing the challenge. |
| Main audio plus pending ship, launch and look work | Does the integrated experience communicate thrust, danger and recovery consistently? | Resolve integration correctness with Fable, using [the launch/audio handoff in draft PR #25](https://github.com/hmblasko/Orbital-ante/pull/25). Audio is already in this main snapshot; pending ship, launch and look changes are not. |

### Points, antes, and rewards: defer implementation until Phase 2 is authorized

The constitution and build order already put these beyond the current refinement work. The research does not supply a mathematically optimal points economy. Before coding one, Fable should specify:

1. The choices the score should support: what can be earned, spent, banked or lost, and when the player learns each rule.
2. Which distinct activities should remain viable. Compare earnings and opportunities across exploration, travel and fighting without turning every activity into mandatory equalized work.
3. Which commitment creates risk and how a player can predict it. Make eligibility, rounding and banking rules consistent between preview and commit; state uncertainty honestly.
4. How repetition is handled without hidden diminishing returns, invisible eligibility changes or pressure to play daily. Any anti-farming rule needs a comprehensible design reason.
5. Which incentives preserve experimentation and sidegrades. Test whether players still choose an interesting approach when a safer repetitive route earns more points.

An ordinary points system may fit the game. The evidence argues against assuming that increasing its output proves the game became more enjoyable. Do not add an economy simply because reinforcement is measurable.

## A small evaluation before more changes

Use the [specific control-hint protocol](../../reports/fun-control-hints.md#playtest-protocol-for-fable) for this patch. For a later design change, select one encounter or decision, state the suspected breakdown and prediction, and write the acceptance criteria before observing the result. Counterbalance comparison order when useful; record practice and carry-over effects. Keep major tuning and content constant during a feedback comparison.

Ask what the player intended before acting and what caused the outcome afterward, without supplying the answer. Record correct understanding and repeated errors alongside task success. Collect enjoyment, control, challenge and pressure as separate questions. An optional further attempt is useful context, not evidence of long-term retention. A few sessions can expose problems; they cannot establish population-level psychological effects or replace longer playtesting.

## Handoff language for Fable

> Read `docs/research/fun-and-motivation/README.md`, both linked evidence files, and `docs/reports/fun-control-hints.md`. This is new psychology research, with causal and transfer limits, plus one bounded control-hint correction. You retain all design, creation, art, feel and tuning decisions. Review the implementation and its unresolved Unity/Deck checks before adopting it. Use the findings to identify one observed Phase 1 comprehension, agency or learning problem; specify the intended player experience, smallest change, acceptance criteria and comparison protocol. Delegate exact engineering work to Codex where useful. Keep reward economies and other Phase 2 systems deferred until authorized. Evaluate understanding, enjoyment and control separately from points, success rate and time played. Do not treat these papers or this patch as proof that a feature is fun.
