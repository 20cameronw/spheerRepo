# Hyperspace Run - integration guide for the Unity agent

Hyperspace Run replaces the 2D travel mini-game (`TravelGame`) with a forward-moving 3D rail shooter/dodger played during warp travel.
The **deterministic simulation core** is pure C# (`namespace Frontier.Hyperspace`, C# 9, netstandard2.1, no `UnityEngine`, fixed step 1/60 s, zero steady-state allocations).
The Unity adapter renders its state and feeds it input. The original core API reference below was verified by 55 tests on CoreCLR and Unity's Mono.

## Unity adapter in this repository

The renderer now lives in `SpheerURP\Assets\Frontier\Scripts\HyperspaceView` (`Frontier.HyperspaceView`).
`FrontierBootstrap` registers `HyperspaceGame` with `TravelMinigames.Create`; development builds can select the legacy tap game in Settings > Debug.
The simulation source and its golden constants are unchanged.

* **Driver/input:** `HyperspaceSession` owns the fixed-step accumulator, drains every step, freezes on downed/pause/background, and resets catch-up time on resume. Relative pointer drag uses **panel units** and inverted y, retains the target on lift, and captures one pointer. Bomb taps stay latched until an actual simulation step consumes them.
* **Presentation:** an isolated URP camera explicitly renders into a full-bleed UI Toolkit texture. One reusable vertex-colour mesh batches ships, hazards, bullets, pickups, star streaks and bounded event bursts. There are no per-entity GameObjects or per-step instantiate/destroy calls. Laser gate geometry comes directly from `Hazards.Gate`; turret and Guardian fan markers use the simulation's spread/count. Safe-area HUD shows hull, shields, bomb recharge, combo, score, route/encounter progress, overdrive and Guardian phase/health.
* **Results/arrival:** results stay visible until Continue. The adapter returns `fuelRefundFraction` in the existing travel result's `quality` field and scales raw loot once. `UIRoot.TravelSequence` records each leg in `GameState.PendingTravel`; GameState settles arrival, fuel, completed-leg refunds/loot/XP and checkpoint removal together. Abandoning still permits arrival but grants no refund, loot or XP for that leg. Standalone sessions without a checkpoint still abandon on teardown and complete the callback once; checkpointed session teardown preserves the journal without invoking an arrival callback.
* **Revives:** absent a wallet/ad provider, `FrontierSettings.freeRevivesPerRun` defaults to one. Set it to zero when monetized revives ship. A non-null `TravelMinigames.ReviveOffer` replaces the free offer; it must answer on the Unity main thread. Duplicate/late callbacks cannot revive a finished or disposed session. Finish trip remains available while an offer is pending.
* **Audio/debug:** travel event cues reuse the preloaded authored audio library; starting a run creates no event clips. `AudioService` owns ten reusable one-shot voices shared with UI/combat cues plus an engine bed and music source (twelve sources total). The authored `FrontierAudio` mixer routes Music/SFX/UI separately; `Sfx` forwards legacy callers, and mute immediately stops active playback. Legacy synthesized cues use a bounded 64-clip numeric cache; eviction preserves clips still playing. Unseen randomized parameters still allocate when rendering a new clip, so the cache is not a zero-GC guarantee. Settings preserve assist and drag sensitivity; Debug exposes the performance overlay and an on-device comparison of all nine golden digests.

### Current-schema integration assumptions

Upgrade families are read by `ShipFamiliesFromGame` and interpolated between the simulation's starter/endgame stats:
reactor health -> hull/shields, turret damage/rate -> weapons, tap damage -> pierce, reward bonus -> magnet,
fuel efficiency -> handling, and player levels 8/16/24 -> extra bombs. Backpack capacity is deliberately excluded.
These are the upgrades available on this branch, not dependencies on a sibling upgrade redesign.
Loot maps Salvage -> `scrap`, Component -> `alloy`, Core -> `core`; missing resources are skipped.
There is no premium-wallet, rewarded-ad or Remote Config implementation on this branch; hooks do not pretend those services exist.

The mobile baseline uses flat neon/faceted geometry and a texture capped at 1280 pixels high, without bloom, realtime shadows,
chromatic post-processing or native haptics. Phone GPU/audio latency, touch feel and IL2CPP/ARM64 determinism still require device validation.
No cross-device replay sharing is enabled. The game pauses rather than simulating time spent backgrounded.
Local cold-start recovery reopens the saved run paused, or restores its downed/result dialog. Autopilot resumes its
saved remaining leg time; completed legs are not replayed for gameplay rewards.

### Durable local journeys

`SaveV2.travelPending` and `travel` hold a versioned `TravelCheckpoint` without changing the v2 schema. The
checkpoint preserves the original route, seeds, next-leg index, free-revive allowance and id-based leg rewards.
`TravelLegCheckpoint.hasReplay` distinguishes a real journal from Unity JSON's materialized null inline objects.
Old saves with no presence flags load without an active journey.

`RunReplay` records exact fixed-step input plus successful revive/abandon actions; repeated identical input is
run-length encoded. It freezes the original `RunParams` and `ShipStats`, checks the tuning fingerprint and
schedule digest, then verifies the reconstructed state digest before attaching the restored simulation. Historical
events are discarded rather than replaying audio/FX. Journals are bounded to 36,000 ticks per leg and the existing
route planner's maximum search depth, and allocate input history when enabled; the unrecorded Core simulation retains its pooled,
allocation-free step path. Bump `RunReplay.CurrentVersion` whenever simulation behavior changes incompatibly,
even if the schedule is unchanged.

The straight-shot/forward-bomb change uses replay version 2. Version-1 in-flight journals are deliberately
incompatible and use the existing interrupted-journey recovery/discard UI; they are never silently replayed
under different combat rules. New journals reconstruct the in-flight bomb position and spent charge.

The normal ten-second maximum save debounce covers active input; pause/focus/quit, downed/result, leg boundaries
and purchases flush immediately. Process termination can lose input since the last successful checkpoint. Paid
revive saves capture both the mutated run journal and wallet debit, so replay never purchases again. Final
settlement clears the checkpoint in the same snapshot as fuel and rewards. A failed write keeps the in-memory
transaction dirty and reports it; the previous durable checkpoint remains recoverable without duplicate grants.

`TravelMinigames.PrepareRecovery` is registered by the bootstrap before the UI resumes an interrupted journey.
It validates journal compatibility and completed-leg results using the live resource resolver. Incompatible,
damaged or no-longer-valid routes remain preserved until explicit discard, which returns to the origin without
a fuel charge or trip rewards; already consumed revive shards are not refunded. Read-only discard affects only
the current session. In-flight cloud snapshots cannot be manually restored in this build. Real Android/iOS
kill/relaunch, storage-pressure, save-size and frame-time acceptance remain device gates.

### Reproducible verification

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-checks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Platform EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Platform PlayMode
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\hyperspace\verify.ps1 -Tests
powershell -NoProfile -ExecutionPolicy Bypass -File tools\capture-screens.ps1 -Out SpheerURP\Screens\hyperspace-final -Devices phone-1080x1920,phone-1170x2532,phone-1080x2340,tablet-1536x2048 -Only hyperspace
```

The bundled-toolchain verifier needs no installed .NET SDK: it compiles the actual Core sources with warnings as errors,
compares nine goldens on CoreCLR/Unity Mono, and runs all 55 simulation tests (including allocation and fairness proofs) on CoreCLR.
Unity Mono cannot report thread allocation counts on this machine, so those two EditMode tests explicitly skip; CoreCLR verifies both.
The PlayMode adapter tests exercise pointer coordinates, render pixels without mutating state, pause, completion/abandonment,
free revive limits, duplicate/late offers, and overlay teardown. Harness shots cover intro, combat, downed, results, pause and Guardian.
The 24 captures in `SpheerURP\Screens\hyperspace-final` have zero small-text, undersized-touch or offscreen-panel lint findings
on the four listed virtual devices. Screens, NUnit XML, compiler outputs and logs remain git-ignored.
Local validation on Unity 6000.4.0f1: all five editor check suites passed; EditMode with `-IncludeSlow` passed 478 tests
with the two allocation-probe skips noted above; all 16 PlayMode tests passed; CoreCLR passed all 55 simulation tests.

The remaining reference retains paths from the original standalone core package. In this repository, `src\Hyperspace`
is `SpheerURP\Assets\Frontier\Scripts\Core\Hyperspace`, and `src\Tests` is `SpheerURP\Assets\Frontier\Tests\EditMode\Hyperspace`.
Standalone `run.ps1`/FloatLint commands mentioned below are not bundled here; the verifier above is the available runtime/golden/test gate.

* Sources: `src\Hyperspace\*.cs` (33 files) -> add to the `Frontier.Core` asmdef (`noEngineReferences`). `Hash.cs` (`Frontier.Universe.Hash`/`Rng`) is **already in Frontier.Core** and is used unmodified.
* Tests: `src\Tests\*.cs` (NUnit attributes, `[Category("Heavy")]` = statistical, skip on slow runners) -> `Frontier.Core.Tests` (EditMode).
* Tools (stay outside Unity): `runner\` (solver/balance/render CLI), `tools\FloatLint`, `crosscheck.ps1`, `monotests.ps1`, `lint.ps1`.
* `link.xml`: `<type fullname="Frontier.Hyperspace.HyperspaceTuning" preserve="all"/>` (the Remote Config key table uses reflection over its public fields).
* Name check against the repo (read-only grep of `SpheerURP\Assets\Frontier`): no type clashes inside `Frontier.Core`; two same-named types live in *other namespaces* - `Frontier.Universe.Hazards` (enum) vs `Frontier.Hyperspace.Hazards` (static class) and the internal `GateKind` in `Economy/SimPlan.cs` vs `Frontier.Hyperspace.GateKind` - so avoid `using` both namespaces in one file (or alias one).

## 1. Ten-line quick start

```csharp
var lib    = EncounterLibrary.CreateDefault();            // build once, immutable, share between runs
var tuning = new HyperspaceTuning();                      // defaults; apply Remote Config with TrySet (section 11)
ShipStats ship = HyperspaceBridge.ShipFromUpgrades(...);  // section 10
var p   = new RunParams(seed, distanceLy, planet.danger, planet.tier, ship.Power(tuning), RunMode.Normal);
var run = HyperspaceRun.Create(p, ship, tuning, lib);     // allocates every pool here, never again
// every fixed step (1/60 s):
run.Step(new PlayerInput(targetX, targetY, bombPressed)); // targetX/Y in play-area units, see section 4
int n = run.Events.Drain(eventBuffer);                    // HEvent[512]; react with FX/audio (section 6)
// after run.State == RunState.Ended (or Abandon()):
RunResult r = run.BuildResult();                          // grade, refund fraction, loot, xp, ...
```

## 2. Public API (exact)

### Creating and driving a run
| Member | Meaning |
|---|---|
| `static HyperspaceRun Create(RunParams, ShipStats, HyperspaceTuning, EncounterLibrary)` | Builds route + schedule (pure function of the inputs) and all pools. |
| `void Step(in PlayerInput)` | Advance exactly 1/60 s. No-op while `Downed` or `Ended`. Drain events after every step. |
| `bool Revive()` | Only when `State == Downed` and `RevivesUsed < tuning.maxRevives` (2): hull = 50 %, 3 s invulnerability, clears bolts and every enemy with `z < 75`, shield >= 1. Returns false otherwise. |
| `void Abandon()` | Finish the trip without refund or loot (valid while Running or Downed). Never a hard fail: the host always has this choice. |
| `void Reset()` | Replays the same route from step 0 (same plan, same seed). |
| `RunResult BuildResult()` | Safe at any time (preview); final once `State == Ended`. |
| `float ComputeQuality()`, `ulong StateDigest()`, `int LootCount(LootKind)` | Quality 0..1; digest of the full state (tests/telemetry); raw loot counters. |

### State you read (never write)
`State` (`Running|Downed|Ended`), `StepIndex`, `Time` (s), `Progress` (0..1), `Speed` (world scroll u/s), `Scroll`, `EndStep`, `Player` (`PlayerState`: x, y, vx, vy, hull/maxHull, shield/maxShield, shieldRegenTimer, invulnerable, overdrive, bombs/maxBombs, bombKillProgress, bombCooldown, combo, comboTimer, targetX/Y, maxSpeed),
`Enemies[]/EnemyHigh/EnemyCount`, `Bullets[]/BulletHigh`, `Bolts[]/BoltHigh`, `Pickups[]/PickupHigh` (iterate `0..High-1`, skip `!active`; slots are reused, `uid` identifies the spawn),
`Kills, HitsTaken, HullHits, MaxCombo, PickupsCollected, PickupsSpawned, RevivesUsed, Score, KillableSpawned, FuelCanisters, FuelBonus, BossDefeated, Abandoned, Defeated, CanRevive, SpawnFailures, ActiveBullets/Bolts/Pickups`,
boss HUD: `BossActive, BossPhase (1-3), BossHpFraction, BossVolleyEta/AimX/AimY/Count`. Route info: `Route` (`RouteSpec`: duration, hasBoss, bossStart, difficulty, budgetTotal, segments[] = pacing, speed[]), `Plan` (`Schedule`: records, encounters = `PlannedEncounter[]` with template id, anchorStep, kind) - useful for a "route preview" strip.

### Data types
* `PlayerInput { float targetX, targetY; bool bomb }` - target the ship should follow, **play-area units**; NaN/Inf ignored, values clamped to the area. `bomb` is a level (cooldown 0.8 s swallows a held button).
* `ShipStats { hull, shield (hits absorbed), shieldRecharge (s per charge), damage, fireRate, pierce, magnet, handling (follow speed x), bombCapacity }`; `Starter()`, `Endgame()`, `AtProgress(t)`; `tuning.ShipPower(stats)` = difficulty rating (starter = 1).
* `RunParams { ulong seed; float distanceLy, danger; int tier; float shipPower; RunMode mode (Normal|Assist|Challenge) }`.
* `RunResult { grade (S/A/B/C), quality, yieldMultiplier, killCount, hitsTaken, maxCombo, cleanRun, fuelRefundFraction, lootList[] (LootKind Salvage/Component/Core + count), reachedEnd, revivesUsed, bossDefeated, defeated, pickupsCollected, score, xp, elapsedSeconds, durationSeconds, digest }`.
* `HEvent { EventType type; int step, a, b, c; float x, y, z, v }` (field use per type in section 6) and `EventRing` (`Drain(HEvent[])`, `TryDequeue`, `Peek(i)`, `Count`, `Clear`, `Digest`, `Dropped`, capacity 512; the digest covers every pushed event, so skipping a drain never changes determinism).
* `HyperspaceTuning`: every number (270 keys, `reports\tuning_keys.txt`), `const float Dt = 1/60`, `AllKeys()/TryGet/TrySet/Clone()`, `ShipPower`, `ExpectedPower(tier)`, `MaxShipSpeed(handling)`.
* `EncounterLibrary` (`CreateDefault()`, `Count`, `this[i]`, `ById`, `CountAt(tier)`), `EncounterTemplate`, `TemplateBuilder` (authoring DSL, section 12).
* Tools you may also ship: `Bot`/`BotSkill` (novice/average/expert dodger: attract mode, auto-play for "skip", QA soak), `SelfCheck.Run(tuning, lib)` (9 digests, section 9), `FairnessSolver` (editor only).

## 3. Fixed-step driver (MonoBehaviour)

```csharp
public sealed class HyperspaceRunner : MonoBehaviour
{
    const float Dt = HyperspaceTuning.Dt;              // 1/60
    HyperspaceRun run; float accumulator; PlayerInput input; readonly HEvent[] events = new HEvent[EventRing.Capacity];

    public void Begin(HyperspaceRun r) { run = r; accumulator = 0f; }

    void Update()
    {
        if (run == null) return;
        input = ReadInput();                           // target in play-area units + bomb button (section 4)
        accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // clamp spikes: the sim never needs catch-up bursts
        int guard = 0;
        while (accumulator >= Dt && guard++ < 6 && run.State == RunState.Running)
        {
            run.Step(input);
            accumulator -= Dt;
            int n = run.Events.Drain(events);          // drain EVERY step so step-accurate FX stay in order
            for (int i = 0; i < n; i++) Dispatch(events[i]);
        }
        if (run.State == RunState.Downed) ShowDownedDialog();      // revive (shards/ad) or finish the trip
        if (run.State == RunState.Ended) { Complete(run.BuildResult()); run = null; return; }
        Present(accumulator / Dt);                     // render with alpha in [0,1)
    }
}
```
Interpolation: the sim keeps no previous snapshot. Extrapolate each view by `alpha * Dt` using the velocities you already have: enemies `x += vx*a, y += vy*a, z -= (Speed + vzOwn)*a`, bolts/bullets `p += v*a`, player `+= (vx, vy)*a`. Do not use `Time.timeScale` for the sim; pause by not calling `Step`.
When the app is backgrounded call nothing (the run is frozen); on resume reset `accumulator = 0`.

## 4. Coordinate system, camera and input (portrait)

* Right-handed sim space: **x right, y up, z forward**. The ship is at `z = 0`, the world scrolls toward `-z`; enemies appear at `z = spawnDistance = 140` and are removed behind the ship (`despawnZ = -12`). Play area: `x in [-2.7, 2.7]`, `y in [-3.4, 3.4]` (`tuning.areaHalfWidth/areaHalfHeight`), ship radius 0.30.
* Map 1 unit = 1 Unity unit (`new Vector3(e.x, e.y, e.z)`), ship at the origin. Suggested camera: position `(0, 1.8, -10)`, looking at `(0, 0.2, 30)`, **vertical FOV 60-65**, portrait 9:16. At the ship plane the visible width is ~6.7 u, so the 5.4 u play width fills ~80 % of the screen and the 6.8 u height ~55 %, leaving room for HUD top/bottom. Add FOV kick: `fov = 60 + 6 * (Speed - 25) / 15`.
* Parallax/streaks: scroll star streaks by `Speed` (u/s). Use `Scroll` (cumulative distance) for tiled backdrops.
* **Input (one thumb, drag anywhere)** - recommended *relative drag*, so the finger never covers the ship:
```csharp
Vector2 anchorScreen; Vector2 shipAtTouch;                    // on touch-begin: remember both
float unitsPerPixel = 5.4f / (Screen.width * 0.8f);           // 80 % of screen width = play width
Vector2 target = shipAtTouch + (screenPos - anchorScreen) * unitsPerPixel * sensitivity;   // sensitivity 1.0-1.6, expose in settings
input = new PlayerInput(target.x, target.y, bombButtonHeld);  // sim clamps and smooths (critically damped, followOmega 15, max speed 9.5 x handling)
```
  Absolute mode (finger = target via `Plane(z=0).Raycast`) is also valid. Keep the last target when the finger lifts.
  Auto-fire remains on, but bullets travel straight along +z without target selection or steering: line up the ship to hit.
  Bomb input launches one projectile from the ship's current x/y at 70 u/s to `bossZ` (62 u), then detonates with a 7 u
  spherical blast. It deals the greater of normal bomb damage or 18% of guardian max hull, respects invulnerability,
  and only clears enemy bolts inside the blast. A charge is spent at launch; only one bomb may be in flight.
* Accessibility: drag sensitivity and colour-blind-safe telegraph shapes (section 5). New player journeys always use
  `RunMode.Normal`; no Assist toggle is exposed, and old stored preferences are ignored. The Core enum retains Assist for analysis tooling.

## 5. Entity types -> suggested visual language (neon, additive glow)

All hazards share one rule: **the telegraph is geometry, not just colour** (rings, wedges, lines) and appears >= `TurretTelegraphFor(tier)` (0.85 -> 0.6 s) before it hurts.

| Type (`EntityType`) | Silhouette / colour | Motion / trail | Telegraph / state visuals |
|---|---|---|---|
| Drone | small cyan arrowhead (r 0.42) | straight, thin cyan trail | blink white on `flash` (hit) |
| Zigzag | green zig-zag blade | sine path, wavy ribbon trail | - |
| Swooper | magenta crescent | curved entry arc, trail follows the curve | - |
| TurretPod (r 0.7) | orange hexagon with barrel | static | **lock ring** at `aimX,aimY` shrinking over the telegraph (`Telegraphing` flag, event `TurretTelegraph.v` seconds), bolt = orange capsule with comet tail |
| Mine (r 0.45; blast 1.25) | red spiked orb | static | inner pulse when `Armed`; draw the blast radius ring (1.25) faint red when the ship is within `mineArmRadius` 2.1 |
| Asteroid (r 1.6/0.8/0.4) | grey faceted rock, rim light | slow tumble | cracks as hp drops; splits into two inside its footprint (`AsteroidSplit`) |
| LaserGate | two yellow-orange beams forming a wall; gap shown by bright bracket lines | slit slides, slot rises, ring drifts, bar rotates | `GateTelegraph` 1.6 s ahead: gap outline pulses + chevrons pointing at `x` gap centre; wall never appears without it |
| Seeker | yellow missile with flame | homes, then locks straight | `TurretTelegraph` on lock: thin line from missile to its aim point + ring; weak (hp ~14), shoot it |
| Elite | purple armoured diamond with shield bubble | slow | shield bubble (`shield/maxShield`) flickers on hit and pops when depleted (`EnemyHit.c = 1`) |
| Guardian | large magenta-pink hull, glowing **weak point** (`aimX,aimY` when `WeakPointExposed`) | holds at z = 62 | boss bar (HUD), phase change flash, volley telegraph from `BossVolleyEta/AimX/AimY/Count` (draw the fan before bolts exist) |
| Pickups | Bomb orange, Shield blue, Overdrive magenta, Fuel green canister, Loot crate gold cube | slow spin, magnet lines when `magnetized` | spawn sparkle (`PickupSpawned`) |
| Player ship | white-cyan delta; engine trail scaled by `Speed` | bank by `vx` | shield ring (charges), invulnerability flicker (`invulnerable > 0`), overdrive aura (`overdrive > 0`) |
| Bullets | bright cyan dashes (collision r 0.64) | fly straight forward; line up the ship | impact spark at `EnemyHit` |

## 6. Events -> FX and audio

Drain after every step. `x,y,z` are in the ship frame (same as the pools).

| Event (`EventType`) | Fields | Visual FX | Audio cue |
|---|---|---|---|
| ShotFired | v=1 (rate-limited, every >= 4 steps) | muzzle flash | soft pew (pitch up with overdrive) |
| EnemyHit | a=type, b=uid, c=1 if the shield broke, v=damage, xyz | spark + 1-frame flash, floating number optional | tick |
| EnemyKilled | a=type, b=uid, c=salvage, v=score | type-coloured burst + shockwave ring; scrap motes fly to HUD | pop (pitch by type), combo tick |
| AsteroidSplit | b=child uid | rock debris | crumble |
| MineArmed / MineBlast | b=uid | red ring / red blast sphere | beep loop / boom |
| TurretTelegraph | a=type (4 turret, 8 seeker), b=uid, xy=aim, v=seconds | lock ring converging on xy | rising lock tone |
| GateTelegraph | a=GateKind, b=uid, v=seconds, x=gap centre | gap outline + chevrons | whoosh warning |
| SeekerLaunched | b=uid | launch flare | missile whoosh |
| PlayerHit | a=source type (0 bolt), v=damage, c=hull left | red vignette, camera shake, hull bar flash | heavy hit + haptic |
| ShieldBreak / ShieldRecharged | c=charges | blue ring burst / ring refill | glass break / chime |
| Revived / PlayerDowned | c=revives used | time-freeze desaturate (downed), blue pulse (revive) | alarm / power-up |
| BombUsed / BombCharged | c=bombs left, v=damage | distant local blast at event x/y/z / ready marker | boom / ready ping |
| PickupSpawned / PickupCollected | a=type, b=uid, c=loot count, v=loot kind | sparkle / fly-to-HUD burst | pick-up jingle per type |
| OverdriveStart / OverdriveEnd | v=seconds | aura, FOV punch | rev up / down |
| ComboBreak | c=combo lost | combo counter shatters | low blip |
| WaveCleared | a=wave index | small banner "CLEAR" | chime |
| BossWarning / BossPhase / BossWeakPoint / BossDefeated | v=seconds / a=phase / - / v=fight seconds | warning bars, phase flash, weak-point glow, mega explosion | siren, stinger, ping, fanfare |
| RunEnded | a=Grade, b=reachedEnd, c=kills | results sequence | grade jingle (S/A/B/C) |

Haptics: PlayerHit (strong), ShieldBreak (medium), BombUsed (medium), EnemyKilled (very light, rate-limited), BossPhase (pattern).

## 7. HUD data

| HUD element | Source |
|---|---|
| Hull bar | `Player.hull / Player.maxHull` (flash on `PlayerHit`) |
| Shield pips | `Player.shield / Player.maxShield`; recharge ring `shieldRegenTimer` |
| Bomb charges + kill progress | `Player.bombs/maxBombs`, `bombKillProgress / tuning.bombKillsPerCharge` |
| Combo | `Player.combo`, timer `Player.comboTimer / tuning.comboWindow`, best = `MaxCombo` |
| Overdrive | `Player.overdrive` seconds left |
| Route progress | `Progress` (0..1) plus encounter ticks from `Plan.encounters[i].anchorStep / EndStep`; the Guardian jumps progress to 1 when destroyed |
| Boss bar | `BossActive`, `BossHpFraction`, `BossPhase`, `BossVolleyEta` |
| Score / kills / loot | `Score`, `Kills`, `LootCount(kind)`, `FuelCanisters` |
| Downed dialog | `State == Downed`, `CanRevive`, `RevivesUsed`: buttons *Revive (shards / rewarded ad)* and *Finish trip (no refund, no loot)* |

## 8. Pooling, instancing and phone performance budgets

* The simulation owns fixed pools (`maxEnemies 96, maxPlayerBullets 96, maxEnemyBolts 160, maxPickups 40`; all in tuning). **Mirror them 1:1 in the renderer**: `GameObject[]/instance slots` with the same length, indexed by pool slot. Each frame, for `i < EnemyHigh`: if `Enemies[i].active` and `view[i].uid != Enemies[i].uid` -> (re)bind/skin it, else if `!active` -> hide. No `Instantiate/Destroy` during a run; no LINQ; reuse the `HEvent[512]` buffer.
* Prefer instanced drawing: one mesh + `MaterialPropertyBlock`/`Graphics.RenderMeshInstanced` per entity type (drones, bullets and bolts are the bulk: bullets/bolts as one instanced quad-trail mesh each; trails via a ring-buffer vertex strip, not `TrailRenderer`). Draw order: opaque rocks/hulls, then additive glow.
* Budgets (target: 60 fps on a 2019 mid-range phone, 30 fps acceptable because the sim is fixed-step and renders 2 steps per frame):
  * Simulation: measured 0.4-2 us per step on desktop (even with every pool full), i.e. well under 0.1 ms on a phone; **0 bytes allocated per step** (`PerformanceTests`). Do not read `StateDigest()` per frame (it hashes all pools).
  * Draw calls <= 60 (instancing + one additive particle pass); triangles <= 120k; live particles <= 300; at most 6 layers of transparent overdraw; bloom at half resolution only on high tier devices; no realtime shadows (fake blob/rim light).
  * Audio: <= 12 concurrent voices; rate-limit per event type (ShotFired already limited; cap EnemyHit sounds to 1 per 40 ms).
  * Memory: pre-warm pools and particle systems during the loading screen; keep `HEvent` buffer, `PlayerInput`, `RunParams` as locals/fields (all are structs).
  * If frame time spikes, drop effects first (bloom, trails), never the step rate; the accumulator clamp (0.1 s) prevents spirals.

## 9. Determinism contract (what must not change)

Same `(RunParams, ShipStats, HyperspaceTuning, EncounterLibrary)` + same `PlayerInput` per step => identical schedule, event stream, state and result. Verified: **all 9 `SelfCheck` digests are identical on the .NET 8 CoreCLR and on Unity's own Mono** (`crosscheck.ps1`), and `DeterminismTests.GoldenDigestsMatchOnThisRuntime` passes on both.

| Scenario (SelfCheck) | plan | events | final state |
|---|---|---|---|
| first hop, average bot | `0xEF8CE0688767BECE` | `0x3BDCF5892839ADB0` | `0xD4FD5ECD0922B0E3` |
| tier 6, 800 ly, Guardian, expert bot | `0x552FE83D957EBFF6` | `0xB0E94E1CACA52569` | `0x97F345A4A76DEC70` |
| tier 12, 3000 ly, revives, novice bot | `0xAF6249FCE21DA302` | `0x455BB62538A230B7` | `0xC2FDE4538AA019F1` |

Rules the code obeys (enforced by `tools\FloatLint`, run with `lint.ps1`; 0 findings):
1. **All simulation arithmetic is `double`; stored state stays `float`, rounded once with an explicit `(float)` cast.** Reason found during this work: Unity's Mono evaluates float expressions and float locals at double precision (CoreCLR and IL2CPP round every operation), so any multi-step float expression differs in the last bit between runtimes. + - * / sqrt in double are identical everywhere.
2. Own `HMath` (sin/cos/atan2/log2/exp2/pow from series, no libm), `Hash.cs` `Rng` only, no `string.GetHashCode`, `DateTime`, `System.Random`, LINQ, static mutable state, `Time.deltaTime`; stable insertion sorts instead of `Array.Sort`.
3. When you edit sim code: build -> `lint.ps1` -> run `crosscheck.ps1` (Unity Mono vs CoreCLR) -> if the change is intended, update `Golden.cs`.
4. **IL2CPP/ARM64 is not verified here** (no device available): add a debug-menu item that calls `SelfCheck.Run(tuning, lib)` on device and compares with the table above; a mismatch means the player build reorders or fuses double arithmetic (e.g. FMA contraction) and the run is not replayable across devices (it stays deterministic on a given build). Replays/ghost sharing should therefore be gated by that check.

## 10. How the host maps real game state

| Host data | Hyperspace input |
|---|---|
| route length (ly) | `RunParams.distanceLy` (same-system hop: 1) |
| `PlanetData.danger` / destination tier | `RunParams.danger` (~0.6 + 1.1 x tier) / `RunParams.tier` 0..12 |
| ship upgrades | `ShipStats`: hull = armour/hull stat, shield = shield charges, shieldRecharge = shield regen seconds per charge, damage/fireRate/pierce = weapon stats, magnet = pickup radius, handling = thruster/agility multiplier (1 = starter), bombCapacity = bay size. `RunParams.shipPower = tuning.ShipPower(ship)` |
| seed | `Hash.Combine(saveSeed, tripIndex)` so a trip is reproducible (daily/weekly boards: fixed seed + `RunMode.Challenge`) |
| accessibility | Drag sensitivity; no player-facing difficulty override. Core Assist is analysis-only. |

After the run (`RunResult r`):
* **Fuel refund**: `refund = floor(fuelCost * travelRefundMax * r.fuelRefundFraction)` (`travelRefundMax` = the existing 0.4; the fraction is `quality^refundQualityPower + fuelCanisterBonus`, 0 after an abandoned/defeated run).
* **Loot**: `r.lootList` holds raw Salvage/Component/Core counts; multiply by `r.yieldMultiplier = 0.4 + 1.1 x quality` (same curve as the harvest games) before crediting. Empty when abandoned after defeat. Guardian drops `bossCores` Cores.
* **XP**: add `r.xp` (0 when abandoned). **Grade** S/A/B/C = quality >= 0.90 / 0.78 / 0.55; show with `r.cleanRun`, `r.maxCombo`, `r.killCount`, `r.hitsTaken`.
* **Revive flow**: when `State == Downed` freeze gameplay input and show the dialog. *Revive* -> host charges shards or plays a rewarded ad, then calls `run.Revive()` (max 2 per run, each costs 0.10 quality). *Finish trip* -> `run.Abandon()` then `BuildResult()` (no refund, no loot, the trip itself still completes: never a hard fail or softlock). If the player quits the app mid-run, treat it as `Abandon()`.
* Duration shown to the player: `Route.duration` (20-35 s short hops growing to ~60-92 s: `18 + 4.4 x ly^0.35` s clamped to [20, 92], Guardian routes append the boss slot). Rationale: a micro-session on a phone must stay under about 90 s; the 0.35 exponent makes a 600x longer route only ~3x longer in time so long routes feel big without becoming a chore.

## 11. Tuning keys (Remote Config)

All numbers live in `HyperspaceTuning` (270 keys; the complete default list is `reports\tuning_keys.txt`). Keys are the field names; per-enemy arrays use `name.EnemyType` (e.g. `enemyHp.Drone`). Apply:
```csharp
foreach (var k in HyperspaceTuning.AllKeys())
    if (rc.TryGet("hs_" + k, out float v)) tuning.TrySet(k, v);   // clamp/validate server-side; NaN/Inf are rejected
```
Groups: **world** (`areaHalfWidth/Height, spawnDistance, despawnZ, nominalSpeed, speedBase/PerTier/PerDistanceTerm, speedRampSeconds, speedBrake*`), **player** (`followOmega, playerMaxAccel, baseMaxSpeed, playerRadius, invulnSeconds, shieldDelay, bomb*, overdrive*, magnetPullSpeed, bulletSpeed/Radius/Range`), **enemies** (`enemyHp/Damage/Radius/HalfDepth/Score/Scrap/Cost.<Type>`, `turret*, seeker*, mine*, gate*, asteroid*, bolt*`), **boss** (`boss*`), **route/difficulty** (`duration*, budget*, expectedPower*, hpPerTier, damagePerTier, challenge*`), **pacing** (`introSeconds, outroSeconds, waveSeconds, calm/build/peak/relax Fraction and Rate, gapTier0/12`), **tier scaling** (`gapScale*, timeScale*, zig*, gateSlide/Spin, swooperCurve, linePathSpeed, dartSpeed`), **fairness contract** (`reactionBudget 0.30, fairnessSpeedFraction 0.80, fairnessMargin(+Deep), entryLead 1.35, gapSlack(+Deep)`), **results** (`grade*, quality*, comboForFullBonus, comboWindow, refundQualityPower, yieldBase 0.4, yieldPerQuality 1.1, xp*, fuelRefundPerCanister`), **loot** (`dropChance*, dropPityKills, crate*`), **revive** (`maxRevives, reviveHullFraction, reviveInvulnSeconds, reviveClearZ`), **caps** (`maxEnemies, maxPlayerBullets, maxEnemyBolts, maxPickups`).
**Re-run `run.ps1 solve` and `run.ps1 test` after changing any fairness-relevant key** (everything that changes geometry or timing: gap/time scaling, speeds, radii, telegraphs, margins); tuning knobs that only change rewards/loot/XP/grades are safe. Changing the tuning changes the schedule digest, so replays recorded with other values do not match.

## 12. Encounter template authoring guide (designers)

The live Unity host loads `Assets/Frontier/Resources/Frontier/EncounterCatalog.asset`, which references 64 editable `EncounterSO` assets under `Encounters` (63 playable formations plus the Guardian). Catalog order determines stable runtime indices; preserve that order when changing existing content. A run copies the data before use, so simulation changes never mutate ScriptableObjects.

Use **Create > Frontier > Hyperspace Encounter** for a new formation and add it to the catalog. Each entry exposes its entity, path, parameters, offsets and group; offsets must be sorted, begin at zero, and end at `durationZ`. IDs must be unique, tiers must be within 0-12, costs/weights positive, and all numerical fields finite. Invalid data fails explicitly rather than silently using defaults. Select a non-boss asset and use **Assets > Frontier > Validate Encounter Fairness** to prove its declared speed at every allowed tier, both route speeds and both permitted mirrors. Guardian behavior still requires its dedicated boss-phase tests.

**Frontier > Hyperspace > Create Encounter Assets If Missing** bootstraps missing assets from the engine-free defaults without overwriting an existing catalog or encounter. The existing `EncounterLibrary*.cs` builder library remains the engine-free lab/bootstrap source, not the Unity runtime content source. The initial exported catalog preserves every field and all nine canonical plan/event/state digests. Editing the shipped catalog intentionally invalidates that parity baseline; update the content expectations and run the fairness/pacing checks before shipping the change.

The engine-free lab workflow below edits its builder source; its CLI does not automatically load Unity assets.

1. **Write it** with the fluent `TemplateBuilder` inside a `BuildXxx()` method and `Register(builder)` it:
```csharp
var t = new TemplateBuilder("gate_zig", minTier: 4, maxTier: 12, cost: 14f, EncounterRole.Peak, EncounterFamily.Gates, declaredSpeed: 0.7f, weight: 1f, mirrorable: true)
    .Notes("Three slits whose gaps alternate sides.");
t.Gate(GateKind.Slit, -1.5f, 0f,  0f, 1.3f).Gate(GateKind.Slit, 1.5f, 0f, 30f, 1.3f).Gate(GateKind.Slit, -1.5f, 0f, 60f, 1.3f);
Register(t);
```
   Builder calls: `Drone, DroneDrift, Zig, Swoop, Asteroid, Mine, Gate(Slit|Slot|Ring|Bar, ...), Turret(Aimed|Fan|Lane, ..., offsetSeconds), Seeker, Elite, Reward(pickup), Guardian`.
   * `x,y` are the **position at arrival** (when the entity crosses the ship plane) in play-area units; `zOffset` is distance at `nominalSpeed` (30 u/s) from the first arrival and is converted to seconds, so the formation looks the same at any route speed. Offsets shrink by the tier's time scale (formations get tighter with tier).
   * `cost` = director budget points (roughly threat x duration; compare with similar templates: a drone line 5, a gate sequence 14, an elite escort 18). `role` picks the pacing segment (Calm/Build/Peak), `family` is the variety bucket (never 3 of a family in a row), `weight` biases selection, `mirrorable` lets the director flip it left/right.
   * Gate parameters: `half` = half opening (slit width / slot height / hole radius / bar length), `amp,omega,phase` slide the opening (sine), `scaleGap` lets the tier narrow the opening (never below `MinGapHalf`). Turrets: `offsetSeconds` is when the lock cycle starts before arrival (keep >= telegraph + 0.45 s); entries with the same `group` share one lock window.
   * Tier window: `minTier`/`maxTier` in the constructor; easy fillers are retired in `EncounterLibrary.Tiers.cs` (`TierCap`).
2. **Prove it**: `powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1 solve --template gate_zig` prints one row per template: `declared` (your promised fraction of the fair ship speed, 0.30-1.00) vs `measured` (what the perfect solver needs, searched in 0.1 steps from 0.30), failures per tier/speed/mirror instance. **Fix every failure; set `declaredSpeed >= measured`** (tests enforce both). `solve --template gate_zig --describe 8` shows the most restrictive step and lock windows of one instance. `solve` without arguments proves the whole library in ~3 s.
   The solver replays the template through the real simulation with a ghost ship, records every hazard shape and aimed threat, then runs a bit-parallel BFS over a lattice of ship positions (one layer per step, kernel = one step of fair speed), keeping ship radius + `fairnessMargin(tier)` + half a cell from every hazard, freezing the ship for `reactionBudget` (0.30 s) after each aimed lock. A template passes only if a collision-free path exists from **every** starting position `entryLead` (1.35 s) before the first hazard, at every tier it allows, at both route speeds, mirrored and not.
   Typical failures: gap narrower than ship + margin, two gaps too far apart for the time between them, overlapping lock windows from different turrets (give them one `group` or separate them), a gate hidden behind another within the reaction time.
3. **Look at it**: `run.ps1 render templates` -> `png\templates_at_min_tier.png` and `png\templates_at_tier12.png` (contact sheets, ship at the bottom, x panel and y panel), `run.ps1 render timeline` for whole routes.
4. **Check the pacing/budget effect**: `run.ps1 probe --tier 6 --dist 200` lists the plan; `run.ps1 balance --runs 48 --quick` and `run.ps1 hits <tier> <ly> <skill>` show which hazard types cost hits.
5. **Composites** (two proven templates welded together, spacing searched automatically): add a recipe line in `EncounterLibrary.Recipes.cs` and run `run.ps1 compose --contains <id>`; a recipe is accepted only if it is solvable at **all** tiers, both speeds, both mirrors.
6. Run `run.ps1 test` (and `monotests.ps1`); if the library or tuning changed, update `Golden.Digests` with `crosscheck.ps1` output and regenerate `reports\balance_report.txt` (`run.ps1 balance --runs 96 --out reports\balance_report.txt`).

Rules of thumb: telegraph everything >= 0.55 s; never ask for more than the declared speed; keep openings >= ship diameter (0.6) + 0.35; a template must stay readable in a still frame (formation shape first, then timing); give every Peak template a way to be shot as well as dodged (shooting only ever makes it easier).

## 13. Fairness contract and balance bands

Fairness (the player never meets unavoidable damage): proven for all 63 non-boss templates in every allowed tier (1768 tier x speed x mirror instances), both route speeds (start and full), and the 3 Guardian phases (`FairnessTests`). Telegraph >= `reactionBudget + 0.25 s` for turrets (0.85 -> 0.6 s), seekers commit 0.80 -> 0.62 s before impact, gates announce 1.6 s ahead.

Target bands (enforced by `BalanceBandTests`, Category Heavy, seeded and reproducible) - bots: novice (0.40 s delay, 55 % speed, noisy), average (0.30 s, 80 %), expert (0.20 s, 100 %, exact prediction); ship = the expected one for the tier (`power = 1 + 0.75 x tier`); averaged over 5/50/200/800/3000 ly; no revives used. Measured values (32 runs per cell; `reports\balance_report.txt` has 96-run tables):

| Tiers | Skill | Survival (band / measured) | S+A share (band / measured) | Hits per run (measured) |
|---|---|---|---|---|
| 0-2 | novice | >= 97 % / 100 % | 55-95 % / 72 % (B+ 100 %) | 0.9 |
| 0-2 | average / expert | >= 99 % / 100 % | >= 92 % / 100 % | 0.0 |
| 3-5 | novice | >= 95 % / 100 % | 25-65 % / 42 % | 2.0 |
| 3-5 | average | >= 98 % / 100 % | 60-95 % / 79 % | 0.6 |
| 3-5 | expert | >= 99 % / 100 % | 80-100 % / 95 % | 0.2 |
| 6-8 | novice | >= 93 % / 100 % | <= 35 % / 19 % | 3.4 |
| 6-8 | average | >= 97 % / 100 % | 35-80 % / 54 % | 1.2 |
| 6-8 | expert | >= 99 % / 100 % | 65-98 % / 88 % | 0.2 |
| 9-12 | novice | >= 75 % / 95 % | <= 15 % / 10 % | 5.5 |
| 9-12 | average | >= 90 % / 100 % | <= 40 % / 23 % | 2.6 |
| 9-12 | expert | >= 97 % / 100 % | 25-75 % / 55 % | 0.9 |

Other enforced relations: first hops (tier 0-2, 5/50 ly): novice survives 100 % with B+ in 100 % of runs; skill gap (expert - novice S/A) >= 30 points from tier 3; hits non-decreasing in tier and route length; **a starter ship at tier 6 is underpowered for a novice (survival 67 %, kills 5 %) while the expected ship survives 100 %**; **endgame ship on tier-0 routes is a power trip (novice S/A 97 %, kills 100 %) but not a free pass (3000 ly: novice still takes 0.8 hits per run, 79 % S/A)**. Grades: S >= 0.90, A >= 0.78 (one shield-absorbed hit still makes A, a hull hit does not), B >= 0.55, C below; quality = 0.42 + 0.20 kills + 0.10 combo + 0.10 pickups + 0.18 clean-run bonus - 0.07 per hull hit (0.4x for shield hits), -0.10 per revive; `yieldMultiplier = 0.4 + 1.1 x quality`.
Honest deviation from the first target statement ("tier 3+ expert 70-90 % S/A"): experts sit at 80-100 % on tiers 3-8 and 25-75 % on 9-12; short routes at tier 3-6 are above 90 % because every template is provably flyable at <= 80 % of the stock ship's speed, so a precise pilot rarely gets hit; length (more encounters), tier and ship power are what push experts down (800-3000 ly at tier 3-6: 55-85 %).

## 14. Known limitations and renderer recommendations

* The bots are heuristics (lattice dodger with reaction delay, noise, attention lapses), not human data: re-tune bands against real telemetry (`hits` per hazard type, grade histogram, revive rate) via Remote Config.
* The fairness proof assumes the ship can use `fairnessSpeedFraction x baseMaxSpeed` (0.8 x 9.5 u/s) with a 0.30 s reaction, anywhere in the area; shooting is never assumed. Touch precision (finger smoothing) is not modelled: keep sensitivity adjustable and the drag smoothing described in section 4.
* Large asteroids (radius 1.6) are 60 % of the play width: they are meant to be shot (70 hp x tier scale) or sidestepped early; make their silhouette and hp obvious.
* Determinism is verified on x64 CoreCLR and Unity's x64 Mono only (see section 9 for IL2CPP/ARM64). Floats are stored as `float`; a replay must also reuse the same tuning.
* No physics engine: collisions are circle/slab tests in the x-y plane with a swept z test and z-bucket broad phase (O(entities) per step); entity caps drop spawns (`SpawnFailures`) instead of growing.
* Renderer recommendations: neon silhouettes with 2-colour rim light, additive glow + half-res bloom, speed-line streaks tied to `Speed`, chromatic kick on `PlayerHit`, always draw telegraph geometry on a separate high-contrast layer, keep the play-area bounds faintly visible (a frame that brightens near edges), and give the Guardian weak point a distinct glow so the player understands where auto-fire is being aimed.