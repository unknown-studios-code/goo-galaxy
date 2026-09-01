using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Core.DI;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Input.Views;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Presenters;
using GooGalaxy.Runtime.UI.Views;
using GooGalaxy.Tests.PlayMode.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

namespace GooGalaxy.Tests.PlayMode.Core
{
    [TestFixture]
    public class GameLifetimeScopeTests
    {
        private const int MaxAutoScaffoldAttempts = 10;
        private const int ActingPlayerId = 1;
        private const int ActingUnitId = 1;
        private const float Tolerance = 0.0001f;
        private const float CellVisualSize = 1f;
        private const string MatchHudViewUxmlPath = "Assets/UI/UXML/MatchHudView.uxml";
        private const string MatchInputActionsPath = "Assets/Settings/Input/MatchInput.inputactions";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _jumpTarget = new(2, 0);

        private readonly List<GameObject> _autoScaffoldedGOs = new();
        private readonly List<CardDataSO> _kitCards = new();

        private GameObject _scopeGO;
        private GameObject _presenterGO;
        private GameObject _energyPresenterGO;
        private GameObject _cellPrefabGO;
        private GameObject _unitPrefabGO;
        private GameObject _hudGO;
        private GameObject _matchHudViewGO;
        private GameObject _pointerInputViewGO;
        private GameObject _boardCameraGO;
        private PanelSettings _matchHudViewPanelSettings;
        private GameLifetimeScope _scope;
        private EnergyPresenter _energyPresenter;
        private DeckPresenter _deckPresenter;
        private KitDataSO _kit;

        [TearDown]
        public void TearDown()
        {
            if (_presenterGO != null)
            {
                Object.DestroyImmediate(_presenterGO);
            }

            if (_energyPresenterGO != null)
            {
                Object.DestroyImmediate(_energyPresenterGO);
            }

            if (_cellPrefabGO != null)
            {
                Object.DestroyImmediate(_cellPrefabGO);
            }

            if (_unitPrefabGO != null)
            {
                Object.DestroyImmediate(_unitPrefabGO);
            }

            if (_hudGO != null)
            {
                Object.DestroyImmediate(_hudGO);
            }

            if (_matchHudViewGO != null)
            {
                Object.DestroyImmediate(_matchHudViewGO);
            }

            if (_matchHudViewPanelSettings != null)
            {
                Object.DestroyImmediate(_matchHudViewPanelSettings);
            }

            if (_pointerInputViewGO != null)
            {
                Object.DestroyImmediate(_pointerInputViewGO);
            }

            if (_boardCameraGO != null)
            {
                Object.DestroyImmediate(_boardCameraGO);
            }

            foreach (GameObject go in _autoScaffoldedGOs)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _autoScaffoldedGOs.Clear();

            if (_kit != null)
            {
                Object.DestroyImmediate(_kit);
            }

            foreach (CardDataSO card in _kitCards)
            {
                if (card != null)
                {
                    Object.DestroyImmediate(card);
                }
            }

            _kitCards.Clear();

            if (_scopeGO != null)
            {
                Object.DestroyImmediate(_scopeGO);
            }
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_BuildsContainer()
        {
            // GIVEN
            _presenterGO = CreateBoard();

            // WHEN
            CreateScope();
            BuildContainer();

            // THEN
            Assert.That(_scope.Container, Is.Not.Null, "VContainer container was not initialized");
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesTheSceneEnergyPresenterAsIEnergyLedger()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            _energyPresenterGO = CreateEnergyPresenter();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<IEnergyLedger>(), Is.SameAs(_energyPresenter));
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesTheSceneEnergyPresenterAsIDiscardLedger()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            _energyPresenterGO = CreateEnergyPresenter();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<IDiscardLedger>(), Is.SameAs(_energyPresenter));
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsTheResolvedLedgerIntoTheSceneUnitPresenter()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            _energyPresenterGO = CreateEnergyPresenter();
            CreateScope();
            BuildContainer();

            UnitPresenter unitPresenter = _presenterGO.GetComponent<UnitPresenter>();
            _energyPresenter.InitializePlayer(ActingPlayerId, new EnergyConfig(10f, 0f, 10f));
            var unit = new GridUnit(ActingUnitId, ActingPlayerId, new CardId("subject_alpha"), _origin);
            Assert.That(unitPresenter.RegisterUnit(unit, new FakeMoveCapability()), Is.True, "Test setup expects the unit to register.");
            var command = new MoveCommand(MoveType.Jump, _origin, _jumpTarget, ActingPlayerId, ActingUnitId);

            // WHEN
            unitPresenter.ResolveMove(command);

            // THEN
            Assert.That(
                _energyPresenter.GetEnergy(ActingPlayerId),
                Is.EqualTo(9.5f).Within(Tolerance),
                "The balance only moves on the scene's own EnergyPresenter, so this proves Build() replaced the "
                    + "manually-injected fake with the container-resolved ledger rather than leaving it in place."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsTheSceneUnitPresenterIntoTheFuseController()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();

            // WHEN
            FuseController fuseController = _scope.Container.Resolve<FuseController>();

            // THEN
            Assert.That(
                fuseController.Fuses,
                Is.Not.Null,
                "FuseController is now mandatory in any scene carrying the scope; a null Fuses means it was never injected with a UnitPresenter."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsTheBoardIntoTheSceneAbilityController()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();

            var command = new SpellCommand(ActingPlayerId, new CardId("cryo_stasis"), new[] { _origin });

            // WHEN
            SpellResult result = _scope.Container.Resolve<AbilityController>().ResolveSpell(command, null);

            // THEN
            Assert.That(
                result,
                Is.Not.EqualTo(SpellResult.BoardUnavailable),
                "BoardUnavailable is returned only while the board reference is null, so any other result proves "
                    + "the container injected the scene's presenters into the auto-scaffolded controller."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesDeckPresenterAndDeployController()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<DeckPresenter>(), Is.Not.Null);
            Assert.That(_scope.Container.Resolve<DeployController>(), Is.Not.Null);
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesTheSceneDeckPresenterAsICardCycle()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<ICardCycle>(), Is.SameAs(_deckPresenter));
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesICardCycleAndDeckPresenterAsOneInstance()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();

            // WHEN
            ICardCycle cardCycle = _scope.Container.Resolve<ICardCycle>();

            // THEN
            Assert.That(
                cardCycle,
                Is.SameAs(_scope.Container.Resolve<DeckPresenter>()),
                "AsSelf() and As<ICardCycle>() must name one component: a second instance would deal a hand the "
                    + "action resolvers never rotate, and rotate one MatchInitializer never dealt."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesCardDiscardController()
        {
            // GIVEN — CardDiscardController carries no scene requirements of its own, so a missing one is
            // expected to be auto-scaffolded by BuildContainer rather than fail this fixture.
            _presenterGO = CreateBoard();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<CardDiscardController>(), Is.Not.Null);
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsARealDeckPresenterIntoTheCardDiscardController()
        {
            // GIVEN — the phase gate would otherwise win with MatchNotInPlay before the deck is ever read, since
            // the resolved MatchController defaults to MatchPhase.None with no MatchConfigSO assigned.
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();
            CardDiscardController discardController = _scope.Container.Resolve<CardDiscardController>();
            _scope.Container.Resolve<MatchController>().SetPhaseForTests(MatchPhase.Standard);

            // WHEN
            CardDiscardResult result = discardController.TryDiscardCard(ActingPlayerId, 0);

            // THEN
            Assert.That(
                result,
                Is.EqualTo(CardDiscardResult.UnknownPlayer),
                "DeckUnavailable would mean a dependency was never injected; UnknownPlayer only proves CardDiscardController.Construct "
                    + "received a non-null DeckPresenter, IDiscardLedger and DeployController — a bare auto-scaffolded DeckPresenter "
                    + "returns UnknownPlayer too, so this does not prove the scene's real, kitted one was wired in."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsARealDeckPresenterIntoTheDeployController()
        {
            // GIVEN — the phase gate would otherwise win with MatchNotInPlay before the deck is ever read, since
            // the resolved MatchController defaults to MatchPhase.None with no MatchConfigSO assigned.
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();
            DeployController deployController = _scope.Container.Resolve<DeployController>();
            _scope.Container.Resolve<MatchController>().SetPhaseForTests(MatchPhase.Standard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new[] { _origin });

            // THEN
            Assert.That(
                result,
                Is.EqualTo(CardPlayResult.UnknownPlayer),
                "BoardUnavailable would mean a dependency was never injected; UnknownPlayer only proves DeployController.Construct "
                    + "received a non-null DeckPresenter alongside the other four dependencies BuildContainer already had to "
                    + "auto-scaffold for the container to build at all — a bare auto-scaffolded DeckPresenter returns UnknownPlayer "
                    + "too, so this does not prove the scene's real, kitted one was wired in."
            );
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesMatchController()
        {
            // GIVEN
            _presenterGO = CreateBoard();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<MatchController>(), Is.Not.Null);
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_ResolvesMatchInitializerAsThePlainClassRegistration()
        {
            // GIVEN — MatchInitializer is the project's first non-component registration: a plain class VContainer
            // constructs from the presenters registered above it, rather than a component it finds in the scene.
            _presenterGO = CreateBoard();
            CreateScope();

            // WHEN
            BuildContainer();

            // THEN
            Assert.That(_scope.Container.Resolve<MatchInitializer>(), Is.Not.Null);
        }

        [Test]
        [Timeout(10000)]
        public void Configure_WithPresentersInScene_InjectsTheMatchControllerIntoTheDeployController()
        {
            // GIVEN — MatchController pushes itself into DeployController from its own Construct rather than being
            // injected into it, because the reverse registration is a dependency cycle VContainer's
            // TypeAnalyzer.CheckCircularDependency refuses at Build(). A prior attempt registered exactly that
            // cycle and Build() threw. The cycle throw carries a non-null InvalidType, so BuildContainer's filter
            // catches it just like a missing component: it scaffolds, retries, throws again, and ends on its
            // Assert.Fail rather than surfacing the VContainerException. MatchNotInPlay — rather than
            // BoardUnavailable — is what proves DeployController actually received the pushed reference: its
            // MatchController defaults to MatchPhase.None with no MatchConfigSO assigned.
            _presenterGO = CreateBoard();
            CreateScope();
            BuildContainer();
            DeployController deployController = _scope.Container.Resolve<DeployController>();

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(ActingPlayerId, 0, new[] { _origin });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.MatchNotInPlay));
        }

        private void CreateScope()
        {
            _scopeGO = new GameObject("LifetimeScopeTest");
            _scopeGO.SetActive(false);
            _scope = _scopeGO.AddComponent<GameLifetimeScope>();
        }

        // Creates the board GameObject carrying every registration that needs authored data before it wakes:
        // GridPresenter needs a grid layout, and the two views assert on a prefab. The generic auto-scaffolding
        // in BuildContainer (below) cannot supply any of that, so they are built here.
        private GameObject CreateBoard()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();

            var presenterGO = new GameObject("GridPresenter_DI_Test");
            presenterGO.SetActive(false);
            UnitPresenter unitPresenter = presenterGO.AddComponent<UnitPresenter>();
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();
            unitPresenter.Construct(presenter, new FakeEnergyLedger());
            // Deliberately not Constructed by hand: the container injecting it is the thing under test, and an
            // arrange that calls Construct itself would leave Fuses non-null even if the registration were gone.
            presenterGO.AddComponent<FuseController>();

            presenter.SetGridLayout(gridLayout);

            _cellPrefabGO = new GameObject("CellPrefab_DI_Test");
            _cellPrefabGO.SetActive(false);
            presenterGO.AddComponent<GridView>().SetViewConfiguration(_cellPrefabGO.AddComponent<CellView>(), CellVisualSize);

            _unitPrefabGO = new GameObject("UnitPrefab_DI_Test");
            _unitPrefabGO.SetActive(false);
            presenterGO.AddComponent<UnitView>().SetViewConfiguration(_unitPrefabGO, null, null, null, CellVisualSize);

            // A kitted DeckPresenter here, rather than an auto-scaffolded bare one, is what keeps BuildContainer
            // free of DeckLogMessages.KitDataMissing on every build.
            _deckPresenter = presenterGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(BuildKit(), DeckState.DefaultHandSize);

            presenterGO.SetActive(true);

            CreateHud();
            CreateMatchHudView();
            CreatePointerInputView();
            CreateBoardCamera();

            return presenterGO;
        }

        // GameLifetimeScope registers MatchHudPresenter with RegisterComponentInHierarchy, which makes it
        // mandatory in any scene carrying the scope. Left to the generic auto-scaffolding in BuildContainer, the
        // bare component it creates has no view assigned, and its Start() would log UiLogMessages.HudViewMissing
        // as an error no test here anticipates — the same class of problem the grid layout and the kitted
        // DeckPresenter above already solve, solved the same way: author a properly formed one ahead of the
        // scaffold. Every [Test] in this fixture runs synchronously to completion and TearDown destroys the
        // scene before the player loop ever reaches a deferred Start(), which is what let the bare scaffolded
        // presenter pass silently before this existed — a latent failure rather than a genuine one.
        //
        // The double is assigned through SetViewForTests rather than a real MatchHudView, because a real one
        // brings its own UIDocument via RequireComponent and that UIDocument has no authored Source Asset here;
        // this fixture exercises container wiring, not HUD rendering, and ResolveSink treats a MatchHudView-typed
        // sink as a destroyed authored view once its serialized _view field is null, which a real component
        // assigned only through this seam would be.
        private void CreateHud()
        {
            _hudGO = new GameObject("MatchHudPresenter_DI_Test");
            _hudGO.SetActive(false);
            MatchHudPresenter presenter = _hudGO.AddComponent<MatchHudPresenter>();
            presenter.SetViewForTests(new FakeMatchHudView());
            _hudGO.SetActive(true);
        }

        // GameLifetimeScope also registers MatchHudView itself — RegisterComponentInHierarchy<MatchHudView>()
        // .AsSelf().As<IHandGestureSource>() — a separate registration from MatchHudPresenter above, which only
        // ever sees MatchHudView through the IMatchHudView seam FakeMatchHudView stands in for. This registration
        // resolves the component directly, so the fake cannot cover it. MatchHudView carries
        // RequireComponent(typeof(UIDocument)), which is exactly the bespoke-setup-data case this fixture's own
        // rule calls out for GridPresenter's grid layout: a bare auto-scaffolded UIDocument has no Source Asset,
        // so CacheElements fails its very first RequireElement lookup. A real one, with the authored UXML
        // assigned, is built here instead.
        private void CreateMatchHudView()
        {
            _matchHudViewPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // World space with a fixed size, matching MatchHudPortraitRatioTests.BuildPanelAsync — the one
            // PanelSettings configuration that fixture found settles deterministically in Play Mode, unlike
            // ScaleWithScreenSize behind a render-texture target.
            _matchHudViewPanelSettings.renderMode = PanelRenderMode.WorldSpace;
            _matchHudViewPanelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _matchHudViewPanelSettings.scale = 1f;

            _matchHudViewGO = new GameObject("MatchHudView_DI_Test");
            _matchHudViewGO.SetActive(false);

            UIDocument document = _matchHudViewGO.AddComponent<UIDocument>();
            document.panelSettings = _matchHudViewPanelSettings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = new Vector2(1080f, 1920f);

            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MatchHudViewUxmlPath);
            Assert.That(visualTreeAsset, Is.Not.Null, $"Test setup expects '{MatchHudViewUxmlPath}' to exist and import as a VisualTreeAsset.");
            document.visualTreeAsset = visualTreeAsset;

            _matchHudViewGO.AddComponent<MatchHudView>();
            _matchHudViewGO.SetActive(true);
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

            _pointerInputViewGO = new GameObject("PointerInputView_DI_Test");
            _pointerInputViewGO.SetActive(false);
            JsonUtility.FromJsonOverwrite(
                $"{{\"_inputActions\":{{\"instanceID\":{inputActions.GetInstanceID()}}}}}",
                _pointerInputViewGO.AddComponent<PointerInputView>()
            );
            _pointerInputViewGO.SetActive(true);
        }

        // GameLifetimeScope also registers MatchInputController, whose Awake logs
        // InputLogMessages.BoardCameraMissing the instant a bare auto-scaffolded instance finds no camera tagged
        // MainCamera — the scene this fixture builds has none otherwise, since board rendering is not what it
        // exercises.
        private void CreateBoardCamera()
        {
            _boardCameraGO = new GameObject("BoardCamera_DI_Test");
            _boardCameraGO.AddComponent<Camera>();
            _boardCameraGO.tag = "MainCamera";
        }

        private KitDataSO BuildKit()
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(DeckState.DefaultHandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
                card.SetAuthoredData($"kit_card_{i}", $"kit_card_{i}", "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);
                _kitCards.Add(card);
                cards[i] = card;
            }

            _kit = ScriptableObject.CreateInstance<KitDataSO>();
            _kit.SetAuthoredCards(cards);

            return _kit;
        }

        // Creates an EnergyPresenter scene GameObject so GameLifetimeScope's
        // RegisterComponentInHierarchy<EnergyPresenter>() registration resolves to a known instance instead of one
        // auto-scaffolded anonymously by BuildContainer (below).
        private GameObject CreateEnergyPresenter()
        {
            var energyPresenterGO = new GameObject("EnergyPresenter_DI_Test");
            _energyPresenter = energyPresenterGO.AddComponent<EnergyPresenter>();

            return energyPresenterGO;
        }

        // WORKAROUND: builds the scope's container by calling LifetimeScope.Build directly instead of activating the
        // GameObject, since Unity silently swallows exceptions thrown from Awake and only logs them — which is what
        // previously made a missing scene component look like a log-only failure instead of a catchable exception.
        // Any type registered in GameLifetimeScope via RegisterComponentInHierarchy that isn't yet present in the
        // scene is reported by VContainer as a VContainerException carrying the missing Type; this method
        // auto-scaffolds a bare component of that type and retries, so this test does not need to change every time a
        // new plain component-in-hierarchy registration is added. Registrations that need bespoke setup data (like
        // GridPresenter's grid layout) must still be created explicitly above, since no generic scaffolding can know
        // what data they require.
        private void BuildContainer()
        {
            for (int attempt = 0; attempt < MaxAutoScaffoldAttempts; attempt++)
            {
                try
                {
                    _scope.Build();
                    return;
                }
                catch (VContainerException ex) when (ex.InvalidType != null)
                {
                    var scaffoldGO = new GameObject($"AutoScaffolded_{ex.InvalidType.Name}");
                    scaffoldGO.AddComponent(ex.InvalidType);
                    _autoScaffoldedGOs.Add(scaffoldGO);
                }
            }

            Assert.Fail($"Container did not build after {MaxAutoScaffoldAttempts} auto-scaffold attempts.");
        }

        // Permissive on purpose: this fixture exercises container wiring, never Energy pricing, so every move is
        // affordable and no test has to seed a balance.
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

        // Jump-only on purpose: the injection test needs one legal move to reach the container-resolved ledger,
        // and a Jump needs no spawner.
        private sealed class FakeMoveCapability : IMoveCapable
        {
            public bool CanClone => false;

            public bool CanJump => true;

            public bool CanIgnoreHazards => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;
        }
    }
}
