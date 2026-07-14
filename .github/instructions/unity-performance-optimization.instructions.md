---
description: "Use when writing performance-sensitive Unity C# code. Covers update loop rules, allocation avoidance, caching, physics, rendering, and the LINQ ban."
applyTo: "Assets/Scripts/**/*.cs"
---

# Unity Performance Optimization

## 1. Overview

This document defines performance optimization rules and constraints. Its primary objective is to eliminate garbage collection allocations in hot paths, optimize CPU overhead in update loops, and establish efficient rendering and physics practices.

## 2. Cross-References

- **Code Style** → [unity-code-style.instructions.md](unity-code-style.instructions.md) (Standard object pooling syntax and collection initialization rules)
- **Debugging** → [unity-debugging.instructions.md](unity-debugging.instructions.md) (Programmatic profiling and runtime diagnostic metrics)
- **UI Toolkit** → [unity-ui-toolkit.instructions.md](unity-ui-toolkit.instructions.md) (UI rendering efficiency, pooling, and binding optimizations)

## 3. Core Rules

- **Rule 1 (Zero Allocations in Update Loops):** Never allocate memory in `Update()`, `FixedUpdate()`, or `LateUpdate()`. Prohibit the use of `new` for reference types, string concatenation/interpolation, LINQ operations, and uncached component queries (`FindObjectsByType`, `GetComponent`, `Camera.main`).
- **Rule 2 (Component Caching):** Cache all local component references (e.g. `Transform`, `Rigidbody`, `Camera.main`) in `Awake()`. Do not fetch them repeatedly in update loops or properties.
- **Rule 3 (Execution Throttling):** Throttle expensive logic using elapsed timers, distance checks, or staggered frame counts. Do not execute heavy calculations every frame.
- **Rule 4 (Memory and Boxing Avoidance):** Initialize generic collections with a default capacity. Call `.Clear()` instead of instantiating new collections. Use generic collections strictly to prevent value-type boxing. Use `Span<T>` and `stackalloc` for low-lifetime temporary arrays.
- **Rule 5 (Non-Allocating Physics APIs):** Use non-allocating physics query methods (e.g., `OverlapSphereNonAlloc`, `RaycastNonAlloc`) with pre-allocated buffers. Cache `LayerMask` hashes and use simple primitive colliders over mesh colliders.
- **Rule 6 (Rendering & Material Instances):** Avoid accessing `Renderer.material` directly, as this creates a duplicate material instance. Use `MaterialPropertyBlock` for per-instance property modification. Cache shader property IDs using `Shader.PropertyToID()`.
- **Rule 7 (LINQ & Closure Banishment):** Banish all LINQ methods and closure-allocating lambda functions from hot paths. Use explicit `for` or `foreach` loops for iterations. Prefer method groups over lambdas for event callbacks.
- **Rule 8 (Coroutine Allocation Avoidance):** Cache `WaitForSeconds` yield instructions to prevent garbage collection allocation in coroutine loops. Prefer `Awaitable` for asynchronous delays.
- **Rule 9 (Transform and Tag Optimizations):** Combine transform modifications using `transform.SetPositionAndRotation()`. Perform tag comparisons via `.CompareTag()` rather than direct string equivalence operators.
- **Rule 10 (Profiler Markers):** Instrument critical update steps with `ProfilerMarker` blocks to analyze CPU overhead directly in the Unity Profiler.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadPerf> : MonoBehaviour
{
    private void Update()
    {
        // ❌ Scene scan, allocation, and LINQ in update loop
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var activeEnemies = enemies.Where(e => e.isActiveAndEnabled).ToList();

        // ❌ String allocation and Camera.main scene lookup
        string info = "Target: " + Camera.main.transform.position;

        // ❌ Allocating physics overlap
        Collider[] hits = Physics.OverlapSphere(transform.position, 10f);

        // ❌ Instantiates material copy
        GetComponent<Renderer>().material.color = Color.red;
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(Renderer))]
public class <GoodPerf> : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private float _radius = 10f;

    private Camera _cachedCamera;
    private Renderer _cachedRenderer;
    private MaterialPropertyBlock _propBlock;
    private LayerMask _targetLayer;
    private float _nextUpdateTime;

    private readonly List<Enemy> _enemyCache = new(32);
    private readonly Collider[] _physBuffer = new Collider[16];

    private void Awake()
    {
        // ✅ Pre-cache components and setup structures
        _cachedCamera = Camera.main;
        _cachedRenderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _targetLayer = LayerMask.GetMask("<LayerName>");
    }

    private void Update()
    {
        // ✅ Time-based throttling and early return
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + 0.1f;

        // ✅ Zero allocation list retrieval and iteration
        int count = FindObjectsByType<Enemy>(FindObjectsSortMode.None, _enemyCache);
        for (int i = 0; i < count; i++)
        {
            if (_enemyCache[i].isActiveAndEnabled)
            {
                <MethodName>(_enemyCache[i]);
            }
        }

        // ✅ Non-allocating physics query
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _physBuffer, _targetLayer);
    }

    private void SetColor(Color <Value>)
    {
        // ✅ Modifying materials via MaterialPropertyBlock to prevent instances
        _cachedRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorId, <Value>);
        _cachedRenderer.SetPropertyBlock(_propBlock);
    }

    private void <MethodName>(Enemy <Target>) { }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadCoroutines> : MonoBehaviour
{
    private IEnumerator <MethodName>Co()
    {
        while (true)
        {
            // ❌ Allocates a new yield object every loop iteration
            yield return new WaitForSeconds(0.5f);
            <Step>();
        }
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodCoroutines> : MonoBehaviour
{
    private readonly WaitForSeconds _waitInstruction = new(0.5f);

    private IEnumerator <MethodName>Co()
    {
        while (true)
        {
            // ✅ Reuses single cached yield instruction
            yield return _waitInstruction;
            <Step>();
        }
    }

    private void <Step>() { }
}
```

## 5. Quick Reference & Decision Matrix

| Operation Category | Avoid Pattern                               | Optimized Replacement                                 |
| :----------------- | :------------------------------------------ | :---------------------------------------------------- |
| Component Fetching | `GetComponent<T>()` in Update               | Cache in `Awake()` or use `[RequireComponent]`        |
| Active Camera      | `Camera.main` inside Update loops           | Cache `Camera.main` in `Awake()`                      |
| Collections        | `new List<T>()` in update loops             | Pre-allocate and reuse via `.Clear()`                 |
| String Formatting  | `string + string` or `$""` in hot paths     | Use `StringBuilder` and cache formatted string        |
| Physics Queries    | `Physics.OverlapSphere`                     | Use `Physics.OverlapSphereNonAlloc` with local buffer |
| Material Property  | `Renderer.material.color = c`               | Use `MaterialPropertyBlock` with static shader IDs    |
| Loop Filtering     | LINQ query (`Where`, `Select`, `ToList`)    | Explicit `for` loops filtering to pre-allocated List  |
| Coroutine Wait     | `new WaitForSeconds(t)` inside loops        | Cache `WaitForSeconds` instances                      |
| Object Instance    | `Instantiate` and `Destroy` inside gameplay | Implement `UnityEngine.Pool.ObjectPool<T>`            |
