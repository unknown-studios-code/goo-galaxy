# Goo Galaxy — Copilot Instructions

## Project

Goo Galaxy — real-time PvP mobile strategy game (Unity 6.3.4f1 LTS, URP 17.3). Hex-grid territorial domination (Ataxx/Heggagon-style) with asymmetrical deck-building and specimen deployment. Sentient alien slime sci-fi theme. Targets iOS and Android via IL2CPP.

## Project Skills

Project-specific skills live in `.github/skills/`:

- `commit-messages` — Conventional Commits formatting with tracker footers
- `pull-requests` — PR creation with template bodies and label assignment
- `task-refinement` — structured task/story/epic/bug templates
- `task-tracking` — Notion task lookup/update via GOOE/GOOS/GOOT/GOOM IDs

Use these skills directly (e.g. `/commit-messages`, `/task-tracking`) rather than hand-formatting.

## Workflow

Feature work flows: Notion task (GOO\*) → branch → commits → PR → merge. Use project skills at each step to keep formatting and tracking consistent. Branch naming: `feat/GOOM-1`, `fix/GOOE-42`, etc.

## High-Level Architecture

```
Assets/Scripts/Runtime/
├── Board/         — Board simulation, tile views, hex logic, commands
├── Networking/    — Netcode for GameObjects (NGO) integration, session flow
└── Shared/        — Cross-feature contracts, helpers, services
```

Each Runtime folder is a separate `.asmdef` assembly (`GooGalaxy.Runtime.<Feature>`), created on demand when a feature needs code. Future features (match orchestration, card logic, HUD, etc.) are scaffolded when needed, not pre-allocated. Editor tooling lives under `Assets/Editor/` with its own assemblies (Automation, Build, Importing, Inspectors, Menus, Shared, Validation, Windows).

**Key patterns** (detailed in [unity-design-patterns.instructions.md](instructions/unity-design-patterns.instructions.md)):

- **StaticGameEvents** — centralized static event bus (Observer)
- **UITKBaseClass** — Template Method base for all UI Toolkit views (MVP pattern)
- **ServiceLocator / DependencyInjector** — runtime DI
- **Enum-based state machines** — UI/view state (class-based for complex AI)
- **Composition** — GameObjects assembled from focused components (MapTile + MapTileMilitary + …)
- **ScriptableObject data** — authored config, not runtime state (suffix `*SO`/`*DataSO`)

**Tech choices** (never deviate):

- Unity Input System (new) — not legacy Input Manager
- UI Toolkit — not uGUI (BEM naming, USS variables on `:root`, no hex colors)
- `Awaitable` over coroutines for delays/sequencing (Unity 6+)
- `UnityEngine.Pool.ObjectPool<T>` for frequent spawn/despawn
- Netcode for GameObjects (NGO) + Unity Multiplayer Services

## Design & Code Conventions

**All detailed rules are in `.github/instructions/` — they auto-apply when editing `Assets/Scripts/**/\*.cs`:\*\*

| Instructions file                                | Covers                                                                                                |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| `unity-code-style.instructions.md`               | Formatting, naming, class organization, braces, fields, methods, async, pooling                       |
| `unity-code-documentation.instructions.md`       | XML comments rules, Unity inspector tooltips, self-documenting code, inline comments                  |
| `unity-class-organization.instructions.md`       | Strict member ordering for classes, MonoBehaviours, ScriptableObjects, structs, records, interfaces   |
| `unity-design-patterns.instructions.md`          | Observer, State, Template Method, Singleton, Service Locator, Composition, Factory, Command, Strategy |
| `unity-performance-optimization.instructions.md` | Update loop rules, allocation avoidance, caching, physics, rendering, LINQ ban                        |
| `unity-debugging.instructions.md`                | Diagnostic priority, null refs, lifecycle, Input System, physics, animation                           |
| `unity-ui-toolkit.instructions.md`               | USS/CSS differences, BEM, data binding, MVP, custom elements, ListView                                |
| `unity-project-configuration.instructions.md`    | Domain reload, Burst, asset presets, URP tiers, asmdefs                                               |

Quick hits:

- **Allman braces**, 160-char line width, 4-space indent, LF line endings
- **`_camelCase`** private fields, **PascalCase** properties/methods/types
- **No allocations in Update loops** — cache, pool, reuse
- **No LINQ in hot paths**, no `Camera.main` every frame
- **`Awaitable` Async suffix**, coroutines `Co` suffix
- **Early returns** over nested `if`
- **XML Documentation** — write XML comments only for interfaces, abstract members, cross-assembly public APIs, and generic utilities/extensions (see [unity-code-documentation.instructions.md](instructions/unity-code-documentation.instructions.md))
- **Testing Pattern** — all tests must follow the GIVEN-WHEN-THEN structure using `// GIVEN`, `// WHEN`, and `// THEN` comments

## Gotchas

- **`.slnx`** — new .NET XML solution format; some IDE versions and external tools don't recognize it. If tooling complains, open the `.slnx` directly.
- **Husky** — git hooks managed by Husky. If commits fail on format checks, run `npm run format` and retry.

## Agent Boundaries

- **Never create `.asset` or `.meta` files.** These are authored/generated through the Unity Editor. When a task calls for ScriptableObject instances or other assets, only provide step-by-step guidance (menu path, fields to fill, values) for the user to create them manually in-editor.
- **Never run tests.** Do not invoke test runners or test tools (e.g. `runTests`) on the user's behalf. The user always runs tests manually.

## Conventional Commits

PRs require Conventional Commits format: `type(scope): subject`. Scopes are mandatory. Types: feat, fix, docs, style, refactor, perf, test, chore, ci, build, revert.

## GDD

Full game design doc in `.docs/GDD/` — 12 chapters covering mechanics, math/balance, specimens, economy, meta-game, art, audio, tech architecture, MVP roadmap, ops/legal. Read relevant chapters before designing features. Keep GDD chapters in sync when project structure changes.
