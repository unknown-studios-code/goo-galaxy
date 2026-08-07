using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardDefinitionTests
    {
        [Test]
        public void Constructor_FromCardData_CopiesEveryAuthoredValue()
        {
            // GIVEN
            var source = new FakeCardData(
                "bio_phalanx",
                "Bio-Phalanx",
                CardType.Troop,
                3,
                canClone: true,
                canJump: true,
                hasArmor: true,
                ignoresHazards: true,
                conversionRadius: 2
            );

            // WHEN
            var definition = new CardDefinition(source);

            // THEN
            Assert.That(definition.CardId, Is.EqualTo(source.CardId));
            Assert.That(definition.DisplayName, Is.EqualTo(source.DisplayName));
            Assert.That(definition.Type, Is.EqualTo(source.Type));
            Assert.That(definition.EnergyCost, Is.EqualTo(source.EnergyCost));
            Assert.That(definition.CanClone, Is.EqualTo(source.CanClone));
            Assert.That(definition.CanJump, Is.EqualTo(source.CanJump));
            Assert.That(definition.HasArmor, Is.EqualTo(source.HasArmor));
            Assert.That(definition.IgnoresHazards, Is.EqualTo(source.IgnoresHazards));
            Assert.That(definition.ConversionRadius, Is.EqualTo(source.ConversionRadius));
        }

        [Test]
        public void Constructor_FromCardDataWithLandingEffects_CopiesThemFieldForField()
        {
            // GIVEN
            var landingEffects = new[] { new ImpactEffect(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.Enemy, 3) };
            var source = new FakeCardData(
                "cryo_stasis",
                "Cryo-Stasis",
                CardType.Spell,
                2,
                canClone: false,
                canJump: false,
                hasArmor: false,
                landingEffects: landingEffects
            );

            // WHEN
            IAbilityCapable abilityCapable = new CardDefinition(source);

            // THEN
            Assert.That(abilityCapable.LandingEffects[0].Type, Is.EqualTo(ImpactEffectType.ApplyStatus));
            Assert.That(abilityCapable.LandingEffects[0].Status, Is.EqualTo(StatusType.Frozen));
            Assert.That(abilityCapable.LandingEffects[0].Radius, Is.EqualTo(1));
            Assert.That(abilityCapable.LandingEffects[0].Duration, Is.EqualTo(1));
            Assert.That(abilityCapable.LandingEffects[0].Target, Is.EqualTo(TargetFilter.Enemy));
            Assert.That(abilityCapable.LandingEffects[0].ClusterSize, Is.EqualTo(3));
        }

        [Test]
        public void Constructor_FromCardDataWithNoLandingEffects_LandingEffectsIsEmptyNotNull()
        {
            // GIVEN
            var source = new FakeCardData("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);

            // WHEN
            IAbilityCapable abilityCapable = new CardDefinition(source);

            // THEN
            Assert.That(abilityCapable.LandingEffects, Is.Empty);
        }

        [Test]
        public void MoveCapabilities_FromAuthoredFlags_MatchCloneAndJump()
        {
            // GIVEN
            var source = new FakeCardData("volatile_mass", "Volatile Mass", CardType.Troop, 4, canClone: false, canJump: true, hasArmor: false);

            // WHEN
            IMoveCapable moveCapable = new CardDefinition(source);

            // THEN
            Assert.That(moveCapable.CanClone, Is.False);
            Assert.That(moveCapable.CanJump, Is.True);
        }

        [Test]
        public void Constructor_CalledTwiceOnOneSource_ProducesMatchingValues()
        {
            // GIVEN
            var source = new FakeCardData("acid_crawler", "Acid Crawler", CardType.Troop, 2, canClone: true, canJump: true, hasArmor: false);

            // WHEN
            var definitionA = new CardDefinition(source);
            var definitionB = new CardDefinition(source);

            // THEN
            Assert.That(definitionB.CardId, Is.EqualTo(definitionA.CardId));
            Assert.That(definitionB.DisplayName, Is.EqualTo(definitionA.DisplayName));
            Assert.That(definitionB.Type, Is.EqualTo(definitionA.Type));
            Assert.That(definitionB.EnergyCost, Is.EqualTo(definitionA.EnergyCost));
            Assert.That(definitionB.CanClone, Is.EqualTo(definitionA.CanClone));
            Assert.That(definitionB.CanJump, Is.EqualTo(definitionA.CanJump));
            Assert.That(definitionB.HasArmor, Is.EqualTo(definitionA.HasArmor));
        }

        [Test]
        public void Definition_StoredInMoveCapableRegistry_AllocatesNoManagedMemory()
        {
            // GIVEN
            var source = new FakeCardData("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            var definition = new CardDefinition(source);
            var registry = new Dictionary<int, IMoveCapable>(1) { [0] = definition }; // Warm-up to exclude JIT allocation from the measurement.

            // WHEN
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                registry[0] = definition;
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            // THEN
            Assert.That(allocatedAfter - allocatedBefore, Is.EqualTo(0));
        }

        [Test]
        public void Definition_ReadBackFromMoveCapableRegistry_IsTheSameInstance()
        {
            // GIVEN
            var source = new FakeCardData("subject_alpha", "Subject Alpha", CardType.Troop, 1, canClone: true, canJump: true, hasArmor: false);
            var definition = new CardDefinition(source);
            var registry = new Dictionary<int, IMoveCapable>(1) { [0] = definition };

            // WHEN
            IMoveCapable stored = registry[0];

            // THEN
            Assert.That(stored, Is.SameAs(definition));
        }

        private sealed class FakeCardData : ICardData
        {
            private static readonly ImpactEffect[] _noLandingEffects = Array.Empty<ImpactEffect>();

            public FakeCardData(
                string cardId,
                string displayName,
                CardType type,
                int energyCost,
                bool canClone,
                bool canJump,
                bool hasArmor,
                bool ignoresHazards = false,
                int conversionRadius = 1,
                IReadOnlyList<ImpactEffect> landingEffects = null
            )
            {
                CardId = new CardId(cardId);
                DisplayName = displayName;
                Type = type;
                EnergyCost = energyCost;
                CanClone = canClone;
                CanJump = canJump;
                HasArmor = hasArmor;
                IgnoresHazards = ignoresHazards;
                ConversionRadius = conversionRadius;
                LandingEffects = landingEffects ?? _noLandingEffects;
            }

            public CardId CardId { get; }

            public string DisplayName { get; }

            public CardType Type { get; }

            public int EnergyCost { get; }

            public bool CanClone { get; }

            public bool CanJump { get; }

            public bool HasArmor { get; }

            public bool IgnoresHazards { get; }

            public int ConversionRadius { get; }

            public IReadOnlyList<ImpactEffect> LandingEffects { get; }
        }
    }
}
