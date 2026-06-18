# Goo Galaxy — Claude Code Project Instructions

@package.json
@.claude/templates/pr-template.md

## Project Identity

Goo Galaxy is a Unity 6 mobile strategy game. The repository follows **GitHub Flow** targeting `main` with short-lived topic branches, Conventional Commits, and Notion-tracked tasks.

## Build & Test Commands

```bash
# Restore .NET dependencies
dotnet restore goo-galaxy.slnx

# Build all projects
dotnet build goo-galaxy.slnx

# Run Edit Mode tests
dotnet test GooGalaxy.Runtime.Tests.csproj

# Format C# code
dotnet csharpier .
```

## Architecture

Unity 6 project with runtime code under `Assets/Scripts/Runtime/`, organized by gameplay domain:

| Domain      | Path                                  |
| ----------- | ------------------------------------- |
| Board       | `Assets/Scripts/Runtime/Board/`       |
| Cards       | `Assets/Scripts/Runtime/Cards/`       |
| HUD         | `Assets/Scripts/Runtime/HUD/`         |
| Input       | `Assets/Scripts/Runtime/Input/`       |
| Match       | `Assets/Scripts/Runtime/Match/`       |
| Networking  | `Assets/Scripts/Runtime/Networking/`  |
| Progression | `Assets/Scripts/Runtime/Progression/` |
| Bootstrap   | `Assets/Scripts/Runtime/Bootstrap/`   |
| Shared      | `Assets/Scripts/Runtime/Shared/`      |

Tests live under `Assets/Scripts/Tests/EditMode/` and `Assets/Scripts/Tests/PlayMode/`.

Game design documents are in `.docs/GDD/`. For networking, sessions, or multiplayer architecture, reference `.docs/GDD/08_Technical_Architecture_and_Multiplayer.md`.

## Conventions

- **Commits:** Conventional Commits with mandatory scope. Automated commits MUST set `HUSKY=0`. See `.claude/rules/commit-messages.md`.
- **Pull Requests:** Conventional Commits titles, targeting `main`, PR body from `.claude/templates/pr-template.md`. Use GitHub MCP first; fall back to `gh` CLI. See `.claude/rules/pull-requests.md`.
- **Task Tracking:** Notion MCP for GOOE/GOOS/GOOT/GOOM task IDs. See `.claude/rules/notion-mcp.md`.
- **Code Style:** CSharpier formatting (config in `.csharpierrc.json`), EditorConfig (`.editorconfig`).
- **Language:** All PR bodies, commit messages, and code comments in English.

## Git Workflow

- Base branch is `main`
- Short-lived topic branches
- Commits use `type(scope): subject` format with Goo Galaxy scopes
- Automated commits bypass Husky hooks via `$env:HUSKY = "0"` in PowerShell
- PowerShell is this project's shell — no bash-style inline env vars

## MCP Tools

This project benefits from these MCP servers (configured at user level, not in this repo):

- **GitHub** — PR creation, label management, repository operations (primary)
- **Notion** — task/story/epic lookup and property updates
- **Figma** — design references and asset extraction

No local MCP configuration is stored in this repository.
