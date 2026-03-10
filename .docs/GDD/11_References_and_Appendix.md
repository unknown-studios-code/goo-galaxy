# References & Appendix

## Glossary of Terms

| Term                         | Definition                                                                                                                                                                                   |
| :--------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **APM**                      | Actions Per Minute. A measure of player execution speed, especially relevant during Overtime.                                                                                                |
| **Acid Puddle**              | A temporary hazardous hex-state modifier created by Acid Crawler's Jump. Blocks landings until its owner action window expires or it is cleansed.                                            |
| **Arena**                    | A ranked tier in the Trophy Road progression system. 10 Arenas in total.                                                                                                                     |
| **ARPDAU**                   | Average Revenue Per Daily Active User. Key monetization metric.                                                                                                                              |
| **Ataxx**                    | A 1990 abstract strategy board game by Leland Corporation. Pieces clone/jump and convert adjacent enemies. The primary mechanical inspiration for Goo Galaxy.                                |
| **Axial Coordinates (q, r)** | A two-axis coordinate system for hexagonal grids. Enables non-branching mathematical operations for distance/neighbor calculations.                                                          |
| **Clone**                    | A 1-hex range movement. Creates a copy of the unit on the target hex while keeping the original in place. Net +1 piece.                                                                      |
| **Conversion**               | When a unit lands adjacent to enemy pieces, those enemies are flipped to the deploying player's faction. The core mechanic of spatial domination.                                            |
| **COPPA**                    | Children's Online Privacy Protection Act (US). Requires verifiable parental consent for data collection from children under 13. Amended Rule effective April 2026.                           |
| **DAU**                      | Daily Active Users. Players who log in and play at least once per day.                                                                                                                       |
| **Domination**               | An instant win condition — triggered when a player eliminates/converts every enemy piece on the board.                                                                                       |
| **Draft Mode**               | A competitive format where players draft from a shared random pool at normalized card levels. Removes progression advantages.                                                                |
| **Defender Action Window**   | A temporary duration that expires after the affected unit's controller completes their next successful deployment.                                                                           |
| **Draft Seed**               | The deterministic server-generated value used to build the same draft offer sequence for both players in Draft Mode.                                                                         |
| **Energy / Elixir**          | The accumulating resource used to deploy cards. Generates at 1.0/2.8s (standard) or 1.0/1.4s (Overtime).                                                                                     |
| **FMOD**                     | A professional audio middleware tool used for adaptive music and interactive sound design.                                                                                                   |
| **FOMO**                     | Fear Of Missing Out. A psychological driver used in time-limited events and rotating shop offers.                                                                                            |
| **Fragment**                 | A card-specific collectible required (along with Gold) to upgrade a card's level.                                                                                                            |
| **Frozen**                   | A protected status that prevents movement and conversion interactions for its defender action window duration.                                                                               |
| **FTUE**                     | First-Time User Experience. The tutorial and onboarding flow.                                                                                                                                |
| **Galaxy Pass**              | The seasonal Battle Pass. 35 tiers with free and premium tracks. 30-day seasons.                                                                                                             |
| **GDPR**                     | General Data Protection Regulation (EU). Governs personal data collection, storage, and processing.                                                                                          |
| **Hexxagon**                 | A hexagonal variant of Ataxx. Played on a 61-hex grid. Direct inspiration for Goo Galaxy's board geometry.                                                                                   |
| **Jump**                     | A 2-hex range movement. Repositions the unit to the target hex, leaving the source hex empty. Net +0 pieces.                                                                                 |
| **Komi**                     | A mathematical compensation system (inspired by Go) that gives Player 2 bonus starting Energy to offset First Mover Advantage.                                                               |
| **Laboratory**               | The in-game name for Clans/Guilds. A social group of up to 50 players.                                                                                                                       |
| **LGPD**                     | Lei Geral de Proteção de Dados (Brazil). Brazilian data protection law similar to GDPR.                                                                                                      |
| **LiveOps**                  | Live Operations. Ongoing game updates, events, balance patches, and content drops delivered without full client updates.                                                                     |
| **LTV**                      | Lifetime Value. Total revenue expected from a single player over their entire engagement with the game.                                                                                      |
| **MAU**                      | Monthly Active Users. Players who log in at least once per month.                                                                                                                            |
| **MCTS**                     | Monte Carlo Tree Search. An AI algorithm used for simulating game outcomes. Used in balance testing.                                                                                         |
| **MVP**                      | Minimum Viable Product. In this GDD it often refers to the Phase 2 target slice, though a leaner internal cut may be required before external playtesting.                                   |
| **MPS SDK**                  | Unity Multiplayer Services SDK. The preferred session-layer integration point for Lobby, Relay, Matchmaker, and related multiplayer service flows.                                           |
| **NGO**                      | Netcode for GameObjects. Unity's official multiplayer networking framework.                                                                                                                  |
| **Owner Action Window**      | A temporary duration that expires after the effect owner's next successful deployment resolves.                                                                                              |
| **Overtime**                 | A 1-minute sudden death phase triggered when the match score is tied at the end of standard time. Energy generation doubles (2x).                                                            |
| **Score**                    | For timeout and Overtime resolution, score equals the number of troops currently controlled on the board.                                                                                    |
| **P2W**                      | Pay-to-Win. A monetization design where spending real money provides direct competitive advantages. Avoided in Goo Galaxy.                                                                   |
| **$P_v$**                    | Power Value. The theoretical impact budget of a card, calculated as $P_v = k \cdot E^2$.                                                                                                     |
| **ScriptableObject (SO)**    | A Unity data container that exists as a project-level asset. In Goo Galaxy these primarily live under `Assets/Data/*` and hold card definitions, configs, registries, and tuning parameters. |
| **Sealed**                   | A temporary hex-state modifier that blocks Clone/Jump landings on selected empty hexes until its owner action window expires or it is cleansed.                                              |
| **Soft Launch**              | A limited-geography release to test retention, monetization, and server stability before global launch.                                                                                      |
| **SUV Framework**            | Social, Utility, Vanity — the three pillars of ethical F2P monetization.                                                                                                                     |
| **Trophy**                   | The ranked currency earned/lost from competitive matches. Determines Arena placement.                                                                                                        |
| **UGS**                      | Unity Gaming Services. Includes Relay, Lobby, Cloud Save, and Analytics.                                                                                                                     |
| **WCAG**                     | Web Content Accessibility Guidelines. Used as the accessibility standard for color contrast (minimum 4.5:1 ratio).                                                                           |
| **Rooted**                   | A status that prevents a unit's controller from moving that unit until the defender action window expires.                                                                                   |

---

## Key Formulas Reference

### 1. Power-Cost Scaling

$$P_v = k \cdot E^2 \quad (k = 1.0, \text{ normalized to Subject Alpha})$$

### 2. Stat Progression per Level

$$\text{Stat}_{Lv} = \text{Stat}_{Base} \times 1.10^{(Lv - 1)}$$

### 3. Hex Distance (Axial Coordinates)

$$d = \frac{|q_1 - q_2| + |r_1 - r_2| + |(q_1 + r_1) - (q_2 + r_2)|}{2}$$

### 4. Trophy Change per Match

$$\Delta T = T_{base} \times M_{streak} \times M_{arena}$$

### 5. Season Trophy Reset (Soft Reset)

$$T_{new} = 3000 + \frac{T_{current} - 3000}{2}$$

### 6. Energy Generation

$$E_{generated}(t) = \frac{t}{R} \quad \text{where } R = 2.8s \text{ (standard)} \text{ or } 1.4s \text{ (overtime)}$$

### 7. Effective Conversion Rate (Balance Metric)

$$ECR = \frac{\text{Average enemy hexes flipped per deployment}}{E_{cost}}$$

### 8. Timeout Score

$$Score_{player} = \text{controlled troops currently on the board}$$

---

## Launch Roster Quick Reference

| Card               | Cost | Type  | Rarity    | Key Ability                                                                      |
| :----------------- | :--: | :---- | :-------- | :------------------------------------------------------------------------------- |
| Subject Alpha      |  1   | Troop | Common    | Standard conversion. Baseline unit.                                              |
| Acid Crawler       |  2   | Troop | Common    | Jump leaves an acid puddle for 2 owner action windows (area denial).             |
| Bio-Phalanx        |  3   | Troop | Rare      | Armored Membrane — first valid conversion attempt strips armor, second converts. |
| Volatile Mass      |  4   | Troop | Epic      | 2-hex AoE conversion. Cannot Clone. Self-destructs.                              |
| Plasmic Leaper     |  4   | Troop | Epic      | Hover (ignores hazards). Roots newly converted enemies.                          |
| The Apex Strain    |  5   | Troop | Legendary | Seismic push (displaces enemies 1 hex). Immovable.                               |
| Cryo-Stasis        |  2   | Spell | Rare      | Freezes a 3-hex cluster for 1 defender action window. Immune to all interaction. |
| Sterilization Beam |  4   | Spell | Epic      | Vaporizes 4-hex cluster. Ignores all defenses.                                   |

---

## Expansion Prototype Quick Reference

| Card             | Cost | Type  | Rarity | Key Ability                                                                 |
| :--------------- | :--: | :---- | :----- | :-------------------------------------------------------------------------- |
| Quarantine Drone |  3   | Troop | Rare   | Creates up to 2 Sealed adjacent empty hexes for 1 owner action window.      |
| Detox Mycelium   |  3   | Troop | Rare   | Cleanses nearby friendly Frozen/Rooted states and dissolves acid puddles.   |
| Purge Pulse      |  2   | Spell | Rare   | Cleanses Frozen, Rooted, Sealed, and acid puddles in a 3-hex cluster.       |
| Phase Relay      |  3   | Spell | Epic   | Repositions 1 allied troop with a free Jump that still resolves on landing. |

---

## Arena Quick Reference

|  #  | Arena Name       | Trophies  | Key Unlock                      |
| :-: | :--------------- | :-------- | :------------------------------ |
|  1  | Petri Dish       | 0-299     | Subject Alpha, Acid Crawler     |
|  2  | Observation Lab  | 300-599   | Bio-Phalanx                     |
|  3  | Containment Wing | 600-999   | Volatile Mass, Clans            |
|  4  | Mutation Chamber | 1000-1399 | Plasmic Leaper, Shop            |
|  5  | Biohazard Sector | 1400-1799 | Cryo-Stasis, Draft Mode         |
|  6  | Xenobiology Lab  | 1800-2199 | Sterilization Beam, Galaxy Pass |
|  7  | Deep Space Wing  | 2200-2599 | The Apex Strain                 |
|  8  | Apex Research    | 2600-2999 | Epic card pool expansion        |
|  9  | Director's Suite | 3000-3499 | Legendary pool, Tournaments     |
| 10  | The Nexus        | 3500+     | Infinite ladder, Leaderboard    |

---

## Energy System Quick Reference

| Parameter          |   Standard    |   Overtime    |
| :----------------- | :-----------: | :-----------: |
| Generation Rate    | 1.0 / 2.8 sec | 1.0 / 1.4 sec |
| Max Cap            |     10.0      |     10.0      |
| P1 Starting        |      5.0      |       —       |
| P2 Starting (Komi) |      5.5      |       —       |
| Match Duration     |   3:00 min    |   1:00 min    |

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
| 25  | [Gaming Color Palette Combinations (Media.io)](https://www.media.io/color-palette/gaming-color-palette.html)                                                | Cyber Neon palette            |
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
