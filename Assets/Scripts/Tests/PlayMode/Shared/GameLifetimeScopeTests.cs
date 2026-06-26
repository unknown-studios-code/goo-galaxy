using GooGalaxy.Runtime.Shared.Services;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Tests.PlayMode
{
    [TestFixture]
    public class GameLifetimeScopeTests
    {
        private GameObject _go;
        private GameLifetimeScope _scope;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Configure_BuildsContainerSuccessfully()
        {
            // GIVEN
            _go = new GameObject("LifetimeScopeTest");
            _go.SetActive(false);
            _scope = _go.AddComponent<GameLifetimeScope>();

            // WHEN
            _go.SetActive(true);

            // THEN
            IObjectResolver container = _scope.Container;
            Assert.IsNotNull(container, "VContainer container was not initialized");
        }
    }
}
