using System.Collections;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Energy
{
    [TestFixture]
    public class EnergyPresenterTests
    {
        private GameObject _go;
        private EnergyPresenter _presenter;

        private int _changedCount;
        private int _lastChangedPlayerId;
        private float _lastChangedEnergy;

        private int _spentCount;
        private int _lastSpentPlayerId;
        private float _lastSpentEnergy;
        private bool _lastSpentSuccess;

        private const float Tolerance = 0.0001f;

        // Mirrors the private EnergyPresenter.EnergyPublishQuantum: publication trails CurrentEnergy by up to
        // this much between flushes, so an assertion pinned to the last published value needs this tolerance
        // rather than the exact one above.
        private const float EnergyPublishQuantum = 0.05f;

        private readonly WaitForSeconds _waitForHalfSecond = new(0.5f);
        private readonly WaitForSeconds _waitForThreeTenthsSecond = new(0.3f);

        [SetUp]
        public void SetUp()
        {
            _changedCount = 0;
            _spentCount = 0;

            MatchEvents.EnergyChanged += HandleEnergyChanged;
            MatchEvents.EnergySpent += HandleEnergySpent;

            _go = new GameObject("EnergyPresenter_Test");
            _presenter = _go.AddComponent<EnergyPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.EnergyChanged -= HandleEnergyChanged;
            MatchEvents.EnergySpent -= HandleEnergySpent;

            if (_go != null)
            {
                Object.Destroy(_go);
            }
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator InitializePlayer_WithConfig_SetsStartingEnergyAndRaisesChanged()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 4.0f);

            // WHEN
            _presenter.InitializePlayer(1, config);

            // THEN
            Assert.That(_presenter.GetEnergy(1), Is.EqualTo(4.0f).Within(Tolerance));
            Assert.That(_changedCount >= 1, Is.True);
            Assert.That(_lastChangedPlayerId, Is.EqualTo(1));
            Assert.That(_lastChangedEnergy, Is.EqualTo(4.0f).Within(Tolerance));
            yield return null;
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator Update_OverElapsedFrames_AccumulatesEnergyAndRaisesChanged()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 2.0f, 1.0f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;

            // WHEN
            yield return _waitForHalfSecond;

            // THEN
            float currentEnergy = _presenter.GetEnergy(1);
            Assert.That(currentEnergy, Is.GreaterThan(1.0f));
            Assert.That(_changedCount >= 1, Is.True);
            Assert.That(_lastChangedPlayerId, Is.EqualTo(1));
            Assert.That(_lastChangedEnergy, Is.EqualTo(currentEnergy).Within(EnergyPublishQuantum));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TrySpendEnergy_WithSufficient_SucceedsDeductsAndFiresEvents()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 0.1f, 5.0f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;

            // WHEN
            SpendResult result = _presenter.TrySpendEnergy(1, 3.0f);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.Success));
            Assert.That(_presenter.GetEnergy(1), Is.EqualTo(2.0f).Within(Tolerance));
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_spentCount, Is.EqualTo(1));
            Assert.That(_lastSpentPlayerId, Is.EqualTo(1));
            Assert.That(_lastSpentEnergy, Is.EqualTo(2.0f).Within(Tolerance));
            Assert.That(_lastSpentSuccess, Is.True);
            yield return null;
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TrySpendEnergy_WithInsufficient_FailsAndFiresSpentEvent()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 0.1f, 2.0f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;

            // WHEN
            SpendResult result = _presenter.TrySpendEnergy(1, 3.0f);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.InsufficientEnergy));
            Assert.That(_presenter.GetEnergy(1), Is.EqualTo(2.0f).Within(Tolerance));
            Assert.That(_changedCount, Is.EqualTo(0));
            Assert.That(_spentCount, Is.EqualTo(1));
            Assert.That(_lastSpentPlayerId, Is.EqualTo(1));
            Assert.That(_lastSpentEnergy, Is.EqualTo(2.0f).Within(Tolerance));
            Assert.That(_lastSpentSuccess, Is.False);
            yield return null;
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator SetOvertime_WhenEnabled_DoublesRegenerationRate()
        {
            // GIVEN
            var config = new EnergyConfig(10.0f, 1.0f, 1.0f);
            _presenter.InitializePlayer(1, config);
            _presenter.SetOvertime(true);

            // WHEN
            yield return _waitForHalfSecond;

            // THEN
            float energy = _presenter.GetEnergy(1);
            Assert.That(energy, Is.GreaterThanOrEqualTo(1.5f));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator Update_WithEnergyAtCap_StopsRaisingChanged()
        {
            // GIVEN
            var config = new EnergyConfig(5.0f, 10.0f, 5.0f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;

            // WHEN
            yield return _waitForThreeTenthsSecond;

            // THEN
            Assert.That(_changedCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TryPayForMove_PaidMove_FlushesOneEnergyChangedAndOneEnergySpentOnTheNextFrame()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 10f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;
            _presenter.TryPayForMove(1, MoveType.Clone, 4);

            // WHEN
            yield return null;

            // THEN
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_spentCount, Is.EqualTo(1));
            Assert.That(_lastSpentSuccess, Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TryPayForMoveThenRefundMove_InTheSameFrame_PublishesNothingEver()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 10f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;
            _presenter.TryPayForMove(1, MoveType.Clone, 4);
            _presenter.RefundMove(1, MoveType.Clone, 4);

            // WHEN
            yield return null;
            yield return null;
            yield return null;

            // THEN
            Assert.That(_changedCount, Is.EqualTo(0));
            Assert.That(_spentCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TryPayForMove_TwoPaidMovesInOneFrame_FlushesTwoEnergySpentEventsAndOneEnergyChanged()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 10f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;
            _presenter.TryPayForMove(1, MoveType.Clone, 4);
            _presenter.TryPayForMove(1, MoveType.Jump, 4);

            // WHEN
            yield return null;

            // THEN
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_spentCount, Is.EqualTo(2));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TryPayForMove_TwoPaidMovesWithOneRefunded_FlushesExactlyOneEnergyChangedAndOneEnergySpent()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 10f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;
            _presenter.TryPayForMove(1, MoveType.Clone, 4);
            _presenter.TryPayForMove(1, MoveType.Jump, 4);
            _presenter.RefundMove(1, MoveType.Jump, 4);

            // WHEN
            yield return null;

            // THEN
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_spentCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator TryPayForMove_Unaffordable_PublishesNothingAndLeavesTheBalanceUntouched()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 1f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;
            _presenter.TryPayForMove(1, MoveType.Deploy, 5);

            // WHEN
            yield return null;

            // THEN
            Assert.That(_presenter.GetEnergy(1), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(_changedCount, Is.EqualTo(0));
            Assert.That(_spentCount, Is.EqualTo(0));
        }

        [Test]
        public void TryPayForMove_PaidMove_PublishesNothingOnTheChargedFrame()
        {
            // GIVEN
            var config = new EnergyConfig(10f, 0f, 10f);
            _presenter.InitializePlayer(1, config);
            _changedCount = 0;
            _spentCount = 0;

            // WHEN
            _presenter.TryPayForMove(1, MoveType.Clone, 4);

            // THEN
            Assert.That(_changedCount, Is.EqualTo(0));
            Assert.That(_spentCount, Is.EqualTo(0));
        }

        [Test]
        public void InitializeMatch_WithSerializedConfigs_SetsStartingEnergyForBothPlayers()
        {
            // GIVEN
            // both players come from the presenter's own serialized configuration

            // WHEN
            _presenter.InitializeMatch();

            // THEN
            Assert.That(_presenter.GetEnergy(1), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(_presenter.GetEnergy(2), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void TrySpendEnergy_ForUnknownPlayer_ReturnsInsufficientEnergyAndRaisesFailure()
        {
            // GIVEN
            // no player was ever initialized on this presenter

            // WHEN
            SpendResult result = _presenter.TrySpendEnergy(99, 1f);

            // THEN
            Assert.That(result, Is.EqualTo(SpendResult.InsufficientEnergy));
            Assert.That(_lastSpentPlayerId, Is.EqualTo(99));
            Assert.That(_lastSpentSuccess, Is.False);
        }

        [Test]
        public void GetEnergy_ForUnknownPlayer_ReturnsZero()
        {
            // GIVEN
            // no player was ever initialized on this presenter

            // WHEN
            float energy = _presenter.GetEnergy(99);

            // THEN
            Assert.That(energy, Is.EqualTo(0f));
        }

        [Test]
        public void GetState_ForUnknownPlayer_ReturnsNull()
        {
            // GIVEN
            // no player was ever initialized on this presenter

            // WHEN
            EnergyState state = _presenter.GetState(99);

            // THEN
            Assert.That(state, Is.Null);
        }

        private void HandleEnergyChanged(int playerId, float energy)
        {
            _changedCount++;
            _lastChangedPlayerId = playerId;
            _lastChangedEnergy = energy;
        }

        private void HandleEnergySpent(int playerId, float energy, bool success)
        {
            _spentCount++;
            _lastSpentPlayerId = playerId;
            _lastSpentEnergy = energy;
            _lastSpentSuccess = success;
        }
    }
}
