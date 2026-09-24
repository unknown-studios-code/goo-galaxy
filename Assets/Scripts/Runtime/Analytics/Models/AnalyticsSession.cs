namespace GooGalaxy.Runtime.Analytics.Models
{
    /// <summary>
    /// The facts about one capture session that never change while it runs: the id its file is named after, and the
    /// device context the <see cref="AnalyticsEventType.SessionStart" /> line reports.
    /// </summary>
    /// <remarks>
    /// Captured once, when the session opens, and handed to the sink so no record has to carry the device strings. Nothing in
    /// here identifies a person or a device instance: the model and OS strings are shared by every unit of that
    /// hardware, and no unique device identifier is ever read. Any field may be null, which is written as an empty
    /// string.
    /// </remarks>
    public readonly struct AnalyticsSession
    {
        public AnalyticsSession(string id, string deviceModel, string operatingSystem, string appVersion)
        {
            Id = id;
            DeviceModel = deviceModel;
            OperatingSystem = operatingSystem;
            AppVersion = appVersion;
        }

        /// <summary>The session id the file is named after, e.g. <c>20260924-101500Z</c> (UTC).</summary>
        public string Id { get; }

        public string DeviceModel { get; }

        public string OperatingSystem { get; }

        public string AppVersion { get; }
    }
}
