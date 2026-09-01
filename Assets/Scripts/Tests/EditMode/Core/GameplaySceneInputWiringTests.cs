using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

        // Every scene a player can be dropped into. A gameplay scene added later inherits all three checks by
        // being listed here, which is the point — the defect this fixture exists for is one a brand-new scene
        // reproduces most easily.
        private static readonly string[] _gameplayScenePaths = { MatchPveScenePath, MatchPvpScenePath };

        [Test]
        public void GameplayScene_EveryAuthoredScene_CarriesExactlyOneEventSystem([ValueSource(nameof(_gameplayScenePaths))] string scenePath)
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
        public void GameplayScene_EveryAuthoredScene_DrivesItsEventSystemWithTheInputSystemModule([ValueSource(nameof(_gameplayScenePaths))] string scenePath)
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
        public void GameplayScene_EveryAuthoredScene_ResolvesThePointAndClickActionsItsModuleNeeds([ValueSource(nameof(_gameplayScenePaths))] string scenePath)
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
        public void GameplayScene_EveryAuthoredScene_CarriesAUIDocumentWithMarkupAssigned([ValueSource(nameof(_gameplayScenePaths))] string scenePath)
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
            var found = new System.Collections.Generic.List<T>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found.ToArray();
        }
    }
}
