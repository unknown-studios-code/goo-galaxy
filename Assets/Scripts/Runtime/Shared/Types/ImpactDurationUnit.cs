namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// What an authored impact's duration counts. Lives in Shared because cards author the value and the
    /// board's ability rules dispatch on it, so neither assembly has to depend on the other.
    /// </summary>
    /// <remarks>
    /// Values are explicit because they are authored into card assets and will travel to the client with the
    /// card definition: adding a member is safe, renumbering or reordering one silently repoints every asset
    /// already saved with the old number.
    /// <para>
    /// <see cref="ActionWindows"/> is deliberately zero. Every card asset authored before this field existed
    /// deserializes its missing value as zero, and action windows are what those durations already meant — so
    /// the whole existing roster loads with its behaviour unchanged. Any other numbering would silently
    /// reinterpret every saved status and hazard as a duration in seconds.
    /// </para>
    /// </remarks>
    public enum ImpactDurationUnit
    {
        /// <summary>
        /// Deployments. A status counts defender windows and a hazard counts owner windows; either way the
        /// clock only advances when somebody deploys.
        /// </summary>
        ActionWindows = 0,

        /// <summary>
        /// Seconds, counted down every frame from the match's scaled clock, so a paused match freezes them.
        /// Only a fuse is measured this way — it is the one effect that resolves without anybody acting.
        /// </summary>
        Seconds = 1,
    }
}
