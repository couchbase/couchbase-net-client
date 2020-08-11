using System;
using System.Collections.Generic;
using System.Text;
using static Couchbase.Core.Diagnostics.Tracing.RequestTracing;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public class ThresholdOptions
    {
        public IReadOnlyDictionary<string, TimeSpan> GetServiceThresholds() => new Dictionary<string, TimeSpan>()
        {
            { ServiceIdentifier.Kv,        KvThreshold },
            { ServiceIdentifier.View,      ViewThreshold },
            { ServiceIdentifier.Query,     QueryThreshold },
            { ServiceIdentifier.Search,    SearchThreshold },
            { ServiceIdentifier.Analytics, AnalyticsThreshold },
        };

        public static readonly int DefaultSampleSize = 10;
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan DefaultKvThreshold = TimeSpan.FromMilliseconds(500);
        public static readonly TimeSpan DefaultViewThreshold = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DefaultQueryThreshold = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DefaultSearchThreshold = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DefaultAnalyticsThreshold = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum number of items to log per service.
        /// </summary>
        public int SampleSize { get; set; } = DefaultSampleSize;

        /// <summary>
        /// Gets or sets the interval between executions that processes collection operation spans/activities.
        /// </summary>
        public TimeSpan Interval { get; set; } = DefaultInterval;

        /// <summary>
        /// Gets or sets the KV operation threshold.
        /// </summary>
        public TimeSpan KvThreshold { get; set; } = DefaultKvThreshold;

        /// <summary>
        /// Gets or sets the View query operation threshold.
        /// </summary>
        public TimeSpan ViewThreshold { get; set; } = DefaultViewThreshold;

        /// <summary>
        /// Gets or sets the N1QL query operation threshold.
        /// </summary>
        public TimeSpan QueryThreshold { get; set; } = DefaultQueryThreshold;

        /// <summary>
        /// Gets or sets the Full Text Search query operation threshold.
        /// </summary>
        public TimeSpan SearchThreshold { get; set; } = DefaultSearchThreshold;

        /// <summary>
        /// Gets or sets the Analytics query operation threshold.
        /// </summary>
        public TimeSpan AnalyticsThreshold { get; set; } = DefaultAnalyticsThreshold;

        #region self-Builder pattern
        public ThresholdOptions WithSampleSize(int sampleSize)
        {
            SampleSize = sampleSize;
            return this;
        }

        public ThresholdOptions WithInterval(TimeSpan interval)
        {
            Interval = interval;
            return this;
        }

        public ThresholdOptions WithKvThreshold(TimeSpan threshold)
        {
            KvThreshold = threshold;
            return this;
        }

        public ThresholdOptions WithViewThreshold(TimeSpan threshold)
        {
            ViewThreshold = threshold;
            return this;
        }

        public ThresholdOptions WithQueryThreshold(TimeSpan threshold)
        {
            QueryThreshold = threshold;
            return this;
        }

        public ThresholdOptions WithSearchThreshold(TimeSpan threshold)
        {
            SearchThreshold = threshold;
            return this;
        }

        public ThresholdOptions WithAnalyticsThreshold(TimeSpan threshold)
        {
            AnalyticsThreshold = threshold;
            return this;
        }
        #endregion
    }
}
