# Frontier

A portrait mobile space game: land on procedurally styled worlds, harvest resources through 3D mini-games, refine and build,
call in alien waves for loot, then spend fuel to warp to new worlds. Built with Unity 6 (URP) and UI Toolkit; all content is
data-driven ScriptableObjects.

> Working title. The repository was formerly the original *Spheer* prototype; that game was removed
> ([docs/LEGACY_REMOVED.md](docs/LEGACY_REMOVED.md), tag `legacy-spheer-v1`).

## Open it

1. Install **Unity 6000.4.0f1** (Android and iOS modules recommended).
2. Open the `SpheerURP` folder as a project.
3. Open `Assets/Frontier/Scenes/Frontier.unity` and press Play. Everything else is created from code and the data assets.

Saves live in `Application.persistentDataPath/frontier_save.json`. **Frontier > Reset Save** deletes it.

## Tools

| Do this | Run |
|---------|-----|
| Validate planets, storage, combat, layout, icons | **Frontier > Validate ...** menu items, or `tools/run-unity.ps1 -Method Frontier.EditorTools.<Check>.Run` |
| See every screen on virtual phones/tablets | `tools/capture-screens.ps1` (PNGs, UI lint in `report.json`) |
| Compare two capture runs | `tools/image-diff.ps1` |
| Apply the mobile baseline (portrait, IL2CPP, ARM64) | **Frontier > Apply Mobile Project Settings** |

Details, conventions and the code map are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Documents

| Document | What it covers |
|----------|----------------|
| [docs/ROADMAP.md](docs/ROADMAP.md) | the approved master plan: audit, architecture, universe, economy, mini-games, UI, monetization, release |
| [docs/AUDIT.md](docs/AUDIT.md) | baseline UI audit from real screenshots and lint numbers, plus the after-tokens/icons results |
| [docs/DESIGN.md](docs/DESIGN.md) | design system: tokens, type scale, spacing, hierarchy rules, icon system and how to extend them |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | code map, rules, tooling |
| [docs/SYSTEMS.md](docs/SYSTEMS.md) | how combat, warehouses, sizing, travel and panels behave today |
| [docs/LEGACY_REMOVED.md](docs/LEGACY_REMOVED.md) | what was removed from the old prototype and how to recover it |
