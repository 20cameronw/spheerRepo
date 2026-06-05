# Spheer — Game Design Document & Developer Reference

## What the Game Is

Spheer is a **mobile base-builder** inspired by *Clash of Clans* — your planet is your base. Spin/tap the world to earn resources, place structures that generate passive income across multiple resource types (Stardust, Plasma, Electricity), research upgrades, and eventually train troops to attack other bases. You are **never attacked while online** — offline shields protect your base when you're away. The goal is a satisfying loop of building, producing, researching, and progressing through worlds.

### Core Loop
1. **Spin / tap the world** → earn Stardust based on your current `power` stat.
2. **Place structures** on limited slots → each building produces a specific resource passively:
   - **Windmill** → Electricity (powers other buildings)
   - **Drill / Extractor** → Stardust (primary building resource)
   - **Cell Tower / Sat Dish** → Plasma (used for research and troops)
   - **Barracks** → trains Troops over time (consumed when attacking)
   - **Town Hall** → gates what other buildings and upgrades are available
3. **Resources** accrue every second and while offline (capped by storage buildings).
4. **Research upgrades** unlock multipliers for production rates, troop stats, build speed, and more.
5. **XP levels** unlock higher-tier structures and upgrades.
6. **Prestige** when ready — earn **Dark Matter** (permanent currency) that boosts future runs.
7. **Multiple worlds** unlock as you progress, costing **Cores** earned from progression.
8. *(Future)* **Attack other bases** using your trained troops in a separate attack scene — your home world is never the target.

### Resources
| Resource | Produced By | Used For |
|----------|-------------|----------|
| **Stardust** | Drill, Extractor, spinning the world | Buildings, upgrades |
| **Plasma** | Cell Tower, Sat Dish | Research, troops |
| **Electricity** | Windmill | Powers higher-tier buildings (requirement, not consumed) |
| **Cores** | World progression milestones | Unlocking new worlds |
| **Dark Matter** | Prestige reward | Permanent passive bonuses across all runs |
| **Void Crystal** *(planned)* | Deep Extractor (late game) | Elite troops, top-tier research |

### Tech Stack
- **Unity (URP)** — 3D mobile game targeting iOS/Android
- **LeanTween** — UI and world animations
- **Unity Ads** — banner, interstitial, and rewarded video
- **JSON file-based save system** with offline-earnings calculation

---

## Vision

Think **Clash of Clans** set in space on a 3D planet surface:
- Your planet surface is your base. Structures are placed on limited slots — layout and resource balance matter.
- Different buildings produce different resources. Electricity from Windmills is required (not consumed) to run advanced buildings — build enough Windmills or you'll be bottlenecked.
- No enemies attack you while you're online. Offline, a **Shield** system will protect your base (planned).
- Attack *other* bases using Troops trained in your Barracks — completely separate from your home world.
- Prestige resets your base but keeps permanent Dark Matter bonuses — the long-term grind loop.

---

## Planned Rework Roadmap

### Phase 1 — Strip the Alien Wave System
- Remove alien wave logic: `EnemyScripts/`, `EnemySpawner`, `MissileLauncher`, `MissileProjectile`, `TargetIndicator`, `AttackManager`, `AttackBuildingView`, `AttackWorldView`, `AttackInputHandler`, `WorldAttackTarget`
- **Keep** `Turret.cs`, `lazer.cs`, `Bullet.cs` — these will be repurposed for base defense against invading troops (same role as Archer Towers / Cannons in CoC)
- Remove combat-only research items and replace with economy/production upgrades
- Clean up `Player.cs` alien targeting system

### Phase 2 — Multi-Resource Economy
- Add `ResourceType` enum: `Stardust`, `Plasma`, `Electricity`, `VoidCrystal`
- Each `ShopItemSO` declares which resource it produces and at what rate
- `Player.cs` tracks separate pools: `stardust`, `plasma`, `electricity`, `voidCrystal`
- UI resource bar shows all active resources (like CoC top bar)
- Storage buildings cap each resource pool

### Phase 3 — Town Hall & Electricity Requirements
- `Town Hall` building gates what other buildings can be placed (level requirement)
- Buildings with an `electricityRequired > 0` field are disabled/grayed-out if player's total Electricity production is insufficient
- Windmills become critical infrastructure, not just passive income

### Phase 4 — Barracks & Troops
- `Barracks` building trains troops over time, consuming Plasma
- Troops accumulate in a troop camp (capped by camp size)
- Troops are consumed when launching an attack (future Attack scene)

### Phase 5 — Offline Shield
- On logout, a Shield timer starts (duration based on Shield Generator level)
- While shield is active, base is protected from attack (server-side or honor system)
- Shield breaks if the player attacks someone else while it's active (CoC rule)

### Phase 6 — Attack Mode *(Future)*
- Separate scene: tap-to-deploy troops onto a target base layout
- Completely decoupled from home world — home world is never the target
- Loot earned from successful attacks adds to Stardust/Plasma pools

---

## Building Reference

| Building | Resource Produced | Slot Size | Notes |
|----------|------------------|-----------|-------|
| Windmill | Electricity | 1 | Powers advanced buildings |
| Drill | Stardust | 1 | Basic extractor |
| Extractor | Plasma | 1 | Plasma production |
| Cell Tower | Plasma | 2 | Higher Plasma rate |
| Sat Dish | Stardust | 2 | Higher Stardust rate |
| Turret | — | 2 | Defends against invading troops |
| Laser | — | 3 | High-damage base defense |
| Barracks | Troops | 2 | Trains troops over time |
| Town Hall | — | 3 | Gates building tiers |
| Stardust Vault | — | 2 | Increases Stardust cap |
| Plasma Tank | — | 2 | Increases Plasma cap |
| Satellite *(orbit)* | Stardust | — | Orbit slot, no surface placement |

| World | `maxBuildingSlots` |
|-------|-------------------|
| Lerth (starter) | 12 |
| Domny | 16 |
| Chotis | 20 |
| Purp | 24 |
| Dark | 28 |
| Saturn | 32 |
| Spiky | 36 |

---

## Repository Structure

```
SpheerURP/
  Assets/
    _Scripts/
      Monobehaviors/
        UI/                    # All panel scripts (UIManager, MissionsPanel, ResearchPanel, etc.)
        Player.cs              # Singleton: resources, XP, prestige, save/load
        PopupManager.cs        # Notification icon stack
        PopupMessage.cs        # Individual notification: slide-in, wiggle, expand
        TutorialManager.cs     # First-time player timed tutorial messages
        PlacementManager.cs    # Interactive building placement
        TransactionManager.cs  # Purchase validation & resource deduction
        WorldSpawner.cs        # World model swapping & object loading
      ScriptableObjects/
        Missions/
          MissionSO.cs         # Mission data class + MissionType enum
          MissionsListSO.cs    # List container ScriptableObject
          MissionDefinitions.cs # Hard-coded mission list
        Research/              # ResearchItemSO per upgrade
        Upgrade/               # ShopItemSO per building (includes resourceType)
        World/                 # WorldSO per planet
        Lists/                 # Combined list SOs (research info.asset, shop items.asset, etc.)
      SaveSystem/
        PlayerData.cs          # Serializable snapshot of Player state
        SaveSystem.cs          # JSON read/write to persistent data path
    Prefabs/                   # Buildings, slot marker, UI cards
    Scenes/                    # MainGame + PhotoBooth
    Sounds/                    # Audio clips
    Models/                    # 3D world/building models
```

---

## Key Systems Reference

### Panel System
All panels extend `MenuPanel` and are registered in `UIManager.getPanelFromName()`.
Keys: `"main"`, `"worlds"`, `"research"`, `"structures"`, `"debug menu"`, `"info"`, `"prestige"`, `"missions"`, `"leaderboard"`, `"stats"`.

### Resource System *(planned)*
`Player.cs` will expose `getStardust()`, `getPlasma()`, `getElectricity()`, `getVoidCrystal()` alongside the existing `getDollars()` (to be migrated to Stardust). Each `ShopItemSO` will have a `ResourceType resourceProduced` field and a `float productionRate`. Buildings with `electricityRequired > 0` will be gated by total Electricity output. `Cores` and `Dark Matter` remain as progression/prestige currencies and are not produced by buildings.

### Research System
Research items (indices 0–N). `EnsureResearchCountSize()` auto-pads saves. Always call `RecalculateProductionRate()` when adding indices that affect production multipliers.

### Missions System
Missions defined in `MissionDefinitions.cs`. Missions never reset. Completing a mission toggles the checkbox image on its card. No rewards yet (planned).

### Notification / Popup System
`PopupManager.ShowPopup(message)` creates an icon that slides up the right side of the screen. If not tapped within 5 seconds it starts wiggling. Tapping expands it to a full message; tapping again closes it.

### Tutorial
`TutorialManager` detects first-time players (via `Player.hasSeenTutorial`) and sends timed messages explaining the core loop.

---
