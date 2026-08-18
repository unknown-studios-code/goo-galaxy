using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Energy
{
    // Exercises the pure balance arithmetic of EnergyPresenter through the IEnergyLedger and IDiscardLedger
    // surfaces the board and the deck depend on. TryPayForMove/RefundMove and TryPayForDiscard/RefundDiscard all
    // mutate the balance synchronously and publish nothing themselves — publication is deferred to Update, which
    // needs a frame and therefore cannot be driven from EditMode. That behavior lives in
    // GooGalaxy.Tests.PlayMode.Energy.EnergyPresenterTests instead, which already owns this presenter's other
    // frame-dependent behavior (regeneration, overtime).
    [TestFixture]
    public class EnergyLedgerTests
    {
        private const int PlayerId = 1;
        private const int OtherPlayerId = 2;
        private const int UnknownPlayerId = 99;
        private const float Tolerance = 0.0001f;

        private GameObject _go;
        private EnergyPresenter _presenter;
        private IEnergyLedger _ledger;
        private IDiscardLedger _discardLedger;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EnergyLedger_Test");
            _presenter = _go.AddComponent<EnergyPresenter>();
            _ledger = _presenter;
            _discardLedger = _presenter;
        }

        [TearDown]
        public void TearDown()
        {
            // This fixture never subscribes, but InitializePlayer raises EnergyChanged synchronously on the
            // shared static bus regardless, so the reset protects whichever fixture runs next rather than
            // cleaning up a subscription of this one's own.
            MatchEvents.ResetEvents();

            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void TryPayForMove_AffordableClone_DeductsTheResolvedCost()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f));
            const int unitEnergyCost = 4;

            // WHEN
            _ledger.TryPayForMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void TryPayForMove_UnaffordableMove_LeavesTheBalanceUntouched()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 1f));
            const int unitEnergyCost = 5;

            // WHEN
            _ledger.TryPayForMove(PlayerId, MoveType.Deploy, unitEnergyCost);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TryPayForMove_UnknownPlayer_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool paid = _ledger.TryPayForMove(UnknownPlayerId, MoveType.Jump, 1);

            // THEN
            Assert.That(paid, Is.False);
        }

        [Test]
        public void TryPayForMove_ExactBalance_SucceedsAndLandsOnZero()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 2.5f));
            const int unitEnergyCost = 5;

            // WHEN
            bool paid = _ledger.TryPayForMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(paid, Is.True);
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TryPayForMove_TwoPlayersWithDifferentJumpCost_EachPricedAgainstItsOwnConfig()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 0.5f, 0.5f));
            _presenter.InitializePlayer(OtherPlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 2.0f, 0.5f));

            // WHEN
            _ledger.TryPayForMove(PlayerId, MoveType.Jump, 4);
            _ledger.TryPayForMove(OtherPlayerId, MoveType.Jump, 4);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(9.5f).Within(Tolerance));
            Assert.That(_presenter.GetEnergy(OtherPlayerId), Is.EqualTo(8.0f).Within(Tolerance));
        }

        [Test]
        public void RefundMove_AfterAPaidClone_RestoresTheExactCost()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f));
            const int unitEnergyCost = 6;
            _ledger.TryPayForMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // WHEN
            _ledger.RefundMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void RefundMove_ZeroCostCharge_WithdrawsThePendingSpend()
        {
            // GIVEN — TrySpend treats a cost of 0f as a successful charge, so TryPayForMove marks a spend pending
            // even for a free move, which CloneCostMultiplier authored at 0 makes reachable. The withdrawal must
            // therefore be unconditional: gating it on cost > 0f leaves a spend queued for a move the board
            // rolled back, and the next flush announces it.
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0f, 0.5f, 0.5f));
            const int unitEnergyCost = 4;
            _ledger.TryPayForMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // WHEN
            _ledger.RefundMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(_presenter.GetState(PlayerId).PendingSpendCount, Is.EqualTo(0));
        }

        [Test]
        public void RefundMove_UnknownPlayer_LeavesNoStateBehind()
        {
            // GIVEN

            // WHEN
            _ledger.RefundMove(UnknownPlayerId, MoveType.Clone, 4);

            // THEN
            Assert.That(_presenter.GetState(UnknownPlayerId), Is.Null);
        }

        [Test]
        public void RefundMove_WithoutAPrecedingTryPayForMove_CreditsEnergyNeverPaid()
        {
            // GIVEN — IEnergyLedger documents this call order as illegal. This pins what RefundMove actually
            // does today, not what it should do, so a later change to the behavior is a deliberate decision
            // instead of an accidental regression.
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 5f));
            const int unitEnergyCost = 4;

            // WHEN
            _ledger.RefundMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void CanAffordMove_ExactBalance_ReturnsTrueAndChargesNothing()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 2.5f));
            const int unitEnergyCost = 5;

            // WHEN
            bool canAfford = _ledger.CanAffordMove(PlayerId, MoveType.Clone, unitEnergyCost);

            // THEN
            Assert.That(canAfford, Is.True);
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(2.5f).Within(Tolerance));
        }

        [Test]
        public void TryPayForDiscard_Affordable_DeductsExactlyTheDiscardEnergyCost()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 0.5f, 0.5f));

            // WHEN
            _discardLedger.TryPayForDiscard(PlayerId);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(9.5f).Within(Tolerance));
        }

        [Test]
        public void TryPayForDiscard_Unaffordable_ReturnsFalseAndLeavesTheBalanceUntouched()
        {
            // GIVEN — IDiscardLedger states the untouched balance as an invariant of a false return, so the
            // balance is asserted here rather than only the boolean.
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 0.49f, 0.5f, 0.5f, 0.5f));

            // WHEN
            bool wasPaid = _discardLedger.TryPayForDiscard(PlayerId);

            // THEN
            Assert.That(wasPaid, Is.False);
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(0.49f).Within(Tolerance));
        }

        [Test]
        public void TryPayForDiscard_UnknownPlayer_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool wasPaid = _discardLedger.TryPayForDiscard(UnknownPlayerId);

            // THEN
            Assert.That(wasPaid, Is.False);
        }

        [Test]
        public void RefundDiscard_AfterAPaidDiscard_RestoresTheBalance()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 0.5f, 0.5f));
            _discardLedger.TryPayForDiscard(PlayerId);

            // WHEN
            _discardLedger.RefundDiscard(PlayerId);

            // THEN
            Assert.That(_presenter.GetEnergy(PlayerId), Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void RefundDiscard_ZeroCostCharge_WithdrawsThePendingSpend()
        {
            // GIVEN — mirrors RefundMove_ZeroCostCharge_WithdrawsThePendingSpend: TryPayForDiscard marks a spend
            // pending even for a free discard, so the withdrawal in RefundDiscard must be unconditional too.
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 0.5f, 0f));
            _discardLedger.TryPayForDiscard(PlayerId);

            // WHEN
            _discardLedger.RefundDiscard(PlayerId);

            // THEN
            Assert.That(_presenter.GetState(PlayerId).PendingSpendCount, Is.EqualTo(0));
        }

        [Test]
        public void RefundDiscard_CalledTwiceForOneCharge_ClampsPendingSpendCountAtZero()
        {
            // GIVEN — a second RefundDiscard for the same charge violates IDiscardLedger's documented call order
            // (only legal once, immediately after the TryPayForDiscard it reverses); this pins only that
            // PendingSpendCount clamps at zero instead of going negative. The resulting over-credited balance is
            // outside the contract and is deliberately not asserted here.
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 10f, 0.5f, 0.5f, 0.5f));
            _discardLedger.TryPayForDiscard(PlayerId);
            _discardLedger.RefundDiscard(PlayerId);

            // WHEN
            _discardLedger.RefundDiscard(PlayerId);

            // THEN
            Assert.That(_presenter.GetState(PlayerId).PendingSpendCount, Is.EqualTo(0));
        }

        [Test]
        public void RefundDiscard_UnknownPlayer_DoesNotThrow()
        {
            // GIVEN

            // WHEN / THEN
            Assert.DoesNotThrow(() => _discardLedger.RefundDiscard(UnknownPlayerId));
        }

        [Test]
        public void CanAffordDiscard_BalanceExactlyAtTheCost_ReturnsTrue()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 0.5f, 0.5f, 0.5f, 0.5f));

            // WHEN
            bool canAfford = _discardLedger.CanAffordDiscard(PlayerId);

            // THEN
            Assert.That(canAfford, Is.True);
        }

        [Test]
        public void CanAffordDiscard_BalanceJustBelowTheCost_ReturnsFalse()
        {
            // GIVEN
            _presenter.InitializePlayer(PlayerId, new EnergyConfig(10f, 0f, 0.49f, 0.5f, 0.5f, 0.5f));

            // WHEN
            bool canAfford = _discardLedger.CanAffordDiscard(PlayerId);

            // THEN
            Assert.That(canAfford, Is.False);
        }

        [Test]
        public void CanAffordDiscard_UnknownPlayer_ReturnsFalse()
        {
            // GIVEN

            // WHEN
            bool canAfford = _discardLedger.CanAffordDiscard(UnknownPlayerId);

            // THEN
            Assert.That(canAfford, Is.False);
        }
    }
}
