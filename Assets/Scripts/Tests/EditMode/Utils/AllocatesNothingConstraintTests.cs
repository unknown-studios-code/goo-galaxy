using System;
using System.Collections.Generic;
using System.Threading;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;

namespace GooGalaxy.Tests.EditMode.Utils
{
    [TestFixture]
    public class AllocatesNothingConstraintTests
    {
        private const int SinkLength = 3;
        private const int WorkerAllocationCount = 1000;
        private const int WorkerJoinTimeoutMilliseconds = 5000;
        private const int StressThreadCount = 3;
        private const int StressMeasurementCount = 200_000;

        private readonly object[] _sink = new object[SinkLength];
        private readonly object[] _workerSink = new object[WorkerAllocationCount];
        private readonly List<Thread> _stressThreads = new(StressThreadCount);

        private Thread _worker;
        private ManualResetEventSlim _workerStarted;
        private ManualResetEventSlim _workerFinished;
        private volatile bool _isStopping;

        [TearDown]
        public void TearDown()
        {
            _isStopping = true;
            _workerStarted?.Set();
            _worker?.Join(WorkerJoinTimeoutMilliseconds);

            for (int i = 0; i < _stressThreads.Count; i++)
            {
                _stressThreads[i].Join(WorkerJoinTimeoutMilliseconds);
            }

            _workerStarted?.Dispose();
            _workerFinished?.Dispose();
            _worker = null;
            _workerStarted = null;
            _workerFinished = null;
            _stressThreads.Clear();
            _isStopping = false;
            Array.Clear(_sink, 0, _sink.Length);
            Array.Clear(_workerSink, 0, _workerSink.Length);
        }

        [Test]
        [Category("Allocation")]
        public void ApplyTo_DelegateThatDoesNotAllocate_Succeeds()
        {
            // GIVEN — run once outside the measurement, so its JIT is not what gets counted.
            TestDelegate code = CopyWithoutAllocating;
            code();

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        [Category("Allocation")]
        public void ApplyTo_DelegateThatAllocates_Fails()
        {
            // GIVEN
            TestDelegate code = AllocateThreeObjects;
            code();

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        [Category("Allocation")]
        public void ApplyTo_ValueReturningDelegateThatDoesNotAllocate_Succeeds()
        {
            // GIVEN
            ActualValueDelegate<object> code = ReturnWithoutAllocating;
            code();

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        [Category("Allocation")]
        public void ApplyTo_ValueReturningDelegateThatAllocates_Fails()
        {
            // GIVEN
            ActualValueDelegate<object> code = ReturnANewObject;
            code();

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        [Category("Allocation")]
        [Timeout(10000)]
        public void ApplyTo_AnotherThreadAllocatesDuringTheDelegate_Succeeds()
        {
            // GIVEN — one full handshake first, so the measured Set/Wait pair allocates nothing of its own. A counter that
            // summed every thread would fail here; Unity's constraint passes it too, because its leak is at the switch
            // around the window rather than inside it (see the stress test below).
            StartAllocatingWorker();
            TestDelegate code = RunWorkerRound;
            code();

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        [Category("Allocation")]
        [Explicit("GOOM-36 stress: probabilistic by construction")]
        public void ApplyTo_OtherThreadsAllocatingAcrossManyMeasurements_NeverCountsTheirAllocations()
        {
            // GIVEN — the shape that failed Unity's constraint: many near-empty measurements while other threads allocate
            // flat out, so a sample in flight at the edge of a window is as likely as it gets.
            TestDelegate code = CopyWithoutAllocating;
            code();
            StartStressThreads();
            var constraint = new AllocatesNothingConstraint();

            // WHEN
            int countedForeignAllocations = CountFailedMeasurements(constraint, code);

            // THEN
            Assert.That(countedForeignAllocations, Is.EqualTo(0));
        }

        [Test]
        public void ApplyTo_ActualIsNotATestDelegate_ThrowsArgumentException()
        {
            // GIVEN
            var constraint = new AllocatesNothingConstraint();

            // WHEN / THEN
            Assert.Throws<ArgumentException>(() => constraint.ApplyTo(new object()));
        }

        [Test]
        [Category("Allocation")]
        public void WriteMessageTo_ResultOfDelegateThatAllocatesThreeObjects_NamesTheCount()
        {
            // GIVEN
            TestDelegate code = AllocateThreeObjects;
            code();
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);
            var writer = new TextMessageWriter();

            // WHEN
            result.WriteMessageTo(writer);

            // THEN
            Assert.That(writer.ToString(), Does.Contain("3 managed allocation(s) on the calling thread"));
        }

        [Test]
        [Category("Allocation")]
        public void AssertThat_DelegateThatAllocatesThreeObjects_ThrowsWithTheCountInTheMessage()
        {
            // GIVEN
            TestDelegate code = AllocateThreeObjects;
            code();

            // WHEN
            AssertionException failure = Assert.Throws<AssertionException>(() => Assert.That(code, new AllocatesNothingConstraint()));

            // THEN
            Assert.That(failure.Message, Does.Contain("3 managed allocation(s) on the calling thread"));
        }

        private static int CountFailedMeasurements(AllocatesNothingConstraint constraint, TestDelegate code)
        {
            int failed = 0;

            for (int i = 0; i < StressMeasurementCount; i++)
            {
                if (!constraint.ApplyTo(code).IsSuccess)
                {
                    failed++;
                }
            }

            return failed;
        }

        private void CopyWithoutAllocating()
        {
            _sink[0] = _sink[1];
        }

        private void AllocateThreeObjects()
        {
            _sink[0] = new object();
            _sink[1] = new object();
            _sink[2] = new object();
        }

        private object ReturnWithoutAllocating()
        {
            return _sink[0];
        }

        private object ReturnANewObject()
        {
            return new object();
        }

        private void StartAllocatingWorker()
        {
            _workerStarted = new ManualResetEventSlim(false);
            _workerFinished = new ManualResetEventSlim(false);
            _worker = new Thread(RunAllocatingWorker) { IsBackground = true };
            _worker.Start();
        }

        private void RunAllocatingWorker()
        {
            while (true)
            {
                _workerStarted.Wait();
                _workerStarted.Reset();

                if (_isStopping)
                {
                    return;
                }

                for (int i = 0; i < _workerSink.Length; i++)
                {
                    _workerSink[i] = new object();
                }

                _workerFinished.Set();
            }
        }

        private void RunWorkerRound()
        {
            _workerFinished.Reset();
            _workerStarted.Set();
            _workerFinished.Wait();
        }

        private void StartStressThreads()
        {
            for (int i = 0; i < StressThreadCount; i++)
            {
                var thread = new Thread(AllocateUntilStopped) { IsBackground = true };
                _stressThreads.Add(thread);
                thread.Start();
            }
        }

        private void AllocateUntilStopped()
        {
            int slot = 0;

            while (!_isStopping)
            {
                _workerSink[slot] = new object();
                slot = (slot + 1) % _workerSink.Length;
            }
        }
    }
}
