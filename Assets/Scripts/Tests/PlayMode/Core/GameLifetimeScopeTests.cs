using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
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

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _jumpTarget = new(2, 0);

        private GameObject _scopeGO;
        private GameObject _presenterGO;
        private GameObject _energyPresenterGO;
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
            _presenterGO = CreateGridPresenter();

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
            _presenterGO = CreateGridPresenter();
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
            _presenterGO = CreateGridPresenter();
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

        private void CreateScope()
        {
            _scopeGO = new GameObject("LifetimeScopeTest");
            _scopeGO.SetActive(false);
            _scope = _scopeGO.AddComponent<GameLifetimeScope>();
        }

        /// <summary>
        /// Creates a <c>GridPresenter</c> scene GameObject wired with a valid grid layout, mirroring the
        /// bespoke setup <see cref="GameLifetimeScope"/> expects to find in the hierarchy at build time.
        /// </summary>
        private GameObject CreateGridPresenter()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();

            var presenterGO = new GameObject("GridPresenter_DI_Test");
            presenterGO.SetActive(false);
            UnitPresenter unitPresenter = presenterGO.AddComponent<UnitPresenter>();
            unitPresenter.Construct(new FakeEnergyLedger());
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();

            presenter.SetGridLayout(gridLayout);

            presenterGO.SetActive(true);

            return presenterGO;
        }

        /// <summary>
        /// Creates an <c>EnergyPresenter</c> scene GameObject so <see cref="GameLifetimeScope"/>'s
        /// <c>RegisterComponentInHierarchy&lt;EnergyPresenter&gt;()</c> registration resolves to a known instance
        /// instead of one auto-scaffolded anonymously by <see cref="BuildContainer"/>.
        /// </summary>
        private GameObject CreateEnergyPresenter()
        {
            var energyPresenterGO = new GameObject("EnergyPresenter_DI_Test");
            _energyPresenter = energyPresenterGO.AddComponent<EnergyPresenter>();

            return energyPresenterGO;
        }

        /// <summary>
        /// Builds the scope's container by calling <see cref="LifetimeScope.Build"/> directly instead of
        /// activating the GameObject, since Unity silently swallows exceptions thrown from <c>Awake</c> and
        /// only logs them (which is what previously made a missing scene component look like a log-only
        /// failure instead of a catchable exception). Any type registered in <see cref="GameLifetimeScope"/>
        /// via <c>RegisterComponentInHierarchy</c> that isn't yet present in the scene is reported by VContainer
        /// as a <see cref="VContainerException"/> carrying the missing <see cref="System.Type"/>; this method
        /// auto-scaffolds a bare component of that type and retries, so this test does not need to change every
        /// time a new plain component-in-hierarchy registration is added. Registrations that need bespoke setup
        /// data (like <c>GridPresenter</c>'s grid layout) must still be created explicitly above, since no
        /// generic scaffolding can know what data they require.
        /// </summary>
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

        /// <summary>
        /// Permissive stand-in for the board's <see cref="IEnergyLedger"/>. This fixture exercises container
        /// wiring, never Energy pricing, so every move is simply affordable.
        /// </summary>
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

        /// <summary>
        /// Minimal jump-only capability for the DI-injection test, which only needs a legal Jump to reach the
        /// container-resolved ledger.
        /// </summary>
        private sealed class FakeMoveCapability : IMoveCapable
        {
            public bool CanClone => false;

            public bool CanJump => true;

            public bool IgnoresHazards => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;
        }
    }
}
