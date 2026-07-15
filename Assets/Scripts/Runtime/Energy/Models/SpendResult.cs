namespace GooGalaxy.Runtime.Energy.Models
{
    /// <summary>
    /// Represents the outcome of an attempted energy expenditure.
    /// </summary>
    public enum SpendResult
    {
        /// <summary>
        /// The energy cost was affordable and successfully deducted.
        /// </summary>
        Success,

        /// <summary>
        /// The energy cost could not be deducted because the player lacked sufficient energy.
        /// </summary>
        InsufficientEnergy,
    }
}
