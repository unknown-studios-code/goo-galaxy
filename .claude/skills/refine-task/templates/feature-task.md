# Feature Task Template

Use for new gameplay/user-facing functionality. Adapt sections as needed — the goal is a complete, reviewer-ready design, not rigid adherence to every subsection.

---

````markdown
# [Feature Title - Specific and Implementation-Focused]

### 📝 Description

[Describe what needs to be implemented from a gameplay/user perspective. Be specific about the visible behavior.]

---

### ✅ Definition of Done

_Adapt the list below — add, remove, or reorder items to fit the task._

1. [Create specific file/component with exact path]
2. [Implement specific functionality]
3. [Add specific fields/properties]
4. [Write tests for specific scenarios]
5. [Profile and verify performance target]
6. [Verify no errors or warnings in Unity Console]
7. [Add XML documentation comments to all public APIs]
8. Branch created following naming convention and link added to Notion task property "Branch"
9. PR created following PR template and link added to Notion task property "Pull Request"
10. Pull Request reviewed, approved, and merged

---

### ⚙️ Technical Refinement

**Architecture:** _(recommended — establishes the pattern and assembly placement)_

[One-line pattern description. Example: "Humble Object Pattern — all grid logic lives in pure C# classes under `GooGalaxy.Runtime.Board`. MonoBehaviours in Task 16 will observe this domain layer."]

**Core Scripts:** _(add when creating new files)_

- `Assets/Scripts/Runtime/[Feature]/[FileName].cs` - [Script responsibility]

**Key Prefabs/Assets:** _(add when creating or modifying assets)_

- `Assets/Data/[Feature]/[Asset].asset` - [Asset purpose]
- `Assets/Prefabs/[Path]/[Prefab].prefab` - [Prefab purpose]

**Inspector Values:** _(add when exposing fields in the Unity Inspector)_

- In `[Component].cs`, expose `[type] [FieldName] = [default]` (description)

**Implementation Notes:**

- Use `[specific API/pattern]` for [specific purpose]
- [Additional implementation guidance]

**Assembly Dependencies:** _(add when crossing assembly boundaries)_

```
GooGalaxy.Runtime.[Feature]
├── references: GooGalaxy.Runtime.Shared  ([types used])
├── does NOT reference: [assemblies intentionally excluded]
└── InternalsVisibleTo: GooGalaxy.Tests.EditMode
```

**Test Coverage:** _(add specific test files and scenarios)_

- `Assets/Scripts/Tests/EditMode/[TestFileName].cs` — [Specific scenario covered]

**Edge Cases:** _(optional — add if the feature has non-obvious scenarios)_

| Scenario                | Behavior            |
| ----------------------- | ------------------- |
| [Edge case description] | [Expected behavior] |

**Performance Budget:** _(optional — include when introducing runtime operations)_

| Metric                  | Target         | Rationale         |
| ----------------------- | -------------- | ----------------- |
| [Method/operation name] | [target value] | [why this target] |

**Naming Convention:** _(optional — add when introducing new terms or conventions)_

| Term           | Meaning              | Notes                |
| -------------- | -------------------- | -------------------- |
| [Term in code] | [What it represents] | [Additional context] |

---

### ⚠️ Potential Risks

_List risks relevant to this task. Use categories appropriate to the work (performance, edge case, integration, testing, etc.). Risk count varies — some tasks have 1, others have 5._

_Choose the emoji color based on the risk: 🔴 critical (breaks builds/crashes), 🟠 high (degrades core flow), 🟡 medium (edge case or future concern), 🟢 low (cosmetic or unlikely), 🔵 informational (team knowledge, future-proofing)._

- 🔴 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address]
- 🟡 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address]

---

### 🔗 References

- **🎨 Design Files:**
  - [Behavior Flow Diagram](URL)
- **📚 GDD:** _(always `<mention-page>` — take the URL from the `read-gdd` skill)_
  - <mention-page url="https://app.notion.com/3b856d55129b8150b24ee9eaa76020bf">Technical Architecture & Multiplayer</mention-page> — [why this chapter matters to the task]
- **📐 Rules & Code:**
  - `.claude/rules/[rule-file].md` — [what it governs here]
  - `Assets/Scripts/Runtime/[Feature]/[File].cs` — [what a reader should look at]
- **🌐 External Resources:**
  - [Unity 6 or package documentation](URL)
````

---

## Quality Checks

_These are guidelines, not hard gates. Use judgment._

- [ ] Description focuses on user-facing behavior
- [ ] Definition of Done adapted to fit the task (not copy-pasted from template)
- [ ] File paths are exact and follow project structure
- [ ] Technical Refinement starts with Architecture pattern
- [ ] Implementation Notes include algorithm choices and performance considerations
- [ ] Assembly Dependencies shown when crossing assembly boundaries
- [ ] Test Coverage lists specific files and scenarios
- [ ] Risks are implementation-specific (not generic) and categorized with emoji (🔴🟠🟡🟢🔵) matching severity
- [ ] Performance Budget included for performance-sensitive features
- [ ] PR workflow items (Branch, PR, Review) close the Definition of Done
- [ ] Sections marked optional are omitted when they don't apply
- [ ] Final document is self-contained — a reviewer can understand it without reading other task docs
