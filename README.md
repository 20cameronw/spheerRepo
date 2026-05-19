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

---

## Unity Editor Setup — Interactive Placement System

The code for the building placement system (`PlacementManager`, `PlacementSlot`, slot tracking in `WorldSpawner`) is complete, but the following steps **must be done in the Unity Editor** because they involve creating assets, prefabs, and wiring scene references.

---

### Step 1 — Create the Slot Marker Prefab (blue dot)

1. In the Scene or Hierarchy, create a **Sphere** primitive (*GameObject → 3D Object → Sphere*).
2. Set its **Scale** to `(0.15, 0.15, 0.15)`.
3. Create a new **Material** (*Assets → Create → Material*), name it `SlotMarkerMat`.
   - Shader: `Universal Render Pipeline/Lit`
   - Surface Type: **Transparent**
   - Base Color: `R 0.2  G 0.5  B 1.0  A 0.75` (semi-transparent blue)
4. Assign `SlotMarkerMat` to the sphere's `MeshRenderer`.
5. Add the **`PlacementSlot`** script component to the sphere.
6. The sphere already has a `SphereCollider` — leave it enabled (it is used for raycasting).
7. Drag the sphere from the Hierarchy into `Assets/Prefabs/` to make it a prefab, name it `SlotMarker`.
8. Delete the instance from the scene.

---

### Step 2 — Create the Placement Overlay UI

This is the "Cancel" panel shown while the player is choosing a slot.

1. In the **MainGame** scene Hierarchy, select the existing Canvas.
2. Add a **Panel** child, name it `PlacementOverlay`.
   - Anchor: stretch to fill, or centre — your choice.
   - Set the Image color to semi-transparent dark (e.g. `A = 50`).
3. Inside `PlacementOverlay`, add a **Button** (*UI → Button - TextMeshPro*).
   - Label it **"Cancel"**.
   - In the Button's `OnClick()` list, add an entry → drag the `PlacementManager` GameObject → select `PlacementManager.CancelPlacement()`.
4. Disable `PlacementOverlay` in the Inspector (`active = false`) — the script enables/disables it at runtime.

---

### Step 3 — Add PlacementManager to the Scene

1. Create an **empty GameObject** in the MainGame scene, name it `PlacementManager`.
2. Add the **`PlacementManager`** script component.
3. Fill in its Inspector fields:

| Field | Value |
|-------|-------|
| **World Spawner** | drag the `WorldSpawner` GameObject |
| **Ui Manager** | drag the `UIManager` GameObject |
| **Structures Panel** | drag the `StructuresPanel` GameObject |
| **Main Camera** | drag `Main Camera` |
| **Placement Overlay UI** | drag the `PlacementOverlay` panel created in Step 2 |
| **Slot Marker Prefab** | drag the `SlotMarker` prefab from Assets/Prefabs |
| **Normal Camera Distance** | set to match your current camera's distance from the world (measure in the Scene view — typically `~15`) |
| **Placement Camera Distance** | `8` (adjustable — closer = more intimate view of the surface) |
| **Camera Zoom Duration** | `0.5` |
| **Spin Sensitivity** | `0.3` |

---

### Step 4 — Wire WorldSpawner's new field

1. Select the **WorldSpawner** GameObject in the scene.
2. In its Inspector, find the new **"Worlds List SO"** field.
3. Drag `Assets/_Scripts/ScriptableObjects/Lists/Worlds Info.asset` into that field.

---

### Step 5 — Set `maxBuildingSlots` on each World ScriptableObject

Open each world asset in `Assets/_Scripts/ScriptableObjects/World/` and set **Max Building Slots** to reflect how big the world should feel:

| World | Suggested `maxBuildingSlots` |
|-------|------------------------------|
| Lerth (starter) | 12 |
| Domny | 16 |
| Chotis | 20 |
| Purp | 24 |
| Dark | 28 |
| Saturn | 32 |
| Spiky | 36 |

---

### Step 6 — Set `slotSize` on each Upgrade ScriptableObject

Open each upgrade asset in `Assets/_Scripts/ScriptableObjects/Upgrade/` and set **Slot Size** to reflect how much space the building should occupy:

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

---

### Step 7 — Test in Play Mode

1. Hit Play, earn enough money to buy a building.
2. Tap **Buy** on any surface building card.
3. The UI panel should close, the camera should zoom in, and **blue dots** should appear on the world surface.
4. Drag your mouse (or finger) to **spin the world**.
5. Click/tap a blue dot → building spawns there, camera zooms back out.
6. Or click **Cancel** → purchase is refunded.

> **Tip:** If the blue dots don't appear, check the Console for `[PlacementManager] slotMarkerPrefab is not assigned` — it means Step 1/3 above wasn't completed.

---

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
