---
description: "Use when documenting Unity C# code. Covers when XML docs are required, inspector tooltips, self-documenting code, and comment conventions."
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/Editor/**/*.cs"
---

# Unity Code Documentation

## 1. Overview

The default is **no comment and no XML doc**. Structure carries meaning first: a precise name, a small method, an extracted predicate, and a type that cannot hold an invalid state say more than any prose, and they cannot go stale. Documentation is the exception, reserved for what the reader cannot recover from the code itself — a contract across an assembly boundary, an invariant, a decision that looks wrong until explained.

Every comment is a maintenance liability. A comment that contradicts the code is worse than no comment, so delete or update it in the same change as the code it describes.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Naming rules that make most comments unnecessary)
- **Class Organization** → [unity-class-organization.md](unity-class-organization.md) (Predictable layout removes the need for navigational comments)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Assertions and log messages as executable documentation)
- **Testing** → [unity-testing.md](unity-testing.md) (Test naming and GIVEN-WHEN-THEN structure)

## 3. Core Rules

- **Rule 1 (Structure First):** Before writing a comment, try to remove the need for it: rename the symbol, extract the condition into a named boolean property (`CanJump`, `IsWithinRange`), extract the block into a method whose name states the intent, replace a magic value with a named constant, or replace a flag pair with an enum. Only document what survives that pass.
- **Rule 2 (Where XML Docs Are Required):** Write `///` docs on the API a caller consumes without reading the body: public and protected members of interfaces and abstract types, public types and members reachable across an `.asmdef` boundary, and general-purpose utilities and extension methods. Document the contract — meaning of parameters, meaning of the return value, what counts as invalid input — not the implementation.
- **Rule 3 (Where XML Docs Are Forbidden):** No `///` on private members, Unity lifecycle callbacks, overrides that add nothing to the base contract, serialized fields, or any member whose signature already says everything (`public int Health => _health;`). An `internal` member takes no `<summary>`, `<param>`, or `<returns>` for the same reason — its callers compile against the body — but it **may** carry a `<remarks>` stating an invariant Rule 4 requires, because an invariant is invisible at every accessibility level. On an implementation of a documented interface, use `<inheritdoc />` rather than copying the text — and only when it carries a `<remarks>` of its own, since an `<inheritdoc />` with nothing attached to it is noise this rule's default exists to prevent. An `[Inject] Construct` method follows the `internal` shape regardless of its accessibility: `<remarks>` only, and only for a genuine invariant such as injection ordering, because the container resolves parameter _types_ and never reads the documentation.
- **Rule 4 (Document Invariants, Not Mechanics):** When a member carries a rule the signature cannot express, state it: ownership and lifetime of a buffer passed to subscribers, allocation guarantees on a hot path, the order in which checks run when several can fail, "must be called after X", units and coordinate space. These are the notes that earn their place; a `<remarks>` block restating the method body is not.
- **Rule 5 (Inline Comments Explain Why):** Use `//` only for a reason that is invisible in the code: an engine bug or platform quirk being worked around, a deliberate performance trade-off, a non-obvious formula (hex axial math, easing curves) with a reference, or a deliberate omission. Never narrate what the next line does. Prefix workarounds with `// WORKAROUND:` and performance-driven oddities with `// PERF:` so they can be found and revisited.
- **Rule 6 (Inspector Tooltips):** Add `[Tooltip]` to serialized fields a designer or artist tunes, and make it earn its space: state the unit, the practical range, and what breaks outside it. Skip it on wiring references whose name and type already say everything (`[SerializeField] private UnitPresenter _unitPresenter;`). Group related serialized fields with `[Header]`; use `[Range]`, `[Min]`, and `[Space]` where they make the constraint enforceable rather than described.
- **Rule 7 (Banned Comment Forms):** No commented-out code — git holds the history. No file banners with author, date, or changelog. No `#region` used to hide a class that has grown too large. No `TODO` without a tracker ID and an intent (`// TODO (GOOM-42): replace the linear scan once the spatial index lands`). No decorative separators.
- **Rule 8 (Log Messages Are Documentation):** A log or assertion message is read at the worst possible moment. Say what failed, on which object, and what the reader should do about it; pass the object as the context argument (`Debug.Assert(_gridLayout != null, BoardLogMessages.GridLayoutConfigurationMissing, this)`), and keep the text as a `const` in the feature's message class. A class with no `UnityEngine.Object` to pass as context does not log at all — it returns a diagnostic and lets its MonoBehaviour caller write the message with `this` (see `AbilityDiagnostic`). That keeps the rule a pure function of board state and lets a test assert on the flag instead of matching a log line.
- **Rule 9 (Test Structure):** Test methods are documented by their name and shape, not by prose: `MethodUnderTest_Scenario_ExpectedOutcome`, with the body partitioned by literal `// GIVEN`, `// WHEN`, and `// THEN` comments. One behavior per test. A combined marker — `// GIVEN / WHEN` or `// WHEN / THEN` — is permitted only when the two adjacent phases collapse into a **single statement** (a helper that both arranges and acts; an `Assert.DoesNotThrow` that is simultaneously the act and the check). Never combine all three. Keep an empty `// GIVEN` when the fixture needs no arrangement: a missing marker reads as an oversight, an empty one reads as a decision. A `yield return null` immediately after the act, solely to let its effect land, stays inside `// WHEN`. A test that only compares an invariant, with neither arrange nor act, may carry `// THEN` alone.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <Type> : MonoBehaviour
{
    // ❌ XML doc that repeats the signature, on a private member
    /// <summary>Updates the speed.</summary>
    /// <param name="value">The value.</param>
    private void UpdateSpeed(float value) { }

    // ❌ Tooltip that adds nothing a designer can act on
    [Tooltip("The speed")]
    [SerializeField]
    private float _speed = 5f;

    // ❌ Wiring reference does not need a tooltip
    [Tooltip("The unit presenter.")]
    [SerializeField]
    private UnitPresenter _unitPresenter;

    private void Update()
    {
        // ❌ Narrates the code instead of explaining a decision
        // Check if the player is grounded and the jump cooldown is complete
        if (_isGrounded && _jumpCooldown <= 0f)
        {
            Jump();
        }

        // ❌ Dead code kept "just in case"
        // if (_legacyJump) { JumpLegacy(); }

        // ❌ Untracked TODO
        // TODO: fix this later
    }
}
```

### ✅ Do (Good)

```csharp
/// <summary>
/// Stateless legality checks for board moves. Shared across assemblies, so the contract is documented here.
/// </summary>
/// <remarks>
/// Checks run in a fixed order — source presence, ownership, capability, range, target vacancy — so the
/// returned code is predictable when several rules are broken at once. Allocation-free on every path.
/// </remarks>
public static class <MovementValidator>
{
    /// <summary>Validates a clone move onto an adjacent cell, leaving the source unit in place.</summary>
    /// <param name="grid">Board being played on. Must not be null.</param>
    /// <returns>The first rule that failed, or <see cref="MoveResult.Valid"/> when the move is legal.</returns>
    public static MoveResult ValidateClone(IHexGrid grid, MoveCommand command) => <Implementation>;
}

public class <Type> : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Meters per second. Above 12 the unit tunnels through thin colliders on low-tier devices.")]
    [Range(1f, 12f)]
    [SerializeField]
    private float _speedInMetersPerSecond = 5f;

    [SerializeField]
    private UnitPresenter _unitPresenter;

    // ✅ Named predicate replaces the comment that would have explained the condition
    private bool CanJump => _isGrounded && _jumpCooldown <= 0f;

    private void Awake()
    {
        Debug.Assert(_unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);
    }

    private void Update()
    {
        if (CanJump)
        {
            Jump();
        }
    }

    private void Jump()
    {
        // WORKAROUND: Rigidbody interpolation reads a stale transform on the frame a unit is re-parented,
        // so force the sync before applying the impulse. Remove once UUM-XXXXX ships.
        Physics.SyncTransforms();
    }
}

[Test]
public void ValidateClone_TargetOccupied_ReturnsTargetBlocked()
{
    // GIVEN
    var grid = <BuildGridWithOccupiedNeighbor>();

    // WHEN
    MoveResult result = <MovementValidator>.ValidateClone(grid, <CloneCommand>);

    // THEN
    Assert.That(result, Is.EqualTo(MoveResult.TargetBlocked));
}
```

## 5. Quick Reference & Decision Matrix

| Code Element                                 | Documentation                      | Rationale                                             |
| :------------------------------------------- | :--------------------------------- | :---------------------------------------------------- |
| Interface / abstract member                  | `/// <summary>` + params/returns   | The contract is the only thing an implementer sees    |
| Public member crossing an `.asmdef` boundary | `/// <summary>`                    | Consumers cannot read the body from another assembly  |
| Generic utility or extension method          | `/// <summary>`                    | Reused far from its definition                        |
| Implementation of a documented interface     | `<inheritdoc />`                   | One source of truth for the contract                  |
| Invariant, ownership, allocation guarantee   | `/// <remarks>`                    | Cannot be expressed in the signature                  |
| Internal member carrying such an invariant   | `/// <remarks>` only               | Accessibility does not make an invariant visible      |
| Private helper, override, lifecycle callback | **None**                           | Short, self-named methods carry it                    |
| Serialized field a designer tunes            | `[Tooltip]` (+ `[Range]`)          | Units, safe bounds, and consequence, in the Inspector |
| Serialized wiring reference                  | **None**                           | Name and type already say it                          |
| Engine bug or performance trade-off          | `// WORKAROUND:` / `// PERF:`      | Invisible in the code, and must be revisitable        |
| Deferred work                                | `// TODO (GOO<ID>): <intent>`      | Traceable to the tracker, or it never gets done       |
| Test method                                  | `// GIVEN` / `// WHEN` / `// THEN` | Structure is the documentation                        |
