---
description: "Use when designing systems or writing Unity C# code that applies architectural patterns. Covers Observer, State, Template Method, Singleton, Service Locator, Composition, Factory, Command, and Strategy."
applyTo: "Assets/Scripts/**/*.cs"
---

# Design Patterns for Unity

## 1. Overview

This document outlines structural, behavioral, and architectural patterns approved for use in the codebase. Adhering to these patterns ensures loose coupling, testability, and consistency.

## 2. Cross-References

- **Code Style** → [unity-code-style.instructions.md](unity-code-style.instructions.md) (Align naming and event subscription syntax with these patterns)
- **Performance Optimization** → [unity-performance-optimization.instructions.md](unity-performance-optimization.instructions.md) (Minimize allocations when implementing patterns)
- **UI Toolkit** → [unity-ui-toolkit.instructions.md](unity-ui-toolkit.instructions.md) (Apply Template Method and MVP patterns in UI layouts)

## 3. Core Rules

### SOLID Principles

- **Single Responsibility (SRP):** Restrict classes to a single, focused concern. Decompose large behaviors into smaller components.
- **Open/Closed (OCP):** Author code that is open for extension but closed for modification. Leverage abstract base classes and interfaces to support behavior extensions.
- **Liskov Substitution (LSP):** Ensure derived classes can replace base classes without violating core behavior contracts or returning unexpected sentinel values.
- **Interface Segregation (ISP):** Design small, cohesive, and feature-specific interfaces. Do not force classes to implement methods they do not require.
- **Dependency Inversion (DIP):** Depend strictly on abstractions (interfaces) rather than concrete implementations. Query dependencies via the central Service Locator.

### Core Architectural Patterns

- **Observer Pattern:** Implement a centralized static event bus (`StaticGameEvents`) for global broadcasting. Limit event triggering to dedicated static invoke methods inside the bus. Subscribe in `OnEnable()` and unsubscribe in `OnDisable()`.
- **State Pattern:** Use Enum-based state machines for lightweight transitions (e.g., UI panels). Apply class-based state patterns when states have complex independent logic (e.g., AI behaviors or character controllers).
- **Template Method Pattern:** Enforce view setup lifecycles by inheriting from `UITKBaseClass`. Inheritors must implement abstract hooks for initialization and callback setup.
- **Singleton Pattern:** Limit Singleton usage to persistent, global manager components. Resolve instances via thread-safe static properties and destroy duplicate instances in `Awake()`.
- **Service Locator / Dependency Injection:** Register runtime dependencies in the `ServiceLocator` registry during subsystem startup. Unregister or clear them on object destruction to avoid memory leaks.
- **Composition over Inheritance:** Assemble GameObjects by composing multiple, highly focused Monobehaviour components (e.g., `MapTile` + `MapTileMilitary`) rather than deep inheritance hierarchies.
- **Object Pooling:** Implement `UnityEngine.Pool.ObjectPool<T>` for high-frequency runtime spawning (e.g., projectiles, visual effects).
- **Factory Pattern:** Centralize complex object instantiation and setup logic in factory classes.
- **Command Pattern:** Wrap actions inside undoable command objects (`ICommand`) to support replaying, undoing, or queuing user inputs.
- **Strategy Pattern:** Abstract algorithms or behaviors into interchangeable strategy interfaces to allow dynamic runtime configuration.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
// ❌ Violates Interface Segregation (ISP) by bundling unrelated actions
public interface IEntity
{
    void TakeDamage(int <Amount>);
    void Move(Vector3 <Position>);
    void Attack(IEntity <Target>);
}

// ❌ Direct modification of singleton assets or un-encapsulated global state
public class <BadManager> : MonoBehaviour
{
    public static <BadManager> Instance;

    private void Awake()
    {
        Instance = this; // ❌ Doesn't destroy duplicates
    }
}
```

### ✅ Do (Good)

```csharp
// ✅ Compliant with ISP: Small, focused interface contracts
public interface IDamageable
{
    void TakeDamage(int <Amount>);
}

public interface IMovable
{
    void Move(Vector3 <Position>);
}

// ✅ Correct Singleton pattern protecting lifetime instances
public class <GoodManager> : MonoBehaviour
{
    private static <GoodManager> _instance;
    public static <GoodManager> Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<<GoodManager>>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
}
```

### 🚫 Don't (Bad)

```csharp
// ❌ Violates Dependency Inversion (DIP) by coupling directly to concrete classes
public class <CombatSystem> : MonoBehaviour
{
    private ConcreteAudioPlayer _audioPlayer; // ❌ Tight coupling

    private void Awake()
    {
        _audioPlayer = new ConcreteAudioPlayer();
    }
}
```

### ✅ Do (Good)

```csharp
// ✅ Complies with DIP: references service interface resolved from ServiceLocator
public interface IAudioService
{
    void PlaySound(string <ClipName>);
}

public class <CombatSystem> : MonoBehaviour
{
    private IAudioService _audioService;

    private void Awake()
    {
        _audioService = ServiceLocator.Resolve<IAudioService>();
    }
}
```

## 5. Quick Reference & Decision Matrix

| Pattern               | Best Use Case                                                    | Location / File Suffix                 |
| :-------------------- | :--------------------------------------------------------------- | :------------------------------------- |
| Centralized Event Bus | Global decoupling of gameplay triggers (e.g. turns, selections)  | `StaticGameEvents.cs`                  |
| Enum State Machine    | UI panel visibility control, simple state branching (<10 states) | `*Controller.cs`                       |
| Class State Machine   | Complex character movement states, AI behavioral loops           | `*State.cs` & `*Controller.cs`         |
| Template Method       | Base view initialization flow (UXML, USS binding lifecycle)      | `UITKBaseClass.cs`                     |
| Service Locator       | Decoupling gameplay systems from runtime managers                | `ServiceLocator.cs`                    |
| Composition           | Customizing GameObject logic dynamically                         | Separate components (e.g., `MapTile*`) |
| Factory               | Hiding instantiation complexity and pre-configuring prefabs      | `*Factory.cs`                          |
| Command               | Implementing undo/redo actions or input replays                  | `*Command.cs`                          |
| Strategy              | Exchanging character algorithms or AI routines at runtime        | `I*Strategy.cs`                        |
