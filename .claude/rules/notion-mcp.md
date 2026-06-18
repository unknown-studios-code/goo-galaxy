# Notion MCP Usage

Use these rules whenever task, story, epic, branch, or pull request metadata needs to be read from or written to Notion.

## Available Operations

| Operation    | Use Case                                                                                      |
| ------------ | --------------------------------------------------------------------------------------------- |
| Search       | Find tasks, stories, epics, and related Notion pages by `GOOE`, `GOOS`, `GOOT`, or `GOOM` IDs |
| Update page  | Update task properties with branch name and PR link                                           |
| Create pages | Create new Notion pages only when explicitly requested                                        |

Use the Notion MCP tools available in your environment. Do not rely on legacy tool wrappers or skill-based Notion flows.

## ID Conventions

- `GOOE-XX` for epics
- `GOOS-X` for stories
- `GOOT-XX` for standard tasks
- `GOOM-XX` for MVP tasks

## Search Workflow

Use Notion MCP search with internal query type. Example query: `GOOM-13` → resolves to "Assembly Definitions & Edit Mode Test Suite".

Use search results to identify the correct Notion page before creating commit messages, pull requests, or updating task metadata.

## Update Task Properties

After PR creation, update the Notion task page with branch name and PR URL:

- `Branch` → GitHub branch name (text)
- `Pull Request` → GitHub PR link (URL)
- `Priority` → priority level for PR labels (select)

## Property Reference

| Property         | Type   | Use                            | Source               |
| ---------------- | ------ | ------------------------------ | -------------------- |
| `Branch`         | Text   | GitHub branch name             | Project workflow     |
| `Pull Request`   | URL    | GitHub PR link                 | Project workflow     |
| `Priority`       | Select | PR labels and prioritization   | Project workflow     |
| `userDefined:ID` | Text   | Task ID (`GOOT-X` or `GOOM-X`) | Project workflow     |
| `Name`           | Title  | Task title                     | Notion search result |

## Extractable Information

| Property   | Use For                 |
| ---------- | ----------------------- |
| `Name`     | Commit subject, PR body |
| `Priority` | PR labels               |
| `Content`  | Definition of Done      |
| `URL`      | PR references           |

## Safety Rules

- Do not guess page IDs.
- Do not assume property names beyond the workflow defaults above.
- If search results are ambiguous, ask which Notion page should be updated.
- If task metadata is unavailable, say that the PR or commit text is a draft missing synced Notion data rather than inventing it.

## Verification

The current Notion MCP toolset supports search and update operations. Task existence and page URL can be verified through search. If a read-capable tool becomes available, use it to inspect full page details. Until then, do not promise full field-by-field page inspection.
