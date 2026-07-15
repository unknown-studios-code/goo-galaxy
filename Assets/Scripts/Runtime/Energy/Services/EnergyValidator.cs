using GooGalaxy.Runtime.Energy.Models;

namespace GooGalaxy.Runtime.Energy.Services
{
    /// <summary>
    /// Pure validation utility for energy expenditures.
    /// </summary>
    public static class EnergyValidator
    {
        /// <summary>
        /// Determines if the player can afford the specified energy cost.
        /// </summary>
        /// <param name="currentEnergy">The current energy value.</param>
        /// <param name="cost">The energy cost to check.</param>
        /// <returns>True if the cost is non-negative and less than or equal to current energy; otherwise, false.</returns>
        public static bool CanAfford(float currentEnergy, float cost)
        {
            if (cost < 0f)
            {
                return false;
            }

            return cost <= currentEnergy;
        }

        /// <summary>
        /// Validates the expenditure and deducts the cost from the current energy atomically by reference if affordable.
        /// </summary>
        /// <param name="currentEnergy">A reference to the mutable current energy float.</param>
        /// <param name="cost">The energy cost to spend.</param>
        /// <returns>Success if the transaction was approved and completed, or InsufficientEnergy otherwise.</returns>
        public static SpendResult TrySpend(ref float currentEnergy, float cost)
        {
            if (cost < 0f)
            {
                return SpendResult.InsufficientEnergy;
            }

            if (cost <= currentEnergy)
            {
                currentEnergy -= cost;
                return SpendResult.Success;
            }

            return SpendResult.InsufficientEnergy;
        }
    }
}
