using System.Globalization;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// One card in the hand strip, or the queued next card: the card's name, its Energy cost, and whether the
    /// player can currently pay for it.
    /// </summary>
    /// <remarks>
    /// <b>The slot draws artwork when the card has some</b>, behind the text and under the countdown scrim, and
    /// is hidden when it has none. It also carries a rarity modifier class that no stylesheet paints yet,
    /// because the GDD has not designed the treatment; the hook exists so the design lands as USS alone.
    /// <para>
    /// <b>The border is still not available to card identity.</b> It already carries two states — Protocol tints it
    /// and unaffordable dims the whole slot — so the card's accent goes on the accent bar instead, where neither
    /// can collide with it.
    /// </para>
    /// <para>
    /// <b>Affordability is set apart from the card.</b> It flips as Energy regenerates, several times a second,
    /// and only toggles a class; the card's text is rewritten solely when the hand rotates.
    /// </para>
    /// <para>
    /// The slot keeps the default <c>PickingMode.Position</c> even though nothing reads a pick from it yet, so
    /// the gesture work in GOOM-17 lands on the slot rather than on the labels inside it.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class CardSlotElement : VisualElement
    {
        // The costliest card in the roster is far below this. Above it the label is composed instead, so an
        // unexpected value still renders.
        private const int MaxTabulatedCost = 20;

        private static readonly string[] _costTexts = BuildCostTexts();

        private readonly VisualElement _artwork;
        private readonly VisualElement _accentBar;
        private readonly Label _costLabel;
        private readonly Label _nameLabel;

        private HandSlotState _state = HandSlotState.Empty;
        private string _accentClass;
        private Sprite _artworkSprite;
        private string _rarityClass;
        private bool _isAffordable = true;
        private bool _isDrawn;

        public CardSlotElement()
        {
            AddToClassList(HudSelectors.CardSlotBlock);

            // Starts hidden, matching the state SetState leaves it in for a slot without artwork.
            _artwork = new VisualElement { pickingMode = PickingMode.Ignore };
            _artwork.AddToClassList(HudSelectors.CardSlotArtwork);
            _artwork.AddToClassList(HudSelectors.IsHidden);

            _accentBar = new VisualElement { pickingMode = PickingMode.Ignore };
            _accentBar.AddToClassList(HudSelectors.CardSlotAccent);

            // Hidden from construction, not merely from the first push. The bar paints nothing until a modifier
            // gives it a colour, so without this an un-pushed slot shows a transparent strip that still takes its
            // height out of the card.
            _accentBar.AddToClassList(HudSelectors.IsHidden);

            _costLabel = new Label { pickingMode = PickingMode.Ignore };
            _costLabel.AddToClassList(HudSelectors.CardSlotCost);

            _nameLabel = new Label { pickingMode = PickingMode.Ignore };
            _nameLabel.AddToClassList(HudSelectors.CardSlotName);

            // No field: the scrim is driven entirely by the card-slot--next descendant rule, so nothing here
            // ever needs to reach it again.
            var scrim = new VisualElement { pickingMode = PickingMode.Ignore };
            scrim.AddToClassList(HudSelectors.CardSlotScrim);

            // Stacking is child order — USS has no z-index — so the artwork goes on first to paint under the
            // accent bar, the cost and the name, and the scrim goes on last to paint over all of them. A child
            // added after the scrim would paint on top of it instead of under it, which is the whole cue lost.
            Add(_artwork);
            Add(_accentBar);
            Add(_costLabel);
            Add(_nameLabel);
            Add(scrim);
        }

        public HandSlotState State => _state;

        public bool IsAffordable => _isAffordable;

        /// <summary>Draws a card into the slot, or empties it.</summary>
        /// <param name="state">The card to draw. <see cref="HandSlotState.Empty" /> empties the slot.</param>
        /// <param name="artwork">The card's artwork, or null for none. Compare with <c>!= null</c>.</param>
        public void SetState(in HandSlotState state, Sprite artwork)
        {
            // Every member the slot draws is compared, including the two the presenter derives from CardId today.
            // Leaving those out would make the early-out correct only by coincidence, and silently wrong the day a
            // card is re-costed or renamed without its id changing.
            // The artwork comparison uses Unity's overloaded ==, so a destroyed sprite reads as null. Accepted: card art
            // is asset-backed and never destroyed at runtime.
            if (
                _isDrawn
                && (state.CardId == _state.CardId)
                && (state.Kind == _state.Kind)
                && (state.Accent == _state.Accent)
                && (state.Rarity == _state.Rarity)
                && (artwork == _artworkSprite)
                && (state.EnergyCost == _state.EnergyCost)
                && (state.DisplayName == _state.DisplayName)
            )
            {
                return;
            }

            _costLabel.text = state.IsFilled ? ResolveCostText(state.EnergyCost) : HudText.EmptySlot;
            _nameLabel.text = state.IsFilled ? state.DisplayName : HudText.EmptySlot;

            if (_accentClass != null)
            {
                _accentBar.RemoveFromClassList(_accentClass);
            }

            _accentClass = ResolveAccentClass(state.Accent);

            if (_accentClass != null)
            {
                _accentBar.AddToClassList(_accentClass);
            }

            // No class means no colour to paint — CardAccent.None, and equally an accent added to the enum that
            // nothing here maps yet. Either way the bar leaves layout rather than showing an unpainted strip.
            _accentBar.EnableInClassList(HudSelectors.IsHidden, _accentClass == null);

            bool hasArtwork = artwork != null;
            _artwork.style.backgroundImage = hasArtwork ? new StyleBackground(artwork) : new StyleBackground(StyleKeyword.None);
            _artwork.EnableInClassList(HudSelectors.IsHidden, !hasArtwork);

            if (_rarityClass != null)
            {
                RemoveFromClassList(_rarityClass);
            }

            _rarityClass = ResolveRarityClass(state.Rarity);

            if (_rarityClass != null)
            {
                AddToClassList(_rarityClass);
            }

            EnableInClassList(HudSelectors.CardSlotEmpty, !state.IsFilled);
            EnableInClassList(HudSelectors.CardSlotProtocol, state.Kind == HandSlotKind.Protocol);

            _state = state;
            _artworkSprite = artwork;
            _isDrawn = true;
        }

        /// <summary>Dims or restores the slot to report whether the player can pay for the card right now.</summary>
        /// <param name="isAffordable">Whether a Deploy priced at this card's cost is within the player's balance.</param>
        public void SetAffordable(bool isAffordable)
        {
            _isAffordable = isAffordable;
            EnableInClassList(HudSelectors.CardSlotUnaffordable, !isAffordable);
        }

        private static string ResolveAccentClass(CardAccent accent)
        {
            return accent switch
            {
                CardAccent.Baseline => HudSelectors.CardSlotAccentBaseline,
                CardAccent.Control => HudSelectors.CardSlotAccentControl,
                CardAccent.Explosive => HudSelectors.CardSlotAccentExplosive,
                CardAccent.Defensive => HudSelectors.CardSlotAccentDefensive,
                CardAccent.Corrosive => HudSelectors.CardSlotAccentCorrosive,
                _ => null,
            };
        }

        private static string ResolveRarityClass(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => HudSelectors.CardSlotRarityCommon,
                CardRarity.Rare => HudSelectors.CardSlotRarityRare,
                CardRarity.Epic => HudSelectors.CardSlotRarityEpic,
                CardRarity.Legendary => HudSelectors.CardSlotRarityLegendary,
                _ => null,
            };
        }

        private static string[] BuildCostTexts()
        {
            string[] texts = new string[MaxTabulatedCost + 1];

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = i.ToString(CultureInfo.InvariantCulture);
            }

            return texts;
        }

        private static string ResolveCostText(int energyCost)
        {
            if (energyCost is < 0 or > MaxTabulatedCost)
            {
                return energyCost.ToString(CultureInfo.InvariantCulture);
            }

            return _costTexts[energyCost];
        }
    }
}
