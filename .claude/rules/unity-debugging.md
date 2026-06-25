---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Unity Debugging Guide

> **Cross-references:**
>
> - Code style & lifecycle → [unity-code-style.md](unity-code-style.md)
> - Performance profiling → [unity-performance-optimization.md](unity-performance-optimization.md)
> - UI Toolkit debugging → [unity-ui-toolkit.md](unity-ui-toolkit.md)

## Diagnostic Priority

When investigating issues, check in this order:

1. **Console errors/warnings** — always start here.
2. **Null reference exceptions** — most common Unity issue.
3. **Serialization state** — Inspector values vs runtime values.
4. **Lifecycle timing** — script execution order problems.
5. **Scene/Prefab state** — missing references, disabled objects.
6. **Physics/Rendering settings** — layer masks, culling, collision matrices.

---

## Console Output

### Error Categories

| Prefix                      | Typical Cause                                                          |
| --------------------------- | ---------------------------------------------------------------------- |
| `NullReferenceException`    | Unassigned SerializeField, destroyed object, wrong execution order     |
| `MissingReferenceException` | Accessing object after `Destroy()`                                     |
| `MissingComponentException` | `GetComponent<T>` returned null — component not attached or wrong type |
| `IndexOutOfRangeException`  | Off-by-one, empty collection                                           |
| `InvalidOperationException` | Modifying collection while iterating                                   |

### Common Warnings

```csharp
// "SendMessage cannot be called during Awake..."
// → Defer to Start() or use Invoke/Coroutine

// "The referenced script on this Behaviour is missing!"
// → Script deleted or class name doesn't match filename

// "You are trying to create a MonoBehaviour using the 'new' keyword"
// → Use AddComponent<T>() or Instantiate()
```

---

## SerializeField & Inspector Debugging

- `[SerializeField] private GameObject _target;` → visible in Inspector.
- `private GameObject _target;` → **not** serialized (no SerializeField, no public).
- `public GameObject target;` → serialized but exposed (avoid — use property instead).
- `[HideInInspector] public GameObject target;` → serialized but hidden.

### Runtime vs Editor Values

```csharp
private void OnValidate() => Debug.Log($"[Editor] _target assigned: {_target != null}");
private void Awake() => Debug.Log($"[Runtime] _target assigned: {_target != null}");
```

### Prefab Override Issues

When SerializeField appears assigned in Prefab but null at runtime:

1. Check for scene instance override (bold in Inspector).
2. Check if value cleared in prefab variant.
3. Check if `OnValidate()` or `Reset()` clears the value.

---

## Script Execution Order

```
Awake() → OnEnable() → Start() → FixedUpdate() → Update() → LateUpdate() → OnDisable() → OnDestroy()
```

- **Awake:** self-initialization, cache own components.
- **OnEnable:** subscribe to events.
- **Start:** references to other objects, initialization depending on others.
- **FixedUpdate:** physics (fixed timestep).
- **Update:** game logic (every frame).
- **LateUpdate:** camera follow, post-processing.
- **OnDisable:** unsubscribe.

### Common Timing Issues

| Symptom                     | Likely Cause                               | Solution                                      |
| --------------------------- | ------------------------------------------ | --------------------------------------------- |
| Reference null in `Awake()` | Other object not yet initialized           | Move to `Start()`                             |
| Reference null in `Start()` | Object created later                       | Use events or null-checked `FindObjectOfType` |
| Camera jitter               | Camera in `Update()`, target in `Update()` | Move camera to `LateUpdate()`                 |
| Physics inconsistency       | Physics in `Update()`                      | Move to `FixedUpdate()`                       |
| State resets unexpectedly   | Domain reload on/off mismatch              | Check Enter Play Mode Options                 |

```csharp
[DefaultExecutionOrder(-100)] public class GameManager : MonoBehaviour { }
[DefaultExecutionOrder(100)]  public class UIManager : MonoBehaviour { }
```

---

## Null Reference Debugging

### Patterns

```csharp
// Validate SerializeFields
private void Awake()
{
    Debug.Assert(_playerTransform != null, "PlayerTransform not assigned!", this);
}

// Null-conditional for optional refs
_optionalComponent?.DoSomething();

// Explicit null check with error
if (_requiredComponent == null)
{
    Debug.LogError($"Required component missing on {gameObject.name}", this);
    enabled = false;
    return;
}
```

### GetComponent Failures

```csharp
// ❌ Only checks THIS object
Rigidbody rb = GetComponent<Rigidbody>();

// ✅ Specify scope
Rigidbody rb = GetComponentInChildren<Rigidbody>();
Rigidbody rb = GetComponentInParent<Rigidbody>();

// ✅ Guarantee presence
[RequireComponent(typeof(Rigidbody))]
public class PhysicsController : MonoBehaviour { }
```

### Destroyed Object Access

```csharp
// Unity's fake null check catches destroyed objects:
if (_enemy != null) { /* safe */ }

// C# pattern does NOT:
if (_enemy is not null) { /* WRONG — misses destroyed Unity objects */ }
```

---

## Input System Debugging

### Device Connection

```csharp
private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
private void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    => Debug.Log($"Device '{device.displayName}' {change}");
```

### PlayerInput Issues

| Symptom                    | Check                            | Solution                                |
| -------------------------- | -------------------------------- | --------------------------------------- |
| No input response          | InputActionAsset assigned?       | Assign in Inspector or via code         |
| Actions not firing         | Action map enabled?              | `actionMap.Enable()`                    |
| Wrong device input         | Control scheme correct?          | Verify bindings for target device       |
| Input works in Editor only | Input System package in build?   | Player Settings → Active Input Handling |
| Duplicate events           | Multiple PlayerInput components? | Use single PlayerInput                  |

- **Input Debugger:** Window → Analysis → Input Debugger (inspect devices, events, action states live).

---

## Physics Debugging

### Layer & Collision

```csharp
Debug.Log($"Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
Debug.DrawRay(origin, direction * maxDistance, Color.red, 2f);
```

Check **Edit → Project Settings → Physics** — verify collision matrix.

### Rigidbody Issues

| Symptom                   | Check                  | Solution                               |
| ------------------------- | ---------------------- | -------------------------------------- |
| No collision              | Rigidbody present?     | Add Rigidbody to at least one object   |
| Collision but no callback | Trigger misconfigured? | Match OnCollision vs OnTrigger methods |
| Objects pass through      | Kinematic + no CCD?    | Enable Continuous collision detection  |
| Jittery movement          | Moving in Update()?    | Move physics in FixedUpdate()          |

### Trigger vs Collision Methods

```csharp
// Colliders (IsTrigger = false)
void OnCollisionEnter(Collision c) { }
void OnCollisionStay(Collision c) { }
void OnCollisionExit(Collision c) { }

// Triggers (IsTrigger = true)
void OnTriggerEnter(Collider other) { }
void OnTriggerStay(Collider other) { }
void OnTriggerExit(Collider other) { }

// 2D variants: OnCollisionEnter2D, OnTriggerEnter2D, etc.
```

---

## Animation & Animator Debugging

### State Debugging

```csharp
// Cache parameter hashes (required for performance)
private static readonly int _speedHash = Animator.StringToHash("Speed");

// Log current state
var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
Debug.Log($"State: {stateInfo.shortNameHash}, Time: {stateInfo.normalizedTime}");

// List all parameters and values
foreach (var param in _animator.parameters)
    Debug.Log($"{param.name} ({param.type}) = {GetParamValue(param)}");
```

### Animation Event Requirements

```csharp
// Valid signatures:
public void OnFootstep() { }
public void OnFootstep(string sound) { }
public void OnFootstep(float volume) { }
public void OnFootstep(int index) { }
public void OnFootstep(AnimationEvent evt) { }
```

### Root Motion Issues

| Symptom              | Solution                                   |
| -------------------- | ------------------------------------------ |
| Character not moving | Enable Apply Root Motion on Animator       |
| Jitter               | Don't mix root motion with script movement |
| Wrong direction      | Check Bake Into Pose options               |

---

## UI Toolkit Debugging

```csharp
// Element not found? List all:
root.Query().ForEach(e => Debug.Log($"{e.GetType().Name}: {e.name}"));

// Styles not applying? Check UI Toolkit Debugger (Window → UI Toolkit → Debugger)

// Events not firing? Verify:
button.pickingMode = PickingMode.Position;
```

For full UI Toolkit reference → [unity-ui-toolkit.md](unity-ui-toolkit.md).

---

## Audio Debugging

### Quick Checklist

1. AudioClip assigned?
2. Volume > 0?
3. AudioListener in scene?
4. AudioSource not muted?
5. GameObject active?

### Spatial Audio

```csharp
_audioSource.spatialBlend = 1f; // 0 = 2D, 1 = 3D
// Mixer uses decibels (-80 to 0), not linear (0 to 1)
float LinearToDecibel(float linear) => linear > 0 ? 20f * Mathf.Log10(linear) : -80f;
```

---

## Async & Coroutine Debugging

### Coroutine Pitfalls

```csharp
// ❌ Multiple coroutines running → store and stop reference:
private Coroutine _currentCoroutine;
public void StartMyCoroutine()
{
    if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
    _currentCoroutine = StartCoroutine(MyCoroutine());
}
```

### Awaitable (Unity 6+ Preferred)

```csharp
// Auto-cancelled when destroyed:
private async Awaitable DoSomethingAsync()
{
    await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
    // Safe — won't execute if destroyed
    transform.position = Vector3.zero;
}

// Custom cancellation:
private CancellationTokenSource _cts;
private async Awaitable DoWithCancellation()
{
    _cts = new CancellationTokenSource();
    try
    {
        await Awaitable.WaitForSecondsAsync(5f, _cts.Token);
    }
    catch (OperationCanceledException) { Debug.Log("Cancelled"); }
}
```

### Awaitable vs Coroutine

| Feature            | Coroutine            | Awaitable (Unity 6)        |
| ------------------ | -------------------- | -------------------------- |
| Cancellation       | Manual StopCoroutine | Built-in token             |
| Return values      | No                   | Yes (`Awaitable<T>`)       |
| Exception handling | Limited              | Full try/catch             |
| Destruction safety | Manual check         | `destroyCancellationToken` |

---

## Event System Debugging

### Subscription Leaks

```csharp
// ❌ Memory leak:
private void Start() => GameManager.OnGameOver += HandleGameOver;

// ✅ Always pair subscribe/unsubscribe:
private void OnEnable() => GameManager.OnGameOver += HandleGameOver;
private void OnDisable() => GameManager.OnGameOver -= HandleGameOver;
```

| Symptom                    | Solution                                                            |
| -------------------------- | ------------------------------------------------------------------- |
| Event never fires          | Add `?.Invoke()`                                                    |
| Event fires multiple times | Check for duplicate subscriptions — always unsubscribe in OnDisable |
| NullReference on Invoke    | Use `?.Invoke()` pattern                                            |

---

## ScriptableObject Runtime Issues

```csharp
// ❌ Modifies the ASSET in Editor:
[SerializeField] private PlayerDataSO _playerData;
private void TakeDamage(int d) => _playerData.health -= d;

// ✅ Create runtime copy:
private PlayerDataSO _runtimeData;
private void Awake() => _runtimeData = Instantiate(_playerData);
private void OnDestroy() { if (_runtimeData) Destroy(_runtimeData); }
```

| Symptom                      | Solution                                      |
| ---------------------------- | --------------------------------------------- |
| Changes persist after play   | `Instantiate()` for runtime copy              |
| Multiple objects share state | Intentional? Keep. Otherwise: `Instantiate()` |
| SO null at runtime           | Check Resources folder or Addressables        |

---

## Transform & Hierarchy

### Local vs World

```csharp
transform.position = new Vector3(0,0,0);    // World space
transform.localPosition = new Vector3(0,0,0); // Parent-relative

// SetParent behavior:
transform.SetParent(newParent);        // Maintains world position
transform.SetParent(newParent, false); // Maintains local position
```

---

## Performance Diagnostics

```csharp
using Unity.Profiling;
private static readonly ProfilerMarker _updateMarker = new("MyScript.Update");
private void Update() { using (_updateMarker.Auto()) { /* code */ } }
```

| Symptom      | Diagnostic          | Solution                       |
| ------------ | ------------------- | ------------------------------ |
| Frame drops  | Profiler → CPU      | Identify expensive methods     |
| Memory grows | Profiler → Memory   | Check leaks, implement pooling |
| GC spikes    | Profiler → GC Alloc | Reduce allocations in Update   |

---

## Build-Only Issues

Common "works in Editor, fails in build" causes:

1. **Script stripping** — add `[Preserve]` or link.xml.
2. **Assembly definitions** — missing .asmdef references.
3. **Resources path** — case sensitivity on some platforms.
4. **Editor-only code** — not wrapped in `#if UNITY_EDITOR`.

```csharp
#if UNITY_EDITOR
    Debug.Log("Editor only");
#elif UNITY_ANDROID
    // Android-specific
#endif
```

---

## Programmatic Breakpoints

```csharp
if (_health < 0)
{
    Debug.LogError("Health negative — breaking");
    System.Diagnostics.Debugger.Break(); // Pauses Rider/VS
}
```
