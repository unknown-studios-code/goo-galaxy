---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Design Patterns for Unity

> **Cross-references:**
>
> - Code style & naming → [unity-code-style.md](unity-code-style.md)
> - UI Toolkit patterns (MVP, data binding, Template Method) → [unity-ui-toolkit.md](unity-ui-toolkit.md)
> - Performance anti-patterns → [unity-performance-optimization.md](unity-performance-optimization.md)

## SOLID Principles

### Single Responsibility

- One reason to change per class. Break large classes into focused components.
- This project applies SRP via [composition](#composition-over-inheritance) on MapTile.

### Open/Closed

- Open for extension, closed for modification.
- Use abstract bases so new types can be added without changing existing code.

```csharp
public abstract class Shape { public abstract float CalculateArea(); }
public class Rectangle : Shape { public float Width, Height; public override float CalculateArea() => Width * Height; }
public class Circle : Shape { public float Radius; public override float CalculateArea() => Radius * Radius * Mathf.PI; }
```

### Liskov Substitution

- Subtypes must be substitutable for base types without breaking correctness.
- Don't override methods in ways that violate base class expectations.

```csharp
// ✅ Derived classes honor the contract
public abstract class Unit { public abstract int CalculateDamage(); }

// ❌ Violates LSP — returns -1 when callers expect positive values
public class RangedUnit : Unit { public override int CalculateDamage() => _ammo <= 0 ? -1 : _baseDamage; }
```

### Interface Segregation

- No class should implement interfaces it doesn't use.
- Prefer small, focused interfaces over large ones.
- **Naming:** `I` prefix, PascalCase → [unity-code-style.md](unity-code-style.md#interfaces).

```csharp
// ✅ Focused
public interface IDamageable { void ApplyDamage(int amount); }
public interface IHealable { void Heal(int amount); }

// ❌ Forces implementers to handle capabilities they don't have
public interface IEntity { void ApplyDamage(int); void Heal(int); void MoveTo(Vector3); void Attack(IEntity); }
```

### Dependency Inversion

- Depend on abstractions (interfaces), not concrete classes.
- This project uses a [Service Locator](#service-locator--dependency-injection) for runtime resolution.

```csharp
public interface IAudioService { void PlaySound(string clipName); }
public class CombatController : MonoBehaviour
{
    private IAudioService _audioService;
    private void Awake() => _audioService = ServiceLocator.Resolve<IAudioService>();
}
```

---

## Project Patterns (Used in This Codebase)

Match these when generating new code:

| Pattern                                                   | Location                                      | Purpose                             |
| --------------------------------------------------------- | --------------------------------------------- | ----------------------------------- |
| [Observer](#observer-pattern)                             | `StaticGameEvents.cs`                         | Centralized event bus               |
| [State (Enum)](#enum-based-state-pattern)                 | `UIGameController.cs`                         | UI state machine                    |
| [Template Method](#template-method-pattern)               | `UITKBaseClass.cs`                            | Base class for all UI Toolkit views |
| [Singleton](#singleton-pattern)                           | `UIGameController.cs`                         | Global UI state access              |
| [Service Locator](#service-locator--dependency-injection) | `ServiceLocator.cs` / `DependencyInjector.cs` | Runtime dependency resolution       |
| [Composition](#composition-over-inheritance)              | `MapTile` + `MapTileMilitary`                 | Focused tile components             |
| ScriptableObject Data                                     | `*SO` / `*DataSO` classes                     | Static configuration                |
| Data Binding                                              | `[CreateProperty]` + `dataSource`             | UI Toolkit reactive UI              |

### Reference Patterns (Not Yet in Codebase)

| Pattern                                         | Use Case                            |
| ----------------------------------------------- | ----------------------------------- |
| [Class-Based State](#class-based-state-pattern) | Complex AI or character controllers |
| [Object Pooling](#object-pooling)               | Frequent spawn/despawn              |
| [Factory](#factory-pattern)                     | Centralized object creation         |
| [Command](#command-pattern)                     | Undo/redo, action history           |
| [Strategy](#strategy-pattern)                   | Interchangeable runtime behaviors   |

---

## Observer Pattern

**This project's pattern: centralized static event bus in `StaticGameEvents.cs`.**

- **Static events** for game-wide broadcasts (turn ended, tile selected, resources changed).
- **Static invoke methods** control invocation — external code cannot fire events arbitrarily.
- **Subscribe in `OnEnable()`, unsubscribe in `OnDisable()`.**

```csharp
// Centralized event bus (actual project pattern)
public static class StaticGameEvents
{
    public static event Action<MapTile> OnTileSelected;
    public static event Action OnTurnEnded;
    public static event Action<UIGameState> OnUIStateChanged;

    public static void InvokeOnTileSelected(MapTile tile) => OnTileSelected?.Invoke(tile);
    public static void InvokeOnTurnEnded() => OnTurnEnded?.Invoke();
    public static void InvokeOnUIStateChanged(UIGameState s) => OnUIStateChanged?.Invoke(s);
}

// Consumer
public class MapTile : MonoBehaviour
{
    private void OnEnable() => StaticGameEvents.OnTurnEnded += CalculateEndOfTurnDif;
    private void OnDisable() => StaticGameEvents.OnTurnEnded -= CalculateEndOfTurnDif;
}
```

---

## State Pattern

### Enum-Based (This Project's Approach)

Used by `UIGameController` — states control which panels are visible. Prefer when states have minimal per-state logic.

```csharp
public enum UIGameState { DefaultMapView, ArmyView, TownView, ConquestView /* ... */ }

public class UIGameController : MonoBehaviour
{
    [SerializeField] private UIGameState _currentState;

    public void ChangeState(UIGameState newState)
    {
        _currentState = newState;
        StaticGameEvents.InvokeOnUIStateChanged(_currentState);
        ApplyStateToPanels(newState);
    }

    private void ApplyStateToPanels(UIGameState state)
    {
        HideAllPanels();
        switch (state)
        {
            case UIGameState.DefaultMapView:
                SetPanelsActive(_resources, true);
                break;
            case UIGameState.ArmyView:
                SetPanelsActive(_commanderView, true);
                break;
            // ...
        }
    }
}
```

### Class-Based (For Complex Cases)

Use when each state has substantial logic (complex AI, character controllers).

| Criteria        | Enum + Switch           | Class-Based                        |
| --------------- | ----------------------- | ---------------------------------- |
| State count     | < 10                    | Many or growing                    |
| Per-state logic | Minimal (toggle panels) | Complex (different Update loops)   |
| Example         | UI view management      | AI behavior, character controllers |

```csharp
public abstract class PlayerState
{
    protected PlayerController _controller;
    public PlayerState(PlayerController c) { _controller = c; }
    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
}

public class PlayerController : MonoBehaviour
{
    private PlayerState _currentState;
    private void Update() => _currentState.Update();
    public void ChangeState(PlayerState s) { _currentState.Exit(); _currentState = s; _currentState.Enter(); }
}
```

---

## Template Method Pattern

**This project's pattern: `UITKBaseClass` as base for all UI Toolkit views.**

Defines the skeleton: `InitializeElements → RegisterCallbacks → UnregisterCallbacks → ShowPanel`.

```csharp
public abstract class UITKBaseClass : MonoBehaviour
{
    protected UIDocument _uiDocument;
    protected VisualElement _rootVisualElement;

    protected virtual void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _rootVisualElement = _uiDocument.rootVisualElement;
        InitializeElements();
    }

    protected virtual void OnEnable() => RegisterCallbacks();
    protected virtual void OnDisable() => UnregisterCallbacks();

    protected abstract void InitializeElements();
    protected abstract void RegisterCallbacks();
    protected abstract void UnregisterCallbacks();
    public abstract void ShowPanel(bool show);
}
```

---

## Singleton Pattern

**This project's pattern (from `UIGameController.cs`):**

```csharp
public class UIGameController : MonoBehaviour
{
    private static UIGameController _instance;
    public static UIGameController Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<UIGameController>(); return _instance; }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }
}

// With DontDestroyOnLoad:
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    // ... same pattern, add DontDestroyOnLoad(gameObject) in Awake
}
```

---

## Service Locator / Dependency Injection

**This project's pattern:**

```csharp
// Lightweight static registry
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    public static void Register<T>(T service) where T : class => _services[typeof(T)] = service;
    public static T Resolve<T>() where T : class => _services.TryGetValue(typeof(T), out object s) ? s as T
        : throw new InvalidOperationException($"Service of type {typeof(T)} not registered.");
    public static void Clear() => _services.Clear();
}

// Centralized registration
public class DependencyInjector : MonoBehaviour
{
    [SerializeField] private GameResources _gameResources;
    private void Awake() { ServiceLocator.Register(_gameResources); /* ... */ }
    private void OnDestroy() => ServiceLocator.Clear();
}

// Consumer
public class RecruitmentManager : MonoBehaviour
{
    private GameResources _gameResources;
    private void Awake() => _gameResources = ServiceLocator.Resolve<GameResources>();
}
```

---

## Composition over Inheritance

**This project's approach: decompose GameObjects into focused components.**

```
GameObject: "MapTile_Farmland"
├── MapTile                  — Population, happiness, taxation
├── MapTileMilitary          — Recruitment pool, military strength
├── MapTileBuildings         — Building slots, construction bonuses
└── MapTileConstructionManager — Active construction logic
```

- Each component = single responsibility.
- Wire via `GetComponent<T>()` in `Awake()`.
- Add new concerns by adding components, not modifying existing classes.

---

## Object Pooling

> Canonical code example → [unity-code-style.md](unity-code-style.md#object-pooling). Performance rules → [unity-performance-optimization.md](unity-performance-optimization.md).

For frequent spawn/despawn (bullets, particles, enemies):

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
            defaultCapacity: 20, maxSize: 100
        );
    }

    public Bullet GetFromPool() => _pool.Get();
    public void ReturnToPool(Bullet bullet) => _pool.Release(bullet);
}
```

For collection reuse → `CollectionPool<T>`, `ListPool<T>`, `DictionaryPool<TKey, TValue>`.

---

## Factory Pattern

Centralize object creation with setup logic:

```csharp
public class EnemyFactory : MonoBehaviour
{
    [SerializeField] private GameObject _infantryPrefab, _cavalryPrefab, _archerPrefab;

    public GameObject CreateEnemy(UnitCategory category, Vector3 spawn)
    {
        GameObject prefab = category switch
        {
            UnitCategory.Infantry => _infantryPrefab,
            UnitCategory.Cavalry  => _cavalryPrefab,
            UnitCategory.Ranged   => _archerPrefab,
            _ => throw new ArgumentException($"Unknown: {category}")
        };
        var enemy = Instantiate(prefab, spawn, Quaternion.identity);
        enemy.name = $"{category}_{Time.frameCount}";
        return enemy;
    }
}
```

---

## Command Pattern

For undo/redo, action history, input replay:

```csharp
public interface ICommand { void Execute(); void Undo(); }

public class MoveArmyCommand : ICommand
{
    private readonly ArmyController _army;
    private readonly Vector3 _target, _previous;
    public MoveArmyCommand(ArmyController army, Vector3 target) { _army = army; _target = target; _previous = army.transform.position; }
    public void Execute() => _army.transform.position = _target;
    public void Undo() => _army.transform.position = _previous;
}

public class CommandInvoker
{
    private readonly Stack<ICommand> _undoStack = new(), _redoStack = new();
    public void ExecuteCommand(ICommand cmd) { cmd.Execute(); _undoStack.Push(cmd); _redoStack.Clear(); }
    public void Undo() { if (_undoStack.Count == 0) return; var cmd = _undoStack.Pop(); cmd.Undo(); _redoStack.Push(cmd); }
    public void Redo() { if (_redoStack.Count == 0) return; var cmd = _redoStack.Pop(); cmd.Execute(); _undoStack.Push(cmd); }
}
```

---

## Strategy Pattern

For interchangeable runtime behaviors (AI, movement, attack types):

```csharp
public interface IMovementStrategy { void Move(Transform t, Vector3 target, float speed); }
public class DirectMovement : IMovementStrategy { public void Move(Transform t, Vector3 target, float speed) => t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime); }

public class ArmyMovementController : MonoBehaviour
{
    private IMovementStrategy _strategy;
    public void SetMovementStrategy(IMovementStrategy s) => _strategy = s;
    private void Update() => _strategy?.Move(transform, _targetPosition, _moveSpeed);
}
```
