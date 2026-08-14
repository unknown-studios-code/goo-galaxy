using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Energy.Services
{
    internal static class MoveCostResolver
    {
        /// <remarks>
        /// The single source of the move pricing rule: the affordability check, the charge, and the refund all
        /// read their number here, so the three cannot disagree about what one action cost. Deterministic and
        /// allocation-free — it runs once per attempted move on the input path, so it stays a switch and a
        /// multiply with no lookup behind it. An undefined move type is priced at nothing, since a command the
        /// board rejects as invalid must never take a payment.
        /// </remarks>
        internal static float GetCost(MoveType moveType, int unitEnergyCost, in EnergyConfig config)
        {
            return moveType switch
            {
                MoveType.Deploy => unitEnergyCost,
                MoveType.Clone => unitEnergyCost * config.CloneCostMultiplier,
                MoveType.Jump => config.JumpEnergyCost,
                _ => 0f,
            };
        }
    }
}
