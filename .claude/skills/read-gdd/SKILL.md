---
name: read-gdd
description: >-
  Resolve a Goo Galaxy Game Design Document chapter to its Notion page and fetch it. Use whenever design intent is needed — mechanics, balance numbers, card stats, economy, art direction, audio, technical architecture, roadmap, legal — or whenever a task, PR, or commit needs to cite a chapter. Carries the chapter-to-URL table so a lookup costs one fetch instead of a search.
---

# Goo Galaxy: Read the GDD

The Game Design Document is **12 Notion pages** in the Documentation wiki, one per chapter. It is the design source of truth: mechanics, balance, the card roster, economy, meta-game, art, audio, technical architecture, MVP roadmap, and ops/legal.

There is no copy in the repository. Every read is a fetch against the URL below, and every citation is a link to it.

## Chapter Table

Pick the chapter that **governs** the question, fetch it, and stop. Fetching all twelve to answer one question wastes a large amount of context — the chapters run 200–570 lines each.

| Chapter                                     | Governs                                                                                                                                                                                                                                                                      | Notion URL                                                |
| :------------------------------------------ | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :-------------------------------------------------------- |
| **Pitch & Overview**                        | Vision, USP, core loop, KPI targets, monetization model at the headline level, the chapter index                                                                                                                                                                             | `https://app.notion.com/3b856d55129b81918a37f8ab9416087e` |
| **Mechanics & Core Gameplay**               | Board geometry, Deploy/Clone/Jump, conversion rules, action windows, interaction resolution order, Energy parameters, Kit and hand rules, controls, match flow                                                                                                               | `https://app.notion.com/3b856d55129b81669164c91aeb4cbe77` |
| **Mathematics & Balancing**                 | The `P_v = E^2` budget, Impact Profile caps, ECR and validation method, progression curve, rarity, Komi, DP formula, matchmaking, balance dashboard thresholds                                                                                                               | `https://app.notion.com/3b856d55129b814687e2dbe013ec8ae1` |
| **Specimens, Protocols & Factions**         | The full card roster and every stat block, counter matrix, Kit archetypes, landing-impact targeting, the `CardDataSO` authored schema, impact types still to come, the expansion roadmap. **Factions are undesigned** — the word means only specimen ownership               | `https://app.notion.com/3b856d55129b81d99ea9fd13ff4187e4` |
| **Economy & Monetization**                  | Anti-P2W rules, the two progressions, dual currency, capsules, Galaxy Pass, pricing, revenue scenarios, cosmetics                                                                                                                                                            | `https://app.notion.com/3b856d55129b8180a594d824ee178793` |
| **Meta-Game, Retention & LiveOps**          | Retention horizons, FTUE, contracts, Crews, weekend events, Blind Discovery, seasonal calendar, push notifications                                                                                                                                                           | `https://app.notion.com/3b856d55129b81cda2fee48d89da7a1a` |
| **Art Direction & UX**                      | Theme, the Cosmic Neon palette and its WCAG ratios, character and specimen design, screen flow, HUD zoning, accessibility, art asset naming                                                                                                                                  | `https://app.notion.com/3b856d55129b81ddb106cabe6fe3d140` |
| **Audio & Sound Design**                    | Audio philosophy, FMOD choice and integration, adaptive music parameters, the SFX catalog, mobile audio budget, audio state machine                                                                                                                                          | `https://app.notion.com/3b856d55129b81d0897ffc603a13c33b` |
| **Technical Architecture & Multiplayer**    | Engine and stack, folder and assembly conventions, class ownership, data flow, NGO and MPS networking, performance budgets, CI/CD, branch strategy, QoE thresholds                                                                                                           | `https://app.notion.com/3b856d55129b8150b24ee9eaa76020bf` |
| **MVP & Roadmap**                           | Lean MVP and Alpha scope, the five production phases and their gates, post-launch sequencing, kill-switch and pivot criteria, staffing, risk matrix                                                                                                                          | `https://app.notion.com/3b856d55129b8161b849e3aef859cff0` |
| **Operations, Security & Legal Compliance** | GDPR/LGPD/CCPA, COPPA and the age gate, loot box disclosure and regional variants, anti-cheat, analytics events, soft launch, incident response, and the **secret-scanning security model** — push protection vs. Betterleaks coverage, and the credential-exposure response | `https://app.notion.com/3b856d55129b81e68975e0844955aedf` |
| **References & Appendix**                   | The canonical glossary — the naming authority for the whole GDD — plus key formulas, quick-reference tables for roster, Star Systems, and Energy, and the bibliography every other chapter cites                                                                             | `https://app.notion.com/3b856d55129b8116995ee6a66fa7e300` |

## How to Read

1. **Match the question to one chapter** using the Governs column. When two look plausible, the more specific one wins: a card's stat block is chapter 03 even though balance theory is chapter 02.
2. **Fetch that page** with the Notion MCP fetch tool, passing the URL from the table.
3. **Follow the mentions.** Chapters cross-link each other with `<mention-page>`; a fetched page's mentions carry the URLs, so a follow-up read needs no lookup here.

**A page ID that fails to resolve is not a reason to guess.** Re-resolve it against the wiki database at `https://app.notion.com/p/31b56d55129b801aa007d27114249b81` — query its data source (`collection://31b56d55-129b-816c-ae28-000b2669064a`) rather than searching by name, because the database titles itself **Documentation** while every chapter's `ancestor-path` reports it as **Engineering Wiki**, so a name search matches one or the other depending on which string you picked. Report the new URL so this table can be corrected. A recreated page gets a new ID.

**Precondition:** this needs a connected Notion MCP server. Without one, say the GDD is unreachable and what has to be connected — never answer a design question from memory, and never invent a number, a cost, or a rule.

## How to Cite

Where the citation lands decides its form. Both render as dead text in the wrong place:

| Writing into                            | Use                                                                 |
| :-------------------------------------- | :------------------------------------------------------------------ |
| A Notion page (task, story, epic, GDD)  | `<mention-page url="…">Chapter Title</mention-page>`                |
| A PR body, commit message, code comment | An ordinary Markdown link or bare URL                               |
| Chat, when the user asked a question    | The chapter title plus the specific claim — a link only if it helps |

Never cite a chapter by a repository path. The GDD is not in the repository, and a path resolves to nothing for anyone reading a task or a PR.

## How to Update

Design changes are edits to the **Notion page**, through the Notion MCP. `gdd-steward` owns that work and the consistency rules that come with it — chapter set is fixed, cross-references stay accurate, numbers come from chapters 02 and 03 or from the user.

Do not create a local copy of a chapter to edit and re-upload. Update the page.
