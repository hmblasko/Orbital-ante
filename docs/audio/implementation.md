# Audio implementation guide

How the audio system is put together, where to change things, and how to hear and inspect it.

## The short version

- Game code asks for sound by name: `AudioDirector.Play("weapon.rifle.shot", position)`. It never names a file.
- The names, files, levels, variations and limits are all in one manifest, `Resources/Audio/audio_manifest.json`, which the audio build writes. Game and files cannot drift apart.
- Music is not "played", it is directed: the game reports a situation and a danger level, and the music director decides what plays.
- If the audio files are missing, the game falls back to the old code-made placeholder sounds, so nothing breaks.

## The parts

All in `unity/Assets/OrbitalAnte/Scripts/Runtime/Audio/`.

**Rules (plain C#, no Unity types, all unit tested):**

| Class | Job |
|---|---|
| `AudioManifest`, `MiniJson` | Reads the manifest. `Validate` lists anything wrong with it. |
| `IntensityModel` | Turns events (shots, hits, damage) and steady threat into the 0 to 100 intensity and its band. |
| `MusicBrain` | The music director. Given the clock, the situation and intensity, it issues commands: start this piece on that beat, fade this layer in on the next bar, play the riser now. |
| `GainLine`, `MixMath` | Scheduled fades and level arithmetic. |
| `RewardRules` | Which reward sound plays when several arrive at once. |
| `AcousticsModel` | What vacuum, thin air, caves and cabins do to a sound. |
| `VoiceAllocator` | The voice budget: who gets a voice, who gives one up. |
| `DemoTimeline`, `DemoSimulator` | The five-minute showcase script, and a way to run it with no Unity. |

**Unity side:**

| Class | Job |
|---|---|
| `AudioDirector` | The one entry point. Pooled voices, looping beds, rewards, owns the music player and the intensity model. |
| `AdaptiveMusicPlayer` | Carries out the director's commands with audio sources scheduled on the audio clock. |
| `AudioPrefs` | The player's four volume settings. |
| `GameAudioHooks` | Connects today's game to the audio (hits, damage, footsteps, engine, which music situation). |
| `AudioDebugPanel` | The F9 readout. |
| `AudioDemoSequence` | Runs the showcase from the main menu. |

**Editor:** `AudioImportRules` sets every audio file's import settings automatically. `AudioTools` adds two menu items under Orbital Ante > Audio.

## Using it from game code

```csharp
AudioDirector.Play("impact.metal", hitPoint);            // a sound in the world
AudioDirector.PlayLocal("ui.select");                     // a sound in the player's head
AudioDirector.Instance.SetLoop("ship.engine.loop", 0.8f); // a bed; level 0 fades it out and stops it
AudioDirector.Instance.Reward(RewardKind.Tier2);          // a reward (the rules decide if and when)
AudioDirector.Instance.Intensity.Report(IntensityEvent.DamageTaken);
AudioDirector.Instance.Intensity.SetThreat(55f);          // steady danger, e.g. enemies that know where you are
AudioDirector.Instance.SetMusicContext("surface");        // menu, surface, flight, mystery, space, silence
AudioDirector.Instance.Space = new ListenerSpace { Air = 0f, Enclosure = Enclosure.Open }; // vacuum
```

`Play` returns false if the sound did not play, which is how the old placeholder code knows to step in.

## Events in the slice

| Family | Events |
|---|---|
| Weapons | `weapon.rifle.shot`, `weapon.smg.shot`, `weapon.handcannon.shot`, `weapon.sniper.shot`, `weapon.sniper.bolt`, `weapon.suppressed.shot`, `weapon.laser_revolver.shot`, `weapon.raygun.shot`, `weapon.laser_shotgun.shot`, `weapon.gravity.grab`, `weapon.gravity.hold` (loop), `weapon.gravity.punt`, `weapon.mag_out`, `weapon.mag_in`, `weapon.bolt_rack`, `weapon.dry_fire` |
| Combat | `impact.metal`, `impact.ricochet`, `impact.dirt`, `impact.flesh`, `impact.armor`, `gore.pop.small`, `.medium`, `.big`, `.torso`, `.head`, `gore.splat`, `hit.marker`, `hit.headshot`, `hit.kill` |
| Thrusters | `ship.thruster.nose.loop`, `.left.loop`, `.right.loop`, `.belly.loop`, `.roof.loop`, `.turn.loop`, `ship.thruster.start`, `ship.thruster.stop`: driven by one call, `AudioDirector.SetShipThrust(localPush, turn, boost)` |
| Ship | `ship.engine.loop`, `ship.engine.ignite`, `ship.rcs`, `ship.cabin.loop`, `ship.switch`, `ship.confirm`, `ship.seatbelt`, `ship.touchdown` |
| Player | `player.step.regolith`, `player.step.metal`, `player.land`, `player.jetpack.loop`, `player.jetpack.burst`, `player.breath.loop`, `player.suit.alarm`, `player.hurt`, `player.interact` |
| Ambience | `amb.wind.thin`, `amb.wind.thick`, `amb.cave`, `amb.facility`, `amb.radio.static`, `amb.space.suit` |
| Rewards | `reward.points.tick`, `reward.points.rack` (loop), `reward.points.rack_end`, `reward.tier1` to `reward.tier4`, `reward.multiplier`, `reward.loot.common`, `reward.loot.rare`, `reward.discovery`, `reward.repair` |
| Interface | `ui.move`, `ui.select`, `ui.back` |

## What is wired into the game today, and what is waiting

| Wired now | How |
|---|---|
| Rifle and pistol shots, magazine out and in, dry fire, bullet hitting a gun | `WeaponAudio` asks the director first. The test pistol uses the hand cannon sound. |
| Bullet hitting the world | `WeaponFx.Impact` |
| Limb pops and splats | `GoreAudio` |
| Hit marker, headshot, kill | `GameAudioHooks`, from `CombatEvents` |
| Footsteps and landing in the arena | `GameAudioHooks` |
| Ship engine and cabin in the solar system | `GameAudioHooks` (the craft's old rumble is muted, its file is untouched) |
| Menu clicks and moves | `UiKit.Button`, `GameAudioHooks` |
| Music everywhere, volumes in the pause menu | `GameAudioHooks`, `PauseMenu` |

| Made but waiting for its game system | Needs |
|---|---|
| SMG, sniper, suppressed pistol, laser revolver, ray gun, laser shotgun, gravity gun | Those weapons (set `shotEvent` on the weapon's data asset) |
| Jetpack, breath, suit alarm, vacuum treatment on foot | The suit and jetpack (Milestone 11) |
| Seatbelt, switches, confirm, touchdown, RCS | The ship interior and cockpit (Milestones 7 to 10) |
| Reward ladder, score count-up, loot, discovery, repair | Points and loot (Phase 2). Audible now in the showcase. |
| Cave, facility, radio and wind beds | Hooks into `PlanetWorld` for where the player is standing |
| Storm wind and thunder | Still the old code-made sounds in `PlanetWeatherFx` |

## Mixing

- Eight buses (`AudioBus`). Each event names its bus in the manifest.
- Levels are set in code: event level x bus x the player's slider, with 4 dB of headroom on the whole output (`MixMath.HeadroomDb`), because Unity clips rather than limits.
- An AudioMixer asset is optional. If `Resources/Audio/OrbitalAnteMixer.mixer` exists, every sound is routed to the group named after its bus, and you can watch and treat each bus in Unity's Audio Mixer window. The menu item Orbital Ante > Audio > Create Mixer Asset tries to make one; Unity has no public way to do that from a script, so it may not work in your Unity version. If it does not, it changes nothing. Mixer snapshots are not used: the same job (ducking, pause, vacuum) is done in code where it can be tested.

## Changing the music's behaviour

The rules are data, in `tools/audio/music/__init__.py` (`DIRECTION`): which piece belongs to which situation, the intensity at which combat starts and stops, and how each pair of pieces hands over (quantise to beat or bar, which cue, fade times, hush). Each stem's intensity range is in its piece's file (`META`). After changing either, run `python3 tools/audio/build.py music --only cue` to refresh the manifest.

## Checking it

| What | Where |
|---|---|
| Rule tests | `Tests/EditMode/AudioRulesTests.cs`: 26 tests. In Unity: Window > General > Test Runner > EditMode. |
| File measurements | `python3 tools/audio/qa.py` writes `docs/audio/qa_report.md`. |
| Every file present | In Unity: Orbital Ante > Audio > Check Audio Files. |
| Live readout | F9 in the game. |
