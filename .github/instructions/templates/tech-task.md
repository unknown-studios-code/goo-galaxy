# Tech Task Template

Use for refactorings, optimizations, and internal technical improvements. Replace all placeholder content with real implementation details.

---

```markdown
# [Tech Title - Clear Technical Objective]

### 🎯 Technical Objective

- **What:** [Technical change being made]
- **Why:** [Benefit/motivation with quantifiable improvement if possible]

---

### ✅ Definition of Done

1. [Specific measurable outcome]
2. [Specific measurable outcome with performance target]
3. [Backward compatibility requirement]
4. [Verification method]
5. [Test requirement]
6. [Documentation update]
7. Branch created following naming convention and link added to Notion task property "Branch"
8. PR created, reviewed, approved, and merged

---

### ⚙️ Technical Refinement

**Step 1: [Step Title]**

- [Specific file to modify with path]
- [Specific change to make]
- [Keep unchanged aspects]

**Step 2: [Step Title]**

- [Specific file to modify with path]
- [Specific API change (old → new)]
- [Affected files to update]

**Step 3: [Step Title]**

- [Specific files to update]
- [Query/filter changes]
- [Affected systems list]

**Step 4: [Performance Validation]**

- Open Unity Profiler (Window > Analysis > Profiler)
- Navigate to [specific test scene]
- Measure [specific metric]
- Target: [specific target value]

**Step 5: [Compatibility Testing]**

- [Load/verify specific assets]
- [Verify specific behaviors]
- [Check specific UI elements]

**Step 6: [Documentation Updates]**

- Add section in [documentation path] about [topic]
- Document when to use [pattern/approach]

**Performance Impact:**

- Before: [metric with value]
- After: [metric with improved value]
- **[X]x improvement** in [specific area]

---

### ⚠️ Potential Risks

- 🔴 **Migration Risk:** [Risk description]
  - Mitigation: [How to address]
  - Rollback: [Rollback strategy]
- 🟡 **Testing Coverage:** [Risk description]
  - Mitigation: [How to address]
- 🟠 **Performance Regression:** [Risk description]
  - Mitigation: [How to address]
- 🔵 **Team Knowledge:** [Risk description]
  - Mitigation: [How to address]

---

### 🔗 References

- **📚 Documentation:**
  - [Unity 6 or package documentation](URL)
  - [Goo Galaxy Technical Architecture](.docs/GDD/08_Technical_Architecture_and_Multiplayer.md)
- **🌐 External Resources:**
  - [Technical Talk/Article](URL)
```

---

## Tech Task Quality Checks

- [ ] Technical Objective explains WHAT + WHY with quantifiable improvement
- [ ] Definition of Done includes performance metrics (before/after)
- [ ] Technical Refinement provides step-by-step plan
- [ ] Performance Impact section shows concrete improvement ("10x faster")
- [ ] Rollback strategy included for risky changes
- [ ] Documentation updates included in DoD
