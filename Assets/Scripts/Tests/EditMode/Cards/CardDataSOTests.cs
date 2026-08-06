using System.Reflection;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using NUnit.Framework;
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
                Object.DestroyImmediate(_card);
            }
        }

        [Test]
        public void ValidateAuthoredData_WithEmptyCardId_WarnsThatTheCardCannotBeRegistered()
        {
            // GIVEN
            _card = ScriptableObject.CreateInstance<CardDataSO>();
            _card.name = "TestCard";
            _card.SetAuthoredData(string.Empty, "Unnamed", CardType.Troop, energyCost: 1, canClone: false, canJump: false, hasArmor: false);
            LogAssert.Expect(LogType.Warning, "TestCard: CardId is empty. Assign a unique, stable id before referencing this card in a CardPresenter.");

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
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData("bio_phalanx", "Bio-Phalanx", CardType.Troop, energyCost: 3, canClone: true, canJump: true, hasArmor: true);

            // WHEN / THEN
            Assert.That(card.CardId.Value, Is.EqualTo("bio_phalanx"));
            Assert.That(card.DisplayName, Is.EqualTo("Bio-Phalanx"));
            Assert.That(card.Type, Is.EqualTo(CardType.Troop));
            Assert.That(card.EnergyCost, Is.EqualTo(3));
            Assert.That(card.CanClone, Is.True);
            Assert.That(card.CanJump, Is.True);
            Assert.That(card.HasArmor, Is.True);
        }

        [Test]
        public void CreateInstance_DefaultValues_MatchExpectedDefaults()
        {
            // GIVEN
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();

            // THEN
            Assert.That(card.Type, Is.EqualTo(CardType.Troop));
            Assert.That(card.EnergyCost, Is.EqualTo(1));
            Assert.That(card.CanClone, Is.False);
            Assert.That(card.CanJump, Is.False);
            Assert.That(card.HasArmor, Is.False);
        }
    }
}
