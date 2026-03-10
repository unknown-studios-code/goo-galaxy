---
description: "Use when drafting or creating GitHub pull requests, PR titles, PR bodies, labels, and task references for Goo Galaxy. Covers Conventional Commits PR titles, Goo Galaxy task IDs, the shared pr-template.md PR body template, English-only PR copy, GitHub Flow targeting main, and Notion MCP updates."
name: "Pull Request Standards"
---

# Pull Request Creation Standards

Follow these rules whenever drafting, reviewing, or creating a pull request for Goo Galaxy.

## Workflow Overview

1. Identify the current branch, target branch, and task or story IDs.
2. Fetch the task and story details with Notion MCP before writing the PR body.
3. Generate the PR title in Conventional Commits format.
4. Read `.github/instructions/templates/pr-template.md`.
5. Generate the PR body from that template.
6. Assign labels using the mapping rules below.
7. Create the PR with GitHub CLI using `gh pr create`, targeting `main` unless the user explicitly asks for a different base.
8. Apply or adjust labels with GitHub CLI using `gh pr edit` when needed.
9. Update the related Notion records with the PR URL using Notion MCP after PR creation.

## Repository Context

Use the current Goo Galaxy architecture and branch strategy when writing PRs:

- Base branch is `main`.
- The repository follows GitHub Flow with short-lived topic branches.
- PR summaries must use the real feature-oriented structure from `Assets/Scripts/Runtime`, `Assets/Data`, `Assets/Prefabs`, `Assets/Scenes`, `Assets/Settings`, and `Assets/Editor`.
- Refer to gameplay domains and systems using Goo Galaxy names such as `board`, `cards`, `match`, `networking`, `hud`, `input`, `progression`, `bootstrap`, and `shared`.
- Do not describe the project using old Spellwright or DOTS-specific architecture unless the actual change explicitly touches legacy documentation.

## GitHub CLI Usage

Use GitHub CLI for PR operations.

- Create pull requests with `gh pr create`.
- Update labels, title, or body after creation with `gh pr edit`.
- Use the current branch as the PR head unless the user explicitly asks for something else.
- Prefer passing explicit `--base`, `--head`, `--title`, and `--body-file` values when creating the PR.

## PR Title Format

All PR titles must follow this format:

```text
<type>(<scope>): <subject>
```

Rules:

- `type` is mandatory and lowercase.
- `scope` is mandatory, lowercase, and must describe the affected area.
- `subject` is mandatory, lowercase, imperative, and must not end with a period.
- Keep the subject under 72 characters.

Allowed title types:

- `feat`: a new feature.
- `fix`: a bug fix.
- `docs`: documentation only changes.
- `style`: changes that do not affect the meaning of the code.
- `refactor`: a code change that neither fixes a bug nor adds a feature.
- `perf`: a code change that improves performance.
- `test`: adding missing tests or correcting existing tests.
- `build`: changes that affect the build system or external dependencies.
- `ci`: changes to CI configuration files and scripts.
- `chore`: other changes that do not modify source or test files.
- `revert`: reverts a previous commit.

Prefer Goo Galaxy scopes such as:

- `bootstrap`
- `board`
- `cards`
- `hud`
- `input`
- `match`
- `networking`
- `progression`
- `shared`
- `tests`
- `docs`
- `build`
- `ci`

These title rules must remain compatible with the PR Check workflow.

## Template Selection

Use `.github/instructions/templates/pr-template.md` as the PR body source.

Inside that template, choose exactly one task-type section:

- `Feature Additions` for feature work
- `Technical Improvements` for refactors, optimizations, architecture work, tooling, or delivery improvements
- `Bug Fix Details` for bug fixes and regressions

Optional supporting context blocks:

- `story.md` when reviewers need a short story summary from Notion MCP
- `epic.md` when reviewers need epic-level context from Notion MCP

Rules:

- Remove all placeholders from `pr-template.md` before returning the final PR body.
- Keep the final output in English.
- Use Notion MCP as the source for task, story, epic, priority, acceptance criteria, and URLs.

## Task And Story IDs

Use the current repository identifiers:

- `GOOE`: epic identifiers.
- `GOOS`: story identifiers.
- `GOOT`: standard task identifiers.
- `GOOM`: MVP task identifiers.

Rules:

- Use `GOOT` or `GOOM` in the PR task header.
- Use `GOOS` for the parent story.
- Use `GOOE` only when it is genuinely relevant in references or notes.
- If no task or story ID is available, ask before inventing references.

## Notion MCP Usage

When a PR references work tracked in Notion, use Notion MCP rather than any legacy Notion skill flow.

Use Notion MCP to:

- Fetch the task page.
- Fetch the parent story page.
- Fetch epic context only when it materially helps reviewers.
- Copy Definition of Done or acceptance criteria into the PR checklist.
- Pull priority, type, and linked references.
- Update the task page with the PR URL after the PR is created.

Do not mention or rely on Notion skills in the PR workflow.

## Labels

Assign 3 to 5 labels when the repository label set supports them.

### Labels (3-5 required)

**Type (1):** `type: feat`, `type: fix`, `type: docs`, `type: style`, `type: refactor`, `type: perf`, `type: test`, `type: build`, `type: ci`, `type: chore`, `type: revert`

**Priority (1):** `priority: critical`, `priority: high`, `priority: medium`, `priority: low`

**Context (1-2):**

- Domain: `domain: board`, `domain: cards`, `domain: match`, `domain: progression`
- Client: `client: hud`, `client: input`, `client: audio`
- Platform: `platform: networking`, `platform: bootstrap`, `platform: shared`, `platform: rendering`
- Infrastructure: `infra: build`, `infra: ci`, `infra: tests`, `infra: docs`

**Status (optional):** `status: blocked`, `status: in progress`, `status: needs review`, `status: needs testing`

### Label Selection Rules

- Always assign 1 type label and 1 priority label.
- Add 1 or 2 context labels based on the main affected area.
- Add at most 1 optional status label when it materially helps reviewers.
- If the repository labels do not exist exactly as written, use the closest available labels and say so.

### Type Label Heuristics

- Title type `feat` -> `type: feat`
- Title type `fix` -> `type: fix`
- Title type `docs` -> `type: docs`
- Title type `style` -> `type: style`
- Title type `refactor` -> `type: refactor`
- Title type `perf` -> `type: perf`
- Title type `test` -> `type: test`
- Title type `build` -> `type: build`
- Title type `ci` or changes under `.github/workflows/` -> `type: ci`
- Title type `chore` -> `type: chore`
- Title type `revert` -> `type: revert`

### Priority Label Source

- Prefer the task priority fetched from Notion MCP.
- Fallback to `priority: medium` if no priority is available.

## Body Writing Rules

- Write the PR body in English only.
- Remove all placeholders and instructional markers from `pr-template.md` before returning the final PR body.
- Select exactly one task-type section inside the PR template: Feature, Tech, or Bug.
- Use `story.md` or `epic.md` only as optional supporting context blocks when they materially help reviewers.
- Summarize reviewer-relevant outcomes, not a commit-by-commit changelog.
- Include `Key Technical Decisions` with rationale when the PR changes architecture, flow, tooling, networking, or non-obvious implementation choices.
- Use concrete file paths, metrics, and validation notes when available.
- Keep references aligned with Goo Galaxy architecture and actual repo paths.

## Files Section Rules

Prefer the real Goo Galaxy structure when describing file changes. Group changes under paths such as:

- `Assets/Scripts/Runtime/Board/`
- `Assets/Scripts/Runtime/Cards/`
- `Assets/Scripts/Runtime/HUD/`
- `Assets/Scripts/Runtime/Input/`
- `Assets/Scripts/Runtime/Match/`
- `Assets/Scripts/Runtime/Networking/`
- `Assets/Scripts/Runtime/Progression/`
- `Assets/Scripts/Runtime/Bootstrap/`
- `Assets/Scripts/Runtime/Shared/`
- `Assets/Scripts/Tests/EditMode/`
- `Assets/Scripts/Tests/PlayMode/`
- `Assets/Data/`
- `Assets/Prefabs/`
- `Assets/Scenes/`
- `Assets/Settings/`
- `Assets/Editor/`

Do not fabricate old folder structures such as `Assets/Scripts/Components/Common/` or generic architecture buckets that do not exist in this repository.

## Definition Of Done Rules

- Copy the real acceptance criteria from Notion MCP whenever available.
- Mark items as completed only when the work is actually done.
- If the task is not complete, prefer a draft PR or stop and ask before generating a misleading done checklist.

## References Rules

- Always include the Notion task link and story link when available.
- Include relevant repository documentation when it materially helps reviewers.
- Prefer Goo Galaxy docs under `.docs/GDD/` when architecture context is relevant.
- If the PR touches networking, sessions, or multiplayer flow, prefer `.docs/GDD/08_Technical_Architecture_and_Multiplayer.md` as a supporting reference.

## Quality Checks

Before creating or returning a PR draft, verify:

- Title follows `type(scope): subject`.
- Scope is present and specific.
- Subject is lowercase, imperative, has no period, and is under 72 characters.
- Type is one of the allowed PR Check values.
- Body uses `.github/instructions/templates/pr-template.md`.
- Task and story IDs use `GOOT`, `GOOM`, `GOOS`, and `GOOE` correctly.
- Exactly one task-type section remains in the final PR body.
- Definition of Done is copied from Notion MCP when available.
- Key Technical Decisions explains why important implementation choices were made.
- References include task, story, and useful documentation.
- Base branch is `main` unless the user explicitly overrides it.

## Best Practices

- Write for reviewers who need fast context, risk assessment, and verification status.
- Prefer small PRs with clear ownership over broad mixed changes.
- Call out multiplayer, session, or authoritative-state impacts explicitly when relevant.
- Mention tests run, test gaps, and known limitations honestly.
- Use a draft PR when acceptance criteria are not fully satisfied.

## Ambiguity Handling

- If the task type is unclear, ask whether the work should be framed as Feature, Tech, or Bug.
- If the repository labels are unknown, state that label selection is best-effort.
- If Notion data is unavailable, say the PR body is a draft missing synced task metadata rather than inventing it.
