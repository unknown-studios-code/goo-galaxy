---
description: "Use when any task touches the running Unity Editor — running tests, building, editing scenes, prefabs, assets or project settings, or waiting for a recompile. Covers the Unity MCP tool surface, the Unity CLI, and why batch mode is banned."
---

# Unity Editor Automation

## 1. Overview

The Editor is the authority for everything under `Assets/` and `ProjectSettings/`. It owns the GUIDs, the import pipeline, and the serialized form of every asset, so any change to that content goes **through** a running Editor rather than around it. Everything else in the repository is plain text you edit directly.

Two surfaces reach the Editor and one must never be used:

| Surface                                | Provided by                                      | Use for                                         |
| :------------------------------------- | :----------------------------------------------- | :---------------------------------------------- |
| **Unity MCP tools** (`mcp__unity__*`)  | `com.unity.pipeline`, served through `unity mcp` | Every operation inside the Editor               |
| **Unity CLI** (`unity …` in the shell) | the standalone `unity` binary                    | Questions the Editor cannot answer about itself |
| **`Unity.exe -batchmode`**             | —                                                | **Never.** See Rule 2                           |

The Editor stays open. Do not ask for it to be closed, and do not design a workflow that requires it.

## 2. Cross-References

- **Project Configuration** → [unity-project-configuration.md](unity-project-configuration.md) (Assembly definitions, Build Profiles, and the editor-owned asset list)
- **Testing** → [unity-testing.md](unity-testing.md) (What to write; this file covers how to run it)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Reading the console and diagnosing what a change did)
- **Code Style** → [unity-code-style.md](unity-code-style.md) (The C# that `eval` compiles still follows it)

## 3. Core Rules

- **Rule 1 (Decision Order):** For any Editor-side operation, take the first step that applies. **(1)** Plain text the Editor does not own — `.cs`, `.uxml`, `.uss`, `.asmdef`, `.md`, repo config — edit directly with Read/Write/Edit; the Editor re-imports it. **(2)** A named MCP tool exists for the job — use it. The tool exists because the operation is common, and it returns structured output with one Undo step. **(3)** No tool fits — `eval` or `eval_file`. **(4)** Never batch mode. Do not skip from (1) to (3): writing C# to do what `set_serialized_field` already does is slower, compiles a snippet, and loses the Undo grouping.
- **Rule 2 (Batch Mode Is Banned):** `Unity.exe -batchmode` needs the project lock, so it forces the Editor closed, and it mutates settings as a side effect of running. One measured build run rewrote `UniversalRP.asset` (shader prefiltering), `UniversalRenderPipelineGlobalSettings.asset` (render graph feature list), `UnityConnectSettings.asset` (`m_Enabled: 0 → 1`), and `ProjectSettings.asset` (`m_BuildTargetBatching`) — none of them asked for, all of them invisible until `git status`. `run_tests` and `build` do the same jobs against the open Editor with none of it.
- **Rule 3 (Wait On Status, Never On Time):** Every long operation is asynchronous and has a matching status tool: `build` → `build_status`, `run_tests` → `test_status`, `recompile` → `recompile_status`, `bake_navmesh` → `navmesh_bake_status`, `bake_lighting` → `lighting_bake_status`, `switch_build_target` → `switch_build_target_status`. Poll the status tool. Never sleep and hope, and never infer completion from elapsed time.
- **Rule 4 (A Silent Bridge Is Not A Closed Editor):** The MCP bridge drops while the Editor imports or compiles, and returns something like "Unity not detected". That reports the bridge, not the process. `editor_status` gives `status`, `compiling`, and `domainReloadInProgress` when the bridge is up; `unity status` in the shell answers from outside when it is not, printing port, project path, version, and PID. Check before concluding anything, and never close or relaunch the Editor on the strength of a failed tool call.
- **Rule 5 (What The Deny Rules Protect):** `.claude/settings.json` blocks byte-level writes to `.asset`, `.meta`, `.prefab`, and `.unity`. That protects GUIDs and serialized references from being corrupted by a text write — it is **not** a prohibition on changing those files. Change them through the Editor, where the tooling keeps them valid: `move_asset` and `rename_asset` preserve the GUID so scene and prefab references survive a rename, and `delete_asset` removes the `.meta` alongside the asset. A denial on a direct write means "use the Editor", not "this cannot be done".
- **Rule 6 (Verify Writes Independently):** A setter's return value is not proof the write landed. `set_component_properties` applies `m_Enabled` but omits it from the property map it echoes back, so trusting the echo reports a false failure. When a value matters, read it back with a different tool — `get_component_properties`, `get_serialized_fields`, or `eval` against the live object — and when it does not matter, do not assert that it worked.
- **Rule 7 (Destructive Tools Have Two Gates):** Tools that destroy authored content require `confirm=true` — `delete_asset`, `clear_baked_lighting`, `clear_occlusion_culling`, `set_player_settings`. That flag is the machine's gate against an accidental call. It is not the user's consent: deleting or overwriting authored content still needs an explicit ask, exactly as it would for any other irreversible change.
- **Rule 8 (`eval` Compiles Statements):** `eval` and `eval_file` take a statement body, not a compilation unit, so a `using` directive is parsed as a `using` _statement_ and fails to compile. Fully qualify instead — `System.Collections.Generic.HashSet<int>`, `UnityEditor.SerializedObject`. Reflection and generic collections work normally; there is no sandbox stripping them. Keep the snippet small: anything longer than a screen belongs in an editor script under `Assets/Editor/{Domain}/`, which is version-controlled and reviewable.
- **Rule 9 (Bound Every Listing):** Broad queries overflow the tool-result limit and get written to a file instead of returned — `list_tests` with no filter returns roughly 170 KB for this project's suites. Pass the narrowest filter the tool offers (`mode`, `filter`, a path) and reach for the file only when the full set is genuinely needed.
- **Rule 10 (Leave The Editor As You Found It):** Creating and deleting a probe object still marks the scene dirty, and a stray save writes that churn to disk. Check `list_open_scenes` for `isDirty` when finished; discard with `open_scene` on the same path, and confirm against `git status` that the file did not change. Never save a scene or asset that the task did not intend to change.
- **Rule 11 (What Stays In The Shell):** The CLI answers what the Editor cannot answer about itself: `unity status` (is it alive, on which port), `unity doctor` / `diagnose` / `logs` / `env` (environment health), `unity install` / `install-modules` / `editors` (Editor and module management), `unity auth` / `license` / `cloud` (account and licence), `unity pipeline` (the package providing this bridge), and `unity mcp configure` (client wiring). Everything inside the Editor goes through the MCP tools.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```powershell
# ❌ Batch mode: needs the project lock, so the Editor must close, and the run rewrites
# render pipeline and project settings as a side effect
Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode -testResults r.xml

# ❌ Guessing when the Editor is ready instead of asking
Start-Sleep -Seconds 30
```

```csharp
// ❌ eval doing what a named tool already does, with a using directive that cannot compile
using System.Reflection;
var so = new UnityEditor.SerializedObject(collider);
so.FindProperty("m_IsTrigger").boolValue = true;
so.ApplyModifiedProperties();
```

### ✅ Do (Good)

```
// ✅ Named tool: one typed call, one Undo step, structured result
set_component_properties(target: "/Board", type: "BoxCollider2D", properties: {"m_IsTrigger": true})

// ✅ Independent read-back, because the setter's echo omits some fields
eval(code: "return GameObject.Find(\"Board\").GetComponent<BoxCollider2D>().isTrigger;")

// ✅ Async work polls its own status tool
run_tests(mode: "EditMode")   → test_status   until it reports completion
build(...)                    → build_status  until status is 'completed'
recompile()                   → recompile_status until 'completed' or 'up_to_date'
```

```csharp
// ✅ eval when no tool fits: statements only, fully qualified, small
var grid = UnityEngine.Object.FindFirstObjectByType<GooGalaxy.Runtime.Board.Presenters.GridPresenter>();
var seen = new System.Collections.Generic.HashSet<int>();
return grid == null ? "no grid" : grid.HexGrid.Cells.Count.ToString();
```

## 5. Quick Reference & Decision Matrix

| Job                                                 | Reach for                                                             | Not                                                    |
| :-------------------------------------------------- | :-------------------------------------------------------------------- | :----------------------------------------------------- |
| Edit a script, UXML, USS, asmdef, markdown          | Read / Write / Edit                                                   | Any Editor tool                                        |
| Run tests                                           | `run_tests` + `test_status`                                           | `-batchmode -runTests`, or asking the user to run them |
| Build a player                                      | `build` + `build_status`, `add_scene_to_build`                        | A hand-written `BuildPipeline` script                  |
| Create, delete, move, rename an asset               | `create_asset`, `delete_asset`, `move_asset`, `rename_asset`          | Writing the file, or deleting its `.meta`              |
| Change a component or serialized field              | `set_component_properties`, `set_serialized_field`                    | Hand-editing the `.unity` or `.prefab`                 |
| Change project settings                             | `set_player_settings`, `set_quality_settings`, and siblings           | Editing `ProjectSettings/*.asset`                      |
| Open, save, inspect a scene                         | `open_scene`, `save_scene`, `list_open_scenes`, `get_scene_hierarchy` | —                                                      |
| Read the console                                    | `get_console_logs`, `console`                                         | Tailing `Editor.log`                                   |
| Know whether a recompile finished                   | `recompile_status`                                                    | Sleeping                                               |
| Know whether the Editor is alive                    | `editor_status`, or `unity status` when the bridge is down            | Assuming from a failed tool call                       |
| Something with no tool                              | `eval`, `eval_file`                                                   | Reaching for batch mode                                |
| Install a module, check a licence, diagnose the CLI | `unity …` in the shell                                                | —                                                      |

| Symptom                                           | Cause                                                 | What to do                                                             |
| :------------------------------------------------ | :---------------------------------------------------- | :--------------------------------------------------------------------- |
| "Unity not detected" from an MCP tool             | Bridge dropped during import or compile               | `unity status` to confirm the process; wait and retry. Do not relaunch |
| A setter's response omits the field you set       | The echoed map is partial, not a verification surface | Read back with `get_*` or `eval`                                       |
| Tool result written to a file instead of returned | The result exceeded the token limit                   | Re-run with a narrower filter                                          |
| Scene shows as dirty after a read-only task       | A probe object, or a tool that marks dirty regardless | `open_scene` on the same path to discard; verify with `git status`     |
| A direct write to `.asset` / `.meta` is denied    | Policy, protecting GUIDs                              | Use the matching Editor tool instead                                   |
