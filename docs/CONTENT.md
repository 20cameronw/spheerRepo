# Economy content pipeline

## Player-flow reward authoring

`Resources/Frontier/GameplayRewardRules.asset` is the explicit gameplay placement gate. Harvest and
construction ship disabled; construction seconds remains zero until tuning is approved. Do not copy
historical `RewardedAdsPolicy` caps into approved gameplay tuning. Construction uses existing service
placement ID `timer-skip`; harvest uses `double-yield`. The proposed values and child-inclusive activation
gates are documented in [SYSTEMS.md](SYSTEMS.md), not activated by this content.
The user's subsequent frequency decision removes daily caps for these two placements via explicit
`unlimitedDaily` flags. A zero numeric cap without that flag still means disabled. Cooldowns remain in
force; flags do not enable gameplay or native providers. Other staged placements are unchanged.

Player-flow copy lives in `UiStrings.asset` (`project.*`, `reward.*` and first-departure guidance).
Use `FrontierLocalization.SyncEnglishCatalog` after canonical edits; generated locale tables must not
be edited manually. Stable project content/world/slot IDs live in existing save settings; do not rename
content to change navigation. Tracked quantities and availability use the originating action's pool.

## XP-level progression requirements

Building, structure/ship/defense upgrades, research, tool tiers and recipes now use player XP
level for their numeric unlock requirements. Existing catalog numbers are reused one-for-one;
`requiresReachTier`, structure `unlockTier`, research `tier` and recipe `tier` retain their
serialized names for content/save compatibility, but these **gate fields mean required XP level**.
Research prerequisites, current-world lab/station levels and material costs still apply.
Already-purchased progression is retained. The existing XP curve and victory XP awards are unchanged;
other existing XP sources still count. Planet tier, resource distribution, hazards, exploration
objectives and Frontier Score reach remain geographic. Reachability models repeatable XP once
an Alarm and fuel are obtainable. The historical offline balance simulator still models the
original exploration-gated progression; its long-term era forecasts are not XP-loop forecasts
and it never authorizes live purchases.

## Planet network and the Grand Archive

`Resources/Frontier/NetworkRules.asset` is the editable, validated ScriptableObject for the new
network progression; it sits alongside, and does not rewrite, the imported economy catalog.
After Warp Gate Engineering, visit and build an operational gate on each endpoint, then pay
the local link cost through a gate or Map > Planet Network.
Links are permanent, undirected, transitive and saved as canonical planet ids. They remain
after dismantling a gate. Solid green lines connect worlds on the local orbital chart and
systems on the universe chart. Connected worlds use zero fuel and have no flight legs,
mini-game or autopilot surcharge; a short warp/arrival effect presents the already-durable
arrival. Landing protection still applies. Unlinked travel is unchanged.

The Grand Archive requires Stellar Engineering and five *different* connected supply worlds
with Wood, Ice, Titanium Ore, Radium and Dark Matter deposits. A maximum matching assigns
one world to each deposit requirement; the archive hub cannot also supply a role. The hub
needs an operational gate and Cargo Depot, at least twelve warehouses, enough warehouses
to use every remaining pad of capacity after the gate/depot footprints, and 4,600 warehouse
slots. A 48-pad Giant world with 43 fully upgraded warehouses can hold the authored final
shipment; even the maximum backpack cannot hold it. The shipment includes each raw supply
resource plus titanium plates, quantum chips, warp coils and void alloy. Costs come only
from carried materials and that world's operational pooled storage, never other planets.
Gate-link purchases and archive commissioning persist detached paid snapshots before
changing live resources; failed writes spend nothing, and repeating commissioning cannot
consume another shipment or grant another reward.

Commissioning saves completion before playing a skippable, replayable neon network finale.
It unlocks free lightspeed to any **visited** world, including worlds outside the gate network;
new destinations still require ordinary exploration and hull safety is never bypassed.
Players may keep all their progress. Optional Ascension is now available after this ending,
using the existing explicit review/confirmation and pre-reset recovery copy. It retains
knowledge/currency/claims and the existing capped legacy harvest bonus, but starts a new
seed, clears the network/archive/lightspeed and resets ship fuel to starting reserves.
Watching, replaying or skipping the finale never triggers a reset.

## Dedicated fuel tanks and gallons

Fuel, Plasma and Antimatter use ship tank capacity instead of backpack slots. Four base tank
racks are extended by the existing Fuel Tank upgrade's authored cumulative effect; that upgrade
is now purchasable through the normal research/reach/cost checks. Tank racks use each fuel's
existing stack size, keeping density meaningful. Fuel Depots remain local reserves: quick
store/withdraw transfers between them and ship tanks, not the backpack. Ordinary quick-store
leaves ship fuel aboard; old fuel in regular warehouses can still be withdrawn. Crafting, rewards,
wave calls, relay payments, travel, refunds and offline fuel collection use the same tank-aware
inventory facade, so none can refill past capacity or consume cargo space.

The HUD, route prices, crafting, wave controls, depot stacks and travel rewards display
**1,000 equivalent gallons per existing energy unit**. Plasma and Antimatter still carry 8
and 64 energy per unit. No reserves or prices are multiplied internally: this avoids rounding,
overflow and accidental economy inflation. The tank panel shows an exact gallon total and
the breakdown; the HUD abbreviates millions/billions and depot grid numbers use `k`.
Resource types and counts are unchanged.

V2 saves add `player.fuelTank`, `player.fuelStorageVersion` and `network`. Loading an older
save moves carried fuel into tanks without truncating over-capacity reserves; new fuel is
blocked until space is available, while existing reserves can still be spent or deposited.
Unknown/newer tank data loads read-only rather than being overwritten. Existing interrupted
travel retains its original departure cargo surcharge across the fuel migration so its saved
route and replay remain valid. Fresh travel checkpoints also freeze that departure value.

## Schematic orbital map

The map opens on the current system's orbital chart rather than the wide star field. Planets use
flat illustrated colours, highlights and ring silhouettes, with evenly spaced visible orbit lanes;
this spacing is schematic and never changes saved orbits, travel distances or fuel costs.
Planet targets support touch and keyboard selection, with names, a current-world marker and a
distinct selected orbit. Universe Map / Planet Orbits switches between the local chart and the
existing unbounded universe. Selecting another system or finding a named world opens its chart;
coordinate searches stay in universe view. Search, resource filters, pins and route previews remain.

## World interaction and shared mining

Orbit/Surface framing uses an interruptible 0.6-second eased camera move and an icon-only
world-view button with a localized action tooltip. Carried cargo usage lives on the Ship dock action
as eight segmented green-to-amber-to-red bars and an exact used/total count, not a second top-bar
chip. The dock is Research / Map / Ship / Build. Ship opens Cargo and also owns Tools and Systems;
Research owns Tech and global Improvements. Buildings are managed by tapping them. Existing construction timers, finish
confirmations and recruitment remain accessible through Build > Construction Jobs, without
changing saved crews or progression. Map pinning is a compact native checkbox.

Ship > Tools uses one catalog upgrade list rather than the old axe/pick/drill tabbed
legacy shop. The Mining entry uses the existing Pick ladder's costs, research and reach gates;
both Mine and Drill receive its tier. The highest previously owned Pick or Drill tier is retained
without deleting either saved entry. Old legacy pick/drill power bonuses remain effective for both
games; new purchases use the catalog rather than requiring two parallel upgrade tracks.

Mine now runs a five-lane descending vein cutter: read six upcoming rows, steer into bright elongated
rich seams, and avoid dark flattened unstable rock. Four hull points and eighteen rows bound a run.
Rich seams yield three ore versus one from ordinary rock; accumulated ore determines quality.
Stronger tools preserve existing yield benefits and add reaction time. Hold BRAKE (Space) halves the real row speed;
each three consecutive rich seams restores one lost integrity point, never multiplying ore or exceeding four integrity.

Chopping uses a moving timing marker and a central good zone with a narrower perfect center. Tap SWING or press Space;
the existing axe cooldown, four stages and perfect/good/miss damage remain. Difficulty changes marker speed, and tool power
and tolerance widen the forgiving zone. Axe/tree impacts still reflect the actual swing result.

Drilling retains independent pressure control (Space) and adds hold BOOST (left Shift): productive progress is 2.2x,
with slightly stronger pressure rise. Heat gains 28 percentage points/second during boost and cools at 35/second otherwise.
At 100% it pauses cutting until 35%, preserving all earned depth; releasing boost early avoids the lockout.
The bit and ore surface follow real progress, not an accelerated cosmetic timer. A 90-second safety finish keeps earned
partial quality proportional to depth (idle timeout yields zero).

Siphoning adds pressure management: pumping adds 28% pressure, narrowing the capture window by up to 35%.
Hold VENT (left Shift) to cool pressure at 50%/second rather than passive 5.5%; pumping is blocked while venting.
Consecutive clean pumps stabilize the next window, capped at 16% extra width; seven pulses and averaged quality remain.
Excavation now gives grid direction plus Manhattan steps from the last ping, rather than only vague echo strength;
three seams and eighteen pings remain. Salvage selects a moving target, then holds TETHER (Space) and releases between
0.55 and 1.4 seconds. Early/late releases spend a cast but never remove previous captures; cancellation or pause spends
nothing. Six fragments/ten casts and all quality ceilings and harvest/reward settlement remain unchanged.

Natural trees and mineral seams are tappable on the planet. Their resource panels launch the existing
gather/reward flow through an operational matching structure, preserving method restrictions, hazard
gates and structure bonuses. Sites do not consume or move persisted building pads. Surface View crops
into the upper hemisphere for rotation and selection; Orbit View restores the overview. Called waves
return to the overview so enemies remain visible. Missing-resource
cost chips are native buttons with localized find tooltips, not separate FIND actions. Debris has no
decorative circle/glow; fallback structures use recognizable storage, dome, gantry and machinery silhouettes.

## Licensed audio cues

The editable `Resources/Frontier/AudioCueLibrary.asset` maps click/confirm/error, weapon shot, explosion,
warp, wood/stone/metal impacts and ship ignition/touchdown to selected CC0 clips. Eleven cue mappings share
ten short recordings/derivatives within a 1 MiB decoded sample ceiling. UI cues route through the UI mixer group; combat
and warp cues use SFX. The existing bounded voice pool, independent volume preferences and mute behavior
remain in force. Hyperspace borrows shot/impact/explosion assets without destroying them when a run closes;
its generated pickup/warning/chime cues remain separate. Chopping, mining and drill-contact sounds
use distinct wood, mining and metal assets with authored gain; grading/haptic timing remains unchanged.

Sources: Kenney [Interface Sounds 1.0](https://kenney.nl/assets/interface-sounds),
[Sci-Fi Sounds 1.0](https://kenney.nl/assets/sci-fi-sounds) and
[Impact Sounds 1.0](https://kenney.nl/assets/impact-sounds). All three archives explicitly permit commercial use
under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/); their original `License.txt` files are
retained beside each pack in `Resources/Frontier/Audio`. Only selected clips are included,
not whole archives. They import as preloaded mono PCM/decompress-on-load for predictable short-cue playback.
**Frontier > Audio > Import Licensed Cue Library** creates a missing library or appends newly supported
material/ship cues, preserving existing designer mappings/import settings. Ship ignition is an editor-baked,
normalized 550ms envelope of the licensed engine recording; touchdown reuses the metal contact asset at its
own authored gain. Neither ship cue uses runtime-generated tones.

## Adaptive soundscape

`Resources/Frontier/SoundscapeLibrary.asset` maps exploration/combat/travel arrangements and four weather
textures (calm wind, rain/storm, snow and embers). `FrontierSoundscape` follows actual defense and complete
manual/autopilot travel state; it does not change progression. Arrangement changes fade through silence on the
existing Music source, while weather uses the existing SFX bed. Hyperspace exclusively owns that bed during
travel. Ownership-aware cleanup prevents late engine disposal from stopping weather. Music resumes after
unmute; application pause stops the director's loops and resume fades music back in. Effects and Music sliders
remain independent, with twelve total sources and no runtime music/ambience synthesis.

The seven original loops are deterministic editor-baked PCM files under `Audio/Original`; their provenance is
recorded there. **Frontier > Audio > Create Original Soundscapes If Missing** bootstraps missing content but
never overwrites an existing library. Three aligned 16-second arrangements provide an initial soundtrack,
not an approved replacement for the master plan's commercially licensed music-pack/stem selection.
The travel engine borrows a shared, preloaded loop derived from Kenney Sci-Fi `spaceEngineSmall_000.ogg`;
its original source, 100ms seam-crossfaded derivative and CC0 license stay together in `Audio/KenneySciFi`.
The original source is retained for reproducibility. All eight loop assets together have an 8 MiB decoded
sample-data ceiling. Physical-device listening/mixing, full biome-specific art direction, menu/stem music-pack
selection and final audio sign-off remain open.

## This milestone

### Offline clock history

V2 saves now additionally retain `clock.highWaterUtc`, the greatest observed gameplay UTC. Older saves
derive it from `lastSavedUtc`. Auto-Harvester and Dyson offline production measure only intervals after
that watermark; a backward device-clock edit, saving and restarting cannot make the earlier interval
eligible again. Load reports warn while the clock is behind; new production resumes only past the recorded
time. Explicit restore/undo retain the greater local/incoming watermark without mutating the supplied
snapshot. An unreadable watermark loads read-only and rejects explicit restore rather than discarding history.
This does not establish trusted time: forward date changes, edited/deleted files, older automatic recovery
copies and other devices still require server authority before paid/live-service activation.

The reviewed economy is now importable as editable Unity ScriptableObjects. No economy values were redesigned.
`docs/economy/data` remains the single committed JSON source (regenerate it with the economy lab, never hand-edit it).
The engine-free JSON reader is also compiled and tested by the lab and the plain .NET CI project.

**The imported database is now the compatibility-guarded runtime default.** `Resources/Frontier/EconomyDatabase.asset` references
34 resources, 24 recipes, 31 structures, 6 tool ladders, 16 upgrades, 60 research nodes, 8 obstacle kinds, and the
habitat, obstacle, shard, score, legacy-id and tuning assets. It includes the exact level tables, costs, timers,
requirements, 606 tuning values, category colours and starting inventory from the export.

Every catalog resource has a distinct exact-id SVG, and every structure kind has its own SVG. The 47 new glyphs
extend the existing 48-unit white line-art family and inherit the resource/UI tint at use sites. Authored resource
icon overrides take precedence over canonical ids, which take precedence over keyword fallbacks. The icon check
verifies VectorImage import, 48×48 bounds and full distinct coverage; four paginated economy icon harness shots
show every glyph at 24/32/48/64 physical pixels.

`GameDatabase.Load()` validates the supported v1 catalog, tuning, required families, starting capacity, typed adapters,
legacy aliases, and the complete authored-world/combat links before selecting it. A missing, old, incomplete or
incompatible import falls back to `Frontier/GameDatabase`, then `DefaultContent` when that asset is absent.
The shipped scene leaves its database override empty; explicit legacy/custom overrides still work. An explicit
imported override goes through the same guard.

Selection creates a cached, transient runtime view (`ContentSource` points to the imported asset). It keeps the imported
resources, recipes, structures, upgrade rows and rules, but copies authored planets/enemies and the three legacy tool
upgrades to rebind their item references. No authored asset, GUID, legacy database or imported catalog is rewritten.
Legacy fuel deposits are omitted from the runtime view because fuel is now crafted-only; source deposits remain intact.
Procedural deposits use the imported raw-resource/habitat tables. Planet ids, authored placement, models and visual seeds
stay stable; generated deposit contents now follow economy content v1 rather than the former seven-resource table.

Both v1 migration and existing v2 saves resolve missing resource/structure/upgrade ids through `LegacyIdMapSO`.
Existing stable ids take precedence, so reloads never migrate them twice. `alloy` becomes `steel`, `satellite` becomes
`orbital_relay`; warehouse slots stay in place and colliding inventory aliases sum, while upgrade aliases take the maximum
owned level. Legacy `axe`/`pick`/`drill` purchases and levels remain legacy upgrades; catalog tiers for supported Axe/Pick/Drill ladders
are persisted alongside them in v2. The save schema stays v2, v1 originals are backed up, and newer read-only saves are never overwritten.

Imported recipes are filtered by exact station id, station level, XP level and research, including the authoritative craft
action. Purchased research now passes authored research gates; node prerequisites, current-world lab level, XP level,
catalog identity and resource costs are checked by the runtime. Research exposes technology; Ship exposes tool ladders.
Research, tools and upgrades spend carried cargo plus operational current-world storage connected by a Cargo Depot,
using the same atomic spending helper as construction. Cost chips show that same pool; remote stock is never included.
Only Axe/Pick/Drill tiers can be purchased; their yield and active tool level are reflected in harvest rewards and tool feedback.
Other ladder game types remain visibly unavailable until their minigames exist.
Unsupported structures/effects cannot be purchased. Supported legacy actions remain synchronous: batch size is not treated
as free output, and upgrade rows use their cumulative values and correct units. This is a bounded progression slice,
**not** the complete economy progression loop.

## Import and validation

Editor menus:

* **Frontier > Generate Editable Data Assets** is a create-only legacy bootstrap: it stops when a database or data asset already exists.
  Use the reviewed economy importer for edits to the current economy assets.
* **Frontier > Economy > Import JSON** — reads the exports, validates their schemas, family/version envelopes,
  tuning digest and catalog references, then creates or updates assets by canonical id.
* **Frontier > Economy > Validate Imported Content** — reconstructs the catalog from the saved assets, runs
  `CatalogValidator`, checks drift against the export and validates tuning completeness/digest. Catalog validation rejects
  recipe dependency cycles, unreachable resources and non-bonus recipes outside the tuned value-conservation band.
  The editor icon check verifies unique resource glyph coverage; structures without a model key use the procedural kit fallback.

Batch import:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-unity.ps1 -Method Frontier.EditorTools.EconomyImporter.Import
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Platform EditMode -TestFilter Frontier.Tests.ContentImportTests
```

Assets live at `Assets/Frontier/Data/Economy/<Family>/<id>.asset`. Reimport updates existing objects in place,
preserving GUIDs and references. Removed ids are reported and retained, never deleted. Unchanged imports do not dirty
assets; the tests import twice and compare every generated asset and meta file byte-for-byte, including the legacy
database's bytes. Paths are identity: don't rename generated asset files.

### Ownership and overrides

* Imported: ids, costs, level rows, requirements, categories, catalog Atlas text, icon concepts, tuning and rule data.
* Authored: resource `color`, `description`, `icon`; structure `color`, `modelKey`; upgrade `color`, `icon`.
  New resources start with the exported colour. Non-empty top-level `displayName` and structure/upgrade `description`
  are also preserved for authoring/localisation and excluded from numeric drift checks.
* `ContentSO.lockedFields` pins exact **top-level** field names (e.g. `stackSize`, `levels`).
  Rules and new tool/research/obstacle assets wrap serializable economy data: pin `data` or `rows` as appropriate.
  Locks are skipped by drift checks, **not** by structural/catalog validity checks. Never lock `id` or rename ids.
  Nested-field locks are not supported. Tuning values must remain complete, in range, and match their recorded digest.
* Import logs each created/updated path, retired paths, content version, tuning digest and total changed count.
  A clean reimport reports `changed=0`.

Authored presentation uses `ContentText` and the SO `LocalizedName`/`LocalizedDescription` accessors; serialized
English fields and `ToDef`/`ToCatalog` remain untranslated. Run **Frontier > Localization > Sync English Catalog**
after adding content to append its `content.<category>.<id>.<field>` keys and rebuild the preloaded English table.
Existing source wording is retained, so edit existing translations in the string catalog as needed. English reads
the authored fields live; other installed locales must provide every nonempty source entry before selection/build.
Wave modifiers and authored gather-spot keys use their existing list index: preserve ordering when maintaining
translations. Planet and player proper names are not translated.

Service copy lives in the engine-free `ServiceMessage.EnglishEntries` catalog. Use `ServiceMessage.Get` for authored
service messages and retain `ServiceResult.text` when wrapping a failure; `message` remains English for diagnostics.
Nested message arguments resolve in the current UI locale, while literal SDK diagnostics are never interpreted as
format strings. Catalog sync updates these authoritative English templates, and the build guard rejects missing or
stale service source entries. Keep `EraRequirement.Id` stable when editing requirement labels; era names/requirements
are exported through `ContentText.EnglishEntries` without changing simulator wording or progression rules.

Each original SO keeps its legacy fields and fallback methods. New `ToDef`/`ToCatalog` adapters normalize Unity's
empty-string representation of optional ids back to null, without mutating saved assets. Structure kinds and
upgrade effects are appended, never reordered, preserving old serialized enum integers. `StructureKindMap` is
the explicit canonical-id-to-behaviour map; imported kinds without gameplay are **not** claimed implemented.

## Remaining integration

1. Add builder jobs and timer execution; research completion, supported tool tiers and research gates are now persistent.
2. Wire timers/batch jobs and the remaining structure behaviours, tank/compression effects, full travel/hull progression,
   obstacles, megaprojects, shards and score. The current interim travel settings are unchanged.
3. Add full presentation coverage for Atlas/research/tools. Merely importing a structure is not gameplay.
4. Add safe live tuning/Remote Config overrides later; current travel reads the validated imported tuning and catalog.

The ordered design and runtime API guide remains `tools/labs/economy/INTEGRATION.template.md`; the balanced source and
review exports are documented in `docs/economy/README.md`. This pipeline deliberately does not rebalance them.

## Milestone verification (2026-10-13)

* Economy lab: full 111-test suite passed, including simulator bands, reachability, snapshots and source hygiene.
  The subsequent reader-only run passed all 21 cases, including four additional mixed/incomplete-export cases.
* Unity EditMode including Slow: 428/429 initially passed; the sole failure was the old assertion that every enum
  kind must exist in the live legacy database. Updated it to require implemented kinds live and imported-only
  kinds absent. The affected database/import fixtures then passed 12/12 (including two byte-identical imports).
  The five new import tests and all 21 JSON reader cases were exercised in the full run.
* Unity PlayMode: 8/8 passed.
* Editor checks: Planet, Storage, Combat, Icon and Layout all passed, with zero C# diagnostics.
* Harness: 24 captures across phone-1080x1920 and tablet-1536x2048 (HUD, build tabs, combat HUD and all four
  economy icon pages); zero harness errors, text-under-12dp, touch-under-48dp or overflow on either device.
  Contact sheets and full-resolution icon pages were visually reviewed. These are targeted captures, not a
  full four-device screen sweep.

Local, ignored artifacts: `SpheerURP/TestResults`, `SpheerURP/Logs`, and
`SpheerURP/Screens/economy-content` (including contact sheets). Batch Unity's default logs now also stay inside
the worktree's `SpheerURP/Logs`, rather than the system temporary directory.

The harness also added six pair-adjustment records to `Barlow-Medium-SDF.asset` while rendering the new labels.
Those additive font-feature changes are retained; no existing font data or interrupted work was discarded.

## Runtime cutover verification (2026-10-13)

Final full EditMode including Slow: **535 passed, 2 skipped** (Mono allocation API unavailable); PlayMode: **19 passed**;
all five editor check suites passed with **zero compile diagnostics**; economy lab: **127 passed**. Coverage includes
guarded selection/fallback, every selected catalog item reference, real v1 migration, mixed-id v2 roundtrips, legacy tools,
warehouse slots, cumulative effects, station/reach/research gates, crafted-only fuel and canonical Hyperspace loot.
No economy export/balance values were changed, and no new screenshot/device sweep was claimed. The existing generated
font SDF changes were excluded from the cutover commits.

## Persistent progression verification

Full EditMode including Slow: **539 passed, 2 skipped** (Mono allocation API unavailable); PlayMode: **19 passed**; all five
editor checks passed with zero compile diagnostics. Coverage includes research and supported-tool purchases, their effects and
save/load, reset behavior, invalid catalog entries, and round-tripping progression through a legacy-content fallback. Only
Axe/Pick/Drill ladders are enabled; builder timers, Atlas discovery/rewards and unsupported harvest minigames remain unimplemented.
