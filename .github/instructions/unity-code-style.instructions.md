---
description: "Use when writing or reviewing Unity C# code. Covers formatting, naming, braces, fields, properties, methods, events, async, pooling, and file conventions."
applyTo: "Assets/Scripts/**/*.cs"
---

# Unity C# Code Style

## 1. Overview

This document defines the C# style, formatting, naming, and architectural conventions for the codebase. These rules ensure maintainability, readability, and performance across all game systems.

## 2. Cross-References

- **Class Organization** → [unity-class-organization.instructions.md](unity-class-organization.instructions.md) (Detailed member layout rules for types)
- **Code Documentation** → [unity-code-documentation.instructions.md](unity-code-documentation.instructions.md) (XML, tooltip, and comment rules)
- **Debugging** → [unity-debugging.instructions.md](unity-debugging.instructions.md) (Standard practices for assertions and diagnostics)
- **Performance Optimization** → [unity-performance-optimization.instructions.md](unity-performance-optimization.instructions.md) (Banned patterns in update loops and hot paths)
- **UI Toolkit** → [unity-ui-toolkit.instructions.md](unity-ui-toolkit.instructions.md) (Style and naming conventions specific to UI systems)

## 3. Core Rules

### General Formatting & Structure

- **Braces:** Apply Allman style (opening braces on a new line).
- **Line Limits:** Keep lines under 160 characters. Break long lines logically.
- **Spacing:** Put a single space before flow-control conditions. Do not put spaces inside brackets or immediately inside parentheses. Put a single space after commas between arguments.
- **Regions:** Avoid `#region` blocks. Only use them to group Animation or Input Event Handlers.
- **Imports:** Group using statements in order: `System` namespaces, `UnityEngine` namespaces, then Project-specific namespaces. Remove all unused usings.
- **Namespaces:** Format namespace names in PascalCase without underscores.

### Fields, Properties, and Enums

- **Fields:** Always specify the `private` access modifier explicitly. Prefix private instance and static fields with `_camelCase`. Format constants in PascalCase. Use descriptive names; include units if applicable (e.g. `_speedInMetersPerSecond`). Boolean fields must use `is`, `has`, or `can` prefixes. Expose private fields in the inspector via `[SerializeField]` rather than making fields public.
- **Properties:** Format properties in PascalCase without prefixes. Place properties immediately after fields. Boolean properties must use `Is`, `Has`, or `Can` prefixes. Do not serialize properties. Ensure properties represent lightweight state access with no side effects or heavy computations.
- **Enums:** Format enum names and values in PascalCase. Use singular nouns for enum names. Do not use prefixes or suffixes (like `Enum` or `E_`). Use enums instead of strings or integers for state tracking.

### Events & Methods

- **Events:** Use `event Action` or `event Action<T>` for C# events. Use `UnityEvent` only when Inspector exposure is required. Name events with past-tense verbs (e.g., `DoorOpened`). Raise events using an `On` prefix method and null-conditional invocation. Subscribe to events during `OnEnable` and unsubscribe during `OnDisable` using named methods (avoid lambdas to prevent leaks).
- **Methods:** Use active verbs for method names (e.g., `Jump`). Use `Set` for assignments, `Change` for modifying state, `Process` for system-driven logic, and `Handle` for event callbacks. Name boolean methods with `Is`, `Has`, or `Can` prefixes (e.g., `IsPlayerAlive`). Avoid noun-only or gerund names.

### MonoBehaviour Lifecycle & Composition

- **Awake:** Perform self-initialization and cache local component references. Do not run heavy setup, scene-dependent calls, or event subscriptions here.
- **OnEnable / OnDisable:** Subscribe to events in `OnEnable` and unsubscribe in `OnDisable`.
- **Start:** Wire up UI and initialize references requiring other scene components.
- **FixedUpdate:** Perform physics calculations only. Keep logic allocation-free. Read input in `Update`, apply it in `FixedUpdate`.
- **Update:** Handle input, timers, non-physics state updates, and logic branches. Use early returns to minimize nesting. Keep logic allocation-free.
- **Composition:** Apply `[RequireComponent(typeof(<Type>))]` to guarantee dependency presence and eliminate manual null checks.

### Memory, Performance, and Async

- **Collections:** Use `List<T>` for dynamic sizing and `Dictionary<TKey, TValue>` for lookups. Use arrays for performance-critical fixed-size paths. Do not allocate collections inside loops; instead, pre-allocate and call `.Clear()`. Initialize collections with an explicit capacity when possible.
- **Async:** Prefer `Awaitable` for asynchronous delays and operations. Append the `Async` suffix to async methods, and the `Co` suffix to coroutines. Pass `destroyCancellationToken` to all asynchronous operations. Guard references immediately after an `await` point (e.g. `if (this == null) return;`).
- **Object Pooling:** Use Unity's built-in `UnityEngine.Pool.ObjectPool<T>`. Pre-warm pools in `Awake()`, reset object states prior to reuse, and return objects to the pool instead of destroying them.
- **String Allocation:** Use string interpolation (`$""`) over concatenation (`+`) to minimize allocations.
- **Control Flow:** Prefer early returns to reduce nesting level. Use try-catch blocks strictly for external operations (I/O, networking) rather than internal game logic control.

### File & Configuration Integration

- **Files/Folders:** Name files and folders in PascalCase. Do not use special characters or spaces.
- **Empty Methods:** Do not stub methods with `NotImplementedException`. Instead, use an empty body or insert a `TODO` comment.
- **ScriptableObjects:** Use ScriptableObjects for static configuration data only, not runtime state. Use the `[CreateAssetMenu]` attribute and name files with a `DataSO` suffix. Expose data via public read-only properties rather than public fields.
- **Tags & Layers:** Define text-based tags, layers, and input names as `const` strings in centralized static classes.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadStyle> : MonoBehaviour
{
    public int health; // ❌ Public field (should be private serialized with property)
    private float speed; // ❌ Missing explicit private modifier and underscore prefix

    private void Update()
    {
        // ❌ Deeply nested condition, allocations, and string concatenation
        if (_isActive)
        {
            if (Time.time > _nextActionTime)
            {
                var targetList = new List<Transform>(); // ❌ Allocation in Update loop
                string message = "Time: " + Time.time; // ❌ String concatenation
                throw new NotImplementedException(); // ❌ Throwing exception for stub
            }
        }
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(Rigidbody))]
public class <GoodStyle> : MonoBehaviour
{
    [SerializeField] private int _health;
    [SerializeField] private float _speedInMetersPerSecond;

    private Rigidbody _rigidbody;
    private readonly List<Transform> _targetCache = new(16);

    public int Health => _health;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // ✅ Early return to prevent nesting, zero allocations
        if (!_isActive) return;
        if (Time.time <= _nextActionTime) return;

        ProcessAction();
    }

    private void ProcessAction()
    {
        _targetCache.Clear();
        string statusMessage = $"Time: {Time.time:F2}";
        // ✅ Implementation goes here
    }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadAsyncAndEvent> : MonoBehaviour
{
    public event Action OnDeath; // ❌ Missing past-tense naming and event suffix is wrong

    private void OnEnable()
    {
        // ❌ Lambda subscription prevents unsubscribing, leading to leaks
        StaticEvents.ScoreChanged += (score) => UpdateScore(score);
    }

    private async void Start()
    {
        await Task.Delay(1000); // ❌ Using Task.Delay instead of Awaitable
        _field = 10; // ❌ Missing lifecycle guard after await
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodAsyncAndEvent> : MonoBehaviour
{
    public event Action <EventName>Died;

    private void OnEnable()
    {
        StaticEvents.ScoreChanged += HandleScoreChanged; // ✅ Named handler subscription
    }

    private void OnDisable()
    {
        StaticEvents.ScoreChanged -= HandleScoreChanged; // ✅ Explicit unsubscribe
    }

    private async Awaitable <MethodName>Async()
    {
        // ✅ Awaitable with destroy token and lifecycle guard
        await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
        if (this == null || !isActiveAndEnabled) return;

        <EventName>Died?.Invoke();
    }

    private void HandleScoreChanged(int score)
    {
        // ✅ Implementation goes here
    }
}
```

## 5. Quick Reference & Decision Matrix

| Code Element                                  | Naming Style                          | Location / Scope                 |
| :-------------------------------------------- | :------------------------------------ | :------------------------------- |
| Public Types (Class, Struct, Interface, Enum) | `PascalCase`                          | Namespace block scope            |
| Interfaces                                    | `IPascalCase` (e.g. `IDamageable`)    | Separate file or namespace       |
| Constants / Static Read-Only                  | `PascalCase` (e.g. `MaxCount`)        | Top of type definition           |
| Private Fields (Instance/Static)              | `_camelCase` (e.g. `_speed`)          | Precedes properties and methods  |
| Public Properties                             | `PascalCase` (e.g. `Health`)          | Follows fields, precedes methods |
| Methods                                       | `PascalCase` (e.g. `TakeDamage`)      | Grouped by visibility / overload |
| Local Variables                               | `camelCase` (e.g. `distance`)         | Method execution block scope     |
| ScriptableObject Files                        | `*DataSO.cs` (e.g. `WeaponDataSO.cs`) | Dedicated Assets/Data folder     |
