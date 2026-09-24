using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GooGalaxy.Runtime.Analytics.Interfaces;
using GooGalaxy.Runtime.Analytics.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.Analytics.Services
{
    /// <summary>
    /// Writes each capture session to its own JSON Lines file, <c>session-{id}.jsonl</c>, in one folder, and keeps
    /// only the most recent sessions there. Nothing is ever uploaded.
    /// </summary>
    /// <remarks>
    /// <b>Nothing touches the disk until the first <see cref="Write" />.</b> <see cref="Open" /> only records the
    /// session; the folder is created, old sessions pruned, and the file named on the first write that has
    /// something to say.
    /// <para>
    /// <b>Synchronous by design.</b> A mobile app can be killed without warning right after it is paused, so the
    /// flush that pausing triggers has to have reached the file by the time it returns. Each write opens the file for
    /// append, streams the lines through a fixed-size UTF-8 scratch buffer — never materializing the whole payload as
    /// one string — and closes it, so no handle is held between flushes and a kill loses at most the records captured
    /// since the last one.
    /// </para>
    /// <para>
    /// <b>A failure never reaches gameplay.</b> Only the file-system exceptions a device can actually raise —
    /// <see cref="IOException" /> and <see cref="UnauthorizedAccessException" /> — are caught; the first one logs a
    /// single warning naming the path and latches <see cref="IsFaulted" />, and every later write is a no-op until
    /// the next <see cref="Open" />.
    /// </para>
    /// <para>
    /// <b>Pruning</b> runs once per session, before its file is created. It considers only files this sink names —
    /// <c>session-yyyyMMdd-HHmmssZ.jsonl</c>, optionally with a <c>_N</c> collision suffix — orders them by id and then
    /// by suffix, and deletes the oldest until at most <c>maxSessionFiles - 1</c> remain, leaving room for the new
    /// one. A file that cannot be deleted is skipped; pruning never faults the session. A session opened without an
    /// id is written under a timestamp-free fallback name that pruning never matches.
    /// </para>
    /// </remarks>
    public class JsonlFileSink : IAnalyticsSink
    {
        /// <summary>The folder name, under the app's persistent data path, sessions are written to in a build.</summary>
        public const string DefaultDirectoryName = "Analytics";

        /// <summary>How many session files are kept when no other limit is given.</summary>
        public const int DefaultMaxSessionFiles = 20;

        private const string FileNamePrefix = "session-";
        private const string FileExtension = ".jsonl";
        private const string FileSearchPattern = FileNamePrefix + "*" + FileExtension;
        private const char CollisionSeparator = '_';
        private const string FallbackSessionId = "unnamed";

        // The id is "yyyyMMdd-HHmmssZ": 8 digits, a dash, 6 digits and the UTC designator.
        private const int SessionIdLength = 16;
        private const int SessionIdDateLength = 8;
        private const char SessionIdTimeSeparator = '-';
        private const char SessionIdUtcDesignator = 'Z';
        private const int FirstCollisionSuffix = 2;

        // PERF: the builder is drained once it holds this many characters, so a flush of any size costs one fixed
        // scratch buffer rather than a string the size of the whole payload.
        private const int ChunkCharacters = 8 * 1024;
        private const int BuilderCapacity = ChunkCharacters * 2;

        // A single-byte buffer makes every Write reach the OS immediately; the scratch buffer already batches.
        private const int FileStreamBufferSize = 1;

        private static readonly UTF8Encoding _utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly StringBuilder _builder = new(BuilderCapacity);
        private readonly char[] _charScratch = new char[ChunkCharacters];
        private readonly byte[] _byteScratch = new byte[_utf8WithoutBom.GetMaxByteCount(ChunkCharacters)];
        private readonly Encoder _encoder = _utf8WithoutBom.GetEncoder();
        private readonly string _directoryPath;
        private readonly int _maxSessionFiles;

        private AnalyticsSession _session;
        private string _filePath;
        private bool _isOpen;
        private bool _isFaulted;

        /// <summary>Builds a sink that writes into a folder. Touches nothing on disk.</summary>
        /// <param name="directoryPath">
        /// The folder session files are written to; created on the first write if it does not exist. A test points
        /// this at a temporary folder.
        /// </param>
        /// <param name="maxSessionFiles">How many session files the folder may hold, the new one included. At least one.</param>
        /// <exception cref="ArgumentException">The folder path is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The file limit is below one.</exception>
        public JsonlFileSink(string directoryPath, int maxSessionFiles = DefaultMaxSessionFiles)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("An analytics sink needs a folder to write to.", nameof(directoryPath));
            }

            if (maxSessionFiles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSessionFiles), maxSessionFiles, "At least one session file must be kept.");
            }

            _directoryPath = directoryPath;
            _maxSessionFiles = maxSessionFiles;
        }

        public bool IsFaulted => _isFaulted;

        public string DirectoryPath => _directoryPath;

        /// <summary>
        /// The current session's file path, resolved on the session's first non-empty write; null before that and after
        /// <see cref="Close" />. Resolved is not the same as written: after a fault the path stays set even though
        /// nothing further reaches it.
        /// </summary>
        public string FilePath => _filePath;

        public void Open(in AnalyticsSession session)
        {
            _session = session;
            _filePath = null;
            _isFaulted = false;
            _isOpen = true;
        }

        public void Write(AnalyticsBuffer buffer)
        {
            if (!_isOpen || _isFaulted || (buffer == null) || (buffer.Count == 0))
            {
                return;
            }

            if ((_filePath == null) && !TryPrepareFile())
            {
                return;
            }

            FileStream stream = null;

            try
            {
                _builder.Clear();
                _encoder.Reset();

                for (int i = 0; i < buffer.Count; i++)
                {
                    if (!AnalyticsRecordFormatter.TryAppendLine(_builder, in buffer[i], in _session))
                    {
                        continue;
                    }

                    stream ??= new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read, FileStreamBufferSize);

                    if (_builder.Length >= ChunkCharacters)
                    {
                        DrainBuilder(stream, isFinal: false);
                    }
                }

                if (stream != null)
                {
                    DrainBuilder(stream, isFinal: true);
                }
            }
            catch (IOException exception)
            {
                Fault(AnalyticsLogMessages.SessionFileWriteFailedFormat, _filePath, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Fault(AnalyticsLogMessages.SessionFileWriteFailedFormat, _filePath, exception);
            }
            finally
            {
                stream?.Dispose();
                _builder.Clear();
            }
        }

        public void Close()
        {
            _isOpen = false;
            _filePath = null;
        }

        private static bool TryParseSessionFileName(string fileName, out string sessionId, out int collisionSuffix)
        {
            sessionId = null;
            collisionSuffix = 0;

            int idStart = FileNamePrefix.Length;
            int minimumLength = idStart + SessionIdLength + FileExtension.Length;

            if (
                (fileName.Length < minimumLength)
                || !fileName.StartsWith(FileNamePrefix, StringComparison.Ordinal)
                || !fileName.EndsWith(FileExtension, StringComparison.Ordinal)
                || !IsSessionId(fileName, idStart)
            )
            {
                return false;
            }

            int suffixStart = idStart + SessionIdLength;
            int suffixEnd = fileName.Length - FileExtension.Length;

            if (suffixStart != suffixEnd)
            {
                if ((fileName[suffixStart] != CollisionSeparator) || !TryParseDigits(fileName, suffixStart + 1, suffixEnd, out collisionSuffix))
                {
                    return false;
                }
            }

            sessionId = fileName.Substring(idStart, SessionIdLength);

            return true;
        }

        private static bool IsSessionId(string text, int start)
        {
            for (int i = 0; i < SessionIdLength; i++)
            {
                char character = text[start + i];

                bool isValid = i switch
                {
                    SessionIdDateLength => character == SessionIdTimeSeparator,
                    SessionIdLength - 1 => character == SessionIdUtcDesignator,
                    _ => character is >= '0' and <= '9',
                };

                if (!isValid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseDigits(string text, int start, int end, out int value)
        {
            value = 0;

            if (start >= end)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                char character = text[i];

                if (character is < '0' or > '9')
                {
                    return false;
                }

                value = (value * 10) + (character - '0');
            }

            return true;
        }

        private static int CompareSessionFiles((string Path, string Id, int Suffix) left, (string Path, string Id, int Suffix) right)
        {
            int byId = string.CompareOrdinal(left.Id, right.Id);

            return byId != 0 ? byId : left.Suffix.CompareTo(right.Suffix);
        }

        // A file another process holds, or one already gone, is left for the next session's prune rather than
        // costing this session its capture.
        private static bool TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private bool TryPrepareFile()
        {
            try
            {
                Directory.CreateDirectory(_directoryPath);
                PruneOldSessions();
                _filePath = ResolveFilePath();

                return true;
            }
            catch (IOException exception)
            {
                Fault(AnalyticsLogMessages.SessionDirectoryUnavailableFormat, _directoryPath, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Fault(AnalyticsLogMessages.SessionDirectoryUnavailableFormat, _directoryPath, exception);
            }

            return false;
        }

        private void PruneOldSessions()
        {
            string[] candidates = Directory.GetFiles(_directoryPath, FileSearchPattern);
            var sessionFiles = new List<(string Path, string Id, int Suffix)>(candidates.Length);

            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryParseSessionFileName(Path.GetFileName(candidates[i]), out string sessionId, out int collisionSuffix))
                {
                    sessionFiles.Add((candidates[i], sessionId, collisionSuffix));
                }
            }

            int excess = sessionFiles.Count - (_maxSessionFiles - 1);

            if (excess <= 0)
            {
                return;
            }

            sessionFiles.Sort(CompareSessionFiles);

            for (int i = 0; i < excess; i++)
            {
                TryDeleteFile(sessionFiles[i].Path);
            }
        }

        private string ResolveFilePath()
        {
            string sessionId = string.IsNullOrEmpty(_session.Id) ? FallbackSessionId : _session.Id;
            string filePath = Path.Combine(_directoryPath, FileNamePrefix + sessionId + FileExtension);

            for (int collision = FirstCollisionSuffix; File.Exists(filePath); collision++)
            {
                filePath = Path.Combine(_directoryPath, $"{FileNamePrefix}{sessionId}{CollisionSeparator}{collision}{FileExtension}");
            }

            return filePath;
        }

        // The encoder is stateful, so a surrogate pair split across two slices is still encoded as one character; the
        // final slice flushes whatever it holds.
        private void DrainBuilder(FileStream stream, bool isFinal)
        {
            int length = _builder.Length;
            int offset = 0;

            while (offset < length)
            {
                int count = Math.Min(ChunkCharacters, length - offset);

                _builder.CopyTo(offset, _charScratch, 0, count);
                offset += count;

                int byteCount = _encoder.GetBytes(_charScratch, 0, count, _byteScratch, 0, isFinal && (offset == length));
                stream.Write(_byteScratch, 0, byteCount);
            }

            _builder.Clear();
        }

        private void Fault(string format, string path, Exception exception)
        {
            _isFaulted = true;
            Debug.LogWarning(string.Format(format, path, exception.Message));
        }
    }
}
