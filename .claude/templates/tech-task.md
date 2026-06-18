# Tech Task Template

Use for refactorings, optimizations, and internal technical improvements. The step structure is a guideline — for tasks that don't fit sequential steps (e.g., data pipelines, test suites), use a narrative or grouped structure. Adapt sections as needed.

---

````markdown
# [Tech Title - Clear Technical Objective]

### 🎯 Technical Objective

- **What:** [Technical change being made]
- **Why:** [Benefit/motivation with quantifiable improvement if possible]

---

### ✅ Definition of Done

_Adapt the list below — add, remove, or reorder items to fit the task._

1. [Specific measurable outcome]
2. [Specific measurable outcome with performance target]
3. [Verification method]
4. [Test requirement]
5. [Documentation update]
6. Branch created following naming convention and link added to Notion task property "Branch"
7. PR created, reviewed, approved, and merged

---

### ⚙️ Technical Refinement

**Architecture:** _(recommended — establishes the pattern and assembly placement)_

[One-line pattern description.]

**Implementation Steps:** _(use step-by-step, narrative, or grouped format — whichever fits the task best)_

_Step format example:_

- **Step 1: [Title]** — [Specific files to modify, changes to make, rationale]
- **Step 2: [Title]** — [Specific files to modify, changes to make, rationale]

_Performance validation (include when relevant):_

- Open Unity Profiler (Window > Analysis > Profiler)
- Navigate to [specific test scene]
- Measure [specific metric]
- Target: [specific target value]

**Assembly Dependencies:** _(add when crossing assembly boundaries)_

```
GooGalaxy.Runtime.[Feature]
├── references: GooGalaxy.Runtime.Shared  ([types used])
├── does NOT reference: [assemblies intentionally excluded]
└── InternalsVisibleTo: GooGalaxy.Runtime.Tests.EditMode
```

**Test Coverage:** _(add specific test files and scenarios)_

- `Assets/Scripts/Tests/EditMode/[TestFileName].cs` — [Specific scenario covered]

**Performance Impact:** _(include when the change affects performance)_

- Before: [metric with value]
- After: [metric with improved value]

**Performance Budget:** _(optional — include when introducing new runtime operations)_

| Metric           | Target         | Rationale         |
| ---------------- | -------------- | ----------------- |
| [Operation name] | [target value] | [why this target] |

---

### ⚠️ Potential Risks

_List risks relevant to this task. Use categories appropriate to the work (migration, testing, performance, team knowledge, rollback, etc.)._

_Choose the emoji color based on the risk: 🔴 critical (breaks builds/crashes), 🟠 high (degrades core flow), 🟡 medium (edge case or future concern), 🟢 low (cosmetic or unlikely), 🔵 informational (team knowledge, future-proofing)._

- 🔴 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address]
  - Rollback: [Rollback strategy — include for risky changes]

---

### 🔗 References

- **🎨 Design Files:**
  - [Behavior Flow Diagram](URL)
- **📚 Documentation:**
  - [Goo Galaxy Technical Architecture](.docs/GDD/08_Technical_Architecture_and_Multiplayer.md)
  - [Unity 6 or package documentation](URL)
- **🌐 External Resources:**
  - [Reference Implementation](URL)
````

---

## Quality Checks

_These are guidelines, not hard gates. Use judgment._

- [ ] Technical Objective explains WHAT + WHY with quantifiable improvement
- [ ] Definition of Done adapted to fit the task (not copy-pasted from template)
- [ ] Technical Refinement starts with Architecture pattern
- [ ] Implementation steps are clear and followable
- [ ] Performance impact quantified when applicable (before/after)
- [ ] Assembly Dependencies shown when crossing assembly boundaries
- [ ] Test Coverage lists specific files and scenarios
- [ ] Risks are categorized with emoji (🔴🟠🟡🟢🔵) matching severity; rollback strategy included for risky changes
- [ ] PR workflow items close the Definition of Done
- [ ] Sections marked optional are omitted when they don't apply
- [ ] Final document is self-contained — a reviewer can understand it without reading other task docs
