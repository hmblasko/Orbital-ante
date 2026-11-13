# Coordinated enemy tactics for Orbital Ante

Research prepared 13 November 2026 for the request to replace aimless enemy movement with deliberate group attacks. This is a targeted technical review of eight original developer publications, not an exhaustive literature review or a player study. The relevant technical sections of the linked papers/slides were read, including their constraints and failure cases. These sources document engineering approaches in other games; they do not establish that a particular tactic, attacker count or timing will be fun here.

Fable 5.2 retains design, creation, asset, presentation and tuning authority. Use general algorithms to create an original implementation; do not copy another game's characters, encounters, visual language or proprietary code. Current Orbital Ante has arena mannequins, direct locomotion, shooting/reloading, damage-dependent capabilities and eight arena enemies. The appropriate first implementation is coordinated behavior for that existing combat slice, rather than new factions, enemy types or a planet-wide strategic war.

## Decision

Use a **bounded squad coordinator, utility-scored tactical positions and a small individual execution state machine**. The coordinator assigns complementary jobs and reserves positions/attack opportunities. Individuals retain perception, reaction time, aiming, weapon rules and damage responses. Navigation must prove a route is traversable; separation alone cannot create a flank around a wall. A reason to move, a reachable destination and an interrupt condition should explain every combat movement.

This is an engineering recommendation derived from the sources and the inspected code, not a measured result. More planning complexity is not itself better intelligence. The useful outcome is a group that visibly divides work, changes approach when circumstances change and still gives the player understandable counterplay.

## Eight primary sources

### 1. Tactical positions and paths

**Arjen Beij and Remco Straatman (2005), _Killzone's AI: Dynamic Procedural Tactics_, GDC Europe.** [Original studio page](https://www.guerrilla-games.com/read/killzones-ai-dynamic-procedural-tactics); [39-slide presentation](https://www.guerrilla-games.com/media/News/Files/gdce05_killzone_ai.pdf), especially slides 6–18 and 22–32.

The presentation scores candidate positions using factors such as range, proximity, line of fire and cover. It also adds tactical costs to paths: the shortest route may cross friendly firing lanes or expose the agent. Its suppression example targets reachable firing opportunities near a hidden opponent rather than repeatedly shooting the wall hiding them.

**Use:** evaluate both destination and route, and reject blocked firing positions. **Limit:** a production case study, not a controlled comparison; it assumes usable navigation and world-query information. Its particular weights, suppression behavior and map representation are not requirements for Orbital Ante. If the first patch only fires at visible targets, describe that as covering/support fire, not a complete suppression system.

### 2. Individual planning and squad coordination are different jobs

**Jeff Orkin (2006), _Three States and a Plan: The A.I. of F.E.A.R._, GDC.** [Original paper, mirrored](https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf); [conference record](https://www.gdcvault.com/play/1013282/Three-States-and-a), particularly pp. 3–4 and 13–15.

Orkin separates action planning from low-level execution. Squad behaviors find suitable participants, give orders, reserve valid cover and monitor success or interruption. Crucially, the paper explicitly says its shipped squad behaviors did **not** include a complex flanking planner: apparently sophisticated pincers could emerge from coordinated cover movement and available routes. Its individual GOAP system and comparatively ad-hoc squad logic were separate.

**Use:** coordinate complementary roles while letting disarming, death or urgent local needs interrupt an order. **Limit:** this is not evidence that GOAP is necessary, nor proof that emergent repositioning always produces a deliberate flank. Orbital Ante should explicitly measure useful approach angles and preserve routes when intentional flanking is the requirement.

### 3. Generate, filter, score, schedule and explain

**Matthew Jack (2013), _Tactical Position Selection: An Architecture and Query Language_, Game AI Pro, chapter 26, pp. 337–359.** [Author/editor-authorized chapter](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter26_Tactical_Position_Selection.pdf), sections 26.3–26.7.

The architecture separates candidate generation, hard eligibility tests and soft preferences. Failure to find a valid point informs the higher-level behavior. Group-level selection can reduce contention, but tightly couples behaviors. The performance discussion prioritizes cheap tests before expensive queries and schedules/timeslices evaluation; debug views show why points passed or failed.

**Use:** first reject occupied, unreachable or unsafe candidates; then compare tactical value. Expose the chosen role, goal, score and rejection reason in development tools. **Limit:** a detailed industry architecture rather than a universal benchmark. A query language, asynchronous scheduler and the whole commercial system would be excessive for the current arena; implement only the necessary abstractions.

### 4. Position ownership and permission to attack are separate

**Michael Dawe (2013), _Beyond the Kung-Fu Circle: A Flexible System for Managing NPC Attacks_, Game AI Pro, chapter 28, pp. 369–375.** [Authorized chapter](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf), especially sections 28.3–28.6.

The described combat manager distinguishes spatial participation from concurrent attack capacity. It rotates opportunities, considers different attack costs and locks assignments during active attacks so reassignment does not accidentally exceed capacity. Waiting agents still occupy useful approach positions.

**Use:** finite firing leases can prevent a synchronized dogpile while other members maneuver. Release leases on death, disarm, loss of eligibility or timeout; avoid permanent monopolies. **Limit:** the implementation concerns an action RPG with substantial melee combat. Its circular layout, weights and capacities should not be transplanted into a first-person shooter. The correct pressure and permit count are Fable's tuning questions, not published constants.

### 5. Knowledge must come through perception

**Rich Welsh (2013), _Crytek's Target Tracks Perception System_, Game AI Pro, chapter 31, pp. 403–411.** [Authorized chapter](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter31_Crytek%27s_Target_Tracks_Perception_System.pdf), sections 31.3–31.6.

The system represents observations as stimuli and maintains decaying target tracks. Range, interest and field-of-view filters avoid unnecessary visibility rays. The article distinguishes reacting to nearby incoming bullets from immediately knowing their shooter's exact location.

**Use:** squad knowledge should be reported observations with timestamps; after sight is lost, pursue the last observed location and eventually stop or search. Personal shooting still requires actual visibility and the existing reaction/aim rules. **Limit:** the full sensory/envelope system is more elaborate than this slice needs. A timestamped sight memory is an adaptation, not a replication. Shared squad awareness must not become unrestricted access to the player's current transform through walls.

### 6. Stable spatial organization supports readable combat

**Tobias Karlsson (2021), _Squad Coordination in Days Gone_, Game AI Pro Online Edition, chapter 12.** [Original chapter](https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter12_Squad_Coordination_in_Days_Gone.pdf), sections 2, 5–7.

The squad assigns roles and general spaces; individuals execute them and may prioritize urgent behaviors. A modeled frontline and lanes help avoid bunching and communicate combat direction. The chapter emphasizes stability: continuously changing spatial relationships can make agents spend all their time repositioning. Thresholds and smoothing temper that response while allowing large changes to matter.

**Use:** commit to a useful flank/anchor goal until it completes, becomes invalid or a materially better response is justified. Do not rotate all goals whenever the player looks around. **Limit:** this is an author-reported production approach, not an experiment establishing player comprehension. Its confidence model, factions and deliberate response handicaps are not needed for this implementation.

### 7. A director is not a flank planner

**Michael Booth (2009), _The AI Systems of Left 4 Dead_, AIIDE.** [Original Valve slides](https://cdn.cloudflare.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf); [Valve publication catalog](https://www.valvesoftware.com/en/publications), slides 11–14, 37–43 and 77–92.

Booth separates locomotion, vision, intention and actions. Reactive local path following complements planned routes, and action transitions carry reasons that help debugging. The director's intensity model modulates encounter population/pacing; it is distinct from the agents' combat decision-making.

**Use:** retain a clear boundary between group tactics, movement and any future encounter pacing. Instrument action transitions rather than attributing every movement to one opaque “AI” function. **Limit:** this presentation does not provide a plug-in flanking algorithm or justify new adaptive spawning, hidden difficulty adjustments or a director in the current patch. Reported robustness in its navigation environment does not establish robustness in generated planets.

### 8. Crowd scaling needs bounded local work

**Craig W. Reynolds (2000), _Interaction with Groups of Autonomous Characters_, GDC, pp. 449–460.** [Author page](https://red3d.com/cwr/papers/2000/pip.html); [paper](https://red3d.com/cwr/papers/2000/pip.pdf), particularly pp. 7–10.

The interactive flock demonstration separates animation, behavior and more frequent collision-avoidance updates. Staggering behavior phases spreads work. Spatial subdivision replaces scanning the full population for every neighbor query; its favorable scaling depends on bounded local density.

**Use:** separate expensive planning from movement, bound local separation neighbors and stagger sensing/path requests. **Limit:** a flock is not a tactical squad, and separation cannot choose a meaningful attack goal. Historical demo rates and performance do not transfer to Unity or Deck. Spatial bins still require occupancy/query caps because dense clusters can recreate expensive work.

## Three viable architectures

| Architecture | Strength | Risk for this project | Recommendation |
| --- | --- | --- | --- |
| Individual FSM/behavior tree with shared blackboard and reservations | Easy to integrate with the current shooting/locomotion code; explicit priorities. | Independent agents can all choose the same tactic; reservations alone do not assign complementary jobs. | Suitable execution layer, with a real coordinator above it. |
| Utility-scored squad roles and positions plus small execution states | Responsive to current geometry/capabilities; bounded candidate set; decisions can be explained. | Needs comparable scores, goal stability, contention handling and genuine reachability checks. | Best fit for the existing arena. Keep hard invalidity separate from soft preference. |
| GOAP/HTN-style multi-step planner plus navigation | Useful when a broad action vocabulary creates many valid sequences and dependencies. | State modeling, plan invalidation and search costs increase sharply; a planner does not supply missing paths, perception or attack fairness. | Reconsider when the action catalog warrants it; not required to achieve coordinated flanks now. |

## Proposed implementation contract

These are original project recommendations, not a source's shipped recipe or claims of implemented behavior.

1. **Observe:** cheap range/FOV filtering precedes line of sight. Feed successful sightings into timestamped shared memory. No current-position updates when nobody can perceive the player. Expired information becomes a bounded search/hold decision, not clairvoyant pursuit.
2. **Assign complementary work:** a pressure/anchor member maintains a firing opportunity, mobile members take left/right approaches, and remaining members hold useful support positions. One or two eligible enemies receive simpler plans. A disarmed actor seeks a reachable weapon or protection; a crawling actor should not receive a running flank; a head-only actor should not reserve a combat slot.
3. **Pick purposeful positions:** evaluate angle to the established engagement direction, weapon range, actual shot visibility, cover opportunities, path length and space reserved by allies. Reject impossible candidates before scoring. A successful flank must change the approach angle, not just have a role named “flanker.”
4. **Reach them deliberately:** use actual navigable space. A bounded flat arena grid is a reasonable first adapter because current locomotion has no NavMesh. Prevent diagonal corner cutting and capsule-width shortcuts. Revalidate blocked segments, detect lack of progress and choose an explicit fallback; do not replace a failed route with a random perpendicular direction.
5. **Control concurrency:** reserve tactical destinations and lease a configured number of firing opportunities. Preserve reaction, aim, muzzle obstruction, reload and one-handed penalties. Allies must not shoot through each other merely because the player's line is clear. Replace stale/dead/ineligible lease holders fairly.
6. **Stabilize and interrupt:** retain valid goals; replan for a lost route, meaningful target movement, changed capability or completion. Immediate death/disarm/hold-fire events override plans. Do not add randomness as the reason to move; seeded randomness may only break otherwise equivalent decisions or preserve existing weapon variation.
7. **Bound work:** cap participants, candidates, grid dimensions, node expansions and physics probes; stagger requests and reuse buffers. A large group must degrade to fewer updates or simpler valid choices without skipping safety checks. Enforce caps in code rather than relying on eight present enemies forever. New deterministic decisions use named seeded streams or stable tie-breaking, never Unity random.

## Acceptance and limits

| Principle | Concrete implementation evidence | Failure to reject |
| --- | --- | --- |
| Complementary roles | An anchor keeps pressure while a mobile member follows another approach. | Every actor receives the same destination or periodically swaps sides without cause. |
| Tactical validity before preference | Selected point has clearance, a route and a role-appropriate firing/cover opportunity. | A high-scoring point lies behind an impassable wall. |
| Limited knowledge | Pursuit uses the last reported sighting with a visible age/expiry in diagnostics. | Hidden player movement continuously changes the squad's goal. |
| Attack concurrency | Active leases remain within the configured cap through death, reload and disable events. | A removed lease owner continues firing or one actor monopolizes permission. |
| Goal stability | A valid goal persists until completion, meaningful change or an explicit interruption. | Small target changes cause repeated route reversal. |
| Capability awareness | Damage immediately changes eligible roles and preserves weapon/locomotion penalties. | A handless actor suppresses or a head-only actor claims a flank. |
| Bounded cost | Counters show enforced work limits and fair scheduling under stress. | Increasing enemies silently multiplies uncapped physics or all-pairs work. |

Before claiming a sophisticated swarm, inspect evidence of coordination: distinct reachable lanes, an anchor while others reposition, useful attack angles, finite concurrent attacks and adaptation after a casualty. An AI debug view should show role, observed-target age, reserved goal, path failure reason and permit owner. These are development diagnostics; Fable chooses player-facing audio/animation cues and their style.

Plain-rule cases should cover one/two/many members, no eligible flankers, disarm/death during an attack lease, lease expiry/fairness, no duplicate goal claims, stale sight information, a wall separating a good-looking point from a reachable one, blocked corners, deterministic equal-score choices and planner caps. Engine checks must cover real collider clearance, moving props, hold-fire/restart cleanup, body loss, friendly firing lanes and the route actually followed by locomotion.

For a formative comparison, keep the same seed, enemies, weapon values and arena geometry. Observe whether players can describe the group's intended pressure and find a counterplay, and whether the flanks actually arrive rather than oscillate or stall. Ask perceived fairness and enjoyment separately from deaths or damage taken. Record profiler CPU time, physics queries, allocations and path work at present and stress counts on Deck. No source review or plain test can establish those outcomes.

This first adapter is for the existing flat arena. Terrain, arbitrary gravity, zero-G enemies, dynamic navigation rebuilding and very large crowds require separate adapters and measurements. The implementation report must say which pieces actually shipped in the patch and which remain proposals; research recommendations are not permission to claim an unbuilt sensory, suppression or strategic system.
