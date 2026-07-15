---
description: "Use when documenting Unity C# code. Covers XML comment rules, Unity inspector tooltips, self-documenting code, and inline comment conventions."
applyTo: "Assets/Scripts/**/*.cs"
---

# Unity Code Documentation

## 1. Overview

This document defines rules for documenting C# code in the codebase. It aims to maximize comment and metadata utility for both developers and designers while eliminating redundancy, clutter, and obsolete comments.

## 2. Cross-References

- **Code Style** → [unity-code-style.instructions.md](unity-code-style.instructions.md) (Verify naming conventions and general class layout)
- **Debugging** → [unity-debugging.instructions.md](unity-debugging.instructions.md) (Understand assertion patterns and error logging)

## 3. Core Rules

- **Rule 1 (XML Documentation `///` Restriction):** Do not write XML comments for private/internal members, obvious signatures, Unity lifecycle methods, overridden methods, or standard fields.
- **Rule 2 (XML Documentation `///` Application):** Write XML comments exclusively for interface contracts, abstract members, public APIs crossing Assembly Definition (`.asmdef`) boundaries, and global utility or extension methods.
- **Rule 3 (Inspector Tooltips `[Tooltip]`):** Apply `[Tooltip("...")]` to every public or serialized (`[SerializeField]`/`[SerializeReference]`) field. Frame explanations for designers and technical artists: explain the variable's impact, units of measurement, and safety limits. Use `[Header("...")]` to group serialized variables.
- **Rule 4 (Self-Documenting Code):** Express architectural constraints using C# features. Prefer expression-bodied members (`=>`) for simple accessors. Use init-only properties (`init`) for post-initialization immutability. Avoid inline logic comments by extracting complex evaluations into clearly named boolean properties.
- **Rule 5 (Inline Comments `//`):** Write inline comments only to explain "why" a non-standard or complex decision was made (e.g., engine bugs, performance hacks, or necessary workarounds). Never write inline comments that explain "what" standard code is doing.
- **Rule 6 (Test File Comments):** Enforce the GIVEN-WHEN-THEN block structure in all unit and integration test methods. Explicitly partition the test flow using `// GIVEN`, `// WHEN`, and `// THEN` inline comments.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <Type> : MonoBehaviour
{
    // ❌ Boilerplate XML on private member
    /// <summary>Updates speed</summary>
    private void UpdateSpeed() { }

    // ❌ Inline comment explaining obvious mechanics
    // Check if player is grounded and jump cooldown is complete
    if (_isGrounded && _jumpCooldown <= 0)
    {
        // ...
    }

    // ❌ Missing Tooltip on serialized field
    [SerializeField] private float _speed = 5f;
}

// ❌ Unstructured test method lacking clear block comments
[Test]
public void <TestName>()
{
    var player = new Player();
    player.TakeDamage(10);
    Assert.AreEqual(90, player.Health);
}
```

### ✅ Do (Good)

```csharp
public class <Type> : MonoBehaviour
{
    [Header("<Section Name>")]
    [Tooltip("<Explanation of impact, scale units (e.g., meters/sec), and safe bounds>")]
    [SerializeField] private float _speed = 5f;

    // ✅ Extracted descriptive boolean property instead of inline comments
    private bool CanJump => _isGrounded && _jumpCooldown <= 0;

    private void Awake()
    {
        if (CanJump)
        {
            // WORKAROUND: Force physics transform synchronization due to Unity Rigidbody interpolation bug
            Physics.SyncTransforms();
        }
    }
}

// ✅ Structured test utilizing standard block comments to partition setup, execution, and verification
[Test]
public void <TestName>()
{
    // GIVEN
    var player = new Player();

    // WHEN
    player.TakeDamage(10);

    // THEN
    Assert.AreEqual(90, player.Health);
}
```

## 5. Quick Reference & Decision Matrix

| Code Element                             | Documentation Pattern            | Rationale                                                                       |
| :--------------------------------------- | :------------------------------- | :------------------------------------------------------------------------------ |
| `public interface <Interface>`           | `/// <summary>`                  | Contract definition; propagates IntelliSense across the project.                |
| `[SerializeField] private <Type> _field` | `[Tooltip("...")]`               | Exposes descriptive metadata to designers in the UI Toolkit Inspector.          |
| Private Helper Methods                   | **None**                         | Rely on short, focused methods with self-documenting naming.                    |
| Unity Lifecycle Callbacks (e.g. `Start`) | **None**                         | Avoid boilerplate on standard engine methods.                                   |
| Counter-intuitive Workarounds            | `// WORKAROUND: ...`             | Clarifies specific anomalies, workarounds, or critical optimization logic.      |
| Test Methods                             | `// GIVEN`, `// WHEN`, `// THEN` | Enforces structural partition for test setup, action execution, and assertions. |
