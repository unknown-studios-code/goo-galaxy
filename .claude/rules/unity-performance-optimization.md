---
description: "Use when writing performance-sensitive Unity C# code. Covers update loop rules, allocation avoidance, caching, collections, physics, rendering, and the LINQ ban."
paths:
  - "Assets/**/*.cs"
---

# Unity Performance Optimization

## 1. Overview

This document defines performance optimization rules and constraints for a mobile IL2CPP target. Its primary objective is to eliminate garbage collection allocations in hot paths, optimize CPU overhead in update loops, and establish efficient rendering and physics practices.

**Hot path** means `Update`, `FixedUpdate`, `LateUpdate`, network tick handlers, input callbacks, UI rebuild callbacks, animation events, and anything invoked per tile of the board. One-time setup in `Awake`, `Start`, or `OnEnable` may allocate.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Standard object pooling syntax and collection initialization rules)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Programmatic profiling and runtime diagnostic metrics)
- **UI Toolkit** → [unity-ui-toolkit.md](unity-ui-toolkit.md) (UI rendering efficiency, pooling, and binding optimizations)
- **Netcode** → [unity-netcode.md](unity-netcode.md) (Allocation-free serialization on the network tick)

## 3. Core Rules

- **Rule 1 (Zero Allocations in Update Loops):** Never allocate memory in a hot path. Prohibit `new` for reference types, string concatenation and interpolation, `ToString()`, LINQ, closures that capture state, `params` arrays, and scene scans (`FindObjectsByType`, `FindFirstObjectByType`, `GameObject.Find`).
- **Rule 2 (Component Caching):** Resolve component references once — `[SerializeField]` assignment, constructor/`[Inject]` injection, or a single `GetComponent` in `Awake` — and reuse the cached field. Cache `Camera.main`, `transform`, and `gameObject` the same way. Use `TryGetComponent` when the component is genuinely optional; it avoids the failed-lookup allocation of `GetComponent` plus a null check.
- **Rule 3 (Execution Throttling):** Throttle expensive logic with elapsed timers, distance checks, or staggered frame counts, and spread batch work across frames. Never run a whole-board or whole-scene pass every frame.
- **Rule 4 (Collections and Boxing):** Initialize collections with a capacity, reuse them via `.Clear()`, and never allocate one inside a loop. Use generic collections only — a value type stored as `object`, in a non-generic collection, or through an interface it implements will box. Supply an `IEqualityComparer<T>` for enum-keyed dictionaries. Use `Span<T>` and `stackalloc` for short-lived temporary buffers, and borrow from `ListPool<T>`, `DictionaryPool<TKey, TValue>`, or `CollectionPool<T, TItem>` for temporary collections inside a method.
- **Rule 5 (Data Structure Selection):** Choose by access pattern, not by habit: `Dictionary<TKey, TValue>` for O(1) keyed lookup, `HashSet<T>` for O(1) membership, `List<T>` for ordered iteration and growth, arrays for fixed-size hot data, `Queue<T>` for FIFO, `Stack<T>` for LIFO and undo history, `NativeArray<T>` when the data feeds Jobs or Burst.
- **Rule 6 (String Handling):** Keep every string operation out of hot paths, including `string.Format` and interpolation. Build dynamic text with `StringBuilder`, cache formatted strings while their inputs are unchanged, and only refresh them when the underlying value actually changes.
- **Rule 7 (Object Pooling):** Use `UnityEngine.Pool.ObjectPool<T>` for anything spawned more than a few times per second. Set `defaultCapacity` and `maxSize` deliberately, pre-warm to the expected peak, and reset state through `actionOnGet`/`actionOnRelease` — position, rotation, active state, subscriptions, and accumulated data. Return objects instead of destroying them, and never pool an object whose state cannot be reliably reset.
- **Rule 8 (Physics):** Run physics queries in `FixedUpdate` or behind a throttle, never per frame in `Update`. Use non-allocating overloads (`Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc`) with pre-allocated buffers, always pass a `maxDistance` and a cached `LayerMask`, and pass `QueryTriggerInteraction.Ignore` when triggers are irrelevant. Prefer primitive colliders over mesh colliders, keep the collision matrix trimmed, and batch large query sets with `RaycastCommand` plus Jobs.
- **Rule 9 (Rendering & Materials):** Never read `Renderer.material` — it instantiates a copy. Read `sharedMaterial`, and drive per-instance values through `MaterialPropertyBlock` with IDs cached from `Shader.PropertyToID` in a `static readonly` field. Enable GPU instancing for repeated meshes, keep transparent layers and overdraw to a minimum, and avoid swapping materials at runtime.
- **Rule 10 (LINQ & Closures):** Banish LINQ and closure-allocating lambdas from hot paths; use explicit loops and cache delegates or use method groups for callbacks. LINQ remains acceptable in editor tooling, tests, and one-time initialization.
- **Rule 11 (Async & Coroutines):** Prefer `Awaitable` over coroutines. When a coroutine is required, cache `WaitForSeconds` (or `WaitForSecondsRealtime` for unscaled time) in a `readonly` field instead of allocating one per iteration, and verify the object still exists after every `yield` or `await`.
- **Rule 12 (Unity API Shortcuts):** Combine transform writes with `SetPositionAndRotation` instead of assigning position and rotation separately. Compare tags with `CompareTag`, never with `==` against a string.
- **Rule 13 (Profiler Markers):** Wrap critical systems in `ProfilerMarker` blocks so the cost is attributable in the Unity Profiler, and state which marker or Memory Profiler view proves a fix worked.

## 4. Review Checklist

Scan in this order when auditing a change; the first three catch most real regressions.

1. Allocation inside `Update`/`FixedUpdate`/`LateUpdate`/network tick — `new`, LINQ, string building, lambdas, boxing.
2. Uncached lookups — `GetComponent`, `Camera.main`, `Find*`, `Resources.Load` on a repeating path.
3. `Instantiate`/`Destroy` churn where a pool belongs.
4. Physics queries per frame, allocating query overloads, missing layer masks or `maxDistance`.
5. `Renderer.material` access and string-based shader property lookups.
6. Collections rebuilt instead of cleared, or the wrong container for the access pattern.
7. Coroutines allocating yield instructions in a loop; async paths without cancellation.

Never quote a millisecond figure you have not measured — name the mechanism (allocation per frame, GC spike, extra draw call, cache miss) and the frequency instead.

## 5. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadPerf> : MonoBehaviour
{
    private void Update()
    {
        // ❌ Scene scan, allocation, and LINQ in an update loop
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        List<Enemy> activeEnemies = enemies.Where(e => e.isActiveAndEnabled).ToList();

        // ❌ String allocation and an uncached camera lookup
        string info = "Target: " + Camera.main.transform.position;

        // ❌ Allocating physics query with no layer mask or distance limit
        Collider[] hits = Physics.OverlapSphere(transform.position, 10f);

        // ❌ Instantiates a material copy on first access
        GetComponent<Renderer>().material.color = Color.red;
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(Renderer))]
public class <GoodPerf> : MonoBehaviour
{
    private static readonly int _colorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("Radius, in meters, of the periodic proximity sweep.")]
    [SerializeField]
    private float _radius = 10f;

    [Tooltip("Registry that already tracks live enemies. Assigned in the Inspector; never searched for at runtime.")]
    [SerializeField]
    private EnemyRegistry _enemyRegistry;

    private readonly Collider[] _physicsBuffer = new Collider[16];

    private Camera _mainCamera;
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private LayerMask _targetLayer;
    private float _nextSweepTime;

    private void Awake()
    {
        // ✅ Every lookup happens once
        _mainCamera = Camera.main;
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _targetLayer = LayerMask.GetMask("<LayerName>");
    }

    private void Update()
    {
        // ✅ Time-based throttling with an early return
        if (Time.time < _nextSweepTime)
        {
            return;
        }

        _nextSweepTime = Time.time + 0.1f;

        // ✅ Iterate a registry that is maintained by spawn/despawn events, not by scanning the scene
        IReadOnlyList<Enemy> enemies = _enemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].isActiveAndEnabled)
            {
                <MethodName>(enemies[i]);
            }
        }

        // ✅ Non-allocating query, bounded by buffer, layer, and radius
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _physicsBuffer, _targetLayer, QueryTriggerInteraction.Ignore);
    }

    private void SetColor(Color value)
    {
        // ✅ MaterialPropertyBlock instead of instantiating a material
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_colorId, value);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    private void <MethodName>(Enemy target) { }
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
            // ✅ Reuses a single cached yield instruction
            yield return _waitInstruction;
            <Step>();
        }
    }

    private void <Step>() { }
}
```

## 6. Quick Reference & Decision Matrix

| Operation Category | Avoid Pattern                               | Optimized Replacement                                        |
| :----------------- | :------------------------------------------ | :----------------------------------------------------------- |
| Component Fetching | `GetComponent<T>()` in Update               | Cache in `Awake()`, `[SerializeField]`, or `TryGetComponent` |
| Object Discovery   | `FindObjectsByType`, `GameObject.Find`      | Registry maintained on spawn/despawn, or injected reference  |
| Active Camera      | `Camera.main` inside Update loops           | Cache `Camera.main` in `Awake()`                             |
| Collections        | `new List<T>()` in update loops             | Pre-allocate with capacity and reuse via `.Clear()`          |
| Temporary buffers  | `new List<T>()` inside a method             | `ListPool<T>` / `DictionaryPool<K,V>` / `stackalloc`         |
| String Formatting  | `"a" + b` or `$""` in hot paths             | `StringBuilder`, cached formatted string                     |
| Physics Queries    | `Physics.OverlapSphere` in `Update`         | `OverlapSphereNonAlloc` in `FixedUpdate`, buffered + masked  |
| Material Property  | `Renderer.material.color = c`               | `MaterialPropertyBlock` with static shader IDs               |
| Loop Filtering     | LINQ query (`Where`, `Select`, `ToList`)    | Explicit `for` loop into a pre-allocated list                |
| Coroutine Wait     | `new WaitForSeconds(t)` inside loops        | Cached `WaitForSeconds` / `WaitForSecondsRealtime`           |
| Object Instance    | `Instantiate` and `Destroy` inside gameplay | `UnityEngine.Pool.ObjectPool<T>` with reset callbacks        |
| Tag Comparison     | `gameObject.tag == "Player"`                | `gameObject.CompareTag(Tags.Player)`                         |
