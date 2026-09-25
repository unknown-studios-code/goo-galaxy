using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Unity.Profiling;

namespace GooGalaxy.Tests.Utils
{
    /// <summary>
    /// Passes when a delegate makes no managed allocation on the thread that runs it, and reports how many it made when it
    /// fails.
    /// </summary>
    /// <remarks>
    /// Use this, not Unity's <c>Is.Not.AllocatingGCMemory()</c>. Unity's constraint (com.unity.test-framework 1.6.0) scopes
    /// the process-wide <c>GC.Alloc</c> recorder to the calling thread, runs the delegate, disables the recorder, widens it
    /// back to every thread, and only then reads the count. That it counts other threads' allocations around that switch
    /// is not visible in the source; it was established by measurement: with one other thread allocating, it failed a
    /// provably allocation-free delegate in up to one run in five hundred, and in about three empty measurements in a
    /// thousand with three — the intermittent CI failures of GOOM-36. Its <c>Not</c> form also drops the count from the failure message.
    /// Revisit when the package version changes.
    /// <para>
    /// This constraint measures with a private <see cref="ProfilerRecorder" /> bound to the calling thread when it is
    /// created, the way com.unity.test-framework.performance measures allocations, so there is no shared filter to switch
    /// or to be left switched by someone else. Under the same load it counted no foreign allocation in over thirty-seven
    /// million measurements, including while Unity's constraint widened the shared recorder between them.
    /// </para>
    /// <para>
    /// The count is of allocations, not bytes: this runtime has no per-thread byte counter.
    /// <see cref="GC.GetAllocatedBytesForCurrentThread" /> returns 0, the recorder's sample value is elapsed time, and the
    /// <c>GC Allocated In Frame</c> profiler counter sums every thread. Find the allocation itself in the Profiler with
    /// allocation call stacks enabled.
    /// </para>
    /// <para>
    /// Only the calling thread is measured, so an allocation made by a scheduled callback or on another thread is not
    /// seen. Warm the path up before measuring: a first call's JIT and type initialization allocate.
    /// </para>
    /// </remarks>
    public sealed class AllocatesNothingConstraint : Constraint
    {
        private const string GcAllocMarkerName = "GC.Alloc";
        private const int RecorderCapacity = 1;
        private const string RecorderUnavailableMessage =
            "The GC.Alloc profiler recorder is unavailable on this platform, so allocations cannot be measured. Run allocation tests in the Editor or a development build.";
        private const string RecorderProducedNoSampleMessage =
            "The GC.Alloc profiler recorder produced no sample for the measured delegate, so its allocations are unknown.";

        private const ProfilerRecorderOptions RecorderOptions =
            ProfilerRecorderOptions.WrapAroundWhenCapacityReached
            | ProfilerRecorderOptions.SumAllSamplesInFrame
            | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;

        public override string Description => "a delegate that makes no managed allocation on the calling thread";

        /// <summary>Runs <paramref name="actual" /> once and counts the managed allocations it made on this thread.</summary>
        /// <param name="actual">The code to measure.</param>
        /// <returns>A result that succeeds when the count is zero, and names the count when it is not.</returns>
        /// <exception cref="ArgumentException"><paramref name="actual" /> is not a <see cref="TestDelegate" />.</exception>
        /// <exception cref="InvalidOperationException">The profiler recorder is unavailable, so nothing could be measured.</exception>
        public override ConstraintResult ApplyTo(object actual)
        {
            if (actual is not TestDelegate code)
            {
                throw new ArgumentException($"The actual value must be a {nameof(TestDelegate)}, but was {actual?.GetType().Name ?? "null"}.", nameof(actual));
            }

            return new AllocationCountResult(this, actual, CountAllocations(code));
        }

        /// <summary>
        /// Runs <paramref name="del" /> once, discarding its value, and counts the managed allocations it made on this
        /// thread.
        /// </summary>
        /// <param name="del">The code to measure.</param>
        /// <returns>A result that succeeds when the count is zero, and names the count when it is not.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="del" /> is null.</exception>
        /// <exception cref="InvalidOperationException">The profiler recorder is unavailable, so nothing could be measured.</exception>
        public override ConstraintResult ApplyTo<TActual>(ActualValueDelegate<TActual> del)
        {
            if (del == null)
            {
                throw new ArgumentNullException(nameof(del));
            }

            return new AllocationCountResult(this, del, CountAllocations(del));
        }

        private static long CountAllocations(TestDelegate code)
        {
            ProfilerRecorder recorder = CreateRecorder();

            try
            {
                recorder.Start();
                code();
                recorder.Stop();

                return ReadCount(recorder);
            }
            finally
            {
                recorder.Dispose();
            }
        }

        private static long CountAllocations<TActual>(ActualValueDelegate<TActual> del)
        {
            ProfilerRecorder recorder = CreateRecorder();

            try
            {
                recorder.Start();
                _ = del();
                recorder.Stop();

                return ReadCount(recorder);
            }
            finally
            {
                recorder.Dispose();
            }
        }

        private static ProfilerRecorder CreateRecorder()
        {
            var recorder = new ProfilerRecorder(ProfilerCategory.Memory, GcAllocMarkerName, RecorderCapacity, RecorderOptions);

            if (!recorder.Valid)
            {
                recorder.Dispose();

                throw new InvalidOperationException(RecorderUnavailableMessage);
            }

            return recorder;
        }

        private static long ReadCount(ProfilerRecorder recorder)
        {
            if (recorder.Count == 0)
            {
                throw new InvalidOperationException(RecorderProducedNoSampleMessage);
            }

            return recorder.GetSample(0).Count;
        }

        private sealed class AllocationCountResult : ConstraintResult
        {
            private readonly long _allocationCount;

            public AllocationCountResult(IConstraint constraint, object actual, long allocationCount)
                : base(constraint, actual, allocationCount == 0)
            {
                _allocationCount = allocationCount;
            }

            public override void WriteActualValueTo(MessageWriter writer)
            {
                writer.Write($"{_allocationCount} managed allocation(s) on the calling thread");
            }
        }
    }
}
