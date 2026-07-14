using System.Collections;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Energy
{
    [TestFixture]
    public class EnergyControllerTests
    {
        private GameObject _go;
        private EnergyController _controller;

        private int _changedCount;
        private int _lastChangedPlayerId;
        private float _lastChangedEnergy;

        private int _spentCount;
        private int _lastSpentPlayerId;
        private float _lastSpentEnergy;
        private bool _lastSpentSuccess;

        private readonly WaitForSeconds _waitForHalfSecond = new(0.5f);
        private readonly WaitForSeconds _waitForThreeTenthsSecond = new(0.3f);

        [SetUp]
        public void SetUp()
        {
            _changedCount = 0;
            _spentCount = 0;

            StaticGameEvents.EnergyChanged += OnEnergyChanged;
            StaticGameEvents.EnergySpent += OnEnergySpent;

            _go = new GameObject("EnergyController_Test");
            _controller = _go.AddComponent<EnergyController>();
        }

        [TearDown]
        public void TearDown()
        {
            StaticGameEvents.EnergyChanged -= OnEnergyChanged;
            StaticGameEvents.EnergySpent -= OnEnergySpent;

            if (_go != null)
            {
                Object.Destroy(_go);
            }
        }

        [UnityTest]
        public IEnumerator Initialize_SetsStartingEnergyAndFiresEvent()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 4.0f);

            // WHEN
            _controller.InitializePlayer(1, config);

            // THEN
            Assert.AreEqual(4.0f, _controller.GetEnergy(1));
            Assert.IsTrue(_changedCount >= 1);
            Assert.AreEqual(1, _lastChangedPlayerId);
            Assert.AreEqual(4.0f, _lastChangedEnergy);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Update_AccumulatesEnergyOverTimeAndFiresEvent()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 2.0f, 1.0f);
            _controller.InitializePlayer(1, config);
            _changedCount = 0;

            // WHEN
            yield return _waitForHalfSecond;

            // THEN
            float currentEnergy = _controller.GetEnergy(1);
            Assert.Greater(currentEnergy, 1.0f);
            Assert.IsTrue(_changedCount >= 1);
            Assert.AreEqual(1, _lastChangedPlayerId);
            Assert.AreEqual(currentEnergy, _lastChangedEnergy);
        }

        [UnityTest]
        public IEnumerator TrySpendEnergy_WithSufficient_SucceedsDeductsAndFiresEvents()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 0.1f, 5.0f);
            _controller.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;

            // WHEN
            SpendResult result = _controller.TrySpendEnergy(1, 3.0f);

            // THEN
            Assert.AreEqual(SpendResult.Success, result);
            Assert.AreEqual(2.0f, _controller.GetEnergy(1));
            Assert.AreEqual(1, _changedCount);
            Assert.AreEqual(1, _spentCount);
            Assert.AreEqual(1, _lastSpentPlayerId);
            Assert.AreEqual(2.0f, _lastSpentEnergy);
            Assert.IsTrue(_lastSpentSuccess);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrySpendEnergy_WithInsufficient_FailsAndFiresSpentEvent()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 0.1f, 2.0f);
            _controller.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;

            // WHEN
            SpendResult result = _controller.TrySpendEnergy(1, 3.0f);

            // THEN
            Assert.AreEqual(SpendResult.InsufficientEnergy, result);
            Assert.AreEqual(2.0f, _controller.GetEnergy(1));
            Assert.AreEqual(0, _changedCount);
            Assert.AreEqual(1, _spentCount);
            Assert.AreEqual(1, _lastSpentPlayerId);
            Assert.AreEqual(2.0f, _lastSpentEnergy);
            Assert.IsFalse(_lastSpentSuccess);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetOvertime_DoublesRegenerationSpeed()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 1.0f);
            _controller.InitializePlayer(1, config);
            _controller.SetOvertime(true);

            // WHEN
            yield return _waitForHalfSecond;

            // THEN
            float energy = _controller.GetEnergy(1);
            Assert.GreaterOrEqual(energy, 1.5f);
        }

        [UnityTest]
        public IEnumerator EnergyAtCap_DoesNotFloodChangedEvents()
        {
            // GIVEN
            var config = new EnergyConfig(5.0f, 10.0f, 5.0f);
            _controller.InitializePlayer(1, config);
            _changedCount = 0;

            // WHEN
            yield return _waitForThreeTenthsSecond;

            // THEN
            Assert.AreEqual(0, _changedCount);
        }

        private void OnEnergyChanged(int playerId, float energy)
        {
            _changedCount++;
            _lastChangedPlayerId = playerId;
            _lastChangedEnergy = energy;
        }

        private void OnEnergySpent(int playerId, float energy, bool success)
        {
            _spentCount++;
            _lastSpentPlayerId = playerId;
            _lastSpentEnergy = energy;
            _lastSpentSuccess = success;
        }
    }
}
