using System;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>One authored starting unit: which card it is, who owns it, and where it starts.</summary>
    /// <remarks>
    /// A mutable <c>struct</c> rather than the <c>readonly struct</c> the rest of the shared value types use:
    /// Unity deserializes an array of these in place, straight into the Match Config asset, and cannot write
    /// into a readonly one. The fields are public because there is nothing here to guard — a
    /// <c>[SerializeField] private</c> field inside a <c>[Serializable]</c> struct would serialize identically,
    /// so the choice buys authoring flatness rather than serialization. The whole opening position is validated
    /// together and seeded together — see <c>MatchStartResult.InvalidPlacement</c>.
    /// </remarks>
    [Serializable]
    public struct StartingPlacement
    {
        [Tooltip("Must match the Card Id authored on a CardDataSO registered with the CardPresenter.")]
        public string CardId;

        [Tooltip(
            "Unique across starting units. Keep these below 1000 — every unit spawned during the match, clones and deploys alike, is numbered "
                + "from there up."
        )]
        public int UnitId;

        [Tooltip(
            "Which side owns the unit. Only 1 and 2 are played: a unit authored for any other id is seeded onto the board but has no energy, no "
                + "deck, and is counted towards neither player's score."
        )]
        public int PlayerId;

        [Tooltip("Axial Q of the starting hex. Must be a free, unblocked cell inside the radius authored on the Grid Layout asset, or no match starts.")]
        public int Q;

        [Tooltip("Axial R of the starting hex. Same bounds as Q, and the pair must name a cell no other placement already claims.")]
        public int R;
    }
}
