---
description: "Use when debugging or troubleshooting Unity C# code. Covers diagnostic priority, null references, lifecycle timing, transforms, Input System, physics, animation, and async pitfalls."
paths:
  - "Assets/**/*.cs"
---

# Unity Debugging Guide

## 1. Overview

This document provides a systematic approach to diagnostics, debugging, and troubleshooting in Unity. Find the root cause before changing code: a fix that makes the symptom disappear without an explanation is not a fix.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Standard lifecycle methods, event subscription syntax, and Awaitable rules)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (CPU/GPU profiling and garbage reduction)
- **Project Configuration** → [unity-project-configuration.md](unity-project-configuration.md) (Domain reload and static state behavior)
- **UI Toolkit** → [unity-ui-toolkit.md](unity-ui-toolkit.md) (Debugging elements, picking modes, and USS bindings)

## 3. Core Rules

- **Rule 1 (Diagnostic Priority):** Analyze issues in this sequence: console errors and warnings → null reference exceptions → serialization state (Inspector vs runtime values) → lifecycle timing and script execution order → scene/prefab override state → physics and rendering settings.
- **Rule 2 (Evidence Before Hypothesis):** Collect the exact error text and full stack trace, the reproduction steps, whether the failure is deterministic, and what changed just before it appeared. In the stack trace, read the first frame **inside project code** — the deepest engine frame is rarely the defect. Ask for the console output or a profiler capture instead of guessing; you cannot run the editor.
- **Rule 3 (Null Reference & Component Verification):** Assert required serialized fields in `Awake()` with `Debug.Assert(field != null, Messages.X, this)`. Use `[RequireComponent]` to guarantee siblings, and `TryGetComponent` for genuinely optional ones. Treat a missing serialized reference as a wiring bug to fix, not a null check to add.
- **Rule 4 (Unity Null Semantics):** `UnityEngine.Object` overloads `==` so a destroyed object compares equal to `null`. Always compare with `== null` / `!= null`. Never use `is null`, `is not null`, `?.`, `??`, or `??=` on a `UnityEngine.Object` — all four bypass the overload and treat a destroyed object as alive. Null-conditional invocation stays correct for events, delegates, and plain C# objects.
- **Rule 5 (Lifecycle & Script Execution Order):** Initialize internal state in `Awake()`, subscribe in `OnEnable()`, wire cross-object references in `Start()`, run physics in `FixedUpdate()`, game logic in `Update()`, camera and post-animation corrections in `LateUpdate()`, unsubscribe in `OnDisable()`, and release owned resources in `OnDestroy()`. When two scripts genuinely must run in a fixed order, declare it with `[DefaultExecutionOrder]` rather than relying on `Awake` ordering.
- **Rule 6 (Domain Reload & Static State):** Domain and scene reload are disabled in this project. A bug that appears only on the second play session is static state that was never reset — a static field, a static event with accumulated subscribers, or a cached instance. Reset it in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- **Rule 7 (Transform & Hierarchy):** Distinguish local from world space: `localPosition`/`localRotation` are relative to the parent, `position`/`rotation` are absolute. Reparenting with `SetParent(parent)` preserves world position; `SetParent(parent, false)` keeps local values. Non-uniform parent scale skews children and shears rotated colliders — keep parents at uniform scale. When a transform looks correct in the Inspector but wrong on screen, check whether the value is being overwritten later in the frame by animation, physics, or a `LateUpdate` writer.
- **Rule 8 (Input System Diagnostics):** Verify the action map is enabled, the action asset is the one the component references, and the control scheme matches the device. Subscribe to `InputSystem.onDeviceChange` for hot-plug issues and use the Input Debugger window for live device state. Input that "does nothing" is usually a disabled map, a `PlayerInput` behavior mismatch, or UI consuming the event.
- **Rule 9 (Physics Diagnostics):** Verify the Layer Collision Matrix, layer masks, and that colliders and rigidbodies are configured as intended (kinematic flags, continuous collision detection for fast movers). Use `Debug.DrawRay`/`Debug.DrawLine` to visualize queries. Match the callback signature to the collider type: `OnCollisionEnter(Collision)` versus `OnTriggerEnter(Collider)` — a wrong parameter type compiles but never fires.
- **Rule 10 (Animation & Root Motion):** Cache parameter hashes with `Animator.StringToHash`. Log the current state and parameter values when a transition does not fire; the usual causes are an unmet condition, a mismatched parameter name, or a transition with exit time still enabled. Animation event handlers must be public with a signature Unity can invoke, and root motion must be either applied by the Animator or by script, never both.
- **Rule 11 (Events & Subscriptions):** An event firing twice is a duplicated subscription; an event that stops firing is an object destroyed while still subscribed. Pair every `+=` with a `-=` in the mirrored callback. C# `event Action` invocation order is registration order and must not be relied upon; when order matters, sequence the calls explicitly. `UnityEvent` wiring lives in the scene, so a silent handler is usually a missing or stale Inspector reference.
- **Rule 12 (Async & Coroutines):** Pass `destroyCancellationToken` to every `Awaitable`, catch `OperationCanceledException`, and re-check `this == null || !isActiveAndEnabled` after each `await`. A stalled async flow is almost always an awaited operation whose token never fires or an `await` across a scene unload. Coroutines stop when their MonoBehaviour is disabled — track the handle and stop the previous one before starting a new one to avoid duplicates. Do not mix `Awaitable` and coroutines in the same workflow.
- **Rule 13 (ScriptableObject Safety):** Writing to a `ScriptableObject` asset at runtime persists in the Editor and silently resets in a build. Clone the template with `Instantiate()` in `Awake()`, use the copy, and `Destroy` it in `OnDestroy()`.
- **Rule 14 (UI Toolkit & Audio):** For UI, query elements after the panel exists, check `pickingMode`, element size, and `display`, and use the UI Toolkit Debugger to inspect the live hierarchy. For audio, check the spatial blend, the mixer group routing, and remember that mixer volume is in decibels — linear 0.5 is not half volume.
- **Rule 15 (Editor-Only Code & Breakpoints):** Wrap editor-only code in `#if UNITY_EDITOR`, including `OnValidate`. Use `System.Diagnostics.Debugger.Break()` to stop on a specific condition, and remove every temporary log or breakpoint before handing the change over.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadDebug> : MonoBehaviour
{
    private <Type> _reference;

    private void Awake()
    {
        // ❌ Scene scan in Awake, plus a race with other objects' initialization
        _reference = FindFirstObjectByType<<Type>>();
        _reference.Initialize();
    }

    private void OnCollisionEnter(Collider other)
    {
        // ❌ Wrong parameter type: compiles, never fires
    }

    private void Update()
    {
        // ❌ Pattern matching and null-conditional bypass Unity's destroyed-object check
        if (_reference is not null)
        {
            _reference?.DoSomething();
        }
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(<Component>))]
public class <GoodDebug> : MonoBehaviour
{
    [SerializeField]
    private <Type> _reference;

    private <Component> _localComponent;

    private void Awake()
    {
        _localComponent = GetComponent<<Component>>();

        // ✅ Fail loudly at the source instead of null-checking forever downstream
        Debug.Assert(_reference != null, <Messages>.ReferenceMissing, this);
    }

    private void OnEnable()
    {
        MatchEvents.MoveExecuted += HandleMoveExecuted;
    }

    private void OnDisable()
    {
        MatchEvents.MoveExecuted -= HandleMoveExecuted;
    }

    private void Update()
    {
        // ✅ Unity null check that also detects destroyed objects
        if (_reference != null)
        {
            _reference.DoSomething();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ✅ Signature matches the trigger callback
    }

    private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affected)
    {
        // ✅ Implementation goes here
    }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadSOAndAsync> : MonoBehaviour
{
    [SerializeField]
    private <Type>DataSO _playerData;

    private async void Start()
    {
        // ❌ Writes to the project asset; the change persists in the Editor
        _playerData.Health = 100;

        await Awaitable.WaitForSecondsAsync(1f);
        // ❌ No cancellation token and no lifecycle guard after the await
        transform.position = Vector3.zero;
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodSOAndAsync> : MonoBehaviour
{
    [SerializeField]
    private <Type>DataSO _playerDataTemplate;

    private <Type>DataSO _runtimeData;

    private void Awake()
    {
        // ✅ Runtime copy; the authored asset stays untouched
        _runtimeData = Instantiate(_playerDataTemplate);
    }

    private async Awaitable <MethodName>Async()
    {
        try
        {
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

            // ✅ Guard after the await boundary
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            transform.position = Vector3.zero;
        }
        catch (OperationCanceledException)
        {
            // ✅ Expected on destroy; nothing to clean up here
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

| Issue Category                    | Common Cause                                           | Diagnostic / Solution                                                                |
| :-------------------------------- | :----------------------------------------------------- | :----------------------------------------------------------------------------------- |
| `NullReferenceException`          | Unassigned Inspector field / wrong execution timing    | Assert in `Awake()`, move cross-object wiring to `Start()`, use `[RequireComponent]` |
| `MissingReferenceException`       | Access to a destroyed Unity Object                     | Compare with `!= null`; never `is not null`, `?.`, or `??`                           |
| Works once, breaks on second play | Static state surviving disabled domain reload          | Reset statics and static events in `SubsystemRegistration`                           |
| Jittery or fighting movement      | Transform written in the wrong loop, or written twice  | Physics in `FixedUpdate()`, camera in `LateUpdate()`; find the second writer         |
| Child transform skewed            | Non-uniform scale on a rotated parent                  | Keep parents uniformly scaled; check `SetParent` world-position flag                 |
| Event fires twice / stops firing  | Duplicate subscription, or object destroyed subscribed | Pair every `+=` in `OnEnable` with `-=` in `OnDisable`                               |
| Collision callback never runs     | Wrong signature or layer matrix                        | `OnCollisionEnter(Collision)` vs `OnTriggerEnter(Collider)`; check the matrix        |
| Input does nothing                | Action map disabled or UI consuming the event          | Input Debugger, verify map/scheme and `PlayerInput` behavior                         |
| Async never completes             | Token never triggered, or await across scene unload    | Pass `destroyCancellationToken`, catch `OperationCanceledException`                  |
| SO edits persist between sessions | Writing directly to the authored asset                 | `Instantiate()` the template in `Awake()`, `Destroy` the copy in `OnDestroy()`       |
| Need to stop at a specific state  | Condition too rare for a manual breakpoint             | `System.Diagnostics.Debugger.Break()` behind the condition                           |
