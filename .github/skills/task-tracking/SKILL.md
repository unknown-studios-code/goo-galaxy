---
name: task-tracking
description: >-
  Look up and update Goo Galaxy tasks, stories, and epics in Notion using
  GOOE/GOOS/GOOT/GOOM identifiers. Use whenever the user mentions a task ID,
  needs Notion task metadata for commits or PRs, or asks to update task
  status in Notion — even if they don't explicitly say "Notion."
---

# Goo Galaxy Task Tracking (Notion MCP)

When task, story, epic, branch, or pull request metadata needs to be read
from or written to Notion, use the Notion MCP tools directly. Do not rely on
legacy tool wrappers or skill-based Notion flows.

## Available Operations

| Operation    | Use Case                                                            |
| :----------- | :------------------------------------------------------------------ |
| Search       | Find tasks, stories, epics by `GOOE`, `GOOS`, `GOOT`, or `GOOM` IDs |
| Update page  | Update task properties with branch name, PR link, and priority      |
| Create pages | Create new Notion pages only when explicitly requested              |

## ID Conventions

| Prefix | Meaning          | Format    |
| :----- | :--------------- | :-------- |
| `GOOE` | Epic identifier  | `GOOE-XX` |
| `GOOS` | Story identifier | `GOOS-X`  |
| `GOOT` | Standard task    | `GOOT-XX` |
| `GOOM` | MVP task         | `GOOM-XX` |

## Search Workflow

Use Notion MCP search with internal query type. For example, searching
`GOOM-13` resolves to "Assembly Definitions & Edit Mode Test Suite".

Search for the task before creating commit messages, pull requests, or
updating task metadata. Use search results to identify the correct Notion
page.

## Update Task Properties

After PR creation, update the Notion task page with:

| Property         | Type   | Value                          |
| :--------------- | :----- | :----------------------------- |
| `Branch`         | Text   | GitHub branch name             |
| `Pull Request`   | URL    | GitHub PR link                 |
| `Priority`       | Select | Priority level for PR labels   |
| `userDefined:ID` | Text   | Task ID (`GOOT-X` or `GOOM-X`) |

## Extractable Information

When reading a task page, these properties are useful:

| Property   | Use For                 |
| :--------- | :---------------------- |
| `Name`     | Commit subject, PR body |
| `Priority` | PR labels               |
| `Content`  | Definition of Done      |
| `URL`      | PR references           |

## Safety Rules

- Do not guess page IDs — always search first.
- Do not assume property names beyond the workflow defaults listed above.
- If search results are ambiguous, ask which Notion page should be updated.
- If task metadata is unavailable, say that the PR or commit text is a draft
  missing synced Notion data rather than inventing it.
- Do not promise full field-by-field page inspection unless a read-capable
  Notion MCP tool is available and has been used successfully.
