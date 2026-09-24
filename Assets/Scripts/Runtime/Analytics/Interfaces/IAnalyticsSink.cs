using GooGalaxy.Runtime.Analytics.Models;

namespace GooGalaxy.Runtime.Analytics.Interfaces
{
    /// <summary>
    /// A write-only destination for captured analytics records. The controller that captures them never knows where
    /// they end up, which is what lets a test point capture at a temporary folder or at an in-memory fake.
    /// </summary>
    /// <remarks>
    /// <b>An implementation never throws into gameplay.</b> A failure it cannot recover from is reported once and
    /// latched as <see cref="IsFaulted" />; every <see cref="Write" /> after that is a silent no-op until the next
    /// <see cref="Open" />. Calls arrive on the main thread,
    /// in the order <see cref="Open" />, any number of <see cref="Write" />, then <see cref="Close" />.
    /// </remarks>
    public interface IAnalyticsSink
    {
        /// <summary>
        /// Whether a failure has disabled the sink for the rest of the session. Once true it stays true until the next
        /// <see cref="Open" />.
        /// </summary>
        public bool IsFaulted { get; }

        /// <summary>Starts a session. Nothing needs to be written yet; an implementation may defer its first I/O.</summary>
        /// <param name="session">The session every later record belongs to, including the device context.</param>
        public void Open(in AnalyticsSession session);

        /// <summary>Writes every record the buffer holds, oldest first, and returns once they are written.</summary>
        /// <remarks>
        /// The buffer is borrowed for the call: the caller clears and refills it afterwards, so an implementation must
        /// not keep a reference to it. An empty buffer writes nothing.
        /// </remarks>
        /// <param name="buffer">The records to write. Must not be null.</param>
        public void Write(AnalyticsBuffer buffer);

        /// <summary>Ends the session. A later <see cref="Write" /> without a new <see cref="Open" /> writes nothing.</summary>
        public void Close();
    }
}
