---
description: "Use when writing or reviewing Unity tests. Covers EditMode vs PlayMode, naming and GIVEN-WHEN-THEN structure, the NUnit constraint model, test doubles, determinism, cleanup, and assembly wiring."
paths:
  - "Assets/Scripts/Tests/**/*.cs"
---

# Unity Testing

## 1. Overview

A test exists to fail for exactly one reason, and to say which reason in its name. This document defines how tests are written, named, structured, and isolated in this project.

**Tests are executed only through the open Editor**, with `run_tests` and `test_status` from [unity-editor-automation.md](unity-editor-automation.md) — never with `Unity.exe -batchmode`, and never by handing the list to the user instead of running it. Write them, name them, run them, and report what failed with the reason. A suite that was written but never run is a guess about whether the change works. CI runs both suites on every PR.

## 2. Cross-References

- **Editor Automation** → [unity-editor-automation.md](unity-editor-automation.md) (`run_tests` and `test_status` — how the suites this file defines are actually executed)
- **Code Style** → [unity-code-style.md](unity-code-style.md) (Naming, braces, and formatting apply to test code unchanged)
- **Class Organization** → [unity-class-organization.md](unity-class-organization.md) (Fixture layout follows the same member order)
- **Code Documentation** → [unity-code-documentation.md](unity-code-documentation.md) (GIVEN-WHEN-THEN is the documentation of a test)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (Injectable types are the ones that are testable)
- **Project Configuration** → [unity-project-configuration.md](unity-project-configuration.md) (Disabled domain reload is why static state must be reset)
- **Netcode** → [unity-netcode.md](unity-netcode.md) (Session and replication flows belong to PlayMode)

## 3. Core Rules

- **Rule 1 (Choose the Cheapest Suite):** Default to EditMode. Anything that runs without the engine loop — Models, `Services/` validators and resolvers, hex math, value types, data validation on a `ScriptableObject` — belongs in `GooGalaxy.Tests.EditMode`. Escalate to `GooGalaxy.Tests.PlayMode` only when the behavior genuinely needs frames, scene lifecycle, physics steps, UI panels, coroutines, or networking. An EditMode test that loads a scene, touches the file system, or waits on time is in the wrong suite. Keep each EditMode test in the millisecond range: it is the loop developers run constantly, and a slow suite stops being run.
- **Rule 2 (Naming & Structure):** One `[TestFixture]` per type under test, in a file named `<TypeUnderTest>Tests.cs`, in a namespace mirroring the runtime folder. Name every test `MethodUnderTest_Scenario_ExpectedOutcome`. Partition the body with literal `// GIVEN`, `// WHEN`, and `// THEN` comments, in that order, with the act step as a single statement.
- **Rule 3 (One Behavior Per Test):** Assert one behavior. When the same behavior must hold for many inputs, use `[TestCase]`, `[TestCaseSource]`, or `[Values]` instead of a loop or a chain of asserts — a parameterized failure names the offending input, a loop does not. When one logical outcome spans several fields of a value, assert the value as a whole with a single `Is.EqualTo(expected)` where the type has structural equality, or list the field assertions in sequence — Unity ships a custom NUnit based on 3.5, so `Assert.Multiple` does not exist here. Keep logic out of the test body entirely — no `if`, no `switch`, no arithmetic that re-derives the expected value from the production formula. Write the expectation as a literal, or the test only proves the code agrees with itself.
- **Rule 4 (Constraint Assertions):** Write assertions in the NUnit constraint model — `Assert.That(actual, Is.EqualTo(expected))`, `Is.True`, `Is.Null`, `Has.Count.EqualTo(n)`, `Does.Contain(x)`. The classic `Assert.AreEqual` model is not used in this project: the constraint model composes, reads as a sentence, and produces a far better failure message. Compare floats with an explicit tolerance (`Is.EqualTo(1.5f).Within(0.001f)`), never exactly. Assert exceptions with `Assert.Throws<T>` and assert the message only when the message is the contract. Stay inside the NUnit 3.5 surface that `com.unity.ext.nunit` provides: constraints added in later NUnit versions do not compile in this project.
- **Rule 5 (Test the Surface, Not the Implementation):** Exercise the public or `internal` surface. Do not reflect into private fields or invoke private methods with `BindingFlags.NonPublic` — a rename then breaks the test at runtime instead of at compile time, and the test starts asserting mechanics instead of behavior. When a type has no seam, create one: expose an `internal` factory, constructor, or setter through the existing `InternalsVisibleTo`, populate serialized data with `JsonUtility.FromJsonOverwrite`, or drive an authored asset through `SerializedObject` so Unity's own serialization (and `OnValidate`) runs. A type that is hard to set up is telling you it has too many responsibilities.
- **Rule 6 (Build Fixtures in Code):** Construct the world the test needs inside the test — `new`, `ScriptableObject.CreateInstance<T>()`, or a builder in the test assembly. Share setup through a base fixture only when several fixtures genuinely need the same world, and keep each test readable on its own — a base class that no fixture inherits is dead weight. Do not depend on committed scenes or authored assets, and do not use `Resources.Load`, unless the authored asset itself is what is under test.
- **Rule 7 (Determinism):** A test must produce the same result on any machine, in any order, at any time. No wall-clock reads (`DateTime.Now`, `Time.realtimeSinceStartup`), no dependence on frame rate or machine speed, no unseeded randomness — seed it (`new System.Random(1234)`, `UnityEngine.Random.InitState(1234)`) and state the seed. No test may depend on another test having run first, and no test may leave state that changes another's outcome.
- **Rule 8 (Static State & Domain Reload):** Domain reload is disabled, so statics survive between tests and between play sessions. Any test that subscribes to `MatchEvents`, mutates a static field, or registers a singleton must undo it in `[TearDown]` — unsubscribe every handler and call the type's reset. A suite that passes in isolation and fails in a full run is almost always this.
- **Rule 9 (Cleanup):** Destroy everything the test created: `Object.DestroyImmediate` in EditMode, `Object.Destroy` in PlayMode, including `ScriptableObject` instances. Dispose native collections and pooled objects. Write `[TearDown]` to be null-safe, because arrange may have failed halfway.
- **Rule 10 (Test Doubles):** Depend on interfaces and hand-write fakes in the test assembly — a ten-line fake beats a mocking framework, and none is installed. Name them `Fake<Interface>` for working stand-ins and `Stub<Interface>` for canned responses, and keep their recorded state public so the test can assert on it. Never build a VContainer scope in an EditMode test: types designed for injection accept their dependencies directly. Resolve through a real `LifetimeScope` only in PlayMode, and only when the container wiring itself is the subject.
- **Rule 11 (PlayMode Specifics):** Use `[UnityTest] IEnumerator` with `yield return null` to advance exactly the frames the behavior needs, and `[UnitySetUp]`/`[UnityTearDown]` when setup itself must yield. Build the scene in code. Do not sleep with `WaitForSeconds` — poll the condition with a bounded frame budget and fail with a message that says what never happened. Put `[Timeout(ms)]` on anything that could hang.
- **Rule 12 (Expected Logs and Errors):** A `Debug.LogError` or exception logged during a test fails it. When the error is the expected behavior, declare it with `LogAssert.Expect(LogType.Error, <Messages>.Constant)` before the act step, using the same `const` the production code logs. Use `LogAssert.NoUnexpectedReceived()` when silence is part of the contract.
- **Rule 13 (What Not to Test):** Do not test the engine, third-party packages, private helpers, or property pass-throughs. Do test boundaries, invalid input, and the exact edge that broke: every bug fix ships with a regression test named after the defect it prevents.
- **Rule 14 (Assembly Wiring):** Test assemblies keep `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"autoReferenced": false`, and `nunit.framework.dll` in `precompiledReferences`; EditMode also keeps `"includePlatforms": ["Editor"]`. Add the assembly under test to `references` and expose its internals with `[assembly: InternalsVisibleTo("GooGalaxy.Tests.EditMode")]` in that assembly's `AssemblyInfo.cs` — never by widening a member to `public` for a test. State every `.asmdef` change in the handoff, since the file is edited but the recompile happens in the editor.

- **Rule 15 (Flaky and Disabled Tests):** An intermittent failure is a defect in the test or in the code, never noise. Fix it or delete it the day it appears — a suite people re-run until it turns green has stopped being a signal. Never comment a test out, and never leave `[Ignore]` without a tracker ID and the condition for re-enabling it (`[Ignore("GOOM-42: pending the deterministic tick")]`).

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
[TestFixture]
public class <BadTests>
{
    [Test]
    public void TestGrid() // ❌ Name says nothing about scenario or expectation
    {
        // ❌ Reflection into private state: a rename breaks this at runtime, silently
        FieldInfo radiusField = typeof(GridLayoutSO).GetField("_gridRadius", BindingFlags.NonPublic | BindingFlags.Instance);
        var layout = ScriptableObject.CreateInstance<GridLayoutSO>();
        radiusField.SetValue(layout, 4);

        // ❌ Several behaviors in one test: the first failure hides the rest
        Assert.AreEqual(4, layout.GridRadius);
        Assert.IsTrue(layout.IsValid);

        // ❌ Loop hides which input failed
        foreach (int radius in new[] { 1, 2, 3 })
        {
            Assert.IsTrue(<BuildGrid>(radius).IsValid);
        }

        // ❌ Exact float comparison, unseeded randomness, and no cleanup
        Assert.AreEqual(0.3f, <Compute>(UnityEngine.Random.value));
    }
}
```

### ✅ Do (Good)

```csharp
[TestFixture]
public class <GoodTests>
{
    private <Type>DataSO _config;

    [SetUp]
    public void SetUp()
    {
        // ✅ Fixture built in code, populated through an internal seam
        _config = ScriptableObject.CreateInstance<<Type>DataSO>();
        _config.SetGridRadiusForTests(4);
    }

    [TearDown]
    public void TearDown()
    {
        // ✅ Null-safe teardown; arrange may have failed
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
        }

        MatchEvents.ResetEvents();
    }

    [Test]
    public void ValidateClone_TargetOccupied_ReturnsTargetBlocked()
    {
        // GIVEN
        IHexGrid grid = <BuildGridWithOccupiedNeighbor>();

        // WHEN
        MoveResult result = <MovementValidator>.ValidateClone(grid, <CloneCommand>);

        // THEN
        Assert.That(result, Is.EqualTo(MoveResult.TargetBlocked));
    }

    [TestCase(0, ExpectedResult = 1)]
    [TestCase(1, ExpectedResult = 7)]
    [TestCase(2, ExpectedResult = 19)]
    public int CellCount_ForRadius_MatchesHexFormula(int radius)
    {
        // GIVEN / WHEN / THEN — a parameterized failure names the offending input
        return <HexMathUtils>.CountCells(radius);
    }

    [Test]
    public void Regenerate_PerSecondRate_AccumulatesWithinTolerance()
    {
        // GIVEN
        var state = new EnergyState(startingEnergy: 0f);

        // WHEN
        <EnergyRegenerator>.Tick(state, deltaTime: 0.5f);

        // THEN
        Assert.That(state.Current, Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void Initialize_MissingLayout_LogsErrorAndKeepsGridEmpty()
    {
        // GIVEN
        LogAssert.Expect(LogType.Error, BoardLogMessages.GridLayoutConfigurationMissing);

        // WHEN
        var grid = <BuildGridWithoutLayout>();

        // THEN
        Assert.That(grid.Cells, Is.Empty);
    }
}

// ✅ Hand-written fake instead of a mocking framework
internal sealed class Fake<IAudioService> : IAudioService
{
    public List<CardId> PlayedClips { get; } = new();

    public void PlaySound(CardId clipId)
    {
        PlayedClips.Add(clipId);
    }
}
```

### ✅ Do (Good) — PlayMode

```csharp
[TestFixture]
public class <GoodViewTests>
{
    private GameObject _gameObject;
    private <Type>View _view;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject(nameof(<Type>View));
        _view = _gameObject.AddComponent<<Type>View>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_gameObject != null)
        {
            Object.Destroy(_gameObject);
        }
    }

    [UnityTest]
    [Timeout(5000)]
    public IEnumerator SetHighlightState_Enabled_AppliesHighlightWithinOneFrame()
    {
        // GIVEN
        _view.InitializeCell(new HexCoordinates(0, 0));

        // WHEN
        _view.SetHighlightState(true);
        yield return null; // ✅ Advance exactly the frame the behavior needs

        // THEN
        Assert.That(_view.IsHighlighted, Is.True);
    }
}
```

## 5. Quick Reference & Decision Matrix

| Subject under test                                  | Suite    | Shape                                       |
| :-------------------------------------------------- | :------- | :------------------------------------------ |
| Models, value types, hex math, `Services/` rules    | EditMode | `[Test]`                                    |
| ScriptableObject data validation                    | EditMode | `[Test]` + `CreateInstance` + internal seam |
| Presenter logic with injected fakes                 | EditMode | `[Test]`                                    |
| MonoBehaviour lifecycle, Views, UI panels           | PlayMode | `[UnityTest] IEnumerator`                   |
| Physics, animation, frame-dependent behavior        | PlayMode | `[UnityTest]` + `yield return null`         |
| Container wiring, scene bootstrap, NGO session flow | PlayMode | `[UnityTest]` + real `LifetimeScope`        |

| Need                          | Use                                                  | Not                                     |
| :---------------------------- | :--------------------------------------------------- | :-------------------------------------- |
| Equality                      | `Assert.That(actual, Is.EqualTo(expected))`          | `Assert.AreEqual`                       |
| Float equality                | `Is.EqualTo(x).Within(tolerance)`                    | Exact comparison                        |
| Collection contents           | `Has.Count.EqualTo(n)`, `Does.Contain(item)`         | Manual loop with asserts                |
| Several fields, one outcome   | One `Is.EqualTo(expectedValue)` over the whole value | `Assert.Multiple` (absent in NUnit 3.5) |
| Same behavior, many inputs    | `[TestCase]` / `[TestCaseSource]`                    | `foreach` inside one test               |
| Exception is the contract     | `Assert.Throws<T>`                                   | `try`/`catch` with a flag               |
| Error log is the contract     | `LogAssert.Expect(LogType.Error, Messages.Constant)` | Letting the test fail on the log        |
| Access to non-public behavior | `internal` + `InternalsVisibleTo`                    | `BindingFlags.NonPublic`                |
| A dependency                  | Hand-written `Fake*`/`Stub*`                         | Mocking framework, live container       |
