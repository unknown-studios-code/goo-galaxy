---
description: "Use when writing commit messages, creating git commits, or suggesting commit text for this repository. Enforces Conventional Commits, Commitizen-compatible formatting, Goo Galaxy scopes, footer rules, and HUSKY=0 for automated commits."
name: "Commit Message Standards"
---

# Git Commit Message Standards

Follow these rules whenever generating a commit message or creating a commit in this repository.

## Required Format

Use this structure:

```text
<type>(<scope>): <subject>

[optional body]

[optional footer(s)]
```

Rules:

- `type` is mandatory and lowercase.
- `scope` is mandatory, lowercase, and must describe the affected area.
- `subject` is mandatory, lowercase, imperative, and must not end with a period.
- Keep the subject under 72 characters.
- Separate subject, body, and footers with blank lines.

## Allowed Types

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

Use `feat` for MINOR version bumps. Use `fix` or `perf` for PATCH bumps.

## Preferred Goo Galaxy Scopes

Prefer scopes that match the current project structure:

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

If none of these fit, choose the smallest clear subsystem name and keep it lowercase.

## Subject Rules

Do:

- Use imperative mood such as `add`, `fix`, `change`, `remove`.
- Start with lowercase.
- Keep it specific.
- Keep it under 72 characters.

Do not:

- Use past tense such as `added` or `fixed`.
- Capitalize the first word.
- End with a period.
- Use vague text such as `update code` or `fix bug`.

## Body Rules

Add a body when the change is complex, breaking, spans multiple related changes, or needs historical context.

Body rules:

- Leave one blank line after the subject.
- Wrap lines at 72 characters.
- Explain what changed and why it changed.
- Use bullet points when multiple points improve clarity.
- Start bullet points with uppercase.
- Do not repeat the subject.
- Do not focus on low-level implementation details.

## Organized Sections for Larger Commits

For broader commits, use grouped sections in the body:

```text
<type>(<scope>): <subject>

[short introductory paragraph]

Implementation:
- First grouped change
- Second grouped change

Tests:
- First testing change
- Second testing change

[footer(s)]
```

Common section names:

- `Implementation:`
- `Tests:`
- `Performance:`
- `Fixes:`
- `Configuration:`

## Allowed Footers

Only use these footer labels:

- `Implements: GOOT-XX or GOOM-XX`
- `Part of: GOOS-X`
- `Related: GOOE-XX, GOOT-XX, GOOM-XX, or GOOS-X`

Identifier meanings:

- `GOOE`: epic identifiers.
- `GOOS`: story identifiers.
- `GOOT`: standard task identifiers.
- `GOOM`: MVP task identifiers.

Footer rules:

- Use one footer per line.
- Keep the label exactly as written.
- Include all relevant references when available.
- Do not invent other footer labels.

Examples:

```text
Implements: GOOT-42
Part of: GOOS-5
Related: GOOE-2, GOOT-15
```

## Commit Examples

Simple:

```text
feat(board): add first turn state setup
```

With body and footers:

```text
fix(match): prevent null state during rematch flow

Match restart could reuse an incomplete runtime state after a fast
disconnect and reconnect sequence. Resetting the state before the new
setup keeps the rematch flow deterministic.

Implements: GOOT-15
Part of: GOOS-3
```

Structured body:

```text
feat(networking): add reconnect bootstrap flow

Add a reconnect path that restores session bootstrap data before the
player returns to the active match.

Implementation:
- Restore connection metadata before scene re-entry
- Rebuild player session state from cached bootstrap data

Tests:
- Add edit mode coverage for reconnect bootstrap guards
- Add play mode coverage for successful session resume

Implements: GOOM-42
Part of: GOOS-8
Related: GOOE-4, GOOT-39
```

These section names must stay aligned with `.github/instructions/references/conventions.md`.

## Agent Commit Rules

When creating commits programmatically, always disable Husky hooks:

```bash
HUSKY=0 git commit -m "type(scope): subject"
```

Do not create automated commits without `HUSKY=0`.

## Final Checklist

Before returning a commit message or creating a commit, verify:

- Type is valid and lowercase.
- Scope is present and matches the affected Goo Galaxy module or subsystem.
- Subject is imperative, lowercase, specific, and has no period.
- Subject is under 72 characters.
- Body explains what and why when needed.
- Body lines wrap at 72 characters.
- Footer only uses `Implements`, `Part of`, or `Related`.
- Automated commits use `HUSKY=0`.

## Ambiguity Handling

If task or story identifiers are not provided, ask before adding footer references.

If the user provides identifiers that do not match the `GOOE`, `GOOS`, `GOOT`, or `GOOM` patterns, ask whether the footer convention should be updated for this repository before committing.
