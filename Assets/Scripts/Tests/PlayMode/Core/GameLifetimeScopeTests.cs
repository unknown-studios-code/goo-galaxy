using System.Reflection;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Core.DI;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.PlayMode
{
    [TestFixture]
    public class GameLifetimeScopeTests
    {
        private GameObject _scopeGO;
        private GameObject _presenterGO;
        private GameLifetimeScope _scope;

        [TearDown]
        public void TearDown()
        {
            if (_presenterGO != null)
            {
                Object.DestroyImmediate(_presenterGO);
            }

            if (_scopeGO != null)
            {
                Object.DestroyImmediate(_scopeGO);
            }
        }

        [Test]
        public void Configure_BuildsContainerSuccessfully()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();

            _presenterGO = new GameObject("GridPresenter_DI_Test");
            _presenterGO.SetActive(false);
            _presenterGO.AddComponent<UnitMovementController>();
            GridPresenter presenter = _presenterGO.AddComponent<GridPresenter>();

            FieldInfo gridLayoutField = typeof(GridPresenter).GetField("_gridLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            gridLayoutField.SetValue(presenter, gridLayout);

            _presenterGO.SetActive(true);

            _scopeGO = new GameObject("LifetimeScopeTest");
            _scopeGO.SetActive(false);
            _scope = _scopeGO.AddComponent<GameLifetimeScope>();

            // WHEN
            _scopeGO.SetActive(true);

            // THEN
            Assert.IsNotNull(_scope.Container, "VContainer container was not initialized");
        }
    }
}
