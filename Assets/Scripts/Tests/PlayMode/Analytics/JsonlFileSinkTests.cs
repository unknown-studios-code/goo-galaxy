using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Analytics.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Analytics
{
    [TestFixture]
    public class JsonlFileSinkTests
    {
        private const int DefaultMatchOrdinal = 1;
        private const int DrainChunkCharacters = 8192;

        private string _rootDirectory;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(Application.temporaryCachePath, "AnalyticsSinkTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, true);
            }
        }

        [Test]
        public void Open_NewSession_TouchesNothingOnDisk()
        {
            // GIVEN
            var sink = new JsonlFileSink(_rootDirectory);

            // WHEN
            sink.Open(new AnalyticsSession("session-id", "Device", "OS", "1.0"));

            // THEN
            Assert.That(Directory.Exists(_rootDirectory), Is.False);
        }

        [Test]
        public void Open_AfterAPriorFault_ResetsIsFaulted()
        {
            // GIVEN
            (JsonlFileSink sink, AnalyticsBuffer buffer) = BuildSinkBlockedByAFile();
            LogAssert.Expect(LogType.Warning, BuildWarningPrefixRegex(AnalyticsLogMessages.SessionDirectoryUnavailableFormat));
            sink.Write(buffer);
            Assert.That(sink.IsFaulted, Is.True, "Test setup expects the first write to fault the sink.");

            // WHEN
            sink.Open(new AnalyticsSession("recovered-session", "Device", "OS", "1.0"));

            // THEN
            Assert.That(sink.IsFaulted, Is.False);
        }

        [Test]
        public void Write_FirstWriteWithRecords_CreatesTheSessionFileLazily()
        {
            // GIVEN
            var sink = new JsonlFileSink(_rootDirectory);
            sink.Open(new AnalyticsSession("session-id", "Device", "OS", "1.0"));
            AnalyticsBuffer buffer = BuildBufferWithOneRecord();

            // WHEN
            sink.Write(buffer);

            // THEN
            Assert.That(File.Exists(sink.FilePath), Is.True);
        }

        [Test]
        public void Write_MultipleRecords_EachLineHasTheJsonlShape()
        {
            // GIVEN
            var sink = new JsonlFileSink(_rootDirectory);
            sink.Open(new AnalyticsSession("session-id", "Device", "OS", "1.0"));
            var buffer = new AnalyticsBuffer(2);
            var first = AnalyticsRecord.ForPhaseChanged(0L, DefaultMatchOrdinal, MatchPhase.Standard);
            var second = AnalyticsRecord.ForPhaseChanged(1L, DefaultMatchOrdinal, MatchPhase.Overtime);
            buffer.TryAdd(in first);
            buffer.TryAdd(in second);

            // WHEN
            sink.Write(buffer);

            // THEN
            string[] lines = File.ReadAllLines(sink.FilePath);
            Assert.That((lines.Length, lines.All(line => line.StartsWith("{\"v\":1") && line.EndsWith("}"))), Is.EqualTo((2, true)));
        }

        [Test]
        public void Write_EmptyBuffer_WritesNoFile()
        {
            // GIVEN
            var sink = new JsonlFileSink(_rootDirectory);
            sink.Open(new AnalyticsSession("session-id", "Device", "OS", "1.0"));
            var buffer = new AnalyticsBuffer(1);

            // WHEN
            sink.Write(buffer);

            // THEN
            Assert.That(Directory.Exists(_rootDirectory), Is.False);
        }

        [Test]
        public void Write_SessionFilesExceedTheLimit_PrunesTheOldestFilesFirst()
        {
            // GIVEN
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260101-000000Z.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260102-000000Z.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260103-000000Z.jsonl"), "{}\n");
            var sink = new JsonlFileSink(_rootDirectory, maxSessionFiles: 3);
            sink.Open(new AnalyticsSession("20260104-000000Z", "Device", "OS", "1.0"));

            // WHEN
            sink.Write(BuildBufferWithOneRecord());

            // THEN
            Assert.That(
                ReadSessionFileNames(),
                Is.EqualTo(new[] { "session-20260102-000000Z.jsonl", "session-20260103-000000Z.jsonl", "session-20260104-000000Z.jsonl" })
            );
        }

        [Test]
        public void Write_FolderContainsNamesPruneDoesNotRecognize_TheyAreNeverDeleted()
        {
            // GIVEN
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(Path.Combine(_rootDirectory, "session-garbage.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-unnamed.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260101-000000Z.jsonl"), "{}\n");
            var sink = new JsonlFileSink(_rootDirectory, maxSessionFiles: 1);
            sink.Open(new AnalyticsSession("20260102-000000Z", "Device", "OS", "1.0"));

            // WHEN — maxSessionFiles of 1 prunes every session file this sink recognizes down to none, so the
            // one properly-named old file is the sole prune candidate and the two it cannot parse are untouched.
            sink.Write(BuildBufferWithOneRecord());

            // THEN
            Assert.That(ReadSessionFileNames(), Is.EqualTo(new[] { "session-20260102-000000Z.jsonl", "session-garbage.jsonl", "session-unnamed.jsonl" }));
        }

        [Test]
        public void Write_CollisionSuffixTen_SortsNewerThanCollisionSuffixTwo()
        {
            // GIVEN — a lexicographic sort would rank "_10" before "_2" and prune the wrong one; the sink
            // parses the suffix as a number instead.
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260101-000000Z.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260101-000000Z_2.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(_rootDirectory, "session-20260101-000000Z_10.jsonl"), "{}\n");
            var sink = new JsonlFileSink(_rootDirectory, maxSessionFiles: 2);
            sink.Open(new AnalyticsSession("20260102-000000Z", "Device", "OS", "1.0"));

            // WHEN
            sink.Write(BuildBufferWithOneRecord());

            // THEN
            Assert.That(ReadSessionFileNames(), Is.EqualTo(new[] { "session-20260101-000000Z_10.jsonl", "session-20260102-000000Z.jsonl" }));
        }

        [Test]
        public void Write_TwoSessionsOpenedWithTheSameId_TheSecondGetsANumberedSuffix()
        {
            // GIVEN
            var session = new AnalyticsSession("20260101-000000Z", "Device", "OS", "1.0");
            var firstSink = new JsonlFileSink(_rootDirectory);
            firstSink.Open(session);
            firstSink.Write(BuildBufferWithOneRecord());
            var secondSink = new JsonlFileSink(_rootDirectory);
            secondSink.Open(session);

            // WHEN
            secondSink.Write(BuildBufferWithOneRecord());

            // THEN
            Assert.That(Path.GetFileName(secondSink.FilePath), Is.EqualTo("session-20260101-000000Z_2.jsonl"));
        }

        [Test]
        public void Write_AnOldFileIsLocked_SkipsItWithoutFaultingTheSession()
        {
            // GIVEN
            Directory.CreateDirectory(_rootDirectory);
            string lockedPath = Path.Combine(_rootDirectory, "session-20260101-000000Z.jsonl");
            File.WriteAllText(lockedPath, "{}\n");
            var sink = new JsonlFileSink(_rootDirectory, maxSessionFiles: 1);
            sink.Open(new AnalyticsSession("20260102-000000Z", "Device", "OS", "1.0"));

            using FileStream lockingHandle = new(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

            // WHEN
            sink.Write(BuildBufferWithOneRecord());

            // THEN — the only prune candidate could not be deleted, so pruning left it in place and the
            // session still captured successfully instead of faulting.
            Assert.That((sink.IsFaulted, File.Exists(lockedPath)), Is.EqualTo((false, true)));
        }

        [Test]
        public void Write_SurrogatePairAtTheDrainChunkBoundary_RoundTripsAsValidUtf8()
        {
            // GIVEN — the sink drains its builder through a fixed-size character buffer, so this positions an
            // astral character's high surrogate as the very last character of the first drain chunk.
            var session = new AnalyticsSession("session-id", "Device", "OS", "1.0");
            var probeBuilder = new StringBuilder();
            var emptyRecord = AnalyticsRecord.ForCardDiscarded(0L, 0, 0, CardId.Empty, 0);
            AnalyticsRecordFormatter.TryAppendLine(probeBuilder, in emptyRecord, in session);
            string probeLine = probeBuilder.ToString();
            int cardValueStart = probeLine.IndexOf("\"card\":\"", StringComparison.Ordinal) + "\"card\":\"".Length;
            string padding = new('a', DrainChunkCharacters - 1 - cardValueStart);
            const string astralCharacter = "\U0001F600";
            string cardValue = padding + astralCharacter;
            var sink = new JsonlFileSink(_rootDirectory);
            sink.Open(session);
            var buffer = new AnalyticsBuffer(1);
            var record = AnalyticsRecord.ForCardDiscarded(0L, 0, 0, new CardId(cardValue), 0);
            buffer.TryAdd(in record);

            // WHEN
            sink.Write(buffer);

            // THEN
            string writtenText = File.ReadAllText(sink.FilePath, Encoding.UTF8);
            Assert.That(writtenText, Does.Contain("\"card\":\"" + cardValue + "\""));
        }

        [Test]
        public void Write_DirectoryPathIsAnExistingFile_LatchesFaulted()
        {
            // GIVEN
            (JsonlFileSink sink, AnalyticsBuffer buffer) = BuildSinkBlockedByAFile();
            LogAssert.Expect(LogType.Warning, BuildWarningPrefixRegex(AnalyticsLogMessages.SessionDirectoryUnavailableFormat));

            // WHEN
            sink.Write(buffer);

            // THEN
            Assert.That(sink.IsFaulted, Is.True);
        }

        [Test]
        public void Write_CalledAgainAfterAFault_DoesNotLogASecondWarning()
        {
            // GIVEN
            (JsonlFileSink sink, AnalyticsBuffer buffer) = BuildSinkBlockedByAFile();
            LogAssert.Expect(LogType.Warning, BuildWarningPrefixRegex(AnalyticsLogMessages.SessionDirectoryUnavailableFormat));
            sink.Write(buffer);
            Assert.That(sink.IsFaulted, Is.True, "Test setup expects the first write to fault the sink.");

            // WHEN
            sink.Write(buffer);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Write_AfterAFault_CreatesNoFile()
        {
            // GIVEN
            (JsonlFileSink sink, AnalyticsBuffer buffer) = BuildSinkBlockedByAFile();
            LogAssert.Expect(LogType.Warning, BuildWarningPrefixRegex(AnalyticsLogMessages.SessionDirectoryUnavailableFormat));
            sink.Write(buffer);
            Assert.That(sink.IsFaulted, Is.True, "Test setup expects the first write to fault the sink.");

            // WHEN
            sink.Write(buffer);

            // THEN
            Assert.That(sink.FilePath, Is.Null);
        }

        private static Regex BuildWarningPrefixRegex(string format)
        {
            string prefix = format[..format.IndexOf('{')];

            return new Regex("^" + Regex.Escape(prefix));
        }

        private static AnalyticsBuffer BuildBufferWithOneRecord()
        {
            var buffer = new AnalyticsBuffer(1);
            var record = AnalyticsRecord.ForPhaseChanged(0L, DefaultMatchOrdinal, MatchPhase.Standard);
            buffer.TryAdd(in record);

            return buffer;
        }

        private string[] ReadSessionFileNames()
        {
            return Directory.GetFiles(_rootDirectory, "session-*.jsonl").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        private (JsonlFileSink Sink, AnalyticsBuffer Buffer) BuildSinkBlockedByAFile()
        {
            Directory.CreateDirectory(_rootDirectory);
            string blockedPath = Path.Combine(_rootDirectory, "blocked");
            File.WriteAllText(blockedPath, string.Empty);

            var sink = new JsonlFileSink(blockedPath);
            sink.Open(new AnalyticsSession("session-id", "Device", "OS", "1.0"));

            return (sink, BuildBufferWithOneRecord());
        }
    }
}
