---
name: create-commit
description: >-
  Stage changes and create git commits in the Goo Galaxy repository following the project's Conventional Commits convention with mandatory scopes, footer trackers, and multi-line message handling in PowerShell and Bash. Use whenever the user asks to create a commit, commit changes, write a commit message, or mentions committing — even if they don't explicitly mention formatting or conventions.
---

# Goo Galaxy: Create Commit

This skill **creates the commit**. Composing the message is a step, not the deliverable. Unless the user explicitly asks for a message only ("just draft the message", "what should the commit message be?"), always finish by running `git commit` and reporting the resulting commit hash and subject.

## Execution Workflow

Run these steps in order:

1. **Inspect the working tree** — run `git status --short` and `git diff --stat` (plus `git diff --staged --stat` when something is already staged) to see what changed.
2. **Read the actual diff** — run `git diff` / `git diff --staged` for the relevant files so the body describes real changes, not assumptions.
3. **Resolve trackers** — if a task ID (`GOOE`/`GOOS`/`GOOT`/`GOOM`) is known or the branch name encodes one (`feat/GOOM-1`), use the `track-task` skill to fetch task metadata for the footers. If no ID can be resolved, ask before adding footer references.
4. **Stage** — stage only files that belong to this commit. Use `git add <path>` with explicit paths; use `git add -A` only when the user asked to commit everything or the whole tree clearly belongs together. Never stage unrelated in-progress work without asking. Do not format or scan by hand: the `pre-commit` hook runs `lint-staged` and both secret gates for you, on exactly what you staged.
5. **Compose the message** — follow the format rules below.
6. **Create the commit** — run the commit command with the body as a multi-line argument (see Running the Commit Command). Never write the message to a temporary file, and **do not set `HUSKY=0`** — the hooks are meant to run.
7. **Verify** — run `git log -1 --stat` and report the commit hash, subject, and file count back to the user.
8. **Do not push** — pushing requires explicit user approval. Offer it as a next step instead.

### Splitting Commits

If the working tree contains clearly unrelated changes, propose a split into multiple commits, confirm the grouping with the user, then create each commit in sequence with its own staged file set.

### When Commits Fail

- The hook fails on formatting: `lint-staged` already rewrote and re-staged what it could, so read its output — what remains is something no formatter fixes, usually a line over 160 characters. Fix it by hand and retry.
- The hook fails on the Docker check: start Docker Desktop and retry. Never work around it with `HUSKY=0`.
- The hook fails on a secret finding: stop and report it. Treat it as a real leak until proven otherwise; never force the commit through.
- Nothing staged: report it and stop; do not create an empty commit.
- Merge conflicts or a dirty rebase state: stop and report; do not force past them.

## Commit Message Format

Every commit message must follow this structure:

```
type(scope): subject

[body]

[optional footer(s)]
```

Every part is mandatory except the footers. Separate subject, body, and footers with blank lines, and wrap all lines at 72 characters.

**Subject** — `type(scope): subject`, all lowercase, imperative mood (`add`, `fix`, `remove`), specific, no trailing period, under 72 characters. Not past tense (`added`), not vague (`update code`, `fix bug`).

**Body** — explains what changed and why, never repeats the subject.

**Sections** — at least one titled section with bullet points, e.g. `Implementation:`, `Tests:`, `Performance:`, `Fixes:`, `Configuration:`. Pick titles that fit the commit; bullets start uppercase.

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
- Do not append any `Co-Authored-By` trailer.
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

## Running the Commit Command

**Let the hooks run.** `HUSKY=0` used to be required here because the Commitizen `prepare-commit-msg` hook opened an interactive prompt that hung any non-interactive caller. It no longer does: that hook exits immediately when git already has a message, which is always the case below. Disabling Husky now only skips the formatting and secret gates, so do not do it.

Pass the subject in the first `-m` and the entire body plus footers in a second `-m` — git inserts the blank line between them. Never chain one `-m` per line, and never write the message to a temporary file. Supplying `-m` is also what keeps Commitizen out of the way, so the two rules reinforce each other.

PowerShell (this project's default shell) — use a literal here-string so `$` and backticks stay verbatim; the terminator `'@` must start at column 1:

```powershell
$body = @'
Body paragraph.

Implementation:
- First change description

Implements: GOOM-42
'@
git commit -m "type(scope): subject" -m $body
```

Bash / Git Bash — embed the newlines inside the quoted argument:

```bash
git commit -m "type(scope): subject" -m "Body paragraph.

Implementation:
- First change description

Implements: GOOM-42"
```

Never mix the two shells' quoting: a PowerShell here-string is a syntax error in bash, and a bash heredoc does nothing in PowerShell.

## Pre-flight

Before running the command, confirm: only the intended files are staged, the message satisfies the format rules above, footers use only `Implements` / `Part of` / `Related`, and the message is inline rather than in a file. `HUSKY=0` must not be set — the hooks are the gates.

After it runs, confirm the commit exists (`git log -1 --stat`) and report the hash, subject, and file count — plus the fact that nothing was pushed.
