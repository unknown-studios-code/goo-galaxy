using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.AI.Services;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Services;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.AI
{
    [TestFixture]
    public class MoveOptionResolverTests
    {
        private const int BoardRadius = 4;
        private const int ActingPlayerId = 1;
        private const int RivalPlayerId = 2;
        private const int ActingUnitId = 1;
        private const int RivalUnitId = 2;
        private const int HandSlotCapacity = 4;
        private const int TroopEnergyCost = 2;
        private const int SpellEnergyCost = 2;
        private const int ClusterSize = 3;
        private const int ClusterRadius = 1;
        private const int StatusDuration = 1;
        private const int NeighborCount = 6;
        private const int RingCountAtDistanceTwo = 12;
        private const int HazardDuration = 1;
        private const int Seed = 12345;

        // Pinned rather than re-derived: the cross-platform guarantee is that this exact seed yields this exact
        // stream on every runtime, and re-deriving it from the production code would only prove self-agreement.
        private const int SeedDerivedForTargetStream = 665222832;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _adjacent = new(1, 0);
        private static readonly HexCoordinates _secondAdjacent = new(0, 1);
        private static readonly HexCoordinates _thirdAdjacent = new(-1, 1);
        private static readonly HexCoordinates _distant = new(2, 0);

        private static readonly HexCoordinates[] _originNeighbors = { new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1) };

        private readonly List<CardDefinition> _handCards = new(HandSlotCapacity);
        private readonly List<MoveOption> _results = new();

        private HexGrid _grid;
        private Dictionary<int, GridUnit> _units;
        private Dictionary<int, IMoveCapable> _capabilities;
        private FakeEnergyLedger _ledger;
        private MoveOptionBuffers _buffers;
        private Xorshift32 _random;

        [SetUp]
        public void SetUp()
        {
            _grid = new HexGrid(new FakeGridLayout());
            _units = new Dictionary<int, GridUnit>();
            _capabilities = new Dictionary<int, IMoveCapable>();
            _ledger = new FakeEnergyLedger();
            _buffers = new MoveOptionBuffers(HandSlotCapacity);
            _random = new Xorshift32(Seed);
            _handCards.Clear();
            _results.Clear();
        }

        [Test]
        public void DeriveSeed_ForTheTargetStream_MatchesThePinnedSeed()
        {
            // GIVEN

            // WHEN
            int derived = MoveOptionResolver.DeriveSeed(Seed);

            // THEN
            Assert.That(derived, Is.EqualTo(SeedDerivedForTargetStream));
        }

        [Test]
        public void Resolve_EmptyBoardAndEmptyHand_ProducesNoOptions()
        {
            // GIVEN

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_TroopInHandAndNoUnitsOnTheBoard_ProducesNoDeployOption()
        {
            // GIVEN — the Deploy footprint is every empty sector next to one the player holds, so holding none
            // leaves it empty however affordable the card is.
            _handCards.Add(CreateTroop());

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_TroopInHandAndOneOwnedUnit_ProducesADeployOptionOnEveryAdjacentEmptySector()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _handCards.Add(CreateTroop());

            // WHEN
            Resolve();

            // THEN
            Assert.That(CollectTargets(_results), Is.EquivalentTo(_originNeighbors));
        }

        [Test]
        public void Resolve_TroopInHandAndOneOwnedUnit_ProducesNoDeployOptionOnTheOccupiedSector()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _handCards.Add(CreateTroop());

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _origin), Is.False);
        }

        [Test]
        public void Resolve_UnaffordableTroopInHand_ProducesNoOption()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _handCards.Add(CreateTroop());
            _ledger.IsAffordable = false;

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_UnaffordableSpellInHand_ProducesNoOption()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));
            _ledger.IsAffordable = false;

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_TroopInHandAndOnlyARivalUnitOnTheBoard_ProducesNoDeployOption()
        {
            // GIVEN — the ownership invariant on the Deploy footprint: a rival sector is never territory the
            // acting player may deploy next to.
            PlaceUnit(RivalUnitId, RivalPlayerId, _origin);
            _handCards.Add(CreateTroop());

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_RivalUnitRegisteredAsMovable_ProducesNoCloneOrJumpOption()
        {
            // GIVEN — the same ownership invariant on movement: a rival unit is reachable through the registry
            // and must still never be commanded by the acting player.
            PlaceUnit(RivalUnitId, RivalPlayerId, _origin);
            _capabilities[RivalUnitId] = new FakeMoveCapability(canClone: true, canJump: true);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_UnitWithNoRegisteredCapability_ProducesNoCloneOrJumpOption()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_UnitThatCannotClone_ProducesNoCloneOption()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: false, canJump: true);

            // WHEN
            Resolve();

            // THEN
            Assert.That(CountMoveType(_results, MoveType.Clone), Is.EqualTo(0));
        }

        [Test]
        public void Resolve_UnitThatCannotJump_ProducesNoJumpOption()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false);

            // WHEN
            Resolve();

            // THEN
            Assert.That(CountMoveType(_results, MoveType.Jump), Is.EqualTo(0));
        }

        [Test]
        public void Resolve_CloneCapableUnit_ProducesOneOptionPerSectorOnTheRingAtTheCloneDistance()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false, cloneDistance: 2);

            // WHEN
            Resolve();

            // THEN — the ring at distance two, not the spiral out to it, which would hold six more.
            Assert.That(_results, Has.Count.EqualTo(RingCountAtDistanceTwo));
        }

        [Test]
        public void Resolve_CloneDistanceOfTwo_ProducesNoOptionOnASectorOneHexAway()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false, cloneDistance: 2);

            // WHEN
            Resolve();

            // THEN — an authored distance is exact rather than a maximum, so the nearer sector is not a target.
            Assert.That(ContainsTarget(_results, _adjacent), Is.False);
        }

        [Test]
        public void Resolve_JumpCapableUnit_ProducesOneOptionPerSectorOnTheRingAtTheJumpDistance()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: false, canJump: true, jumpDistance: 1);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Has.Count.EqualTo(NeighborCount));
        }

        [Test]
        public void Resolve_JumpDistanceOfOne_ProducesNoOptionOnASectorTwoHexesAway()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: false, canJump: true, jumpDistance: 1);

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _distant), Is.False);
        }

        [Test]
        public void Resolve_MoveDistanceBeyondTheBoard_ProducesNoOption()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: true, cloneDistance: 9, jumpDistance: 9);

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_OccupiedSectorInRange_ProducesNoOptionOnIt()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            PlaceUnit(RivalUnitId, RivalPlayerId, _adjacent);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false);

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _adjacent), Is.False);
        }

        [Test]
        public void Resolve_BlockedSectorInRange_ProducesNoOptionOnIt()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            BlockCell(_secondAdjacent);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false);

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _secondAdjacent), Is.False);
        }

        [Test]
        public void Resolve_HazardedSectorInRange_ProducesNoOptionOnIt()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            SetHazard(_thirdAdjacent);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false);

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _thirdAdjacent), Is.False);
        }

        [Test]
        public void Resolve_HazardedSectorAndAHazardIgnoringUnit_StillProducesAnOptionOnIt()
        {
            // GIVEN — Hover is the authored exemption, and the hazard rule is capability-relative rather than
            // absolute, so the enumerator must offer what the validator would accept.
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            SetHazard(_thirdAdjacent);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false, canIgnoreHazards: true);

            // WHEN
            Resolve();

            // THEN
            Assert.That(ContainsTarget(_results, _thirdAdjacent), Is.True);
        }

        [Test]
        public void Resolve_SpellInHand_ProducesOneProtocolOption()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Has.Count.EqualTo(1));
        }

        [Test]
        public void Resolve_SpellInHand_ProducesAClusterOfExactlyTheAuthoredSize()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results[0].TargetCluster, Has.Count.EqualTo(ClusterSize));
        }

        [Test]
        public void Resolve_SpellInHand_ProducesAClusterTheAbilityTargetValidatorAccepts()
        {
            // GIVEN
            ImpactEffect effect = CreateClusterImpact(ClusterSize, ClusterRadius);
            _handCards.Add(CreateSpell(effect));
            Resolve();

            // WHEN
            bool isValid = AbilityTargetValidator.ValidateTargets(_results[0].TargetCluster, effect, _grid);

            // THEN — distinctness, membership of the board, and the radius around the centre, all at once.
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void Resolve_SpellInASecondHandSlot_ProducesAProtocolOptionNamingThatSlot()
        {
            // GIVEN
            _handCards.Add(CreateTroop());
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results[0].SlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_SpellWhoseImpactsDisagreeOnClusterSize_ProducesNoOption()
        {
            // GIVEN — an authoring fault the ability validator already refuses; reconciling it here would offer
            // an action the board then rejects.
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius), CreateClusterImpact(ClusterSize + 1, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_SpellWhoseImpactsDisagreeOnRadius_ProducesNoOption()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius), CreateClusterImpact(ClusterSize, 0)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_SpellClusterLargerThanTheSectorsWithinItsRadius_ProducesNoOption()
        {
            // GIVEN — eight hexes cannot be drawn from a radius of one, which reaches seven at most.
            _handCards.Add(CreateSpell(CreateClusterImpact(8, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN — no option at all, rather than a short cluster the board would refuse.
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_SpellWithAClusterSizeOfZero_ProducesNoOption()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(0, ClusterRadius)));

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_SpellWithNoLandingImpact_ProducesNoOption()
        {
            // GIVEN
            _handCards.Add(CreateSpell());

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_SameSeedAndSameBoard_ProducesTheSameCluster()
        {
            // GIVEN
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));
            var secondRandom = new Xorshift32(Seed);
            var secondBuffers = new MoveOptionBuffers(HandSlotCapacity);
            var secondResults = new List<MoveOption>();
            Resolve();

            // WHEN
            MoveOptionResolver.Resolve(ActingPlayerId, _grid, _units, _capabilities, _handCards, _ledger, ref secondRandom, secondBuffers, secondResults);

            // THEN
            Assert.That(_results[0].TargetCluster, Is.EqualTo(secondResults[0].TargetCluster));
        }

        [Test]
        public void Resolve_SecondPass_HandsTheProtocolOptionTheSameBorrowedClusterBuffer()
        {
            // GIVEN — the borrowing contract: a cluster is the caller buffer, refilled on the next pass, so an
            // option retained past its tick reads whatever that pass wrote.
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));
            Resolve();
            IReadOnlyList<HexCoordinates> firstCluster = _results[0].TargetCluster;

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results[0].TargetCluster, Is.SameAs(firstCluster));
        }

        [Test]
        public void Resolve_SecondPass_ClearsTheOptionsTheFirstProduced()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: false);
            Resolve();
            _units.Clear();
            _capabilities.Clear();

            // WHEN
            Resolve();

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        [Category("Allocation")]
        public void Resolve_RepeatedPasses_AllocatesNoManagedMemory()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _capabilities[ActingUnitId] = new FakeMoveCapability(canClone: true, canJump: true);
            _handCards.Add(CreateTroop());
            _handCards.Add(CreateSpell(CreateClusterImpact(ClusterSize, ClusterRadius)));
            Resolve(); // Warm-up: grows every buffer to its steady-state capacity and excludes JIT allocation.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 100; i++)
            {
                Resolve();
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0), "MoveOptionResolver.Resolve allocated memory on a per-tick path!");
        }

        [Test]
        public void Resolve_NullGrid_ProducesAnEmptyList()
        {
            // GIVEN
            _handCards.Add(CreateTroop());

            // WHEN
            MoveOptionResolver.Resolve(ActingPlayerId, null, _units, _capabilities, _handCards, _ledger, ref _random, _buffers, _results);

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_NullUnitRegistry_ProducesAnEmptyList()
        {
            // GIVEN
            _handCards.Add(CreateTroop());

            // WHEN
            MoveOptionResolver.Resolve(ActingPlayerId, _grid, null, _capabilities, _handCards, _ledger, ref _random, _buffers, _results);

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_NullCapabilityRegistry_ProducesAnEmptyList()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);

            // WHEN
            MoveOptionResolver.Resolve(ActingPlayerId, _grid, _units, null, _handCards, _ledger, ref _random, _buffers, _results);

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_UnavailableEnergyLedger_ProducesAnEmptyList()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);
            _handCards.Add(CreateTroop());

            // WHEN
            MoveOptionResolver.Resolve(ActingPlayerId, _grid, _units, _capabilities, _handCards, null, ref _random, _buffers, _results);

            // THEN
            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Resolve_NullResultsBuffer_DoesNotThrow()
        {
            // GIVEN
            PlaceUnit(ActingUnitId, ActingPlayerId, _origin);

            // WHEN / THEN
            Assert.DoesNotThrow(() =>
                MoveOptionResolver.Resolve(ActingPlayerId, _grid, _units, _capabilities, _handCards, _ledger, ref _random, _buffers, null)
            );
        }

        private static ImpactEffect CreateClusterImpact(int clusterSize, int radius)
        {
            return new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, radius, StatusDuration, TargetFilter.All, clusterSize);
        }

        private static CardDefinition CreateTroop()
        {
            return new CardDefinition(new FakeCardData("troop_card", CardType.Troop, TroopEnergyCost, null));
        }

        private static CardDefinition CreateSpell(params ImpactEffect[] landingEffects)
        {
            return new CardDefinition(new FakeCardData("spell_card", CardType.Spell, SpellEnergyCost, landingEffects));
        }

        private static int CountMoveType(IReadOnlyList<MoveOption> options, MoveType moveType)
        {
            int count = 0;

            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].MoveType == moveType)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsTarget(IReadOnlyList<MoveOption> options, HexCoordinates target)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Target == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<HexCoordinates> CollectTargets(IReadOnlyList<MoveOption> options)
        {
            var targets = new List<HexCoordinates>(options.Count);

            for (int i = 0; i < options.Count; i++)
            {
                targets.Add(options[i].Target);
            }

            return targets;
        }

        private void Resolve()
        {
            MoveOptionResolver.Resolve(ActingPlayerId, _grid, _units, _capabilities, _handCards, _ledger, ref _random, _buffers, _results);
        }

        private void PlaceUnit(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId("troop_card"), position);
            _units[unitId] = unit;

            Assert.That(_grid.TryGetCell(position, out HexCell cell), Is.True, $"Test setup expects {position} to exist on the grid.");
            cell.SetOccupant(unitId);
        }

        private void BlockCell(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");
            cell.IsBlocked = true;
        }

        private void SetHazard(HexCoordinates coordinates)
        {
            Assert.That(_grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test setup expects {coordinates} to exist on the grid.");
            cell.SetHazard(RivalPlayerId, HazardDuration);
        }

        private sealed class FakeGridLayout : IGridLayout
        {
            public int GridRadius { get; } = BoardRadius;

            public IReadOnlySet<HexCoordinates> BlockedCoordinates { get; } = new ReadOnlySet<HexCoordinates>(new HashSet<HexCoordinates>());
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public bool IsAffordable { get; set; } = true;

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return IsAffordable;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return IsAffordable;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost) { }
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public FakeMoveCapability(
                bool canClone,
                bool canJump,
                bool canIgnoreHazards = false,
                int cloneDistance = BoardMetrics.DefaultCloneDistance,
                int jumpDistance = BoardMetrics.DefaultJumpDistance
            )
            {
                CanClone = canClone;
                CanJump = canJump;
                CanIgnoreHazards = canIgnoreHazards;
                CloneDistance = cloneDistance;
                JumpDistance = jumpDistance;
            }

            public bool CanClone { get; }

            public bool CanJump { get; }

            public bool CanIgnoreHazards { get; }

            public int CloneDistance { get; }

            public int JumpDistance { get; }
        }

        private sealed class FakeCardData : ICardData
        {
            private static readonly ImpactEffect[] _noLandingEffects = Array.Empty<ImpactEffect>();

            public FakeCardData(string cardId, CardType type, int energyCost, IReadOnlyList<ImpactEffect> landingEffects)
            {
                CardId = new CardId(cardId);
                Type = type;
                EnergyCost = energyCost;
                LandingEffects = landingEffects ?? _noLandingEffects;
            }

            public CardId CardId { get; }

            public string DisplayName => "Test Card";

            public string Description => string.Empty;

            public CardType Type { get; }

            // Accent is authored presentation the resolver never reads; None keeps the fake at the same
            // "no accent" degrade path an unauthored card asset takes.
            public CardAccent Accent => CardAccent.None;

            public int EnergyCost { get; }

            public bool CanClone => false;

            public bool CanJump => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => BoardMetrics.DefaultJumpDistance;

            public bool HasArmor => false;

            public bool CanIgnoreHazards => false;

            public int ConversionRadius => BoardMetrics.DefaultConversionRadius;

            public IReadOnlyList<ImpactEffect> LandingEffects { get; }
        }
    }
}
