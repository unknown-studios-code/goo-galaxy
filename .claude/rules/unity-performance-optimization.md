---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Unity Performance Optimization

> **Cross-references:**
>
> - Object pooling → [unity-code-style.md](unity-code-style.md#object-pooling)
> - Collections → [unity-code-style.md](unity-code-style.md#collections)
> - Async/Awaitable → [unity-debugging.md](unity-debugging.md#async--coroutine-debugging)
> - Design patterns → [unity-design-patterns.md](unity-design-patterns.md)
> - UI Toolkit performance → [unity-ui-toolkit.md](unity-ui-toolkit.md#performance-tips)

## Unity 6 Notes

- **`Awaitable`** is more performant than coroutines for simple delays.
- **`UnityEngine.Pool.ObjectPool<T>`** preferred over custom pooling.
- **Burst compiler** + Jobs for math-heavy code.
- **IL2CPP** has different perf characteristics than Mono — profile on target.

---

## Review Priority

Check in this order:

1. **Update loops** — allocations, expensive ops, unnecessary work
2. **Physics** — `OverlapSphere`, Raycast frequency, collision matrix
3. **Memory** — string concat, LINQ in hot paths, boxing
4. **GetComponent/Find** — uncached lookups, per-frame calls
5. **Rendering** — material instances, shader keywords, draw calls

---

## Update Loop Rules

### Never Allocate in Update/FixedUpdate/LateUpdate

- ❌ `new` for reference types
- ❌ String concatenation or interpolation
- ❌ LINQ queries
- ❌ `FindObjectOfType<T>()`, `GameObject.Find()`
- ✅ Pre-allocate and `.Clear()` collections
- ✅ Object pooling for frequent spawn/despawn

```csharp
// ❌ Allocates every frame
void Update()
{
    var enemies = FindObjectsOfType<Enemy>();
    var nearby = new List<Enemy>();
    string status = $"Enemies: {enemies.Length}";
    foreach (var e in enemies.Where(e => e.IsAlive)) nearby.Add(e);
}

// ✅ Zero allocations
private readonly List<Enemy> _cache = new(100);
private readonly List<Enemy> _nearby = new(50);
private readonly StringBuilder _sb = new(64);

void Update()
{
    int count = FindObjectsByType<Enemy>(FindObjectsSortMode.None, _cache);
    _nearby.Clear();
    for (int i = 0; i < count; i++)
        if (_cache[i].IsAlive) _nearby.Add(_cache[i]);
    _sb.Clear().Append("Enemies: ").Append(count);
}
```

### Cache References

- Cache `Transform`, `Rigidbody`, `Camera.main` in `Awake()`.
- Use dirty flags to recalculate only when state changes.
- Use `[SerializeField]` for Inspector-assigned references (zero lookup cost).

```csharp
// ❌ Property access overhead every frame
void Update() { Vector3 pos = transform.position; }

// ✅ Cached
private Transform _transform;
private void Awake() => _transform = transform;
```

### Throttle Expensive Work

```csharp
// Time-based throttling
private float _nextUpdate;
void Update()
{
    if (Time.time < _nextUpdate) return;
    _nextUpdate = Time.time + 0.1f;
    ExpensiveOp();
}

// Staggered processing
private int _index;
private const int ItemsPerFrame = 10;
void Update()
{
    int end = Mathf.Min(_index + ItemsPerFrame, _items.Count);
    for (int i = _index; i < end; i++) ProcessItem(_items[i]);
    _index = end >= _items.Count ? 0 : end;
}
```

---

## Memory Management

### Strings

- ❌ `+` concatenation or `string.Format()` in hot paths.
- ✅ `StringBuilder` for dynamic strings.
- ✅ Cache formatted strings when values rarely change.

```csharp
// ❌
_scoreText.text = "Score: " + _score;

// ✅
private readonly StringBuilder _sb = new(32);
private int _lastScore = -1;
private string _cachedText;
void UpdateUI()
{
    if (_score != _lastScore)
    {
        _sb.Clear().Append("Score: ").Append(_score);
        _cachedText = _sb.ToString();
        _lastScore = _score;
    }
    _scoreText.text = _cachedText;
}
```

### Collections

- ✅ Initialize with capacity: `new List<Enemy>(100)`.
- ✅ `.Clear()` over creating new instances.
- ✅ `ListPool<T>`, `CollectionPool<T>` from `UnityEngine.Pool`.
- ✅ `Span<T>` and `stackalloc` for small temp arrays.
- ❌ `ToArray()`, `ToList()` in perf-critical code.

### Boxing

- ❌ Value types in non-generic collections (`ArrayList`).
- ❌ Passing value types as `object`.
- ✅ Generic collections: `List<int>`, `Dictionary<int, string>`.

```csharp
// ❌ Boxing
ArrayList old = new(); old.Add(42);
// ✅ No boxing
List<int> list = new(); list.Add(42);
```

---

## Object Pooling

- ✅ `UnityEngine.Pool.ObjectPool<T>` (Unity 6 built-in).
- ✅ `defaultCapacity` + `maxSize` based on expected usage.
- ✅ Reset state in `actionOnRelease`.
- ❌ `Instantiate`/`Destroy` for objects spawned more than a few times/sec.

For full code example → [unity-code-style.md](unity-code-style.md#object-pooling)

---

## Physics Optimization

- ✅ Non-allocating methods: `RaycastNonAlloc`, `OverlapSphereNonAlloc`.
- ✅ Cache `LayerMask` — don't call `LayerMask.GetMask()` every frame.
- ✅ Prefer simple colliders (sphere, capsule, box) over mesh colliders.
- ✅ Physics in `FixedUpdate()`, not `Update()`.
- ✅ Configure collision matrix to disable unnecessary layer interactions.
- ✅ `Raycast` with `maxDistance` + `QueryTriggerInteraction.Ignore`.

```csharp
// ❌
Collider[] hits = Physics.OverlapSphere(pos, radius);

// ✅
private readonly Collider[] _buffer = new Collider[32];
private LayerMask _enemyLayer;

private void Awake() => _enemyLayer = LayerMask.GetMask("Enemy");

private void FixedUpdate()
{
    int count = Physics.OverlapSphereNonAlloc(transform.position, _radius, _buffer, _enemyLayer);
    for (int i = 0; i < count; i++) ProcessHit(_buffer[i]);
}
```

---

## Rendering

- ❌ `.material` creates instance → use `.sharedMaterial` when possible.
- ✅ `MaterialPropertyBlock` for per-instance property changes (no allocation after first call).
- ✅ Cache shader property IDs: `Shader.PropertyToID("_Color")`.
- ✅ GPU instancing for many similar objects.

```csharp
// ❌ Creates material instance:
GetComponent<Renderer>().material.color = Color.red;

// ✅ MaterialPropertyBlock:
private static readonly int ColorId = Shader.PropertyToID("_Color");
private MaterialPropertyBlock _block;
private Renderer _renderer;

private void Awake()
{
    _renderer = GetComponent<Renderer>();
    _block = new MaterialPropertyBlock();
}

private void SetColor(Color c)
{
    _renderer.GetPropertyBlock(_block);
    _block.SetColor(ColorId, c);
    _renderer.SetPropertyBlock(_block);
}
```

---

## GetComponent & Find

- ❌ **Never** `GetComponent<T>()` in Update — cache in `Awake()`.
- ❌ `FindObjectOfType<T>()`, `GameObject.Find()` at runtime.
- ✅ `[SerializeField]` for Inspector assignment (zero cost).
- ✅ `TryGetComponent<T>(out T)` for null-safe lookups.
- ✅ `[RequireComponent]` guarantees presence.

```csharp
[RequireComponent(typeof(Rigidbody))]
public class PhysicsController : MonoBehaviour
{
    private Rigidbody _rb;
    private void Awake() => _rb = GetComponent<Rigidbody>(); // Safe — RequireComponent guarantees
}
```

---

## LINQ & Delegates

- ❌ **Never LINQ in Update loops** — most methods allocate.
- ❌ Lambda expressions in hot paths (allocate closures).
- ✅ Explicit `for`/`foreach` loops.
- ✅ Method groups over lambdas for event subscriptions.

```csharp
// ❌ LINQ in Update
var active = _enemies.Where(e => e.IsActive).ToList();

// ✅ Explicit loop, no allocation
_active.Clear();
for (int i = 0; i < _enemies.Count; i++)
    if (_enemies[i].IsActive) _active.Add(_enemies[i]);

// ❌ Allocates closure
_button.clicked += () => OnClicked();

// ✅ Method group
_button.clicked += OnClicked;
```

---

## Async & Coroutine Patterns

- ✅ Prefer `Awaitable` (Unity 6+) for delays.
- ✅ Cache `WaitForSeconds` when using coroutines.
- ❌ Don't `new WaitForSeconds()` in coroutine loops.
- ✅ Check for destruction after await: `if (this == null) return;`.
- ✅ Use `destroyCancellationToken` with Awaitable.

```csharp
// ❌ Allocates every iteration
IEnumerator Bad() { while (true) { yield return new WaitForSeconds(0.1f); DoWork(); } }

// ✅ Cached
private readonly WaitForSeconds _wait = new(0.1f);
IEnumerator Good() { while (true) { yield return _wait; DoWork(); } }

// ✅ Better — Unity 6 Awaitable
private async Awaitable PeriodicAsync(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        await Awaitable.WaitForSecondsAsync(0.1f, token);
        if (this == null) return;
        DoWork();
    }
}
```

---

## Data Structure Selection

| Structure         | Use Case                                  |
| ----------------- | ----------------------------------------- |
| `Dictionary<K,V>` | O(1) key lookups                          |
| `HashSet<T>`      | O(1) contains checks                      |
| `List<T>`         | Ordered, frequent iteration, dynamic size |
| Array             | Fixed size, frequent access               |
| `Queue<T>`        | FIFO (command queues)                     |
| `Stack<T>`        | LIFO (undo, state history)                |

---

## Unity API Best Practices

### Transform

```csharp
// ❌ Multiple operations
transform.position = pos; transform.rotation = rot;
// ✅ Combined
transform.SetPositionAndRotation(pos, rot);
```

### Tag Comparison

```csharp
// ❌ Allocates
if (other.gameObject.tag == "Player")
// ✅ No allocation
if (other.CompareTag("Player"))
```

### Camera.main

```csharp
// ❌ FindGameObjectWithTag every frame
void Update() => Camera.main.WorldToScreenPoint(transform.position);
// ✅ Cached
private Camera _cam;
private void Awake() => _cam = Camera.main;
```

---

## Profiler Markers

```csharp
using Unity.Profiling;

private static readonly ProfilerMarker _marker = new("MySystem.Update");
void Update() { using (_marker.Auto()) { /* code */ } }
```

---

## Anti-Pattern Checklist

| Anti-Pattern                             | Impact   | Fix                      |
| ---------------------------------------- | -------- | ------------------------ |
| `GetComponent` in Update                 | **High** | Cache in Awake           |
| `FindObjectOfType` at runtime            | **High** | Use references or events |
| `new List<T>()` in Update                | **High** | Pre-allocate, Clear()    |
| String concat in loops                   | Medium   | StringBuilder            |
| `Camera.main` in Update                  | Medium   | Cache reference          |
| LINQ in Update                           | Medium   | Explicit loops           |
| `.material` instead of `.sharedMaterial` | Medium   | MaterialPropertyBlock    |
| Lambda in event subscription             | Low      | Method group             |
| `new WaitForSeconds` in coroutine loop   | Low      | Cache wait object        |

### Code Smells in Update

```csharp
// 🔴 Red flags:
GetComponent<T>()       // uncached lookup
FindObjectOfType<T>()   // scene scan
new List<T>()           // allocation
string + string         // allocation
$"interpolated"         // allocation
.Where().Select().ToList() // LINQ
Camera.main             // uncached
Physics.OverlapSphere() // allocating version

// 🟢 Preferred:
_cachedComponent       // cached
_list.Clear()          // reused
_sb.Clear().Append()   // reused builder
for (int i = 0; ...)    // explicit loop
_cachedCamera          // cached
Physics.OverlapSphereNonAlloc() // non-allocating
```

---

## Quick Reference

**Always Do:**

- Cache component references in Awake()
- Pre-allocate collections with capacity
- Use object pooling for frequent spawn/despawn
- Non-allocating physics methods
- Cache shader property IDs
- Use ProfilerMarker
- Use Awaitable over coroutines (Unity 6+)

**Never Do:**

- GetComponent/Find in Update loops
- Allocate in Update loops
- LINQ in Update loops
- String concat in hot paths
- Camera.main every frame
- `new WaitForSeconds` in coroutine loops
- `.material` when `.sharedMaterial` suffices
