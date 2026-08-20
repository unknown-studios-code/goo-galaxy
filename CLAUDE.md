# CLAUDE.md

## Project

Goo Galaxy — real-time PvP mobile strategy game. Hex-grid territorial domination (Ataxx/Hexxagon-style) with asymmetrical deck-building and specimen deployment. Sentient alien slime sci-fi theme.

## Tech Stack

Versions come from `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` — read those rather than trusting this table when a version decides something.

| Layer         | Choice                                                                                   |
| :------------ | :--------------------------------------------------------------------------------------- |
| Engine        | Unity 6000.3.18f1, URP 17.3.0                                                            |
| Target        | iOS and Android, IL2CPP, ARM64                                                           |
| DI            | VContainer 1.18.0 (OpenUPM), `GameLifetimeScope` as the composition root                 |
| Multiplayer   | Netcode for GameObjects 2.13.1, distributed authority — no authoritative server          |
| Sessions      | Multiplayer Services SDK (Lobby, Matchmaker, Relay) — specified, not yet in the manifest |
| Input         | Input System 1.19.0                                                                      |
| UI            | UI Toolkit — UXML and USS                                                                |
| Async         | `Awaitable`, always with `destroyCancellationToken`                                      |
| Spawning      | `UnityEngine.Pool.ObjectPool<T>`                                                         |
| Config        | `ScriptableObject` for authored data only, never runtime state                           |
| Tests         | Unity Test Framework 1.6.0, EditMode and PlayMode                                        |
| Editor bridge | `com.unity.pipeline` 0.4.0-exp.1, driving the `unity cmd` surface                        |
| Formatting    | CSharpier, `dotnet format`, Prettier — gated by Husky and `lint-staged`                  |

**Never deviate.** The legacy Input Manager, uGUI, coroutines for delays, and `Instantiate`/`Destroy` churn each have a replacement above; reaching for one of them needs a stated reason.

## Rules

Most rules arrive on their own when you open a file they govern. `build-and-tooling.md` and `unity-editor-automation.md` arrive on nothing and are always opened by path, and **dispatching a subagent hands it no rules — name the paths it must open in the prompt.**

| Rule                                | Covers                                                                                          | Use it when                                                                   |
| :---------------------------------- | :---------------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------- |
| `build-and-tooling.md`              | Prerequisites, the `npm` format and check chain, the Husky pre-commit sequence, secret scanning | Committing, formatting, or a hook or the CI Format Check fails                |
| `unity-class-organization.md`       | File layout and the mandated member order for every type, test fixtures included                | Adding a type, or deciding where a member goes inside one                     |
| `unity-code-documentation.md`       | XML doc scope, tooltips, comments that earn their place, log text, GIVEN-WHEN-THEN              | Weighing whether something needs a doc, a tooltip, or a comment at all        |
| `unity-code-style.md`               | Formatting, naming, fields, properties, events, async, pooling, type suffixes                   | Naming anything, or picking the suffix that declares a type's role            |
| `unity-debugging.md`                | Diagnostic order, Unity null semantics, lifecycle timing, Input System, physics, async          | Something throws, misfires, or never fires, and the cause is unknown          |
| `unity-design-patterns.md`          | SOLID, VContainer DI, `MatchEvents`, State, Template Method, Command, pooling                   | Choosing a pattern, registering a dependency, or adding an event              |
| `unity-editor-automation.md`        | The `unity cmd` surface, tests and builds through the open editor, why batch mode is banned     | Running tests or a build, or changing a scene, prefab, asset or setting       |
| `unity-netcode.md`                  | Authority topology, ownership, `NetworkVariable` vs RPC, session, matchmaking, relay            | Writing anything replicated, or chasing a desync                              |
| `unity-performance-optimization.md` | What counts as a hot path, allocations, caching, collections, physics, rendering, the LINQ ban  | Writing code that runs per frame, per tick, or per tile on a repeating pass   |
| `unity-project-configuration.md`    | Play mode and domain reload, static resets, assemblies, build profiles, IL2CPP                  | Adding an assembly, holding static state, or touching a build or play setting |
| `unity-testing.md`                  | EditMode vs PlayMode, naming, GIVEN-WHEN-THEN, doubles, determinism, assembly wiring            | Writing a test, or a suite fails and the test itself is suspect               |
| `unity-ui-toolkit.md`               | USS/CSS differences, BEM, flexbox, binding, custom elements, `ListView` virtualization          | Building a screen, or an element will not lay out, style, or receive a click  |

## Skills

Work runs Notion (GOO\*) → branch (`feat/GOOM-1`, `fix/GOOE-42`) → commits → PR → merge. The skills perform each step rather than draft text for it, and they own the conventions — Conventional Commits with a **mandatory scope** among them — so invoke the skill instead of hand-formatting.

| Skill                | Does                                                                                  | Invoke when                                         |
| :------------------- | :------------------------------------------------------------------------------------ | :-------------------------------------------------- |
| `/refine-task`       | Writes a task, story, epic or bug into the Notion database from the project templates | Scoping work before any code is written             |
| `/start-task`        | Grounds a task against the real repo, then picks and sequences the specialist agents  | Beginning implementation of a GOO\* task            |
| `/track-task`        | Looks up and updates GOOE/GOOS/GOOT/GOOM pages                                        | A task ID is mentioned, or its status must move     |
| `/create-commit`     | Stages and commits with the mandatory scope, footer trackers, multi-line handling     | Committing anything                                 |
| `/open-pull-request` | Opens, updates and labels the PR, with the template body and Notion sync              | Opening or updating a pull request                  |
| `/read-gdd`          | Resolves a GDD chapter to its Notion page and fetches it                              | Design intent is needed, or a chapter must be cited |

## Agents

**Pass an explicit model tier for every subagent you dispatch.** The `Agent` tool's `model` parameter overrides the agent's frontmatter; omitting it inherits the session model — `opus` here — and silently promotes routine work. Choose from the complexity of that agent's slice, not the agent's identity: `haiku` for mechanical, fully-specified work (renames, applying findings someone already wrote out, `.asmdef` scaffolding, mirroring an existing test); **`sonnet` as the default** for ordinary implementation inside established patterns; `opus` for architecture and assembly-boundary decisions, root-causing intermittent or desync defects, balance math, and reviewing a large diff. The four read-only auditors below pin `model: opus` in their frontmatter and are never dispatched lower, because a silent false negative from the last line of defense costs far more than the tokens it saved. Delegate a whole discipline to one agent rather than spreading a task across several, and state the tier next to the roster so the choice is visible.

| Agent                      | Owns                                                                                    | Dispatch when                                          |
| :------------------------- | :-------------------------------------------------------------------------------------- | :----------------------------------------------------- |
| `dependency-doctor`        | Packages, asmdef compile errors, stale csproj, Roslyn analyzers, npm and Husky tooling  | Plumbing blocks the build                              |
| `game-balance-analyst`     | Capture and flip math, power budgets, energy curves, pacing, reward rates, pricing      | Numbers need modelling; it does not implement systems  |
| `gdd-steward`              | The 12 GDD chapters in Notion, and drift between them and the repo                      | The design doc must change or be audited               |
| `release-engineer`         | GitHub Actions, IL2CPP build profiles, licence and Library caching, LFS, rulesets       | CI or a player build needs work                        |
| `shader-vfx-artist`        | URP Shader Graph and HLSL, goo surfaces, VFX Graph, variant and quality-tier budgets    | An effect must be built, or is too expensive on mobile |
| `task-planner`             | Turning a rough idea, bug report or request into a refined Notion page                  | A request needs scoping; it does not implement         |
| `unity-bug-hunter`         | Nulls, lifecycle order, static state, Input System, physics, async cancellation, desync | Something misbehaves and the cause is unknown          |
| `unity-code-reviewer`      | Style, ordering, naming, doc scope, patterns, assembly direction, correctness           | A diff needs review before commit or PR — reports only |
| `unity-doc-auditor`        | XML doc scope, comments that narrate or contradict, tooltips, log text, test structure  | Documentation must be audited — reports only           |
| `unity-editor-tooling`     | Inspectors, drawers, EditorWindows, postprocessors, validation and batch asset passes   | Editor tooling is needed; never runtime gameplay       |
| `unity-gameplay-engineer`  | Runtime gameplay in any feature assembly, and scaffolding a new one                     | Implementing or refactoring gameplay                   |
| `unity-netcode-engineer`   | NGO, authority, `NetworkVariable` vs RPC, ownership, lobby, relay, matchmaking          | Any multiplayer work                                   |
| `unity-perf-auditor`       | Allocations, LINQ in hot paths, uncached lookups, boxing, draw calls, IL2CPP pitfalls   | Mobile performance must be audited — reports only      |
| `unity-structure-auditor`  | File layout, using directives, namespace shape, the mandated member order               | Class organization must be audited — reports only      |
| `unity-test-author`        | EditMode and PlayMode suites, GIVEN-WHEN-THEN, `InternalsVisibleTo` and asmdef wiring   | Tests must be written, improved, or run                |
| `unity-uitoolkit-engineer` | UXML, USS, custom elements, binding, `ListView`, HUD and menus, safe area               | Any UI work                                            |

## Boundaries

- **`.asset`, `.meta`, `.prefab` and `.unity` are the editor's to write.** The `deny` rules in `.claude/settings.json` block the byte-level write to protect GUIDs — a denial there is policy, and it means "use the editor", not "this cannot be done". Reach the editor with `unity cmd <command>` in the shell; there is no Unity MCP server on this project. Prefer the named command over `eval`, and read the value back before asserting it landed. Full guidance in `.claude/rules/unity-editor-automation.md`.
- **`goo-galaxy.slnx` and the per-assembly `.csproj` are Unity-generated and untracked** — change the `.asmdef`, never them, and see Rule 3a in `unity-editor-automation.md` for keeping them current.
- **`ProjectSettings/` and render pipeline assignment stay the user's call.** Ask before changing Graphics/Quality settings or anything project-wide.
- **Never commit, push, or open a PR** unless asked.
