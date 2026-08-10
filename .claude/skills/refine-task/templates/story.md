# User Story Template

Use when creating a User Story — a single, coherent piece of work that delivers value to a user. Stories bridge product intent and technical implementation: keep the user story non-technical, then use Technical Refinement for implementation details. Adapt sections as needed.

---

````markdown
# [Story Title - Clear, Action-Oriented]

### 👤 User Story

- **As a** [user type]
- **I want to** [action]
- **So that** [benefit]

---

### ✅ Acceptance Criteria

_Observable, testable behaviors that prove this story is complete. Number varies — typically 3-7._

1. [Observable, testable behavior]
2. [Observable, testable behavior]
3. [Observable, testable behavior]

---

### ⚙️ Technical Refinement

**Components Needed:** _(add when introducing new data structures)_

- `ComponentName` (purpose: what data it represents)

**Systems Needed:** _(add when introducing new runtime systems)_

- `SystemName` (responsibility: what it does, not how)

**Data Flow:** _(optional — add when multiple systems interact in sequence)_

```
[Source]
↓
[Transformation Step] (description)
↓
[Final Output]
```

**Key Architectural Decisions:**

- **Decision 1:** [Decision description]
  - **Rationale:** [Why this approach]

**Assembly Dependencies:** _(add when crossing assembly boundaries)_

```
GooGalaxy.Runtime.[Feature]
├── references: GooGalaxy.Runtime.Shared  ([types used])
├── does NOT reference: [assemblies intentionally excluded]
└── InternalsVisibleTo: GooGalaxy.Tests.EditMode
```

**Integration Points:**

- [How this story connects to other systems/stories]
- [Dependencies on other stories (GOOS-X)]

---

### ⚠️ Potential Risks

_List risks relevant to this story. Risk count varies — some stories have 1, others have 4._

_Choose the emoji color based on the risk: 🔴 critical (blocks story completion), 🟠 high (threatens sprint delivery), 🟡 medium (edge case, dependency), 🟢 low (cosmetic, unlikely), 🔵 informational (knowledge gap, documentation need)._

- 🔴 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address or reduce risk]

---

### 🔗 References

- **🎨 Design Files:**
  - [Architecture Diagram](URL)
  - [UI Mockup](URL)
- **📚 GDD:** _(always `<mention-page>` — take the URL from the `read-gdd` skill)_
  - <mention-page url="https://app.notion.com/3b856d55129b8150b24ee9eaa76020bf">Technical Architecture & Multiplayer</mention-page> — [why this chapter matters to the story]
- **📐 Rules & Code:**
  - `.claude/rules/[rule-file].md` — [what it governs here]
- **🌐 External Resources:**
  - [Unity 6, NGO, or MPS reference](URL)
````

---

## Quality Checks

_These are guidelines, not hard gates._

- [ ] User story follows "As a... I want... So that..." format
- [ ] Acceptance criteria are behavioral, testable, and focus on outcomes
- [ ] Components section describes data structures with clear purposes (WHAT, not HOW)
- [ ] Systems section describes behaviors with clear responsibilities (WHAT, not HOW)
- [ ] Data flow diagram shows conceptual transformations (when included)
- [ ] Architectural decisions include rationale (WHY, not just WHAT)
- [ ] Assembly Dependencies shown when crossing assembly boundaries
- [ ] Integration points identify dependencies on other stories
- [ ] Risks are categorized with emoji (🔴🟠🟡🟢🔵) matching severity and include mitigation
- [ ] References include design files, documentation, and external resources
- [ ] Uses correct Goo Galaxy terminology (runtime service, presenter, ScriptableObject, prefab, scene)
- [ ] Code snippets limited to type signatures and structural examples — not full method bodies
