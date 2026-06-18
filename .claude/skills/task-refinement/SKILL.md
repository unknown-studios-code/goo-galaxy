---
name: task-refinement
description: >-
  Refine or create Goo Galaxy tasks, stories, and epics using the project's
  structured templates. Use whenever the user asks to create a task, refine a
  task, draft a story, write up a bug report, design a feature spec, create an
  epic, or improve a task description — even if they don't mention templates
  explicitly.
---

# Goo Galaxy Task Refinement

When asked to create or refine a task, story, or epic, use the templates in
`templates/`. Each template provides the required structure, optional
sections, and quality checks — the goal is a complete, self-contained document
a reviewer can understand without reading anything else.

## Template Selection

Determine the right template from what the user describes:

| User says                             | Template                    |
| :------------------------------------ | :-------------------------- |
| New feature, gameplay functionality   | `templates/feature-task.md` |
| Bug fix, defect, regression           | `templates/bug-task.md`     |
| Refactor, optimization, internal tech | `templates/tech-task.md`    |
| User story, product requirement       | `templates/story.md`        |
| Large initiative, multi-story body    | `templates/epic.md`         |

If the type is unclear, ask whether the work is a feature, bug, tech
improvement, story, or epic before picking a template.

## Process

1. **Read the template** — always read the chosen template file from
   `templates/` first. Do not work from memory.
2. **Gather context** — ask the user for details that fill each required
   section. Don't invent specifics without asking.
3. **Fill the template** — produce a complete markdown document following the
   template structure exactly. Adapt optional sections — include them when
   they add value, omit them when they don't apply.
4. **Apply quality checks** — run through the template's Quality Checks
   checklist before presenting the result. Flag anything missed.

## Refinement Mode

When refining an existing task (not creating from scratch):

- Read the current task content first
- Identify gaps against the template structure: missing sections, vague
  descriptions, skipped quality checks
- Preserve existing content — fill gaps and sharpen vague language, don't
  rewrite from scratch unless the task type is wrong
- Be specific about what you changed and why

## Cross-Template Rules

These apply regardless of which template is used:

### Risk Emoji Convention

Every template uses the same risk severity colors:

| Emoji | Severity      | Use when                                          |
| :---- | :------------ | :------------------------------------------------ |
| 🔴    | Critical      | Breaks builds, crashes, blocks completion         |
| 🟠    | High          | Degrades core flow, threatens delivery            |
| 🟡    | Medium        | Edge case, dependency, future concern             |
| 🟢    | Low           | Cosmetic, unlikely                                |
| 🔵    | Informational | Team knowledge, platform concern, future-proofing |

### Definition of Done

- Every task must close with branch creation and PR workflow items:
  - Branch created and linked to Notion task property "Branch"
  - PR created, reviewed, approved, and merged
- Adapt the rest of the DoD checklist — add, remove, or reorder items to fit
  the specific task. Never copy-paste the template's example list verbatim.

### Assembly Dependencies

When work crosses assembly boundaries, document them:

```
GooGalaxy.Runtime.[Feature]
├── references: GooGalaxy.Runtime.Shared  ([types used])
├── does NOT reference: [assemblies intentionally excluded]
└── InternalsVisibleTo: GooGalaxy.Runtime.Tests.EditMode
```

### File Paths

Use real Goo Galaxy paths (`Assets/Scripts/Runtime/<Domain>/`,
`Assets/Scripts/Tests/EditMode/`, `Assets/Data/`, `Assets/Prefabs/`,
`Assets/Scenes/`, `Assets/Settings/`, `Assets/Editor/`). Never fabricate
folder structures.

### References

Every task should include, when applicable:

- Design files (Figma links, diagrams)
- Documentation (`.docs/GDD/` files, Unity docs)
- External resources (reference implementations)

### Language

- **Stories and Epics:** stakeholder-friendly, minimal technical jargon
- **Feature, Bug, Tech tasks:** technical precision, exact paths and type
  signatures

## Quality Gate

Before presenting the final document, verify:

- Template structure is followed (not copy-pasted placeholders)
- Required sections are filled with specifics, not placeholders
- Optional sections are included only when they add value
- File paths are exact and follow project structure
- Assembly dependencies are documented when crossing boundaries
- Risks are categorized with the correct emoji and include mitigation
- PR workflow items close the Definition of Done
- The document is self-contained — no external reading required to understand
