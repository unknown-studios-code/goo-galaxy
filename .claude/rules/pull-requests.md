# Pull Request Standards

Follow these rules whenever drafting, reviewing, or creating a pull request for Goo Galaxy.

## Workflow

1. Identify the current branch, target branch, and task or story IDs.
2. Fetch task and story details with Notion MCP before writing the PR body.
3. Generate the PR title in Conventional Commits format.
4. Read `.claude/templates/pr-template.md`.
5. Generate the PR body from that template.
6. Assign labels using the mapping rules below.
7. Create the PR with `gh pr create`, targeting `main` unless the user explicitly asks for a different base.
8. Apply or adjust labels with `gh pr edit` when needed.
9. Update related Notion records with the PR URL using Notion MCP after PR creation.

## Repository Context

- Base branch is `main`.
- The repository follows GitHub Flow with short-lived topic branches.
- PR summaries must use the real feature-oriented structure from `Assets/Scripts/Runtime`, `Assets/Data`, `Assets/Prefabs`, `Assets/Scenes`, `Assets/Settings`, and `Assets/Editor`.
- Refer to gameplay domains and systems using Goo Galaxy names: `board`, `cards`, `match`, `networking`, `hud`, `input`, `progression`, `bootstrap`, and `shared`.
- Do not describe the project using old Spellwright or DOTS-specific architecture unless the actual change explicitly touches legacy documentation.

## GitHub CLI

- Create pull requests with `gh pr create`.
- Update labels, title, or body after creation with `gh pr edit`.
- Use the current branch as the PR head unless the user explicitly asks for something else.
- Prefer passing explicit `--base`, `--head`, `--title`, and `--body-file` values when creating the PR.

## PR Title Format

```
<type>(<scope>): <subject>
```

Rules:

- `type` is mandatory and lowercase.
- `scope` is mandatory, lowercase, and must describe the affected area.
- `subject` is mandatory, lowercase, imperative, and must not end with a period.
- Keep the subject under 72 characters.

**Allowed types:** `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`

**Goo Galaxy scopes:** `bootstrap`, `board`, `cards`, `hud`, `input`, `match`, `networking`, `progression`, `shared`, `tests`, `docs`, `build`, `ci`

These title rules must remain compatible with the PR Check workflow.

## Template Selection

Use `.claude/templates/pr-template.md` as the PR body source.

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

- `GOOE`: epic identifiers
- `GOOS`: story identifiers
- `GOOT`: standard task identifiers
- `GOOM`: MVP task identifiers

Rules:

- Use `GOOT` or `GOOM` in the PR task header.
- Use `GOOS` for the parent story.
- Use `GOOE` only when it is genuinely relevant in references or notes.
- If no task or story ID is available, ask before inventing references.

## Notion MCP Usage

When a PR references work tracked in Notion, use Notion MCP tools to:

- Fetch the task page.
- Fetch the parent story page.
- Fetch epic context only when it materially helps reviewers.
- Copy Definition of Done or acceptance criteria into the PR checklist.
- Pull priority, type, and linked references.
- Update the task page with the PR URL after the PR is created.

## Labels

Assign 3 to 5 labels when the repository label set supports them.

### Label Categories

**Type (1 required):** `type: feat`, `type: fix`, `type: docs`, `type: style`, `type: refactor`, `type: perf`, `type: test`, `type: build`, `type: ci`, `type: chore`, `type: revert`

**Priority (1 required):** `priority: critical`, `priority: high`, `priority: medium`, `priority: low`

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

## Files Section

Group changes under real Goo Galaxy paths:

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

Do not fabricate old folder structures or generic architecture buckets that do not exist in this repository.

## Definition Of Done

- Copy the real acceptance criteria from Notion MCP whenever available.
- Mark items as completed only when the work is actually done.
- If the task is not complete, prefer a draft PR or stop and ask before generating a misleading done checklist.

## References

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
- Body uses `.claude/templates/pr-template.md`.
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
