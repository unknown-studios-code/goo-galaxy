using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEditor;

namespace GooGalaxy.Tests.EditMode.Cards
{
    [TestFixture]
    public class RosterValidationTests
    {
        private const string SubjectAlphaPath = "Assets/Data/Cards/Troops/SubjectAlpha.asset";
        private const string AcidCrawlerPath = "Assets/Data/Cards/Troops/AcidCrawler.asset";
        private const string BioPhalanxPath = "Assets/Data/Cards/Troops/BioPhalanx.asset";
        private const string VolatileMassPath = "Assets/Data/Cards/Troops/VolatileMass.asset";
        private const string CryoStasisPath = "Assets/Data/Cards/Spell/CryoStasis.asset";

        [TestCase(SubjectAlphaPath)]
        [TestCase(AcidCrawlerPath)]
        [TestCase(BioPhalanxPath)]
        [TestCase(VolatileMassPath)]
        [TestCase(CryoStasisPath)]
        public void LoadAssetAtPath_ForRosterCard_ReturnsANonNullAsset(string path)
        {
            // GIVEN

            // WHEN
            CardDataSO card = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);

            // THEN
            Assert.That(card, Is.Not.Null, $"Expected a CardDataSO asset at '{path}'.");
        }

        [TestCase(SubjectAlphaPath, "subject_alpha", "Subject Alpha", CardType.Troop, 1)]
        [TestCase(AcidCrawlerPath, "acid_crawler", "Acid Crawler", CardType.Troop, 2)]
        [TestCase(BioPhalanxPath, "bio_phalanx", "Bio-Phalanx", CardType.Troop, 3)]
        [TestCase(VolatileMassPath, "volatile_mass", "Volatile Mass", CardType.Troop, 4)]
        [TestCase(CryoStasisPath, "cryo_stasis", "Cryo-Stasis", CardType.Spell, 2)]
        public void AuthoredCard_KnownValues_MatchesTheAuthoredIdentity(string path, string cardId, string displayName, CardType type, int energyCost)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN
            (string Value, string DisplayName, CardType Type, int EnergyCost) actual = (card.CardId.Value, card.DisplayName, card.Type, card.EnergyCost);

            // THEN
            Assert.That(actual, Is.EqualTo((cardId, displayName, type, energyCost)));
        }

        [TestCase(SubjectAlphaPath, true, true, false, false)]
        [TestCase(AcidCrawlerPath, true, true, false, false)]
        [TestCase(BioPhalanxPath, true, true, true, false)]
        [TestCase(VolatileMassPath, false, true, false, false)]
        [TestCase(CryoStasisPath, false, false, false, false)]
        public void AuthoredCard_KnownValues_MatchesTheAuthoredCapability(string path, bool canClone, bool canJump, bool hasArmor, bool canIgnoreHazards)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN
            (bool CanClone, bool CanJump, bool HasArmor, bool CanIgnoreHazards) actual = (card.CanClone, card.CanJump, card.HasArmor, card.CanIgnoreHazards);

            // THEN
            Assert.That(actual, Is.EqualTo((canClone, canJump, hasArmor, canIgnoreHazards)));
        }

        [TestCase(SubjectAlphaPath, 1)]
        [TestCase(AcidCrawlerPath, 1)]
        [TestCase(BioPhalanxPath, 1)]
        [TestCase(VolatileMassPath, 2)]
        [TestCase(CryoStasisPath, 1)]
        public void ConversionRadius_ForRosterCard_MatchesTheAuthoredValue(string path, int conversionRadius)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN / THEN
            Assert.That(card.ConversionRadius, Is.EqualTo(conversionRadius));
        }

        [TestCase(AcidCrawlerPath, ImpactEffectType.SpawnHazard, StatusType.None, 0, 2, TargetFilter.Self, 0, ImpactDurationUnit.ActionWindows)]
        [TestCase(VolatileMassPath, ImpactEffectType.ArmFuse, StatusType.None, 0, 3, TargetFilter.Self, 0, ImpactDurationUnit.Seconds)]
        [TestCase(CryoStasisPath, ImpactEffectType.ApplyStatus, StatusType.Frozen, 1, 1, TargetFilter.All, 3, ImpactDurationUnit.ActionWindows)]
        public void LandingEffects_ForCardWithAnAuthoredEffect_MatchesTheAuthoredImpact(
            string path,
            ImpactEffectType type,
            StatusType status,
            int radius,
            int duration,
            TargetFilter target,
            int clusterSize,
            ImpactDurationUnit durationUnit
        )
        {
            // GIVEN
            CardDataSO card = LoadCard(path);
            Assert.That(
                card.LandingEffects.Count,
                Is.EqualTo(1),
                "A second authored effect would silently pass, and a removed one would throw instead of failing by name."
            );

            // WHEN
            ImpactEffect effect = card.LandingEffects[0];
            (ImpactEffectType Type, StatusType Status, int Radius, int Duration, TargetFilter Target, int ClusterSize, ImpactDurationUnit DurationUnit) actual =
                (effect.Type, effect.Status, effect.Radius, effect.Duration, effect.Target, effect.ClusterSize, effect.DurationUnit);

            // THEN
            Assert.That(actual, Is.EqualTo((type, status, radius, duration, target, clusterSize, durationUnit)));
        }

        [TestCase(SubjectAlphaPath)]
        [TestCase(BioPhalanxPath)]
        public void LandingEffects_ForCardWithNoAuthoredAbility_IsEmpty(string path)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN / THEN
            Assert.That(card.LandingEffects, Is.Empty);
        }

        [TestCase(SubjectAlphaPath)]
        [TestCase(AcidCrawlerPath)]
        [TestCase(BioPhalanxPath)]
        [TestCase(VolatileMassPath)]
        [TestCase(CryoStasisPath)]
        public void MoveDistances_ForRosterCard_AreTheStandardOneAndTwo(string path)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN
            (int CloneDistance, int JumpDistance) actual = (card.CloneDistance, card.JumpDistance);

            // THEN
            Assert.That(actual, Is.EqualTo((1, 2)));
        }

        [TestCase(SubjectAlphaPath)]
        [TestCase(AcidCrawlerPath)]
        [TestCase(BioPhalanxPath)]
        [TestCase(VolatileMassPath)]
        [TestCase(CryoStasisPath)]
        public void CardId_ForRosterCard_ValueIsNotEmpty(string path)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN / THEN
            Assert.That(card.CardId.Value, Is.Not.Null.And.Not.Empty);
        }

        [TestCase(SubjectAlphaPath)]
        [TestCase(AcidCrawlerPath)]
        [TestCase(BioPhalanxPath)]
        [TestCase(VolatileMassPath)]
        [TestCase(CryoStasisPath)]
        public void DisplayName_ForRosterCard_IsNotEmpty(string path)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN / THEN
            Assert.That(card.DisplayName, Is.Not.Null.And.Not.Empty);
        }

        [TestCase(SubjectAlphaPath)]
        [TestCase(AcidCrawlerPath)]
        [TestCase(BioPhalanxPath)]
        [TestCase(VolatileMassPath)]
        [TestCase(CryoStasisPath)]
        public void Description_ForRosterCard_IsNotEmpty(string path)
        {
            // GIVEN
            CardDataSO card = LoadCard(path);

            // WHEN / THEN
            Assert.That(card.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void CardId_AcrossTheRoster_AreAllDistinct()
        {
            // GIVEN
            string[] cardIds =
            {
                LoadCard(SubjectAlphaPath).CardId.Value,
                LoadCard(AcidCrawlerPath).CardId.Value,
                LoadCard(BioPhalanxPath).CardId.Value,
                LoadCard(VolatileMassPath).CardId.Value,
                LoadCard(CryoStasisPath).CardId.Value,
            };

            // WHEN / THEN
            Assert.That(cardIds, Is.Unique);
        }

        [Test]
        public void EnergyCost_AcrossTheRoster_MatchesThePowerBudgetOrdering()
        {
            // GIVEN
            int[] energyCosts =
            {
                LoadCard(VolatileMassPath).EnergyCost,
                LoadCard(BioPhalanxPath).EnergyCost,
                LoadCard(AcidCrawlerPath).EnergyCost,
                LoadCard(SubjectAlphaPath).EnergyCost,
            };

            // WHEN / THEN
            Assert.That(energyCosts, Is.Ordered.Descending);
        }

        private static CardDataSO LoadCard(string path)
        {
            CardDataSO card = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
            Assert.That(card, Is.Not.Null, $"Expected a CardDataSO asset at '{path}'.");

            return card;
        }
    }
}
