using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Analytics.Interfaces;
using GooGalaxy.Runtime.Analytics.Models;

namespace GooGalaxy.Tests.PlayMode.Analytics
{
    internal sealed class FakeAnalyticsSink : IAnalyticsSink
    {
        public List<AnalyticsSession> OpenedSessions { get; } = new();

        public List<AnalyticsRecord> WrittenRecords { get; } = new();

        public bool IsFaulted { get; set; }

        public bool ShouldThrowOnWrite { get; set; }

        public int CloseCallCount { get; private set; }

        public void Open(in AnalyticsSession session)
        {
            OpenedSessions.Add(session);
        }

        public void Write(AnalyticsBuffer buffer)
        {
            if (ShouldThrowOnWrite)
            {
                throw new InvalidOperationException("Fake sink failure.");
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                WrittenRecords.Add(buffer[i]);
            }
        }

        public void Close()
        {
            CloseCallCount++;
        }
    }
}
