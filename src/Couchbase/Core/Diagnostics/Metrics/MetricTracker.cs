using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.Utils;
using Couchbase.Views;

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
                unit: "us",
                description: "Duration of operations, excluding retries");

        private static readonly Counter<long> OperationCounts =
            KeyValueMeter.CreateCounter<long>(name: Names.OperationCounts,
                unit: "{operations}",
                description: "Number of operations executed");

        private static readonly Counter<long> ResponseStatus =
            KeyValueMeter.CreateCounter<long>(name: Names.OperationStatus,
                unit: "{operations}",
                description: "KVResponse");

        private static readonly Counter<long> Orphans =
            KeyValueMeter.CreateCounter<long>(name: Names.Orphans,
                unit: "{operations}",
                description: "Number of operations sent which did not receive a response");

        private static readonly Counter<long> Retries =
            KeyValueMeter.CreateCounter<long>(name: Names.Retries,
                unit: "{operations}",
                description: "Number of operations retried");

        private static readonly Counter<long> SendQueueFullErrors =
            KeyValueMeter.CreateCounter<long>(name: Names.SendQueueFullErrors,
                unit: "{operations}",
                description: "Number operations rejected due to a full send queue");

        private static readonly Counter<long> Timeouts =
            KeyValueMeter.CreateCounter<long>(name: Names.Timeouts,
                unit: "{operations}",
                description: "Number of operations timed out");

        static MetricTracker()
        {
            // Due to lazy initialization, we should initialize Observable metrics here rather than static fields

            KeyValueMeter.CreateObservableGauge(name: Names.Connections,
                unit: "{connections}",
                observeValue: () => new Measurement<int>(MultiplexingConnection.GetConnectionCount(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of active connections");

            KeyValueMeter.CreateObservableGauge(name: Names.SendQueueLength,
                unit: "{operations}",
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
            public static void TrackOperation(OperationBase operation, TimeSpan duration)
            {
                var tagList = new TagList
                {
                    { OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name },
                    { OuterRequestSpans.Attributes.Operation, operation.OpCode.ToMetricTag() },
                    { OuterRequestSpans.Attributes.BucketName, operation.BucketName },
                    { OuterRequestSpans.Attributes.ScopeName, operation.SName },
                    { OuterRequestSpans.Attributes.CollectionName, operation.CName }
                };

                Operations.Record(duration.ToMicroseconds(), tagList);
                OperationCounts.Add(1, tagList);
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
            public static void TrackOperation(QueryRequest queryRequest, TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.BucketName,
                        queryRequest.Options?.BucketName),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.ScopeName,
                        queryRequest.Options?.ScopeName));
        }

        public static class Analytics
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(AnalyticsRequest analyticsRequest, TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.AnalyticsQuery),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.BucketName,
                        analyticsRequest.Options?.BucketName),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.ScopeName,
                        analyticsRequest.Options?.ScopeName));
        }

        public static class Search
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(FtsSearchRequest searchRequest, TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.SearchQuery),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.ScopeName,
                        searchRequest.Options?.ScopeName));
        }

        public static class Views
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(ViewQuery viewQuery, TimeSpan duration) =>
                Operations.Record(duration.ToMicroseconds(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.ViewQuery),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.BucketName,
                        viewQuery.BucketName));
        }
    }
}
