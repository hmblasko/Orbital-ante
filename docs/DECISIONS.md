# Decisions and open questions

Matthew said "I'll defer to you" on 9 Nov 2026, so these are the agent's provisional calls.
Anything marked **provisional** can be changed by Matthew at any time.

| # | Question | Current answer | Status |
|---|---|---|---|
| 1 | Official faction names | Placeholders "Faction A/B/C" only. The old Godot doc's working names are not carried over. | Waiting on Matthew |
| 2 | Ante timer length, what ends an ante | Not needed until Phase 2 (Milestone 14). Will propose numbers then. | Open |
| 3 | Weapons and melee in the first playable | Milestone 3: AK-47 spoof + test pistol. First Phase 2 playable: AK spoof, double-barrel laser shotgun, throwable rock, baseball bat. | Provisional |
| 4 | Does the skateboard return? | Left out until Matthew asks for it. | Deferred |
| 5 | "Unlimited ammo" vs magazines/reload | Endless reserve ammo, but real magazines and reloads, so reload timing still matters. | Provisional |
| 6 | One-handed reload toggle default | Default: one hand can still reload, just slower. Toggle switches to "no reload with one hand". | Provisional |
| 7 | Mannequin animation source | Humanoid rig is real; clips (e.g. Mixamo walk/crawl) can drop in later. Until then a procedural gait animates every state. | Provisional |
| 8 | Repo layout | Unity replaces Godot on `main`; Godot archived on `archive/godot-rust`. | Decided by Matthew |
| 9 | Test-only pistol vs one-gun rule | The pistol exists only in the test arena so the two can be compared. In the real game you carry one gun + melee + rope gun. | Provisional |
