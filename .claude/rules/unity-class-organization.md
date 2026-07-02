---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Unity Class Organization

## 1. Overview

This file establishes a strict and predictable member layout ordering for all C# type definitions including Classes, MonoBehaviours, ScriptableObjects, Structs, Records, Interfaces, and Enums. Maintaining a consistent order ensures readability, simplifies code reviews, and prevents configuration issues.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Align fields and property naming conventions with class member layout)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Verify script execution and lifecycle initialization order)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (Apply consistent layouts to components and patterns)

## 3. Core Rules

- **Rule 1 (Pure Class Ordering):** Order members in pure C# classes strictly as follows:
  1. Constants
  2. Fields (Static -> Instance; descending visibility: public -> internal -> protected -> private)
  3. Delegates
  4. Events
  5. Constructors and Finalizers
  6. Properties and Indexers
  7. Operators
  8. Methods (Static -> Public -> Internal -> Protected -> Override -> Partial -> Private)
  9. Nested Types (Enums -> Interfaces -> Structs -> Records -> Classes)

- **Rule 2 (MonoBehaviour Ordering):** Order members in MonoBehaviours strictly as follows:
  1. Constants
  2. Static Fields
  3. Inspector Fields (descending visibility: public -> protected -> private [SerializeField])
  4. Runtime Fields (public [HideInInspector] -> internal -> protected -> private)
  5. Delegates and Events
  6. UnityEvents
  7. Constructors and Finalizers (avoid using constructors; use Unity lifecycle callbacks instead)
  8. Properties and Indexers
  9. Operators
  10. Unity Lifecycle Callbacks (Initialization -> Game Loop -> Teardown)
  11. Unity Physics Callbacks
  12. Unity Editor/Input Callbacks
  13. Methods (Static -> Public -> Internal -> Protected -> Override -> Partial -> Private)
  14. Nested Types

- **Rule 3 (ScriptableObject Ordering):** Order members in ScriptableObjects strictly as follows:
  1. Constants
  2. Static Fields
  3. Inspector Fields (descending visibility: public -> protected -> private)
  4. Runtime Fields
  5. Delegates and Events
  6. UnityEvents
  7. Constructors and Finalizers
  8. Properties and Indexers
  9. Operators
  10. Unity Lifecycle Callbacks (Initialization -> Teardown)
  11. Unity Editor Callbacks
  12. Methods (Static -> Public -> Internal -> Protected -> Override -> Partial -> Private)
  13. Nested Types

- **Rule 4 (Structs, Records, Interfaces, and Enums):** Enforce descending visibility ordering for all members. For interfaces, order elements as: constants, static members, property/indexer declarations, event declarations, and method declarations (static -> instance -> default interface methods -> nested).

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <Type> : MonoBehaviour
{
    private void Start() => <Method>(); // ❌ Unity callback placed before fields
    [SerializeField] private int _value; // ❌ Field declared in middle of class
    private void <Method>() { }
}
```

### ✅ Do (Good)

```csharp
public class <Type> : MonoBehaviour
{
    [SerializeField] private int _value;

    private void Start() => <Method>();
    private void <Method>() { }
}
```

## 5. Quick Reference & Decision Matrix

| Member Category     | Ordering Position | Serialized/Inspector Priority                                |
| ------------------- | ----------------- | ------------------------------------------------------------ |
| Constants / Statics | Always Top        | Precedes all instance members                                |
| Inspector Fields    | Middle-Top        | Ordered by: Public -> Protected -> Private                   |
| Unity Callbacks     | Middle            | Initialization (`Awake`) -> Game Loop (`Update`) -> Teardown |
| Methods             | Bottom            | Ordered by: Static -> Instance (Public to Private)           |
