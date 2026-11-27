# Run simulator

Plays thousands of made-up runs through the game's real ante, score, perk, shop, scratch-off, hazard and
enemy-pressure rules (every Unity-free file in `Scripts/Runtime`, compiled the same way as `tools/rules-tests`).

```
cd tools/run-sim
dotnet build -c Release
dotnet bin/Release/net8.0/OrbitalAnte.RunSim.dll 1000 > report.md
```

1000 runs a row takes about 20 seconds. The report has: the starting kits against Standard for each player model,
each player model under both missed-target rules, what each perk is worth when forced at the first break, draft
habits, the upgrade screen habits (break, field, rerolls), hazards, and ante by ante. `1000 kits` prints only the
kits table (about 10 seconds).

**The player is a guess.** `Profile` in `Sim.cs` (First-time, Average, Skilled) says how often it kills, gets hit,
lands somewhere new. Those numbers were made up, then the danger scale is calibrated so half of first-time players
reach ante 4 (Part B section 5). So the simulator answers questions about the shape of the rules (is one perk far
ahead, do deaths or clocks end runs, how long between rewards, does the wallet outgrow the shop), never about how
the game feels. Never quote it as a playtest result.

Perks the model cannot value (it has no melee, no long shots, no rover, no stances) come out at +0: Hot Brass,
Heavy Swing, In Your Face, Eagle Eye, Cold Blood, Road Rage, Last Stand. That is the model's blind spot, not the perk's
worth.
