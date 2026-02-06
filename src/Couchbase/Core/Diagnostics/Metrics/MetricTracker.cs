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
    /// Constants for Couchbase .NET metrics.
    /// </summary>
    public static class CouchbaseMetrics
    {
        /// <summary>
        /// The name of the legacy Couchbase meter.
        /// </summary>
        public const string MeterName = "CouchbaseNetClient";

        /// <summary>
        /// The name of the modern Couchbase meter (following OTel semantic conventions).
        /// </summary>
        public const string ModernMeterName = "CouchbaseNetClient.Modern";
    }

    /// <summary>
    /// Methods for easily tracking metrics via the .NET metrics system.
    /// </summary>
    internal static class MetricTracker
    {
        public const string MeterName = CouchbaseMetrics.MeterName;
        public const string ModernMeterName = CouchbaseMetrics.ModernMeterName;

        public static class Names
        {
            // ReSharper disable MemberHidesStaticFromOuterClass

            // Legacy metric names
            public const string Connections = "db.couchbase.connections";
            public const string Operations = "db.couchbase.operations";
            public const string OperationCounts = "db.couchbase.operations.count";
            public const string OperationStatus = "db.couchbase.operations.status";
            public const string Orphans = "db.couchbase.orphans";
            public const string Retries = "db.couchbase.retries";
            public const string SendQueueFullErrors = "db.couchbase.sendqueue.fullerrors";
            public const string SendQueueLength = "db.couchbase.sendqueue.length";
            public const string Timeouts = "db.couchbase.timeouts";

            // Modern metric names (OTel semantic conventions)
            public const string ModernOperations = "db.client.operation.duration";

            // ReSharper restore MemberHidesStaticFromOuterClass
        }

        #region Legacy Meter and Instruments

        private static readonly Meter LegacyMeter = new(MeterName, "1.0.0");

        private static readonly Histogram<long> LegacyOperations =
            LegacyMeter.CreateHistogram<long>(name: Names.Operations,
                unit: "us",
                description: "Duration of operations, excluding retries");

        private static readonly Counter<long> LegacyOperationCounts =
            LegacyMeter.CreateCounter<long>(name: Names.OperationCounts,
                unit: "{operations}",
                description: "Number of operations executed");

        private static readonly Counter<long> LegacyResponseStatus =
            LegacyMeter.CreateCounter<long>(name: Names.OperationStatus,
                unit: "{operations}",
                description: "KVResponse");

        private static readonly Counter<long> LegacyOrphans =
            LegacyMeter.CreateCounter<long>(name: Names.Orphans,
                unit: "{operations}",
                description: "Number of operations sent which did not receive a response");

        private static readonly Counter<long> LegacyRetries =
            LegacyMeter.CreateCounter<long>(name: Names.Retries,
                unit: "{operations}",
                description: "Number of operations retried");

        private static readonly Counter<long> LegacySendQueueFullErrors =
            LegacyMeter.CreateCounter<long>(name: Names.SendQueueFullErrors,
                unit: "{operations}",
                description: "Number operations rejected due to a full send queue");

        private static readonly Counter<long> LegacyTimeouts =
            LegacyMeter.CreateCounter<long>(name: Names.Timeouts,
                unit: "{operations}",
                description: "Number of operations timed out");

        #endregion

        #region Modern Meter and Instruments

        private static readonly Meter ModernMeter = new(ModernMeterName, "1.0.1");

        private static readonly Histogram<double> ModernOperations =
            ModernMeter.CreateHistogram<double>(name: Names.ModernOperations,
                unit: "s",
                description: "Duration of database client operations");

        // Note: The following counters are intentionally omitted from the modern meter
        // as they are redundant with the histogram data:
        // - OperationCounts: redundant with histogram count
        // - OperationStatus: included in outcome tag on histogram
        // - Timeouts: included in outcome tag on histogram

        private static readonly Counter<long> ModernOrphans =
            ModernMeter.CreateCounter<long>(name: Names.Orphans,
                unit: "{operations}",
                description: "Number of operations sent which did not receive a response");

        private static readonly Counter<long> ModernRetries =
            ModernMeter.CreateCounter<long>(name: Names.Retries,
                unit: "{operations}",
                description: "Number of operations retried");

        private static readonly Counter<long> ModernSendQueueFullErrors =
            ModernMeter.CreateCounter<long>(name: Names.SendQueueFullErrors,
                unit: "{operations}",
                description: "Number operations rejected due to a full send queue");

        #endregion

        static MetricTracker()
        {
            // Due to lazy initialization, we should initialize Observable metrics here rather than static fields

            // Legacy observable gauges
            LegacyMeter.CreateObservableGauge(name: Names.Connections,
                unit: "{connections}",
                observeValue: () => new Measurement<int>(MultiplexingConnection.GetConnectionCount(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of active connections");

            LegacyMeter.CreateObservableGauge(name: Names.SendQueueLength,
                unit: "{operations}",
                observeValue: () => new Measurement<int>(ConnectionPoolBase.GetSendQueueLength(),
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of operations queued to be sent");

            // Modern observable gauges use the same data with different tag names
            ModernMeter.CreateObservableGauge(name: Names.Connections,
                unit: "{connections}",
                observeValue: () => new Measurement<int>(MultiplexingConnection.GetConnectionCount(),
                    new KeyValuePair<string, object?>(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of active connections");

            ModernMeter.CreateObservableGauge(name: Names.SendQueueLength,
                unit: "{operations}",
                observeValue: () => new Measurement<int>(ConnectionPoolBase.GetSendQueueLength(),
                    new KeyValuePair<string, object?>(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name)),
                description: "Number of operations queued to be sent");
        }

        /// <summary>
        /// Modern attribute names following OTel semantic conventions.
        /// </summary>
        private static class ModernAttributes
        {
            public const string Service = "couchbase.service";
            public const string Operation = "db.operation.name";
            public const string Namespace = "db.namespace";
            public const string ScopeName = "couchbase.scope.name";
            public const string CollectionName = "couchbase.collection.name";
            public const string Outcome = "error.type";
            public const string ClusterName = "couchbase.cluster.name";
            public const string ClusterUuid = "couchbase.cluster.uuid";
        }

        public static class KeyValue
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(OperationBase operation, TimeSpan duration, Type? errorType)
            {
                // Legacy metrics
                var legacyTags = new TagList
                {
                    { OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name },
                    { OuterRequestSpans.Attributes.Operation, operation.OpCode.ToMetricTag() },
                    { OuterRequestSpans.Attributes.BucketName, operation.BucketName },
                    { OuterRequestSpans.Attributes.ScopeName, operation.SName },
                    { OuterRequestSpans.Attributes.CollectionName, operation.CName },
                    { OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType) },
                };

                legacyTags.AddClusterLabelsIfProvided(operation.Span);

                LegacyOperations.Record(duration.ToMicroseconds(), legacyTags);
                LegacyOperationCounts.Add(1, legacyTags);

                // Modern metrics
                var modernTags = new TagList
                {
                    { ModernAttributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name },
                    { ModernAttributes.Operation, operation.OpCode.ToMetricTag() },
                    { ModernAttributes.Namespace, operation.BucketName },
                    { ModernAttributes.ScopeName, operation.SName },
                    { ModernAttributes.CollectionName, operation.CName },
                    { ModernAttributes.Outcome, GetOutcome(errorType) },
                };

                AddModernClusterLabels(ref modernTags, operation.Span);

                ModernOperations.Record(duration.TotalSeconds, modernTags);
            }

            /// <summary>
            /// Tracks the response status for each response from the server.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackResponseStatus(OpCode opCode, ResponseStatus status)
            {
                LegacyResponseStatus.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()),
                    new(OuterRequestSpans.Attributes.ResponseStatus, status));
            }

            /// <summary>
            /// Tracks an operation retry.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackRetry(OpCode opCode)
            {
                LegacyRetries.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));

                ModernRetries.Add(1,
                    new(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(ModernAttributes.Operation, opCode.ToMetricTag()));
            }

            /// <summary>
            /// Track an orphaned operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOrphaned()
            {
                LegacyOrphans.Add(1,
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));

                ModernOrphans.Add(1,
                    new KeyValuePair<string, object?>(ModernAttributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));
            }

            /// <summary>
            /// Tracks an operation rejected due to a full connection pool send queue.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackSendQueueFull()
            {
                LegacySendQueueFullErrors.Add(1,
                    new KeyValuePair<string, object?>(OuterRequestSpans.Attributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));

                ModernSendQueueFullErrors.Add(1,
                    new KeyValuePair<string, object?>(ModernAttributes.Service,
                        OuterRequestSpans.ServiceSpan.Kv.Name));
            }

            /// <summary>
            /// Tracks an operation which has failed due to a timeout.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackTimeout(OpCode opCode)
            {
                LegacyTimeouts.Add(1,
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name),
                    new(OuterRequestSpans.Attributes.Operation, opCode.ToMetricTag()));
            }
        }

        public static class N1Ql
        {
            /// <summary>
            /// Tracks the first attempt of an operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void TrackOperation(QueryRequest queryRequest, TimeSpan duration, Type? errorType)
            {
                // Legacy metrics
                var legacyTags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(OuterRequestSpans.Attributes.Operation, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(OuterRequestSpans.Attributes.BucketName, queryRequest.Options?.BucketName),
                    new(OuterRequestSpans.Attributes.ScopeName, queryRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                legacyTags.AddClusterLabelsIfProvided(queryRequest.Options?.RequestSpanValue);
                LegacyOperations.Record(duration.ToMicroseconds(), legacyTags);

                // Modern metrics
                var modernTags = new TagList
                {
                    new(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(ModernAttributes.Operation, OuterRequestSpans.ServiceSpan.N1QLQuery),
                    new(ModernAttributes.Namespace, queryRequest.Options?.BucketName),
                    new(ModernAttributes.ScopeName, queryRequest.Options?.ScopeName),
                    new(ModernAttributes.Outcome, GetOutcome(errorType))
                };

                AddModernClusterLabels(ref modernTags, queryRequest.Options?.RequestSpanValue);
                ModernOperations.Record(duration.TotalSeconds, modernTags);
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
                // Legacy metrics
                var legacyTags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.AnalyticsQuery),
                    new(OuterRequestSpans.Attributes.BucketName, analyticsRequest.Options?.BucketName),
                    new(OuterRequestSpans.Attributes.ScopeName, analyticsRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                legacyTags.AddClusterLabelsIfProvided(analyticsRequest.Options?.RequestSpanValue);
                LegacyOperations.Record(duration.ToMicroseconds(), legacyTags);

                // Modern metrics
                var modernTags = new TagList
                {
                    new(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.AnalyticsQuery),
                    new(ModernAttributes.Namespace, analyticsRequest.Options?.BucketName),
                    new(ModernAttributes.ScopeName, analyticsRequest.Options?.ScopeName),
                    new(ModernAttributes.Outcome, GetOutcome(errorType))
                };

                AddModernClusterLabels(ref modernTags, analyticsRequest.Options?.RequestSpanValue);
                ModernOperations.Record(duration.TotalSeconds, modernTags);
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
                // Legacy metrics
                var legacyTags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.SearchQuery),
                    new(OuterRequestSpans.Attributes.ScopeName, searchRequest.Options?.ScopeName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                legacyTags.AddClusterLabelsIfProvided(searchRequest.Options?.RequestSpanValue);
                LegacyOperations.Record(duration.ToMicroseconds(), legacyTags);

                // Modern metrics
                var modernTags = new TagList
                {
                    new(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.SearchQuery),
                    new(ModernAttributes.ScopeName, searchRequest.Options?.ScopeName),
                    new(ModernAttributes.Outcome, GetOutcome(errorType))
                };

                AddModernClusterLabels(ref modernTags, searchRequest.Options?.RequestSpanValue);
                ModernOperations.Record(duration.TotalSeconds, modernTags);
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
                // Legacy metrics
                var legacyTags = new TagList
                {
                    new(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.ViewQuery),
                    new(OuterRequestSpans.Attributes.BucketName, viewQuery.BucketName),
                    new(OuterRequestSpans.Attributes.Outcome, GetOutcome(errorType))
                };

                legacyTags.AddClusterLabelsIfProvided(((IViewQuery)viewQuery).RequestSpanValue);
                LegacyOperations.Record(duration.ToMicroseconds(), legacyTags);

                // Modern metrics
                var modernTags = new TagList
                {
                    new(ModernAttributes.Service, OuterRequestSpans.ServiceSpan.ViewQuery),
                    new(ModernAttributes.Namespace, viewQuery.BucketName),
                    new(ModernAttributes.Outcome, GetOutcome(errorType))
                };

                AddModernClusterLabels(ref modernTags, ((IViewQuery)viewQuery).RequestSpanValue);
                ModernOperations.Record(duration.TotalSeconds, modernTags);
            }
        }

        private static void AddModernClusterLabels(ref TagList tagList, IRequestSpan? span)
        {
            if (span is RequestSpanWrapper wrapper)
            {
                var labels = wrapper.ClusterLabels;
                if (labels is not null)
                {
                    var clusterName = labels.ClusterName;
                    if (clusterName is not null)
                    {
                        tagList.Add(ModernAttributes.ClusterName, clusterName);
                    }

                    var clusterUuid = labels.ClusterUuid;
                    if (clusterUuid is not null)
                    {
                        tagList.Add(ModernAttributes.ClusterUuid, clusterUuid);
                    }
                }
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
