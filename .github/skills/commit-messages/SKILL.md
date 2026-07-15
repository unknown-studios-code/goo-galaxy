---
name: commit-messages
description: >-
  Format git commit messages following Goo Galaxy's Conventional Commits
  convention with mandatory scopes, footer trackers, and PowerShell HUSKY=0
  handling. Use whenever the user asks to create a commit, write a commit
  message, generate a commit, or mentions committing changes — even if they
  don't explicitly mention formatting or conventions.
---

# Goo Galaxy Commit Messages

When creating a git commit for this repository, always produce a commit
message that follows the Conventional Commits format with Goo Galaxy-specific
scopes and footers.

## Commit Message Format

Every commit message must follow this structure:

```
type(scope): subject

[body]

[optional footer(s)]
```

## Format Rules

- **type** is mandatory and lowercase.
- **scope** is mandatory, lowercase, and must describe the affected area.
- **subject** is mandatory, lowercase, imperative, and must not end with a period.
- **subject** must be under 72 characters.
- **body** is mandatory and must explain what changed and why.
- **organized sections** with bullet points are mandatory.
- Separate **subject**, **body**, and **footers** with blank lines.
- All lines must be wrapped at 72 characters.

## Allowed Types

| Type       | Description                                               |
| :--------- | :-------------------------------------------------------- |
| `feat`     | A new feature (MINOR version bump)                        |
| `fix`      | A bug fix (PATCH bump)                                    |
| `docs`     | Documentation only changes                                |
| `style`    | Changes that do not affect the meaning of the code        |
| `refactor` | A code change that neither fixes a bug nor adds a feature |
| `perf`     | A code change that improves performance (PATCH bump)      |
| `test`     | Adding missing tests or correcting existing tests         |
| `build`    | Changes to the build system or external dependencies      |
| `ci`       | Changes to CI configuration files and scripts             |
| `chore`    | Other changes that do not modify source or test files     |
| `revert`   | Reverts a previous commit                                 |

## Goo Galaxy Scopes

Use the scope that matches the affected project area:

`bootstrap`, `board`, `cards`, `energy`, `hud`, `input`, `match`, `networking`,
`progression`, `shared`, `tests`, `docs`, `build`, `ci`

If none of these fit, choose the smallest clear subsystem name and keep it
lowercase.

## Subject Rules

**Do:**

- Use imperative mood: `add`, `fix`, `change`, `remove`
- Start with lowercase
- Keep it specific
- Keep it under 72 characters

**Don't:**

- Use past tense (`added`, `fixed`)
- Capitalize the first word
- End with a period
- Use vague text (`update code`, `fix bug`)

## Mandatory Body

Every commit message must contain a body explaining what changed and why.

Body rules:

- Leave one blank line after the subject.
- Explain what changed and why.
- Start bullet points with uppercase if used in the main body.
- Do not repeat the subject.

## Mandatory Organized Sections

Every commit message must contain at least one organized section with bullet points detailing the changes.
The agent may choose which section titles fit the commit message context (the titles below are merely examples).

Structure:

```
type(scope): subject

[short introductory paragraph]

Implementation:
- First change description
- Second change description

Tests:
- First testing change description

[footer(s)]
```

Common section names: `Implementation:`, `Tests:`, `Performance:`, `Fixes:`,
`Configuration:`

## Allowed Footers

Only use these footer labels:

| Footer        | Format                                       |
| :------------ | :------------------------------------------- |
| `Implements:` | `GOOT-XX` or `GOOM-XX`                       |
| `Part of:`    | `GOOS-X`                                     |
| `Related:`    | `GOOE-XX`, `GOOT-XX`, `GOOM-XX`, or `GOOS-X` |

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
- If task or story identifiers are not provided, ask before adding footer references.
- If identifiers don't match the expected patterns, ask whether the convention should be updated.

## Example

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

## Creating Automated Commits (HUSKY=0)

When creating commits programmatically, always disable Husky and Commitizen
hooks by setting `HUSKY=0`. The syntax depends on the shell in use — check
the environment before choosing.

### Bash / sh (Linux, macOS, Git Bash on Windows)

```bash
# Single-line commit:
HUSKY=0 git commit -m "type(scope): subject"

# Multi-line body (each -m adds a paragraph):
HUSKY=0 git commit -m "type(scope): subject" -m "body line 1" -m "body line 2"

# Amend:
HUSKY=0 git commit --amend -m "updated message"

# Rebase:
HUSKY=0 git rebase -i HEAD~2
HUSKY=0 git rebase --continue
```

### PowerShell (Windows)

PowerShell does not support bash-style inline environment variables. Set
the variable explicitly before the git command:

```powershell
# Single-line (semicolon chains the commands):
$env:HUSKY = "0"; git commit -m "type(scope): subject"

# Multi-line body:
$env:HUSKY = "0"
git commit -m "type(scope): subject" -m "body line 1" -m "body line 2"

# Amend:
$env:HUSKY = "0"
git commit --amend -m "updated message"

# Rebase:
$env:HUSKY = "0"
git rebase -i HEAD~2
```

### How to choose

- If the environment says `Shell: bash` (or `sh`, `zsh`), use Bash syntax.
- If the environment says `Shell: powershell` (or `pwsh`), use PowerShell
  syntax.
- When unsure, the `echo $0` command reveals the current shell.
- Never mix the two — `$env:HUSKY` fails in bash, `HUSKY=0` at the start
  of a line has no effect in PowerShell.

Never create automated commits without `HUSKY=0`.

## Final Verification

Before returning a commit message or creating a commit, verify:

- Type is valid and lowercase.
- Scope is present and matches the affected Goo Galaxy module or subsystem.
- Subject is imperative, lowercase, specific, and has no period.
- Subject is under 72 characters.
- Body is present and explains what and why
- All lines wrapped at 72 characters.
- At least one organized section with bullet points is present.
- Footer only uses `Implements`, `Part of`, or `Related`.
- Automated commits use `HUSKY=0`.
