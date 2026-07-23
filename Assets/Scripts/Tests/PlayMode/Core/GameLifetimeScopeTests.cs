using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Core.DI;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Tests.PlayMode
{
    [TestFixture]
    public class GameLifetimeScopeTests
    {
        private const int MaxAutoScaffoldAttempts = 10;

        private GameObject _scopeGO;
        private GameObject _presenterGO;
        private GameLifetimeScope _scope;
        private readonly List<GameObject> _autoScaffoldedGOs = new();

        [TearDown]
        public void TearDown()
        {
            if (_presenterGO != null)
            {
                Object.DestroyImmediate(_presenterGO);
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
        public void Configure_BuildsContainerSuccessfully()
        {
            // GIVEN
            _presenterGO = CreateGridPresenter();

            // WHEN
            CreateScope();
            BuildContainer();

            // THEN
            Assert.IsNotNull(_scope.Container, "VContainer container was not initialized");
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
            presenterGO.AddComponent<UnitMovementController>();
            GridPresenter presenter = presenterGO.AddComponent<GridPresenter>();

            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            gridLayoutField.SetValue(presenter, gridLayout);

            presenterGO.SetActive(true);

            return presenterGO;
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
    }
}
