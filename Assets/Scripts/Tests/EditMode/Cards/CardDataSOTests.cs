using System;
using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardDataSOTests
    {
        private CardDataSO _card;

        [TearDown]
        public void TearDown()
        {
            if (_card != null)
            {
                UnityEngine.Object.DestroyImmediate(_card);
            }
        }

        [Test]
        public void ValidateAuthoredData_WithEmptyCardId_WarnsThatTheCardCannotBeRegistered()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestCard";
            _card.SetAuthoredData(
                string.Empty,
                "Unnamed",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: false,
                canJump: false,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null
            );
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.CardIdEmptyFormat, "TestCard"));

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            Assert.That(_card.CardId.Value, Is.Empty);
        }

        [Test]
        public void CardDataSO_AsAuthoredAsset_DeclaresCreateAssetMenuAttribute()
        {
            // GIVEN
            CreateAssetMenuAttribute attribute = typeof(CardDataSO).GetCustomAttribute<CreateAssetMenuAttribute>();

            // THEN
            Assert.That(attribute, Is.Not.Null);
        }

        [Test]
        public void CreateInstance_WithKnownValues_PropertyGettersReturnCorrectValues()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "bio_phalanx",
                "Bio-Phalanx",
                "Test description.",
                CardType.Troop,
                energyCost: 3,
                canClone: true,
                canJump: true,
                hasArmor: true,
                ignoresHazards: true,
                conversionRadius: 2,
                landingEffects: null
            );

            // WHEN / THEN
            Assert.That(_card.CardId.Value, Is.EqualTo("bio_phalanx"));
            Assert.That(_card.DisplayName, Is.EqualTo("Bio-Phalanx"));
            Assert.That(_card.Type, Is.EqualTo(CardType.Troop));
            Assert.That(_card.EnergyCost, Is.EqualTo(3));
            Assert.That(_card.CanClone, Is.True);
            Assert.That(_card.CanJump, Is.True);
            Assert.That(_card.HasArmor, Is.True);
            Assert.That(_card.IgnoresHazards, Is.True);
            Assert.That(_card.ConversionRadius, Is.EqualTo(2));
        }

        [Test]
        public void CreateInstance_DefaultValues_MatchExpectedDefaults()
        {
            // GIVEN
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            _card = card;

            // THEN
            Assert.That(card.Type, Is.EqualTo(CardType.Troop));
            Assert.That(card.EnergyCost, Is.EqualTo(1));
            Assert.That(card.CanClone, Is.False);
            Assert.That(card.CanJump, Is.False);
            Assert.That(card.HasArmor, Is.False);
            Assert.That(card.IgnoresHazards, Is.False);
            Assert.That(card.ConversionRadius, Is.EqualTo(1));
            Assert.That(card.CloneDistance, Is.EqualTo(1));
            Assert.That(card.JumpDistance, Is.EqualTo(2));
            Assert.That(card.LandingEffects, Is.Empty);
        }

        [Test]
        public void CreateInstance_DefaultValues_DescriptionIsEmptyNotNull()
        {
            // GIVEN
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            _card = card;

            // THEN
            Assert.That(card.Description, Is.Not.Null.And.Empty);
        }

        [Test]
        public void Description_AfterSetAuthoredData_RoundTrips()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "subject_alpha",
                "Subject Alpha",
                "Duplicates onto an adjacent hex.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null
            );

            // THEN
            Assert.That(_card.Description, Is.EqualTo("Duplicates onto an adjacent hex."));
        }

        [Test]
        public void ValidateAuthoredData_WithEmptyDescription_WarnsThatTheCardFaceRendersBlank()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestCard";
            _card.SetAuthoredData(
                "subject_alpha",
                "Subject Alpha",
                string.Empty,
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null
            );
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.DescriptionEmptyFormat, "TestCard"));

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ConversionRadius_AfterSetAuthoredData_RoundTrips()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "volatile_mass",
                "Volatile Mass",
                "Test description.",
                CardType.Troop,
                energyCost: 4,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 2,
                landingEffects: null
            );

            // THEN
            Assert.That(_card.ConversionRadius, Is.EqualTo(2));
        }

        [Test]
        public void MoveDistances_AfterSetAuthoredData_RoundTrip()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "long_ranged",
                "Long Ranged",
                "Test description.",
                CardType.Troop,
                energyCost: 4,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null,
                cloneDistance: 2,
                jumpDistance: 4
            );

            // THEN
            Assert.That((_card.CloneDistance, _card.JumpDistance), Is.EqualTo((2, 4)));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void JumpDistance_Unauthored_ResolvesToTheStandardTwoRatherThanTheSharedMinimum(int authored)
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "long_ranged",
                "Long Ranged",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null,
                jumpDistance: authored
            );

            // THEN
            Assert.That(_card.JumpDistance, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void CloneDistance_AuthoredBelowTheMinimum_ClampsUpOnRead(int authored)
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "long_ranged",
                "Long Ranged",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null,
                cloneDistance: authored
            );

            // THEN
            Assert.That(_card.CloneDistance, Is.EqualTo(1));
        }

        [Test]
        public void IgnoresHazards_AfterSetAuthoredData_RoundTrips()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();

            // WHEN
            _card.SetAuthoredData(
                "plasmic_leaper",
                "Plasmic Leaper",
                "Test description.",
                CardType.Troop,
                energyCost: 3,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: true,
                conversionRadius: 1,
                landingEffects: null
            );

            // THEN
            Assert.That(_card.IgnoresHazards, Is.True);
        }

        [Test]
        public void ConversionRadius_AuthoredBelowOne_ClampsToOne()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "subject_alpha",
                "Subject Alpha",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 0,
                landingEffects: null
            );

            // WHEN
            int radius = _card.ConversionRadius;

            // THEN
            Assert.That(radius, Is.EqualTo(BoardMetrics.DefaultConversionRadius));
        }

        [Test]
        public void ConversionRadius_AuthoredAboveMax_ClampsToMaxConversionRadius()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "volatile_mass",
                "Volatile Mass",
                "Test description.",
                CardType.Troop,
                energyCost: 4,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 5,
                landingEffects: null
            );

            // WHEN
            int radius = _card.ConversionRadius;

            // THEN
            Assert.That(radius, Is.EqualTo(BoardMetrics.MaxConversionRadius));
        }

        [Test]
        public void ValidateAuthoredData_OutOfRangeRadius_ClampsTheSerializedBackingField()
        {
            // GIVEN — the property clamps on every read regardless, so the backing field itself has to be
            // inspected through Unity's own serialization to prove ValidateAuthoredData actually wrote it back,
            // rather than the property merely re-clamping an unchanged out-of-range field on every access.
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "volatile_mass",
                "Volatile Mass",
                "Test description.",
                CardType.Troop,
                energyCost: 4,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 5,
                landingEffects: null
            );

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            var serializedCard = new SerializedObject(_card);
            SerializedProperty radiusProperty = serializedCard.FindProperty("_conversionRadius");
            Assert.That(radiusProperty.intValue, Is.EqualTo(BoardMetrics.MaxConversionRadius));
        }

        [Test]
        public void LandingEffects_AuthoredDefinitions_MapFieldForFieldToRuntimeImpactEffects()
        {
            // GIVEN
            var definitions = new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.Enemy, 3) };
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "cryo_stasis",
                "Cryo-Stasis",
                "Test description.",
                CardType.Spell,
                energyCost: 2,
                canClone: false,
                canJump: false,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: definitions
            );

            // WHEN
            IReadOnlyList<ImpactEffect> effects = _card.LandingEffects;

            // THEN
            Assert.That(effects[0].Type, Is.EqualTo(ImpactEffectType.ApplyStatus));
            Assert.That(effects[0].Status, Is.EqualTo(StatusType.Frozen));
            Assert.That(effects[0].Radius, Is.EqualTo(1));
            Assert.That(effects[0].Duration, Is.EqualTo(1));
            Assert.That(effects[0].Target, Is.EqualTo(TargetFilter.Enemy));
            Assert.That(effects[0].ClusterSize, Is.EqualTo(3));
        }

        [Test]
        public void LandingEffects_ReadTwice_ReturnsTheSameCachedInstance()
        {
            // GIVEN
            var definitions = new[] { new ImpactEffectDefinition(ImpactEffectType.SelfDestruct, StatusType.None, 0, 0, TargetFilter.Self, 0) };
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "volatile_mass",
                "Volatile Mass",
                "Test description.",
                CardType.Troop,
                energyCost: 4,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 2,
                landingEffects: definitions
            );

            // WHEN
            IReadOnlyList<ImpactEffect> first = _card.LandingEffects;
            IReadOnlyList<ImpactEffect> second = _card.LandingEffects;

            // THEN
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void LandingEffects_NullAuthoredArray_IsEmptyNotNull()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "subject_alpha",
                "Subject Alpha",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: null
            );

            // WHEN
            IReadOnlyList<ImpactEffect> effects = _card.LandingEffects;

            // THEN
            Assert.That(effects, Is.Empty);
        }

        [Test]
        public void LandingEffects_EmptyAuthoredArray_IsEmpty()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.SetAuthoredData(
                "subject_alpha",
                "Subject Alpha",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: true,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: Array.Empty<ImpactEffectDefinition>()
            );

            // WHEN
            IReadOnlyList<ImpactEffect> effects = _card.LandingEffects;

            // THEN
            Assert.That(effects, Is.Empty);
        }

        [Test]
        public void ValidateAuthoredData_SpellWithOneZeroClusterSizeImpact_WarnsOnce()
        {
            // GIVEN
            var definitions = new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.All, 0) };
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestSpellCard";
            _card.SetAuthoredData(
                "test_spell",
                "Test Spell",
                "Test description.",
                CardType.Spell,
                energyCost: 1,
                canClone: false,
                canJump: false,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: definitions
            );
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.SpellClusterSizeMissingFormat, "TestSpellCard", 0));

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateAuthoredData_SpellWithTwoZeroClusterSizeImpacts_WarnsOncePerImpact()
        {
            // GIVEN
            var definitions = new[]
            {
                new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.All, 0),
                new ImpactEffectDefinition(ImpactEffectType.SpawnHazard, StatusType.None, 0, 1, TargetFilter.Self, 0),
            };
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestSpellCard";
            _card.SetAuthoredData(
                "test_spell",
                "Test Spell",
                "Test description.",
                CardType.Spell,
                energyCost: 1,
                canClone: false,
                canJump: false,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: definitions
            );
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.SpellClusterSizeMissingFormat, "TestSpellCard", 0));
            LogAssert.Expect(LogType.Warning, string.Format(CardLogMessages.SpellClusterSizeMissingFormat, "TestSpellCard", 1));

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateAuthoredData_TroopWithZeroClusterSizeImpact_DoesNotWarn()
        {
            // GIVEN — a Cluster Size of 0 on a Troop means "no ceiling", not "unplayable"; it is only a
            // Protocol's cluster count.
            var definitions = new[] { new ImpactEffectDefinition(ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.Enemy, 0) };
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestTroopCard";
            _card.SetAuthoredData(
                "test_troop",
                "Test Troop",
                "Test description.",
                CardType.Troop,
                energyCost: 1,
                canClone: false,
                canJump: true,
                hasArmor: false,
                ignoresHazards: false,
                conversionRadius: 1,
                landingEffects: definitions
            );

            // WHEN
            _card.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }
    }
}
