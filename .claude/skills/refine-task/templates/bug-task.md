# Bug Task Template

Use to fix identified defects and problems. Adapt sections as needed — some bugs are simple one-liners that don't need every section; others are complex regressions that need full detail.

---

```markdown
# [Bug Title - Specific and Descriptive]

### 🐛 Bug Report

**Steps to Reproduce:**

1. [Step with specific scene/file/action]
2. [Step with specific action]
3. [Step with specific observation point]

**Expected Behavior:**

[Describe what SHOULD happen]

**Actual Behavior:**

[Describe what ACTUALLY happens, including error messages]

**Environment:** _(include what's relevant — not all fields are needed for every bug)_

- **Unity Version:** [e.g., 6000.3.11f1]
- **Build Target:** [e.g., Android, iOS, Editor Play Mode]
- **Commit Hash:** `[hash]` (branch: `[branch-name]`)
- **Reproducibility:** [100% / Intermittent with frequency]
- **Packages/Services:** [if relevant]

---

### Root Cause Analysis

[After investigation, document WHY the bug occurs. For simple bugs, a single sentence is enough.]

**Root cause:** [Brief summary of the fundamental cause]

---

### ✅ Definition of Done

_Adapt the list below — add, remove, or reorder items to fit the bug._

1. [Specific fix applied to specific file]
2. Bug no longer reproducible following original steps
3. No errors in Console
4. Regression test added to [test file path]
5. Fix tested on relevant targets: [Android, iOS, Editor Play Mode]
6. Branch created and link added to Notion task property "Branch"
7. PR created, reviewed, approved, and merged

---

### ⚙️ Technical Refinement

**Architecture:** _(recommended — which assembly/domain is affected)_

[Example: "Bug is in `GooGalaxy.Runtime.Board` — `ResolveConversions` method. Fix is local to Board."]

**Fix Steps:** _(use step-by-step, narrative, or grouped format — whichever fits the bug best)_

- **Step 1: [Title]** — [File to modify, change to make, rationale]
- **Step 2: [Title]** — [File to modify, change to make, rationale]

**Regression Test:**

- Create `[test file path]`
- Test case: [describe what the test validates]
- Use `[Test]` attribute for Unity Test Runner

---

### ⚠️ Potential Risks

_List risks relevant to this bug. Simple fixes may have no risks — that's fine._

_Choose the emoji color based on the risk: 🔴 critical (could cause new crash/regression), 🟠 high (affects core gameplay), 🟡 medium (edge case), 🟢 low (cosmetic side effect), 🔵 informational (platform-specific concern)._

- 🔴 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address]

---

### 🔗 References

- **🐛 Bug Tracking:**
  - [Console Log Screenshot](URL)
  - [Profiler Snapshot](URL)
- **📚 GDD:** _(always `<mention-page>` — take the URL from the `read-gdd` skill)_
  - <mention-page url="https://app.notion.com/3b856d55129b8150b24ee9eaa76020bf">Technical Architecture & Multiplayer</mention-page> — [the rule the defect violates]
- **📐 Rules & Code:**
  - `.claude/rules/[rule-file].md` — [what it governs here]
  - `Assets/Scripts/Runtime/[Feature]/[File].cs` — [where the defect lives]
- **🌐 External Resources:**
  - [Relevant Unity documentation, or a related fix](URL)
```

---

## Quality Checks

_These are guidelines, not hard gates. Simple bugs need fewer checks._

- [ ] Reproduction steps are clear enough for another developer to follow
- [ ] Expected vs Actual behavior contrasted
- [ ] Environment info includes what's relevant (version, platform, commit, reproducibility)
- [ ] Root Cause Analysis explains WHY (not just WHAT)
- [ ] Regression test added (for logic bugs; not always needed for asset/config fixes)
- [ ] Fix tested on affected platforms
- [ ] Technical Refinement identifies affected assembly/domain
- [ ] Risks section can be empty if the fix has no side effects; when present, risks are categorized with emoji (🔴🟠🟡🟢🔵) matching severity
- [ ] PR workflow items close the Definition of Done
