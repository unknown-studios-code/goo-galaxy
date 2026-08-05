using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Data
{
    [CreateAssetMenu(menuName = "Goo Galaxy/Cards/Card Data", fileName = "NewCardData")]
    public class CardDataSO : ScriptableObject, ICardData
    {
        [Header("Identity")]
        [Tooltip("Unique, stable identifier used as the lookup key in CardPresenter. Must not be empty.")]
        [SerializeField]
        private string _cardId;

        [Tooltip("Player-facing card name shown in the HUD and card inspector tools.")]
        [SerializeField]
        private string _displayName;

        [Tooltip("Whether this card deploys a troop unit or resolves a one-time spell effect.")]
        [SerializeField]
        private CardType _type;

        [Header("Energy")]
        [Tooltip("Energy cost required to play this card, in whole Energy units.")]
        [SerializeField]
        private int _energyCost = 1;

        [Header("Movement")]
        [Tooltip("Whether this card can perform a 1-hex Clone move.")]
        [SerializeField]
        private bool _canClone;

        [Tooltip("Whether this card can perform a 2-hex Jump move.")]
        [SerializeField]
        private bool _canJump;

        [Header("Protection")]
        [Tooltip("Whether this card requires two conversion events to flip instead of one.")]
        [SerializeField]
        private bool _hasArmor;

        public CardId CardId => new(_cardId);

        public string DisplayName => _displayName;

        public CardType Type => _type;

        public int EnergyCost => _energyCost;

        public bool CanClone => _canClone;

        public bool CanJump => _canJump;

        public bool HasArmor => _hasArmor;

        /// <summary>Replaces every authored field in one call, mirroring what the Inspector writes.</summary>
        internal void SetAuthoredData(string cardId, string displayName, CardType type, int energyCost, bool canClone, bool canJump, bool hasArmor)
        {
            _cardId = cardId;
            _displayName = displayName;
            _type = type;
            _energyCost = energyCost;
            _canClone = canClone;
            _canJump = canJump;
            _hasArmor = hasArmor;
        }

        private void OnValidate()
        {
            ValidateAuthoredData();
        }

        /// <summary>Warns when the asset cannot be registered because it has no id. Runs on every Inspector edit.</summary>
        internal void ValidateAuthoredData()
        {
            if (string.IsNullOrWhiteSpace(_cardId))
            {
                Debug.LogWarning($"{name}: CardId is empty. Assign a unique, stable id before referencing this card in a CardPresenter.", this);
            }
        }
    }
}
