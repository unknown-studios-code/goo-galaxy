using GooGalaxy.Runtime.Shared.Commands;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>Which submission path a <see cref="MoveOption" /> takes.</summary>
    public enum MoveOptionKind
    {
        /// <summary>A Deploy, a Clone or a Jump: one unit onto one hex, resolved as a <see cref="MoveCommand" />.</summary>
        BoardMove = 0,

        /// <summary>A Protocol played onto a cluster of hexes, resolved as a <see cref="SpellCommand" />.</summary>
        Protocol = 1,
    }
}
