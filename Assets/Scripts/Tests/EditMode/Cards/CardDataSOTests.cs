using System;
using System.Reflection;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Runtime.Tests.EditMode.Cards
{
    [TestFixture]
    public class CardDataSOTests
    {
        [Test]
        public void CardDataSO_HasCreateAssetMenuAttribute()
        {
            // GIVEN
            CreateAssetMenuAttribute attribute = typeof(CardDataSO).GetCustomAttribute<CreateAssetMenuAttribute>();

            // THEN
            Assert.IsNotNull(attribute);
        }

        [Test]
        public void CreateInstance_WithKnownValues_PropertyGettersReturnCorrectValues()
        {
            // GIVEN
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            Type soType = typeof(CardDataSO);

            soType.GetField("_cardId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, "bio_phalanx");
            soType.GetField("_displayName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, "Bio-Phalanx");
            soType.GetField("_type", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, CardType.Troop);
            soType.GetField("_energyCost", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, 3);
            soType.GetField("_canClone", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, true);
            soType.GetField("_canJump", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, true);
            soType.GetField("_hasArmor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, true);

            // WHEN / THEN
            Assert.AreEqual("bio_phalanx", card.CardId.Value);
            Assert.AreEqual("Bio-Phalanx", card.DisplayName);
            Assert.AreEqual(CardType.Troop, card.Type);
            Assert.AreEqual(3, card.EnergyCost);
            Assert.IsTrue(card.CanClone);
            Assert.IsTrue(card.CanJump);
            Assert.IsTrue(card.HasArmor);
        }

        [Test]
        public void CreateInstance_DefaultValues_MatchExpectedDefaults()
        {
            // GIVEN
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();

            // THEN
            Assert.AreEqual(CardType.Troop, card.Type);
            Assert.AreEqual(1, card.EnergyCost);
            Assert.IsFalse(card.CanClone);
            Assert.IsFalse(card.CanJump);
            Assert.IsFalse(card.HasArmor);
        }
    }
}
