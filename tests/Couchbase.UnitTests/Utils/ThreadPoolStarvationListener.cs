#if NET
#nullable enable
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Records thread-pool pressure during a test run, so that flaky tests can be lined up against
    /// it. Off unless COUCHBASE_TEST_THREADPOOL_LOG names an output file.
    /// </summary>
    /// <remarks>
    /// Two sources, on one timeline (UTC):
    /// <list type="bullet">
    /// <item>The runtime's thread-pool adjustment events. Reason 6 (Starvation) means the pool
    /// injected a thread because queued work made no progress; 8 (CooperativeBlocking) means it
    /// injected one because a worker blocked on a task.</item>
    /// <item>A sampler on a dedicated thread. Every interval it records the worker count and the
    /// queue length, and queues a probe work item. The probe's queue-to-run delay is how long any
    /// test continuation would have waited for a pool thread at that moment.</item>
    /// </list>
    /// </remarks>
    internal sealed class ThreadPoolStarvationListener : EventListener
    {
        private const string RuntimeSourceName = "Microsoft-Windows-DotNETRuntime";
        private const EventKeywords ThreadingKeyword = (EventKeywords)0x10000;
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

        private static readonly string[] Reasons =
        {
            "Warmup", "Initializing", "RandomMove", "ClimbingMove", "ChangePoint", "Stabilizing",
            "Starvation", "ThreadTimedOut", "CooperativeBlocking"
        };

        private static ThreadPoolStarvationListener? _instance;
        private static StreamWriter? _writer;
        private static readonly object WriteLock = new();

        [ModuleInitializer]
        internal static void Start()
        {
            var path = Environment.GetEnvironmentVariable("COUCHBASE_TEST_THREADPOOL_LOG");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _writer = new StreamWriter(path, append: false) { AutoFlush = true };
            Write("start", $"processors={Environment.ProcessorCount} pid={Environment.ProcessId}");
            ThreadPool.GetMinThreads(out var minWorkers, out _);
            Write("start", $"minWorkers={minWorkers}");

            _instance = new ThreadPoolStarvationListener();
            new Thread(Sample) { IsBackground = true, Name = "ThreadPoolStarvationSampler" }.Start();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Write("exit", "");
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == RuntimeSourceName)
            {
                EnableEvents(eventSource, EventLevel.Informational, ThreadingKeyword);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName != "ThreadPoolWorkerThreadAdjustmentAdjustment" || eventData.Payload is null)
            {
                return;
            }

            var reason = Convert.ToInt32(Payload(eventData, "Reason"), CultureInfo.InvariantCulture);
            var count = Payload(eventData, "NewWorkerThreadCount");
            var name = reason >= 0 && reason < Reasons.Length ? Reasons[reason] : reason.ToString(CultureInfo.InvariantCulture);
            Write("adjust", $"reason={name} newWorkers={count}");
        }

        private static object? Payload(EventWrittenEventArgs e, string name)
        {
            var index = e.PayloadNames?.IndexOf(name) ?? -1;
            return index >= 0 ? e.Payload![index] : null;
        }

        private static void Sample()
        {
            var probe = new Probe();
            while (true)
            {
                var pendingProbeMs = probe.QueuedAt is { } queuedAt && probe.RanAfterMs < 0
                    ? (long)Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds
                    : -1;

                Write("sample",
                    $"workers={ThreadPool.ThreadCount} queued={ThreadPool.PendingWorkItemCount} " +
                    $"probeMs={probe.RanAfterMs} pendingProbeMs={pendingProbeMs}");

                // Don't stack probes: while one waits, its age is the measurement.
                if (pendingProbeMs < 0)
                {
                    probe = new Probe();
                    probe.QueuedAt = Stopwatch.GetTimestamp();
                    ThreadPool.UnsafeQueueUserWorkItem(static p =>
                        p.RanAfterMs = (long)Stopwatch.GetElapsedTime(p.QueuedAt!.Value).TotalMilliseconds,
                        probe, preferLocal: false);
                }

                Thread.Sleep(SampleInterval);
            }
        }

        private static void Write(string kind, string detail)
        {
            lock (WriteLock)
            {
                _writer?.WriteLine($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}Z {kind} {detail}");
            }
        }

        private sealed class Probe
        {
            public long? QueuedAt;
            public long RanAfterMs = -1;
        }
    }
}
#endif
