# Goo Galaxy: Game Design Document

## Elevator Pitch

> **"Goo Galaxy"** is a real-time PvP mobile strategy game where players — as charming alien **Researchers** — travel the galaxy discovering, cataloging, and deploying squads of sentient, self-replicating alien slimes onto planetary surfaces to assimilate and dominate the terrain. Think **Clash Royale meets Ataxx, set in a colorful cosmos** — fast 3-minute expeditions, deep kit-building, and planets that shift with every discovery.

---

## Executive Summary

"Goo Galaxy" fuses the deterministic, spatial-domination logic of classic board games **Ataxx** and **Hexxagon** with the asymmetrical kit-building, real-time specimen deployment, and energy-based resource management popularized by **Clash Royale**. The result is a competitively deep yet immediately accessible mobile experience designed for the mid-core strategy audience.

Set in a vibrant, lighthearted galaxy, players are comical alien **Researchers** competing to explore uncharted planets, discover exotic slime life forms, and prove their scientific prowess. The game is built on the **Unity Engine** using **Netcode for GameObjects (NGO)** for multiplayer, targeting iOS and Android with a Free-to-Play (F2P) business model driven by cosmetic monetization (Researcher gear, specimen skins, starship themes), a **Galaxy Pass**, and a fair progression system.

> **Note:** For MVP testing scope and project phases, see `09_MVP_And_Roadmap.md`. This document and the rest of the GDD describe the **complete product vision**.

---

## Unique Selling Proposition (USP)

| Dimension            | What Makes Goo Galaxy Unique                                                                                                                                                                          |
| :------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Core Mechanic**    | No other mobile game combines Ataxx-style spatial assimilation with real-time energy-based specimen deployment. Every specimen placed can flip an entire planet.                                      |
| **Tactical Depth**   | Clone vs. Jump movement creates a constant risk/reward dilemma absent from lane-based games like Clash Royale.                                                                                        |
| **Visual Identity**  | Neon-drenched sentient slimes discovered across colorful alien worlds by comical, charming alien Researchers — a character-rich, IP-driven aesthetic with high merchandising and animation potential. |
| **Fair Competition** | Komi system (energy compensation for Player 2) mathematically neutralizes first-mover advantage — a problem no competitor has formally solved.                                                        |
| **Spectator Appeal** | The hex grid's visual clarity and dramatic planetary territory swings make expeditions highly watchable and streamable.                                                                               |

---

## Game Pillars

### 1. Strategic Depth via Spatial Control

The hexagonal planetary surface is the arena. Victory depends on **positioning**, not brute force. Every Clone expands territory; every Jump repositions power. The interplay between specimen abilities and planetary geography creates emergent tactical depth that rewards planning over reflexes alone.

### 2. Dynamic Pacing

A strict **3-minute expedition** with a **1-minute 2x Energy Overtime** ensures no match overstays its welcome. The overtime phase creates dramatic comebacks and tests both strategic foresight and execution speed — the perfect blend for mobile sessions.

### 3. Accessible yet Deep Meta-Game

Specimen collection, DNA Strand-based enhancements, and a **10-Star System** progression ladder create a long discovery arc. The 10% uniform stat scaling per level ensures all specimen interactions remain mathematically identical at every skill tier.

### 4. "Cosmic Discovery" Aesthetics

A unique visual identity — deep space backdrops, colorful alien planets, and vibrant neon slimes discovered and deployed by **comical, expressive alien Researchers**. This wonder-meets-chaos duality drives global appeal, cosmetic desirability, and IP recognition. The Researchers are the emotional heart of the game — bumbling, brilliant, and instantly lovable.

### 5. Fair Competitive Play

Mathematics-driven balance via the $P_v \propto E^2$ power-cost formula, combined with Komi energy compensation for Player 2, ensures competitive integrity. The Draft Mode further strips away progression advantages for pure-skill tournaments.

---

## Core Loop

```mermaid
%%{init: {"theme":"base","themeVariables":{"primaryColor":"#F7E1E8","secondaryColor":"#DCEBF7","tertiaryColor":"#E4F3E1","primaryBorderColor":"#BFA9B5","lineColor":"#A8B6C8","primaryTextColor":"#4E4A57","clusterBkg":"#FAF5EA","clusterBorder":"#CDBFAF","edgeLabelBackground":"#FFF9F2","noteBkgColor":"#F7F1FA","noteTextColor":"#4E4A57","taskBkgColor":"#DCEBF7","taskTextColor":"#4E4A57","taskTextOutsideColor":"#4E4A57","sectionBkgColor":"#E4F3E1","sectionBorderColor":"#BCD0B9","gridColor":"#E8DDD3","todayLineColor":"#C7A7B5","actorBkg":"#F7E1E8","actorBorder":"#BFA9B5","actorTextColor":"#4E4A57","signalColor":"#A8B6C8","signalTextColor":"#4E4A57","labelBoxBkgColor":"#FFF9F2","labelBoxBorderColor":"#CDBFAF","labelTextColor":"#4E4A57"}}}%%
flowchart LR
    A["Embark on Expedition<br/>3-min PvP on Planetary Surface"] --> B["Discovery Complete / Recalled<br/>Earn DP & Capsules"]
    B --> C["Open Capsules<br/>Collect DNA Strands & Stardust"]
    C --> D["Enhance Specimens<br/>Increase Stats +10%/Lv"]
    D --> E["Build Kits<br/>Optimize 8-Specimen Loadout"]
    E --> A
    B --> F["Warp Jump<br/>Unlock Star Systems & Content"]
    F --> A

    style A fill:#DCEBF7,color:#4E4A57,stroke:#B7C8D9
    style B fill:#F7E1E8,color:#4E4A57,stroke:#BFA9B5
    style C fill:#F8F1D7,color:#4E4A57,stroke:#D8C79E
    style D fill:#E4F3E1,color:#4E4A57,stroke:#BCD0B9
    style E fill:#F8E6D8,color:#4E4A57,stroke:#D7B9A4
    style F fill:#EADFF7,color:#4E4A57,stroke:#C5B1D8
```

### Loop Breakdown

| Step | Action                   | Engagement Driver                                                                |
| :--: | :----------------------- | :------------------------------------------------------------------------------- |
|  1   | **Embark on Expedition** | Core fun — real-time spatial PvP on a 61-sector planetary surface.               |
|  2   | **Earn Discoveries**     | Extrinsic motivation — Discovery Points for ranking, Capsules for progression.   |
|  3   | **Open Capsules**        | Variable-ratio reinforcement — randomized but deterministic DNA Strand drops.    |
|  4   | **Enhance Specimens**    | Power fantasy — stat increases feel meaningful but preserve balance.             |
|  5   | **Build Kits**           | Self-expression — experiment with 8-specimen loadouts and discover synergies.    |
|  6   | **Warp to New Systems**  | Long-term aspiration — unlock new Star Systems, specimens, events, and prestige. |

---

## Competitive Landscape

| Game                 | Similarity to Goo Galaxy                                               | Key Difference                                                         |
| :------------------- | :--------------------------------------------------------------------- | :--------------------------------------------------------------------- |
| **Clash Royale**     | Real-time card deployment, elixir economy, 8-card decks, Arena ranking | Lane-based tower destruction vs. hex grid spatial assimilation         |
| **Hexxagon / Ataxx** | Hex grid, Clone/Jump movement, conversion mechanic                     | No asymmetric specimens, no kit-building, no real-time resource system |
| **Chess**            | Deterministic strategy, spatial control, competitive ladder            | Turn-based, no specimen collection, no meta-game progression           |
| **TFT / Auto Chess** | Meta-game depth, unit synergies, ranked competitive                    | Auto-battler with no direct board manipulation by players              |
| **Brawl Stars**      | Supercell polish, short matches, Trophy Road, cosmetics                | Action/shooter gameplay vs. strategic planetary control                |

**Market Positioning:** Goo Galaxy occupies the intersection of **board game tactical depth** and **mobile card-game accessibility** — a niche currently unserved by any major title. Wrapped in a charming space exploration theme, it appeals to both competitive strategists and character-driven casual players.

---

## Target Audience & Platform

- **Platform:** Mobile (iOS & Android). Potential future expansion to tablet-optimized layouts.
- **Primary Audience:** Mid-core strategy players (ages 16-35). Fans of Clash Royale, auto-battlers, chess, and tactical board games.
- **Secondary Audience:** Casual players attracted by the charming slime and Researcher characters, the colorful space aesthetic, and short expedition length.
- **Monetization Sensitivity:** The audience expects fair F2P. Cosmetic-first monetization (Researcher gear, specimen skins, starship themes) with zero pay-to-win mechanics in competitive modes.

---

## Key Performance Indicators (KPI Targets)

| KPI                        | Target          | Industry Benchmark (Mid-Core) |
| :------------------------- | :-------------- | :---------------------------- |
| **Day 1 Retention**        | ≥ 40%           | 25-33%                        |
| **Day 7 Retention**        | ≥ 18%           | 8-15%                         |
| **Day 30 Retention**       | ≥ 7%            | 3-5%                          |
| **Average Session Length** | 8-12 min        | 6-10 min                      |
| **Sessions per Day**       | 4-6             | 3-5                           |
| **P1 vs P2 Win Rate**      | 49-51%          | N/A (unique to Goo Galaxy)    |
| **ARPDAU**                 | USD 0.08 - 0.15 | USD 0.05 - 0.12               |
| **Day 7 Payer Conversion** | ≥ 3%            | 2-3%                          |

---

## Document Index

|  #  | Document                                       | Description                                                                              |
| :-: | :--------------------------------------------- | :--------------------------------------------------------------------------------------- |
| 00  | **Pitch & Overview** _(this document)_         | Vision, core loop, KPIs, and competitive positioning.                                    |
| 01  | `01_Mechanics_and_Core_Gameplay.md`            | Planetary surface rules, energy system, expedition flow, controls.                       |
| 02  | `02_Mathematics_and_Balancing.md`              | Power-cost formula, progression curves, Komi, matchmaking.                               |
| 03  | `03_Specimens_Protocols_and_Factions.md`       | Full specimen roster, stat tables, synergy matrix, kit archetypes.                       |
| 04  | `04_Economy_and_Monetization.md`               | Dual currency (Stardust/Nova Cores), capsule cycle, Galaxy Pass, pricing strategy.       |
| 05  | `05_Meta_Game_Retention_and_LiveOps.md`        | Crews, Galactic Phenomena, expedition cycles, daily/weekly contracts.                    |
| 06  | `06_Art_Direction_and_UX.md`                   | Visual identity (cosmic neon), Researcher & specimen design, screen flow, accessibility. |
| 07  | `07_Audio_and_Sound_Design.md`                 | Adaptive music, SFX catalog, middleware architecture.                                    |
| 08  | `08_Technical_Architecture_and_Multiplayer.md` | Unity architecture, NGO networking, class diagrams, DevOps.                              |
| 09  | `09_MVP_And_Roadmap.md`                        | MVP scope, production phases, Gantt timeline, kill switch criteria.                      |
| 10  | `10_Operations_Security_and_Legal.md`          | GDPR, COPPA, loot box compliance, anti-cheat, soft launch.                               |
| 11  | `11_References_and_Appendix.md`                | Glossary, key formulas, bibliography, data structures.                                   |
