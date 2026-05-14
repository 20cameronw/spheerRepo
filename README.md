# Spheer — Game Design Document

## What the Game Is Right Now

Spheer is a **mobile idle/clicker game** set in space. The player owns a planet-like world and taps it to earn money ("dollars"). That money is spent in a shop to place structures on the world — drills, windmills, cell towers, satellite dishes, missile silos, lasers, gunners, and more — each providing a **passive income bonus** that ticks every second so the game keeps earning while you're away.

### Core Loop (Current)
1. **Tap the world** → earn dollars based on your current `power` stat.
2. **Buy structures** in the shop → each one adds to your passive income-per-second and physically appears on your world.
3. **Passive income** accrues every second (and a portion accrues while offline).
4. **Research upgrades** (common and epic tiers) unlock multipliers: click power, production rate, turret stats, XP rate, sell-back rate, and more.
5. **Enemy waves** (UFOs / aliens) periodically attack. Turret structures auto-shoot them; tapping an enemy manually targets it. Killing aliens grants **XP**.
6. **XP levels** gate higher-tier shop items so you can't buy everything instantly.
7. **Prestige** resets your dollars and buildings, awards **Dark Matter** (a permanent currency), which permanently boosts all future earnings.
8. **Multiple worlds** are unlockable — each is a distinct planet skin (Lerth, Domny, Chotis, Purp, Dark, Saturn, Spiky…).

### Tech Stack
- **Unity (URP)** — 3D mobile game targeting iOS/Android
- **LeanTween** for UI animation
- **Unity Ads** (banner + interstitial + rewarded)
- **JSON file-based save system** with offline-earnings calculation

---

## What's Not Working / Why It Feels Dull

- Tapping the world generates money but there's **no meaningful decision** — you just tap until you can buy the next thing in a linear list.
- Structures appear on the world but the **world doesn't feel like yours** — placement is automatic with no strategy.
- Alien waves happen whether you engage or not; **combat is passive**.
- Progression is fast and numbers just get bigger — **no tension or risk of losing**.
- Nothing social or competitive to keep you coming back.

---

## Pivot Vision — Spheer 2.0: *Clash of Worlds*

The goal is to transform Spheer from a mindless idle tapper into a **strategic base-building / tower-defense / raiding game** with a fun space/alien twist — think *Clash of Clans* in orbit.

### Big Ideas

#### 1. Your World = Your Base
- The planet surface becomes a **grid-based build zone** you actually design.
- Resources (Stardust, Energy Crystals, Dark Matter) are mined by structures you **place strategically**.
- Buildings have health — they can be **destroyed by raiders**.

#### 2. Meaningful Economy (Slow the Progression)
- Remove instant-buy; resources take real time to accumulate.
- Each upgrade tier should feel like an **achievement**, not a routine click.
- Introduce a **builder queue** (like Clash) so you can only upgrade one or two things at a time.

#### 3. Tower Defense Layer
- When aliens (or rival players) attack, **your layout matters** — walls, turret placement, and resource vault positioning determine if you survive.
- Waves scale and enemies adapt over time (fliers, tanks, EMP drones that disable turrets).

#### 4. Raiding / Multiplayer
- **Attack other players' worlds** for resources. Design your strike force from alien unit types you've unlocked.
- **Defense replays** let you watch how your world was attacked while you were offline.
- A **trophy / league system** matches you against similarly-sized worlds.

#### 5. The Fun Twist — *Alien DNA*
- Every enemy you kill drops **Alien DNA fragments**.
- You can **splice DNA** into your own defenses to create hybrid alien-tech buildings (e.g., a turret that fires corrosive slime, a wall that regenerates, a drill that spawns mini-aliens to fight for you).
- This creates a unique meta-game loop: let some enemies through on purpose to farm rare DNA.

#### 6. Rewarding Moments
- **Seasonal events** (meteor showers, alien invasions, rogue black holes) with limited-time rewards.
- Visual **world-level-up animations** when you unlock a new planet tier.
- Satisfying **destruction physics** when buildings get hit.
- A short **cinematic intro** per raid.

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

## Suggested Restructure Roadmap

| Phase | Focus |
|-------|-------|
| 1 — Foundation | Redesign world as a grid; add drag-to-place buildings; slow resource rates |
| 2 — Combat | Rework enemy pathfinding around your base layout; add wall/obstacle buildings |
| 3 — DNA System | Add DNA drops, splicing UI, and hybrid building types |
| 4 — Multiplayer | Cloud saves, player profiles, matchmaking, attack replays |
| 5 — Polish | Seasons, events, leaderboards, monetization rework |

---

## Repository Structure

```
SpheerURP/
  Assets/
    _Scripts/
      Monobehaviors/       # Core gameplay: Player, EnemySpawner, Turret, Bullet, etc.
      ScriptableObjects/   # Data: Upgrade, Research, World, Enemy Waves
      SaveSystem/          # JSON save/load + PlayerData
    Prefabs/               # 3D world objects (buildings, enemies, UI cards)
    Scenes/                # MainGame + PhotoBooth
    Sounds/                # Audio clips
    Models/                # 3D assets
```
