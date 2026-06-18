# User Story Template

Copy this template structure when creating a User Story. Replace all placeholder content with real project-specific information.

---

```markdown
# [Story Title - Clear, Action-Oriented]

### 👤 User Story

- **As a** [user type]
- **I want to** [action]
- **So that** [benefit]

---

### ✅ Acceptance Criteria

1. [Observable, testable behavior that proves this story is complete]
2. [Observable, testable behavior that proves this story is complete]
3. [Observable, testable behavior that proves this story is complete]
4. [Observable, testable behavior that proves this story is complete]
5. [Observable, testable behavior that proves this story is complete]

---

### ⚙️ Technical Refinement

**Components Needed:**

- `ComponentName` (purpose: what data it represents)
- `ComponentName` (purpose: what data it represents)

**Systems Needed:**

- `SystemName` (responsibility: what it does, not how)
- `SystemName` (responsibility: what it does, not how)

**Data Flow:**
```

[Source]
↓
[Transformation Step] (description)
↓
[Next Step] (description)
↓
[Final Output]

```

**Key Architectural Decisions:**

- **Decision 1:** [Decision description]
  - **Rationale:** [Why this approach]
- **Decision 2:** [Decision description]
  - **Rationale:** [Why this approach]

**Integration Points:**

- [How this story connects to other systems/stories]
- [Dependencies on other stories (GOOS-X)]
- [Unity packages used]

---

### ⚠️ Potential Risks

- 🔴 **Technical Risk:** [Risk description]
  - Mitigation: [How to address or reduce risk]
- 🟡 **Dependency Risk:** [Risk description]
  - Mitigation: [How to address or reduce risk]
- 🟠 **Knowledge Risk:** [Risk description]
  - Mitigation: [How to address or reduce risk]
- 🔵 **Scope Risk:** [Risk description]
  - Mitigation: [How to address or reduce risk]

---

### 🔗 References

- **🎨 Design Files:**
    - [Architecture Diagram](URL)
    - [UI Mockup](URL)
- **📚 Documentation:**
  - [Goo Galaxy Technical Architecture](.docs/GDD/08_Technical_Architecture_and_Multiplayer.md)
  - [Unity 6, NGO, or MPS reference](URL)
- **🌐 External Resources:**
    - [Reference Implementation](URL)
```

---

## Story Quality Checks

Before presenting the Story, verify:

- [ ] User story follows "As a... I want... So that..." format
- [ ] Acceptance criteria are behavioral, testable, and focus on outcomes
- [ ] Components section lists data structures with clear purposes (WHAT, not HOW)
- [ ] Systems section lists behaviors with clear responsibilities (WHAT, not HOW)
- [ ] Data flow diagram shows conceptual transformations (not function calls)
- [ ] Architectural decisions include rationale (WHY, not just WHAT)
- [ ] Integration points identify dependencies on other stories
- [ ] Risks are categorized with emoji (🔴🟡🟠🔵) and include mitigation
- [ ] References include design files, documentation, and external resources
- [ ] NO implementation details (no code snippets, no file paths, no line numbers)
- [ ] Uses correct Goo Galaxy terminology (runtime service, presenter, ScriptableObject, prefab, scene)
- [ ] All emoticons and markdown formatting preserved
