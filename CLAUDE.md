# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Goo Galaxy — real-time PvP mobile strategy game (Unity 6000.3.18f1, URP 17.3). Hex-grid territorial domination (Ataxx/Hexxagon-style) with asymmetrical deck-building and specimen deployment. Sentient alien slime sci-fi theme. Targets iOS and Android via IL2CPP.

## Commands

```powershell
npm install            # runs husky + dotnet tool restore via the prepare script
npm run format         # csharpier + dotnet format + prettier — run before every commit
npm run check          # verify-only variants of the same three (what format-check CI runs)
```

Per-formatter variants exist as `format:csharpier`, `format:dotnet`, `format:prettier` and the matching `check:*`.

The Husky `pre-commit` hook runs `npm run format`, but commits created with `HUSKY=0` (see the `create-commit` skill) skip it — format explicitly in that path.

**Never run tests, launch Unity, or start a build.** There is no local test CLI: the user runs EditMode and PlayMode suites from the Unity Test Runner and reports the results. Finish by naming the tests worth running. CI runs them on PRs.

## Architecture

Runtime code is split into one assembly per feature domain — `Assets/Scripts/Runtime/{Feature}/` with an `.asmdef` named `GooGalaxy.Runtime.{Feature}`. The set grows over time, so list the folder instead of assuming it. `Runtime.Shared` is the dependency-free leaf every other assembly may reference and must never depend on a feature assembly; `Runtime.Core` holds DI. Editor assemblies (`GooGalaxy.Editor.*` under `Assets/Editor/`) depend on `Editor.Shared` and are never referenced by runtime code. Tests live in `Assets/Scripts/Tests/{EditMode,PlayMode}/`, reaching internals through `InternalsVisibleTo` rather than widened access modifiers.

**Established patterns** — `MatchEvents` (static event bus for in-match facts, in `Runtime.Shared`; publishers call `Raise*`), **VContainer** DI with `GameLifetimeScope` as the composition root in `Runtime.Core` (constructor injection for plain classes, `[Inject]` methods for MonoBehaviours — never a Service Locator), MVP split into `Models/`, `Views/`, `Presenters/` plus stateless `Services/` per feature (`*Controller` is reserved for gameplay/system control such as `PlayController` or a camera rig, never for a view mediator), composition over inheritance, and `ScriptableObject` for authored config only — never runtime state (suffix `*SO`/`*DataSO`).

**Tech choices, never deviate** — Unity Input System (not the legacy Input Manager), UI Toolkit (not uGUI), `Awaitable` instead of coroutines for delays and sequencing, `UnityEngine.Pool.ObjectPool<T>` for frequent spawn/despawn, Netcode for GameObjects plus Unity Multiplayer Services.

## Conventions

Detailed rules live in `.claude/rules/`, scoped with `paths:` frontmatter so each one loads when you touch the files it governs (`Assets/Scripts/**/*.cs`, `.uxml`/`.uss`, `.asmdef`). Read the matching file before writing or reviewing code — **subagents do not receive them automatically and must open them by path.**

The rules that get violated most:

- **`_camelCase`** private fields, **`PascalCase`** everything else (never `UPPER_CASE` constants); Allman braces, 160-char lines
- **No allocations, LINQ, or `Camera.main` in update loops** — cache, pool, reuse
- **`Awaitable` methods take an `Async` suffix**, coroutines a `Co` suffix — always pass `destroyCancellationToken`
- **Never `is null` / `is not null` on `UnityEngine.Object`** — use `== null`
- **XML docs only** for interfaces, abstract members, cross-assembly public APIs, and generic utilities
- **Tests use GIVEN-WHEN-THEN** with literal `// GIVEN`, `// WHEN`, `// THEN` comments and `MethodUnderTest_Scenario_ExpectedOutcome` names

## Workflow

Notion task (GOO\*) → branch (`feat/GOOM-1`, `fix/GOOE-42`) → commits → PR → merge. Commit and PR titles require Conventional Commits with a **mandatory scope**: `type(scope): subject`, subject lowercase and ≤72 chars.

The project skills own each step (`/create-commit`, `/open-pull-request`, `/refine-task`, `/start-task`, `/track-task`) — invoke them instead of hand-formatting; they perform the action, not just draft text. `/start-task` picks and sequences the specialist subagents in `.claude/agents/`; delegate a whole discipline to one of them rather than spreading a task across several.

**Pass an explicit model tier for every subagent you dispatch.** The `Agent` tool's `model` parameter overrides the agent's frontmatter; omitting it inherits the session model, and the lead here runs on `opus`, so an omitted tier silently promotes routine work. Choose from the complexity of that agent's slice, not the agent's identity: `haiku` for mechanical, fully-specified work (renames, applying findings someone already wrote out, `.asmdef` scaffolding, mirroring an existing test); **`sonnet` as the default** for ordinary implementation inside established patterns; `opus` only for architecture and assembly-boundary decisions, root-causing intermittent or desync defects, balance math, and reviewing a large diff. The two read-only analysts are the exception — `unity-perf-auditor` and `unity-code-reviewer` pin `model: opus` in their frontmatter and are never dispatched lower, because a silent false negative from the last line of defense costs far more than the tokens it saved. State the tier next to the roster so the choice is visible.

## Boundaries

- **Never create or edit `.asset` or `.meta` files.** Unity authors them; writing them from an agent corrupts GUIDs and serialized references. Give step-by-step in-editor instructions instead (menu path, fields, values). The `deny` rules in `.claude/settings.json` enforce this — a denial there is policy, not a bug: switch to manual editor instructions rather than looking for another way to write the file.
- **Never commit, push, or open a PR** unless asked.

## Gotchas

- **`.slnx`** — new .NET XML solution format; some IDE versions and external tools don't recognize it. If tooling complains, open the `.slnx` directly.
- **Root `.csproj` files** are Unity-generated — never hand-edit them; change the `.asmdef` instead.

## GDD

The game design doc in `.docs/GDD/` (12 chapters, `00`–`11`) is the design source of truth — pitch, mechanics, math/balance, troops, economy, meta-game, art, audio, tech architecture, MVP roadmap, ops/legal. Read the governing chapter before designing a feature, and keep it in sync when project structure changes.
