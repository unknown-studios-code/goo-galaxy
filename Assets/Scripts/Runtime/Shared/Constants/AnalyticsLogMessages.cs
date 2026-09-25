namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the local analytics capture: the file and sink faults a tester or an integrator has to act on,
    /// and the wiring fault that leaves capture off.
    /// </summary>
    /// <remarks>
    /// Every file or sink fault is logged as a single warning, never an error: capture is a diagnostic aid, and a device that
    /// cannot write it must still play. Each message therefore says that capture has stopped for the rest of the
    /// session, so a missing file is not mistaken for a quiet one. Every message that takes arguments carries the
    /// <c>Format</c> suffix.
    /// </remarks>
    public static class AnalyticsLogMessages
    {
        public const string SessionDirectoryUnavailableFormat =
            "Analytics could not prepare its session folder '{0}': {1} Capture is disabled for the rest of this session; gameplay is "
            + "unaffected. Check that the device has free storage and that the app can write to its persistent data folder.";

        public const string SessionFileWriteFailedFormat =
            "Analytics could not write to session file '{0}': {1} Capture is disabled for the rest of this session and the records "
            + "not yet written are lost; gameplay is unaffected. Check that the device has free storage and that nothing else holds the file open.";

        public const string SinkWriteThrewFormat =
            "AnalyticsController's sink threw {0} while writing buffered records: {1} Capture is disabled for the rest of this session "
            + "and those records are lost; gameplay is unaffected. An IAnalyticsSink must report failures through IsFaulted rather than throw — fix the sink.";

        public const string SinkMissing =
            "AnalyticsController was injected with a null IAnalyticsSink, so nothing will be captured this session. A sink is required "
            + "even when capture is switched off; check what GameLifetimeScope.CreateAnalyticsSink returns.";
    }
}
