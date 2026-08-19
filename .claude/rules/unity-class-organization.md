---
description: "Use when writing or reviewing Unity C# files — classes, MonoBehaviours, ScriptableObjects, static classes, structs, records, interfaces, enums, and editor types. Covers file layout and strict member ordering."
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/Editor/**/*.cs"
---

# Unity Class Organization

## 1. Overview

This file establishes the file layout and a strict, predictable member order for every C# type in the project. A fixed order means a reviewer always knows where to look, diffs stay small, and serialized fields never drift into the middle of a class.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Naming, braces, accessibility, and formatting)
- **Code Documentation** → [unity-code-documentation.md](unity-code-documentation.md) (What deserves an XML doc or a tooltip)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Lifecycle and execution-order behavior behind the callback order)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (Layouts for the types these patterns produce)

## 3. Core Rules

### Rule 0 — File layout

1. `using` directives, outside the namespace, `System` first, no blank line between groups, none unused.
2. A single block-scoped `namespace` matching the folder path — never file-scoped.
3. One top-level type per file, with the file named after it. A small enum or delegate used only by that type may share the file, declared after it. A file that holds only assembly-level attributes (`AssemblyInfo.cs`) declares no namespace and no type; points 2 and 3 do not apply to it.
4. No `#region` blocks except to group animation or input event handlers. If a class needs regions to stay navigable, split the class.

### Rule 1 — Pure C# classes

1. Constants (`const`), then `static readonly` fields
2. Static fields
3. Instance fields (`readonly` first, then mutable)
4. Delegates
5. Events
6. Constructors, then finalizers
7. Properties, then indexers
8. Operators and conversions
9. Methods
10. Nested types (enums → interfaces → structs → records → classes)

Within fields and methods, order by accessibility: `public` → `internal` → `protected` → `private`. Static members precede instance members of the same accessibility. `override` and `partial` are not accessibility levels — order them by their declared accessibility, and keep an override immediately after the members it relates to. Explicit interface implementations go last among methods, just before nested types.

### Rule 2 — MonoBehaviour

1. Constants and `static readonly` fields (shader IDs, cached hashes)
2. Static fields
3. Serialized inspector fields, grouped with `[Header]`, each attribute on its own line above `[SerializeField]`
4. Runtime fields (`readonly` collections first, then mutable state)
5. Delegates and events, then `UnityEvent` fields
6. Properties and indexers
7. Injection entry point (`[Inject] public void Construct(...)`) when the type is container-resolved
8. Unity lifecycle callbacks, in execution order: `Awake` → `OnEnable` → `Start` → `FixedUpdate` → `Update` → `LateUpdate` → `OnDisable` → `OnDestroy`
9. Unity physics and trigger callbacks: `OnCollisionEnter/Stay/Exit`, `OnTriggerEnter/Stay/Exit`
10. Unity editor and application callbacks: `OnValidate`, `Reset`, `OnDrawGizmos`/`OnDrawGizmosSelected`, `OnApplicationPause`, `OnApplicationFocus`, `OnApplicationQuit` — editor-only ones wrapped in `#if UNITY_EDITOR`
11. Methods (public → internal → protected → private), with event handlers (`Handle*`) kept together as the **last** block of private methods, immediately before any nested type
12. Nested types

Do not declare constructors on a MonoBehaviour; Unity owns instantiation. Use `Awake` for self-initialization and `[Inject]` for dependencies.

### Rule 3 — ScriptableObject

Same order as a MonoBehaviour, minus the frame callbacks. `[CreateAssetMenu]` sits directly above the class declaration, and the only lifecycle callbacks that appear are `OnEnable`, `OnDisable`, `OnValidate`, and `Reset`. Keep authored fields serialized and private, expose them through read-only properties, and keep runtime mutation out of the asset.

### Rule 4 — Static classes

Used for event buses, constant tables, and stateless helpers. Order: constants → `static readonly` fields → static mutable fields → static events → static properties → static methods → nested types. When the class holds resettable static state, its `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset goes last among methods so it is easy to audit against the fields above it.

### Rule 5 — Structs and records

Prefer `readonly struct` for value types; implement `IEquatable<T>` and provide `==`/`!=` whenever the type is compared or used as a dictionary key, overriding `Equals` and `GetHashCode` to match. **The one standing exception is a struct Unity's serializer writes into** — an element of a `[SerializeField]` array on a `ScriptableObject` or MonoBehaviour is deserialized in place, which a `readonly struct` cannot accept, so that type carries writable fields and states the reason at its declaration (see `StartingPlacement`). The exception is the serializer, not authoring convenience: a value type nothing serializes stays `readonly`. Order: constants → `static readonly` → instance fields → constructors → properties → operators (`==`, `!=`, conversions) → methods → interface implementations (`Equals`, `GetHashCode`, `ToString`) → nested types. Positional records declare their parameters in the header and follow the same order for any additional members.

### Rule 6 — Interfaces and enums

Interfaces declare, in order: constants, static members, properties and indexers, events, then methods (instance → static → default implementations). Declare only the capability the consumer needs. Enums list values in a meaningful order — logical sequence or explicit numeric values when they are serialized or sent over the network — with `None = 0` when a neutral value exists. **A result enum has no neutral value**, because every member is a real outcome, so it opens with `Success = 0` and that is not a violation of the clause above (`MatchStartResult`, `CardPlayResult`, `CardDiscardResult`, `MovementResult`, `SpellResult`). Reach for `None = 0` where a default-constructed value would otherwise assert something false — `MatchEndReason` is the case that earns it, since `default(MatchOutcome)` would otherwise read as a real ending.

### Rule 7 — Generic, abstract, and partial types

Abstract bases declare the template first: abstract and virtual members before the concrete helpers that support them, so an implementer reads the contract at the top. Partial classes keep the primary declaration (fields, lifecycle, public surface) in the file named after the type, and each additional part groups one concern; generated or attribute-driven parts (`[UxmlElement] public partial class`) carry no hand-written state.

### Rule 8 — Editor types

`Assets/Editor/` types follow the same order with their own callbacks: `CustomEditor` implementations use `OnEnable` → `CreateInspectorGUI` → `OnInspectorGUI` → helper methods; `PropertyDrawer` uses `CreatePropertyGUI` → helpers; `EditorWindow` uses the `[MenuItem]` opener → `CreateGUI` → `OnEnable`/`OnDisable` → handlers; `AssetPostprocessor` declares its message callbacks first in pipeline order (`OnPreprocess*` → `OnPostprocess*`), then its helpers. Serialized-property lookups are cached in fields at the top, exactly like a runtime component's cached references.

### Rule 9 — Test fixtures

A `[TestFixture]` is a plain C# class and follows Rule 1, with the framework's own callbacks taking fixed positions inside it:

1. Constants (`const`), then `static readonly` fields — the coordinates, ids, durations and tolerances the tests share
2. Instance fields — the system under test and everything the fixture builds for it
3. `[OneTimeSetUp]` / `[OneTimeTearDown]` when present — fixture lifetime is wider than test lifetime, so it reads outside-in
4. `[SetUp]` / `[UnitySetUp]`, then `[TearDown]` / `[UnityTearDown]`
5. Test methods, in the order a reader would meet the behaviour: the happy path first, then boundaries, then failure modes
6. Private helpers — `static` before instance, per Rule 1
7. Nested test doubles (`Fake*`, `Stub*`) last, as Rule 1 requires of any nested type

**When a fixture covers more than one public member of its subject, group by member first:** complete one member's happy-path-through-failure sequence before starting the next member's. The member is the primary axis and severity orders within it, because a reader arrives looking for `Resolve` or `ValidateTargets`, never for "the failure tests" — and a fixture large enough to raise the question is exactly the one where a global severity order stops being navigable.

Setup and teardown sit above the tests because they are the fixture's own lifecycle, the same reason a MonoBehaviour's `Awake` precedes its methods. Helpers sit below the tests they serve: a reader arrives for a behaviour, not for the scaffolding. A fixture whose helpers have grown past its tests is telling you the setup belongs in a shared builder.

Accessibility follows Rule 1 unchanged, which means `[SetUp]` and `[Test]` methods are `public` — NUnit discovers them by reflection — while every helper stays private.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
using UnityEngine;
using System.Collections.Generic; // ❌ System after UnityEngine

namespace GooGalaxy.Runtime.Board; // ❌ File-scoped namespace

public class <Type> : MonoBehaviour
{
    protected void Start() => <Method>(); // ❌ Callback before fields, expression-bodied method

    [SerializeField]
    private int _value; // ❌ Serialized field declared in the middle of the class

    public int Value => _value; // ❌ Property after the callbacks it feeds

    private void <Method>() { }

    private const int MaxValue = 10; // ❌ Constant at the bottom
}
```

### ✅ Do (Good)

```csharp
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Views
{
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class <Type> : MonoBehaviour
    {
        private const int MaxHighlights = 8;

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");

        [Header("Appearance")]
        [Tooltip("Tint applied while the cell is highlighted. Alpha below 0.4 is invisible on low-tier devices.")]
        [SerializeField]
        private Color _highlightColor = new(1f, 1f, 0.5f, 1f);

        private readonly List<HexCoordinates> _highlighted = new(MaxHighlights);

        private MeshRenderer _meshRenderer;
        private bool _isHighlighted;

        public bool IsHighlighted => _isHighlighted;

        protected void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        protected void OnEnable()
        {
            MatchEvents.MoveExecuted += HandleMoveExecuted;
        }

        protected void OnDisable()
        {
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
        }

#if UNITY_EDITOR
        protected void OnValidate()
        {
            _highlightColor.a = Mathf.Max(_highlightColor.a, 0.4f);
        }
#endif

        public void SetHighlightState(bool active)
        {
            _isHighlighted = active;
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affected)
        {
            // ✅ Implementation goes here
        }
    }
}
```

## 5. Quick Reference & Decision Matrix

| Member Category                    | Position     | Ordering Detail                                                                        |
| :--------------------------------- | :----------- | :------------------------------------------------------------------------------------- |
| `const` / `static readonly`        | Top          | Before every instance member, including cached shader and hash IDs                     |
| Serialized inspector fields        | Upper        | Grouped with `[Header]`; attributes stacked on their own lines                         |
| Runtime fields                     | Upper-middle | `readonly` first, then mutable                                                         |
| Delegates, events, `UnityEvent`    | Middle       | Before properties (.NET/StyleCop order)                                                |
| Properties and indexers            | Middle       | After events, before any method                                                        |
| `[Inject] Construct`               | Middle       | First method-shaped member on a container-resolved type                                |
| Unity lifecycle callbacks          | Middle-lower | `Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy` |
| Physics / trigger callbacks        | Lower        | After lifecycle, before editor callbacks                                               |
| Editor / application callbacks     | Lower        | `OnValidate`, `Reset`, gizmos, pause/focus/quit — `#if UNITY_EDITOR`                   |
| Methods                            | Bottom       | public → internal → protected → private; static before instance                        |
| Explicit interface implementations | Bottom       | After ordinary methods, before nested types                                            |
| Nested types                       | Last         | enums → interfaces → structs → records → classes                                       |

Test fixtures — Rule 9, and the shape every fixture in `Assets/Scripts/Tests/` already follows:

| Member Category                        | Position     | Ordering Detail                                                      |
| :------------------------------------- | :----------- | :------------------------------------------------------------------- |
| `const` / `static readonly`            | Top          | Shared ids, coordinates, durations, tolerances                       |
| Instance fields                        | Upper        | The system under test and what the fixture builds for it             |
| `[OneTimeSetUp]` / `[OneTimeTearDown]` | Middle-upper | Above `[SetUp]` — fixture lifetime is wider than test lifetime       |
| `[SetUp]` / `[UnitySetUp]`             | Middle-upper | `public`; NUnit discovers it by reflection                           |
| `[TearDown]` / `[UnityTearDown]`       | Middle-upper | Immediately after setup, null-safe — arrange may have failed halfway |
| `[Test]` / `[UnityTest]`               | Middle       | Grouped by member under test; happy → boundaries → failures within   |
| Private helpers                        | Lower        | `static` before instance, per Rule 1                                 |
| Nested `Fake*` / `Stub*`               | Last         | Hand-written test doubles, per Rule 10 of the testing rules          |
