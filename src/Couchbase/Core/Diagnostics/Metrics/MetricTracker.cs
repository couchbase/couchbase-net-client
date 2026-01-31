using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
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
            public static void TrackOperation(OperationBase operation, TimeSpan duration, Type? errorType)
            {
                var tagList = new TagList
                {
                    { OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name },
                    { OuterRequestSpans.Attributes.Operation, operation.OpCode.ToMetricTag() },
                    { OuterRequestSpans.Attributes.BucketName, operation.BucketName },
                    { OuterRequestSpans.Attributes.ScopeName, operation.SName },
                    { OuterRequestSpans.Attributes.CollectionName, operation.CName },
                    { OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType) },
                };

                tagList.AddClusterLabelsIfProvided(operation.Span);

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
            public static void TrackOperation(QueryRequest queryRequest, TimeSpan duration, Type? errorType)
            {
                var tags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(OuterRequestSpans.Attributes.Operation, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(OuterRequestSpans.Attributes.BucketName, queryRequest.Options?.BucketName),
                    new(OuterRequestSpans.Attributes.ScopeName, queryRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                tags.AddClusterLabelsIfProvided(queryRequest.Options?.RequestSpanValue);
                Operations.Record(duration.ToMicroseconds(), tags);
            }
        }

        public static class Analytics
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(AnalyticsRequest analyticsRequest, TimeSpan duration, Type? errorType)
            {
                var tags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.AnalyticsQuery),
                    new(OuterRequestSpans.Attributes.BucketName, analyticsRequest.Options?.BucketName),
                    new(OuterRequestSpans.Attributes.ScopeName, analyticsRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                tags.AddClusterLabelsIfProvided(analyticsRequest.Options?.RequestSpanValue);

                Operations.Record(duration.ToMicroseconds(), tags);
            }
        }

        public static class Search
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(FtsSearchRequest searchRequest, TimeSpan duration, Type? errorType)
            {
                var tags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.SearchQuery),
                    new(OuterRequestSpans.Attributes.ScopeName, searchRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                tags.AddClusterLabelsIfProvided(searchRequest.Options?.RequestSpanValue);

                Operations.Record(duration.ToMicroseconds(), tags);
            }
        }

        public static class Views
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Obsolete("The View service has been deprecated use the Query service instead.")]
            public static void TrackOperation(ViewQuery viewQuery, TimeSpan duration, Type? errorType)
            {
                var tags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.ViewQuery),
                    new(OuterRequestSpans.Attributes.BucketName, viewQuery.BucketName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                tags.AddClusterLabelsIfProvided(((IViewQuery)viewQuery).RequestSpanValue);
                Operations.Record(duration.ToMicroseconds(), tags);
            }
        }

        // internal for unit testing
        internal static string GetOutcome(Type? errorType)
        {
            if (errorType is null)
            {
                return "Success";
            }

            if (errorType == typeof(DocumentNotFoundException))
            {
                // Fast path for this common error type
                return "DocumentNotFound";
            }

            var couchbaseException = typeof(CouchbaseException);
            if (errorType == couchbaseException || !couchbaseException.IsAssignableFrom(errorType))
            {
                // In the case where this is not an inherited exception, say "Error" not "Couchbase".

                // Also returns "Error" for non-Couchbase exceptions so metric cardinality is not increased
                // for every possible .NET exception type. This can be revised in the future if necessary.

                return "Error";
            }

            const string ExceptionSuffix = "Exception";

            var outcome = errorType.Name;
#if NET6_0_OR_GREATER
            if (outcome.AsSpan().EndsWith(ExceptionSuffix)) // Faster comparison for .NET 6 and later
#else
            if (outcome.EndsWith(ExceptionSuffix, StringComparison.Ordinal))
#endif
            {
                // Strip "Exception" from the end of the type name, matches the behavior of the Java SDK
                outcome = outcome.Substring(0, outcome.Length - ExceptionSuffix.Length);
            }
            return outcome;
        }
    }
}
