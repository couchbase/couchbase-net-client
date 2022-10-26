using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// Methods for easily tracking metrics via the .NET metrics system.
    /// </summary>
    internal static class MetricTracker
    {
        public const string MeterName = "CouchbaseNetClient";

        public static class Names
        {
            // ReSharper disable MemberHidesStaticFromOuterClass

            public const string Connections = "db.couchbase.connections";
            public const string Operations = "db.couchbase.operations";
            public const string OperationCounts = "db.couchbase.operations.count";
            public const string OperationStatus = "db.couchbase.operations.status";
            public const string Orphans = "db.couchbase.orphans";
            public const string Retries = "db.couchbase.retries";
            public const string SendQueueFullErrors = "db.couchbase.sendqueue.fullerrors";
            public const string SendQueueLength = "db.couchbase.sendqueue.length";
            public const string Timeouts = "db.couchbase.timeouts";

            // ReSharper restore MemberHidesStaticFromOuterClass
        }

        private static readonly Meter KeyValueMeter = new(MeterName, "1.0.0");

        private static readonly Histogram<long> Operations =
            KeyValueMeter.CreateHistogram<long>(name: Names.Operations,
                unit: "Microseconds",
                description: "Duration of operations, excluding retries");

        private static readonly Counter<long> OperationCounts =
            KeyValueMeter.CreateCounter<long>(name: Names.OperationCounts,
                unit: "Operations",
                description: "Number of operations executed");

        private static readonly Counter<long> ResponseStatus =
            KeyValueMeter.CreateCounter<long>(name: Names.OperationStatus,
                unit: "Operation",
                description: "KVResponse");

        private static readonly Counter<long> Orphans =
            KeyValueMeter.CreateCounter<long>(name: Names.Orphans,
                unit: "Operations",
                description: "Number of operations sent which did not receive a response");

        private static readonly Counter<long> Retries =
            KeyValueMeter.CreateCounter<long>(name: Names.Retries,
                unit: "Operations",
                description: "Number of operations retried");

        private static readonly Counter<long> SendQueueFullErrors =
            KeyValueMeter.CreateCounter<long>(name: Names.SendQueueFullErrors,
                unit: "Operations",
                description: "Number operations rejected due to a full send queue");

        private static readonly Counter<long> Timeouts =
            KeyValueMeter.CreateCounter<long>(name: Names.Timeouts,
                unit: "Operations",
                description: "Number of operations timed out");

        static MetricTracker()
        {
            // Due to lazy initialization, we should initialize Observable metrics here rather than static fields

            KeyValueMeter.CreateObservableGauge(name: Names.Connections,
                unit: "Connections",
                observeValue: () => new Measurement<int>(MultiplexingConnection.GetConnectionCount(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of active connections");

            KeyValueMeter.CreateObservableGauge(name: Names.SendQueueLength,
                unit: "Operations",
                observeValue: () => new Measurement<int>(ConnectionPoolBase.GetSendQueueLength(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of operations queued to be sent");
        }

        public static class KeyValue
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(OpCode opCode, TimeSpan duration)
            {
                Operations.Record(duration.ToMicroseconds(),
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));

                OperationCounts.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));
            }

            /// <summary>
            /// Tracks the response status for each response from the server.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackResponseStatus(OpCode opCode, ResponseStatus status)
            {
                ResponseStatus.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()),
                    new(OuterRequestSpans.Attributes.ResponseStatus, status));
            }

            /// <summary>
            /// Tracks an operation retry.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackRetry(OpCode opCode) =>
                Retries.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));

            /// <summary>
            /// Track an orphaned operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOrphaned() =>
                Orphans.Add(1,
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));

            /// <summary>
            /// Tracks an operation rejected due to a full connection pool send queue.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackSendQueueFull() =>
                SendQueueFullErrors.Add(1,
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));

            /// <summary>
            /// Tracks an operation which has failed due to a timeout.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackTimeout(OpCode opCode) =>
                Timeouts.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));
        }

        public static class N1Ql
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.N1QLQuery));
        }

        public static class Analytics
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.AnalyticsQuery));
        }

        public static class Search
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.SearchQuery));
        }

        public static class Views
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.ViewQuery));
        }
    }
}
