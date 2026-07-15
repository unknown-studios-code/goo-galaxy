---
description: "Use when debugging or troubleshooting Unity C# code. Covers diagnostic priority, null references, lifecycle timing, Input System, physics, animation, and async pitfalls."
applyTo: "Assets/Scripts/**/*.cs"
---

# Unity Debugging Guide

## 1. Overview

This document provides a systematic approach to diagnostics, debugging, and troubleshooting in Unity. By following standard execution orders and assertion-driven patterns, developers can quickly isolate bugs in serialization, timing, input, and performance.

## 2. Cross-References

- **Code Style** → [unity-code-style.instructions.md](unity-code-style.instructions.md) (Standard lifecycle methods, event subscription syntax, and Awaitable rules)
- **Performance Optimization** → [unity-performance-optimization.instructions.md](unity-performance-optimization.instructions.md) (CPU/GPU profiling and garbage reduction)
- **UI Toolkit** → [unity-ui-toolkit.instructions.md](unity-ui-toolkit.instructions.md) (Debugging elements, picking modes, and USS bindings)

## 3. Core Rules

- **Rule 1 (Diagnostic Priority):** Analyze issues in the following sequence: Console errors/warnings -> Null Reference exceptions -> Serialization state -> Lifecycle timing (script execution order) -> Scene/Prefab override state -> Physics/Rendering settings.
- **Rule 2 (Null Reference & Component Verification):** Assert that all required serialized fields are assigned in `Awake()`. Use `[RequireComponent]` to guarantee sibling components. Use null-conditional (`?.`) accessors for optional fields.
- **Rule 3 (Lifecycle & Script Execution Order):** Initialize internal state in `Awake()`. Subscribe to events in `OnEnable()`. Initialize cross-script references in `Start()`. Run physics in `FixedUpdate()`, game logic in `Update()`, camera movement in `LateUpdate()`, and unsubscribe in `OnDisable()`. Use `[DefaultExecutionOrder]` to resolve explicit timing issues.
- **Rule 4 (Unity Null Checks vs. C# Pattern Matching):** Never use C# pattern matching (`is not null` or `is null`) to check for destroyed Unity `Object` derived instances. Unity's custom null check overrides the `==` operator to detect destroyed objects; pattern matching bypasses this.
- **Rule 5 (Input System Diagnostics):** Subscribe to `InputSystem.onDeviceChange` to detect hardware modifications. Verify active action maps and bindings. Use the Input Debugger window for live device state validation.
- **Rule 6 (Physics Diagnostics):** Draw debug rays (`Debug.DrawRay`) and log layers programmatically. Verify Layer Collision Matrix settings. Ensure rigidbodies are configured correctly (e.g., Continuous Collision Detection, IsKinematic settings). Implement correct signatures for trigger and collision methods.
- **Rule 7 (Animation & Root Motion Debugging):** Cache parameter hashes (`Animator.StringToHash`). Log current states and parameters. Ensure animation event handlers are public with valid parameter signatures.
- **Rule 8 (UI Toolkit and Audio Debugging):** Query elements programmatically to inspect hierarchy. Check element `pickingMode` if clicks are missed. Check spatial audio blend factors and ensure mixer values utilize decibels.
- **Rule 9 (Asynchronous Flow & Memory Leaks):** Use `destroyCancellationToken` with async operations and verify lifecycle status immediately after every `await` point. Pair event subscriptions in `OnEnable` with unsubscriptions in `OnDisable`. Prevent duplicate coroutines by tracking active instances and stopping them before starting new ones.
- **Rule 10 (ScriptableObject Safety):** Do not modify ScriptableObject values directly at runtime. Create runtime copies of template configurations using `Instantiate()`.
- **Rule 11 (Platform Diagnostics):** Wrap editor-only code blocks in `#if UNITY_EDITOR` blocks. Use `System.Diagnostics.Debugger.Break()` to programmatically trigger debug pauses in connected IDEs.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadDebug> : MonoBehaviour
{
    private <Type> _reference;

    private void Awake()
    {
        // ❌ Accessing external components in Awake (leads to race conditions)
        _reference = FindObjectOfType<<Type>>();
        _reference.Initialize();
    }

    private void OnCollisionEnter(Collider <Other>)
    {
        // ❌ Collision handler uses incorrect parameter type (Collider vs Collision)
    }

    private void Update()
    {
        // ❌ Pattern matching bypasses Unity's custom null check for destroyed objects
        if (_reference is not null)
        {
            _reference.DoSomething();
        }
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(<Component>))]
public class <GoodDebug> : MonoBehaviour
{
    [SerializeField] private <Type> _reference;

    private <Component> _localComponent;

    private void Awake()
    {
        // ✅ Self-initialization and component caching
        _localComponent = GetComponent<<Component>>();

        // ✅ Explicitly assert serialized dependencies
        Debug.Assert(_reference != null, "Reference dependency missing!", this);
    }

    private void OnEnable()
    {
        // ✅ Clean event subscription
        StaticEvents.OnAction += HandleAction;
    }

    private void OnDisable()
    {
        // ✅ Clean event unsubscription
        StaticEvents.OnAction -= HandleAction;
    }

    private void Update()
    {
        // ✅ Proper Unity null check that handles destroyed objects
        if (_reference != null)
        {
            _reference.DoSomething();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ✅ Trigger handler uses correct signature
    }

    private void HandleAction()
    {
        // ✅ Implementation goes here
    }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadSOAndAsync> : MonoBehaviour
{
    [SerializeField] private PlayerDataSO _playerData; // SO Asset

    private async void Start()
    {
        // ❌ Directly writing to SO asset modifies file in Editor
        _playerData.health = 100;

        await Awaitable.WaitForSecondsAsync(1f);
        // ❌ Missing destroy token check; continues running even if object is destroyed
        transform.position = Vector3.zero;
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodSOAndAsync> : MonoBehaviour
{
    [SerializeField] private PlayerDataSO _playerDataTemplate;

    private PlayerDataSO _runtimeData;

    private void Awake()
    {
        // ✅ Create instance copy to prevent editing the project asset
        _runtimeData = Instantiate(_playerDataTemplate);
    }

    private async Awaitable Start()
    {
        _runtimeData.health = 100;

        try
        {
            // ✅ Awaitable tracks lifetime cancellation
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

            // ✅ Guard check after asynchronous boundary
            if (this == null || !isActiveAndEnabled) return;
            transform.position = Vector3.zero;
        }
        catch (OperationCanceledException)
        {
            // ✅ Handle cancellation gracefully
        }
    }

    private void OnDestroy()
    {
        if (_runtimeData != null)
        {
            Destroy(_runtimeData);
        }
    }
}
```

## 5. Quick Reference & Decision Matrix

| Issue Category              | Common Cause                                           | Diagnostic / Solution                                                              |
| :-------------------------- | :----------------------------------------------------- | :--------------------------------------------------------------------------------- |
| `NullReferenceException`    | Unassigned Inspector field / wrong execution timing    | Assert in `Awake()`, move initialization to `Start()`, or use `[RequireComponent]` |
| `MissingReferenceException` | Accessing a Unity Object that was previously destroyed | Check with `_object != null` (do not use `is not null`)                            |
| Jittery Movement            | Transform modifications executed in incorrect loop     | Run physics / rigidbody motion in `FixedUpdate()`, cameras in `LateUpdate()`       |
| Event Fires Multiple Times  | Missing unsubscription or duplicate subscriptions      | Always unsubscribe in `OnDisable()`                                                |
| SO Edits Persist            | Direct modification of a ScriptableObject Asset        | Create a runtime duplicate using `Instantiate()` in `Awake()`                      |
| Programmatic Pauses         | Need to break execution at specific code points        | Call `System.Diagnostics.Debugger.Break()` when condition is met                   |
