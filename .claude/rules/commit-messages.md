# Git Commit Message Standards

Follow these rules whenever generating a commit message or creating a commit in this repository.

## Required Format

```
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

- `feat`: a new feature (MINOR version bump)
- `fix`: a bug fix (PATCH bump)
- `docs`: documentation only changes
- `style`: changes that do not affect the meaning of the code
- `refactor`: a code change that neither fixes a bug nor adds a feature
- `perf`: a code change that improves performance (PATCH bump)
- `test`: adding missing tests or correcting existing tests
- `build`: changes that affect the build system or external dependencies
- `ci`: changes to CI configuration files and scripts
- `chore`: other changes that do not modify source or test files
- `revert`: reverts a previous commit

## Goo Galaxy Scopes

Prefer scopes that match the current project structure:
`bootstrap`, `board`, `cards`, `hud`, `input`, `match`, `networking`, `progression`, `shared`, `tests`, `docs`, `build`, `ci`

If none of these fit, choose the smallest clear subsystem name and keep it lowercase.

## Subject Rules

Do:

- Use imperative mood: `add`, `fix`, `change`, `remove`
- Start with lowercase
- Keep it specific
- Keep it under 72 characters

Don't:

- Use past tense (`added`, `fixed`)
- Capitalize the first word
- End with a period
- Use vague text (`update code`, `fix bug`)

## Body Rules

Add a body when the change is complex, breaking, spans multiple related changes, or needs historical context.

- Leave one blank line after the subject.
- Wrap lines at 72 characters.
- Explain what changed and why.
- Use bullet points when multiple points improve clarity.
- Start bullet points with uppercase.
- Do not repeat the subject.

## Organized Sections for Larger Commits

For broader commits, use grouped sections:

```
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

Common section names: `Implementation:`, `Tests:`, `Performance:`, `Fixes:`, `Configuration:`

## Allowed Footers

Only use these footer labels:

- `Implements: GOOT-XX or GOOM-XX`
- `Part of: GOOS-X`
- `Related: GOOE-XX, GOOT-XX, GOOM-XX, or GOOS-X`

Identifier meanings:

- `GOOE`: epic identifiers
- `GOOS`: story identifiers
- `GOOT`: standard task identifiers
- `GOOM`: MVP task identifiers

Footer rules:

- Use one footer per line.
- Keep the label exactly as written.
- Include all relevant references when available.
- Do not invent other footer labels.

## Commit Examples

Simple:

```
feat(board): add first turn state setup
```

With body and footers:

```
fix(match): prevent null state during rematch flow

Match restart could reuse an incomplete runtime state after a fast
disconnect and reconnect sequence. Resetting the state before the new
setup keeps the rematch flow deterministic.

Implements: GOOT-15
Part of: GOOS-3
```

Structured body:

```
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

## Agent Commit Rules — HUSKY=0

IMPORTANT: When creating commits programmatically, always disable Husky and Commitizen hooks by setting `HUSKY=0`.

### PowerShell (this project's shell)

PowerShell does not support bash-style inline environment variables. Always set the variable explicitly:

```powershell
# Single-line commit:
$env:HUSKY = "0"; git commit -m "type(scope): subject"

# Multi-line body (each -m adds a paragraph):
$env:HUSKY = "0"
git commit -m "type(scope): subject" -m "body line 1" -m "body line 2"

# For amend or rebase, set once before the operation:
$env:HUSKY = "0"
git commit --amend -m "updated message"
```

### Bash / sh

```bash
HUSKY=0 git commit -m "type(scope): subject"
```

### Operations Requiring HUSKY=0

| Operation               | Example                                                           |
| :---------------------- | :---------------------------------------------------------------- |
| `git commit`            | `$env:HUSKY = "0"; git commit -m "docs(gdd): update terminology"` |
| `git commit --amend`    | `$env:HUSKY = "0"; git commit --amend -m "new message"`           |
| `git rebase -i`         | `$env:HUSKY = "0"; git rebase -i HEAD~2`                          |
| `git rebase --continue` | `$env:HUSKY = "0"; git rebase --continue`                         |

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

- If task or story identifiers are not provided, ask before adding footer references.
- If identifiers don't match `GOOE`, `GOOS`, `GOOT`, or `GOOM` patterns, ask whether the footer convention should be updated before committing.
