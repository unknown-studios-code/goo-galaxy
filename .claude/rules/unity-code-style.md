---
description: "Use when writing or reviewing Unity C# code. Covers formatting, naming, braces, fields, properties, methods, events, async, pooling, and file conventions."
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/Editor/**/*.cs"
---

# Unity C# Code Style

## 1. Overview

This document defines the C# style, formatting, naming, and architectural conventions for the codebase. These rules ensure maintainability, readability, and performance across all game systems. Layout is enforced by CSharpier (`printWidth 160`) and whitespace by `editorconfig-checker` — when in doubt, run `npm run format` and let the tooling settle it. The naming matrix below is **not** currently checked by any tool: `dotnet format` was removed because it depends on Unity-generated project files (see `.docs/refinement/csharp-analysis-in-a-unity-project.md`), so naming rests on review until Roslyn analyzers run inside Unity's own compilation.

## 2. Cross-References

- **Class Organization** → [unity-class-organization.md](unity-class-organization.md) (Detailed member layout rules for types)
- **Code Documentation** → [unity-code-documentation.md](unity-code-documentation.md) (XML, tooltip, and comment rules)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Standard practices for assertions and diagnostics)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Banned patterns in update loops and hot paths)
- **UI Toolkit** → [unity-ui-toolkit.md](unity-ui-toolkit.md) (Style and naming conventions specific to UI systems)
- **Testing** → [unity-testing.md](unity-testing.md) (How the same conventions apply to test code)

## 3. Core Rules

### General Formatting & Structure

- **Braces:** Apply Allman style (opening braces on a new line). Always use braces, even for a single-statement `if` or an early return.
- **Line Limits:** Keep lines under 160 characters. Break long lines logically; when wrapping a binary expression, put the operator at the beginning of the new line.
- **Spacing:** Put a single space before flow-control conditions and around binary operators. Do not put spaces inside brackets or immediately inside parentheses. Put a single space after commas between arguments. Declare one variable per line.
- **Parentheses:** Add explicit parentheses in arithmetic, relational, and logical expressions to make precedence obvious, even when the compiler does not need them.
- **Regions:** Avoid `#region` blocks. Only use them to group Animation or Input Event Handlers.
- **Imports:** Place `using` directives outside the namespace, `System` first, with no blank line between groups. Remove all unused usings — `IDE0005` is a warning.
- **Namespaces:** Use block-scoped namespaces (`namespace X { ... }`), never file-scoped. Format names in PascalCase, build hierarchy with `.` (e.g. `GooGalaxy.Runtime.Board.Models`), and keep the namespace matching the folder path.
- **Types over keywords:** Use language keywords (`int`, `string`, `float`), not framework names (`Int32`, `String`, `Single`). Do not qualify members with `this.`.
- **`var` usage:** Use `var` only when the type is apparent from the right-hand side (`var grid = new HexGrid(...)`). Use the explicit type for built-in types and for anything a reader cannot infer at a glance.
- **Modifier order:** `public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async`.
- **Expression-bodied members:** Use `=>` for single-line properties, accessors, indexers, lambdas, and local functions. Write methods, constructors, and operators with a full body — including Unity lifecycle callbacks.

### Fields, Properties, and Enums

- **Fields:** Always specify the access modifier explicitly. Prefix private and internal fields — instance _and_ static — with `_camelCase`. Format `const` and public/protected `static readonly` fields in PascalCase. Mark any field that is never reassigned after construction as `readonly`. Expose private fields in the inspector via `[SerializeField]` rather than making fields public.
- **Field naming:** Use descriptive names; include units when applicable (`_speedInMetersPerSecond`). Boolean fields must use `is`, `has`, or `can` prefixes. Do not repeat the type name in the field (`_health` in a `Player`, not `_playerHealth`). Do not abbreviate unless the abbreviation is universal (`UI`, `ID`).
- **Properties:** Format properties in PascalCase without prefixes. Place properties after fields. Boolean properties must use `Is`, `Has`, or `Can` prefixes. Do not serialize properties — serialize a private field and expose it through a read-only property. Properties represent lightweight state access with no side effects; anything that does work is a method.
- **Enums:** Format enum names and values in PascalCase. Use singular nouns and no prefixes or suffixes (`Enum`, `E_`). Use enums instead of strings or integers for state tracking. Declare an enum outside a class when more than one type consumes it.
- **Magic values:** Do not hardcode numbers or strings in logic. Promote them to `const`, a `[SerializeField]`, or a `ScriptableObject` field so designers and reviewers can see them.

### Events & Methods

- **Events:** Use `event Action` or `event Action<T>`. Use `UnityEvent` only when Inspector exposure is required. Name events with past-tense verbs (`DoorOpened`, `MoveExecuted`). Raise them with null-conditional invocation from a method whose prefix matches the shape: `protected virtual void OnMoveExecuted(...)` for an instance event on an unsealed class — the one case the .NET guidelines define, since the point is letting a subclass override it — and `RaiseMoveExecuted(...)` for a static event, a sealed class, or an event bus, where no override is possible and the caller is the publisher. Never `Fire*` or `Trigger*`; `On*` is also what handlers and Unity messages are called, so it must not name a publicly callable publisher. Subscribe in `OnEnable` and unsubscribe in `OnDisable`, always with a named method — never a lambda, which cannot be unsubscribed.
- **Event payloads:** Pass one argument, or a single struct/`EventArgs`-style type when the payload has several fields. Do not grow an event to four or five loose parameters.
- **Event lifetime:** Be explicit when a long-lived publisher (a static bus, a container-scoped service) outlives its subscribers — the subscriber owns the unsubscription, and a missed one is a leak that survives scene loads.
- **Methods:** Use active verbs (`Jump`, `ApplyDamage`). Use `Set` for assignment, `Change` for state modification, `Process` for system-driven game logic, and `Handle` for event callbacks. Name boolean methods with `Is`, `Has`, or `Can`. Avoid noun-only and gerund names (`Walking()`); those describe state, which belongs in a property.

### MonoBehaviour Lifecycle & Composition

- **Awake:** Perform self-initialization and cache local component references. Do not run heavy setup, scene-dependent lookups, or event subscriptions here.
- **OnEnable / OnDisable:** Subscribe to events and register input callbacks in `OnEnable`; reverse every one of them in `OnDisable`.
- **Start:** Initialize anything that depends on other objects already being awake — cross-object wiring, UI population, one-time scene setup.
- **FixedUpdate:** Physics only — forces, velocities, simulation steps. Read input in `Update` and apply it here. Keep it allocation-free.
- **Update:** Input, timers, non-physics state, and state machine ticks. Use early returns to keep nesting shallow and delegate bodies to named methods.
- **LateUpdate:** Anything that must observe the final state of the frame — camera follow, transform corrections after animation, cleanup after `Update`.
- **OnDestroy:** Release what the object owns: pooled instances, runtime `ScriptableObject` copies created with `Instantiate`, native collections, container registrations.
- **Composition:** Apply `[RequireComponent(typeof(<Type>))]` for sibling dependencies, and `[DisallowMultipleComponent]` when a second copy on the same GameObject would be a bug.

### Memory, Performance, and Async

- **Collections:** Pick the type for the access pattern — `List<T>` for ordered iteration, `Dictionary<TKey, TValue>` for keyed lookup, `HashSet<T>` for membership tests, `Queue<T>`/`Stack<T>` for FIFO/LIFO, arrays for fixed-size hot data. Do not allocate collections inside loops; pre-allocate with a capacity and call `.Clear()`. Use `foreach` on read-only iteration and indexed `for` in hot paths.
- **Async:** Prefer `Awaitable` over coroutines for delays and sequencing. Append `Async` to `Awaitable` methods and `Co` to coroutines. Pass `destroyCancellationToken` to every asynchronous operation and guard the continuation after each `await` (`if (this == null || !isActiveAndEnabled) return;`). Do not mix `Awaitable` and coroutines inside one workflow.
- **Object Pooling:** Use `UnityEngine.Pool.ObjectPool<T>` for anything spawned more than a few times per second. Pre-warm at load, reset state on get/release, and return instances instead of destroying them.
- **String Allocation:** Use interpolation (`$""`) over concatenation for readability, and keep both out of per-frame code (see the performance rules).
- **Control Flow:** Prefer early returns over nesting. Use `try`/`catch` only around genuinely external operations (file I/O, networking, platform APIs) — never as flow control for conditions you can test.

### File & Configuration Integration

- **Files/Folders:** Name files and folders in PascalCase, one top-level type per file, file name matching the type. No spaces or special characters.
- **Feature layout:** Inside a feature assembly, keep the established folders: `Data/` (ScriptableObjects), `Models/` (engine-free state and logic), `Views/` (MonoBehaviours that render), `Presenters/` (mediators), `Services/` (stateless rules and calculations), `Interfaces/`, `Utils/`. Add `Controllers/` when a feature grows gameplay control that no single view owns.
- **Type suffixes:** The suffix declares the role, and roles do not overlap. `*View` renders and raises intent; `*Presenter` mediates between Models and one View or screen (`GridPresenter`, `CardPresenter`); `*Controller` drives gameplay or a system that is not bound to a single view — match flow, turn sequencing, camera, input routing (`PlayController`, `CameraController`). Never name a Presenter `Controller` or the reverse. Stateless rule classes take the verb-shaped suffix that says what they do (`*Validator`, `*Resolver`, `*Regenerator`), and pattern types keep their pattern name (`*Factory`, `*Command`, `*Strategy`, `*State`, `*StateMachine`).
- **Empty Methods:** Do not stub with `NotImplementedException`. Leave the body empty or add a `TODO` that names the tracker ID (`// TODO (GOOM-42): ...`).
- **ScriptableObjects:** Authored configuration only, never runtime state. Use `[CreateAssetMenu]`, the `DataSO` suffix, and a dedicated folder under `Assets/Data/{Feature}/`. Keep them data-first: only logic that operates on their own fields belongs there. Expose values through read-only properties.
- **Tags, Layers, and Log Messages:** Define every string that Unity resolves at runtime — tags, layers, sorting layers, animator parameters, input action names — as `const` in a centralized static class, in PascalCase, with `Is`/`Has`/`Can` prefixes for boolean animator parameters. Log message text follows the same rule (see `Shared/Constants/BoardLogMessages.cs`).

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadStyle> : MonoBehaviour
{
    public int health; // ❌ Public field (should be private serialized with property)
    private float speed; // ❌ Missing underscore prefix
    private const int MAXTARGETS = 8; // ❌ Constant not in PascalCase

    private void Update()
    {
        // ❌ Deeply nested condition, allocations, string concatenation, braceless branch
        if (_isActive)
        {
            if (Time.time > _nextActionTime)
            {
                var targetList = new List<Transform>(); // ❌ Allocation in Update loop
                string message = "Time: " + Time.time; // ❌ String concatenation
                if (targetList.Count > 5)
                    throw new NotImplementedException(); // ❌ Stub exception, missing braces
            }
        }
    }
}
```

### ✅ Do (Good)

```csharp
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class <GoodStyle> : MonoBehaviour
{
    private const int MaxTargets = 8;

    [SerializeField]
    private int _health;

    [Tooltip("Movement applied per second, in meters. Values above 12 clip through thin colliders.")]
    [SerializeField]
    private float _speedInMetersPerSecond = 5f;

    private readonly List<Transform> _targetCache = new(MaxTargets);

    private Rigidbody _rigidbody;
    private float _nextActionTime;
    private bool _isActive;

    public int Health => _health;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // ✅ Early returns keep nesting flat; braces are mandatory
        if (!_isActive)
        {
            return;
        }

        if (Time.time <= _nextActionTime)
        {
            return;
        }

        ProcessAction();
    }

    private void ProcessAction()
    {
        _targetCache.Clear();
        // TODO (GOOM-42): resolve targets from the board registry
    }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadAsyncAndEvent> : MonoBehaviour
{
    public event Action OnDeath; // ❌ Event named as a handler instead of a past-tense fact

    private void OnEnable()
    {
        // ❌ Lambda subscription prevents unsubscribing, leading to leaks
        MatchEvents.EnergyChanged += (playerId, energy) => UpdateEnergy(playerId, energy);
    }

    private async void Start()
    {
        await Task.Delay(1000); // ❌ Task.Delay instead of Awaitable, and async void
        _field = 10; // ❌ No cancellation token and no lifecycle guard after the await
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodAsyncAndEvent> : MonoBehaviour
{
    public event Action<int> EnergyDepleted;

    private void OnEnable()
    {
        MatchEvents.EnergyChanged += HandleEnergyChanged; // ✅ Named handler
    }

    private void OnDisable()
    {
        MatchEvents.EnergyChanged -= HandleEnergyChanged; // ✅ Symmetric unsubscribe
    }

    private async Awaitable <MethodName>Async()
    {
        await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

        // ✅ Guard after every await boundary
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        OnEnergyDepleted(_playerId);
    }

    private void OnEnergyDepleted(int playerId)
    {
        EnergyDepleted?.Invoke(playerId);
    }

    private void HandleEnergyChanged(int playerId, float newEnergy)
    {
        // ✅ Implementation goes here
    }
}
```

## 5. Quick Reference & Decision Matrix

Naming matrix — matches the `dotnet_naming_rule` entries in `.editorconfig`:

| Code Element                                  | Naming Style            | Notes                                     |
| :-------------------------------------------- | :---------------------- | :---------------------------------------- |
| Types (class, struct, record, enum)           | `PascalCase`            | One top-level type per file               |
| Interfaces                                    | `IPascalCase`           | `IDamageable`, `IHexGrid`                 |
| Generic type parameters                       | `TPascalCase`           | `TValue`, `TKey`                          |
| Namespaces                                    | `PascalCase`            | Must match the folder path                |
| Constants (`const`)                           | `PascalCase`            | Never `UPPER_CASE`                        |
| Public/protected `static readonly`            | `PascalCase`            | e.g. shared shader IDs exposed to callers |
| Private/internal fields (instance and static) | `_camelCase`            | Includes `private static readonly`        |
| Public/protected fields                       | `PascalCase`            | Avoid — prefer a property                 |
| Properties, methods, events, delegates        | `PascalCase`            | Events in past tense                      |
| Parameters, locals, local functions           | `camelCase`             |                                           |
| ScriptableObject files                        | `*DataSO.cs` / `*SO.cs` | `Assets/Data/{Feature}/`                  |

Type suffixes — the suffix is the contract, so pick it from the role rather than from habit:

| Suffix                                    | Responsibility                                                                                             | Folder               |
| :---------------------------------------- | :--------------------------------------------------------------------------------------------------------- | :------------------- |
| `*View`                                   | MonoBehaviour that renders state and raises user intent; decides nothing                                   | `Views/`             |
| `*Presenter`                              | Mediates between Models and one View or screen, and owns that screen's state                               | `Presenters/`        |
| `*Controller`                             | Gameplay or system control not bound to a single view — match flow, turn sequencing, camera, input routing | `Controllers/`       |
| `*Validator`, `*Resolver`, `*Regenerator` | Stateless rule or calculation; deterministic and allocation-free                                           | `Services/`          |
| `*Factory`, `*Command`, `*Strategy`       | The pattern the type implements                                                                            | Feature or `Shared/` |
| `*State`, `*StateMachine`                 | Class-based state and the driver that sequences it                                                         | Feature folder       |
| `*SO`, `*DataSO`                          | Authored configuration asset                                                                               | `Data/`              |

Formatting matrix — derived from `.editorconfig` and `.csharpierrc.json`:

| Setting      | Value                                     |
| :----------- | :---------------------------------------- |
| Indentation  | 4 spaces (`.cs`), 2 spaces (`.asmdef`)    |
| Line width   | 160                                       |
| Braces       | Allman, always present                    |
| Line endings | LF, final newline, no trailing whitespace |
| Namespaces   | Block-scoped, usings outside              |
| `var`        | Only when the type is apparent            |
