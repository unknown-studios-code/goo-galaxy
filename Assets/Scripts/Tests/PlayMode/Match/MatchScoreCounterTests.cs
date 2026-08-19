using System;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    [TestFixture]
    public class MatchScoreCounterTests
    {
        private const int BoardRadius = 6;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        private static readonly HexCoordinates _playerOneLiveA = new(1, 0);
        private static readonly HexCoordinates _playerOneLiveB = new(2, 0);
        private static readonly HexCoordinates _playerOneDead = new(-1, 0);
        private static readonly HexCoordinates _playerTwoLive = new(-2, 0);

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private UnitPresenter _unitPresenter;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);

            _boardGO = new GameObject("MatchScoreCounter_Board_Test");
            _boardGO.SetActive(false);
            GridPresenter gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(gridPresenter, new FakeEnergyLedger());
            gridPresenter.SetGridLayout(_gridLayout);
            _boardGO.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_boardGO != null)
            {
                Object.Destroy(_boardGO);
            }

            if (_gridLayout != null)
            {
                Object.DestroyImmediate(_gridLayout);
            }
        }

        [Test]
        public void CountLiveUnits_LiveAndDeadUnitsRegistered_CountsOnlyTheRequestedPlayersLiveUnits()
        {
            // GIVEN
            RegisterUnit(1, PlayerOneId, _playerOneLiveA, isAlive: true);
            RegisterUnit(2, PlayerOneId, _playerOneLiveB, isAlive: true);
            RegisterUnit(3, PlayerOneId, _playerOneDead, isAlive: false);
            RegisterUnit(4, PlayerTwoId, _playerTwoLive, isAlive: true);

            // WHEN
            int count = MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId);

            // THEN
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void CountLiveUnits_BothPlayersInOnePass_CountsEachPlayersLiveUnitsSeparately()
        {
            // GIVEN
            RegisterUnit(1, PlayerOneId, _playerOneLiveA, isAlive: true);
            RegisterUnit(2, PlayerOneId, _playerOneLiveB, isAlive: true);
            RegisterUnit(3, PlayerOneId, _playerOneDead, isAlive: false);
            RegisterUnit(4, PlayerTwoId, _playerTwoLive, isAlive: true);

            // WHEN
            MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId, PlayerTwoId, out int playerOneUnits, out int playerTwoUnits);

            // THEN
            Assert.That((playerOneUnits, playerTwoUnits), Is.EqualTo((2, 1)));
        }

        [Test]
        [Category("Allocation")]
        public void CountLiveUnits_RepeatedCalls_AllocatesNoManagedMemory()
        {
            // GIVEN
            RegisterUnit(1, PlayerOneId, _playerOneLiveA, isAlive: true);
            RegisterUnit(2, PlayerTwoId, _playerTwoLive, isAlive: true);
            MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId); // Warm-up: excludes JIT allocation.
            MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId);

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(
                allocatedAfter - allocatedBefore,
                Is.EqualTo(0),
                "CountLiveUnits allocated memory on a path its own <remarks> documents as allocation-free!"
            );
        }

        [Test]
        [Category("Allocation")]
        public void CountLiveUnits_RepeatedTwoPlayerCalls_AllocateNoManagedMemory()
        {
            // GIVEN
            RegisterUnit(1, PlayerOneId, _playerOneLiveA, isAlive: true);
            RegisterUnit(2, PlayerTwoId, _playerTwoLive, isAlive: true);
            MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId, PlayerTwoId, out _, out _); // Warm-up: excludes JIT allocation.
            MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId, PlayerTwoId, out _, out _);

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                MatchScoreCounter.CountLiveUnits(_unitPresenter, PlayerOneId, PlayerTwoId, out _, out _);
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(
                allocatedAfter - allocatedBefore,
                Is.EqualTo(0),
                "CountLiveUnits allocated memory on a path its own <remarks> documents as allocation-free!"
            );
        }

        private void RegisterUnit(int unitId, int playerId, HexCoordinates position, bool isAlive)
        {
            var unit = new GridUnit(unitId, playerId, new CardId($"card_{unitId}"), position) { IsAlive = isAlive };

            Assert.That(_unitPresenter.RegisterUnit(unit, null), Is.True, "Test setup expects the unit to register.");
        }

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
