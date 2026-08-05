---
name: start-task
description: >-
  Kick off implementation of a Goo Galaxy task from a Notion ID (GOOE/GOOS/GOOT/GOOM), a refinement document, or task content pasted into the prompt. Reads and grounds the task against the real repository structure, decides which specialist agents are needed and how many, then delegates the work to them. Use whenever the user says start this task, begin development, implement GOOM-XX, let's build this, work on this story, or hands over a spec to be built.
argument-hint: "A task ID (e.g. GOOM-13), a refinement doc path, or the task content itself"
---

# Goo Galaxy: Start Task

This skill **performs the work**. It ends with code on disk produced by specialist agents, not with a plan in chat.

You act as the **delivery lead**: you own intake, grounding, agent selection, delegation, and integration. You do not personally write the bulk of the implementation — the agents in `.claude/agents/` do.

## 1. Acquire the Task Content

| Input the user gave                          | What to do                                                                             |
| :------------------------------------------- | :------------------------------------------------------------------------------------- |
| A `GOOE`/`GOOS`/`GOOT`/`GOOM` ID             | Use the `track-task` skill to search Notion, fetch the page, and read its full content |
| A path under `.docs/refinement/`             | Read that file                                                                         |
| A branch name like `feat/GOOM-5`             | Extract the ID, then follow the ID row                                                 |
| Pasted task text, a spec, or a plain request | Use it directly                                                                        |
| Nothing usable                               | Ask for the ID or the content. Do not invent a task.                                   |

If the task is too thin to implement (no acceptance criteria, no clear outcome), stop and recommend `/refine-task` instead of guessing.

Restate the task in two or three sentences and confirm the goal before delegating anything.

## 2. Grounding Rule (mandatory)

**Everything concrete in the task content is an example, not an instruction.** File paths, folder names, class names, method names, field names, namespaces, assembly names, event names, and code snippets in a task or Notion page were written during refinement, before the code existed. They are intent, not identifiers.

Before any agent is dispatched, resolve them against reality:

1. List `Assets/Scripts/Runtime/` to learn which feature assemblies actually exist. Never assume the set.
2. Read the target `.asmdef` to learn its real name and current references.
3. Search for the types the task names, plus obvious synonyms, to find whether they already exist under a different name.
4. Read one or two neighboring files in the destination folder to pick up the established naming and structure.
5. Read the `.claude/rules/` rules that match the files you are about to touch.

Then produce a short **grounding table** and show it to the user before delegating:

| Task says           | Actual target                                    | Why                              |
| :------------------ | :----------------------------------------------- | :------------------------------- |
| `BoardManager.cs`   | `Assets/Scripts/Runtime/Board/BoardPresenter.cs` | Existing MVP naming in `Board/`  |
| `GooGalaxy.HexGrid` | `GooGalaxy.Runtime.Board`                        | No such assembly; hex lives here |

If a task path would create a new root structure outside the established `Assets` hierarchy, reject it and pick the correct nested location.

## 3. Plan the Work

Write a todo list covering the whole task. Split by **discipline**, not by file — the split determines which agents you need.

Classify every piece of the task into one or more of these:

| Work in the task                                                        | Agent                      |
| :---------------------------------------------------------------------- | :------------------------- |
| Runtime gameplay code, Models, Presenters, new feature assembly         | `unity-gameplay-engineer`  |
| NGO authority, NetworkVariable/RPC, ownership, lobby/relay/session flow | `unity-netcode-engineer`   |
| UXML layouts, USS styling, custom `VisualElement`s, View layer          | `unity-uitoolkit-engineer` |
| Anything under `Assets/Editor/` — inspectors, windows, validators       | `unity-editor-tooling`     |
| Shaders, VFX Graph, materials, render features                          | `shader-vfx-artist`        |
| EditMode/PlayMode tests                                                 | `unity-test-author`        |
| Tuning values, costs, curves, economy numbers                           | `game-balance-analyst`     |
| Package versions, `.asmdef` graph, solution or toolchain breakage       | `dependency-doctor`        |
| Workflows, build profiles, CI                                           | `release-engineer`         |
| A defect to diagnose before it can be fixed                             | `unity-bug-hunter`         |
| GDD chapters that the change makes stale                                | `gdd-steward`              |
| Mobile hot-path review of what was built                                | `unity-perf-auditor`       |
| Convention audit of the finished diff                                   | `unity-code-reviewer`      |

## 4. Decide How Many Agents

Rules, in priority order:

1. **One agent per discipline touched.** Never spawn two agents for the same discipline on the same task — a second one will fight the first over the same files.
2. **No agent for trivial work.** If the whole task lives in one discipline and is under roughly three files, implement it yourself following the `.claude/rules/` files. Delegation costs more than it saves.
3. **Sequential when output feeds input.** Netcode contracts before gameplay that calls them. Gameplay Models/Presenters before the Views that bind to them. Everything before tests. Tests before review.
4. **Parallel only when the file sets are disjoint.** UI markup and editor tooling can run at once; two agents editing the same assembly cannot.
5. **Always close with `unity-code-reviewer`** once the code is in place.
6. **Always include `unity-test-author`** unless the task is documentation-, config-, or asset-only.
7. **Add `unity-perf-auditor`** when the task touches an update loop, per-tile board work, the network tick, or rendering. Otherwise skip it — the reviewer delegates on its own if needed.

## 5. Choose the Model Tier per Agent

The `Agent` tool takes a `model` parameter that **overrides** the agent's frontmatter for that one dispatch. Only the two read-only analysts pin a model; every other agent in `.claude/agents/` inherits, so **pass `model` explicitly when you dispatch them** — an omitted parameter falls back to the session model, and this project's lead runs on `opus`, which would silently promote every routine slice to the top tier.

Pick the tier from the **complexity of that agent's slice**, not from the agent's identity — the same `unity-gameplay-engineer` takes `haiku` for a rename sweep and `opus` for designing a resolver.

| Tier     | The slice looks like                                                                                                                                                                                                                         |
| :------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `haiku`  | Mechanical and fully specified: renames, applying findings another agent already wrote out, `.asmdef` scaffolding, a test that mirrors an existing one, a values-only balance edit                                                           |
| `sonnet` | **The default for implementation.** Ordinary work inside an established pattern — a new Presenter alongside three siblings, a UXML screen like the others, a normal-sized diff to review                                                     |
| `opus`   | Architecture and assembly-boundary calls, a new feature assembly, netcode authority and reconciliation design, root-causing an intermittent or desync defect, balance math with interacting curves, reviewing a large or cross-assembly diff |

Three rules that override the table:

1. **The read-only analysts are always `opus`, whatever the diff looks like.** `unity-perf-auditor` and `unity-code-reviewer` produce findings nobody double-checks — a false negative there ships silently. They pin `model: opus` in their own frontmatter, so leaving the parameter off already gets this right; never pass a lower tier to either.
2. **Never send `haiku` into an unresolved question.** The tier is a floor for work whose answer is already decided. If the agent still has a decision to make, it is not `haiku` work.
3. **`sonnet` is the resting point, not `opus`.** Escalate on a named reason you can state in the roster line. "The task feels important" is not one — most slices of an important task are still ordinary implementation.

State the roster, the order, **and the tier** before you dispatch, e.g.:

> 3 agents, sequential: `unity-gameplay-engineer` (opus — new assembly + resolver design) → `unity-test-author` (haiku — mirrors `BoardMovementTests`) → `unity-code-reviewer` (opus, pinned).

## 6. Dispatch

Each subagent gets a **self-contained brief**. It cannot see this conversation, the Notion page, or your grounding table.

Every brief must contain:

- The goal, in outcome terms.
- The **resolved** paths, assembly names, and type names from step 2 — never the task's placeholder names.
- The acceptance criteria that apply to this agent's slice.
- Existing files worth reading first, by path.
- The `.claude/rules/` files that govern this slice, by path — subagents do not receive project rules or CLAUDE.md, so an unlisted rule is an unread rule.
- The explicit boundary: what this agent must not touch, because another agent owns it.
- What to return: files changed, decisions made, and anything left for the next agent.

Dispatch each agent with the `model` tier chosen in step 5.

Feed each agent's returned notes into the next agent's brief. If an agent reports a blocker or contradicts the task, stop and bring it to the user rather than dispatching around it. A `haiku` agent that comes back with questions instead of code was mis-tiered — re-dispatch that slice a tier up rather than answering it piecemeal.

## 7. Integrate and Verify

1. Read every file the agents touched and fix what they left broken — you cannot compile, so cross-check signatures, namespaces, and `.asmdef` references by hand.
2. Run `npm run format`.
3. Confirm assembly dependency direction — `Runtime.Shared` stays the leaf, editor never referenced by runtime, no cycles.
4. Confirm the acceptance criteria are actually met, item by item.
5. Never run tests. Tell the user which tests to run in the Unity Test Runner.

## 8. Report and Hand Off

- **Task** — ID, title, and the source it came from.
- **Grounding** — the task-says vs actual table.
- **Agents run** — roster, order, model tier, and what each produced.
- **Changes** — files created and edited, grouped by assembly.
- **Manual editor steps** — every `.asset`, `.meta`, prefab, or scene change the user must make in Unity. You cannot write those files; the `deny` rules in `.claude/settings.json` enforce it.
- **Tests to run** — EditMode and PlayMode, by name.
- **Open questions** — anything a decision is still needed on.
- **Next** — `/create-commit`, then `/open-pull-request`, then `/track-task` to sync the Notion page.

## Boundaries

- Do not create `.asset` or `.meta` files under `Assets/` — provide menu path, fields, and values instead.
- Do not run tests, launch Unity, or start a build.
- Do not commit, push, or open a PR from this skill. Hand off to `/create-commit` and `/open-pull-request`.
- Do not create or switch branches without asking. If the user wants one, follow `feat/GOOM-1`-style naming and offer to write it back to the Notion `Branch` property via `track-task`.
- Do not edit the Notion page as part of implementation. That is `track-task`'s job, after the PR exists.
