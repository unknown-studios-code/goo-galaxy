---
description: "Use when any task touches the running Unity Editor — running tests, building, editing scenes, prefabs, assets or project settings, or waiting for a recompile. Covers the `unity cmd` command surface, the rest of the Unity CLI, and why batch mode is banned."
---

# Unity Editor Automation

## 1. Overview

The Editor is the authority for everything under `Assets/` and `ProjectSettings/`. It owns the GUIDs, the import pipeline, and the serialized form of every asset, so any change to that content goes **through** a running Editor rather than around it. Everything else in the repository is plain text you edit directly.

The Editor reaches you over one surface, and two others must never touch this project:

| Surface                                          | Provided by                                          | Use for                                                      |
| :----------------------------------------------- | :--------------------------------------------------- | :----------------------------------------------------------- |
| **`unity cmd <command>`**                        | `com.unity.pipeline`, over the local HTTP bridge     | Every operation inside the Editor                            |
| **`unity <subcommand>`** (`status`, `doctor`, …) | the standalone `unity` binary                        | Questions the Editor cannot answer about itself. See Rule 11 |
| **`unity test` / `unity build` / `unity run`**   | the same binary, but they spawn **their own** Editor | **Never.** See Rule 2                                        |
| **`Unity.exe -batchmode`**                       | —                                                    | **Never.** See Rule 2                                        |

There is no Unity MCP server on this project. `unity cmd` reaches the same bridge the MCP server used to wrap — same Editor, same port — so every command name that existed as an `mcp__unity__*` tool exists verbatim as a `unity cmd` argument. Coverage is exact: `unity list` reports 140 commands, one per former tool, with none missing on either side.

The Editor stays open. Do not ask for it to be closed, and do not design a workflow that requires it.

## 2. The Call Shape

```powershell
unity cmd <command> [--param value ...] --no-banner --json
```

`unity list --json` is the **authoritative schema**: it returns every command with its parameters, each carrying `name`, `type`, `required`, `default`, and a description. Read it before guessing a parameter name — the plain `unity list` output is only name, group, and description, so the schema is visible under `--json` and nowhere else.

Every response is a JSON envelope. The payload nests as `data.result`, and `data.parameters` echoes **what the CLI actually parsed** — the one place a dropped argument becomes visible:

```json
{
  "success": true,
  "data": {
    "command": "find_gameobjects",
    "parameters": { "name": "Main Camera" },
    "result": { "count": 1, "gameObjects": [ ... ] },
    "target": { "port": 7800, "projectPath": "D:\\Unknown\\Projects\\Unity\\goo-galaxy" }
  },
  "errors": []
}
```

`--timeout <seconds>` defaults to 30 and is per call. It is **consumed by the CLI and never forwarded** — it does not appear in the echoed `parameters`, so a command that declares its own `timeout` (`run_tests` does, defaulting to 300) cannot have it set through `unity cmd`; the flag only governs how long the CLI waits on the HTTP response. Raise it for a command that genuinely takes longer; do not raise it to paper over a bridge that is down.

The bridge port is **not stable for the life of the Editor** — one session here moved from 7800 to 7801 across a play-mode domain reload, same PID. `unity cmd` and `unity status` rediscover it every call, which is why neither ever takes a port. Never hardcode one, and never talk to the bridge with `curl`.

## 3. Cross-References

- **Project Configuration** → [unity-project-configuration.md](unity-project-configuration.md) (Assembly definitions, Build Profiles, and the editor-owned asset list)
- **Testing** → [unity-testing.md](unity-testing.md) (What to write; this file covers how to run it)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Reading the console and diagnosing what a change did)
- **Code Style** → [unity-code-style.md](unity-code-style.md) (The C# that `eval` compiles still follows it)

## 4. Core Rules

- **Rule 1 (Decision Order):** For any Editor-side operation, take the first step that applies. **(1)** Plain text the Editor does not own — `.cs`, `.uxml`, `.uss`, `.asmdef`, `.md`, repo config — edit directly with Read/Write/Edit; the Editor re-imports it. **(2)** A named command exists for the job — `unity cmd <that command>`. It exists because the operation is common, and it returns structured output with one Undo step. **(3)** No command fits — `unity cmd eval` or `unity cmd eval_file`. **(4)** Never batch mode. Do not skip from (1) to (3): writing C# to do what `set_serialized_field` already does is slower, compiles a snippet, and loses the Undo grouping.
- **Rule 2 (Batch Mode Is Banned):** `Unity.exe -batchmode` needs the project lock, so it forces the Editor closed, and it cannot report into the session. **`unity test`, `unity build`, and `unity run` fall under this ban**, despite being CLI subcommands: their own `--help` describes spawning the editor in batch mode, and they carry `--editor-path`, `--allow-install`, and a `--timeout` that "kills the Unity process". They start a second Editor rather than talking to the open one. `unity cmd run_tests` and `unity cmd build` do the same jobs against the open Editor, which is the reason to prefer them — but see Rule 19: on `build` specifically, the open Editor is **not** free of the side effects.
- **Rule 3 (Wait On Status, Never On Time):** Every long operation is asynchronous and has a matching status command: `build` → `build_status`, `run_tests` → `test_status`, `recompile` → `recompile_status`, `bake_navmesh` → `navmesh_bake_status`, `bake_lighting` → `lighting_bake_status`, `bake_occlusion_culling` → `occlusion_bake_status`, `switch_build_target` → `switch_build_target_status`, `package_add`/`package_remove`/`package_resolve` → `package_status`. Poll the status command. Never sleep and hope, and never infer completion from elapsed time. **Poll at an interval, not in a tight loop:** the bridge goes silent across a domain reload, so back-to-back calls each burn the full `--timeout` before failing. Space the polls (5s is comfortable) and let the reload finish. **PlayMode tests are the one case where async is not optional** — entering play mode triggers a domain reload that drops the HTTP request, so a synchronous `run_tests --mode playmode` always fails. Pass `--async_tests true` and poll `test_status`; EditMode runs fine synchronously and returns its results inline.
- **Rule 3a (Refreshing The Project Files `dotnet format` Reads):** The untracked `.csproj` and `goo-galaxy.slnx` are what `dotnet format` reads, and they are refreshed as a **side effect of a compile actually running** — a `.cs` added or removed lands in the matching `.csproj` after `unity cmd recompile`. The side effect is the whole catch. When `recompile_status` answers `up_to_date` nothing was regenerated, and a change that alters the csproj without changing any code — restoring a plugin, relabelling a DLL, editing an `.asmdef` — leaves them stale. Force it with `unity cmd eval` running `Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll()`, then confirm the mtime moved. Files restored to `Assets/` from outside the Editor (a `git checkout`, for instance) are invisible until `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` imports them, and `SyncAll()` before that import writes the old content. Unity also skips the write when the generated content already matches, so an unchanged timestamp means current **or** skipped — settle which by checking whether the referenced file list actually changed, never by mtime alone.
- **Rule 4 (A Silent Bridge Is Not A Closed Editor):** The bridge drops while the Editor imports or compiles, and the call fails or times out. That reports the bridge, not the process. `unity cmd editor_status` gives `status`, `compiling`, and `domainReloadInProgress` when the bridge is up; `unity status` answers from outside when it is not, printing port, project path, version, and PID without touching the bridge at all. Check before concluding anything, and never close or relaunch the Editor on the strength of a failed call.
- **Rule 5 (What The Deny Rules Protect):** `.claude/settings.json` blocks byte-level writes to `.asset`, `.meta`, `.prefab`, and `.unity`. That protects GUIDs and serialized references from being corrupted by a text write — it is **not** a prohibition on changing those files. Change them through the Editor, where the tooling keeps them valid: `move_asset` and `rename_asset` preserve the GUID so scene and prefab references survive a rename, and `delete_asset` removes the `.meta` alongside the asset. A denial on a direct write means "use the Editor", not "this cannot be done".
- **Rule 6 (Verify Writes Independently):** A setter's return value is not proof the write landed. `set_serialized_field` echoes back only the _identity_ of the object it touched — path, instanceId, type — and never the value it wrote; `set_component_properties` applies `m_Enabled` but omits it from the property map it echoes. When a value matters, read it back with a different command — `get_component_properties`, `get_serialized_fields --field <path>`, or `eval` against the live object — and when it does not matter, do not assert that it worked.
- **Rule 7 (Destructive Commands Have Two Gates):** Commands that destroy authored content require `--confirm true` — `delete_asset`, `clear_baked_lighting`, `clear_occlusion_culling`, `set_player_settings`. Most also take `--dry_run true`, which reports the target without touching it and is the right first call. That flag is the machine's gate against an accidental call. It is not the user's consent: deleting or overwriting authored content still needs an explicit ask, exactly as it would for any other irreversible change.
- **Rule 8 (`eval` Compiles Statements):** `eval` and `eval_file` take a statement body, not a compilation unit, so a `using` directive is parsed as a `using` _statement_ and fails to compile. Fully qualify instead — `System.Collections.Generic.HashSet<int>`, `UnityEditor.SerializedObject`. Reflection and generic collections work normally; there is no sandbox stripping them. Keep the snippet small: anything longer than a screen belongs in an editor script under `Assets/Editor/{Domain}/`, which is version-controlled and reviewable.
- **Rule 9 (Bound Every Listing):** Broad queries overflow the tool-result limit and get written to a file instead of returned — a bare `test_status` after a PlayMode run returns about 47 KB, and `list_tests` covers 683 tests across both modes. Pass the narrowest filter the command's schema actually offers, and parse the envelope instead of dumping it: `unity cmd list_tests --mode editor` narrows to 519, `--mode playmode` to 164. **Check the schema first — a filter you assume exists is silently ignored.** `run_tests` takes `--filter`; `list_tests` does **not**, and passing one to `list_tests` returns the full unfiltered set while looking like it worked.
- **Rule 10 (Leave The Editor As You Found It):** Any write marks the scene dirty — even setting a field to the value it already holds, and even creating and deleting a probe object — and a stray save writes that churn to disk. Check `list_open_scenes` for `isDirty` when finished; discard with `open_scene` on the same path, and confirm against `git status` that the file did not change. Never save a scene or asset that the task did not intend to change.
- **Rule 11 (Which `unity` Subcommand):** `unity cmd` is the only subcommand that talks to the open Editor. The rest answer what the Editor cannot answer about itself, and none of them touch the project: `unity status` (is it alive, on which port), `unity list` (what the bridge exposes, with `--json` for schemas), `unity doctor` / `diagnose` / `logs` / `env` (environment health), `unity install` / `install-modules` / `editors` (Editor and module management), `unity auth` / `license` / `cloud` (account and licence), and `unity pipeline` (the package providing this bridge). `unity test`, `unity build`, and `unity run` are banned by Rule 2.
- **Rule 12 (Arguments Need Their Dashes):** A parameter must arrive as a `--flag`. Both `--name "Main Camera"` and `--name="Main Camera"` parse; a bare `name=Main` is **silently dropped** — the command runs with its defaults, returns `success: true`, and looks like it worked. The CLI also accepts flags that no schema declares without complaint, so a typo like `--fliter` is discarded just as quietly. Neither the installed binary's `--help` nor the published docs describe this syntax, so treat `data.parameters` in the response as the proof: if the key is not echoed there, it was not applied.
- **Rule 13 (Every `*_status` Payload Is A String):** `data.result` is normally a JSON object, but **every status command except `editor_status`** returns it as a JSON _string_ that needs a second parse — `recompile_status`, `test_status`, `build_status`, `navmesh_bake_status`, `lighting_bake_status`, `occlusion_bake_status`, `switch_build_target_status`, and `package_status`. Parse twice before reading `status`, `summary`, or `errors` out of them; indexing the string directly yields characters, not fields. The key casing differs between the two surfaces as well: `run_tests` returns `Summary.Total/Passed/Failed`, while `test_status` returns `summary.total/passed/failed`.
- **Rule 14 (Git Bash Mangles Hierarchy Paths):** On Windows, Git Bash rewrites a leading-slash argument into a Windows path, so `--target "/Main Camera"` arrives as `C:/Program Files/Git/Main Camera` and the command fails with `Could not resolve target`. Prefer the **PowerShell** tool for any call carrying a `hierarchyPath`. In Bash, prefix the call with `MSYS_NO_PATHCONV=1`. An `instanceId` or `globalId` target sidesteps the problem entirely.
- **Rule 15 (`success` Is Two Layers, And The Outer One Lies):** The envelope's top-level `success` reports only that the CLI reached the Editor. The command's own verdict is `data.result.success`, with the reason in `data.result.error`. They disagree in exactly the case that matters: a synchronous `run_tests --mode playmode` returns `success: true` at the envelope, `success: false` inside, and a zeroed `Summary` of `Total: 0, Passed: 0, Failed: 0` — which reads as a green suite unless you look one level down. **Check the inner `success` and a non-zero `Total` before reporting any suite as passing**, and never quote counts from a payload whose inner `success` is false.
- **Rule 16 (Use The Scripts For Compile, Test And Console):** These wrap the dispatch-and-poll dance and answer with an exit code, so the traps in Rules 12–15, 17 and 18 cannot be re-introduced by hand (sources in [`scripts/`](../../scripts/)):

  | Command                                       | Does                                                     |
  | :-------------------------------------------- | :------------------------------------------------------- |
  | `npm run unity:recompile`                     | compile and wait; exit 1 if the project does not build   |
  | `npm run unity:test:editmode [-- <filter>]`   | EditMode, whole suite or a partial name match            |
  | `npm run unity:test:playmode [-- <filter>]`   | PlayMode, async internally                               |
  | `npm run unity:console:mark`                  | remember the console position                            |
  | `npm run unity:console [-- <level> [<tail>]]` | show what was logged since the mark; exit 1 on any error |

  **Both test scripts run the recompile gate themselves**, so there is nothing to chain. The test filter is positional and a case-insensitive partial match on the full test name — there is no flag name to misspell, and an unmatched filter fails loudly instead of reporting an empty pass. Drop to a raw `unity cmd` only for something the scripts do not cover, and then you own every trap yourself.

- **Rule 17 (`recompile_status.failed` Does Not Mean The Project Compiles):** It describes the last recompile _attempt_, not current state, and a later `recompile` that finds nothing to do **overwrites it with a clean `{status: up_to_date, failed: false, errors: []}` while the project is still broken** — measured here. The durable signal is `UnityEditor.EditorUtility.scriptCompilationFailed` via `eval`, which stayed `True` across exactly that sequence. This matters because a suite launched against a broken compile runs the **previously built** assemblies and reports them green: with a syntax error on disk, EditMode still answered 519/519 passed, while the new type was provably absent from the loaded assembly. Never read a green suite as proof the code compiles.
- **Rule 18 (There Are Two Console Stores, And `clear_console` Empties Neither Of Them):** `console` and `get_console_logs` do not read the same buffer — measured in one breath, `console --tail 200` returned 200 entries while `get_console_logs --severity all --limit 200` returned 0. `clear_console` answers `{"cleared": true}` and leaves the `console` buffer untouched: same 200 entries, same max seq, before and after. So a read is bounded by the **cursor**, never by clearing: take `result.cursor` from a `console` response and pass it back as `--since` to get only what came after. This matters because the buffer holds everything the Editor ever logged, and the PlayMode suite intentionally logs errors it asserts on with `LogAssert.Expect` — read straight after a green run it showed 39 errors, every one of them expected. `npm run unity:console:mark` does the cursor bookkeeping for you.
- **Rule 19 (Building Dirties The Project, In Either Mode):** The settings churn long blamed on batch mode is caused by **building**, not by the mode it runs in. A `unity cmd build` against the open Editor rewrote `Assets/Settings/Rendering/Pipeline/UniversalRP.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset` and `ProjectSettings/UnityConnectSettings.asset`, and created a stray `Assets/DefaultVolumeProfile.asset` with its `.meta` — the same list previously attributed to batch mode, none of it asked for, all of it invisible until `git status`. So: **never run a build to "check something", and never run one on a dirty tree.** Validate with `--dry_run true`, which answers `{status: dry_run, valid: true, validationErrors: []}` and touches nothing. When a real build is genuinely needed, take a `git status` before, and afterwards restore with `git restore` for the tracked files plus `git clean` for the stray profile — `git reset` alone will not do it, since it unstages rather than discarding working-tree edits. Player builds belong to CI (`unity-build-android.yml`, `unity-build-ios.yml`) and to `release-engineer`; a local one is a deliberate, cleaned-up exception. Building also opens the scenes in the build list, so check `list_open_scenes` and restore what was open (Rule 10).

## 5. Code & Configuration Examples

### 🚫 Don't (Bad)

```powershell
# ❌ Batch mode: needs the project lock, so the Editor must close, and the run rewrites
# render pipeline and project settings as a side effect
Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode -testResults r.xml

# ❌ Same ban, wearing a CLI subcommand: these spawn their own Editor in batch mode
unity test --mode EditMode
unity build --target Android --execute-method Builder.Build

# ❌ Bare key=value: silently dropped, so this returns every GameObject in the scene
unity cmd find_gameobjects name="Main Camera"

# ❌ Guessing when the Editor is ready instead of asking
Start-Sleep -Seconds 30
```

```csharp
// ❌ eval doing what a named command already does, with a using directive that cannot compile
using System.Reflection;
var so = new UnityEditor.SerializedObject(collider);
so.FindProperty("m_IsTrigger").boolValue = true;
so.ApplyModifiedProperties();
```

### ✅ Do (Good)

```powershell
# ✅ Named command: one typed call, one Undo step, structured result
unity cmd set_component_properties --target "/Board" --type BoxCollider2D `
  --properties '{"m_IsTrigger": true}' --no-banner --json

# ✅ Independent read-back, because the setter echoes identity, not the value it wrote
unity cmd eval --code 'return GameObject.Find("Board").GetComponent<BoxCollider2D>().isTrigger;' `
  --no-banner --json

# ✅ Check the schema before inventing a parameter name
unity list --no-banner --json    # every command, with types, defaults and required flags

# ✅ EditMode runs synchronously and returns results inline (519 tests, ~11s here)
unity cmd run_tests --mode editor --no-banner --json

# ✅ PlayMode MUST be async — play mode's domain reload drops a synchronous request
unity cmd run_tests --mode playmode --async_tests true --no-banner --json
unity cmd test_status --no-banner --json    # poll every ~5s until status is 'completed'

# ✅ Other async work polls its own status command — spaced out, and parsed twice
unity cmd build ...                                    → build_status  until status is 'completed'
unity cmd recompile                                    → recompile_status until 'completed' or 'up_to_date'
```

```powershell
# ✅ Both layers of `success`, because the outer one only means "the CLI reached the Editor"
$env = unity cmd run_tests --mode playmode --async_tests true --no-banner --json | ConvertFrom-Json
if (-not $env.success) { throw "bridge: $($env.errors[0].message)" }
if (-not $env.data.result.success) { throw "command: $($env.data.result.error)" }
```

```powershell
# ✅ Reading a status payload: the envelope parses, then the result parses again
$env = unity cmd recompile_status --no-banner --json | ConvertFrom-Json
$state = $env.data.result | ConvertFrom-Json
$state.status    # idle | triggered | compiling | completed | up_to_date
```

```csharp
// ✅ eval when no command fits: statements only, fully qualified, small
var grid = UnityEngine.Object.FindFirstObjectByType<GooGalaxy.Runtime.Board.Presenters.GridPresenter>();
var seen = new System.Collections.Generic.HashSet<int>();
return grid == null ? "no grid" : grid.HexGrid.Cells.Count.ToString();
```

## 6. Quick Reference & Decision Matrix

| Job                                                 | Reach for                                                                          | Not                                                      |
| :-------------------------------------------------- | :--------------------------------------------------------------------------------- | :------------------------------------------------------- |
| Edit a script, UXML, USS, asmdef, markdown          | Read / Write / Edit                                                                | Any Editor command                                       |
| Discover a command's parameters                     | `unity list --json`                                                                | Guessing, or plain `unity list` (no schema)              |
| Run tests                                           | `npm run unity:test:editmode` / `:playmode`                                        | `unity test`, `-batchmode -runTests`, or asking the user |
| Validate a build without building                   | `unity cmd build --dry_run true`                                                   | A real build "just to check" (Rule 19)                   |
| Build a player                                      | CI, or `unity cmd build` + `build_status` on a clean tree, then restore            | `unity build`, or a hand-written `BuildPipeline` script  |
| Create, delete, move, rename an asset               | `unity cmd create_asset` / `delete_asset` / `move_asset` / `rename_asset`          | Writing the file, or deleting its `.meta`                |
| Change a component or serialized field              | `unity cmd set_component_properties` / `set_serialized_field`                      | Hand-editing the `.unity` or `.prefab`                   |
| Change project settings                             | `unity cmd set_player_settings` / `set_quality_settings`, and siblings             | Editing `ProjectSettings/*.asset`                        |
| Open, save, inspect a scene                         | `unity cmd open_scene` / `save_scene` / `list_open_scenes` / `get_scene_hierarchy` | —                                                        |
| Read the console                                    | `npm run unity:console:mark`, then `npm run unity:console`                         | `clear_console`, or tailing `Editor.log`                 |
| Know whether a recompile finished                   | `unity cmd recompile_status`                                                       | Sleeping                                                 |
| Refresh `.csproj` / `.slnx` before `dotnet format`  | `unity cmd recompile`; `SyncAll()` via `eval` when it reports `up_to_date`         | Trusting a stale csproj, or judging by mtime alone       |
| Know whether the Editor is alive                    | `unity cmd editor_status`, or `unity status` when the bridge is down               | Assuming from a failed call                              |
| Something with no command                           | `unity cmd eval` / `eval_file`                                                     | Reaching for batch mode                                  |
| Install a module, check a licence, diagnose the CLI | `unity doctor` / `install-modules` / `license` in the shell                        | —                                                        |

| Symptom                                                   | Cause                                                             | What to do                                                                        |
| :-------------------------------------------------------- | :---------------------------------------------------------------- | :-------------------------------------------------------------------------------- |
| Call fails or times out mid-import                        | Bridge dropped during import or compile                           | `unity status` to confirm the process; wait and retry spaced out. Do not relaunch |
| Command ran with defaults and ignored your filter         | Argument passed as bare `key=value`, or a name no schema declares | Re-issue with `--flag value`; confirm against `data.parameters` (Rule 12)         |
| `data.result` behaves like text, not an object            | A `*_status` command — the payload is a JSON string               | Parse it a second time (Rule 13)                                                  |
| A suite reports 0 total, 0 failed, and looks green        | Inner `data.result.success` is false — PlayMode ran synchronously | Re-run with `--async_tests true` and poll `test_status` (Rules 3, 15)             |
| Tests pass but the code demonstrably does not build       | The run used the previously compiled assemblies                   | Gate on `scriptCompilationFailed`, or just use the scripts (Rules 16, 17)         |
| Render pipeline and project settings changed on their own | A build ran — the open Editor does this too, not just batch mode  | `git restore` the tracked files, `git clean` the stray volume profile (Rule 19)   |
| `Could not resolve target: C:/Program Files/Git/...`      | Git Bash rewrote a leading-slash hierarchy path                   | Use the PowerShell tool, or `MSYS_NO_PATHCONV=1` (Rule 14)                        |
| A setter's response omits the field you set               | The echo carries identity, not values                             | Read back with `get_*` or `eval`                                                  |
| Result written to a file instead of returned              | The result exceeded the token limit                               | Re-run narrower, and parse the envelope rather than dumping it                    |
| Scene shows as dirty after a read-only task               | A probe object, or any write at all — even a no-op one            | `open_scene` on the same path to discard; verify with `git status`                |
| A direct write to `.asset` / `.meta` is denied            | Policy, protecting GUIDs                                          | Use the matching Editor command instead                                           |
