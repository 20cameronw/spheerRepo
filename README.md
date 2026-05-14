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
