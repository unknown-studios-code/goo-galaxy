using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Playtest HUD: an energy bar and a live troop count per player, plus the match result banner and the
    /// Reset button. Renders and raises intent only — it decides nothing and reads no game state directly.
    /// </summary>
    /// <remarks>
    /// Energy arrives through <c>MatchEvents.EnergyChanged</c>, so the bars stay correct without polling.
    /// Troop counts and the result come from the harness, which is the only thing that knows when a match ended.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class PlaytestHudView : MonoBehaviour
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        private const string RootName = "hud-root";
        private const string FillOneName = "energy-bar__fill-one";
        private const string FillTwoName = "energy-bar__fill-two";
        private const string ValueOneName = "energy-bar__value-one";
        private const string ValueTwoName = "energy-bar__value-two";
        private const string TroopsOneName = "energy-bar__troops-one";
        private const string TroopsTwoName = "energy-bar__troops-two";
        private const string ResultLabelName = "hud__result-label";
        private const string ResetButtonName = "hud__reset-button";
        private const string PlayerButtonName = "hud__player-button";
        private const string HandName = "hand";
        private const string ResultVisibleClass = "hud__result-label--visible";
        private const string CardClass = "hand__card";
        private const string CardNameClass = "hand__card-name";
        private const string CardCostClass = "hand__card-cost";
        private const string CardSelectedClass = "hand__card--selected";
        private const string CardUnaffordableClass = "hand__card--unaffordable";
        private const string PlayerButtonTwoClass = "hud__player-button--player-two";

        [Tooltip("Energy ceiling the bars are drawn against. Must match the EnergyConfig cap or the fill misreports.")]
        [Min(0.01f)]
        [SerializeField]
        private float _maxEnergy = 10f;

        [Tooltip("Placeholder card colours, matched by card id. A card with no entry here falls back to grey.")]
        [SerializeField]
        private CardColor[] _cardColors = Array.Empty<CardColor>();

        private UIDocument _document;
        private VisualElement _fillOne;
        private VisualElement _fillTwo;
        private Label _valueOne;
        private Label _valueTwo;
        private Label _troopsOne;
        private Label _troopsTwo;
        private Label _resultLabel;
        private Button _resetButton;
        private Button _playerButton;
        private VisualElement _hand;

        private readonly Dictionary<string, Button> _cardButtons = new();

        private string _selectedCardId;

        // NaN so the first energy update always publishes, whatever value it carries.
        private float _lastShownEnergyOne = float.NaN;
        private float _lastShownEnergyTwo = float.NaN;

        /// <summary>Raised when the player asks for a fresh match. The harness owns what "reset" actually does.</summary>
        public event Action ResetRequested;

        /// <summary>Raised when a card is picked, or with null when the pick is cleared by re-clicking it.</summary>
        public event Action<string> CardSelected;

        /// <summary>Raised when the active-player button is pressed. The harness decides who is active next.</summary>
        public event Action ActivePlayerToggleRequested;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = _document.rootVisualElement;

            if (root == null)
            {
                Debug.LogError("PlaytestHudView: the UIDocument has no root. Is a Source Asset and Panel Settings assigned?", this);
                return;
            }

            _fillOne = root.Q<VisualElement>(FillOneName);
            _fillTwo = root.Q<VisualElement>(FillTwoName);
            _valueOne = root.Q<Label>(ValueOneName);
            _valueTwo = root.Q<Label>(ValueTwoName);
            _troopsOne = root.Q<Label>(TroopsOneName);
            _troopsTwo = root.Q<Label>(TroopsTwoName);
            _resultLabel = root.Q<Label>(ResultLabelName);
            _resetButton = root.Q<Button>(ResetButtonName);
            _playerButton = root.Q<Button>(PlayerButtonName);
            _hand = root.Q<VisualElement>(HandName);

            if (_fillOne == null || _resetButton == null)
            {
                Debug.LogError($"PlaytestHudView: '{RootName}' is missing expected children. Did PlaytestHud.uxml change?", this);
                return;
            }

            _resetButton.RegisterCallback<ClickEvent>(HandleResetClicked);

            if (_playerButton != null)
            {
                _playerButton.RegisterCallback<ClickEvent>(HandlePlayerButtonClicked);
            }

            MatchEvents.EnergyChanged += HandleEnergyChanged;

            // The panel re-queries its elements here, so they carry USS defaults again — the quantiser must not
            // suppress the next update on the grounds that it already drew that value to the previous elements.
            _lastShownEnergyOne = float.NaN;
            _lastShownEnergyTwo = float.NaN;

            ClearResult();
        }

        private void OnDisable()
        {
            MatchEvents.EnergyChanged -= HandleEnergyChanged;

            if (_resetButton != null)
            {
                _resetButton.UnregisterCallback<ClickEvent>(HandleResetClicked);
            }

            if (_playerButton != null)
            {
                _playerButton.UnregisterCallback<ClickEvent>(HandlePlayerButtonClicked);
            }

            ClearHand();
        }

        /// <summary>
        /// Rebuilds the hand from the roster the harness supplies. Each card renders as a flat coloured
        /// rectangle carrying its name and energy cost — placeholder art, per the MVP scope.
        /// </summary>
        /// <param name="cards">The cards to show, in display order.</param>
        public void SetHand(IReadOnlyList<HandCard> cards)
        {
            if (_hand == null)
            {
                return;
            }

            ClearHand();

            if (cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                HandCard card = cards[i];
                var button = new Button();
                button.AddToClassList(CardClass);
                button.style.backgroundColor = ResolveCardColor(card.CardId);

                var cost = new Label(card.EnergyCost.ToString());
                cost.AddToClassList(CardCostClass);
                button.Add(cost);

                var name = new Label(card.DisplayName);
                name.AddToClassList(CardNameClass);
                button.Add(name);

                // The id is captured per button rather than read back from the event target, so a later
                // reordering of the hand cannot desynchronise the click from the card it draws.
                string cardId = card.CardId;
                button.RegisterCallback<ClickEvent>(_ => HandleCardClicked(cardId));

                _cardButtons[card.CardId] = button;
                _hand.Add(button);
            }

            ApplySelectionClasses();
        }

        /// <summary>Marks which card is currently picked, or clears the pick when given null.</summary>
        /// <param name="cardId">The picked card's id, or null for no pick.</param>
        public void SetSelectedCard(string cardId)
        {
            _selectedCardId = cardId;
            ApplySelectionClasses();
        }

        /// <summary>Dims the cards the active player cannot currently pay for.</summary>
        /// <param name="availableEnergy">The active player's current energy.</param>
        /// <param name="costs">Energy cost per card id, as shown in the hand.</param>
        public void SetAffordability(float availableEnergy, IReadOnlyDictionary<string, int> costs)
        {
            if (costs == null)
            {
                return;
            }

            foreach (KeyValuePair<string, Button> entry in _cardButtons)
            {
                bool canAfford = !costs.TryGetValue(entry.Key, out int cost) || availableEnergy >= cost;
                entry.Value.EnableInClassList(CardUnaffordableClass, !canAfford);
            }
        }

        /// <summary>Shows which player the hand currently plays for.</summary>
        /// <param name="playerId">The active player.</param>
        public void SetActivePlayer(int playerId)
        {
            if (_playerButton == null)
            {
                return;
            }

            _playerButton.text = $"Playing: P{playerId}";
            _playerButton.EnableInClassList(PlayerButtonTwoClass, playerId == PlayerTwoId);
        }

        /// <summary>Updates the live troop count shown next to each energy bar.</summary>
        /// <param name="playerOneTroops">Units player 1 currently controls.</param>
        /// <param name="playerTwoTroops">Units player 2 currently controls.</param>
        public void SetTroopCounts(int playerOneTroops, int playerTwoTroops)
        {
            if (_troopsOne == null || _troopsTwo == null)
            {
                return;
            }

            _troopsOne.text = playerOneTroops.ToString();
            _troopsTwo.text = playerTwoTroops.ToString();
        }

        /// <summary>Shows the end-of-match banner.</summary>
        /// <param name="message">The result to display, already phrased for a player.</param>
        public void ShowResult(string message)
        {
            if (_resultLabel == null)
            {
                return;
            }

            _resultLabel.text = message;
            _resultLabel.AddToClassList(ResultVisibleClass);
        }

        /// <summary>Hides the end-of-match banner, for the start of a fresh match.</summary>
        public void ClearResult()
        {
            if (_resultLabel == null)
            {
                return;
            }

            _resultLabel.text = string.Empty;
            _resultLabel.RemoveFromClassList(ResultVisibleClass);
        }

        /// <summary>
        /// Reports whether a screen-space point lands on a pickable HUD element, so board input under the HUD
        /// can be swallowed by the caller instead of falling through to the board's physics pick.
        /// </summary>
        /// <remarks>
        /// The HUD root ("hud-root", class "hud") is a full-screen invisible flex host and carries
        /// <c>picking-mode="Ignore"</c> as a UXML attribute in PlaytestHud.uxml for exactly this reason:
        /// <see cref="IPanel.Pick"/> reports a hit for any pickable element under the point, including
        /// ancestors, and a pickable full-screen root would make this method return true everywhere and
        /// permanently block the board. With the root ignored, a pick only succeeds over the actual widgets —
        /// the energy panel, footer, hand cards, and buttons — and returns null everywhere else, including
        /// over the ignored root itself. Picking mode is not a USS property: written in PlaytestHud.uss it
        /// warns "Unknown property" once at import, is dropped, and leaves the root pickable with no further
        /// signal, so the attribute must stay in the UXML.
        /// <para>
        /// The panel is resolved on every call rather than cached, because <c>UIDocument</c> attaches its root
        /// to the runtime panel in its own <c>OnEnable</c> and nothing orders that against this component's.
        /// A reference taken in <c>OnEnable</c> that lost the race would be null for the rest of the session,
        /// and this method would answer "not over the UI" for every point — silently, since the root itself is
        /// non-null by then and no guard fires.
        /// </para>
        /// </remarks>
        /// <param name="screenPosition">Pointer position in screen space, as read from the Input System.</param>
        /// <returns>True when the point lands on a pickable HUD element.</returns>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (_document == null)
            {
                return false;
            }

            // rootVisualElement and panel are plain C# objects, so the null-conditional is correct here; only
            // the UIDocument above needs Unity's overloaded comparison.
            IPanel panel = _document.rootVisualElement?.panel;

            if (panel == null)
            {
                return false;
            }

            // UI Toolkit panel space has its origin at the top-left with Y increasing downward; screen space
            // (and the Input System's pointer.position) has its origin at the bottom-left with Y increasing
            // upward. Picking with the raw screen point silently mirrors the hit test vertically —
            // RuntimePanelUtils.ScreenToPanel performs the flip.
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);

            return panel.Pick(panelPosition) != null;
        }

        // PERF: EnergyChanged fires every frame, but the bar renders one decimal. Quantising to what is visible
        // drops a per-frame string allocation and UI Toolkit layout pass that produced identical pixels.
        private void HandleEnergyChanged(int playerId, float newEnergy)
        {
            VisualElement fill = null;
            Label value = null;
            float lastShown;

            if (playerId == PlayerOneId)
            {
                fill = _fillOne;
                value = _valueOne;
                lastShown = _lastShownEnergyOne;
            }
            else if (playerId == PlayerTwoId)
            {
                fill = _fillTwo;
                value = _valueTwo;
                lastShown = _lastShownEnergyTwo;
            }
            else
            {
                return;
            }

            if (fill == null)
            {
                return;
            }

            float shown = Mathf.Round(newEnergy * 10f) * 0.1f;

            if (Mathf.Approximately(shown, lastShown))
            {
                return;
            }

            if (playerId == PlayerOneId)
            {
                _lastShownEnergyOne = shown;
            }
            else
            {
                _lastShownEnergyTwo = shown;
            }

            fill.style.width = Length.Percent(Mathf.Clamp01(shown / _maxEnergy) * 100f);

            if (value != null)
            {
                value.text = shown.ToString("F1");
            }
        }

        private void HandleResetClicked(ClickEvent evt)
        {
            ResetRequested?.Invoke();
        }

        private void HandlePlayerButtonClicked(ClickEvent evt)
        {
            ActivePlayerToggleRequested?.Invoke();
        }

        // Clicking the picked card again clears the pick, so there is always a way back to plain board moves.
        private void HandleCardClicked(string cardId)
        {
            CardSelected?.Invoke(_selectedCardId == cardId ? null : cardId);
        }

        private void ApplySelectionClasses()
        {
            foreach (KeyValuePair<string, Button> entry in _cardButtons)
            {
                entry.Value.EnableInClassList(CardSelectedClass, entry.Key == _selectedCardId);
            }
        }

        private Color ResolveCardColor(string cardId)
        {
            for (int i = 0; i < _cardColors.Length; i++)
            {
                if (_cardColors[i].CardId == cardId)
                {
                    return _cardColors[i].Color;
                }
            }

            return new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        private void ClearHand()
        {
            _cardButtons.Clear();

            if (_hand != null)
            {
                _hand.Clear();
            }
        }

        /// <summary>One card as the hand renders it. The harness owns which cards are in play.</summary>
        public readonly struct HandCard
        {
            public HandCard(string cardId, string displayName, int energyCost)
            {
                CardId = cardId;
                DisplayName = displayName;
                EnergyCost = energyCost;
            }

            public string CardId { get; }

            public string DisplayName { get; }

            public int EnergyCost { get; }
        }

        /// <summary>Placeholder colour for one card, matched by id. Authored in the Inspector.</summary>
        [Serializable]
        private struct CardColor
        {
            public string CardId;

            public Color Color;
        }
    }
}
