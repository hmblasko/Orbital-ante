# Which actions should earn points in Orbital Ante?

A good test for the first salvage job is what the player does when they pick up the last crate on a moon. If they turn toward their ship because they understand the delivery, the objective is doing its job. If they leave the crate because the HUD announced the world was clear, increasing the payout will do little for that run. This review connects research on engagement to the game's existing score system and to a Deck playtest Fable can run.

## What the research found

| Study and setting | Result | Application and limit |
|---|---|---|
| [Kao et al., CHI 2024, preregistered action RPG experiment, 1,699 participants](https://people.csail.mit.edu/dkao/pdf/3613904.3642656.pdf) | Feedback contingent on successful actions increased perceived competence, curiosity, and the player's sense of causing effects. Amplifying the effects in this game could weaken those experiences. | Make pickup and delivery sound and read differently. Reserve the score receipt for the moment that earns it. The experiment gives no optimal animation length, payout, or volume for a Deck. |
| [Ryan, Rigby & Przybylski, 2006, four video game studies](https://doi.org/10.1007/s11031-006-9051-8) | Perceived competence and autonomy predicted enjoyment and preference. | Give the player enough information to choose a route and judge its consequences. These associations do not establish which HUD layout causes enjoyment. |
| [Harkin et al., 2016, meta-analysis of 138 randomized studies and 19,951 participants](https://eprints.whiterose.ac.uk/id/eprint/91437/) | Interventions promoting progress monitoring improved goal attainment on average, standardized mean difference 0.40, 95% CI 0.32–0.48. The outcome covered multiple domains. | A carried crate remains outstanding until delivery. A counter can improve orientation. This evidence does not predict whether retrieving a crate feels good. |
| [Mekler et al., 2017, experimental image tagging task](https://edoc.unibas.ch/entities/publication/ccec8ccc-aa54-417d-b205-334b518d6eed) | Points, levels and leaderboards increased the quantity of tags. The study found no significant improvement in intrinsic motivation or perceived competence. | More kills after increasing kill payouts could simply mean players followed the incentive. Ask whether they still preferred combat when salvage was available. |
| [Gómez-Maureira et al., 2021, open-world exploration experiment with 254 participants](https://doi.org/10.1145/3474698) | Spatial design patterns influenced exploration. Having an explicit goal and expecting compensation changed the effect. | Give directions for the crate a player chose. Once it is delivered, let the player decide where to go. A study of one world cannot tell us the right number of map markers. |
| [Abuhamdeh, Csikszentmihalyi & Jalal, 2015, competitive game experiments](https://doi.org/10.1007/s11031-014-9425-2) | Close contests could be more enjoyable through suspense. Participants sometimes chose to replay them over easier wins. | Overtime and the choice to bank are promising places for suspense. A competitive match does not tell us what share of first-time solo players should die at ante four. |
| [Burgers et al., 2015, brain-training game experiment with 157 participants](https://doi.org/10.1016/j.chb.2015.01.038) | Negative feedback could increase immediate play while reducing perceived competence. | Track retries alongside the player's reason for retrying. Replaying to master a landing and replaying because an instruction was hidden demand different design decisions. |

## How the existing points can support a run

The first ante asks for 850 points. In the default balance, an ordinary first-tier enemy is worth 45 base points, a signal mast 180, and a delivered crate 240. The award can change with the ante, skill and risk. The wallet and progress toward the ante are tracked separately, so spending points does not erase the achievement. A run can reward both the pilot who seeks fights and the pilot who retrieves salvage. The earned score, purchase balance and non-currency benefits such as Focus should remain distinguishable in play. [Balance](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Ante/AnteBalance.cs) · [Award calculation](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Ante/ScoreMath.cs) · [Run ledger](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Ante/RunLedger.cs)

Three consequences matter in this build:

1. **The crate sets a destination.** Picking it up, putting it down and picking it up again all leave one delivery outstanding. While carrying it the player sees the ship's lift as the destination. Boarding completes the job and claims the points. Once the marked tasks run out, the HUD reports that fact without instructing the pilot to abandon the world. [Task director](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Tasks/TaskDirector.cs)
2. **A supply pod offers an intelligible choice.** Take a player with 90% Focus and one kill left before the next gun. With default pod settings, a tap offers points and the remaining 10% of the meter. Holding offers points and one gun credit. With a full meter, the tap still earns the pod's points. Previews and receipts use the actual available benefits, so the player can decide what is worth more on this run. [Pod rules](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Tasks/TaskRules.cs)
3. **The first goal has a readable start and end.** The first-run card states when the clock begins. Opening the system map or watching the cockpit feed preserves a guide tip's reading time. Once the target is reached, the guide no longer tells the player to reach it. [Run guide](../../unity/Assets/OrbitalAnte/Scripts/Runtime/Tasks/RunGuideRules.cs)

## What the simulation can tell Fable

I ran the repository's simulator with 1,000 runs per row. Under the overtime policy, its first-time, average and skilled profiles cleared all nine antes in 8%, 89% and 100% of simulated runs respectively. Half of the modeled first-time runs reached ante four because the simulator **calibrates danger to produce that result**. The separation between profiles makes the assumed kill, discovery and damage rates worth checking against actual sessions before adjusting targets. These runs do not exercise the HUD, the controller or a person's choice to keep playing. [Simulator and reproduction command](../../tools/run-sim/README.md)

## First Deck session

Start a fresh profile or reset the guide tips. Use a fixed seed and the same control bindings when comparing builds. Let the player choose their first activity without coaching.

| Moment | Observe or ask | What would justify another change? |
|---|---|---|
| Leaving the pad | Ask “What are you trying to earn, and what starts the clock?” Have them open the map or cockpit feed during a tip and return to play. | They lose a tip while reading the map or cannot identify a first paying activity. |
| Carrying the last crate | Ask “What happens if you put it down, and when do you get paid?” Observe pickup, drop, recovery and delivery. | The player concludes they have already scored, leaves their ship behind, or sees the task disappear before delivery. |
| Opening a pod | From near-full Focus and one credit short of a gun, ask the player to predict both rewards. Try tap and hold in separate runs. | The received benefit differs from the visible choice or the player has to guess what the hold buys. |
| Completing an ante | Ask what they earned, what they can spend, and which activity they would choose on a second run. Ask what made that activity enjoyable or tedious. | Players chase one payout despite preferring another activity, or immediately quit because they cannot explain the next goal. |

Record the player's prediction and the outcome separately from enjoyment. A wrong prediction suggests a problem with feedback. A correct prediction followed by “I wouldn't choose that again” points to the activity, pacing or trade-off itself. Leave the final feel and tuning decision with Fable after observing real play.
