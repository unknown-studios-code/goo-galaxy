using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Input.Controllers;
using GooGalaxy.Runtime.Input.Presenters;
using GooGalaxy.Runtime.Input.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Core
{
    /// <remarks>
    /// <para>
    /// <b>Named for the scenes rather than for a type, and deliberately so.</b> Rule 2 in unity-testing.md asks
    /// for a <c>&lt;TypeUnderTest&gt;Tests</c> fixture and scopes its flow-named exception to PlayMode, but the
    /// subject here is a property of two authored assets that no single type owns: whether a shipped gameplay
    /// scene can receive input at all. The same tension already exists elsewhere in this suite and is worth one
    /// decision in the rule file rather than a silent exception per fixture.
    /// </para>
    /// <para>
    /// <b>Why this class exists.</b> Both gameplay scenes once shipped with no <c>EventSystem</c>. In Unity 6 a
    /// runtime UI Toolkit panel receives pointer events through the interoperability bridge that
    /// <see cref="EventSystem" /> owns, so without one no <c>PointerDownEvent</c> reaches any element: the HUD
    /// rendered perfectly and every card in the hand was inert. The whole PlayMode suite passed throughout,
    /// because every other fixture builds its own GameObjects and none of them ever opens a shipped scene.
    /// </para>
    /// <para>
    /// <b>Scenes are discovered, not listed.</b> <c>MatchInput</c> — carrying <see cref="PointerInputView" />,
    /// <see cref="TargetHighlightPresenter" /> and <see cref="MatchInputController" /> — moved out of both
    /// gameplay scenes and into <c>MatchRoot.prefab</c>, so a scene now earns the input checks below by
    /// referencing that prefab rather than by being named in a hardcoded array. A gameplay scene added later
    /// inherits every check here the moment it drags the prefab in, which is the defect class this fixture
    /// exists to catch: one a brand-new scene reproduces most easily by omitting a piece of wiring nobody
    /// remembered to add by hand.
    /// </para>
    /// <para>
    /// <b>EditMode, not PlayMode, and that is the point.</b> The question is static — does this asset contain
    /// these components — so it needs no frames and no running match. Opening these scenes in PlayMode wakes a
    /// real <c>GameLifetimeScope</c> over a real <c>MatchController</c>, whose <c>Start</c> auto-starts a match
    /// and publishes onto the static <c>MatchEvents</c> bus that every other PlayMode fixture shares; that
    /// leaks an AI think loop into unrelated tests. In EditMode no lifecycle callback runs at all, so the whole
    /// hazard is absent rather than worked around. Rule 6 in unity-testing.md forbids depending on authored
    /// assets <i>unless the authored asset is what is under test</i>, which is exactly the case here.
    /// </para>
    /// <para>
    /// Each test leaves the editor as it found it: a scene already open is inspected in place and left open, and
    /// one this fixture opened is closed again.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class GameplaySceneInputWiringTests
    {
        private const string MatchPveScenePath = "Assets/Scenes/Gameplay/MatchPVE.unity";

        private const string MatchPvpScenePath = "Assets/Scenes/Gameplay/MatchPVP.unity";

        private const string GameplayScenesFolder = "Assets/Scenes";

        private const string MatchRootPrefabPath = "Assets/Prefabs/Match/MatchRoot.prefab";

        [Test]
        public void DiscoverGameplayScenePaths_Always_IsNotEmpty()
        {
            // GIVEN

            // WHEN
            string[] discovered = DiscoverGameplayScenePaths();

            // THEN — an empty ValueSource below would produce zero test cases and a silent green, so the
            // discovery mechanism itself needs a test that fails loudly instead.
            Assert.That(discovered, Is.Not.Empty);
        }

        [Test]
        public void DiscoverGameplayScenePaths_Always_ContainsMatchPve()
        {
            // GIVEN

            // WHEN
            string[] discovered = DiscoverGameplayScenePaths();

            // THEN
            Assert.That(discovered, Does.Contain(MatchPveScenePath));
        }

        [Test]
        public void DiscoverGameplayScenePaths_Always_ContainsMatchPvp()
        {
            // GIVEN

            // WHEN
            string[] discovered = DiscoverGameplayScenePaths();

            // THEN
            Assert.That(discovered, Does.Contain(MatchPvpScenePath));
        }

        [Test]
        public void GameplayScene_EveryDiscoveredScene_CarriesExactlyOneEventSystem([ValueSource(nameof(DiscoverGameplayScenePaths))] string scenePath)
        {
            // GIVEN
            Scene scene = OpenSceneForInspection(scenePath, out bool wasAlreadyOpen);

            try
            {
                // WHEN
                EventSystem[] eventSystems = FindComponentsInScene<EventSystem>(scene);

                // THEN
                Assert.That(eventSystems, Has.Length.EqualTo(1), $"'{scenePath}' must carry exactly one EventSystem, or its HUD receives no pointer input.");
            }
            finally
            {
                CloseSceneIfOpenedHere(scene, wasAlreadyOpen);
            }
        }

        [Test]
        public void GameplayScene_EveryDiscoveredScene_DrivesItsEventSystemWithTheInputSystemModule(
            [ValueSource(nameof(DiscoverGameplayScenePaths))] string scenePath
        )
        {
            // GIVEN
            Scene scene = OpenSceneForInspection(scenePath, out bool wasAlreadyOpen);

            try
            {
                // WHEN
                InputSystemUIInputModule[] modules = FindComponentsInScene<InputSystemUIInputModule>(scene);

                // THEN — this project is new Input System only, so a StandaloneInputModule would leave the
                // EventSystem present and still deliver nothing.
                Assert.That(modules, Has.Length.EqualTo(1), $"'{scenePath}' must drive its EventSystem with an InputSystemUIInputModule.");
            }
            finally
            {
                CloseSceneIfOpenedHere(scene, wasAlreadyOpen);
            }
        }

        [Test]
        public void GameplayScene_EveryDiscoveredScene_ResolvesThePointAndClickActionsItsModuleNeeds(
            [ValueSource(nameof(DiscoverGameplayScenePaths))] string scenePath
        )
        {
            // GIVEN
            Scene scene = OpenSceneForInspection(scenePath, out bool wasAlreadyOpen);

            try
            {
                InputSystemUIInputModule[] modules = FindComponentsInScene<InputSystemUIInputModule>(scene);

                // Asserted as a precondition rather than indexed straight into: the test above owns whether the
                // module exists, and without this guard a scene missing one fails here with an
                // IndexOutOfRangeException instead of a sentence naming the scene — measured, after the module
                // was removed from a scene on purpose to prove this fixture detects it.
                Assert.That(
                    modules,
                    Has.Length.EqualTo(1),
                    $"Precondition: '{scenePath}' must carry exactly one InputSystemUIInputModule before its actions can be checked."
                );

                // WHEN
                (bool hasAsset, bool hasPoint, bool hasClick) wiring = (
                    modules[0].actionsAsset != null,
                    (modules[0].point != null) && (modules[0].point.action != null),
                    (modules[0].leftClick != null) && (modules[0].leftClick.action != null)
                );

                // THEN — a module with no actions wired is as inert as no module at all, and the Inspector
                // shows it as present either way.
                Assert.That(
                    wiring,
                    Is.EqualTo((true, true, true)),
                    $"'{scenePath}' must assign the module's actions asset and its point and left-click actions."
                );
            }
            finally
            {
                CloseSceneIfOpenedHere(scene, wasAlreadyOpen);
            }
        }

        [Test]
        public void GameplayScene_EveryDiscoveredScene_CarriesAUIDocumentWithMarkupAssigned([ValueSource(nameof(DiscoverGameplayScenePaths))] string scenePath)
        {
            // GIVEN
            Scene scene = OpenSceneForInspection(scenePath, out bool wasAlreadyOpen);

            try
            {
                UIDocument[] documents = FindComponentsInScene<UIDocument>(scene);

                // WHEN
                bool hasMarkup = (documents.Length == 1) && (documents[0].visualTreeAsset != null);

                // THEN — the other way a HUD silently renders nothing: the panel exists and has no tree in it.
                Assert.That(hasMarkup, Is.True, $"'{scenePath}' must carry exactly one UIDocument with a visual tree asset assigned.");
            }
            finally
            {
                CloseSceneIfOpenedHere(scene, wasAlreadyOpen);
            }
        }

        [Test]
        public void GameplayScene_EveryDiscoveredScene_CarriesExactlyOneOfEachInputComponent([ValueSource(nameof(DiscoverGameplayScenePaths))] string scenePath)
        {
            // GIVEN
            Scene scene = OpenSceneForInspection(scenePath, out bool wasAlreadyOpen);

            try
            {
                // WHEN — the three components MatchInput carries, now arriving through the MatchRoot.prefab
                // instance rather than being hand-placed in the scene.
                (int pointerViews, int highlightPresenters, int inputControllers) counts = (
                    FindComponentsInScene<PointerInputView>(scene).Length,
                    FindComponentsInScene<TargetHighlightPresenter>(scene).Length,
                    FindComponentsInScene<MatchInputController>(scene).Length
                );

                // THEN
                Assert.That(
                    counts,
                    Is.EqualTo((1, 1, 1)),
                    $"'{scenePath}' must carry exactly one PointerInputView, TargetHighlightPresenter and MatchInputController."
                );
            }
            finally
            {
                CloseSceneIfOpenedHere(scene, wasAlreadyOpen);
            }
        }

        [Test]
        public void MatchRootPrefab_Always_CarriesExactlyOneOfEachInputComponent()
        {
            // GIVEN
            GameObject prefabRoot = LoadMatchRootPrefab();

            // WHEN
            (int pointerViews, int highlightPresenters, int inputControllers) counts = (
                prefabRoot.GetComponentsInChildren<PointerInputView>(true).Length,
                prefabRoot.GetComponentsInChildren<TargetHighlightPresenter>(true).Length,
                prefabRoot.GetComponentsInChildren<MatchInputController>(true).Length
            );

            // THEN
            Assert.That(
                counts,
                Is.EqualTo((1, 1, 1)),
                $"'{MatchRootPrefabPath}' must carry exactly one PointerInputView, TargetHighlightPresenter and MatchInputController."
            );
        }

        [Test]
        public void MatchRootPrefab_MatchInputController_HasBoardCameraAssignedToACameraInsideThePrefab()
        {
            // GIVEN
            GameObject prefabRoot = LoadMatchRootPrefab();
            MatchInputController controller = prefabRoot.GetComponentInChildren<MatchInputController>(true);
            Assert.That(controller, Is.Not.Null, $"Precondition: '{MatchRootPrefabPath}' must carry a MatchInputController.");
            var serializedController = new SerializedObject(controller);

            // WHEN
            Object boardCamera = serializedController.FindProperty("_boardCamera").objectReferenceValue;

            // THEN
            Assert.That(
                IsComponentInsidePrefab(boardCamera, prefabRoot),
                Is.True,
                $"'{MatchRootPrefabPath}' must assign MatchInputController._boardCamera to a camera inside the prefab."
            );
        }

        [Test]
        public void MatchRootPrefab_MatchInputController_HasHudDocumentAssignedToADocumentInsideThePrefab()
        {
            // GIVEN
            GameObject prefabRoot = LoadMatchRootPrefab();
            MatchInputController controller = prefabRoot.GetComponentInChildren<MatchInputController>(true);
            Assert.That(controller, Is.Not.Null, $"Precondition: '{MatchRootPrefabPath}' must carry a MatchInputController.");
            var serializedController = new SerializedObject(controller);

            // WHEN
            Object hudDocument = serializedController.FindProperty("_hudDocument").objectReferenceValue;

            // THEN
            Assert.That(
                IsComponentInsidePrefab(hudDocument, prefabRoot),
                Is.True,
                $"'{MatchRootPrefabPath}' must assign MatchInputController._hudDocument to a UIDocument inside the prefab."
            );
        }

        [Test]
        public void MatchRootPrefab_PointerInputView_HasInputActionsAssigned()
        {
            // GIVEN
            GameObject prefabRoot = LoadMatchRootPrefab();
            PointerInputView view = prefabRoot.GetComponentInChildren<PointerInputView>(true);
            Assert.That(view, Is.Not.Null, $"Precondition: '{MatchRootPrefabPath}' must carry a PointerInputView.");
            var serializedView = new SerializedObject(view);

            // WHEN
            Object inputActions = serializedView.FindProperty("_inputActions").objectReferenceValue;

            // THEN
            Assert.That(inputActions, Is.Not.Null, $"'{MatchRootPrefabPath}' must assign PointerInputView._inputActions.");
        }

        // Every scene under Assets/Scenes that drags MatchRoot.prefab in, directly or through a nested prefab —
        // the defect this fixture exists for is one a brand-new scene reproduces most easily, and a scene earns
        // every check below by referencing the prefab rather than by being named here.
        private static string[] DiscoverGameplayScenePaths()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { GameplayScenesFolder });
            var matchingScenePaths = new List<string>();

            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);

                if (Array.IndexOf(dependencies, MatchRootPrefabPath) >= 0)
                {
                    matchingScenePaths.Add(scenePath);
                }
            }

            return matchingScenePaths.ToArray();
        }

        // Additive rather than single, so a scene the developer already had open is not closed underneath them.
        // An already-loaded scene is inspected in place: re-opening it would discard unsaved edits, and the
        // authored state on disk is not what a reader of a failure would expect to be told about.
        private static Scene OpenSceneForInspection(string scenePath, out bool wasAlreadyOpen)
        {
            Scene existing = SceneManager.GetSceneByPath(scenePath);
            wasAlreadyOpen = existing.IsValid() && existing.isLoaded;

            return wasAlreadyOpen ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        private static void CloseSceneIfOpenedHere(Scene scene, bool wasAlreadyOpen)
        {
            if (!wasAlreadyOpen && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Walks the scene's roots rather than calling FindObjectsByType, which would also answer for whatever
        // else the editor happens to have open — including the other gameplay scene, which carries the same
        // components and would make every count assertion above pass for the wrong reason.
        private static T[] FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found.ToArray();
        }

        private static GameObject LoadMatchRootPrefab()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(MatchRootPrefabPath);
            Assert.That(prefabRoot, Is.Not.Null, $"Test setup expects '{MatchRootPrefabPath}' to exist and import as a prefab.");

            return prefabRoot;
        }

        // A serialized reference is "inside the prefab" when the object it points at is a Component whose
        // transform lives under the prefab's own root — as opposed to null, or a reference into a different
        // asset or scene entirely.
        private static bool IsComponentInsidePrefab(Object candidate, GameObject prefabRoot)
        {
            return (candidate is Component component) && component.transform.IsChildOf(prefabRoot.transform);
        }
    }
}
