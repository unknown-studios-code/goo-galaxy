using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Deck
{
    [TestFixture]
    public class KitDataSOTests
    {
        private const string StarterKitPath = "Assets/Data/Deck/StarterKit.asset";

        private readonly List<Object> _spawned = new();

        private KitDataSO _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = ScriptableObject.CreateInstance<KitDataSO>();
            _kit.name = "TestKit";
        }

        [TearDown]
        public void TearDown()
        {
            if (_kit != null)
            {
                Object.DestroyImmediate(_kit);
            }

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void CardIds_AuthoredArrayWithAnEmptySlot_SkipsTheNullEntry()
        {
            // GIVEN
            CardDataSO cardA = CreateCard("kit_card_a");
            CardDataSO cardB = CreateCard("kit_card_b");
            _kit.SetAuthoredCards(cardA, null, cardB);

            // WHEN
            IReadOnlyList<CardId> cardIds = _kit.CardIds;

            // THEN
            Assert.That(cardIds, Is.EqualTo(new[] { new CardId("kit_card_a"), new CardId("kit_card_b") }));
        }

        [Test]
        public void CardIds_ReadTwice_ReturnsTheSameMemoizedInstance()
        {
            // GIVEN
            _kit.SetAuthoredCards(CreateCard("kit_card_a"), CreateCard("kit_card_b"));

            // WHEN
            IReadOnlyList<CardId> first = _kit.CardIds;
            IReadOnlyList<CardId> second = _kit.CardIds;

            // THEN
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void ValidateAuthoredData_NullEntry_WarnsThatTheSlotIsEmpty()
        {
            // GIVEN — six authored slots so the null skip alone does not also trip the too-small warning.
            CardDataSO card = CreateCard("kit_card_a");
            _kit.SetAuthoredCards(card, null, card, card, card, card);
            LogAssert.Expect(LogType.Warning, string.Format(DeckLogMessages.KitCardMissingFormat, "TestKit", 1));

            // WHEN
            _kit.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateAuthoredData_KitTooSmallForTheDefaultHand_WarnsThatNoHandCanBeDealt()
        {
            // GIVEN
            CardDataSO card = CreateCard("kit_card_a");
            _kit.SetAuthoredCards(card, card);
            int minimumKitSize = DeckState.GetMinimumKitSize(DeckState.DefaultHandSize);
            LogAssert.Expect(LogType.Warning, string.Format(DeckLogMessages.KitTooSmallFormat, "TestKit", 2, minimumKitSize, DeckState.DefaultHandSize));

            // WHEN
            _kit.ValidateAuthoredData();

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void StarterKit_LoadedByPath_IsLargeEnoughToFillTheDefaultHandAndItsNextSlot()
        {
            // GIVEN
            KitDataSO starterKit = AssetDatabase.LoadAssetAtPath<KitDataSO>(StarterKitPath);
            Assert.That(starterKit, Is.Not.Null, $"Expected a KitDataSO asset at '{StarterKitPath}'.");

            // WHEN
            int cardCount = starterKit.CardIds.Count;

            // THEN
            Assert.That(cardCount, Is.GreaterThanOrEqualTo(DeckState.GetMinimumKitSize(DeckState.DefaultHandSize)));
        }

        private CardDataSO CreateCard(string cardId)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, "Display Name", "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);
            _spawned.Add(card);

            return card;
        }
    }
}
