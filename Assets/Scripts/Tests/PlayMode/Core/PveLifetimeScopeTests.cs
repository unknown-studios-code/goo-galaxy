using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Controllers;
using GooGalaxy.Runtime.AI.Data;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Core.DI;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Input.Views;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Presenters;
using GooGalaxy.Runtime.UI.Views;
using GooGalaxy.Tests.PlayMode.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Core
{
    [TestFixture]
    public class PveLifetimeScopeTests
    {
        private const int MaxAutoScaffoldAttempts = 10;
        private const int PanelSettleFrameBudget = 10;
        private const int HumanPlayerId = 1;
        private const int MachinePlayerId = 2;
        private const int MatchSeed = 12345;
        private const float CellVisualSize = 1f;
        private const float ThinkSeconds = 1.5f;
        private const float EnergyCeilingThreshold = 8f;
        private const string MatchHudViewUxmlPath = "Assets/UI/UXML/MatchHudView.uxml";
        private const string MatchInputActionsPath = "Assets/Settings/Input/MatchInput.inputactions";

        private readonly List<Object> _spawned = new();

        private GameObject _cellPrefabGO;
        private GameObject _unitPrefabGO;
        private GameObject _parentScopeGO;
        private GameObject _childScopeGO;
        private GameObject _aiGO;
        private GameLifetimeScope _parentScope;
        private PveLifetimeScope _childScope;
        private AiController _ai;
        private MatchHudView _matchHudView;

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            // The child first: its OnDestroy clears it out of VContainer's static waiting list, which would
            // otherwise still hold it when the next fixture builds a parent of the same type.
            if (_childScopeGO != null)
            {
                Object.DestroyImmediate(_childScopeGO);
            }

            if (_parentScopeGO != null)
            {
                Object.DestroyImmediate(_parentScopeGO);
            }

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _spawned.Clear();
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Awake_ChildBeforeParent_StillResolvesTheParentThroughTheWaitingList()
        {
            // GIVEN — both scopes sit at the same default execution order, which fixes no order between them.
            // The serialized parent type is the only mechanism that survives this: it throws
            // VContainerParentTypeReferenceNotFound, which enqueues the child until the parent finishes building.
            yield return PrepareScene();

            CreateAiController();
            CreateParentScope(isActive: false);
            CreateChildScope();
            Assert.That(_childScope.Container, Is.Null, "Test setup expects the child to have been left waiting on its unbuilt parent.");

            // WHEN
            _parentScopeGO.SetActive(true);

            // THEN
            Assert.That(_childScope.Container.Resolve<AiController>(), Is.SameAs(_ai));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Awake_ChildBeforeParent_AdoptsThatParentScope()
        {
            // GIVEN
            yield return PrepareScene();

            CreateAiController();
            CreateParentScope(isActive: false);
            CreateChildScope();

            // WHEN
            _parentScopeGO.SetActive(true);

            // THEN
            Assert.That(_childScope.Parent, Is.SameAs(_parentScope));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Configure_WithAnAiControllerInScene_ResolvesItFromTheChildScope()
        {
            // GIVEN
            yield return PrepareScene();

            CreateAiController();
            CreateParentScope(isActive: true);

            // WHEN
            CreateChildScope();

            // THEN
            Assert.That(_childScope.Container.Resolve<AiController>(), Is.SameAs(_ai));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Configure_WithAnAiControllerInScene_InheritsTheParentRegistrations()
        {
            // GIVEN — the child adds one entry and inherits every match component from the parent, which is what
            // lets the PvP scene and the PvE scene share the rest of the match.
            yield return PrepareScene();

            CreateAiController();
            CreateParentScope(isActive: true);

            // WHEN
            CreateChildScope();

            // THEN
            Assert.That(_childScope.Container.Resolve<GridPresenter>(), Is.Not.Null);
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Configure_WithAnAiControllerInScene_LeavesTheOpponentThinkingWithoutAnythingResolvingIt()
        {
            // GIVEN — every other test here reaches the controller through Container.Resolve, so none of them can
            // tell a scope that started the opponent from a test that started it by resolving one.
            yield return PrepareScene();

            CreateAiController();
            CreateParentScope(isActive: true);
            CreateChildScope();
            MatchEvents.RaiseMatchStarted(BuildMachineConfiguration());

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_ai.IsThinking, Is.True);
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Configure_WithNoChildScopeAndNoAiController_StillBuildsTheParent()
        {
            // GIVEN — registering the controller on the parent would make it mandatory in the PvP scene too, and
            // that scene's Build would then throw over an opponent it must not have.
            yield return PrepareScene();

            // WHEN
            CreateParentScope(isActive: true);

            // THEN
            Assert.That(_parentScope.Container, Is.Not.Null);
        }

        private static MatchConfiguration BuildMachineConfiguration()
        {
            return new MatchConfiguration(
                MatchSeed,
                new PlayerSlot(HumanPlayerId, PlayerControl.LocalHuman),
                new PlayerSlot(MachinePlayerId, PlayerControl.Machine),
                180f,
                3f,
                60f
            );
        }

        private IEnumerator PrepareScene()
        {
            CreateBoard();
            CreateHud();
            CreateMatchHudView();
            CreatePointerInputView();
            CreateBoardCamera();
            ScaffoldMissingComponents();

            yield return null;

            // MatchHudPresenter's own dependency (CreateHud, above) is a fake and needs no frame at all, but the
            // real MatchHudView added alongside it builds its panel asynchronously — Start() (which every
            // OnEnable in the scene, including this one's, precedes) is what calls TryInitializePanel, and this
            // fixture's single `yield return null` is only guaranteed to cover that first attempt, not the frame
            // or two the panel itself can still take to settle behind it.
            int panelSettleBudget = PanelSettleFrameBudget;

            while (!_matchHudView.IsPanelReady && panelSettleBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_matchHudView.IsPanelReady, Is.True, "Test setup expects the real MatchHudView's panel to have initialized within the settle budget.");
        }

        // Discovers and creates every component GameLifetimeScope expects to find in the scene, using a probe
        // scope that is disposed straight afterwards. The real parent is then free to build inside its own Awake,
        // where Unity would otherwise swallow the failure into a log line instead of an exception a test can see.
        private void ScaffoldMissingComponents()
        {
            var probeGO = new GameObject("ScaffoldProbe_Test");
            probeGO.SetActive(false);
            GameLifetimeScope probe = probeGO.AddComponent<GameLifetimeScope>();

            for (int attempt = 0; attempt < MaxAutoScaffoldAttempts; attempt++)
            {
                try
                {
                    probe.Build();
                    probe.DisposeCore();
                    Object.DestroyImmediate(probeGO);

                    return;
                }
                catch (VContainerException ex) when (ex.InvalidType != null)
                {
                    var scaffoldGO = new GameObject($"AutoScaffolded_{ex.InvalidType.Name}");
                    scaffoldGO.AddComponent(ex.InvalidType);
                    _spawned.Add(scaffoldGO);
                }
            }

            Object.DestroyImmediate(probeGO);
            Assert.Fail($"The scene was not complete after {MaxAutoScaffoldAttempts} auto-scaffold attempts.");
        }

        // Carries every registration that needs authored data before it wakes: GridPresenter needs a layout, the
        // two views assert on a prefab, and a kitted DeckPresenter keeps the build free of a missing-kit error.
        private void CreateBoard()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _spawned.Add(gridLayout);

            var presenterGO = new GameObject("GridPresenter_PVE_Test");
            presenterGO.SetActive(false);
            UnitPresenter unitPresenter = presenterGO.AddComponent<UnitPresenter>();
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();
            unitPresenter.Construct(presenter, new FakeEnergyLedger());
            presenterGO.AddComponent<FuseController>();
            presenter.SetGridLayout(gridLayout);

            _cellPrefabGO = new GameObject("CellPrefab_PVE_Test");
            _cellPrefabGO.SetActive(false);
            presenterGO.AddComponent<GridView>().SetViewConfiguration(_cellPrefabGO.AddComponent<CellView>(), CellVisualSize);
            _spawned.Add(_cellPrefabGO);

            _unitPrefabGO = new GameObject("UnitPrefab_PVE_Test");
            _unitPrefabGO.SetActive(false);
            presenterGO.AddComponent<UnitView>().SetViewConfiguration(_unitPrefabGO, null, null, null, CellVisualSize);
            _spawned.Add(_unitPrefabGO);

            DeckPresenter deckPresenter = presenterGO.AddComponent<DeckPresenter>();
            deckPresenter.SetKit(BuildKit(), DeckState.DefaultHandSize);

            // Created here rather than left to the scaffolding below, for the same reason the grid layout is:
            // a bare MatchController auto-starts on its first Start and reports the config it has no way to hold.
            presenterGO.AddComponent<MatchController>().SetMatchConfigForTests(null, 0, isAutoStartEnabled: false);

            presenterGO.SetActive(true);
            _spawned.Add(presenterGO);
        }

        // GameLifetimeScope now registers MatchHudPresenter with RegisterComponentInHierarchy, which makes it
        // mandatory in any scene carrying the scope. Without a view assigned, its Start() logs
        // UiLogMessages.HudViewMissing as an error the scaffold pass below never anticipates — the same class of
        // problem CreateBoard already solves for DeckPresenter's Kit, solved the same way: author a properly
        // formed one ahead of the scaffold rather than let the probe auto-create a bare component.
        //
        // The double is assigned through SetViewForTests rather than a real MatchHudView, because a real one
        // brings its own UIDocument via RequireComponent and that UIDocument has no authored Source Asset here —
        // this fixture exercises container wiring, not HUD rendering, and depending on a committed UXML asset
        // just to keep the presenter quiet would violate the same rule CreateBoard already follows for its own
        // ScriptableObjects. ResolveSink also treats a MatchHudView-typed sink as a destroyed authored view once
        // its serialized _view field is null, which a real component assigned only through this seam would be.
        private void CreateHud()
        {
            var hudGO = new GameObject("MatchHudPresenter_PVE_Test");
            hudGO.SetActive(false);
            MatchHudPresenter presenter = hudGO.AddComponent<MatchHudPresenter>();
            presenter.SetViewForTests(new FakeMatchHudView());
            hudGO.SetActive(true);
            _spawned.Add(hudGO);
        }

        // GameLifetimeScope also registers MatchHudView itself — RegisterComponentInHierarchy<MatchHudView>()
        // .AsSelf().As<IHandGestureSource>() — a separate registration from MatchHudPresenter above, which only
        // ever sees MatchHudView through the IMatchHudView seam FakeMatchHudView stands in for. This registration
        // resolves the component directly, so the fake cannot cover it. MatchHudView carries
        // RequireComponent(typeof(UIDocument)), which is exactly the bespoke-setup-data case CreateBoard already
        // calls out for GridPresenter's grid layout: a bare auto-scaffolded UIDocument has no Source Asset, so
        // CacheElements fails its very first RequireElement lookup. A real one, with the authored UXML assigned,
        // is built here instead — PrepareScene polls its IsPanelReady before any test proceeds.
        private void CreateMatchHudView()
        {
            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            // World space with a fixed size, matching MatchHudPortraitRatioTests.BuildPanelAsync — the one
            // PanelSettings configuration that fixture found settles deterministically in Play Mode, unlike
            // ScaleWithScreenSize behind a render-texture target.
            panelSettings.renderMode = PanelRenderMode.WorldSpace;
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = 1f;
            _spawned.Add(panelSettings);

            var documentGO = new GameObject("MatchHudView_PVE_Test");
            documentGO.SetActive(false);

            UIDocument document = documentGO.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = new Vector2(1080f, 1920f);

            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MatchHudViewUxmlPath);
            Assert.That(visualTreeAsset, Is.Not.Null, $"Test setup expects '{MatchHudViewUxmlPath}' to exist and import as a VisualTreeAsset.");
            document.visualTreeAsset = visualTreeAsset;

            _matchHudView = documentGO.AddComponent<MatchHudView>();
            documentGO.SetActive(true);
            _spawned.Add(documentGO);
        }

        // GameLifetimeScope also registers PointerInputView — RegisterComponentInHierarchy<PointerInputView>()
        // .AsSelf().As<IPointerSource>(). Its Awake runs ResolveActions synchronously, which logs
        // InputLogMessages.PointerActionAssetMissing the instant a bare auto-scaffolded instance finds no
        // InputActionAsset assigned — the same class of bespoke-setup-data fault CreateMatchHudView solves for
        // MatchHudView, solved the same way: assign the authored asset before this fixture's own scaffold ever
        // gets the chance to build a bare one.
        private void CreatePointerInputView()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(MatchInputActionsPath);
            Assert.That(inputActions, Is.Not.Null, $"Test setup expects '{MatchInputActionsPath}' to exist and import as an InputActionAsset.");

            var pointerGO = new GameObject("PointerInputView_PVE_Test");
            pointerGO.SetActive(false);
            JsonUtility.FromJsonOverwrite(
                $"{{\"_inputActions\":{{\"instanceID\":{inputActions.GetInstanceID()}}}}}",
                pointerGO.AddComponent<PointerInputView>()
            );
            pointerGO.SetActive(true);
            _spawned.Add(pointerGO);
        }

        // GameLifetimeScope also registers MatchInputController, whose Awake logs
        // InputLogMessages.BoardCameraMissing the instant a bare auto-scaffolded instance finds no camera tagged
        // MainCamera — the scene this fixture builds has none otherwise, since board rendering is not what it
        // exercises.
        private void CreateBoardCamera()
        {
            var cameraGO = new GameObject("BoardCamera_PVE_Test");
            cameraGO.AddComponent<Camera>();
            cameraGO.tag = "MainCamera";
            _spawned.Add(cameraGO);
        }

        private KitDataSO BuildKit()
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(DeckState.DefaultHandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
                card.SetAuthoredData($"kit_card_{i}", $"kit_card_{i}", "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);
                _spawned.Add(card);
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            return kit;
        }

        // The tuning asset is written through Unity's own serialization, the same mechanism the Inspector uses,
        // because the controller exposes no setter for it and would otherwise log a configuration fault on Awake.
        private void CreateAiController()
        {
            AiConfigSO config = ScriptableObject.CreateInstance<AiConfigSO>();
            config.name = "TestAiConfig";
            config.SetAuthoredData(ThinkSeconds, ThinkSeconds, EnergyCeilingThreshold, isDiscardEnabled: true, AiConfig.DerivedSeed);
            _spawned.Add(config);

            _aiGO = new GameObject("AiController_PVE_Test");
            _aiGO.SetActive(false);
            _ai = _aiGO.AddComponent<AiController>();
            JsonUtility.FromJsonOverwrite($"{{\"_config\":{{\"instanceID\":{config.GetInstanceID()}}}}}", _ai);
            _aiGO.SetActive(true);
            _spawned.Add(_aiGO);
        }

        private void CreateParentScope(bool isActive)
        {
            _parentScopeGO = new GameObject("GameLifetimeScope_PVE_Test");
            _parentScopeGO.SetActive(false);
            _parentScope = _parentScopeGO.AddComponent<GameLifetimeScope>();
            _parentScopeGO.SetActive(isActive);
        }

        private void CreateChildScope()
        {
            _childScopeGO = new GameObject("PveLifetimeScope_Test");
            _childScopeGO.SetActive(false);
            _childScope = _childScopeGO.AddComponent<PveLifetimeScope>();
            _childScopeGO.SetActive(true);
        }

        // Permissive on purpose: this fixture exercises container wiring, never Energy pricing.
        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost) { }
        }
    }
}
