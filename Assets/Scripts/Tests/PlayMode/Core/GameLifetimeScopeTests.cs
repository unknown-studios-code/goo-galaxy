using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Core.DI;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Tests.PlayMode
{
    [TestFixture]
    public class GameLifetimeScopeTests
    {
        private const int MaxAutoScaffoldAttempts = 10;
        private const int ActingPlayerId = 1;
        private const int ActingUnitId = 1;
        private const float Tolerance = 0.0001f;
        private const float CellVisualSize = 1f;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _jumpTarget = new(2, 0);

        private GameObject _scopeGO;
        private GameObject _presenterGO;
        private GameObject _energyPresenterGO;
        private GameObject _cellPrefabGO;
        private GameObject _unitPrefabGO;
        private GameLifetimeScope _scope;
        private EnergyPresenter _energyPresenter;
        private readonly List<GameObject> _autoScaffoldedGOs = new();

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

            foreach (GameObject go in _autoScaffoldedGOs)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _autoScaffoldedGOs.Clear();

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

            presenterGO.SetActive(true);

            return presenterGO;
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
