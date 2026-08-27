using System.Globalization;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// One player's live unit count, tinted with that player's faction colour.
    /// </summary>
    /// <remarks>
    /// <b>The tint follows the player id, not the side of the screen.</b> The board colours units by id — player
    /// one cyan, player two magenta — so a badge tinted by seat would contradict the board in every match the
    /// local player takes the second seat.
    /// <para>
    /// Scores are drawn from a table rather than composed, because a capture can flip several units in one
    /// landing and the score is republished for each side that moved.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class ScoreBadgeElement : VisualElement
    {
        // The two seat ids the rest of the runtime already agrees on. MatchController holds them privately and
        // seeds the energy states and unit colours from the same pair; a networked session will hand them down.
        private const int PlayerOneId = 1;

        private const int PlayerTwoId = 2;

        // The board holds 61 sectors, so no legal score reaches this. Above it the value is composed instead.
        private const int MaxTabulatedScore = 99;

        private static readonly string[] _scoreTexts = BuildScoreTexts();

        private readonly Label _valueLabel;

        private int _score = -1;
        private int _playerId = PlayerSlot.UnassignedId;

        public ScoreBadgeElement()
        {
            AddToClassList(HudSelectors.ScoreBadgeBlock);
            pickingMode = PickingMode.Ignore;

            _valueLabel = new Label { pickingMode = PickingMode.Ignore };
            _valueLabel.AddToClassList(HudSelectors.ScoreBadgeValue);

            Add(_valueLabel);
        }

        /// <summary>The unit count this badge last drew, or -1 before it drew anything.</summary>
        public int Score => _score;

        /// <summary>The player this badge is tinted for, or zero while no seat has been assigned to it.</summary>
        public int PlayerId => _playerId;

        /// <summary>Draws a player's live unit count.</summary>
        /// <param name="unitCount">The count to draw. Negative values are drawn as they arrive.</param>
        public void SetScore(int unitCount)
        {
            if (unitCount == _score)
            {
                return;
            }

            _score = unitCount;
            _valueLabel.text = ResolveScoreText(unitCount);
        }

        /// <summary>Tints the badge for the player whose count it carries.</summary>
        /// <param name="playerId">The seat id. Anything other than the two known ids leaves the badge untinted.</param>
        public void SetPlayer(int playerId)
        {
            _playerId = playerId;

            EnableInClassList(HudSelectors.ScoreBadgeFactionOne, playerId == PlayerOneId);
            EnableInClassList(HudSelectors.ScoreBadgeFactionTwo, playerId == PlayerTwoId);
        }

        private static string[] BuildScoreTexts()
        {
            string[] texts = new string[MaxTabulatedScore + 1];

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = i.ToString(CultureInfo.InvariantCulture);
            }

            return texts;
        }

        private static string ResolveScoreText(int unitCount)
        {
            if (unitCount is < 0 or > MaxTabulatedScore)
            {
                return unitCount.ToString(CultureInfo.InvariantCulture);
            }

            return _scoreTexts[unitCount];
        }
    }
}
