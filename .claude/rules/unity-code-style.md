---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Unity C# Code Style

> **Cross-references:**
>
> - Debugging → [unity-debugging.md](unity-debugging.md)
> - Design patterns → [unity-design-patterns.md](unity-design-patterns.md)
> - UI Toolkit → [unity-ui-toolkit.md](unity-ui-toolkit.md)
> - Performance → [unity-performance-optimization.md](unity-performance-optimization.md)

## Project Standards

- **Unity 6.3** — use Unity 6+ APIs only. Prefer `Awaitable` over coroutines.
- **Input System** (new) — not legacy Input Manager.
- **UI Toolkit** — not UGUI.
- **Universal Render Pipeline** — not Built-in Render Pipeline.
- **CSharpier** formatting (`.csharpierrc.json`), EditorConfig (`.editorconfig`).

---

## Formatting

- **Allman style** — opening braces on new line.
- **Max line width:** 160 characters. Break long lines; don't overflow.
- **Single space** before flow-control conditions: `while (x == y)`.
- **No spaces inside brackets:** `x = dataArray[index]`.
- **Single space after comma** between arguments: `CollectItem(myObject, 0, 1)`.
- **No spaces** between function name and parenthesis: `DropPowerUp(...)`.
- **No spaces** just inside parentheses: not `CollectItem( myObject, 0, 1 )`.
- **Vertical spacing** (blank lines) for visual separation between logical blocks.
- **One variable declaration per line.**

```csharp
// ✅ Allman braces + spacing
public void ProcessItems(List<Item> items, int startIndex)
{
    for (int i = startIndex; i < items.Count; i++)
    {
        ProcessItem(items[i]);
    }

    Debug.Log("Processing complete");
}

// ❌
public void ProcessItems ( List<Item>items,int startIndex ) { for(int i=startIndex;i<items.Count;i++) { ProcessItem( items [ i ] ); } Debug.Log("Processing complete"); }
```

### Regions

- **Use sparingly** — regions hide code and reduce readability.
- **Valid use:** grouping Animation Event Handlers or Input Event Handlers called from animation/input systems.

```csharp
#region Animation Event Methods
public void OnLand() { Debug.Log("OnLand called from animation event"); }
public void OnFootstep() { /* footstep sound */ }
#endregion
```

---

## Comments

- **Comment intent ("why"), not mechanics ("what").**
- Use `[Tooltip]`, `[Header]`, `[Space]` for serialized fields needing Inspector context.

```csharp
// Skip processing if below threshold to avoid performance issues with small batches
if (itemCount < processingThreshold) return;

[Tooltip("Maximum distance the player can travel in one frame")]
[SerializeField] private float _maxDeltaMovement = 10f;
```

---

## Class Organization

Order by Unity script execution order:

1. Using statements
2. Namespace
3. **Fields** (constants → static → serialized private → private)
4. **Properties**
5. **Events**
6. **MonoBehaviour methods:** `Awake()` → `OnEnable()` → `Start()` → `OnDisable()` → `OnDestroy()` → `FixedUpdate()` → `Update()` → `LateUpdate()`
7. Public methods
8. Private methods

### Using Statements

- System namespaces first (`System`, `System.Collections`).
- Unity namespaces second (`UnityEngine`).
- Project namespaces last.
- **Remove unused usings.**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MyGameProject.Utilities;
```

### Namespaces

- **PascalCase**, no underscores or special chars.
- Sub-namespaces with dot: `MyApplication.GameFlow`.

```csharp
namespace MyGame.Characters { }
```

---

## Fields

- **Always include `private` accessor** — explicit intent.
- **`_camelCase` prefix** for all private fields (instance and static). Per `.editorconfig` Section 4.
- **PascalCase** for constants (no prefix). Per `.editorconfig` Section 4.
- **Descriptive names** — include units if applicable: `_speedInMetersPerSecond`.
- **Boolean prefix:** `is`, `has`, `can` — `_isActive`, `_hasPermission`.
- **No abbreviations** (except widely known: `UI`, `ID`).
- **No class name repetition** in field names: use `_health` in `Player`, not `_playerHealth`.
- **Expose with `[SerializeField]`** — keep private, use properties for external access.

```csharp
[SerializeField] private int _health;
private static int _sharedCount;
private const int MaxCount = 100;
private int _elapsedTimeInHours;
[SerializeField] private bool _isPlayerDead;
```

### Properties

- **PascalCase**, no prefixes/suffixes.
- Place after fields, before MonoBehaviour methods.
- **Boolean properties:** `Is`/`Has`/`Can` prefix.
- **Do not serialize properties.** Use `[SerializeField] private T _field` + public property.
- **Properties = lightweight state access** (Health, Speed, IsGrounded).
- **Methods = actions/operations** (ApplyDamage, not SetHealth).
- **No side effects** in property getters; no significant computation.

```csharp
private int _maxHealth;
public int MaxHealthReadOnly => _maxHealth;
public int MaxHealth
{
    get => _maxHealth;
    set => _maxHealth = value;
}
public string DescriptionName { get; set; } = "Fireball";
```

### Events

- **`event Action` / `event Action<T>`** for code-only events (lightweight).
- **`UnityEvent`** only when Inspector exposure needed.
- **Past-tense verb** for event names: `DoorOpened`, `PointsScored`.
- **`On` prefix** for raising methods: `OnDoorOpened()`.
- **Null-conditional (`?.`) when invoking:** `DoorOpened?.Invoke()`.
- Use **EventArgs** subclasses for complex/multi-parameter data.
- **Subscribe in `OnEnable`, unsubscribe in `OnDisable`.**
- **Avoid lambdas** for subscriptions — prevents unsubscribing.
- **Static events** from long-lived objects (singletons) → beware memory leaks with short-lived subscribers.

```csharp
public event Action DoorOpened;
public event Action<int> PointsScored;

public void OnDoorOpened() => DoorOpened?.Invoke();
public void OnPointsScored(int points) => PointsScored?.Invoke(points);

private void OnEnable() => _gameManager.DoorOpened += HandleDoorOpened;
private void OnDisable() => _gameManager.DoorOpened -= HandleDoorOpened;
```

---

## MonoBehaviour Methods

### Awake()

- Initialize self-references and sibling component references.
- Cache `GetComponent<T>()` results, create pools.
- **No** heavy work, scene-dependent calls, or external event subscriptions.

### OnEnable()

- Subscribe to events, register input callbacks.
- Keep work small and reversible in `OnDisable()`.

### Start()

- Initialization requiring other components/scene objects to be ready.
- One-time setup: animations, UI wiring.

### OnDisable()

- Unsubscribe from events, clean up per-enable state.

### FixedUpdate()

- **Physics only:** `AddForce`, rigidbody velocity, simulation steps.
- Runs on fixed timestep; may fire 0–N times per Update.
- **Read input in Update(), apply in FixedUpdate().**
- Keep allocation-free.

### Update()

- Input handling, timers, non-physics per-frame logic, state machines.
- **Early returns** to skip processing: `if (!_isActive) return;`.
- **No allocations:** reuse collections, cache references.
- Delegate logic to well-named helper methods.

### LateUpdate()

- Finalize transforms, camera follow, post-Update cleanup.

### General Notes

- **`[RequireComponent(typeof(X))]`** when dependencies exist — ensures component presence, removes null checks.
- **Cache expensive operations** outside update loops.
- **No magic numbers/strings** — use constants or serialized fields.
- **Keep MonoBehaviours single-responsibility.**

```csharp
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private bool _isActive;
    private float _nextUpdateTime;
    private const float UpdateInterval = 0.5f;

    private void Awake() => _rigidbody = GetComponent<Rigidbody>();

    private void Update()
    {
        if (!_isActive) return;
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + UpdateInterval;
        UpdateNearbyEnemies();
    }
}
```

---

## Methods

- **Verb names** describing action: `Jump()`, `TakeDamage(int)`, `CalculateDamage()`.
- **`Set` prefix** for assigning/updating: `SetMovementInput(Vector2)`.
- **`Change` prefix** for modifying/transforming state: `ChangeHealth(int)`.
- **`Process` prefix** for game-logic operations (turn-based, system-driven): `ProcessTradeIncome()`.
- **`Handle` prefix** for event-driven callbacks: `HandleTileSelected()`, `HandleTurnEnded()`.
- **Boolean methods:** `Is`/`Has`/`Can` prefix, return `bool`: `IsPlayerAlive()`.
- **Avoid** noun-only or gerund names: `Walking()` → use `isWalking` property instead.
- Prefer "method" (C# terminology).

```csharp
public void Jump() => _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
public void SetMovementInput(Vector2 input) => _forwardMovementInput = input;
public void ChangeHealth(int amount) => _health += amount;
public bool IsNewPosition(Vector3 pos) => transform.position == pos;
```

---

## Interfaces

- **`I` prefix**, PascalCase: `IDamageable`, `IAudioService`.
- **Small, focused** — one responsibility per interface (Interface Segregation).
- Verb-named methods, `Is`/`Has`/`Can` for booleans.
- Interface = pure contract. Abstract base class = shared implementation.

```csharp
public interface IDamageable
{
    string DamageTypeName { get; }
    float DamageValue { get; }
    bool ApplyDamage(string description, float damage, int numberOfHits);
}
```

---

## Files & Folders

- **PascalCase** for all files and folders: `CharacterController.cs`, `CoreSystems/`.
- Organize by functionality/feature: `CoreSystems/`, `UI/`.
- Long folder paths are fine if they improve organization.
- **No spaces or special characters.**
- Use `_` for word separation in very long names: `InputSystemActions_PlayerInputComponent_UnityEvents`.
- **Never** stub with `throw new NotImplementedException()` — leave empty body or comment.

```csharp
// ✅
private void LookInputReceived(InputAction.CallbackContext context)
{
    // TODO: implement look input handling
}

// ❌
private void LookInputReceived(InputAction.CallbackContext context)
{
    throw new NotImplementedException();
}
```

---

## Enums

- **PascalCase** names and values. Singular noun for enum name.
- **No prefixes/suffixes** (no `Enum`, `Type`, `E_`).
- Use for mutually exclusive states. Use `switch` for handling.
- **Never** use strings/integers for state tracking.
- Public enums can be declared outside class for global access.

```csharp
public enum Direction { North, South, East, West }

[Flags]
public enum AttackModes
{
    None   = 0,
    Melee  = 1,
    Ranged = 2,
    Special = 4,
    MeleeAndSpecial = Melee | Special
}
```

---

## Collections

- **`List<T>`** — dynamic size, frequent add/remove.
- **Array** — fixed size, performance-critical paths.
- **`Stack<T>`** — LIFO (undo, state history, command buffers).
- **`Dictionary<TKey, TValue>`** — fast key lookups.
- **No allocations in loops** — reuse collections, call `.Clear()`.
- **Initialize with capacity** when possible: `new List<T>(capacity)`.
- **`foreach`** for read-only iteration (readability). `for` for performance.

```csharp
[SerializeField] private List<GameObject> _enemies = new(); // C# 9+
```

---

## Async & Awaitable (Unity 6+)

- **Prefer `Awaitable`** over coroutines for delays and sequencing.
- **`Async` suffix** for async methods: `OpenDoorAsync()`.
- **`Co` suffix** for coroutines: `LoadAssetsCo()`.
- **Use `destroyCancellationToken`** for auto-cancellation when destroyed.
- **Don't mix** Awaitable and coroutines in one workflow.
- **Guard after await:** `if (this == null || !isActiveAndEnabled) return;`.

```csharp
public async Awaitable OpenDoorAsync()
{
    await Awaitable.WaitForSecondsAsync(2f, destroyCancellationToken);
    Debug.Log("Door opened!");
}
```

---

## Object Pooling

- **Use `UnityEngine.Pool.ObjectPool<T>`** — Unity 6 built-in.
- **Prefer over `Instantiate`/`Destroy`** for frequent spawn/despawn.
- **Initialize in `Awake()`**, pre-warm with reasonable capacity.
- **Always reset state** (position, rotation, active) before reuse.
- **Return to pool** — never `Destroy()` pooled objects.
- Name methods `GetFromPool()` / `ReturnToPool()`.

```csharp
using UnityEngine.Pool;

public class BulletPool : MonoBehaviour
{
    [SerializeField] private Bullet _bulletPrefab;
    private ObjectPool<Bullet> _pool;

    private void Awake()
    {
        _pool = new ObjectPool<Bullet>(
            createFunc: () => Instantiate(_bulletPrefab),
            actionOnGet: bullet => bullet.gameObject.SetActive(true),
            actionOnRelease: bullet => bullet.gameObject.SetActive(false),
            actionOnDestroy: bullet => Destroy(bullet.gameObject),
            collectionCheck: false,
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public Bullet GetFromPool() => _pool.Get();
    public void ReturnToPool(Bullet bullet) => _pool.Release(bullet);
}
```

---

## ScriptableObjects

- **For static configuration data** — weapons, enemy stats, skill effects. Not runtime state.
- **`[CreateAssetMenu]`** for easy creation.
- **`DataSO` suffix:** `WeaponDataSO`.
- Store in dedicated folders: `Assets/Data/Weapons/`.
- **Single responsibility** — one SO per concept.
- **Properties over public fields** for encapsulation.

```csharp
[CreateAssetMenu(fileName = "WeaponData", menuName = "Game Data/Weapon", order = 0)]
public class WeaponDataSO : ScriptableObject
{
    [SerializeField] private string _weaponName;
    [SerializeField] private int _damage;
    public string WeaponName => _weaponName;
    public int Damage => _damage;
}
```

---

## Animation Parameters, Layers, Tags, Input Actions

- **PascalCase** for all text-based references.
- **Boolean params:** `Is`/`Has`/`Can` prefix (`IsRunning`).
- **Define as `const` strings** — prevents runtime typos, enables refactoring.
- **Centralize** in static classes: `Layers`, `Tags`, `InputActions`.

```csharp
public static class Layers
{
    public const string Player = "Player";
    public const string Enemy = "Enemy";
}

private const string IsRunningParam = "IsRunning";
private const string SpeedParam = "Speed";

_animator.SetBool(IsRunningParam, isMoving);
```

---

## String Allocation

- **String interpolation (`$""`)** over concatenation (`+`) — reduces garbage.

```csharp
// ❌
return "Score: " + score + " Time: " + time;

// ✅
string result = $"Score: {score} Time: {time:F1}";
```

---

## Try-Catch

- **For external dependencies only:** file I/O, network, database.
- **Not for internal logic** — validate inputs, use control flow.
- **Log exception details** (`ex.ToString()`).
- Consider `Debug.Break()` in `#if UNITY_EDITOR` for unexpected exceptions.

---

## Avoid Nesting

- **Early returns** over nested `if` blocks.

```csharp
// ❌
if (conditionA) { if (conditionB) { ExecuteAction(); } }

// ✅
if (!conditionA) return;
if (!conditionB) return;
ExecuteAction();
```
