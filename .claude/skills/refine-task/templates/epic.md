# Epic Template

Use when creating an Epic — a large body of work spanning multiple stories. Epics are stakeholder-facing: keep language non-technical and focused on outcomes. Adapt sections as needed.

---

```markdown
# [Epic Name - Clear and Concise Title]

### 🎯 Strategic Goal

[2-3 sentence "elevator pitch" explaining what this epic achieves and why it matters. Understandable by anyone, including non-technical stakeholders.]

---

### 🤔 Problem Statement

**What problem are we solving?**

[Specific problem description with quantifiable impact if possible]

**Who is it for?**

[Target user/persona who benefits]

---

### 💡 Value Hypothesis

[Describe the expected cause-and-effect. Format is flexible — use what communicates best.]

- **We believe that** [ACTION/CHANGE]
- **Will result in** [OUTCOME/BENEFIT]
- **Measured by** [METRIC/KPI]

---

### 📦 Scope

**In Scope:**

1. [Specific feature, deliverable, or work item]
2. [Specific feature, deliverable, or work item]

**Out of Scope:**

- [What is explicitly NOT being addressed]

---

### ⚙️ Architecture Notes _(optional — add when the epic spans multiple assemblies or systems)_

- **Assemblies affected:** [List of asmdefs]
- **Data/Assets affected:** [List of folders]
- **Cross-cutting concerns:** [Networking, persistence, analytics, etc.]

---

### 📈 Success Criteria

_Include quantitative metrics, qualitative acceptance criteria, or both — whichever fits the epic._

**Quantitative Metrics:**

- **Metric 1:** [MEASUREMENT with target value]

**Qualitative Acceptance Criteria:**

1. [Testable acceptance criterion]
2. [Testable acceptance criterion]

---

### ⚠️ Potential Risks

_List relevant risks. Epic-level risks focus on scope, dependencies, timelines, and technical unknowns._

_Choose the emoji color based on the risk: 🔴 critical (threatens epic completion), 🟠 high (significant delay or scope increase), 🟡 medium (manageable with mitigation), 🟢 low (minor concern), 🔵 informational (team/external dependency awareness)._

- 🔴 **[Risk Category]:** [Risk description]
  - Mitigation: [How to address or reduce risk]

---

### 🔗 References

- **🎨 Design Files:**
  - [Design Document Title](URL)
- **📚 GDD:** _(always `<mention-page>` — take the URL from the `read-gdd` skill)_
  - <mention-page url="https://app.notion.com/3b856d55129b8150b24ee9eaa76020bf">Technical Architecture & Multiplayer</mention-page> — [why this chapter scopes the epic]
- **📐 Rules & Code:**
  - `.claude/rules/[rule-file].md` — [what it governs here]
- **🌐 External Resources:**
  - [Reference Resource](URL)
```

---

## Quality Checks

_These are guidelines, not hard gates._

- [ ] Strategic Goal is a clear elevator pitch (non-technical)
- [ ] Problem statement explains who it's for and why it matters
- [ ] Value Hypothesis connects action → outcome → measurement
- [ ] Scope clearly separates IN SCOPE vs OUT OF SCOPE
- [ ] Architecture Notes identifies affected assemblies when applicable
- [ ] Success Criteria include measurable outcomes (quantitative, qualitative, or both)
- [ ] Risks focus on epic-level concerns and are categorized with emoji (🔴🟠🟡🟢🔵) matching severity
- [ ] References include design files and documentation
- [ ] Language is stakeholder-friendly (minimal technical jargon)
