using System;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Runtime.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardDefinitionTests
    {
        [Test]
        public void Constructor_CopiesAllValuesFromSource()
        {
            // GIVEN
            var source = new FakeCardData("bio_phalanx", "Bio-Phalanx", CardType.Troop, 3, canClone: true, canJump: true, hasArmor: true);

            // WHEN
            var definition = new CardDefinition(source);

            // THEN
            Assert.AreEqual(source.CardId, definition.CardId);
            Assert.AreEqual(source.DisplayName, definition.DisplayName);
            Assert.AreEqual(source.Type, definition.Type);
            Assert.AreEqual(source.EnergyCost, definition.EnergyCost);
            Assert.AreEqual(source.CanClone, definition.CanClone);
            Assert.AreEqual(source.CanJump, definition.CanJump);
            Assert.AreEqual(source.HasArmor, definition.HasArmor);
        }

        [Test]
        public void ImplementsIMoveCapable_ReflectsSourceCloneAndJumpFlags()
        {
            // GIVEN
            var source = new FakeCardData("volatile_mass", "Volatile Mass", CardType.Troop, 4, canClone: false, canJump: true, hasArmor: false);

            // WHEN
            IMoveCapable moveCapable = new CardDefinition(source);

            // THEN
            Assert.IsFalse(moveCapable.CanClone);
            Assert.IsTrue(moveCapable.CanJump);
        }

        [Test]
        public void TwoDefinitionsFromSameSource_AreEqualByValue()
        {
            // GIVEN
            var source = new FakeCardData("acid_crawler", "Acid Crawler", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);

            // WHEN
            var definitionA = new CardDefinition(source);
            var definitionB = new CardDefinition(source);

            // THEN
            Assert.AreEqual(definitionA.CardId, definitionB.CardId);
            Assert.AreEqual(definitionA.DisplayName, definitionB.DisplayName);
            Assert.AreEqual(definitionA.Type, definitionB.Type);
            Assert.AreEqual(definitionA.EnergyCost, definitionB.EnergyCost);
            Assert.AreEqual(definitionA.CanClone, definitionB.CanClone);
            Assert.AreEqual(definitionA.CanJump, definitionB.CanJump);
            Assert.AreEqual(definitionA.HasArmor, definitionB.HasArmor);
        }

        [Test]
        public void Constructor_RepeatedConstructions_AllocatesNoManagedMemory()
        {
            // GIVEN
            var source = new FakeCardData("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            _ = new CardDefinition(source); // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = new CardDefinition(source);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.AreEqual(0, allocatedAfter - allocatedBefore);
        }

        private sealed class FakeCardData : ICardData
        {
            public FakeCardData(string cardId, string displayName, CardType type, int energyCost, bool canClone, bool canJump, bool hasArmor)
            {
                CardId = new CardId(cardId);
                DisplayName = displayName;
                Type = type;
                EnergyCost = energyCost;
                CanClone = canClone;
                CanJump = canJump;
                HasArmor = hasArmor;
            }

            public CardId CardId { get; }

            public string DisplayName { get; }

            public CardType Type { get; }

            public int EnergyCost { get; }

            public bool CanClone { get; }

            public bool CanJump { get; }

            public bool HasArmor { get; }
        }
    }
}
