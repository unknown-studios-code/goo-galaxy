---
name: create-commit
description: >-
  Stage changes and create git commits in the Goo Galaxy repository following the project's Conventional Commits convention with mandatory scopes, footer trackers, and PowerShell HUSKY=0 handling. Use whenever the user asks to create a commit, commit changes, write a commit message, or mentions committing — even if they don't explicitly mention formatting or conventions.
---

# Goo Galaxy: Create Commit

This skill **creates the commit**. Composing the message is a step, not the deliverable. Unless the user explicitly asks for a message only ("just draft the message", "what should the commit message be?"), always finish by running `git commit` and reporting the resulting commit hash and subject.

## Execution Workflow

Run these steps in order:

1. **Inspect the working tree** — run `git status --short` and `git diff --stat` (plus `git diff --staged --stat` when something is already staged) to see what changed.
2. **Read the actual diff** — run `git diff` / `git diff --staged` for the relevant files so the body describes real changes, not assumptions.
3. **Resolve trackers** — if a task ID (`GOOE`/`GOOS`/`GOOT`/`GOOM`) is known or the branch name encodes one (`feat/GOOM-1`), use the `track-task` skill to fetch task metadata for the footers. If no ID can be resolved, ask before adding footer references.
4. **Stage the changes** — stage only files that belong to this commit. Use `git add <path>` with explicit paths; use `git add -A` only when the user asked to commit everything or the whole tree clearly belongs together. Never stage unrelated in-progress work without asking.
5. **Compose the message** — follow the format rules below.
6. **Create the commit** — run the commit command with `HUSKY=0` (see Creating Automated Commits), passing the body as a multi-line argument. Never write the message to a temporary file.
7. **Verify** — run `git log -1 --stat` and report the commit hash, subject, and file count back to the user.
8. **Do not push** — pushing requires explicit user approval. Offer it as a next step instead.

### Splitting Commits

If the working tree contains clearly unrelated changes, propose a split into multiple commits, confirm the grouping with the user, then create each commit in sequence with its own staged file set.

### When Commits Fail

- Husky/format hook failures: run `npm run format`, re-stage, retry.
- Nothing staged: report it and stop; do not create an empty commit.
- Merge conflicts or a dirty rebase state: stop and report; do not force past them.

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

`bootstrap`, `board`, `cards`, `energy`, `hud`, `input`, `match`, `networking`, `progression`, `shared`, `tests`, `docs`, `build`, `ci`

If none of these fit, choose the smallest clear subsystem name and keep it lowercase.

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

Common section names: `Implementation:`, `Tests:`, `Performance:`, `Fixes:`, `Configuration:`

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

When creating commits programmatically, always disable Husky and Commitizen hooks by setting `HUSKY=0`. The syntax depends on the shell in use — check the environment before choosing.

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

PowerShell does not support bash-style inline environment variables. Set the variable explicitly before the git command:

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

### Preferred: multi-line body in a single `-m`

Every Goo Galaxy commit needs a multi-line body with organized sections. Pass that body as one multi-line argument instead of chaining many `-m` flags, and never write it to a temporary file — no cleanup, no leftover artifacts, no risk of committing the temp file itself.

Git treats each `-m` as its own paragraph and inserts a blank line between them, so use the first `-m` for the subject and the second for the whole body plus footers.

In PowerShell, build the body with a literal here-string (`@'` … `'@`) so `$` and backticks in the text are not expanded. The terminator `'@` must start at column 1:

```powershell
$body = @'
Body paragraph.

Implementation:
- First change description

Implements: GOOM-42
'@
$env:HUSKY = "0"; git commit -m "type(scope): subject" -m $body
```

In bash, embed the newlines directly in the quoted argument:

```bash
HUSKY=0 git commit -m "type(scope): subject" -m "Body paragraph.

Implementation:
- First change description

Implements: GOOM-42"
```

Both shells pass the argument to git verbatim, so the body keeps its line breaks exactly as written.

### How to choose

- If the environment says `Shell: bash` (or `sh`, `zsh`), use Bash syntax.
- If the environment says `Shell: powershell` (or `pwsh`), use PowerShell syntax.
- When unsure, the `echo $0` command reveals the current shell.
- Never mix the two — `$env:HUSKY` fails in bash, `HUSKY=0` at the start of a line has no effect in PowerShell.

Never create automated commits without `HUSKY=0`.

## Final Verification

Before running `git commit`, verify:

- Type is valid and lowercase.
- Scope is present and matches the affected Goo Galaxy module or subsystem.
- Subject is imperative, lowercase, specific, and has no period.
- Subject is under 72 characters.
- Body is present and explains what and why
- All lines wrapped at 72 characters.
- At least one organized section with bullet points is present.
- Footer only uses `Implements`, `Part of`, or `Related`.
- Only intended files are staged.
- The command sets `HUSKY=0`.
- The message is passed inline via `-m`, not through a temporary file.

After running `git commit`, verify:

- The command exited successfully and the commit exists (`git log -1`).
- The committed file list matches what was intended.
- Report the commit hash and subject to the user, and confirm nothing was pushed.
