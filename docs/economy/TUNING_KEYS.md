# Tuning keys (606)

Every number of the economy is one key of the single `Tuning` object (string key -> float with default, min, max). Keys that should be exposed through Remote Config are marked in INTEGRATION.md; the rest are design-time knobs. Digest of the defaults: `f7b3888e49b20760`.

| group | keys |
|---|---|
| value | 23 |
| time | 82 |
| yield | 30 |
| cost | 185 |
| tool | 14 |
| upgrade | 107 |
| start | 5 |
| travel | 17 |
| wave | 19 |
| score | 15 |
| obstacle | 39 |
| shard | 45 |
| sim | 25 |

## value

| key | default | range | meaning |
|---|---|---|---|
| `value.premium.refinery` | 0.1 | 0 .. 1 | Value added by a Refinery recipe (output price = input price x (1 + premium)). |
| `value.premium.foundry` | 0.2 | 0 .. 1 | Value added by a Foundry recipe. |
| `value.premium.fabricator` | 0.25 | 0 .. 1 | Value added by a Fabricator recipe. |
| `value.premium.chem_reactor` | 0.3 | 0 .. 1 | Value added by a Chem Reactor recipe. |
| `value.premium.quantum_lab` | 0.35 | 0 .. 1 | Value added by a Quantum Lab recipe. |
| `value.max_ratio.base` | 1.25 | 1 .. 3 | Conservation limit for tier 0 recipes: value out / value in (bonus recipes are exempt). |
| `value.max_ratio.per_tier` | 0.03 | 0 .. 0.2 | Conservation limit grows by this per recipe tier. |
| `value.min_ratio` | 0.2 | 0 .. 1 | Lowest allowed value out / value in (lossy recipes such as the scrap burner stay above this). |
| `value.raw.wood` | 1 | 0.1 .. 100000 | Anchor price of Wood in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.stone` | 1 | 0.1 .. 100000 | Anchor price of Stone in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.scrap` | 1 | 0.1 .. 100000 | Anchor price of Scrap in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.biomass` | 2 | 0.1 .. 100000 | Anchor price of Biomass in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.iron` | 2.5 | 0.1 .. 100000 | Anchor price of Iron Ore in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.copper_ore` | 3 | 0.1 .. 100000 | Anchor price of Copper Ore in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.silica` | 2 | 0.1 .. 100000 | Anchor price of Silica in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.ice` | 4 | 0.1 .. 100000 | Anchor price of Ice in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.titanium_ore` | 8 | 0.1 .. 100000 | Anchor price of Titanium Ore in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.crystal` | 8 | 0.1 .. 100000 | Anchor price of Crystal in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.methane` | 8 | 0.1 .. 100000 | Anchor price of Methane in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.helium3` | 22 | 0.1 .. 100000 | Anchor price of Helium-3 in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.core` | 45 | 0.1 .. 100000 | Anchor price of Xeno Core in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.radium` | 55 | 0.1 .. 100000 | Anchor price of Radium in scrap-equivalents (crafted prices derive from recipes). |
| `value.raw.dark_matter` | 130 | 0.1 .. 100000 | Anchor price of Dark Matter in scrap-equivalents (crafted prices derive from recipes). |

## time

| key | default | range | meaning |
|---|---|---|---|
| `time.anchor.0` | 0 | 0 .. 604800 | Seconds of a job at level index 0 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.1` | 5 | 0 .. 604800 | Seconds of a job at level index 1 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.2` | 20 | 0 .. 604800 | Seconds of a job at level index 2 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.3` | 60 | 0 .. 604800 | Seconds of a job at level index 3 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.4` | 180 | 0 .. 604800 | Seconds of a job at level index 4 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.5` | 600 | 0 .. 604800 | Seconds of a job at level index 5 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.6` | 1800 | 0 .. 604800 | Seconds of a job at level index 6 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.7` | 3600 | 0 .. 604800 | Seconds of a job at level index 7 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.8` | 10800 | 0 .. 604800 | Seconds of a job at level index 8 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.9` | 28800 | 0 .. 604800 | Seconds of a job at level index 9 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.10` | 57600 | 0 .. 604800 | Seconds of a job at level index 10 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.anchor.11` | 86400 | 0 .. 604800 | Seconds of a job at level index 11 of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically. |
| `time.cap_seconds` | 86400 | 3600 .. 604800 | Hard cap for ordinary build/upgrade jobs (about one day). |
| `time.mega.stage1` | 172800 | 3600 .. 2592000 | Seconds of megaproject stage 1 (before the per-project multiplier). |
| `time.mega.stage2` | 259200 | 3600 .. 2592000 | Seconds of megaproject stage 2 (before the per-project multiplier). |
| `time.mega.stage3` | 345600 | 3600 .. 2592000 | Seconds of megaproject stage 3 (before the per-project multiplier). |
| `time.mega.stage4` | 432000 | 3600 .. 2592000 | Seconds of megaproject stage 4 (before the per-project multiplier). |
| `time.mega.stage5` | 604800 | 3600 .. 2592000 | Seconds of megaproject stage 5 (before the per-project multiplier). |
| `time.mega_cap_seconds` | 864000 | 86400 .. 2592000 | Hard cap for a megaproject stage. |
| `time.scale` | 1 | 0.1 .. 10 | Global multiplier on every structure job time (1 = as designed). |
| `time.start.lumber` | 0 | 0 .. 11 | Level index of Lumber Camp level 1 on the shared time curve. |
| `time.step.lumber` | 0.8 | 0 .. 3 | Level index added per level for Lumber Camp. |
| `time.start.mine` | 0 | 0 .. 11 | Level index of Mining Rig level 1 on the shared time curve. |
| `time.step.mine` | 0.8 | 0 .. 3 | Level index added per level for Mining Rig. |
| `time.start.driller` | 0.6 | 0 .. 11 | Level index of Deep Driller level 1 on the shared time curve. |
| `time.step.driller` | 0.85 | 0 .. 3 | Level index added per level for Deep Driller. |
| `time.start.biodome` | 0.8 | 0 .. 11 | Level index of Biodome Harvester level 1 on the shared time curve. |
| `time.step.biodome` | 0.85 | 0 .. 3 | Level index added per level for Biodome Harvester. |
| `time.start.siphon_rig` | 2 | 0 .. 11 | Level index of Gas Siphon Rig level 1 on the shared time curve. |
| `time.step.siphon_rig` | 0.9 | 0 .. 3 | Level index added per level for Gas Siphon Rig. |
| `time.start.excavator` | 2 | 0 .. 11 | Level index of Excavation Rig level 1 on the shared time curve. |
| `time.step.excavator` | 0.9 | 0 .. 3 | Level index added per level for Excavation Rig. |
| `time.start.salvage_yard` | 2 | 0 .. 11 | Level index of Salvage Yard level 1 on the shared time curve. |
| `time.step.salvage_yard` | 0.9 | 0 .. 3 | Level index added per level for Salvage Yard. |
| `time.start.refinery` | 0 | 0 .. 11 | Level index of Refinery level 1 on the shared time curve. |
| `time.step.refinery` | 0.85 | 0 .. 3 | Level index added per level for Refinery. |
| `time.start.fabricator` | 0.8 | 0 .. 11 | Level index of Fabricator level 1 on the shared time curve. |
| `time.step.fabricator` | 0.9 | 0 .. 3 | Level index added per level for Fabricator. |
| `time.start.foundry` | 0.8 | 0 .. 11 | Level index of Foundry level 1 on the shared time curve. |
| `time.step.foundry` | 0.9 | 0 .. 3 | Level index added per level for Foundry. |
| `time.start.chem_reactor` | 3.5 | 0 .. 11 | Level index of Chem Reactor level 1 on the shared time curve. |
| `time.step.chem_reactor` | 0.9 | 0 .. 3 | Level index added per level for Chem Reactor. |
| `time.start.quantum_lab` | 4.5 | 0 .. 11 | Level index of Quantum Lab level 1 on the shared time curve. |
| `time.step.quantum_lab` | 0.9 | 0 .. 3 | Level index added per level for Quantum Lab. |
| `time.start.warehouse` | 0 | 0 .. 11 | Level index of Warehouse level 1 on the shared time curve. |
| `time.step.warehouse` | 0.8 | 0 .. 3 | Level index added per level for Warehouse. |
| `time.start.fuel_depot` | 2 | 0 .. 11 | Level index of Fuel Depot level 1 on the shared time curve. |
| `time.step.fuel_depot` | 0.85 | 0 .. 3 | Level index added per level for Fuel Depot. |
| `time.start.cargo_depot` | 2.5 | 0 .. 11 | Level index of Cargo Depot level 1 on the shared time curve. |
| `time.step.cargo_depot` | 0.9 | 0 .. 3 | Level index added per level for Cargo Depot. |
| `time.start.orbital_relay` | 4.5 | 0 .. 11 | Level index of Orbital Relay level 1 on the shared time curve. |
| `time.step.orbital_relay` | 0.9 | 0 .. 3 | Level index added per level for Orbital Relay. |
| `time.start.gunner` | 0 | 0 .. 11 | Level index of Gunner Turret level 1 on the shared time curve. |
| `time.step.gunner` | 0.8 | 0 .. 3 | Level index added per level for Gunner Turret. |
| `time.start.laser` | 2 | 0 .. 11 | Level index of Laser Turret level 1 on the shared time curve. |
| `time.step.laser` | 0.9 | 0 .. 3 | Level index added per level for Laser Turret. |
| `time.start.shield_pylon` | 2 | 0 .. 11 | Level index of Shield Pylon level 1 on the shared time curve. |
| `time.step.shield_pylon` | 0.9 | 0 .. 3 | Level index added per level for Shield Pylon. |
| `time.start.missile_silo` | 3.5 | 0 .. 11 | Level index of Missile Silo level 1 on the shared time curve. |
| `time.step.missile_silo` | 0.9 | 0 .. 3 | Level index added per level for Missile Silo. |
| `time.start.tesla_coil` | 3.5 | 0 .. 11 | Level index of Tesla Coil level 1 on the shared time curve. |
| `time.step.tesla_coil` | 0.9 | 0 .. 3 | Level index added per level for Tesla Coil. |
| `time.start.repair_bay` | 3.5 | 0 .. 11 | Level index of Repair Bay level 1 on the shared time curve. |
| `time.step.repair_bay` | 0.9 | 0 .. 3 | Level index added per level for Repair Bay. |
| `time.start.alarm` | 0 | 0 .. 11 | Level index of Alarm Beacon level 1 on the shared time curve. |
| `time.step.alarm` | 0.8 | 0 .. 3 | Level index added per level for Alarm Beacon. |
| `time.start.research_lab` | 1 | 0 .. 11 | Level index of Research Lab level 1 on the shared time curve. |
| `time.step.research_lab` | 0.95 | 0 .. 3 | Level index added per level for Research Lab. |
| `time.start.radar_tower` | 1 | 0 .. 11 | Level index of Radar Tower level 1 on the shared time curve. |
| `time.step.radar_tower` | 0.85 | 0 .. 3 | Level index added per level for Radar Tower. |
| `time.start.colony_hub` | 3.5 | 0 .. 11 | Level index of Colony Hub level 1 on the shared time curve. |
| `time.step.colony_hub` | 0.95 | 0 .. 3 | Level index added per level for Colony Hub. |
| `time.start.hangar` | 4.5 | 0 .. 11 | Level index of Hangar level 1 on the shared time curve. |
| `time.step.hangar` | 0.9 | 0 .. 3 | Level index added per level for Hangar. |
| `time.start.auto_harvester` | 4.5 | 0 .. 11 | Level index of Auto-Harvester level 1 on the shared time curve. |
| `time.step.auto_harvester` | 0.9 | 0 .. 3 | Level index added per level for Auto-Harvester. |
| `time.start.terraformer` | 0 | 0 .. 11 | Level index of Terraformer level 1 on the shared time curve. |
| `time.step.terraformer` | 0.85 | 0 .. 3 | Level index added per level for Terraformer. |
| `time.start.warp_gate` | 0 | 0 .. 11 | Level index of Warp Gate level 1 on the shared time curve. |
| `time.step.warp_gate` | 0.85 | 0 .. 3 | Level index added per level for Warp Gate. |
| `time.start.dyson_collector` | 0 | 0 .. 11 | Level index of Dyson Collector level 1 on the shared time curve. |
| `time.step.dyson_collector` | 0.85 | 0 .. 3 | Level index added per level for Dyson Collector. |

## yield

| key | default | range | meaning |
|---|---|---|---|
| `yield.base.wood` | 10 | 0.5 .. 1000 | Deposit base yield of Wood at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.wood` | 0 | 0 .. 100 | Extra base yield of Wood per planet tier. |
| `yield.base.stone` | 8 | 0.5 .. 1000 | Deposit base yield of Stone at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.stone` | 0 | 0 .. 100 | Extra base yield of Stone per planet tier. |
| `yield.base.scrap` | 10 | 0.5 .. 1000 | Deposit base yield of Scrap at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.scrap` | 0 | 0 .. 100 | Extra base yield of Scrap per planet tier. |
| `yield.base.biomass` | 9 | 0.5 .. 1000 | Deposit base yield of Biomass at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.biomass` | 0.5 | 0 .. 100 | Extra base yield of Biomass per planet tier. |
| `yield.base.iron` | 7 | 0.5 .. 1000 | Deposit base yield of Iron Ore at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.iron` | 1 | 0 .. 100 | Extra base yield of Iron Ore per planet tier. |
| `yield.base.copper_ore` | 6 | 0.5 .. 1000 | Deposit base yield of Copper Ore at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.copper_ore` | 0.8 | 0 .. 100 | Extra base yield of Copper Ore per planet tier. |
| `yield.base.silica` | 8 | 0.5 .. 1000 | Deposit base yield of Silica at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.silica` | 0.6 | 0 .. 100 | Extra base yield of Silica per planet tier. |
| `yield.base.ice` | 8 | 0.5 .. 1000 | Deposit base yield of Ice at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.ice` | 0.8 | 0 .. 100 | Extra base yield of Ice per planet tier. |
| `yield.base.titanium_ore` | 5 | 0.5 .. 1000 | Deposit base yield of Titanium Ore at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.titanium_ore` | 0.7 | 0 .. 100 | Extra base yield of Titanium Ore per planet tier. |
| `yield.base.crystal` | 5 | 0.5 .. 1000 | Deposit base yield of Crystal at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.crystal` | 0.8 | 0 .. 100 | Extra base yield of Crystal per planet tier. |
| `yield.base.methane` | 8 | 0.5 .. 1000 | Deposit base yield of Methane at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.methane` | 0.8 | 0 .. 100 | Extra base yield of Methane per planet tier. |
| `yield.base.helium3` | 4 | 0.5 .. 1000 | Deposit base yield of Helium-3 at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.helium3` | 0.6 | 0 .. 100 | Extra base yield of Helium-3 per planet tier. |
| `yield.base.core` | 2 | 0.5 .. 1000 | Deposit base yield of Xeno Core at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.core` | 0.3 | 0 .. 100 | Extra base yield of Xeno Core per planet tier. |
| `yield.base.radium` | 3 | 0.5 .. 1000 | Deposit base yield of Radium at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.radium` | 0.4 | 0 .. 100 | Extra base yield of Radium per planet tier. |
| `yield.base.dark_matter` | 2 | 0.5 .. 1000 | Deposit base yield of Dark Matter at tier 0 (before richness, tool and structure bonuses). |
| `yield.per_tier.dark_matter` | 0.25 | 0 .. 100 | Extra base yield of Dark Matter per planet tier. |

## cost

| key | default | range | meaning |
|---|---|---|---|
| `cost.scale.structure` | 1 | 0.05 .. 20 | Global multiplier on every structure value budget. |
| `cost.scale.tool` | 1 | 0.05 .. 20 | Global multiplier on tool ladder value budgets. |
| `cost.scale.ship` | 1 | 0.05 .. 20 | Global multiplier on ship and logistics upgrade value budgets. |
| `cost.scale.defense` | 1 | 0.05 .. 20 | Global multiplier on defense upgrade value budgets. |
| `cost.scale.research` | 1 | 0.05 .. 20 | Global multiplier on research value budgets. |
| `cost.scale.mega` | 0.7 | 0.05 .. 20 | Global multiplier on megaproject stage budgets (tuned by the simulator: late stages are weeks of play, not months). |
| `cost.era.0` | 1 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 0 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.1` | 1.8 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 1 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.2` | 3.2 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 2 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.3` | 1.8 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 3 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.4` | 1.2 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 4 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.5` | 0.9 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 5 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.6` | 0.8 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 6 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.7` | 0.7 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 7 (the era pacing knob; tuned by the simulator against the era bands). |
| `cost.era.8` | 0.7 | 0.05 .. 20 | Multiplier on every value budget whose item belongs to tier 8 (the era pacing knob; tuned by the simulator against the era bands). |
| `level.reach_step` | 0.5 | 0 .. 2 | A structure level needs reach tier = unlock tier + floor((level - 1) x step): upgrades reward going outward. |
| `cost.cap.base_slots` | 12 | 1 .. 1000 | No single line of a non-megaproject cost may exceed this many slots of its resource at tier 0 (so a base can hold what a purchase asks). |
| `cost.cap.slots_per_tier` | 14 | 0 .. 200 | Extra slots allowed per line for every tier of the purchase's reach requirement. |
| `cost.v.lumber` | 6 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Lumber Camp level 1. |
| `cost.g.lumber` | 1.7 | 1 .. 5 | Geometric cost growth per level of Lumber Camp. |
| `cost.v.mine` | 0 | 0 .. 1000000000 | Mining Rig level 1 is free (set above 0 to charge for it). |
| `cost.v2.mine` | 8 | 0 .. 1000000000 | Value budget of Mining Rig level 2 (the first paid level). |
| `cost.g.mine` | 1.7 | 1 .. 5 | Geometric cost growth per level of Mining Rig. |
| `cost.v.driller` | 33 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Deep Driller level 1. |
| `cost.g.driller` | 1.7 | 1 .. 5 | Geometric cost growth per level of Deep Driller. |
| `cost.v.biodome` | 60 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Biodome Harvester level 1. |
| `cost.g.biodome` | 1.7 | 1 .. 5 | Geometric cost growth per level of Biodome Harvester. |
| `cost.v.siphon_rig` | 700 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Gas Siphon Rig level 1. |
| `cost.g.siphon_rig` | 1.7 | 1 .. 5 | Geometric cost growth per level of Gas Siphon Rig. |
| `cost.v.excavator` | 700 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Excavation Rig level 1. |
| `cost.g.excavator` | 1.7 | 1 .. 5 | Geometric cost growth per level of Excavation Rig. |
| `cost.v.salvage_yard` | 650 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Salvage Yard level 1. |
| `cost.g.salvage_yard` | 1.7 | 1 .. 5 | Geometric cost growth per level of Salvage Yard. |
| `cost.v.refinery` | 15 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Refinery level 1. |
| `cost.g.refinery` | 1.75 | 1 .. 5 | Geometric cost growth per level of Refinery. |
| `cost.v.fabricator` | 70 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Fabricator level 1. |
| `cost.g.fabricator` | 1.75 | 1 .. 5 | Geometric cost growth per level of Fabricator. |
| `cost.v.foundry` | 90 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Foundry level 1. |
| `cost.g.foundry` | 1.75 | 1 .. 5 | Geometric cost growth per level of Foundry. |
| `cost.v.chem_reactor` | 4500 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Chem Reactor level 1. |
| `cost.g.chem_reactor` | 1.75 | 1 .. 5 | Geometric cost growth per level of Chem Reactor. |
| `cost.v.quantum_lab` | 30000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Quantum Lab level 1. |
| `cost.g.quantum_lab` | 1.68 | 1 .. 5 | Geometric cost growth per level of Quantum Lab. |
| `cost.v.warehouse` | 30 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Warehouse level 1. |
| `cost.g.warehouse` | 1.6 | 1 .. 5 | Geometric cost growth per level of Warehouse. |
| `cost.v.fuel_depot` | 600 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Fuel Depot level 1. |
| `cost.g.fuel_depot` | 1.65 | 1 .. 5 | Geometric cost growth per level of Fuel Depot. |
| `cost.v.cargo_depot` | 900 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Cargo Depot level 1. |
| `cost.g.cargo_depot` | 1.65 | 1 .. 5 | Geometric cost growth per level of Cargo Depot. |
| `cost.v.orbital_relay` | 25000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Orbital Relay level 1. |
| `cost.g.orbital_relay` | 1.7 | 1 .. 5 | Geometric cost growth per level of Orbital Relay. |
| `cost.v.gunner` | 29 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Gunner Turret level 1. |
| `cost.g.gunner` | 1.75 | 1 .. 5 | Geometric cost growth per level of Gunner Turret. |
| `cost.v.laser` | 800 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Laser Turret level 1. |
| `cost.g.laser` | 1.75 | 1 .. 5 | Geometric cost growth per level of Laser Turret. |
| `cost.v.shield_pylon` | 700 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Shield Pylon level 1. |
| `cost.g.shield_pylon` | 1.75 | 1 .. 5 | Geometric cost growth per level of Shield Pylon. |
| `cost.v.missile_silo` | 4200 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Missile Silo level 1. |
| `cost.g.missile_silo` | 1.75 | 1 .. 5 | Geometric cost growth per level of Missile Silo. |
| `cost.v.tesla_coil` | 4000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Tesla Coil level 1. |
| `cost.g.tesla_coil` | 1.75 | 1 .. 5 | Geometric cost growth per level of Tesla Coil. |
| `cost.v.repair_bay` | 3800 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Repair Bay level 1. |
| `cost.g.repair_bay` | 1.75 | 1 .. 5 | Geometric cost growth per level of Repair Bay. |
| `cost.v.alarm` | 12 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Alarm Beacon level 1. |
| `cost.g.alarm` | 1.7 | 1 .. 5 | Geometric cost growth per level of Alarm Beacon. |
| `cost.v.research_lab` | 80 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Research Lab level 1. |
| `cost.g.research_lab` | 1.8 | 1 .. 5 | Geometric cost growth per level of Research Lab. |
| `cost.v.radar_tower` | 60 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Radar Tower level 1. |
| `cost.g.radar_tower` | 1.7 | 1 .. 5 | Geometric cost growth per level of Radar Tower. |
| `cost.v.colony_hub` | 6000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Colony Hub level 1. |
| `cost.g.colony_hub` | 1.8 | 1 .. 5 | Geometric cost growth per level of Colony Hub. |
| `cost.v.hangar` | 35000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Hangar level 1. |
| `cost.g.hangar` | 1.75 | 1 .. 5 | Geometric cost growth per level of Hangar. |
| `cost.v.auto_harvester` | 28000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Auto-Harvester level 1. |
| `cost.g.auto_harvester` | 1.75 | 1 .. 5 | Geometric cost growth per level of Auto-Harvester. |
| `cost.v.terraformer` | 600000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Terraformer level 1 (stage 1). |
| `cost.g.terraformer` | 1.9 | 1 .. 5 | Geometric cost growth per level of Terraformer. |
| `cost.v.warp_gate` | 1500000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Warp Gate level 1 (stage 1). |
| `cost.g.warp_gate` | 1.9 | 1 .. 5 | Geometric cost growth per level of Warp Gate. |
| `cost.v.dyson_collector` | 4000000 | 0 .. 1000000000 | Value budget (scrap-equivalents) of Dyson Collector level 1 (stage 1). |
| `cost.g.dyson_collector` | 1.9 | 1 .. 5 | Geometric cost growth per level of Dyson Collector. |
| `cost.v.axe` | 40 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Axe ladder. |
| `cost.g.axe` | 4.6 | 1 .. 20 | Value growth per tool tier of the Axe ladder. |
| `cost.v.pick` | 40 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Pickaxe ladder. |
| `cost.g.pick` | 4.6 | 1 .. 20 | Value growth per tool tier of the Pickaxe ladder. |
| `cost.v.claw` | 60 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Salvage Claw ladder. |
| `cost.g.claw` | 4.6 | 1 .. 20 | Value growth per tool tier of the Salvage Claw ladder. |
| `cost.v.drill` | 80 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Drill ladder. |
| `cost.g.drill` | 4.7 | 1 .. 20 | Value growth per tool tier of the Drill ladder. |
| `cost.v.siphon` | 900 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Siphon ladder. |
| `cost.g.siphon` | 4.6 | 1 .. 20 | Value growth per tool tier of the Siphon ladder. |
| `cost.v.scanner` | 900 | 0 .. 1000000000 | Value budget of the first purchasable tier (tier 2) of the Excavator ladder. |
| `cost.g.scanner` | 4.6 | 1 .. 20 | Value growth per tool tier of the Excavator ladder. |
| `cost.v.pack` | 60 | 0 .. 1000000000 | Value budget of Backpack level 1. |
| `cost.g.pack` | 3 | 1 .. 100 | Value growth per level of Backpack. |
| `cost.v.engine` | 50 | 0 .. 1000000000 | Value budget of Engine Efficiency level 1. |
| `cost.g.engine` | 3 | 1 .. 100 | Value growth per level of Engine Efficiency. |
| `cost.v.jump` | 150 | 0 .. 1000000000 | Value budget of Jump Range level 1. |
| `cost.g.jump` | 3.1 | 1 .. 100 | Value growth per level of Jump Range. |
| `cost.v.scanner_range` | 100 | 0 .. 1000000000 | Value budget of Scanner Range level 1. |
| `cost.g.scanner_range` | 3.2 | 1 .. 100 | Value growth per level of Scanner Range. |
| `cost.v.fuel_tank` | 60 | 0 .. 1000000000 | Value budget of Fuel Tank level 1. |
| `cost.g.fuel_tank` | 3.3 | 1 .. 100 | Value growth per level of Fuel Tank. |
| `cost.v.hull_toxic` | 900 | 0 .. 1000000000 | Value budget of Toxic Seal level 1. |
| `cost.g.hull_toxic` | 24 | 1 .. 100 | Value growth per level of Toxic Seal. |
| `cost.v.hull_heat` | 6000 | 0 .. 1000000000 | Value budget of Thermal Plating level 1. |
| `cost.g.hull_heat` | 14 | 1 .. 100 | Value growth per level of Thermal Plating. |
| `cost.v.hull_cold` | 6000 | 0 .. 1000000000 | Value budget of Cryo Plating level 1. |
| `cost.g.hull_cold` | 14 | 1 .. 100 | Value growth per level of Cryo Plating. |
| `cost.v.hull_rad` | 40000 | 0 .. 1000000000 | Value budget of Rad-Shield Plating level 1. |
| `cost.g.hull_rad` | 12 | 1 .. 100 | Value growth per level of Rad-Shield Plating. |
| `cost.v.hull_void` | 500000 | 0 .. 1000000000 | Value budget of Void Shell level 1. |
| `cost.g.hull_void` | 8 | 1 .. 100 | Value growth per level of Void Shell. |
| `cost.v.turret` | 27 | 0 .. 1000000000 | Value budget of Turret Actuators level 1. |
| `cost.g.turret` | 2.2 | 1 .. 100 | Value growth per level of Turret Actuators. |
| `cost.v.turretdmg` | 33 | 0 .. 1000000000 | Value budget of Turret Damage level 1. |
| `cost.g.turretdmg` | 2.2 | 1 .. 100 | Value growth per level of Turret Damage. |
| `cost.v.pulse` | 28 | 0 .. 1000000000 | Value budget of Ion Pulse level 1. |
| `cost.g.pulse` | 2.2 | 1 .. 100 | Value growth per level of Ion Pulse. |
| `cost.v.shield` | 27 | 0 .. 1000000000 | Value budget of Reactor Shield level 1. |
| `cost.g.shield` | 2.2 | 1 .. 100 | Value growth per level of Reactor Shield. |
| `cost.v.salvage` | 54 | 0 .. 1000000000 | Value budget of Salvage Rig level 1. |
| `cost.g.salvage` | 2.2 | 1 .. 100 | Value growth per level of Salvage Rig. |
| `cost.v.compression` | 18000 | 0 .. 1000000000 | Value budget of Cargo Compression level 1. |
| `cost.g.compression` | 5.5 | 1 .. 100 | Value growth per level of Cargo Compression. |
| `cost.v.survey_basics` | 60 | 0 .. 1000000000 | Value budget of research node Survey Basics. |
| `cost.v.smelting` | 90 | 0 .. 1000000000 | Value budget of research node Smelting. |
| `cost.v.fabrication` | 90 | 0 .. 1000000000 | Value budget of research node Fabrication. |
| `cost.v.deep_drilling` | 70 | 0 .. 1000000000 | Value budget of research node Deep Drilling. |
| `cost.v.bio_cultivation` | 70 | 0 .. 1000000000 | Value budget of research node Bio-Cultivation. |
| `cost.v.jump_drive_1` | 100 | 0 .. 1000000000 | Value budget of research node Jump Drive I. |
| `cost.v.steelmaking` | 500 | 0 .. 1000000000 | Value budget of research node Steelmaking. |
| `cost.v.composites` | 550 | 0 .. 1000000000 | Value budget of research node Composites. |
| `cost.v.circuitry` | 550 | 0 .. 1000000000 | Value budget of research node Circuitry. |
| `cost.v.titanium_working` | 700 | 0 .. 1000000000 | Value budget of research node Titanium Working. |
| `cost.v.gas_extraction` | 650 | 0 .. 1000000000 | Value budget of research node Gas Extraction. |
| `cost.v.geo_survey` | 650 | 0 .. 1000000000 | Value budget of research node Geo-Survey. |
| `cost.v.salvage_ops` | 550 | 0 .. 1000000000 | Value budget of research node Salvage Operations. |
| `cost.v.fuel_logistics` | 550 | 0 .. 1000000000 | Value budget of research node Fuel Logistics. |
| `cost.v.cargo_logistics` | 700 | 0 .. 1000000000 | Value budget of research node Cargo Logistics. |
| `cost.v.ballistics` | 600 | 0 .. 1000000000 | Value budget of research node Ballistics. |
| `cost.v.shield_tech` | 600 | 0 .. 1000000000 | Value budget of research node Shield Tech. |
| `cost.v.hazmat_toxic` | 800 | 0 .. 1000000000 | Value budget of research node Hazmat: Toxic. |
| `cost.v.jump_drive_2` | 500 | 0 .. 1000000000 | Value budget of research node Jump Drive II. |
| `cost.v.field_engineering` | 600 | 0 .. 1000000000 | Value budget of research node Field Engineering. |
| `cost.v.long_range_scanning_1` | 450 | 0 .. 1000000000 | Value budget of research node Long-Range Scanning I. |
| `cost.v.toolsmithing_1` | 650 | 0 .. 1000000000 | Value budget of research node Toolsmithing I. |
| `cost.v.chemistry` | 3500 | 0 .. 1000000000 | Value budget of research node Chemistry. |
| `cost.v.thermal_metallurgy` | 3500 | 0 .. 1000000000 | Value budget of research node Thermal Metallurgy. |
| `cost.v.cryo_chemistry` | 3500 | 0 .. 1000000000 | Value budget of research node Cryo-Chemistry. |
| `cost.v.robotics` | 3200 | 0 .. 1000000000 | Value budget of research node Robotics. |
| `cost.v.optics` | 3200 | 0 .. 1000000000 | Value budget of research node Optics. |
| `cost.v.hazmat_heat` | 4000 | 0 .. 1000000000 | Value budget of research node Hazmat: Heat. |
| `cost.v.hazmat_cold` | 4000 | 0 .. 1000000000 | Value budget of research node Hazmat: Cold. |
| `cost.v.missile_tech` | 3600 | 0 .. 1000000000 | Value budget of research node Missile Tech. |
| `cost.v.tesla_tech` | 3600 | 0 .. 1000000000 | Value budget of research node Tesla Tech. |
| `cost.v.field_repair` | 3400 | 0 .. 1000000000 | Value budget of research node Field Repair. |
| `cost.v.colony_charter` | 4500 | 0 .. 1000000000 | Value budget of research node Colony Charter. |
| `cost.v.jump_drive_3` | 3000 | 0 .. 1000000000 | Value budget of research node Jump Drive III. |
| `cost.v.nav_autopilot` | 2800 | 0 .. 1000000000 | Value budget of research node Autopilot Navigation. |
| `cost.v.toolsmithing_2` | 3500 | 0 .. 1000000000 | Value budget of research node Toolsmithing II. |
| `cost.v.long_range_scanning_2` | 3000 | 0 .. 1000000000 | Value budget of research node Long-Range Scanning II. |
| `cost.v.plasma_physics` | 20000 | 0 .. 1000000000 | Value budget of research node Plasma Physics. |
| `cost.v.radiation_shielding` | 22000 | 0 .. 1000000000 | Value budget of research node Radiation Shielding. |
| `cost.v.quantum_computing` | 25000 | 0 .. 1000000000 | Value budget of research node Quantum Computing. |
| `cost.v.hazmat_rad` | 26000 | 0 .. 1000000000 | Value budget of research node Hazmat: Radiation. |
| `cost.v.orbital_logistics` | 24000 | 0 .. 1000000000 | Value budget of research node Orbital Logistics. |
| `cost.v.automation` | 22000 | 0 .. 1000000000 | Value budget of research node Automation. |
| `cost.v.hangar_ops` | 22000 | 0 .. 1000000000 | Value budget of research node Hangar Operations. |
| `cost.v.toolsmithing_3` | 20000 | 0 .. 1000000000 | Value budget of research node Toolsmithing III. |
| `cost.v.jump_drive_4` | 18000 | 0 .. 1000000000 | Value budget of research node Jump Drive IV. |
| `cost.v.compression_1` | 18000 | 0 .. 1000000000 | Value budget of research node Cargo Compression I. |
| `cost.v.terraforming` | 100000 | 0 .. 1000000000 | Value budget of research node Terraforming. |
| `cost.v.long_range_scanning_3` | 80000 | 0 .. 1000000000 | Value budget of research node Long-Range Scanning III. |
| `cost.v.compression_2` | 90000 | 0 .. 1000000000 | Value budget of research node Cargo Compression II. |
| `cost.v.void_forging` | 300000 | 0 .. 1000000000 | Value budget of research node Void Forging. |
| `cost.v.warp_theory` | 320000 | 0 .. 1000000000 | Value budget of research node Warp Theory. |
| `cost.v.antimatter_synthesis` | 240000 | 0 .. 1000000000 | Value budget of research node Antimatter Synthesis. |
| `cost.v.hazmat_void` | 350000 | 0 .. 1000000000 | Value budget of research node Hazmat: Void. |
| `cost.v.warp_gate_engineering` | 420000 | 0 .. 1000000000 | Value budget of research node Warp Gate Engineering. |
| `cost.v.jump_drive_5` | 220000 | 0 .. 1000000000 | Value budget of research node Jump Drive V. |
| `cost.v.toolsmithing_4` | 250000 | 0 .. 1000000000 | Value budget of research node Toolsmithing IV. |
| `cost.v.stellar_engineering` | 1500000 | 0 .. 1000000000 | Value budget of research node Stellar Engineering. |
| `cost.v.compression_3` | 700000 | 0 .. 1000000000 | Value budget of research node Cargo Compression III. |
| `cost.v.ascension_protocol` | 3000000 | 0 .. 1000000000 | Value budget of research node Ascension Protocol. |

## tool

| key | default | range | meaning |
|---|---|---|---|
| `tool.mult.1` | 1 | 1 .. 100 | Yield multiplier of tool tier 1 (all ladders). |
| `tool.runtime.1` | 1 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 1 (faster swings). |
| `tool.mult.2` | 1.6 | 1 .. 100 | Yield multiplier of tool tier 2 (all ladders). |
| `tool.runtime.2` | 0.96 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 2 (faster swings). |
| `tool.mult.3` | 2.4 | 1 .. 100 | Yield multiplier of tool tier 3 (all ladders). |
| `tool.runtime.3` | 0.92 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 3 (faster swings). |
| `tool.mult.4` | 3.6 | 1 .. 100 | Yield multiplier of tool tier 4 (all ladders). |
| `tool.runtime.4` | 0.88 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 4 (faster swings). |
| `tool.mult.5` | 5.4 | 1 .. 100 | Yield multiplier of tool tier 5 (all ladders). |
| `tool.runtime.5` | 0.84 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 5 (faster swings). |
| `tool.mult.6` | 8.2 | 1 .. 100 | Yield multiplier of tool tier 6 (all ladders). |
| `tool.runtime.6` | 0.8 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 6 (faster swings). |
| `tool.mult.7` | 12.5 | 1 .. 100 | Yield multiplier of tool tier 7 (all ladders). |
| `tool.runtime.7` | 0.76 | 0.3 .. 1.5 | Harvest run time multiplier at tool tier 7 (faster swings). |

## upgrade

| key | default | range | meaning |
|---|---|---|---|
| `upg.pack.1` | 18 | 0 .. 10000000 | Cumulative effect of Backpack at level 1 (BackpackSlots). |
| `upg.pack.2` | 27 | 0 .. 10000000 | Cumulative effect of Backpack at level 2 (BackpackSlots). |
| `upg.pack.3` | 36 | 0 .. 10000000 | Cumulative effect of Backpack at level 3 (BackpackSlots). |
| `upg.pack.4` | 45 | 0 .. 10000000 | Cumulative effect of Backpack at level 4 (BackpackSlots). |
| `upg.pack.5` | 54 | 0 .. 10000000 | Cumulative effect of Backpack at level 5 (BackpackSlots). |
| `upg.pack.6` | 63 | 0 .. 10000000 | Cumulative effect of Backpack at level 6 (BackpackSlots). |
| `upg.pack.7` | 72 | 0 .. 10000000 | Cumulative effect of Backpack at level 7 (BackpackSlots). |
| `upg.pack.8` | 81 | 0 .. 10000000 | Cumulative effect of Backpack at level 8 (BackpackSlots). |
| `upg.engine.1` | 0.92 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 1 (FuelEfficiency). |
| `upg.engine.2` | 0.85 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 2 (FuelEfficiency). |
| `upg.engine.3` | 0.78 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 3 (FuelEfficiency). |
| `upg.engine.4` | 0.72 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 4 (FuelEfficiency). |
| `upg.engine.5` | 0.66 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 5 (FuelEfficiency). |
| `upg.engine.6` | 0.61 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 6 (FuelEfficiency). |
| `upg.engine.7` | 0.56 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 7 (FuelEfficiency). |
| `upg.engine.8` | 0.52 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 8 (FuelEfficiency). |
| `upg.engine.9` | 0.48 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 9 (FuelEfficiency). |
| `upg.engine.10` | 0.45 | 0 .. 10000000 | Cumulative effect of Engine Efficiency at level 10 (FuelEfficiency). |
| `upg.jump.1` | 36 | 0 .. 10000000 | Cumulative effect of Jump Range at level 1 (JumpRange). |
| `upg.jump.2` | 55 | 0 .. 10000000 | Cumulative effect of Jump Range at level 2 (JumpRange). |
| `upg.jump.3` | 85 | 0 .. 10000000 | Cumulative effect of Jump Range at level 3 (JumpRange). |
| `upg.jump.4` | 130 | 0 .. 10000000 | Cumulative effect of Jump Range at level 4 (JumpRange). |
| `upg.jump.5` | 200 | 0 .. 10000000 | Cumulative effect of Jump Range at level 5 (JumpRange). |
| `upg.jump.6` | 310 | 0 .. 10000000 | Cumulative effect of Jump Range at level 6 (JumpRange). |
| `upg.jump.7` | 480 | 0 .. 10000000 | Cumulative effect of Jump Range at level 7 (JumpRange). |
| `upg.jump.8` | 750 | 0 .. 10000000 | Cumulative effect of Jump Range at level 8 (JumpRange). |
| `upg.jump.9` | 1200 | 0 .. 10000000 | Cumulative effect of Jump Range at level 9 (JumpRange). |
| `upg.jump.10` | 2000 | 0 .. 10000000 | Cumulative effect of Jump Range at level 10 (JumpRange). |
| `upg.scanner_range.1` | 60 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 1 (ScannerRange). |
| `upg.scanner_range.2` | 110 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 2 (ScannerRange). |
| `upg.scanner_range.3` | 180 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 3 (ScannerRange). |
| `upg.scanner_range.4` | 300 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 4 (ScannerRange). |
| `upg.scanner_range.5` | 500 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 5 (ScannerRange). |
| `upg.scanner_range.6` | 800 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 6 (ScannerRange). |
| `upg.scanner_range.7` | 1300 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 7 (ScannerRange). |
| `upg.scanner_range.8` | 2200 | 0 .. 10000000 | Cumulative effect of Scanner Range at level 8 (ScannerRange). |
| `upg.fuel_tank.1` | 2 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 1 (FuelTankSlots). |
| `upg.fuel_tank.2` | 4 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 2 (FuelTankSlots). |
| `upg.fuel_tank.3` | 7 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 3 (FuelTankSlots). |
| `upg.fuel_tank.4` | 10 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 4 (FuelTankSlots). |
| `upg.fuel_tank.5` | 14 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 5 (FuelTankSlots). |
| `upg.fuel_tank.6` | 18 | 0 .. 10000000 | Cumulative effect of Fuel Tank at level 6 (FuelTankSlots). |
| `upg.hull_toxic.1` | 1 | 0 .. 10000000 | Cumulative effect of Toxic Seal at level 1 (HullPlating). |
| `upg.hull_toxic.2` | 2 | 0 .. 10000000 | Cumulative effect of Toxic Seal at level 2 (HullPlating). |
| `upg.hull_toxic.3` | 3 | 0 .. 10000000 | Cumulative effect of Toxic Seal at level 3 (HullPlating). |
| `upg.hull_heat.1` | 1 | 0 .. 10000000 | Cumulative effect of Thermal Plating at level 1 (HullPlating). |
| `upg.hull_heat.2` | 2 | 0 .. 10000000 | Cumulative effect of Thermal Plating at level 2 (HullPlating). |
| `upg.hull_heat.3` | 3 | 0 .. 10000000 | Cumulative effect of Thermal Plating at level 3 (HullPlating). |
| `upg.hull_cold.1` | 1 | 0 .. 10000000 | Cumulative effect of Cryo Plating at level 1 (HullPlating). |
| `upg.hull_cold.2` | 2 | 0 .. 10000000 | Cumulative effect of Cryo Plating at level 2 (HullPlating). |
| `upg.hull_cold.3` | 3 | 0 .. 10000000 | Cumulative effect of Cryo Plating at level 3 (HullPlating). |
| `upg.hull_rad.1` | 1 | 0 .. 10000000 | Cumulative effect of Rad-Shield Plating at level 1 (HullPlating). |
| `upg.hull_rad.2` | 2 | 0 .. 10000000 | Cumulative effect of Rad-Shield Plating at level 2 (HullPlating). |
| `upg.hull_rad.3` | 3 | 0 .. 10000000 | Cumulative effect of Rad-Shield Plating at level 3 (HullPlating). |
| `upg.hull_void.1` | 1 | 0 .. 10000000 | Cumulative effect of Void Shell at level 1 (HullPlating). |
| `upg.hull_void.2` | 2 | 0 .. 10000000 | Cumulative effect of Void Shell at level 2 (HullPlating). |
| `upg.hull_void.3` | 3 | 0 .. 10000000 | Cumulative effect of Void Shell at level 3 (HullPlating). |
| `upg.turret.1` | 0.15 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 1 (TurretFireRate). |
| `upg.turret.2` | 0.3 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 2 (TurretFireRate). |
| `upg.turret.3` | 0.45 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 3 (TurretFireRate). |
| `upg.turret.4` | 0.6 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 4 (TurretFireRate). |
| `upg.turret.5` | 0.75 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 5 (TurretFireRate). |
| `upg.turret.6` | 0.9 | 0 .. 10000000 | Cumulative effect of Turret Actuators at level 6 (TurretFireRate). |
| `upg.turretdmg.1` | 0.35 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 1 (TurretDamage). |
| `upg.turretdmg.2` | 0.7 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 2 (TurretDamage). |
| `upg.turretdmg.3` | 1.05 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 3 (TurretDamage). |
| `upg.turretdmg.4` | 1.4 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 4 (TurretDamage). |
| `upg.turretdmg.5` | 1.75 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 5 (TurretDamage). |
| `upg.turretdmg.6` | 2.1 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 6 (TurretDamage). |
| `upg.turretdmg.7` | 2.45 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 7 (TurretDamage). |
| `upg.turretdmg.8` | 2.8 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 8 (TurretDamage). |
| `upg.turretdmg.9` | 3.15 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 9 (TurretDamage). |
| `upg.turretdmg.10` | 3.5 | 0 .. 10000000 | Cumulative effect of Turret Damage at level 10 (TurretDamage). |
| `upg.pulse.1` | 0.4 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 1 (TapDamage). |
| `upg.pulse.2` | 0.8 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 2 (TapDamage). |
| `upg.pulse.3` | 1.2 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 3 (TapDamage). |
| `upg.pulse.4` | 1.6 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 4 (TapDamage). |
| `upg.pulse.5` | 2 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 5 (TapDamage). |
| `upg.pulse.6` | 2.4 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 6 (TapDamage). |
| `upg.pulse.7` | 2.8 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 7 (TapDamage). |
| `upg.pulse.8` | 3.2 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 8 (TapDamage). |
| `upg.pulse.9` | 3.6 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 9 (TapDamage). |
| `upg.pulse.10` | 4 | 0 .. 10000000 | Cumulative effect of Ion Pulse at level 10 (TapDamage). |
| `upg.shield.1` | 25 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 1 (ReactorHealth). |
| `upg.shield.2` | 50 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 2 (ReactorHealth). |
| `upg.shield.3` | 75 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 3 (ReactorHealth). |
| `upg.shield.4` | 100 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 4 (ReactorHealth). |
| `upg.shield.5` | 125 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 5 (ReactorHealth). |
| `upg.shield.6` | 150 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 6 (ReactorHealth). |
| `upg.shield.7` | 175 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 7 (ReactorHealth). |
| `upg.shield.8` | 200 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 8 (ReactorHealth). |
| `upg.shield.9` | 225 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 9 (ReactorHealth). |
| `upg.shield.10` | 250 | 0 .. 10000000 | Cumulative effect of Reactor Shield at level 10 (ReactorHealth). |
| `upg.salvage.1` | 0.1 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 1 (RewardBonus). |
| `upg.salvage.2` | 0.2 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 2 (RewardBonus). |
| `upg.salvage.3` | 0.3 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 3 (RewardBonus). |
| `upg.salvage.4` | 0.4 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 4 (RewardBonus). |
| `upg.salvage.5` | 0.5 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 5 (RewardBonus). |
| `upg.salvage.6` | 0.6 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 6 (RewardBonus). |
| `upg.salvage.7` | 0.7 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 7 (RewardBonus). |
| `upg.salvage.8` | 0.8 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 8 (RewardBonus). |
| `upg.salvage.9` | 0.9 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 9 (RewardBonus). |
| `upg.salvage.10` | 1 | 0 .. 10000000 | Cumulative effect of Salvage Rig at level 10 (RewardBonus). |
| `upg.compression.1` | 1.15 | 0 .. 10000000 | Cumulative effect of Cargo Compression at level 1 (StackMultiplier). |
| `upg.compression.2` | 1.3 | 0 .. 10000000 | Cumulative effect of Cargo Compression at level 2 (StackMultiplier). |
| `upg.compression.3` | 1.5 | 0 .. 10000000 | Cumulative effect of Cargo Compression at level 3 (StackMultiplier). |
| `upg.compression.4` | 1.75 | 0 .. 10000000 | Cumulative effect of Cargo Compression at level 4 (StackMultiplier). |

## start

| key | default | range | meaning |
|---|---|---|---|
| `start.wood` | 24 | 0 .. 1000 | Starting wood. |
| `start.scrap` | 30 | 0 .. 1000 | Starting scrap. |
| `start.fuel` | 10 | 0 .. 1000 | Starting fuel cells. |
| `start.backpack_slots` | 9 | 3 .. 60 | Starting backpack slots (small on purpose: it forces a Warehouse in the first session). |
| `start.builders` | 1 | 1 .. 8 | Builders at the start. |

## travel

| key | default | range | meaning |
|---|---|---|---|
| `travel.base_fuel` | 1.5 | 0 .. 20 | Fixed fuel per jump leg (energy units). |
| `travel.k` | 0.14 | 0.01 .. 2 | Fuel per ly^p of route length. |
| `travel.p` | 1.1 | 1 .. 2 | Route-length exponent: above 1 so long trips cost more per light year. |
| `travel.intra_fuel` | 2 | 0 .. 20 | Fuel of a hop between two worlds of the same system. |
| `travel.intra_fuel_per_au` | 0.25 | 0 .. 5 | Extra fuel per AU of orbit difference inside a system. |
| `travel.cargo_factor` | 0.2 | 0 .. 1 | Fuel penalty at a full hold: x(1 + factor x fill). |
| `travel.autopilot_mult` | 1.6 | 1 .. 4 | Fuel multiplier of an autopilot jump (no mini-game, no refund). |
| `travel.refund_max` | 0.4 | 0 .. 1 | Share of the fuel refunded by a perfect Hyperspace Run (legacy 0.4). |
| `travel.run_base_seconds` | 25 | 5 .. 120 | Seconds of the shortest Hyperspace Run leg. |
| `travel.run_log_seconds` | 12 | 0 .. 60 | Added seconds per ln(1 + legLy/20). |
| `travel.run_cap_seconds` | 90 | 20 .. 300 | Longest Hyperspace Run leg. |
| `travel.autopilot_seconds` | 12 | 1 .. 60 | Seconds of an autopilot leg. |
| `travel.hop_fraction` | 0.9 | 0.5 .. 1 | The route planner keeps each leg below this share of the jump range. |
| `travel.detour` | 1.15 | 1 .. 2 | Route length over straight-line distance (stepping-stone systems are not on the line). |
| `travel.max_discount` | 0.7 | 0 .. 0.95 | Largest combined fuel discount of Hangar and Warp Gate. |
| `travel.plating_l2_tier` | 5 | 1 .. 40 | From this planet tier a hazard needs plating level 2. |
| `travel.plating_l3_tier` | 9 | 1 .. 40 | From this planet tier a hazard needs plating level 3. |

## wave

| key | default | range | meaning |
|---|---|---|---|
| `wave.base_budget` | 5 | 0.5 .. 100 | Threat budget of wave 1 at danger 0 (legacy 5). |
| `wave.linear_per_wave` | 2 | 0 .. 20 | Budget added per wave number (legacy 2). |
| `wave.growth_per_wave` | 1.12 | 1 .. 1.5 | Budget growth per wave number (legacy 1.12). |
| `wave.danger_weight` | 0.5 | 0 .. 3 | Budget x (1 + danger x weight) (legacy 0.5). |
| `wave.fuel_budget_base` | 0.5 | 0 .. 5 | Budget x (base + per x energy burned) (legacy 0.5). |
| `wave.fuel_budget_per` | 0.25 | 0 .. 5 | Budget growth per energy unit burned (legacy 0.25). |
| `wave.base_eff` | 1.3 | 0.5 .. 5 | Value returned per value of fuel burned at wave 1. |
| `wave.eff_per_wave` | 0.03 | 0 .. 0.5 | Efficiency gained per wave cleared on the world. |
| `wave.eff_cap_waves` | 20 | 1 .. 1000 | Waves after which the per-wave gain stops. |
| `wave.eff_per_tier` | 0.04 | 0 .. 0.5 | Efficiency gained per planet tier. |
| `wave.bonus_cap` | 1 | 0 .. 5 | Largest total bonus from Alarm Beacon, Salvage Rig and player level. |
| `wave.core_share` | 0.7 | 0 .. 1 | Share of the returned value paid in Xeno Cores. |
| `wave.refund_share` | 0.2 | 0 .. 1 | Share of the returned value paid back as fuel. |
| `wave.seconds_base` | 40 | 5 .. 600 | Seconds of the shortest wave. |
| `wave.seconds_per_energy` | 0.8 | 0 .. 10 | Extra seconds per energy unit burned. |
| `wave.seconds_max` | 150 | 20 .. 900 | Longest wave. |
| `wave.xp_per_threat` | 6 | 0 .. 100 | XP per threat unit (legacy 6). |
| `wave.max_energy` | 400 | 1 .. 100000 | Most energy units one wave may burn. |
| `wave.defense_k` | 2 | 0.1 .. 100 | A wave is winnable when its threat budget is at most defense rating x k. |

## score

| key | default | range | meaning |
|---|---|---|---|
| `score.reach_coefficient` | 700 | 0 .. 100000 | Reach = floor(coefficient x sqrt(maxDistanceLy / divisor)). |
| `score.reach_divisor_ly` | 10 | 0.1 .. 1000 | Reach distance divisor in light years. |
| `score.world_base` | 130 | 0 .. 10000 | Worlds: each landed world is worth base x (1 + tier). |
| `score.world_full_count` | 500 | 1 .. 100000 | Worlds counted at full value (best tiers first); the rest count at the excess share. |
| `score.world_excess_share` | 0.2 | 0 .. 1 | Share of value for landed worlds beyond the full count. |
| `score.industry_base` | 50 | 0 .. 10000 | Industry: each structure is worth base x level^exponent x (1 + structure tier). |
| `score.industry_exponent` | 1.6 | 0.5 .. 3 | Industry level exponent. |
| `score.tech_per_upgrade_level` | 300 | 0 .. 100000 | Tech: points per ship/defense/logistics upgrade level. |
| `score.tech_per_tool_tier` | 800 | 0 .. 100000 | Tech: points per tool tier purchased. |
| `score.defense_base` | 12 | 0 .. 1000 | Defense: base x sqrt(danger) per cleared wave. |
| `score.defense_wave_cap` | 25 | 1 .. 10000 | Anti-farm: waves beyond this count on one world add nothing. |
| `score.atlas_per_resource` | 100 | 0 .. 100000 | Atlas: points per resource discovered. |
| `score.atlas_per_recipe` | 80 | 0 .. 100000 | Atlas: points per recipe crafted. |
| `score.atlas_per_mega_stage` | 28000 | 0 .. 1000000 | Atlas: points per megaproject stage completed. |
| `score.ascension_step` | 0.1 | 0 .. 1 | Final multiplier = 1 + step x ascension rank. |

## obstacle

| key | default | range | meaning |
|---|---|---|---|
| `obstacle.density` | 0.3 | 0 .. 0.8 | Share of the building pads blocked on an ordinary world. |
| `obstacle.density_jitter` | 0.1 | 0 .. 0.3 | Per-planet random swing of the density (plus or minus). |
| `obstacle.home_density` | 0.08 | 0 .. 0.5 | Share of pads blocked on the home world (it starts mostly clear). |
| `obstacle.free_pads` | 4 | 0 .. 12 | The first pads of every world never carry an obstacle. |
| `obstacle.pod_chance` | 0.05 | 0 .. 0.5 | Chance that an obstacle is a shard pod. |
| `obstacle.value_base` | 8 | 0.5 .. 1000 | Clearing cost in scrap-equivalents on a tier 0 world (size multiplier 1). |
| `obstacle.value_growth` | 2 | 1 .. 10 | Clearing cost growth per world tier. |
| `obstacle.cap_base_slots` | 3 | 1 .. 100 | No cost line of a clearing may exceed this many slots on a tier 0 world (the value the cap cuts moves to denser lines). |
| `obstacle.cap_slots_per_tier` | 1.2 | 0 .. 20 | Extra slots a clearing cost line may take for every world tier. |
| `obstacle.cap_total_base_slots` | 14 | 2 .. 200 | Most slots of items a whole clearing may ask for on a tier 0 world. |
| `obstacle.cap_total_slots_per_tier` | 2.5 | 0 .. 50 | Extra slots of items a whole clearing may ask for per world tier. |
| `obstacle.reward_share` | 0.4 | 0 .. 0.9 | Share of the clearing cost that comes back as salvage (must stay below 1). |
| `obstacle.xp_per_value` | 0.05 | 0 .. 5 | XP per scrap-equivalent of clearing cost. |
| `obstacle.time_cap_seconds` | 18000 | 60 .. 86400 | No obstacle takes a builder longer than this. |
| `obstacle.pads.0` | 12 | 4 .. 200 | Building pads of size class 0 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.0` | 0.7 | 0.1 .. 10 | Clearing cost multiplier of size class 0. |
| `obstacle.pads.1` | 16 | 4 .. 200 | Building pads of size class 1 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.1` | 0.85 | 0.1 .. 10 | Clearing cost multiplier of size class 1. |
| `obstacle.pads.2` | 24 | 4 .. 200 | Building pads of size class 2 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.2` | 1 | 0.1 .. 10 | Clearing cost multiplier of size class 2. |
| `obstacle.pads.3` | 36 | 4 .. 200 | Building pads of size class 3 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.3` | 1.25 | 0.1 .. 10 | Clearing cost multiplier of size class 3. |
| `obstacle.pads.4` | 48 | 4 .. 200 | Building pads of size class 4 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.4` | 1.5 | 0.1 .. 10 | Clearing cost multiplier of size class 4. |
| `obstacle.pads.5` | 64 | 4 .. 200 | Building pads of size class 5 (Dwarf, Small, Medium, Large, Giant, Colossal). |
| `obstacle.size_mult.5` | 1.8 | 0.1 .. 10 | Clearing cost multiplier of size class 5. |
| `obstacle.time.t0` | 0 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 0 world. |
| `obstacle.time.t1` | 6 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 1 world. |
| `obstacle.time.t2` | 20 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 2 world. |
| `obstacle.time.t3` | 60 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 3 world. |
| `obstacle.time.t4` | 180 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 4 world. |
| `obstacle.time.t5` | 480 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 5 world. |
| `obstacle.time.t6` | 1200 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 6 world. |
| `obstacle.time.t7` | 2400 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 7 world. |
| `obstacle.time.t8` | 4200 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 8 world. |
| `obstacle.time.t9` | 6300 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 9 world. |
| `obstacle.time.t10` | 9000 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 10 world. |
| `obstacle.time.t11` | 12600 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 11 world. |
| `obstacle.time.t12` | 18000 | 0 .. 86400 | Builder seconds to clear an obstacle on a tier 12 world. |

## shard

| key | default | range | meaning |
|---|---|---|---|
| `shard.skip.multiplier` | 1.2 | 0.1 .. 20 | Skip price = ceil(multiplier x minutes ^ exponent) shards. |
| `shard.skip.exponent` | 0.75 | 0.3 .. 1 | Exponent of the skip price curve (below 1: long waits cost less per minute, so one big skip never costs more than its parts). |
| `shard.skip.min_cost` | 1 | 1 .. 50 | No paid skip is cheaper than this many shards. |
| `shard.skip.free_finish_seconds` | 30 | 0 .. 600 | A job with at most this long left finishes for free. |
| `shard.builder.max` | 4 | 2 .. 8 | Most builders a player can have, earned or bought. |
| `shard.builder.third_price` | 250 | 0 .. 5000 | Shards for the third builder (the Colony Hub gives it for free at level 3). |
| `shard.builder.fourth_price` | 600 | 0 .. 10000 | Shards for the fourth builder (the Colony Hub gives it for free at level 7). |
| `shard.revive.run` | 5 | 0 .. 100 | Shards to revive in a Hyperspace Run. |
| `shard.revive.wave` | 8 | 0 .. 100 | Shards to revive the reactor in a called wave. |
| `shard.cosmetic.min` | 120 | 0 .. 5000 | Cheapest cosmetic in shards. |
| `shard.cosmetic.max` | 1200 | 0 .. 50000 | Priciest regular cosmetic in shards. |
| `shard.earn.levelup_base` | 3 | 0 .. 50 | Shards for a level-up (plus one per full block of levels). |
| `shard.earn.levelup_block` | 15 | 1 .. 1000 | Levels per block of the level-up bonus. |
| `shard.earn.atlas_resource` | 3 | 0 .. 100 | First discovery of a resource. |
| `shard.earn.atlas_recipe` | 2 | 0 .. 100 | First craft of a recipe. |
| `shard.earn.atlas_structure` | 1 | 0 .. 100 | First build of a structure kind. |
| `shard.earn.achievement_small` | 5 | 0 .. 500 | Small achievement. |
| `shard.earn.achievement_medium` | 10 | 0 .. 500 | Medium achievement (new reach tier). |
| `shard.earn.achievement_large` | 25 | 0 .. 500 | Large achievement (era entry). |
| `shard.earn.streak_day1` | 2 | 0 .. 100 | Daily login streak reward on day 1 of the week. |
| `shard.earn.streak_day2` | 2 | 0 .. 100 | Daily login streak reward on day 2 of the week. |
| `shard.earn.streak_day3` | 3 | 0 .. 100 | Daily login streak reward on day 3 of the week. |
| `shard.earn.streak_day4` | 3 | 0 .. 100 | Daily login streak reward on day 4 of the week. |
| `shard.earn.streak_day5` | 4 | 0 .. 100 | Daily login streak reward on day 5 of the week. |
| `shard.earn.streak_day6` | 4 | 0 .. 100 | Daily login streak reward on day 6 of the week. |
| `shard.earn.streak_day7` | 12 | 0 .. 100 | Daily login streak reward on day 7 of the week. |
| `shard.earn.ad_reward` | 2 | 0 .. 50 | Shards for one opt-in rewarded ad (Free Shards placement). |
| `shard.earn.ads_per_day` | 3 | 0 .. 20 | Daily cap of shard ads. |
| `shard.earn.board_participation` | 20 | 0 .. 500 | Weekly board reward for an active week (the weekly Sector Challenge). |
| `shard.earn.board_top10` | 50 | 0 .. 1000 | Weekly board reward for the top 10 percent (instead of the participation reward). |
| `shard.earn.board_top1` | 150 | 0 .. 5000 | Weekly board reward for the top 1 percent (instead of the participation reward). |
| `shard.earn.weekly_objectives` | 3 | 0 .. 10 | Weekly objectives per week (recurring achievements). |
| `shard.earn.weekly_objective_reward` | 5 | 0 .. 100 | Shards per completed weekly objective. |
| `shard.earn.pod_base` | 1 | 0 .. 50 | Shards in an obstacle pod on a tier 0 world. |
| `shard.earn.pod_per_3_tiers` | 1 | 0 .. 50 | Extra shards in a pod for every 3 tiers of the world. |
| `shard.pack.1.shards` | 80 | 1 .. 1000000 | Shards in consumable pack 1. |
| `shard.pack.1.price_tier` | 1 | 1 .. 1000 | Store price tier (dollars) of pack 1, a guide only. |
| `shard.pack.2.shards` | 500 | 1 .. 1000000 | Shards in consumable pack 2. |
| `shard.pack.2.price_tier` | 5 | 1 .. 1000 | Store price tier (dollars) of pack 2, a guide only. |
| `shard.pack.3.shards` | 1100 | 1 .. 1000000 | Shards in consumable pack 3. |
| `shard.pack.3.price_tier` | 10 | 1 .. 1000 | Store price tier (dollars) of pack 3, a guide only. |
| `shard.pack.4.shards` | 2400 | 1 .. 1000000 | Shards in consumable pack 4. |
| `shard.pack.4.price_tier` | 20 | 1 .. 1000 | Store price tier (dollars) of pack 4, a guide only. |
| `shard.pack.5.shards` | 6500 | 1 .. 1000000 | Shards in consumable pack 5. |
| `shard.pack.5.price_tier` | 50 | 1 .. 1000 | Store price tier (dollars) of pack 5, a guide only. |

## sim

| key | default | range | meaning |
|---|---|---|---|
| `sim.growth_payback_hours` | 10 | 0 .. 1000 | A player buys an income upgrade that pays itself back within this many hours of play (0 turns the habit off). |
| `sim.growth_urgent_hours` | 3 | 0 .. 1000 | An upgrade that pays back within this many hours of play is worked toward at once, like a headline goal. |
| `sim.growth_check_seconds` | 60 | 5 .. 3600 | Seconds of play between two looks at the growth options. |
| `sim.harvest_seconds` | 30 | 10 .. 90 | Seconds of one harvest mini-game run including its cooldown (design: 25 to 40). |
| `sim.harvest_overhead` | 3 | 0 .. 30 | Seconds of menu and walking time added to every harvest run. |
| `sim.craft_tap_seconds` | 2.5 | 0.5 .. 20 | Seconds of one craft tap (crafting itself has no timer). |
| `sim.deposit_seconds` | 4 | 0 .. 30 | Seconds to drag the backpack into a warehouse. |
| `sim.buy_seconds` | 4 | 0 .. 30 | Seconds to place, upgrade or purchase one thing. |
| `sim.land_seconds` | 8 | 0 .. 60 | Seconds of takeoff and landing around every flight. |
| `sim.intra_hop_seconds` | 22 | 5 .. 120 | Seconds of a Hyperspace Run between two worlds of the same system. |
| `sim.base_scanner_ly` | 40 | 5 .. 500 | Survey radius around visited systems before any Scanner Range upgrade. |
| `sim.max_runs_per_step` | 6 | 1 .. 50 | Harvest runs a player does before re-planning. |
| `sim.instant_below_seconds` | 15 | 0 .. 120 | Build jobs shorter than this finish at once and need no builder. |
| `sim.fuel_reserve` | 4 | 0 .. 100 | Energy a player keeps above the cost of a trip. |
| `sim.auto_quality` | 0.5 | 0 .. 1 | Mini-game quality assumed for Auto-Harvester output. |
| `sim.storage_trigger` | 0.9 | 0.3 .. 1 | A purchase needing more than this share of all slots makes the player build storage first. |
| `sim.mega_chunk_share` | 0.15 | 0.02 .. 1 | Share of all slots a single megaproject installment may fill. |
| `sim.session_jitter_hours` | 0.4 | 0 .. 3 | Random shift of each session start (deterministic per seed). |
| `sim.pack_deposit_share` | 0.6 | 0.1 .. 1 | The player empties the backpack into storage once it is this full. |
| `sim.intermediate_share` | 0.12 | 0.02 .. 1 | An intermediate product may fill at most this share of all bag slots; beyond it the player waits for the other ingredients. |
| `sim.max_warehouses` | 6 | 1 .. 12 | Most warehouses the player builds on the home world. |
| `sim.max_wait_seconds` | 120 | 0 .. 3600 | A player waits for a job that finishes within this time instead of leaving. |
| `sim.wave_energy_base` | 6 | 1 .. 100 | Energy burned in the first called wave. |
| `sim.wave_energy_per_wave` | 1.5 | 0 .. 20 | Extra energy burned per wave already called. |
| `sim.wave_energy_cap` | 60 | 1 .. 1000 | Most energy a player burns in one called wave. |

