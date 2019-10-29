using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Retry;
using Newtonsoft.Json;

namespace Couchbase.Analytics
{
    internal class AnalyticsRequest : IAnalyticsRequest
    {
        internal Dictionary<string, object> NamedParameters = new Dictionary<string, object>();
        internal List<object> PositionalArguments = new List<object>();
        private TimeSpan _timeout = TimeSpan.FromMilliseconds(75000);
        private AnalyticsScanConsistency _scanConsistency = AnalyticsScanConsistency.NotBounded;

        public AnalyticsRequest()
        {
            ClientContextId = Guid.NewGuid().ToString();
        }

        public AnalyticsRequest(string statement)
        {
            WithStatement(statement);
        }

        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets the original analytics statement.
        /// </summary>
        public string OriginalStatement { get; private set; }

        public CancellationToken Token { get; set; }

        /// <summary>
        /// Gets the context identifier for the analytics request. Useful for debugging.
        /// </summary>
        /// <returns>The unique request ID.</returns>.
        /// <remarks>
        /// This value changes for every request.
        /// </remarks>
        public string ClientContextId { get; set; }

        /// <summary>
        /// Gets a <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the analytics service.
        /// </summary>
        /// <returns>
        /// The <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the analytics service.
        /// </returns>
        public IDictionary<string, object> GetFormValues()
        {
            var formValues = new Dictionary<string, object>
            {
                {"statement", OriginalStatement}
            };

            foreach (var parameter in NamedParameters)
            {
                formValues.Add(parameter.Key, parameter.Value);
            }

            if (PositionalArguments.Any())
            {
                formValues.Add("args", PositionalArguments.ToArray());
            }

            formValues.Add("timeout", $"{_timeout.TotalMilliseconds}ms");
            formValues.Add("client_context_id", ClientContextId ?? Guid.NewGuid().ToString());

            return formValues;
        }

        /// <summary>
        /// Gets the JSON representation of this analytics request's parameters.
        /// </summary>
        /// <returns>
        /// The form values as a JSON object.
        /// </returns>
        public string GetFormValuesAsJson()
        {
            return JsonConvert.SerializeObject(GetFormValues());
        }

        /// <summary>
        /// Sets a analytics statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid SQL++ statement for.</param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest WithStatement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                throw new ArgumentException("statement cannot be null or empty");
            }

            OriginalStatement = statement.Trim();
            if (!OriginalStatement.EndsWith(";"))
            {
                OriginalStatement += ";";
            }
            return this;
        }

        public string Statement
        {
            get => OriginalStatement;
            set => OriginalStatement = value;
        }

        /// <summary>
        /// A user supplied piece of data supplied with the request to the service. Any result will also contain the same data.
        /// </summary>
        /// <param name="contextId"></param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IAnalyticsRequest WithClientContextId(string contextId)
        {
            if (string.IsNullOrWhiteSpace(contextId))
            {
                contextId = Guid.NewGuid().ToString();
            }
            ClientContextId = contextId;
            return this;
        }

        /// <summary>
        /// Adds a named parameter to be used with the statement.
        /// </summary>
        /// <param name="key">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest AddNamedParameter(string key, object value)
        {
            NamedParameters[key] = value;
            return this;
        }

        /// <summary>
        /// Adds a positional parameter to be used with the statement.
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest AddPositionalParameter(object value)
        {
            PositionalArguments.Add(value);
            return this;
        }

        /// <summary>
        /// Sets the query timeout.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public uint Attempts { get; set; }
        public bool Idempotent { get; set; }
        public List<RetryReason> RetryReasons { get; set; } = new List<RetryReason>();
        public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy();

        public TimeSpan Timeout
        {
            get => _timeout;
            set => _timeout = value;
        }

        /// <summary>
        /// Sets the query priority. Default is <c>false</c>.
        /// </summary>
        /// <param name="priority"><c>true</c> is the query is to be prioritized.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest Priority(bool priority)
        {
            PriorityValue = priority ? -1 : 0;
            return this;
        }

        /// <summary>
        /// Sets the query priority. Default is <c>0</c>.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest Priority(int priority)
        {
            PriorityValue = priority;
            return this;
        }

        internal int PriorityValue { get; private set; }

        public IAnalyticsRequest ScanConsistency(AnalyticsScanConsistency scanConsistency)
        {
            _scanConsistency = scanConsistency;
            return this;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
