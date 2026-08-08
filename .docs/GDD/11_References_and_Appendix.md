# References & Appendix

## Canonical Vocabulary

This chapter's glossary is the **single source of truth for naming across the GDD**. Where a term below says _"Formerly X,"_ X is retired prose: chapters 00–10 use the new term.

Two deliberate exceptions keep the documentation honest about the codebase:

- **Code identifiers are quoted verbatim, never renamed.** The runtime uses `CardType.Troop`, `CardType.Spell`, `conversionRadius`, `HexCoordinates`, and `Assets/Data/Cards/Troops/`. When a chapter names a field, enum member, class, or path, it uses the real identifier in backticks. The thematic vocabulary is a player-facing layer; the code stays in neutral technical terms.
- **"Conversion" and "hex" survive as mechanical terms.** _Assimilation_ and _sector_ are the in-world words used in player-facing prose; _conversion_ and _hex_ remain correct when describing the rule or the code that implements it (`ConversionResolver`, "1-hex radius"). Both are listed below.

---

## Glossary of Terms

| Term                                | Definition                                                                                                                                                                                                                                                                                                      |
| :---------------------------------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Acid Puddle**                     | A temporary hazardous sector-state modifier created by Acid Crawler's Jump. Blocks landings until its owner action window expires or it is cleansed.                                                                                                                                                            |
| **APM**                             | Actions Per Minute. A measure of Researcher execution speed, especially relevant during Overtime.                                                                                                                                                                                                               |
| **Armored Membrane**                | Bio-Phalanx's passive. The first valid conversion attempt strips the armor; a second, separate attempt flips the specimen. Armor never regenerates. Authored as the `hasArmor` flag.                                                                                                                            |
| **Assimilation**                    | When a specimen lands adjacent to enemy specimens, those enemies are flipped to the deploying Researcher's faction. The core mechanic of spatial domination. The in-world term; see **Conversion**.                                                                                                             |
| **Ataxx**                           | A 1990 abstract strategy board game by Leland Corporation. Pieces clone/jump and convert adjacent enemies. The primary mechanical inspiration for Goo Galaxy.                                                                                                                                                   |
| **Axial Coordinates (q, r)**        | A two-axis coordinate system for hexagonal grids. Enables non-branching mathematical operations for distance/neighbor calculations.                                                                                                                                                                             |
| **Blind Discovery**                 | A competitive format where both Researchers draft from the same deterministic, server-seeded sequence of offer pairs at normalized specimen levels. Removes progression advantages. Formerly "Blind Discovery."                                                                                                 |
| **Callsign**                        | A Researcher's unique display name. The in-world term for "username."                                                                                                                                                                                                                                           |
| **Capsule**                         | A sealed orbital container that delivers specimens, Stardust, DNA Strands, and Nova Cores to Researchers. Formerly "Chest."                                                                                                                                                                                     |
| **Capsule Bay**                     | The starship compartment that holds up to 4 Capsules awaiting decapsulation. Formerly "Chest Slots."                                                                                                                                                                                                            |
| **Clone**                           | A 1-sector range movement of a unit already on the board. Copies **that unit's own type** onto an adjacent empty sector, keeping the original in place. Net +1 unit.                                                                                                                                            |
| **Comms**                           | Crew communication channel. The in-world term for clan chat.                                                                                                                                                                                                                                                    |
| **Containment Breach Protocol**     | A catch-up energy bonus that activates when a Researcher controls ≤40% of specimens on the surface. +15% energy regeneration for 20 seconds. 60s cooldown.                                                                                                                                                      |
| **Conversion**                      | The mechanical name for Assimilation, retained wherever the rule or its implementation is described (`ConversionResolver`, `conversionRadius`, "conversion attempt"). Player-facing text says Assimilation.                                                                                                     |
| **COPPA**                           | Children's Online Privacy Protection Act (US). Requires verifiable parental consent for data collection from children under 13. Amended Rule effective 23 June 2025; broad compliance deadline 22 April 2026.                                                                                                   |
| **Crew**                            | A social group of up to 50 Researchers aboard a starship. The in-world term for Clans/Guilds.                                                                                                                                                                                                                   |
| **DAU**                             | Daily Active Users. Researchers who log in and embark on at least one expedition per day.                                                                                                                                                                                                                       |
| **Defender Action Window**          | A temporary duration that expires after the affected specimen's controller completes their next successful deployment.                                                                                                                                                                                          |
| **Deploy**                          | Playing a card from the hand to put a **new** unit of that card's type on an empty sector adjacent to territory the Researcher already controls. Costs the card's Energy. The only action that introduces a new card type to the board.                                                                         |
| **Discovery Complete**              | The victory screen message when a Researcher wins an expedition.                                                                                                                                                                                                                                                |
| **Discovery Cycle**                 | A deterministic 240-Capsule sequence ensuring fair distribution of rarities over time.                                                                                                                                                                                                                          |
| **Discovery Points (DP)**           | The ranked currency earned/lost from competitive expeditions. Determines Star System placement. Formerly "Trophies."                                                                                                                                                                                            |
| **DNA Strands**                     | Specimen-specific collectibles required (along with Stardust) to Enhance a specimen's level. Formerly "Fragments."                                                                                                                                                                                              |
| **Domination / Total Assimilation** | An instant win condition — triggered when a Researcher assimilates every single enemy specimen on the planetary surface. Awards +50% bonus DP.                                                                                                                                                                  |
| **ECR**                             | Effective Conversion Rate. Average enemy sectors flipped per Energy spent. Used to compare cards **of the same Energy cost**, where the geometric conversion ceiling cancels. It cannot be compared across cost tiers or against $P_v/E$ — see the Validation Methodology in `02_Mathematics_and_Balancing.md`. |
| **Energy**                          | The accumulating resource used to deploy specimens and activate protocols. Generates at 1.0/2.8s (standard) or 1.0/1.4s (Overtime). Formerly "Elixir."                                                                                                                                                          |
| **Enhance**                         | The action of raising a specimen's level using **DNA Strands and Stardust**. Consumes no Galaxy Pass XP; the two progressions are independent. What a level actually changes is an open design item — see `02_Mathematics_and_Balancing.md`. Formerly "Upgrade."                                                |
| **Expedition**                      | A single competitive match. Two Researchers descend to a planetary surface and compete for territorial dominance.                                                                                                                                                                                               |
| **Expedition Cache**                | Resources recovered from a successful expedition. Formerly "Victory Chest."                                                                                                                                                                                                                                     |
| **Expedition Cycle**                | A 4-week competitive season. Formerly "Season."                                                                                                                                                                                                                                                                 |
| **Expedition Gear**                 | Cosmetic equipment worn by a Researcher and displayed on the Researcher ID and the pre-expedition screen. Purely vanity; no stat effect.                                                                                                                                                                        |
| **Expedition Log**                  | A replay of a past expedition, reconstructed from authoritative command logs.                                                                                                                                                                                                                                   |
| **Expedition Race**                 | A weekly competitive format where two Crews compete for the most expedition victories. Formerly "Clan Wars."                                                                                                                                                                                                    |
| **Expedition Recalled**             | The defeat screen message when a Researcher loses an expedition. Softer and more thematic than "Defeat."                                                                                                                                                                                                        |
| **FMOD**                            | A professional audio middleware tool used for adaptive music and interactive sound design.                                                                                                                                                                                                                      |
| **FOMO**                            | Fear Of Missing Out. A psychological driver used in time-limited Galactic Phenomena and rotating market offers.                                                                                                                                                                                                 |
| **Frozen**                          | A status applied by Cryo-Stasis. The unit cannot Clone, Jump, or be converted for 1 defender action window. **Immunity is to conversion only** — a Frozen unit can still be removed, displaced, and cleansed.                                                                                                   |
| **FTUE**                            | First-Time User Experience. The tutorial and onboarding flow for new Researchers.                                                                                                                                                                                                                               |
| **Galactic Archives**               | The intergalactic leaderboard. The hall of fame for the galaxy's greatest Researchers.                                                                                                                                                                                                                          |
| **Galactic Core, The**              | The 10th and final Star System. 3,500+ DP. Infinite ladder. Top 1,000 Researchers immortalized.                                                                                                                                                                                                                 |
| **Galactic Market**                 | The intergalactic trading post where Researchers spend Stardust and Nova Cores. Formerly "Shop."                                                                                                                                                                                                                |
| **Galactic Phenomena**              | Time-limited weekend events (Stage Swap, Twisted Rules, Blind Discovery). Formerly "Events."                                                                                                                                                                                                                    |
| **Galaxy Pass**                     | The seasonal progression pass. Tiers with free and premium tracks. Themed around each Expedition Cycle.                                                                                                                                                                                                         |
| **Galaxy Pass XP**                  | Account-level progress toward the 35 Galaxy Pass tiers, earned from expedition victories, Research Contracts, Expedition Milestones, and Sample Sharing. **Never a specimen stat** — it does not level, unlock, or modify a card, and it cannot be spent. Specimen levels move only through **Enhance**.        |
| **GDPR**                            | General Data Protection Regulation (EU). Governs personal data collection, storage, and processing.                                                                                                                                                                                                             |
| **Heavy Biomass**                   | The Apex Strain's passive. The specimen cannot be pushed, pulled, or displaced by any effect — including another Apex Strain's Seismic Shockwave. It can still be assimilated normally.                                                                                                                         |
| **Hexxagon**                        | A hexagonal variant of Ataxx. Played on a 61-hex grid. Direct inspiration for Goo Galaxy's planetary surface geometry.                                                                                                                                                                                          |
| **Hover**                           | Plasmic Leaper's passive. The specimen traverses blocked sectors, acid puddles, and Sealed sectors without penalty. Authored as the `ignoresHazards` flag.                                                                                                                                                      |
| **Impact Profile**                  | How a card divides its $P_v$ budget across Spatial Influence (max 50%), Temporal Impact (25%), and Strategic Utility (25%). A description of spend, not a second definition of $P_v$. Currently qualitative.                                                                                                    |
| **Jump**                            | A 2-sector range movement of a unit already on the board. Repositions it to the target sector, leaving the source empty. Net +0 units.                                                                                                                                                                          |
| **Kit**                             | A Researcher's **8-card** loadout for expeditions — Specimens and Protocols in any legal mix. The in-world term for "Deck."                                                                                                                                                                                     |
| **Komi**                            | A starting-Energy offset (inspired by Go) used to compensate map asymmetry. **Set to 0 on symmetric maps, including every launch map.** Retained and server-tunable for the planned asymmetric maps.                                                                                                            |
| **LGPD**                            | Lei Geral de Proteção de Dados (Brazil). Brazilian data protection law similar to GDPR.                                                                                                                                                                                                                         |
| **LiveOps**                         | Live Operations. Ongoing game updates, Galactic Phenomena, balance patches, and content drops delivered without full client updates.                                                                                                                                                                            |
| **LTV**                             | Lifetime Value. Total revenue expected from a single Researcher over their entire engagement with the game.                                                                                                                                                                                                     |
| **MCTS**                            | Monte Carlo Tree Search. An AI algorithm used for simulating expedition outcomes. Used in balance testing.                                                                                                                                                                                                      |
| **MPS SDK**                         | Unity Multiplayer Services SDK. The preferred session-layer integration point for Lobby, Relay, Matchmaker, and related multiplayer service flows.                                                                                                                                                              |
| **MVP**                             | Minimum Viable Product. In this GDD it often refers to the Phase 2 target slice, though a leaner internal cut may be required before external playtesting.                                                                                                                                                      |
| **NGO**                             | Netcode for GameObjects. Unity's official multiplayer networking framework.                                                                                                                                                                                                                                     |
| **Nova Cores**                      | The premium (hard) currency. Rare, super-dense stellar energy cores purchased with real money. Formerly "Gems."                                                                                                                                                                                                 |
| **Overtime**                        | A 1-minute sudden death phase triggered when expedition scores are tied at the end of standard time. Energy generation doubles (2x).                                                                                                                                                                            |
| **Owner Action Window**             | A temporary duration that expires after the effect owner's next successful deployment resolves.                                                                                                                                                                                                                 |
| **P2W**                             | Pay-to-Win. A monetization design where spending real money provides direct competitive advantages. Avoided in Goo Galaxy.                                                                                                                                                                                      |
| **Planetary Surface**               | The 61-sector hexagonal grid where expeditions take place. Formerly "Hex Grid" / "Board."                                                                                                                                                                                                                       |
| **Protocol**                        | A card type representing a scientific procedure activated during expeditions. Does not place a specimen. Formerly "Spell."                                                                                                                                                                                      |
| **$P_v$**                           | Power Value. A card's impact **budget**, defined solely as the square of its Energy cost. Distinct from the Impact Profile, which describes how that budget is spent — the two are never equal.                                                                                                                 |
| **Research Contract**               | An optional bonus objective from the Galactic Research Council. Formerly "Challenge."                                                                                                                                                                                                                           |
| **Researcher**                      | The player's in-world identity. A comical, charming alien scientist exploring the galaxy.                                                                                                                                                                                                                       |
| **Researcher ID**                   | The player's chosen Researcher character and visual appearance. The primary cosmetic vector.                                                                                                                                                                                                                    |
| **Rooted**                          | A status that prevents a specimen's controller from moving that specimen until the defender action window expires.                                                                                                                                                                                              |
| **Sample Purge**                    | The ability to discard a **card** from the Active Samples for 0.5 Energy, drawing the next in the cycle.                                                                                                                                                                                                        |
| **Sample Sharing**                  | Crewmates sharing duplicate DNA Strands with each other. Formerly "Card Donations."                                                                                                                                                                                                                             |
| **ScriptableObject (SO)**           | A Unity data container that exists as a project-level asset. In Goo Galaxy these primarily live under `Assets/Data/*` and hold specimen definitions, configs, registries, and tuning parameters.                                                                                                                |
| **Sealed**                          | A temporary sector-state modifier that blocks **every landing — Deploy, Clone, and Jump** — on selected empty sectors until its owner action window expires or it is cleansed. Hover ignores it.                                                                                                                |
| **Sector**                          | A single hex on the planetary surface survey grid. Formerly "Hex" / "Tile."                                                                                                                                                                                                                                     |
| **Soft Launch**                     | A limited-geography release to test retention, monetization, and server stability before global launch.                                                                                                                                                                                                         |
| **Specimen**                        | A **card type** representing a slime life form. Playing one deploys a **unit**. Formerly "Troop." A Specimen is the card; see **Unit** for the instance it puts on the board.                                                                                                                                   |
| **Stalemate**                       | The player-facing name for an expedition that ends perfectly tied after Overtime. No DP and no Capsule are awarded. The internal match state is `GameOver_Draw`; prefer "Stalemate" in anything a Researcher reads.                                                                                             |
| **Star System**                     | A ranked tier in the Discovery Points progression system. 10 Star Systems spanning from Gloopiter to The Galactic Core. Formerly "Arena."                                                                                                                                                                       |
| **Stardust**                        | The common (soft) currency. Abundant cosmic resource used for basic enhancements and market purchases. Formerly "Gold."                                                                                                                                                                                         |
| **SUV Framework**                   | Social, Utility, Vanity — the three pillars of ethical F2P monetization.                                                                                                                                                                                                                                        |
| **Symposium**                       | The end-of-cycle competitive showcase held when an Expedition Cycle closes. Top placements award Nova Cores. Unlocked at Singularity Reach.                                                                                                                                                                     |
| **Total Assimilation**              | See Domination.                                                                                                                                                                                                                                                                                                 |
| **Transmission**                    | An in-world push notification from the Researcher's starship. "Incoming Transmission: ..."                                                                                                                                                                                                                      |
| **Unit**                            | One live instance standing on a sector. A Specimen is the card; a unit is what that card put on the board. Cloning a unit produces another unit of the same Specimen. Prefer "unit" over "piece" or "troop".                                                                                                    |
| **Warp Jump**                       | The act of advancing to the next Star System upon earning enough Discovery Points. Formerly "Arena Promotion."                                                                                                                                                                                                  |
| **WCAG**                            | Web Content Accessibility Guidelines. Used as the accessibility standard for color contrast (minimum 4.5:1 ratio).                                                                                                                                                                                              |

---

## Key Formulas Reference

### 1. Power-Cost Scaling

$$P_v = E^2$$

### 2. Stat Progression per Level

$$\text{Stat}_{Lv} = \text{Stat}_{Base} \times 1.10^{(Lv - 1)} \quad \text{— placeholder; the scaling rule is undecided}$$

### 3. Hex Distance (Axial Coordinates)

$$d = \frac{|q_1 - q_2| + |r_1 - r_2| + |(q_1 + r_1) - (q_2 + r_2)|}{2}$$

### 4. Discovery Point Change per Expedition

$$\Delta DP = DP_{base} \times M_{streak} \times M_{system} \times M_{domination}$$

### 5. Expedition Cycle Archival Reset (Soft Reset)

$$DP_{new} = 3000 + \frac{DP_{current} - 3000}{2}$$

### 6. Energy Generation

$$E_{generated}(t) = \frac{t}{R} \quad \text{where } R = 2.8s \text{ (standard)} \text{ or } 1.4s \text{ (overtime)}$$

### 7. Effective Conversion Rate (Balance Metric)

$$ECR = \frac{\text{Average enemy sectors assimilated per deployment}}{E_{cost}} \quad \text{— compare within a cost tier only}$$

### 8. Timeout Score

$$Score_{researcher} = \text{controlled specimens currently on the planetary surface}$$

---

## Launch Roster Quick Reference

| Specimen / Protocol | Cost | Type     | Rarity    | Key Ability                                                                                |
| :------------------ | :--: | :------- | :-------- | :----------------------------------------------------------------------------------------- |
| Subject Alpha       |  1   | Specimen | Common    | Standard assimilation. Baseline specimen.                                                  |
| Acid Crawler        |  2   | Specimen | Common    | Jump leaves an acid puddle for 2 owner action windows (area denial).                       |
| Bio-Phalanx         |  3   | Specimen | Rare      | Armored Membrane — first valid assimilation attempt strips armor, second assimilates.      |
| Volatile Mass       |  4   | Specimen | Epic      | 2-sector AoE on deploy and again on the detonating Jump. 3-second fuse; cannot Clone.      |
| Plasmic Leaper      |  4   | Specimen | Epic      | Hover (ignores hazards). Roots newly assimilated enemies.                                  |
| The Apex Strain     |  5   | Specimen | Legendary | Seismic push (displaces enemies 1 sector). Immovable.                                      |
| Cryo-Stasis         |  2   | Protocol | Rare      | Freezes a 3-sector cluster for 1 defender action window. Frozen units cannot be converted. |
| Sterilization Beam  |  4   | Protocol | Epic      | Vaporizes 4-sector cluster. Ignores all defenses.                                          |

---

## Expansion Prototype Quick Reference

| Specimen / Protocol | Cost | Type     | Rarity | Key Ability                                                                    |
| :------------------ | :--: | :------- | :----- | :----------------------------------------------------------------------------- |
| Quarantine Drone    |  3   | Specimen | Rare   | Creates up to 2 Sealed adjacent empty sectors for 1 owner action window.       |
| Detox Mycelium      |  3   | Specimen | Rare   | Cleanses nearby friendly Frozen/Rooted states and dissolves acid puddles.      |
| Purge Pulse         |  2   | Protocol | Rare   | Cleanses Frozen, Rooted, Sealed, and acid puddles in a 3-sector cluster.       |
| Phase Relay         |  3   | Protocol | Epic   | Repositions 1 allied specimen with a free Jump that still resolves on landing. |

---

## Star System Quick Reference

|  #  | System Name       | DP Range      | Key Unlock                         |
| :-: | :---------------- | :------------ | :--------------------------------- |
|  1  | Gloopiter         | 0 – 299       | Subject Alpha, Acid Crawler        |
|  2  | Sludgar-4         | 300 – 599     | Bio-Phalanx                        |
|  3  | Cryo-9            | 600 – 999     | Volatile Mass, Crews               |
|  4  | Toxis Major       | 1,000 – 1,399 | Plasmic Leaper, Galactic Market    |
|  5  | Nova Rubra        | 1,400 – 1,799 | Cryo-Stasis, Blind Discovery       |
|  6  | Nexar Prime       | 1,800 – 2,199 | Sterilization Beam, Galaxy Pass    |
|  7  | Void's Edge       | 2,200 – 2,599 | The Apex Strain                    |
|  8  | Apex Nebula       | 2,600 – 2,999 | Epic specimen pool expands         |
|  9  | Singularity Reach | 3,000 – 3,499 | Legendary pool, Symposia           |
| 10  | The Galactic Core | 3,500+        | Infinite ladder, Galactic Archives |

> **Canonical:** These ten Star Systems are the progression ladder for the complete product. `02_Mathematics_and_Balancing.md` carries the same ten with their DP ranges and full unlock lists; this table is the quick reference. No chapter should reintroduce the retired "Arena" naming or a reduced tier count.

---

## Energy System Quick Reference

| Parameter       |   Standard    |   Overtime    |
| :-------------- | :-----------: | :-----------: |
| Generation Rate | 1.0 / 2.8 sec | 1.0 / 1.4 sec |
| Max Cap         |     10.0      |     10.0      |
| P1 Starting     |      5.0      |       —       |
| P2 Starting     |      5.0      |       —       |
| Match Duration  |   3:00 min    |   1:00 min    |

---

## Bibliography

### Core Game Design

|  #  | Source                                                                                                                                                                                   | Topic                                                |
| :-: | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :--------------------------------------------------- |
|  1  | [Ataxx - Wikipedia](https://en.wikipedia.org/wiki/Ataxx)                                                                                                                                 | Core mechanic reference                              |
|  2  | [Ataxx - Game Rules and Variations (LinuxOnly)](http://www.linuxonly.nl/docs/4/91_Game_rules_and_variations.html)                                                                        | Clone/Jump rules, Hexxagon variant                   |
|  3  | [Balancing an Evolving Game: Case Clash Royale (Arto Huhta)](https://medium.com/flaregames-blog/balancing-an-evolving-game-case-clash-royale-739c18d16ef7)                               | 10% scaling, chest cycles, economy design            |
|  4  | [Clash Royale's Card Balancing Guru (GDC / Game Developer)](https://www.gamedeveloper.com/design/-i-clash-royale-i-s-card-balancing-guru-leans-less-on-metrics-more-on-design-intuition) | Balance philosophy, buffs > nerfs                    |
|  5  | [Hexagonal Grids - Red Blob Games (Amit Patel)](https://www.redblobgames.com/grids/hexagons/)                                                                                            | Axial coordinates, distance formula, neighbor lookup |

### Game Balance & Theory

|  #  | Source                                                                                                                                                          | Topic                             |
| :-: | :-------------------------------------------------------------------------------------------------------------------------------------------------------------- | :-------------------------------- |
|  6  | [Theory: On Card Stats and Elixir Scaling (r/ClashRoyale)](https://www.reddit.com/r/ClashRoyale/comments/qdgbtr/theory_on_card_stats_and_elixir_scaling/)       | $P_v \propto E^2$ formula         |
|  7  | [The Advantage of Moving First (ResearchGate)](https://www.researchgate.net/publication/316706553_The_Advantage_of_Moving_First_Versus_a_First-Mover_Advantage) | First Mover Advantage theory      |
|  8  | [Performance of MCTS Algorithms in Ataxx (Ribeiro)](https://leoribeiro.github.io/papers/mcts-ataxx-eniac2018.pdf)                                               | AI simulation for balance testing |
|  9  | [Komi in Go (r/baduk)](https://www.reddit.com/r/baduk/comments/5e1l29/what_do_pro_think_about_the_value_of_the_first/)                                          | Komi compensation design          |

### Technical Architecture

|  #  | Source                                                                                                                                  | Topic                             |
| :-: | :-------------------------------------------------------------------------------------------------------------------------------------- | :-------------------------------- |
| 10  | [ScriptableObjects for Modular Architecture - Unity](https://unity.com/how-to/create-modular-game-architecture-with-scriptable-objects) | Data-driven design patterns       |
| 11  | [Object Pooling - Unity 6 Manual](https://docs.unity3d.com/6000.0/Documentation/Manual/performance-object-pooling.html)                 | GC avoidance, mobile performance  |
| 12  | [Netcode for GameObjects - Unity Multiplayer](https://docs-multiplayer.unity3d.com/netcode/current/about/)                              | Server-authoritative networking   |
| 13  | [NetworkVariable - NGO Docs](https://docs-multiplayer.unity3d.com/netcode/current/basics/networkvariable/)                              | State synchronization             |
| 14  | [RPCs - NGO Docs](https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/messaging-system/)                               | ServerRpc / ClientRpc patterns    |
| 15  | [Game Programming Patterns - Unity](https://unity.com/resources/level-up-your-code-with-game-programming-patterns)                      | Observer, Command, State patterns |

### Monetization & Economy

|  #  | Source                                                                                                                                            | Topic                                       |
| :-: | :------------------------------------------------------------------------------------------------------------------------------------------------ | :------------------------------------------ |
| 16  | [Three Pillars of F2P Monetization (Galyonkin)](https://galyonk.in/three-pillars-of-free-to-play-monetization-edbe21852275)                       | SUV framework                               |
| 17  | [Top Mobile Game Monetization Strategies 2026 (The Mind Studios)](https://games.themindstudios.com/post/top-mobile-game-monetization-strategies/) | Battle pass evolution, hybrid models        |
| 18  | [Mobile Game Monetization Strategies 2025 (Adapty)](https://adapty.io/blog/mobile-game-monetization/)                                             | Mini battle passes, live-ops sophistication |

### Retention & LiveOps

|  #  | Source                                                                                                                                        | Topic                          |
| :-: | :-------------------------------------------------------------------------------------------------------------------------------------------- | :----------------------------- |
| 19  | [Game Retention: 12 Strategies (Feature Upvote)](https://featureupvote.com/blog/game-retention/)                                              | FOMO events, progression loops |
| 20  | [Mobile Game Retention Benchmarks (MAF)](https://maf.ad/en/blog/mobile-game-retention-benchmarks/)                                            | D1/D7/D30 industry benchmarks  |
| 21  | [D1/D7/D30 Retention Drivers (Solsten)](https://solsten.io/blog/d1-d7-d30-retention-in-gaming)                                                | Retention architecture         |
| 22  | [FTUE Best Practices (Game Developer)](https://www.gamedeveloper.com/design/best-practices-for-a-successful-ftue-first-time-user-experience-) | Onboarding design              |
| 23  | [10 Tips for FTUE in F2P (GameAnalytics)](https://www.gameanalytics.com/blog/tips-for-a-great-first-time-user-experience-ftue-in-f2p-games)   | Tutorial optimization          |
| 24  | [Live Ops Strategy 2025 (FoxData)](https://foxdata.com/en/blogs/live-ops-strategy-in-2025-the-key-to-longterm-mobile-game-growth/)            | Event design, A/B testing      |

### Art, Audio & UX

|  #  | Source                                                                                                                                                      | Topic                         |
| :-: | :---------------------------------------------------------------------------------------------------------------------------------------------------------- | :---------------------------- |
| 25  | [Gaming Color Palette Combinations (Media.io)](https://www.media.io/color-palette/gaming-color-palette.html)                                                | Cosmic Neon palette           |
| 26  | [Testing Color Contrast in Mobile Apps (Deque)](https://www.deque.com/blog/testing-color-contrast-in-mobile-apps/)                                          | WCAG 1.4.3 compliance         |
| 27  | [FMOD Unity Tutorial (Generalist Programmer)](https://generalistprogrammer.com/tutorials/fmod-unity-complete-game-audio-integration-tutorial)               | Adaptive music implementation |
| 28  | [Creating Game Juice: Sound Effects (Creator Sounds Pro)](https://creatorsoundspro.com/creating-game-juice-using-sound-effects-to-improve-player-feedback/) | Audio feedback design         |
| 29  | [Sound Design Tips (GameAnalytics)](https://www.gameanalytics.com/blog/9-sound-design-tips-to-improve-your-games-audio)                                     | Game audio best practices     |

### Legal & Compliance

|  #  | Source                                                                                                                                                                                        | Topic                                            |
| :-: | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------------------------------------- |
| 30  | [ASA Loot Box Advertising Rules UK 2026 (Lewis Silkin)](https://www.lewissilkin.com/insights/2026/02/27/new-tough-measures-brought-in-by-the-asa-on-advertising-loot-boxes-in-the-uk-102mknq) | UK loot box disclosure (May 2026)                |
| 31  | [COPPA 2025 Amended Rule (ESRB)](https://www.esrb.org/privacy-certified-blog/the-abcs-of-the-2025-privacy-playground-age-assurance-bots-and-coppa/)                                           | Amended COPPA requirements                       |
| 32  | [COPPA Amended Rule Details (Loeb)](https://www.loeb.com/en/insights/publications/2025/05/childrens-online-privacy-in-2025-the-amended-coppa-rule)                                            | Compliance deadline April 2026                   |
| 33  | [Games Industry Legal Trends 2026 (GamesIndustry.biz)](https://www.gamesindustry.biz/games-industry-legal-trends-to-watch-in-2026-ai-child-safety-loot)                                       | German loot box resolution, Digital Fairness Act |

### Soft Launch & Marketing

|  #  | Source                                                                                                                                                       | Topic                                             |
| :-: | :----------------------------------------------------------------------------------------------------------------------------------------------------------- | :------------------------------------------------ |
| 34  | [Soft Launch is Changing in 2026 (PocketGamer.biz)](https://www.pocketgamer.biz/soft-launch-is-changing-in-2026-how-and-where-should-you-release-your-game/) | Regional soft launch strategy                     |
| 35  | [Mobile Game Marketing Strategy 2026 (Stepico)](https://stepico.com/blog/mobile-game-marketing-strategy-in-2026/)                                            | Launch staging, KPI-driven decisions              |
| 36  | [Matchmaking Tips (GameAnalytics)](https://www.gameanalytics.com/blog/matchmaking-tips-for-game-developers)                                                  | Trophy-based matchmaking, queue time optimization |

### Analytics

|  #  | Source                                                                                                                                   | Topic                             |
| :-: | :--------------------------------------------------------------------------------------------------------------------------------------- | :-------------------------------- |
| 37  | [Game Analytics Implementation Best Practices (Adrian Crook)](https://adriancrook.com/best-practices-for-game-analytics-implementation/) | KPI tracking, platform comparison |
| 38  | [2025 Mobile Gaming Benchmarks (GameAnalytics)](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)                     | Industry benchmark data           |
| 39  | [2026 Mobile & PC Gaming Benchmarks (GameAnalytics)](https://www.gameanalytics.com/reports/2026-mobile-pc-gaming-benchmarks)             | Updated benchmark data            |
