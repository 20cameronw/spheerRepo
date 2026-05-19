# Spheer — Game Design Document & Developer Reference

## What the Game Is

Spheer is a **mobile idle/clicker base-builder** inspired by *Clash of Clans* and *Egg Inc* — your world is your base. Tap it to earn money, buy structures that generate passive income, research powerful upgrades, and defend against alien invasion waves. The goal is a satisfying loop of building, earning, defending, and prestiging that keeps you coming back.

### Core Loop
1. **Spin / tap the world** → earn dollars based on your current `power` stat.
2. **Buy structures** in the shop → each one adds to your passive income-per-second and physically appears on your world. Each world has a **limited number of building slots** — choose wisely.
3. **Passive income** accrues every second (and a portion accrues while you're offline).
4. **Research upgrades** unlock multipliers: click power, production rate, turret stats, XP rate, sell-back rate, and more.
5. **Enemy waves** (UFOs / aliens) invade at increasing difficulty. Turrets and lazers auto-attack; you can also **tap an alien directly** to deal bonus damage + see a hit marker.
6. **XP levels** unlock higher-tier structures.
7. **Prestige** when you're ready to reset — earn **Dark Matter** (permanent currency) that boosts all future earnings.
8. **Multiple worlds** unlock as you progress (Lerth → Domny → Chotis → Purp → Dark → Saturn → Spiky…).

### Tech Stack
- **Unity (URP)** — 3D mobile game targeting iOS/Android
- **LeanTween** — UI and world animations
- **Unity Ads** — banner, interstitial, and rewarded video
- **JSON file-based save system** with offline-earnings calculation

---

## Vision — *Spheer: Clash of Worlds*

Think **Clash of Clans** meets **Egg Inc**:
- Your planet surface is your base. Structures are placed on limited slots, so layout matters.
- Income compounds with smart building choices (Egg Inc–style scaling).
- Defense towers protect your base from alien waves that get harder forever.
- Prestige resets progress but unlocks permanent bonuses — the offline grind loop.

---

## Future Scope (Planned Features)

| Feature | Description |
|---------|-------------|
| **Build Timers** | Structures take real time to construct (Clash of Clans–style). Higher tiers take longer. |
| **Builder's Huts** | Buy extra builder slots to construct/upgrade multiple buildings simultaneously. |
| **Science & Research Rework** | Research tiers require accumulated "Science" points (earned by buying upgrades), not XP level — like Egg Inc's research progression. |
| **In-App Purchases** | Gem packs, instant-build skips, Dark Matter boosts. |
| **Rewarded Ads** | Watch an ad to double offline earnings, get a free research boost, etc. |
| **Piggy Bank** | Passively collects in-game currency. Break it (via real purchase or ad) to collect. |
| **More Structures** | New buildings with unique effects (e.g., Shield Generator, Warp Drive income booster, DNA Splicer). |
| **Mission Rewards** | Completing missions will grant real rewards (currency, Dark Matter, boosts). |
| **Leaderboard Polish** | Cross-platform leaderboards via Unity Gaming Services. |


| World | Suggested `maxBuildingSlots` |
|-------|------------------------------|
| Lerth (starter) | 12 |
| Domny | 16 |
| Chotis | 20 |
| Purp | 24 |
| Dark | 28 |
| Saturn | 32 |
| Spiky | 36 |


| Building | Suggested `slotSize` |
|----------|-----------------------|
| Windmill | 1 |
| Drill | 1 |
| Cell Tower | 2 |
| Sat Dish | 2 |
| Gunner | 2 |
| Laser | 3 |
| Missile Silo | 3 |
| Satellite *(orbit)* | — (ignored, orbit items skip placement) |

## Repository Structure

```
SpheerURP/
  Assets/
    _Scripts/
      Monobehaviors/
        EnemyScripts/          # Enemy state machine (Idle, Approaching, Attack, Leaving)
        UI/                    # All panel scripts (UIManager, MissionsPanel, ResearchPanel, etc.)
        Player.cs              # Singleton: money, XP, prestige, targeting, save/load
        EnemySpawner.cs        # Wave generation & enemy scaling
        PopupManager.cs        # Notification icon stack (Egg Inc-style)
        PopupMessage.cs        # Individual notification: slide-in, wiggle, expand
        TutorialManager.cs     # First-time player timed tutorial messages
        Turret.cs / lazer.cs   # Defense building logic
        PlacementManager.cs    # Interactive building placement
        TransactionManager.cs  # Purchase validation
        WorldSpawner.cs        # World model swapping & object loading
      ScriptableObjects/
        Missions/
          MissionSO.cs         # Mission data class + MissionType enum
          MissionsListSO.cs    # List container ScriptableObject
          MissionDefinitions.cs # Hard-coded mission list (45 missions, 9 types × 5 tiers)
        Research/              # ResearchItemSO per upgrade
        Upgrade/               # ShopItemSO per building
        World/                 # WorldSO per planet
        Lists/                 # Combined list SOs (research info.asset, shop items.asset, etc.)
      SaveSystem/
        PlayerData.cs          # Serializable snapshot of Player state
        SaveSystem.cs          # JSON read/write to persistent data path
    Prefabs/                   # UFO enemy, buildings, slot marker, UI cards
    Scenes/                    # MainGame + PhotoBooth
    Sounds/                    # Audio clips
    Models/                    # 3D world/building models
```

---

## Key Systems Reference

### Panel System
All panels extend `MenuPanel` and are registered in `UIManager.getPanelFromName()`.
Keys: `"main"`, `"worlds"`, `"research"`, `"structures"`, `"debug menu"`, `"info"`, `"prestige"`, `"missions"`, `"leaderboard"`, `"stats"`.

### Enemy Tap Damage
A UI button is placed over each enemy prefab. Add the following to its **OnClick** list:
- `EnemyStateManager → TapEnemy()`

This deals configurable tap damage and spawns a hit marker via UIManager.

### Research System
30 research items (indices 0–29). Indices 13/18/19/23 all contribute to `productionRateMultiplier` — always call `RecalculateProductionRate()` when adding new indices that affect it. `EnsureResearchCountSize()` auto-pads saves.

### Missions System
45 missions in 9 categories × 5 difficulty tiers, defined in `MissionDefinitions.cs`. Missions never reset. Completing a mission toggles the checkbox image on its card. No rewards yet (planned for future release).

### Notification / Popup System
`PopupManager.ShowPopup(message)` creates an icon that slides up the right side of the screen. If the icon is not tapped within 5 seconds it starts wiggling. Tapping expands it to a full message; tapping again closes it.

### Tutorial
`TutorialManager` detects first-time players (via `Player.hasSeenTutorial`) and sends 7 timed messages explaining spinning, structures, building slots, research, alien waves, defense, and prestige.

---
