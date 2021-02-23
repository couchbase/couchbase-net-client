using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.Analytics
{
    public class AnalyticsOptions
    {
        //note in a future commit this will be private and AnalyticsOptions will be sent to AnalyticsClient instead of AnalyticsRequest (legacy)
        internal string? ClientContextIdValue;
        internal Dictionary<string, object> NamedParameters  = new Dictionary<string, object>();
        internal List<object> PositionalParameters = new List<object>();
        internal CancellationToken Token = System.Threading.CancellationToken.None;
        internal AnalyticsScanConsistency ScanConsistencyValue = Analytics.AnalyticsScanConsistency.NotBounded;
        internal bool ReadonlyValue;
        internal int PriorityValue { get; set; } = 0;
        internal TimeSpan? TimeoutValue { get; set; }
        internal IRetryStrategy? RetryStrategyValue { get; set; }

        /// <summary>
        /// Overrides the global <see cref="IRetryStrategy"/> defined in <see cref="ClusterOptions"/> for a request.
        /// </summary>
        /// <param name="retryStrategy">The <see cref="IRetryStrategy"/> to use for a single request.</param>
        /// <returns>The options.</returns>
        public AnalyticsOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        public AnalyticsOptions ScanConsistency(
            AnalyticsScanConsistency scanConsistency)
        {
            ScanConsistencyValue = scanConsistency;
            return this;
        }

        public AnalyticsOptions Readonly(bool readOnly)
        {
            ReadonlyValue = readOnly;
            return this;
        }

        public AnalyticsOptions Raw(string key, object value)
        {
            NamedParameters.Add(key, value);
            return this;
        }

        public AnalyticsOptions ClientContextId(string clientContextId)
        {
            ClientContextIdValue = clientContextId;
            return this;
        }

        public AnalyticsOptions Parameter(string parameterName, object value)
        {
            if (NamedParameters == null)
            {
                NamedParameters = new Dictionary<string, object>();
            }

            NamedParameters[parameterName] = value;
            return this;
        }

        public AnalyticsOptions Parameter(object value)
        {
            if (PositionalParameters == null)
            {
                PositionalParameters = new List<object>();
            }

            PositionalParameters.Add(value);
            return this;
        }

        public AnalyticsOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public AnalyticsOptions Priority(bool priority)
        {
            PriorityValue = priority ? -1 : 0;
            return this;
        }

        public AnalyticsOptions CancellationToken(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return this;
        }

        /// <summary>
        ///The alias for the namespace:bucket:scope:collection
        /// </summary>
        /// <returns></returns>
        internal string? QueryContext { get; set; }

        /// <summary>
        /// The name of the bucket if this is a scope level query.
        /// </summary>
        /// <remarks>For internal use only.</remarks>
        internal string? BucketName { get; set; }

        /// <summary>
        /// The name of the scope if this is a scope level query.
        /// </summary>
        /// <remarks>For internal use only.</remarks>
        internal string? ScopeName { get; set; }
    }
}
