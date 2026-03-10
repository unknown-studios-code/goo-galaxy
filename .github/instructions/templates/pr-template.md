# Pull Request Template

Copy this template structure when creating a Pull Request.

---

```markdown
## 📋 Task: GOOT-X or GOOM-X

**Story:** GOOS-X - Story Name
**Type:** Feature, Tech, or Bug
**Priority:** Critical, High, Medium, or Low

---

## 🎯 What Changed

[Provide a short high-level summary of what changed and why it matters. Focus on reviewer-facing outcomes, not an implementation diary.]

### [Choose one task-type section]

#### Feature Additions

- [New feature or user-facing behavior]
- [Integration point or supporting change]
- [Validation or quality note]

**Impact:** [Describe the user-facing or product impact.]

#### Technical Improvements

**Before:**

- [Previous state or limitation]
- [Previous state or limitation]
- [Previous state or limitation]

**After:**

- [Improved state]
- [Improved state]
- [Improved state]

**Impact:** [Describe the technical or delivery impact with metrics when available.]

#### Bug Fix Details

**Issue:** [What was broken]

**Root Cause:** [Why it happened]

**Solution:** [How it was fixed]

**Impact:** [Describe the practical effect of the fix for players, developers, or operations.]

---

## 🔑 Key Technical Decisions

1. **[Decision]** -> [Why this approach was chosen]
2. **[Decision]** -> [Why this approach was chosen]
3. **[Decision]** -> [Why this approach was chosen]

---

## 📁 Files Created/Modified

- `Path/To/File.cs` - [What changed]
- `Path/To/File.asset` - [What changed]

---

## ✅ Definition of Done

- [x] [Completed acceptance criterion copied from Notion MCP]
- [x] [Completed acceptance criterion copied from Notion MCP]
- [x] [Completed acceptance criterion copied from Notion MCP]

---

## 🔗 References

- **Notion Task:** [GOOT-X or GOOM-X](notion_url)
- **Story:** [GOOS-X - Story Name](notion_url)
- **Documentation:** [Relevant doc or file path](doc_or_file)
```

---

## PR Template Quality Checks

- [ ] Task ID uses `GOOT` or `GOOM`
- [ ] Story ID uses `GOOS`
- [ ] Exactly one task-type section remains in the final PR body
- [ ] "What Changed" is concise and reviewer-focused
- [ ] Files section lists actual changed files with concrete paths
- [ ] Definition of Done comes from Notion MCP and is only marked complete when true
- [ ] Key Technical Decisions explains rationale, not just what changed
- [ ] References include task, story, and supporting docs when relevant
- [ ] Final PR body is written in English only

## Important Notes

- **Select only ONE task-type section** (Feature/Tech/Bug) - delete the others
- **Mark all DoD items as complete** - only create PR when task is finished
- **Include rationale** for technical decisions (not just what, but WHY)
- **Verify all links** work before creating PR
- **Use correct emoticons** - they improve readability and scanning
- **Write for reviewers** - provide context they need to understand changes
- **Be specific** - concrete file paths, numbers, metrics
- **English only** - all PR content must be in English
