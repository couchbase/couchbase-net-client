using System;
using System.Collections.Generic;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    public class ThresholdOptions
    {
        public static readonly int DefaultSampleSize = 10;

        public TimeSpan EmitInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The interval after which the aggregated trace information is logged.
        /// </summary>
        /// <remarks>The default is 10 seconds.</remarks>
        /// <param name="emitInterval">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithEmitInterval(TimeSpan emitInterval)
        {
            EmitInterval = emitInterval;
            return this;
        }

        internal TimeSpan KvThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The interval after which the aggregated trace information is logged.
        /// </summary>
        /// <remarks>The default is 500 Milliseconds.</remarks>
        /// <param name="kvThreshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithKvThreshold(TimeSpan kvThreshold)
        {
            KvThreshold = kvThreshold;
            return this;
        }

        internal TimeSpan QueryThreshold { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The threshold over which the request is taken into account for the query service
        /// </summary>
        /// <remarks>The default is 1 second.</remarks>
        /// <param name="queryThreshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithQueryThreshold(TimeSpan queryThreshold )
        {
            QueryThreshold = queryThreshold;
            return this;
        }

        internal TimeSpan ViewsThreshold { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The threshold over which the request is taken into account for the views service
        /// </summary>
        /// <remarks>The default is 1 second.</remarks>
        /// <param name="viewsThreshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithViewsThreshold(TimeSpan viewsThreshold)
        {
            ViewsThreshold = viewsThreshold;
            return this;
        }

        internal TimeSpan SearchThreshold { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The threshold over which the request is taken into account for the search service
        /// </summary>
        /// <remarks>The default is 1 second.</remarks>
        /// <param name="searchThreshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithSearchThreshold(TimeSpan searchThreshold)
        {
            SearchThreshold = searchThreshold;
            return this;
        }

        internal TimeSpan AnalyticsThreshold { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The threshold over which the request is taken into account for the search service
        /// </summary>
        /// <remarks>The default is 1 second.</remarks>
        /// <param name="analyticsThreshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithAnalyticsThreshold(TimeSpan analyticsThreshold)
        {
            AnalyticsThreshold = analyticsThreshold;
            return this;
        }

        internal uint SampleSize { get; set; } = 10u;

        /// <summary>
        /// How many entries to sample per service in each emit interval
        /// </summary>
        /// <remarks>The default is 10 samples.</remarks>
        /// <param name="sampleSize">A <see cref="uint"/> indicating the sample size.</param>
        /// <returns>A <see cref="ThresholdOptions"/> for chaining.</returns>
        public ThresholdOptions WithSampleSize(uint sampleSize)
        {
            SampleSize = sampleSize;
            return this;
        }

        /// <summary>
        /// Provides the means of registering a custom <see cref="TraceListener"/> implementation.
        /// </summary>
        /// <remarks>It is suggested that the default <see cref="ThresholdListener"/> be used instead of a custom implementation.</remarks>
        public TraceListener ThresholdListener { get; set; }

        /// <summary>
        /// Enables threshold tracing. Defaults to enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        public IReadOnlyDictionary<string, TimeSpan> GetServiceThresholds()
        {
            return new Dictionary<string, TimeSpan>
            {
                {nameof(OuterRequestSpans.ServiceSpan.Kv).ToLowerInvariant(), KvThreshold},
                {OuterRequestSpans.ServiceSpan.ViewQuery, ViewsThreshold},
                {OuterRequestSpans.ServiceSpan.N1QLQuery, QueryThreshold},
                {OuterRequestSpans.ServiceSpan.SearchQuery, SearchThreshold},
                {OuterRequestSpans.ServiceSpan.AnalyticsQuery, AnalyticsThreshold}
            };
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
