using System;

namespace GooGalaxy.Runtime.Energy.Services
{
    /// <summary>
    /// Pure mathematical utility for calculating energy regeneration tick state.
    /// </summary>
    public static class EnergyRegenerator
    {
        /// <summary>
        /// Calculates the new energy value after an elapsed time delta.
        /// </summary>
        /// <param name="currentEnergy">The current energy value.</param>
        /// <param name="deltaTime">The time passed in seconds.</param>
        /// <param name="regenRate">The effective regeneration rate per second.</param>
        /// <param name="maxEnergy">The maximum energy cap.</param>
        /// <returns>The updated energy value clamped to the max energy cap.</returns>
        public static float Tick(float currentEnergy, float deltaTime, float regenRate, float maxEnergy)
        {
            if (deltaTime <= 0f)
            {
                return currentEnergy;
            }

            return MathF.Min(currentEnergy + (regenRate * deltaTime), maxEnergy);
        }
    }
}
