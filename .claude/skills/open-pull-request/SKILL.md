---
name: open-pull-request
description: >-
  Open, update, and label GitHub pull requests for Goo Galaxy following the project's PR workflow — Conventional Commits titles, template-driven bodies, label assignment, and Notion task sync. Use whenever the user asks to create a PR, open a pull request, draft a PR, or mentions pull requests — even if they don't explicitly mention the workflow or conventions. Also use when the user wants to update PR labels, title, or body.
---

# Goo Galaxy: Open Pull Request

This skill **opens the pull request**. Drafting the title and body is a step, not the deliverable. Unless the user explicitly asks for body text only ("just write the PR description"), always finish by creating the PR on GitHub and reporting its number and URL.

## Workflow

Follow these steps in order when opening a PR:

1. **Identify context** — run `git branch --show-current`, `git status --short`, and `git log main..HEAD --oneline` to determine the current branch, target branch (usually `main`), commits included, and any task/story IDs (GOOT, GOOM, GOOS, GOOE) from the branch name or commit footers.
2. **Verify the branch is pushable** — refuse to open a PR from `main`. If there are uncommitted changes, ask whether to commit them first (use the `create-commit` skill) or proceed without them.
3. **Push the branch** — run `git push -u origin <branch>` if the branch has no upstream or has unpushed commits. Pushing a feature branch to open a PR is expected; never force-push without explicit approval.
4. **Fetch Notion data** — when an ID was found and Notion MCP is connected, use the `track-task` skill to look up the task and parent story: name, priority, type, acceptance criteria (Definition of Done), page URL. With no ID or no Notion MCP, skip to step 5 and mark the body as missing synced metadata.
5. **Generate the title** — format as `type(scope): subject` using the rules in the Title Format section below.
6. **Read the template** — read `${CLAUDE_SKILL_DIR}/templates/pr-template.md` and use it as the body structure.
7. **Build the body** — fill in the template with Notion data and concrete details (see Body Generation below).
8. **Assign labels** — select 3–5 labels based on type, priority, context, and optional status (see Label Assignment below).
9. **Create the PR** — use **GitHub MCP first** (`create_pull_request`), targeting `main`. Fall back to `gh pr create` only if GitHub MCP is unavailable or fails. Always note which method was used and why if falling back. Open as a draft when the work is incomplete.
10. **Apply labels** — use **GitHub MCP first** to add labels. Fall back to `gh pr edit` if needed.
11. **Update Notion** — when step 4 resolved a page, use `track-task` to write the branch name (`Branch` property) and PR URL (`Pull Request` property) back to it.
12. **Report** — return the PR number, URL, applied labels, and the Notion sync result (updated, skipped, or failed).

### When the PR Cannot Be Opened

- A PR already exists for the branch: report the existing PR and offer to update its title, body, or labels instead of creating a duplicate.
- Push is rejected: report the error and stop; do not force-push.
- Both GitHub MCP and `gh` fail: report the failure and hand back the fully prepared title and body so the user can open it manually.

## Title Format

PR titles follow the same Conventional Commits format as commit messages:

```
type(scope): subject
```

Rules:

- **type** is mandatory and lowercase: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`
- **scope** is mandatory, lowercase, and from the Goo Galaxy scope list: `bootstrap`, `board`, `cards`, `energy`, `hud`, `input`, `match`, `networking`, `progression`, `shared`, `tests`, `docs`, `build`, `ci`
- **subject** is mandatory, lowercase, imperative, no period, under 72 chars

## Body Generation

Use `${CLAUDE_SKILL_DIR}/templates/pr-template.md` as the structural source. Fill in every section with real data, never leaving placeholders.

### Template Section Selection

Choose exactly one task-type section based on the work:

| Section                    | Use for                                                   |
| :------------------------- | :-------------------------------------------------------- |
| **Feature Additions**      | New features, user-facing behavior, integrations          |
| **Technical Improvements** | Refactors, optimizations, architecture, tooling, delivery |
| **Bug Fix Details**        | Bug fixes and regressions                                 |

Delete the two unused sections — only one remains in the final body.

### What Changed

Write a concise, high-level summary focused on reviewer outcomes — what changed and why it matters. Do not write a commit-by-commit changelog.

### Key Technical Decisions

Include this section when the PR changes architecture, flow, tooling, networking, or any non-obvious implementation choice. For each decision, explain the rationale — not just what was done, but why that approach was chosen.

### Files Section

Group changes by the folders the diff actually touched — take them from `git diff --name-only`, never from memory:

| Path                                               | Group                                                          |
| :------------------------------------------------- | :------------------------------------------------------------- |
| `Assets/Scripts/Runtime/{Feature}/`                | One group per feature assembly (`GooGalaxy.Runtime.{Feature}`) |
| `Assets/Scripts/Tests/{EditMode,PlayMode}/`        | Tests                                                          |
| `Assets/Editor/{Domain}/`                          | Editor tooling                                                 |
| `Assets/{Art,Audio,Data,Prefabs,Scenes,Settings}/` | Content and configuration                                      |
| `.github/`, `package.json`, `.husky/`              | CI and tooling                                                 |
| `.docs/`, `.claude/`                               | Documentation and agent configuration                          |

Do not fabricate folder structures or architecture buckets that do not exist in this repository.

### Definition of Done

Copy real acceptance criteria from Notion MCP whenever available. Mark items as completed (`[x]`) only when the work is actually done. If the task is not complete, use a draft PR or stop and ask before generating a misleading checklist.

### References

Always include:

- The Notion task link (when available)
- The Notion story link (when available)
- Relevant documentation under `.docs/GDD/` when it helps reviewers

If the PR touches networking, sessions, or multiplayer, reference `.docs/GDD/08_Technical_Architecture_and_Multiplayer.md`.

## Task and Story IDs

| Prefix | Meaning          | Usage                                   |
| :----- | :--------------- | :-------------------------------------- |
| `GOOE` | Epic identifier  | References only when genuinely relevant |
| `GOOS` | Story identifier | Parent story in PR header               |
| `GOOT` | Standard task    | Task header in PR body                  |
| `GOOM` | MVP task         | Task header in PR body                  |

Rules:

- Use `GOOT` or `GOOM` in the PR task header.
- Use `GOOS` for the parent story.
- If no task or story ID is available, ask before inventing references.
- If identifiers don't match expected patterns, ask whether the convention should be updated.

## Notion MCP Integration

When a PR references Notion-tracked work:

- **Before creating the PR:** Search for the task by ID, fetch its name, priority, type, and acceptance criteria. Fetch the parent story for context.
- **After creating the PR:** Update the task page with the branch name and PR URL using the `Branch` (text) and `Pull Request` (URL) properties.
- **Epic context:** Only fetch epic-level context when it materially helps reviewers understand the change.

If Notion data is unavailable, say the PR body is a draft missing synced task metadata — do not invent task details.

## Label Assignment

Assign 3 to 5 labels. If the repository labels do not exist exactly as listed, use the closest available ones and note the discrepancy.

### Label Categories

**Type (1 required):**

`type: feat`, `type: fix`, `type: docs`, `type: style`, `type: refactor`, `type: perf`, `type: test`, `type: build`, `type: ci`, `type: chore`, `type: revert`

**Priority (1 required):**

`priority: critical`, `priority: high`, `priority: medium`, `priority: low`

Source: task priority from Notion MCP. Fallback: `priority: medium`.

**Context (1–2):**

| Category       | Labels                                                                                   |
| :------------- | :--------------------------------------------------------------------------------------- |
| Domain         | `domain: board`, `domain: cards`, `domain: match`, `domain: progression`                 |
| Client         | `client: hud`, `client: input`, `client: audio`                                          |
| Platform       | `platform: networking`, `platform: bootstrap`, `platform: shared`, `platform: rendering` |
| Infrastructure | `infra: build`, `infra: ci`, `infra: tests`, `infra: docs`                               |

**Status (optional, at most 1):**

`status: blocked`, `status: in progress`, `status: needs review`, `status: needs testing`

### Selection Rules

- Always assign 1 type label and 1 priority label.
- Add 1 or 2 context labels based on the main affected area.
- Add at most 1 status label, only when it materially helps reviewers.

## GitHub Operations

### Primary: GitHub MCP

Use GitHub MCP tools for all PR operations:

- `create_pull_request` — create the PR
- `update_pull_request` — update title, body, state, reviewers
- `list_pull_requests` / `pull_request_read` — read PR details

GitHub MCP operates directly against the GitHub API with full authenticated access.

### Fallback: GitHub CLI

Only use `gh` CLI when GitHub MCP is unavailable or returns an error:

- `gh pr create` — create the PR, passing `--base`, `--head`, `--title`, and `--body-file` explicitly
- `gh pr edit` — update labels, title, or body after creation

Always log which method was used and why the primary method was skipped.

## Quality Checks

The body is written in English, uses concrete file paths and real data, and reads as a summary of outcomes rather than a commit log. Before opening the PR, verify:

- Title follows the format above with a valid type and scope
- Exactly one task-type section remains, with no placeholders or instructional markers left
- Definition of Done is copied from Notion MCP when available
- Task and story IDs use correct `GOOT`/`GOOM`/`GOOS` prefixes
- Base branch is `main` unless explicitly overridden, and the head branch is pushed and is not `main`

After opening it, report the PR number and URL, the labels that were applied, and whether the Notion task now carries the branch name and PR URL.

## Ambiguity Handling

- If the task type is unclear, ask whether to frame as Feature, Tech, or Bug.
- If repository labels are unknown, state that label selection is best-effort.
- If Notion data is unavailable, say the PR body is a draft missing synced metadata — do not invent it.
- If task or story IDs are not provided, ask before inventing references.
