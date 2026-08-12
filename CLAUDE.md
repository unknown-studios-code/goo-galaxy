# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Goo Galaxy — real-time PvP mobile strategy game (Unity 6000.3.18f1, URP 17.3). Hex-grid territorial domination (Ataxx/Hexxagon-style) with asymmetrical deck-building and specimen deployment. Sentient alien slime sci-fi theme. Targets iOS and Android via IL2CPP.

## Commands

Prerequisites: Unity 6000.3.18f1, Node.js (`npm` scripts and Husky), the .NET SDK (`dotnet tool restore` pulls CSharpier), and **Docker Desktop** — the pre-commit hook shells out to a container to scan staged changes for secrets and fails the commit outright if the daemon is not reachable.

```powershell
npm install            # runs husky + dotnet tool restore via the prepare script
npm run format         # csharpier + prettier, rewriting in place — run before every commit
npm run check          # csharpier + editorconfig + prettier, verify only — what the CI Format Check runs
```

Run `format` first, then `check`: `format` fixes what a formatter can, and `check` reports what is left. Per-formatter variants exist as `format:csharpier`, `format:prettier` and the matching `check:*`. `check:editorconfig` has no `format:` counterpart — the tool only verifies, so anything it flags (indentation, line endings, the 160-character limit) has to be fixed by hand.

**There is deliberately no `dotnet format`.** It needs the Unity-generated `.csproj`/`.slnx`, which are untracked, so it cannot run in CI or on a fresh clone; and in write mode a csproj that is stale relative to the `.asmdef` files makes it delete `using` directives it wrongly reads as unused.

The Husky `pre-commit` hook checks that Docker is reachable, then runs `lint-staged`, then the two secret gates (see below). **`lint-staged` works on the staged files only** — CSharpier then editorconfig-checker on staged C#, Prettier on staged JSON/Markdown/YAML — and re-stages what it rewrote, so the formatter's output lands in the commit. It hides unstaged hunks while it runs, so a partially staged file keeps the hunks you deliberately left out. Its config lives under `lint-staged` in `package.json`.

The order is deliberate at both ends. Docker is checked first because it is the one gate answerable without the index, so a stopped daemon fails in milliseconds instead of after the formatters run. The secret gates run last because `lint-staged` is what finally settles the index, and scanning any earlier would examine pre-formatter content — or miss a file the formatter restaged.

The trade is that the hook no longer verifies the **whole repository**: a violation in a file you did not stage now reaches CI instead of failing locally. That is what CI's Format Check is for, and `npm run check` covers it locally on demand.

**Do not disable the hooks with `HUSKY=0`.** It used to be the routine path for agent-authored commits, because the Commitizen `prepare-commit-msg` hook opened an interactive prompt that hung any non-interactive caller. That hook now exits immediately whenever git already has a message — which `git commit -m` always does — so the prompt only appears for a bare `git commit`. `HUSKY=0` today buys nothing and costs the formatting and secret gates.

**Run tests and builds through the open editor, never through batch mode.** `run_tests` and `build` drive the running editor and report back; `Unity.exe -batchmode` needs the project lock, forces the editor closed, and rewrites render pipeline and project settings as a side effect. See `.claude/rules/unity-editor-automation.md`. CI runs both suites on PRs.

**Secret scanning runs on every commit and in CI.** The `pre-commit` hook and `.github/workflows/secret-scan.yml` both run [Betterleaks](https://github.com/betterleaks/betterleaks) as a container, digest-pinned — `ghcr.io/betterleaks/betterleaks@sha256:16f903f0100ce7358ef1f870858777e55bec94cf04c6b65c45d013274ea3311c`, never a tag, never `:latest` — plus a filename-extension gate for secret-shaped files (`.key`, `.pem`, `.p12`, `.pfx`, `.keystore`, `.jks`, `.mobileprovision`, `.cer`, `.p8`, sourced from `.github/rulesets/push-rulesets/01-sensitive-files-protection.json`). If the hook fails on the Docker check, start Docker Desktop and retry — it fails the commit rather than skipping the scan, on purpose, so never work around it by uninstalling or ignoring the hook. If it fails on an actual finding, treat it as a real leak until proven otherwise: fix the content or rotate the credential. Never add `--redact` removal, `|| true`, `continue-on-error`, or a `.betterleaksignore`/`.betterleaks.toml` to force it green — ask first if you believe a finding is a false positive.

## Architecture

Runtime code is split into one assembly per feature domain — `Assets/Scripts/Runtime/{Feature}/` with an `.asmdef` named `GooGalaxy.Runtime.{Feature}`. The set grows over time, so list the folder instead of assuming it. `Runtime.Shared` is the dependency-free leaf every other assembly may reference and must never depend on a feature assembly; `Runtime.Core` holds DI. Editor assemblies (`GooGalaxy.Editor.*` under `Assets/Editor/`) depend on `Editor.Shared` and are never referenced by runtime code. Tests live in `Assets/Scripts/Tests/{EditMode,PlayMode}/`, reaching internals through `InternalsVisibleTo` rather than widened access modifiers.

**Established patterns** — `MatchEvents` (static event bus for in-match facts, in `Runtime.Shared`; publishers call `Raise*`), **VContainer** DI with `GameLifetimeScope` as the composition root in `Runtime.Core` (constructor injection for plain classes, `[Inject]` methods for MonoBehaviours — never a Service Locator), MVP split into `Models/`, `Views/`, `Presenters/` plus stateless `Services/` per feature (`*Controller` is reserved for gameplay/system control such as `PlayController` or a camera rig, never for a view mediator), composition over inheritance, and `ScriptableObject` for authored config only — never runtime state (suffix `*SO`/`*DataSO`).

**Tech choices, never deviate** — Unity Input System (not the legacy Input Manager), UI Toolkit (not uGUI), `Awaitable` instead of coroutines for delays and sequencing, `UnityEngine.Pool.ObjectPool<T>` for frequent spawn/despawn, Netcode for GameObjects plus Unity Multiplayer Services.

## Conventions

Detailed rules live in `.claude/rules/`. Most carry `paths:` frontmatter so they load when you touch the files they govern (`Assets/**/*.cs`, `.uxml`/`.uss`, `.asmdef`); `unity-editor-automation.md` deliberately has none, because how to reach the editor is not triggered by opening a particular file. Read the matching file before writing or reviewing code — **subagents do not receive them automatically and must open them by path.**

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

- **Never write `.asset`, `.meta`, `.prefab`, or `.unity` files directly.** Unity authors them; writing the bytes from an agent corrupts GUIDs and serialized references. The `deny` rules in `.claude/settings.json` enforce this — a denial there is policy, not a bug.
- **Changing those assets through the Unity MCP is allowed and is the expected path**, because the editor writes the files and GUIDs stay valid. Prefer the named tool — `set_serialized_field`, `set_component_properties`, `move_asset`, `delete_asset` — over `eval`, and read the value back with a different tool before asserting it landed. A denial on a direct write means "use the editor", not "this cannot be done". With no MCP connected, `unity status` says whether the editor is alive before you conclude anything. Full guidance in `.claude/rules/unity-editor-automation.md`.
- **`ProjectSettings/` and render pipeline assignment stay the user's call.** Ask before changing Graphics/Quality settings or anything project-wide.
- **Never commit, push, or open a PR** unless asked.

## Gotchas

- **`.slnx`** — new .NET XML solution format; some IDE versions and external tools don't recognize it. If tooling complains, open the `.slnx` directly.
- **Root `.csproj` files** are Unity-generated — never hand-edit them; change the `.asmdef` instead.

## GDD

The game design doc is the design source of truth — pitch, mechanics, math/balance, troops, economy, meta-game, art, audio, tech architecture, MVP roadmap, ops/legal. Read the governing chapter before designing a feature, and keep it in sync when project structure changes.

**It lives in Notion**, as 12 pages in the [Documentation wiki](https://app.notion.com/p/31b56d55129b801aa007d27114249b81) — one per chapter, tagged and cross-linked. There is no copy in the repository, so it is never grepped: every read is a fetch and every citation is a link.

The `read-gdd` skill is the index. It carries the chapter-to-URL table and says which chapter governs what, so a lookup costs one fetch instead of a search — invoke it rather than hunting for a page. Cite a chapter with `<mention-page>` inside Notion and with its URL everywhere else; **`gdd-steward` owns edits**, and they are made to the page, never to a local copy. Refinement documents follow the same rule — `/refine-task` creates them in the Notion task database, not on disk.
