# Bug Task Template

Use to fix identified defects and problems. Replace all placeholder content with real bug details.

---

```markdown
# [Bug Title - Specific and Descriptive]

### 🐛 Bug Report

**Steps to Reproduce:**

1. [Exact step with specific scene/file]
2. [Exact step with specific action]
3. [Exact step with specific input]
4. [Exact step with specific observation point]

**Expected Behavior:**

[Describe what SHOULD happen in detail]

**Actual Behavior:**

[Describe what ACTUALLY happens, including error messages]

**Environment:**

- **Unity Version:** [e.g., 6000.3.11f1]
- **Relevant Packages/Services:**
  - [package name] [version]
  - [package name] [version]
- **Build Target:** [e.g., Android, iOS, Editor Play Mode]
- **Device Specs:** [OS, CPU, GPU, RAM if relevant]
- **Commit Hash:** `[hash]` (branch: `[branch-name]`)
- **Reproducibility:** [100% / Intermittent with frequency]

---

### Root Cause Analysis

[After investigation, document WHY the bug occurs. Technical explanation of the underlying issue.]

**Root cause:** [Brief summary of the fundamental cause]

---

### ✅ Definition of Done

1. [Specific fix applied to specific file]
2. Bug no longer reproducible following original steps (tested X+ times)
3. [Specific expected behavior now works]
4. No errors in Console
5. [Memory/resource cleanup verified]
6. Fix tested on all relevant targets: [Android, iOS, Editor Play Mode]
7. Regression test added to [test file path]
8. Verified with [stress test scenario]
9. PR includes detailed explanation of fix and root cause
10. Branch created and link added to Notion task property "Branch"
11. PR created, reviewed, approved, and merged

---

### 🔧 Implementation Plan

**Step 1: [Fix Step Title]**

- Open `[file path]`
- [Specific change to make]
- [Rationale for this change]

**Step 2: [Additional Fix Step]**

- In `[file/method]`, add check: `[specific code/logic]`
- [Prevents specific issue]

**Step 3: [Create Regression Test]**

- Create `[test file path]`
- Test case: [describe test scenario]
- Use `[Test]` attribute for Unity Test Runner

**Step 4: [Validate Resource Cleanup]**

- Open [Profiler/Tool] (Window > Analysis > [Tool])
- Capture snapshot [before/after scenario]
- Verify [no leaked resources]

---

### ⚠️ Potential Risks

- 🔴 **Side Effect Risk:** [Risk description]
  - Mitigation: [How to address]
- 🟡 **Regression Risk:** [Risk description]
  - Mitigation: [How to address]
- 🟠 **Performance Risk:** [Risk description]
  - Mitigation: [How to address]
- 🔵 **Platform Risk:** [Risk description]
  - Mitigation: [How to address]

---

### 🔗 References

- **🐛 Bug Tracking:**
  - [Notion Bug Report](URL)
  - [Console Log Screenshot](URL)
  - [Profiler Snapshot](URL)
- **📚 Documentation:**
  - [Relevant Unity Documentation](URL)
  - [Goo Galaxy Technical Architecture](.docs/GDD/08_Technical_Architecture_and_Multiplayer.md)
```

---

## Bug Task Quality Checks

- [ ] Reproduction steps are exact and detailed (any developer can reproduce)
- [ ] Expected vs Actual behavior clearly contrasted
- [ ] Environment info complete (versions, platform, commit hash, reproducibility %)
- [ ] Root Cause Analysis explains WHY (not just WHAT)
- [ ] Regression test required in Definition of Done
- [ ] Fix tested across multiple platforms if applicable
