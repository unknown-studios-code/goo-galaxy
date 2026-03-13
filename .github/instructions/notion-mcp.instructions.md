---
description: "Use when searching Notion tasks or stories, extracting task metadata for commits or pull requests, or updating Notion task properties such as Branch name and Pull Request URL. Covers the Notion MCP tools available in this environment for Goo Galaxy task tracking."
name: "Notion MCP Usage"
---

# Notion MCP Usage

Use these rules whenever task, story, epic, branch, or pull request metadata needs to be read from or written to Notion.

## Available Tools

| Tool                             | Use Case                                                                                      |
| -------------------------------- | --------------------------------------------------------------------------------------------- |
| `mcp_notion_notion-search`       | Find tasks, stories, epics, and related Notion pages by `GOOE`, `GOOS`, `GOOT`, or `GOOM` IDs |
| `mcp_notion_notion-update-page`  | Update task properties with branch name and PR link                                           |
| `mcp_notion_notion-create-pages` | Create new Notion pages only when explicitly requested                                        |

Do not instruct the agent to use legacy `notion-search`, `notion-fetch`, or `CallMcpTool` wrappers.

## ID Conventions

Use the current project identifiers:

- `GOOE-XX` for epics
- `GOOS-X` for stories
- `GOOT-XX` for standard tasks
- `GOOM-XX` for MVP tasks

## Search Workflow

Use `mcp_notion_notion-search` with `query_type: "internal"`.

```json
{
  "query": "GOOM-13",
  "query_type": "internal"
}
```

Verified result in this environment:

- `GOOM-13` resolves to `Assembly Definitions & Edit Mode Test Suite`
- URL: `https://www.notion.so/32156d55129b81b8ad97f4bf4c171dfd`

Use search results to identify the correct Notion page before creating commit messages, pull requests, or updating task metadata.

## Update Task Properties

Use `mcp_notion_notion-update-page`.

```json
{
  "page_id": "<task_page_id>",
  "command": "update_properties",
  "properties": {
    "Branch": "tech/GOOM-13",
    "Pull Request": "https://github.com/unknown-studios-code/goo-galaxy/pull/42",
    "Priority": "Medium"
  }
}
```

## Property Reference

| Property         | Type   | Use                            | Verification                            |
| ---------------- | ------ | ------------------------------ | --------------------------------------- |
| `Branch`         | Text   | GitHub branch name             | Confirmed by project workflow reference |
| `Pull Request`   | URL    | GitHub PR link                 | Confirmed by project workflow reference |
| `Priority`       | Select | PR labels and prioritization   | Confirmed by project workflow reference |
| `userDefined:ID` | Text   | Task ID (`GOOT-X` or `GOOM-X`) | Confirmed by project workflow reference |
| `Name`           | Title  | Task title                     | Confirmed by `GOOM-13` search result    |

## Extractable Information

| Property   | Use For                 |
| ---------- | ----------------------- |
| `Name`     | Commit subject, PR body |
| `Priority` | PR labels               |
| `Content`  | Definition of Done      |
| `URL`      | PR references           |

## Verification Notes

The current Notion MCP toolset exposed in this environment supports search and update operations, but it does not expose a standalone page fetch tool.

Because of that:

- Task existence and page URL can be verified directly through `mcp_notion_notion-search`
- The property names above are confirmed from the established project workflow and update usage
- Full field-by-field page inspection should not be promised unless a read-capable Notion MCP tool becomes available

## Safety Rules

- Do not guess page IDs.
- Do not assume property names beyond the workflow defaults above when the user or project workflow does not confirm them.
- If search results are ambiguous, ask which Notion page should be updated.
- If task metadata is unavailable, say that the PR or commit text is a draft missing synced Notion data.
