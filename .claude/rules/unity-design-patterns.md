---
description: "Use when designing systems or writing Unity C# code that applies architectural patterns. Covers SOLID, Observer, State, Template Method, VContainer DI, Composition, Pooling, Factory, Command, and Strategy."
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/Editor/**/*.cs"
---

# Design Patterns for Unity

## 1. Overview

This document outlines structural, behavioral, and architectural patterns approved for use in the codebase. Adhering to these patterns ensures loose coupling, testability, and consistency. Apply a pattern when it solves a real problem — an unnecessary abstraction costs more than the duplication it removes.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Align naming and event subscription syntax with these patterns)
- **Class Organization** → [unity-class-organization.md](unity-class-organization.md) (Member layout for the types these patterns produce)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Minimize allocations when implementing patterns)
- **UI Toolkit** → [unity-ui-toolkit.md](unity-ui-toolkit.md) (Apply Template Method and MVP patterns in UI layouts)
- **Netcode** → [unity-netcode.md](unity-netcode.md) (Authority, ownership, and why `MatchEvents` never crosses the wire)

## 3. Core Rules

### SOLID Principles

- **Single Responsibility (SRP):** Restrict classes to a single, focused concern. Decompose large behaviors into smaller components.
- **Open/Closed (OCP):** Author code that is open for extension but closed for modification. Leverage abstract base classes and interfaces to support behavior extensions.
- **Liskov Substitution (LSP):** Ensure derived classes can replace base classes without violating core behavior contracts or returning unexpected sentinel values (a negative damage value, a `null` where a caller expects a collection).
- **Interface Segregation (ISP):** Design small, cohesive, capability-shaped interfaces (`IDamageable`, `IMoveCapable`). Do not force classes to implement methods they do not require.
- **Dependency Inversion (DIP):** Depend on abstractions, not concrete implementations, and receive them through injection rather than reaching for them.

### Dependency Injection (VContainer)

- **Container:** This project uses **VContainer** (`jp.hadashikick.vcontainer`). The composition root is `GameLifetimeScope : LifetimeScope` in `Runtime.Core`; scene- or feature-scoped children may be added as the game grows.
- **Registration:** Register everything explicitly in `Configure(IContainerBuilder)`. Use `RegisterComponentInHierarchy<T>()` for scene MonoBehaviours, `Register<TInterface, TImplementation>(Lifetime.Singleton)` for plain services, and `RegisterInstance` for pre-built configuration objects. Register against the interface whenever consumers should not see the concrete type.
- **Resolution:** Prefer constructor injection for plain C# classes and `[Inject]` method injection for MonoBehaviours, which cannot have their constructors called by Unity. Never resolve the container from inside a type (`IObjectResolver.Resolve<T>()` scattered through gameplay code is a Service Locator in disguise — it hides dependencies and defeats the testability the container exists to provide).
- **Lifetime:** Match the registration lifetime to the object's real lifetime, and let the scope dispose what it created. A `LifetimeScope` destroyed with its scene takes its singletons with it — never cache a resolved instance in a `static` field.
- **Testability:** Types built for injection take their dependencies as parameters, so EditMode tests construct them directly with fakes and never need a container.

### Core Architectural Patterns

- **Observer Pattern:** Use the centralized static event bus (`MatchEvents`) for global, cross-assembly broadcasts. Declare `event Action<T>`, expose a static `Raise<EventName>` method so only the bus can invoke the event, and reset every event in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` hook because domain reload is disabled. Subscribe in `OnEnable()` and unsubscribe in `OnDisable()`. Do not route tightly coupled collaborators through an event when a direct call or an injected interface is clearer.
- **Event payload ownership:** When an event carries a collection, state in the XML doc who owns the buffer and how long it is valid. Subscribers copy what they need to keep.
- **State Pattern:** Use an enum plus `switch` for lightweight state (UI panels, simple phases, fewer than ~10 states) — held by the `*Presenter` when it drives one screen, or by a `*Controller` when it drives gameplay flow. Move to class-based states — an abstract base with `Enter()`, `Tick()`, and `Exit()` — when each state carries substantial independent logic, such as AI or character controllers.
- **Template Method Pattern:** Define the skeleton in a base class and let subclasses fill the steps, so every implementation follows the same lifecycle. UI Toolkit views inherit from a shared base that owns the `UIDocument` wiring and exposes abstract initialize/register/unregister hooks.
- **Composition over Inheritance:** Assemble GameObjects from focused components rather than deep hierarchies. Wire sibling components with `[RequireComponent]` plus a single `GetComponent` in `Awake`, and add a new component instead of extending an existing class.
- **Object Pooling:** Use `UnityEngine.Pool.ObjectPool<T>` for frequent spawn/despawn. Pre-warm to the expected peak, reset state in `actionOnGet`/`actionOnRelease`, name the operations clearly, keep the pool internals private, and never `Destroy` a pooled instance outside teardown. Do not pool objects whose state cannot be fully reset.
- **Factory Pattern:** Centralize construction when creating an object involves more than `new` — prefab selection, data lookup, dependency wiring. Keeps call sites free of assembly logic.
- **Command Pattern:** Encapsulate an action as an object (`MoveCommand`) when it must be validated, queued, replayed, sent over the network, or undone. Pair with a `Stack<ICommand>` for undo history.
- **Strategy Pattern:** Extract interchangeable algorithms behind an interface when the variation is selected at runtime (AI behaviors, movement rules, scoring).
- **Singleton:** Avoid it. The container already provides single instances with a managed lifetime. If a genuine engine-level exception appears (an editor-only tool, a bootstrap that must exist before any scope), guard `Awake` against duplicates, expose the instance through a static property, use `DontDestroyOnLoad` only when it must survive scene loads, clear the static reference in `OnDestroy`, and reset it in the subsystem-registration hook.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
// ❌ Violates Interface Segregation by bundling unrelated capabilities
public interface IEntity
{
    void TakeDamage(int amount);
    void Move(Vector3 position);
    void Attack(IEntity target);
}

// ❌ Service Locator / ambient singleton: dependencies are invisible and untestable
public class <BadCombat> : MonoBehaviour
{
    private void Awake()
    {
        _audio = ServiceLocator.Resolve<IAudioService>();
        _board = GameManager.Instance.Board;
    }
}
```

### ✅ Do (Good)

```csharp
// ✅ Small, capability-shaped contracts
public interface IDamageable
{
    void TakeDamage(int amount);
}

public interface IAudioService
{
    void PlaySound(CardId clipId);
}

// ✅ Composition root: everything the game needs is registered in one readable place
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IAudioService, UnityAudioService>(Lifetime.Singleton);
        builder.RegisterComponentInHierarchy<GridPresenter>().AsSelf();
    }
}

// ✅ Plain C# service: constructor injection, trivially testable with a fake
public class <CombatService>
{
    private readonly IAudioService _audioService;

    public <CombatService>(IAudioService audioService)
    {
        _audioService = audioService;
    }

    public void ApplyHit(IDamageable target, int amount)
    {
        target.TakeDamage(amount);
        _audioService.PlaySound(<ClipId>);
    }
}

// ✅ MonoBehaviour: method injection, since Unity owns the constructor
public class <CombatPresenter> : MonoBehaviour
{
    private <CombatService> _combatService;

    [Inject]
    public void Construct(<CombatService> combatService)
    {
        _combatService = combatService;
    }
}
```

### 🚫 Don't (Bad)

```csharp
public class <BadEvents> : MonoBehaviour
{
    private void OnEnable()
    {
        // ❌ Raising a bus event directly from a consumer, and subscribing with a lambda
        MatchEvents.MoveExecuted += (command, coords) => Redraw(coords);
    }

    private void ApplyMove(MoveCommand command)
    {
        _lastAffected = _affectedCoordinates; // ❌ Retains a buffer the publisher still owns
    }
}
```

### ✅ Do (Good)

```csharp
public class <GoodEvents> : MonoBehaviour
{
    private readonly List<HexCoordinates> _affectedCopy = new(16);

    private void OnEnable()
    {
        MatchEvents.MoveExecuted += HandleMoveExecuted; // ✅ Named handler
    }

    private void OnDisable()
    {
        MatchEvents.MoveExecuted -= HandleMoveExecuted;
    }

    private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
    {
        // ✅ The publisher owns the list only for this callback — copy before storing
        _affectedCopy.Clear();
        for (int i = 0; i < affectedCoordinates.Count; i++)
        {
            _affectedCopy.Add(affectedCoordinates[i]);
        }
    }
}
```

## 5. Quick Reference & Decision Matrix

| Pattern               | Best Use Case                                               | Location / File Suffix                                           |
| :-------------------- | :---------------------------------------------------------- | :--------------------------------------------------------------- |
| Dependency Injection  | Every cross-type dependency                                 | `Runtime/Core/DI/GameLifetimeScope.cs`                           |
| Centralized Event Bus | Global decoupling of gameplay facts (moves, phases, energy) | `Shared/Events/MatchEvents.cs`                                   |
| Stateless Service     | Pure rules and calculations shared by presenters            | `{Feature}/Services/*Validator                                   | *Resolver` |
| Enum State Machine    | Simple branching under ~10 states — panels, match phases    | `*Presenter.cs` for a screen, `*Controller.cs` for gameplay flow |
| Class State Machine   | Complex character movement states, AI behavioral loops      | `*State.cs` + `*StateMachine.cs`                                 |
| Template Method       | Base view initialization flow (UXML, USS binding lifecycle) | UI base class in the owning assembly                             |
| Composition           | Customizing GameObject logic dynamically                    | Separate components on one GameObject                            |
| Object Pooling        | Projectiles, VFX, tile highlights, list item views          | Pool owned by the spawning presenter                             |
| Factory               | Hiding instantiation complexity and pre-configuring prefabs | `*Factory.cs`                                                    |
| Command               | Validated, replayable, or networked actions                 | `Shared/Commands/*Command.cs`                                    |
| Strategy              | Exchanging algorithms or AI routines at runtime             | `I*Strategy.cs`                                                  |
| Singleton             | Avoid — use the container                                   | —                                                                |
