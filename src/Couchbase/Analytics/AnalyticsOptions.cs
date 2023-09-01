using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
using System.Text.Json;

#nullable enable

namespace Couchbase.Analytics
{
    public class AnalyticsOptions
    {
        //note in a future commit this will be private and AnalyticsOptions will be sent to AnalyticsClient instead of AnalyticsRequest (legacy)
        internal static AnalyticsOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal string? ClientContextIdValue;
        internal Dictionary<string, object> NamedParameters = new();
        internal List<object> PositionalParameters = new();
        internal CancellationToken Token = System.Threading.CancellationToken.None;
        internal AnalyticsScanConsistency ScanConsistencyValue = Analytics.AnalyticsScanConsistency.NotBounded;
        internal bool ReadonlyValue;
        internal int PriorityValue { get; set; } = 0;
        internal TimeSpan? TimeoutValue { get; set; }
        internal IRetryStrategy? RetryStrategyValue { get; set; }
        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// A parent or external span for tracing.
        /// </summary>
        /// <param name="span">An external <see cref="IRequestSpan"/> implementation for tracing.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Overrides the global <see cref="IRetryStrategy"/> defined in <see cref="ClusterOptions"/> for a request.
        /// </summary>
        /// <param name="retryStrategy">The <see cref="IRetryStrategy"/> to use for a single request.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// The <see cref="AnalyticsScanConsistency" /> you require for your analytics query.
        /// </summary>
        /// <param name="scanConsistency">The <see cref="AnalyticsScanConsistency" /> for documents to be included in the analytics results.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions ScanConsistency(
            AnalyticsScanConsistency scanConsistency)
        {
            ScanConsistencyValue = scanConsistency;
            return this;
        }

        /// <summary>
        /// Allows to specify if the query is readonly.
        /// </summary>
        /// <param name="readOnly"></param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions Readonly(bool readOnly)
        {
            ReadonlyValue = readOnly;
            return this;
        }

        /// <summary>
        /// Specifies values with their key and value as presented as part of the JSON payload.
        /// </summary>
        /// <param name="key">The key of the raw parameter.</param>
        /// <param name="value">The value of the raw parameter.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
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

        /// <summary>
        /// Specifies named parameters.
        /// </summary>
        /// <param name="parameterName">The named parameter value.</param>
        /// <param name="value">The named parameter key or name.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions Parameter(string parameterName, object value)
        {
            NamedParameters[parameterName] = value;
            return this;
        }

        /// <summary>
        /// Specifies positional parameters.
        /// </summary>
        /// <param name="value">The value of the positional parameter.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions Parameter(object value)
        {
            PositionalParameters.Add(value);
            return this;
        }

        /// <summary>
        /// Specifies how long to allow the operation to continue running before it is cancelled.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/></param> value.
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// Allows to give certain requests higher priority than others.
        /// </summary>
        /// <param name="priority">Set to true to prioritize the query.</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
        public AnalyticsOptions Priority(bool priority)
        {
            PriorityValue = priority ? -1 : 0;
            return this;
        }

        /// <summary>
        /// A token for controlling cooperative cancellation of the query.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for controlling query cancellation</param>
        /// <returns>A <see cref="AnalyticsOptions"/> object for chaining options.</returns>
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

        [RequiresUnreferencedCode(AnalyticsClient.AnalyticsRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(AnalyticsClient.AnalyticsRequiresDynamicCodeWarning)]
        internal string GetParametersAsJson()
        {
            if (PositionalParameters.Any())
                return JsonSerializer.Serialize(PositionalParameters);
            return JsonSerializer.Serialize(NamedParameters);
        }

        [RequiresUnreferencedCode(AnalyticsClient.AnalyticsRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(AnalyticsClient.AnalyticsRequiresDynamicCodeWarning)]
        internal string GetFormValuesAsJson(string statement)
        {
            return JsonSerializer.Serialize(GetFormValues(statement));
        }

        internal IDictionary<string, object> GetFormValues(string statement)
        {
            statement = CleanStatement(statement);
            var formValues = new Dictionary<string, object>
            {
                { "statement", statement },
                { "timeout", $"{TimeoutValue?.TotalMilliseconds}ms" },
                { "client_context_id", ClientContextIdValue ?? Guid.NewGuid().ToString() }
            };

            if (!string.IsNullOrWhiteSpace(QueryContext))
            {
                formValues.Add("query_context", QueryContext!);
            }

            foreach (var parameter in NamedParameters)
            {
                formValues.Add(parameter.Key, parameter.Value);
            }

            if (PositionalParameters.Any())
            {
                formValues.Add("args", PositionalParameters.ToArray());
            }

            return formValues;
        }

        private string CleanStatement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                throw new ArgumentException("statement cannot be null or empty");
            }

            statement = statement.Trim();
            if (!statement.EndsWith(";"))
            {
                statement += ";";
            }

            return statement;
        }

        public void Deconstruct(out string? clientContextIdValue,
            out IReadOnlyDictionary<string, object> namedParameters,
            out IReadOnlyList<object> positionalParameters,
            out CancellationToken token,
            out AnalyticsScanConsistency scanConsistencyValue,
            out bool readonlyValue,
            out int priorityValue,
            out TimeSpan? timeoutValue,
            out IRetryStrategy? retryStrategyValue,
            out IRequestSpan? requestSpanValue,
            out string? queryContext,
            out string? bucketName,
            out string? scopeName)
        {
            clientContextIdValue = ClientContextIdValue;
            namedParameters = NamedParameters;
            positionalParameters = PositionalParameters;
            token = Token;
            scanConsistencyValue = ScanConsistencyValue;
            readonlyValue = ReadonlyValue;
            priorityValue = PriorityValue;
            timeoutValue = TimeoutValue;
            retryStrategyValue = RetryStrategyValue;
            requestSpanValue = RequestSpanValue;
            queryContext = QueryContext;
            bucketName = BucketName;
            scopeName = ScopeName;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(
                out string? clientContextIdValue,
                out IReadOnlyDictionary<string, object> namedParameters,
                out IReadOnlyList<object> positionalParameters,
                out CancellationToken token,
                out AnalyticsScanConsistency scanConsistencyValue,
                out bool readonlyValue,
                out int priorityValue,
                out TimeSpan? timeoutValue,
                out IRetryStrategy? retryStrategyValue,
                out IRequestSpan? requestSpanValue,
                out string? queryContext,
                out string? bucketName,
                out string? scopeName);

            return new ReadOnly(
                clientContextIdValue,
                namedParameters,
                positionalParameters,
                token,
                scanConsistencyValue,
                readonlyValue,
                priorityValue,
                timeoutValue,
                retryStrategyValue,
                requestSpanValue,
                queryContext,
                bucketName,
                scopeName);
        }

        public record ReadOnly(
            string? ClientContextId,
            IReadOnlyDictionary<string, object> NamedParameters,
            IReadOnlyList<object> PositionalParameters,
            CancellationToken Token,
            AnalyticsScanConsistency ScanConsistency,
            bool Readonly,
            int Priority,
            TimeSpan? Timeout,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            string? QueryContext,
            string? BucketName,
            string? ScopeName);
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
