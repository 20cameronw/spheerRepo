# Frontier design system

How the Frontier UI looks and why. Code lives in `SpheerURP/Assets/Frontier`: tokens and styles in `Resources/Frontier/Tokens.uss`
and `Frontier.uss`, helpers in `Scripts/UI/UiKit.cs` and `Icons.cs`, fonts in `Art/Fonts`, icons in `Resources/Frontier/Icons`.
Findings that drove this work are in [AUDIT.md](AUDIT.md); the build-out plan is in [ROADMAP.md](ROADMAP.md).

Reusable controls live in `Scripts/UI/FrontierControls.cs`. Unity 6 `[UxmlElement]` types are available to UI Builder/UXML:
`FrontierButton`, `FrontierCard`, `FrontierProgress`, `FrontierChip`, `FrontierTab`, `FrontierTabBar`,
`FrontierStatRow`, `FrontierModal`, `FrontierPopup`, `FrontierListRow`, `FrontierBadge` and `FrontierToast`.
They reuse the existing `fr-*` USS classes and can also be created from C#. UiKit buttons/chips/bars and live
card/tab/stat/modal/popup/toast helpers use these same types. Bind display data with their `[UxmlAttribute]`
properties; bind actions in C# rather than putting gameplay or resource grants in controls. Panels retain their
existing navigation and safe-area owners. Progress values are normalized 0-1; ordinary cards remain neutral until
`Highlighted` is true. Buttons/tabs play one shared click cue; callers must not add a duplicate cue.
Declare `xmlns:frontier="Frontier"` in UXML and use tags such as `<frontier:FrontierProgress value="0.5" />`.
Apply `UiKit.ApplyStyles(root)` and the `fr-root` class at the document root to load tokens/fonts.
`Tests/EditMode/Game/FrontierControlsFixture.uxml` demonstrates nested controls and authored attributes.

The top bar includes fuel, live Nova Shards and player level; carried capacity stays inside Cargo, not on the dock.
The startup arrival makes a full, unskippable orbital loop before its side-on descent at the screen-top silhouette.
First-launch flight lasts 5.2 seconds and repeat-startup flight 4.6 seconds, plus touchdown/reveal;
ordinary travel arrivals/departures retain their shorter presentation.
Two pale air boosters pulse downward to brake descent, cut out at contact, and finish with a small gear settle and
surface dust. This is presentation only; landing slots, travel costs and progression remain unchanged.
Ground structure foundation blocks are three times their original height, extended downward with their top surfaces
and structure transforms unchanged. Model-library foundation depth is excluded from scale fitting and surface lift,
so terrain absorbs the added depth instead of pushing the structure upward; orbital models remain untouched.
The dock retains its four positions: Research / Map / Ship / Build. Ship always opens Cargo in one tap,
with Tools and Systems beside it. Cargo owns its capacity upgrade; Systems places tank upgrades beside ship fuel
and lists engine, range, scanner and hull protection. Surface salvage is explicitly not aboard.
Research has Tech and Improvements (global defense/logistics); completed research is collapsible and available nodes sort first.
XP-locked research has a gray overlay with a lock and required XP level. Its tracking star remains interactive;
research cards omit prerequisite-navigation buttons and retain their written requirements.
The world-view control sits at top left below the HUD bar and always uses a camera glyph; its tooltip names the destination view.
The tutorial retains the full HUD width and overlays the camera button. Covered camera input and the corner project
resume stay unavailable until the tutorial is dismissed or opens another screen.
Debris is embedded slightly into the local rendered terrain at spawn, not placed on the approximate ground sphere.
Sylva uses the authored split land/ocean meshes: raised land is green, the ocean sphere blue, with a white atmosphere.
Map zoom has three discrete levels: local planetary orbits, expanded neighboring systems with their orbits, and collapsed
systems in a wider overview. Plus/minus change one level per press; pinch and wheel use the same levels.
Panning stays near scanner coverage or remembered systems; coordinates outside that space clamp to its nearest edge.
Map focuses on destinations, resources and travel. Pin location sits under the destination status at top right.
The route action stays in the destination card footer outside its scrolling details. Prerequisite actions share a
right-aligned icon row (research, local lab, station upgrade or XP only when needed). Material costs use a bounded,
undecorated wrapping container inside cards, never a nested scroller; exact quantities remain readable.
The planetary-network action is last in the details and disabled until a local Warp Gate is completed. Ship systems remain on
the Ship dock rather than a duplicate readiness action. Build lists structures without jobs/harvest shortcut rows.
Tap a harvester for resources. One hammer below the camera opens construction jobs and crew recruitment;
building headers no longer repeat that shortcut. The HUD shard balance opens the wallet and optional ad offer.
General map/network instructions live in How to Play, not above the operational controls.
Progression requirements use **player XP level**, not the highest planet tier visited. Research and Ship > Tools show
the current XP level and XP remaining to the next level, without repeated XP lessons or a full-row wave shortcut.
Research badges say `XP LV`; locks name the required and current levels. How to Play explains wave XP.
Actual tool tiers still describe equipment quality.
Planet tier remains a world difficulty/resource property, not a purchase requirement.
Missing-material cost and network chips are a single native button containing the existing icon and
quantity/name, using the chip's existing boundary. Do not overlay another icon-sized button or add a
FIND caption/hover box. Pointer interaction keeps the chip's original styling; keyboard focus uses
its existing border. Contextual shortages first compare usable, locally stored and explicitly remote
stock, then production and reachable discovered worlds. A shortfall names its actual quantity.
Shortage panels do not inject previous-step or return-to controls into other panels.
Missing-resource chips open a smaller dimmed overlay without discarding the originating screen.
Keep exact quantities, source names and travel/storage warnings visible; source actions use icon buttons
with localized tooltips. Back, close and backdrop dismiss only that overlay; choosing a source opens its real destination.
All shortage source, capacity and manual actions share one compact horizontal strip below the explanations,
wrapping only when needed; source-specific tooltips identify their destination.
Project tracking is explicit; card headings use a small star action instead of an extra full-width button.
Stars are borderless glyphs with full touch targets: outlined when off, filled cyan when tracked, with an action tooltip.
Resume is a star in the top-right
corner below the HUD bar when full-width first-session guidance is not covering the world controls. Lab requirements use a lab icon that opens
and scrolls to Build > Utility > Research Lab. All build-project routes share the same category-aware destination.
Outside research, prerequisite icons sit in card headings beside the tracking star, without a separate empty action row.
Map opens in Tracked needs mode when that project has missing costs. Green diamond markers identify known
systems/planets with those raw deposits; matching resource chips are outlined. Filled costs and completed or
untracked projects clear their highlights. The normal material filter remains available.
Dashed cyan rings show actual scanner/Radar Tower survey coverage, distinct from the solid jump-range ring.
Unscanned systems (including authored destinations) are hidden in all map zoom levels and cannot be revealed
by resource filters, names or canonical IDs. Coordinates only move the viewport. Visited, scanned and previously
pinned destinations remain known across saves; legacy discoveries are retained rather than reset.
How to Play walks through harvesting, storage, crafting/fuel, XP waves, local-lab research, upgrades, exploration and expansion.
The skippable live first-session guide also requires first research and an upgrade after travel; existing completed
actions count immediately and dismissed guides stay dismissed. Research guidance opens the local lab shop card if
missing, otherwise the lab popup's Tech tab. That popup includes working Tech, Tools and Lab-upgrade tabs;
branch filters and purchase refreshes stay inside the popup. No XP, cost or prerequisite gates are bypassed.
Locked/in-progress producers retain their real upgrade/research/job status; guidance never spends,
teleports or starts a job. One explicitly tracked project provides live requirements and a resume action.
The return summary links completed jobs and stored offline output to their real location, not ship cargo.
Reward offers use explicit ad labels and exact bonuses beside the normal action. iOS gameplay ads are enabled;
watching remains player-initiated, with a contextual advertising-only consent prompt before the first request.
Offers remain visible with a disabled button and explicit playback/cooldown reason when unavailable;
Editor/desktop does not simulate ads or rewards. Construction's offer sits beside its timer, and harvest's
exact bonus is visible from the first successful result. Ordinary collection/waiting remains available.
Available Place buttons share one row with a compact crystal/price action. That action buys an unlocked
structure instantly without materials or a builder, at twice its timer-skip price (minimum 10 shards).
The pad-selection banner repeats the price; choosing a pad confirms the spend, while cancellation is free.
Map sampling includes an offscreen preload margin and refills before that margin reaches the viewport.
The Atlas resource header retains its separate icon control to reveal raw-resource deposits.

Online ranks are available to all ages after explicit online opt-in, without name or birthday entry.
Keep local score/record details on Stats and put online ranks on their own Leaderboard page.
How to Play owns era requirements and the next-step action; the live tutorial links to it directly.
Opening that page after opt-in explicitly submits the current Frontier Score and loads the top 20; it
never uploads at launch or during normal play. Before opt-in, explain anonymous Unity authentication
and score/connection-data sharing and require a deliberate enable action. Show the player's returned rank
outside the list too, with a distinct top-three podium. Display anonymous Pilot labels rather than UGS
profile names. Keep real network errors visible and retryable; never substitute local sample scores for a
failed server response.
These client-reported casual ranks are not a claim of server-validated anti-cheat.

Modal, popup and toast shells enter with a 160 ms ease-out translation (16 px, 6 px and -8 px respectively);
they do not animate layout sizes or delay interaction/dismissal. World structure and build-pad labels use native
Unity 6.4 `UIDocument` world-space rendering with one shared panel per `WorldSurface`, the same Chakra font,
camera-facing anchors and existing planet-occlusion/selected-only rules. These labels ignore picking; structure
taps remain owned by the world input path. Detailed popups stay in the safe-area screen overlay and use the
existing structure-anchor projection rather than putting long interactive panels in 3D.

`Resources/Frontier/UiStrings.asset` is the first editable English string table. Use `UiText.Get("stable.key", args)`
for its migrated navigation/settings/harvest-shell/world-label/travel copy; keep entire sentences and parameterized
templates together rather than concatenating fragments. Duplicate/empty keys, missing lookups and invalid format
arguments fail explicitly. This is copy centralization, not completed localization: other screen/content strings,
locale selection, translations, RTL and language/font coverage remain outstanding. Do not expose another locale
before those paths and assets are ready. Runtime overlay and world-label panels cap dynamic atlases at
1024/2048/4096 for Low/Medium/High and update on quality changes without modifying authored panel assets.

The shared `ShipRig` is a procedural low-poly presentation asset with a steel hull, cyan wing/canopy accents,
twin engine plumes, three folding landing legs and landing lamps. Local +Z is forward. It owns four small meshes
and one palette material, totals 85 triangles across eight renderers, and adds no colliders or gameplay state.
Hyperspace and travel takeoff/touchdown use this same rig; banking/gear/throttle never modify simulation.
World-space rigs also own two small exhaust emitters using the existing Burst template/shared material: each
is capped at 24 particles before quality scaling, emits at up to 60 particles/sec, follows unscaled presentation
time, and stops/clears at zero throttle. Low quality caps each at 11 particles / 27 particles/sec; live quality
changes update the emitters. Offscreen Hyperspace uses camera-scoped mesh draws and its existing trail instead
of globally visible particle renderers. The stage reserves layer 31, also excluded from the world camera.
Cached procedural ignition/touchdown cues route through the existing SFX bus and respect mute.
Brief travel ship beats are skippable by the visible Skip button or Back, without skipping fuel payment or a run.
Startup holds the wordmark over the starfield for 0.65s on first launch, flies a complete orbit and then descends,
revealing the top bar/dock with a 220 ms transform-only slide. Later launches omit the logo, not the loop.
Neither the loop nor its descent has a skip control or Back bypass. Intro completion records only a local presentation preference, not progression. World/input
and transforms are restored before publishing runtime readiness. Arrival uses a pooled amber heat glow, dust
burst and touchdown ripple, then a brief damped camera impulse that restores the exact camera origin on
completion or skip; effects use the existing ownership paths.
The procedural sequence covers the planned travel beats without an additional Timeline/Cinemachine/DOTween
dependency; production art/audio and physical-device visual/performance validation remain outstanding.

Called-wave results use a brief pooled neon ripple/burst centered on the current planet (cyan on victory, red
on defeat); these cues are presentation-only and never target another planet.

Harvest abandonment feedback and unsigned XP chips also use stable catalog templates. Tool-upgrade locks
are rendered from their existing localized reason rather than comparing presentation text to English.

First-session objective headings, help, contextual cues/actions and skip tooltip use stable string keys, as do
backpack/salvage and warehouse/fuel/hopper/cargo/relay help, filters, transfer feedback and empty states.
Category captions use `resource.category.<lowercase enum name>`; adding a category requires its table entry.
Keep whole transfer sentences in the table rather than joining translated verbs to numeric suffixes.
Command tabs/base/tool/upgrade rows, research branches, Goals, travel assist/sensitivity and shared effect
templates use the same catalog. World construction/clearance states, harvest actions/estimates, recipe batch
controls and called-wave previews/HUD also use stable keys. Specialty structure help/stats, clearance duration
units and ranked-alien/modifier captions complete this popup pass (431 English entries). Keep singular/plural batch
capacity sentences separate and preserve authored resource/structure/alien names. Busy non-max structures show
their job state rather than MAXED. Colony Hub summaries show both authored extra crews and clearance discount;
the live clearance price uses the catalog's percentage capacity column, not the separate extra-builder effect.
Gameplay-generated lock reasons and remaining settings/content copy
are not yet migrated; the catalog still does not provide locale selection or translations.

Map search/empty/not-found/error feedback, zoom readouts, destination/pin/survey/hazard/deposit captions and
manual/autopilot prices/actions now use whole catalog templates. Authored world/resource names and generated
biome/size/hazard/method identities remain data, not translated content. Coordinate search still parses invariant
x,y input; previews never spend fuel/shards or change the current world. The English catalog now has 505 entries.
Settings/generated identities and full localization remain open.

All five Atlas tabs use catalog captions, including discovery counts/states, resource acquisition/samples/uses,
recipe gates and input/output, visited-world details, unidentified structures/lifeforms and revealed enemy stats,
armor/boss cadence/bounties. Resource categories reuse the shared category keys; authored names/descriptions and
generated identities remain content. Navigation does not discover entries, award shards, alter resources or travel.
The English catalog now has 566 entries; this is not full localization.

Privacy choices and account recovery/cloud comparison, backup, restore and undo captions/status/errors use
whole catalog templates, including provider/schema/snapshot parameters and second-press warnings. Provider
messages and GameState validation errors remain their own generated text. Privacy help now accurately describes
the implemented manual cloud flow instead of implying that no local/cloud transfer exists. Providers remain
disabled, consent stays opt-in and automatic upload is still absent. This brings the English catalog to 630 entries.
Live provider/native flows, remaining generated/rank/content strings and full locale/font/RTL work stay open.

Ranks/era actions, score-category and record labels, online opt-in/unavailable/loading/empty captions and
connection feedback now use the same catalog (660 English entries). Numeric score formatting, the seven score
components, next-era navigation and read-only/explicit-request online boundary remain unchanged. Era identities
and requirement messages remain generated gameplay content; Ascension and competitive writes stay disabled.

Settings exposes independent Effects & UI and Music sliders, with live percentage labels and 104px input areas.
Dragging updates the current pooled sources without rebuilding the control; values persist locally and apply
when the audio host is recreated. Each source retains its nominal cue volume, so repeated adjustment never
compounds attenuation. Effects includes UI and engine/ambience beds; music includes music-bus one-shots and the
music loop. Zero is silent and master mute still stops playback without changing either saved level. Existing
authored mixer routing is preserved. The catalog now contains 663 English entries. Production music/audio assets
and physical-device sound/touch validation remain open; a volume control does not supply a missing soundtrack.

Settings separates Online Save & Account Deletion from local Reset Progress. Two presses confirm the current
signed-in ID; Cancel, leaving Settings or an identity change discards the confirmation. Erasure remains available
after withdrawing consent and never signs in, creates an account, uploads or resets local progress/restore backup.
The provider removes only Frontier's cloud key with a fresh write lock before deleting the exact UGS account.
Cloud failure stops the account step; account failure explicitly reports partial cloud removal and permits retry.
Pending cloud/recovery/rank operations block deletion and vice versa. The English catalog now has 839 entries;
harvest mini-game instructions and live feedback plus known leaderboard failure statuses also use localized
catalog templates.
Providers remain disabled; `COMPLIANCE.md` tracks legal, web deletion, other backend records and native/store gates.

Unity Localization 1.5.13 now supplies the runtime English UI. `Frontier > Localization > Sync English Catalog`
updates the preloaded `Frontier UI` collection from `UiStrings.asset`, keeping stable entry IDs and leaving other
locales untouched. Edit that source catalog for English copy, not the generated English StringTable. Bootstrap
awaits asynchronous localization initialization and table loading before creating UI; missing assets/keys fail
explicitly instead of displaying success-shaped fallback copy. Editor-only tools can still read the source catalog.
Current-culture composite formatting and literal braces retain their existing semantics; entries are not Smart Strings.
The optional Ascension panel, including confirmation, lock reasons and save outcomes, also uses structured catalog
entries; localizing this copy does not enable its still-gated gameplay.
Build preprocessing rejects stale/missing tables. Addressables content explicitly builds with players and uses
local bundles, with no remote content host required. `Build Local Tables` synchronizes and builds these bundles.
The local Android-target bundle build passed; that is not an APK/native-device sign-off. Only English is authored:
language selection/live switching, translations, remaining generated text, locale fonts and RTL are still open.

Build > Construction Jobs lists active structure construction/upgrades and obstacle-clearance jobs across visited worlds.
Timers and finish prices update once per second without replacing touch controls.
Structure finish uses the authored shard curve, including its free-finish window, and requires a second press.
The game revalidates the job, assignment, quoted maximum price, wallet and read-only state, then uses the normal
completion/reward path and saves job/currency changes together. A failed write explicitly reports the completed
session state and remains dirty for retry; retry cannot spend or award twice. Off-world jobs can finish without
moving the player or charging travel. Local clearance opens its surface controls; remote clearance links to the
world's map card rather than teleporting. Native local crew reminders are enabled with a separate device
preference and OS permission; they do not settle jobs or grant rewards. Ad speed-up and server time remain open work.
Crews also offers the authored third/fourth crew purchases, only once at least two crews are owned and below
the shared cap. Hub-earned and purchased crews stack only up to that cap; a capped player is never offered an
unneeded purchase. Recruitment needs two presses, rechecks the quoted crew count/price and wallet, persists
the entitlement with the debit and reports failed writes explicitly. Purchased crew flags live in the versioned
save's settings, not device preferences, and clear on reset. Active jobs precede recruitment in the scroll view.
Crew captions bring the English catalog to 455 entries.

The Hyperspace downed dialog also offers the authored shard revive price and wallet balance, alongside the
existing free/external-offer path. Spending requires a second press and revalidates price, balance, writable
save and the simulation's shared revive cap. A successful revive transitions the run before another press can
spend again, then saves the debit immediately. Failed writes surface a pending-save warning without repeating
the charge. Pending external offers block paid revives; late/duplicate answers cannot grant another revive.
Background/focus guards and explicit Resume are retained. Finish Trip does not spend the pending quote or
award unfinished-leg rewards/refunds. These four captions bring the English catalog to 459 entries.
This uses earned shards; it does not activate ads/IAP or add durable run restoration after process exit.

Live construction/upgrade/clearance timers use a UTC anchor captured at startup and monotonic elapsed time
within the session. Device-date changes mid-session do not advance or rewind jobs. Offline restart recovery
still uses untrusted device UTC; this is not server-authoritative timing.

## 1. Principles

1. **One thing is loud.** Neon colour plus glow is reserved for the **primary action**, the **selected item** and **live state**
   (a running wave, an armed build pad, a pressed button). Ordinary cards, rows, tabs and dock buttons are *tonal dark glass*: a
   dark surface with a thin, muted outline. A screen should read in one glance: what can I do, what is selected, what is live.
2. **Neon-space identity stays.** Deep navy surfaces, cyan/magenta/lime/amber accents, glowing planets behind the UI.
3. **Tokens, not numbers.** Sizes, spacing, colours and type come from `Tokens.uss` (or the `UiKit.Space/Size` mirrors in code).
   If a value is not in the scale, the scale is wrong, not the call site.
4. **Readable on a phone first.** Caption >= 12 dp, body >= 14 dp, every touch target >= 48 dp. The screenshot harness lints this
   on every screen of every device.
5. **Any portrait screen.** Layout is flex-based with a capped canvas width, safe-area aware, and verified on four virtual
   devices (plus an opt-in 360 dp phone).

Empty backpack/base/storage/relay states explain the next real action and link to an operational compatible gatherer,
Gather Shop or the map. Do not show item-transfer instructions when no items exist or grant resources through navigation.
Settings keep presentation separate from progression: Automatic/Low/Medium/High quality and Automatic/30/60 fps change
only rendering/frame targets. Low retains the neon materials, all enemies, input targets and combat telegraphs while
removing post-processing and reducing decorative particles, stars and offscreen resolution. Phone UI remains full resolution.
Ranks leads with the reached era, concrete next requirements and a route to the relevant live screen, followed by the
Frontier Score breakdown. Era progress is a persistent achievement of this save, not another spendable currency.

Goals live in Menu and are linked from Ranks. Ready rewards lead, followed by three concrete next objectives;
players can expand the remaining list and collected history. Actions open the live build/gather shop, map,
Research or recipe Atlas, rather than a second tutorial. The authored 26-entry ScriptableObject catalogue uses
only one-time milestones already represented in the simulator (12 small, nine reach tiers, five era entries).
At current tuning the complete catalogue awards at most 275 Nova Shards per save, in addition to the separate
existing Atlas rewards. Calling a wave alone is insufficient: the live defense achievement requires clearing it.
Completion persists even if its original evidence is removed. Currency and the claim ledger share one atomic
save; a failed write explicitly reports an unsaved session reward and retries without paying again in memory.
Older saves record proven prior completions without retroactive rewards. No timed/daily reward faucet is added.

## 2. Units and scale

The UI is rendered by one `PanelSettings.scale`. `UIRoot.ComputeScale(shortSidePx, density)` returns
`max(density / 2, shortSidePx / 900)`:

* On phones **1 UI unit = 0.5 dp** (`UIRoot.UnitsPerDp = 2`). A 411 dp phone is a 822-unit wide canvas, 390 dp is 780, 360 dp is 720.
* The canvas is never wider than **900 units** (`UIRoot.MaxCanvasWidth`). Tablets scale *up* instead of stretching the layout:
  the 1536x2048 tablet runs at 1.707 px per unit.
* The old `UiBoost` (a fixed 1.2 multiplier on a 1080 reference) is gone. It made dp targets impossible to reason about; now a
  token value is simply `dp * 2`.

Consequences used throughout: caption **26 units = 13 dp**, body **30 = 15 dp**, touch **104 = 52 dp**, spacing 8 = 4 dp.

Safe areas come from `ScreenMetrics` (never `Screen.*`). Content layers (popups, modals, banner) are inset by the safe area plus
`UIRoot.HudTopInset` (132, below the top bar) and `UIRoot.DockInset` (176, above the dock). Toasts start below the top bar.
Overlays that replace the world (mini-games, wave result) cover the dock.

## 3. Tokens (`Tokens.uss`)

Defined on `.fr-root`, so every descendant sees them. USS cannot use `var()` inside math or nested in functions
(`rgba(var(--c), .5)` is invalid), so every tint is its own token.

**Linear colour space.** The project renders in linear colour space, where translucent overlays blend far brighter than the same
alpha does in a design tool (an alpha of 0.14 reads like 0.38). Surfaces, outlines and tints are therefore **opaque colours picked
by eye**, identical on every device. Keep alpha for scrims, halos and Painter2D map layers only (and author those very low).

| Role | Token | Value |
|------|-------|-------|
| Background | `--fr-bg-0` | `rgb(3, 5, 16)` |
| Surfaces (glass tiers) | `--fr-surface-1/2/3` | `rgb(8,12,32)` card/panel, `rgb(15,23,50)` inset, `rgb(27,39,78)` hover/raised |
| Outline | `--fr-outline`, `--fr-outline-strong` | `rgb(38,54,92)`, `rgb(76,100,150)` |
| Text | `--fr-text-hi/lo/dim` | `rgb(234,245,255)`, `rgb(166,188,216)`, `rgb(112,132,164)` |
| Neon accents | `--fr-cyan/magenta/lime/amber`, `--fr-danger` | `#1AF2FF`, `#FF40F2`, `#80FF4D`, `#FFB340`, `#FF4D59` |
| Spacing | `--fr-sp-4/8/12/16/24/32` | 4, 8, 12, 16, 24, 32 units |
| Radius | `--fr-r-sm/md/lg` | 10, 18, 26 |
| Type | `--fr-fs-caption/body/label/title/display` | 26, 30, 32, 40, 56 |
| Controls | `--fr-touch`, `--fr-icon-sm/md/lg/xl` | 104, 32, 40, 56, 72 |
| Motion | `--fr-dur-fast/base/slow` | 100, 180, 320 ms |
| Scrim | `--fr-scrim` | `rgba(2,3,12,0.72)` |

**Accent classes.** A panel picks its accent with `.fr-acc-cyan|magenta|lime|amber|red|neutral`, which sets three variables for
all descendants: `--fr-acc` (neon, for text/icon/selected outline), `--fr-acc-line` (the muted version used for the *resting* thin
outline) and `--fr-acc-soft` (dark tint fill for pressed/selected/primary). In code: `UiKit.SetAccent(element, color)` snaps any
colour to the nearest accent class. The C# palette (`UiKit.Cyan`, `Magenta`, `Lime`, `Amber`, `Muted`, `Neutral`) mirrors the CSS
tokens; keep them in sync.

Transitions are limited to `scale` (press feedback). Transitioning background/border colours produced mid-transition grey frames
in captures, so state changes are instant.

## 4. Typography

Two SIL OFL faces, built as TextCore dynamic SDF FontAssets and referenced with `-unity-font-definition`:

* **Chakra Petch** (Medium, SemiBold, Bold): display, titles, labels, buttons, dock captions.
* **Barlow** (Regular, Medium, SemiBold): body, captions, numbers in lists.

`Frontier/Build Font Assets` (`Editor/FrontierFonts.cs`, method `FrontierFonts.Run`) regenerates `*-SDF.asset` next to the TTFs in
`Art/Fonts` (72 pt sampling, 6 px padding, SDFAA). The assets are **dynamic**: glyphs and atlas pages are never serialised
(`Clear Dynamic Data On Build`); the atlas is built from the TTF at runtime the first time each character is drawn (ASCII, Latin and
anything else the font has), only the kerning table is serialised.
Running the game does not change the committed files. Licences: `Art/Fonts/OFL-ChakraPetch.txt`, `OFL-Barlow.txt`.

| Level | Class | Size (units / dp) | Face | Use |
|-------|-------|-------------------|------|-----|
| Display | `.fr-t-display` | 56 / 28 | Chakra Petch Bold | big numbers and result headings (defined, not used yet) |
| Title | `.fr-t-title` (`.fr-title`) | 40 / 20 | Chakra Petch Bold | panel titles, planet name |
| Label | `.fr-t-label` (`.fr-h2`) | 32 / 16 | Chakra Petch SemiBold | card titles, buttons |
| Body | `.fr-t-body` (`.fr-muted`, `.fr-small`) | 30 / 15 | Barlow Medium/SemiBold | descriptions, rows, chips |
| Caption | `.fr-t-caption` (`.fr-tiny`) | 26 / 13 | Barlow Medium | dock captions, hints, section labels |

The legacy class names still work and map onto the scale. Nothing may go below caption; if text does not fit, shorten the copy or
let the row wrap, do not shrink the font.

## 5. Components and hierarchy

| Component | Class(es) | Resting look | Neon only when |
|-----------|-----------|--------------|----------------|
| Dock button | `.fr-dock-btn` | surface-1, thin accent-line outline, accent icon, grey caption | pressed (accent fill + caption) |
| Top bar | `.fr-topbar-bar` | surface-1 pill with a planet-tinted hairline | never (the menu button is a tonal round button) |
| Button | `.fr-btn` | tonal, thin accent-line outline, accent label | hover/press; `-primary` = accent fill + 3 px outline (**one per screen**) |
| Ghost button | `.fr-btn-ghost` | transparent, strong outline, white label | n/a (secondary and back actions) |
| Icon button | `.fr-iconbtn` | tonal square/round, >= 104 units | pressed |
| Modal / popup | `.fr-modal`, `.fr-popup` | surface-1, 2 px muted outline, 3 px accent top edge | n/a (the accent top edge identifies the panel) |
| Tab | `.fr-tab` (column: icon over label) | surface-2, outline | `.fr-tab-on` selected (accent outline + soft fill) |
| Card / row | `.fr-card` | surface-2, thin outline, no halo | `.fr-card-on` selected, `.fr-card-dim` locked/unavailable |
| Item chip | `.fr-itemchip` | tonal pill; icon carries the colour | never |
| Slot (backpack) | `.fr-slot` | surface-2 | selected slot gets the accent halo |
| Toast / banner | `.fr-toast`, `.fr-banner-box` | surface-1 with a thin coloured outline | banner glow marks the armed build mode |

Spacing rules: panel padding 24, gap between cards 12, gap inside a card 8-12, row heights come from `--fr-touch`, never below it
for tappable rows. Corner radius: `r-lg` panels and primary buttons, `r-md` cards and tabs, `r-sm` chips and slots.

**First-session cues.** The optional guide uses one static lime caption with a downward arrow next to the current actionable
build/harvest/craft/wave control. It is not another button, does not intercept touches, and never points at a disabled action.
Only the first affordable recipe is marked; relevant shop entries move to the top while the other entries keep their order.
Skipping the guide also removes its cues. Guide actions select operational local structures directly when possible.

## 6. Icon system v2

### Pipeline

Icons are **SVG files** in `Resources/Frontier/Icons/<name>.svg`. Unity's built-in SVG importer
(`Unity.VectorGraphics.Editor.SVGImporter`, default settings) turns each into a `VectorImage`; `Icons.Make(name, color, size)`
loads it with `Resources.Load<VectorImage>` and tints it via `-unity-background-image-tint-color`, so every file is plain white
and gets its colour from a token. The result is resolution independent and antialiased from 24 to 128 px. The old polyline table
(`Painter2D`) and the PNG icons are removed. The Painter2D/SVG-path fallback was not needed: the importer renders arcs, ellipses,
dashes, opacity and round joins correctly, and tinting works on `VectorImage` backgrounds.

Two importer details worth knowing:

* The default tessellation mode ("Antialiased Arc Encodings") **crops the image to the drawn shapes** and ignores the viewBox. Every
  SVG therefore starts with an invisible full-grid rect, `<rect width="48" height="48" fill="#FFFFFF" fill-opacity="0"/>`, which keeps
  the 48x48 grid so all icons share one optical box. Do not remove it.
* Triangulation mode keeps the viewport but is not antialiased; do not switch to it.

### Drawing rules

* 48-unit grid, **2.5-unit stroke**, round caps and joins, ~3 units of safe padding, optical balance over mathematical centring.
* **One metaphor per icon.** No fills, except a faint (12-16 %) fill on object silhouettes so they read as solid at small sizes.
* At most **one small accent** (dot, spark, bubble), as in the flask's bubbles.
* White only (`#FFFFFF`). Colour comes from the tint.
* Every glyph must stay distinct at 32 px. Check it on the icon sheets (below), not in the editor preview.

All 95 icons are original drawings (no third-party shapes, so no attribution file is needed). The economy content
milestone adds 47 glyphs without replacing the existing visual family.

### Set

| Group | Icons |
|-------|-------|
| Dock and controls | `research` (flask, **RESEARCH**), `map` (planet with orbit), `ship` (**SHIP**, cargo-first), `pack` (cargo/backpack within panels), `build` (**hammer crossed with a toothed hand saw**), `menu` (hamburger), `close`, `plus`, `minus`, `up`, `back`, `check`, `lock`, `play` |
| HUD and glyphs | `fuel`, `star`, `bolt`, `skull`, `sound`, `mute`, `gear`, `ranks`, `tutorial`, `about`, `alien`, `shield`, `tools`, `base`, `warp`, `ship` |
| Resources | `wood` (log end), `stone` (faceted rock), `iron` (ore chunks with a shine), `crystal` (gem), `fuel` (drop), `core` (orb with slit pupil), `scrap` (broken gear), `alloy` (ingot), `plasma` (energy cell), `cube` (generic fallback) |
| Structures | `refinery`, `fabricator` (anvil), `alarm` (beacon), `lumber` (axe), `mine` (pick), `drill`, `gunner`, `laser`, `satellite`, `warehouse` (crates) |
| Economy resources | All 34 canonical resource ids now resolve to distinct glyphs; `alloy` remains for legacy content. Concepts: [economy/ICON_BRIEF.md](economy/ICON_BRIEF.md). |
| Economy structures | 21 additional glyphs cover every appended `StructureKind`, from `foundry` through `dyson_collector`; imported data does not imply implemented gameplay. |

### Naming and lookups

* `Icons.Make("fuel", color, size)`, `Icons.IconButton`, `Icons.Apply`: unknown names fall back to `cube`.
* `Icons.ForResource(resource)`: an authored `icon` override first, then the exact resource id.
* `ForResourceId(id)`: all 34 economy resource ids have dedicated glyphs, then a **keyword category fallback**
  (`"unknown_copper_ore"` gets `iron`, `"helium"` gets `fuel`, `"relic"` gets `core`...), then `cube`.
* `Icons.ForStructure(kind)` and `ForTool(kind)` for structures and harvest tools (`UIRoot.StructureIcon(kind)` delegates).
* `Icons.Groups` lists every shipped name; the contact sheets are built from it.

### Adding an icon

1. Draw `<name>.svg` on the 48 grid following the rules above, including the invisible rect.
2. Drop it into `Resources/Frontier/Icons`; Unity imports it (commit the `.meta`).
3. Add the name to `Icons.Groups` and, if it is a resource/structure, to the mapping above.
4. Run the icon check (**Frontier > Validate Icons**, or
   `tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierIconChecks.Run`): every listed icon imports as a 48x48 `VectorImage`,
   every SVG on disk is listed, every resource and structure has its own glyph, and the keyword fallback works.
5. Run the harness (`-Only icons`) and inspect the original sheets plus the four `icons-economy-*` pages.

### Contact sheets

Harness shots draw icons at **24, 32, 48 and 64 physical px** on a dark surface with labels: `icons-ui` (controls and
glyphs), `icons-items` (legacy resources and structures), `icons-color` (live items in their game colours), and
`icons-economy-resources-1/2` plus `icons-economy-structures-1/2` (complete catalog coverage, paginated to avoid clipping).
They are the acceptance test for legibility and consistency.

## 7. Debug gate

The menu's **DEBUG** tile and page exist only when `Debug.isDebugBuild || Application.isEditor` (`UIRoot.DebugToolsAvailable`). The
harness runs in the editor and still captures them; the `menu-release` shot forces the release behaviour
(`DebugMenuAsRelease()`) to prove the gate hides the tile.

## 8. Verifying a change

```powershell
# render and lint (4 devices; add -Devices phone-1080x2340 for the opt-in 360 dp phone)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Out <abs dir under SpheerURP\Screens\>
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\contact-sheet.ps1 -Dir <run>\phone-1080x1920 -Columns 7 -Rows 2 -ThumbWidth 270
```

`report.json` has per-shot lint: `minFontDp`, `textUnder12dp`, `minTouchDp`, `touchUnder48dp`, `overflowing`. Acceptance for UI
changes: zero `overflowing` everywhere and zero `textUnder12dp` / `touchUnder48dp` on the phone devices (mini-game overlays may keep
small 3D-adjacent labels but must be reported). Then look at the contact sheets of every device: lint cannot see overlaps,
clipping or inconsistent padding.

## 9. Known gaps

* The mini-game arena render target is sized from the old reference height (`Minigames.cs`), so on phones it is ~75 % of native
  resolution; it was out of scope for this pass.
* World-space structure labels (3D text) can peek out from under a popup.
* The tutorial copy still describes the old button positions (text-size fixes only were in scope).
* Map labels are world-space and scale with the zoom, so the lint ignores them; they remain small at the default zoom by design.
* A UXML/UI Builder migration of the code-built HUD is planned; class names and tokens are already shaped for it.
