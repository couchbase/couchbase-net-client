using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Analytics
{
    internal class AnalyticsRequest : IAnalyticsRequest
    {
        private string _clientContextId;
        private string _requestContextId;
        private bool _pretty;
        private bool _includeMetrics;
        internal Dictionary<string, string> Credentials = new Dictionary<string, string>();
        internal Dictionary<string, object> NamedParameters = new Dictionary<string, object>();
        internal List<object> PositionalArguments = new List<object>();
        private TimeSpan? _timeout;

        public AnalyticsRequest()
        {
            _clientContextId = Guid.NewGuid().ToString();
            _requestContextId = Guid.NewGuid().ToString();
        }

        public AnalyticsRequest(string statement) : this()
        {
            Statement(statement);
        }

        /// <summary>
        /// Gets the original analytics statement.
        /// </summary>
        public string OriginalStatement { get; private set; }

        /// <summary>
        /// Gets the context identifier for the analytics request. Useful for debugging.
        /// </summary>
        /// <returns>The unique request ID.</returns>.
        /// <remarks>
        /// This value changes for every request.
        /// </remarks>
        public string CurrentContextId => string.Format("{0}::{1}", _clientContextId, _requestContextId);

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
                {"statement", OriginalStatement},
                {"pretty", _pretty},
                {"metrics", _includeMetrics}
            };

            if (Credentials.Any())
            {
                var creds = new List<dynamic>();
                foreach (var credential in Credentials)
                {
                    creds.Add(new { user = credential.Key, pass = credential.Value });
                }
                formValues.Add("creds", creds);
            }

            foreach (var parameter in NamedParameters)
            {
                formValues.Add(parameter.Key, parameter.Value);
            }

            if (PositionalArguments.Any())
            {
                formValues.Add("args", PositionalArguments.ToArray());
            }

            formValues.Add("timeout", $"{Lifespan.Duration * 1000}ms");

            _requestContextId = Guid.NewGuid().ToString();
            formValues.Add("client_context_id", CurrentContextId);

            if (IsDeferred)
            {
                formValues.Add("mode", "async");
            }

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
        /// True if the request exceeded it's <see cref="ClientConfiguration.AnalyticsRequestTimeout" />.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the request times out; otherwise <c>false</c>.
        /// </returns>
        public bool TimedOut()
        {
            // BUG: will throw exception if called before Lifespan has been set
            return Lifespan.TimedOut();
        }

        internal Lifespan Lifespan { get; private set; }

        internal void ConfigureLifespan(uint defaultDuration)
        {
            Lifespan = new Lifespan
            {
                CreationTime = DateTime.UtcNow,
                Duration = _timeout.HasValue ? (uint) _timeout.Value.TotalSeconds : defaultDuration
            };
        }

        /// <summary>
        /// Sets a analytics statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid SQL++ statement for.</param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest Statement(string statement)
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

        /// <summary>
        /// Adds a set of credentials to the list of credentials, in the form of username/password.
        /// </summary>
        /// <param name="username">The bucket or username.</param>
        /// <param name="password">The password of the bucket.</param>
        /// <param name="isAdmin">True if connecting as an admin.</param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">username - cannot be null, empty or whitespace.</exception>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IAnalyticsRequest AddCredentials(string username, string password, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                const string usernameParameter = "username";
                throw new ArgumentOutOfRangeException(username, ExceptionUtil.GetMessage(ExceptionUtil.ParameterCannotBeNullOrEmptyFormat, usernameParameter));
            }

            if (isAdmin)
            {
                if (!username.StartsWith("admin:"))
                {
                    username = "admin:" + username;
                }
            }
            else if (!username.StartsWith("local:"))
            {
                username = "local:" + username;
            }
            Credentials.Add(username, password);
            return this;
        }

        /// <summary>
        /// A user supplied piece of data supplied with the request to the sevice. Any result will also contain the same data.
        /// </summary>
        /// <param name="contextId"></param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IAnalyticsRequest ClientContextId(string contextId)
        {
            if (!string.IsNullOrWhiteSpace(contextId))
            {
                _clientContextId = contextId;
            }
            return this;
        }

        /// <summary>
        /// Sets whether the analytics query and result JSON formatting will be intended.
        /// NOTE: Setting <see cref="Pretty" /> to true can have a negative performance impact due to larger payloads.
        /// </summary>
        /// <param name="pretty">if set to <c>true</c> [pretty].</param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IAnalyticsRequest Pretty(bool pretty)
        {
            _pretty = pretty;
            return this;
        }

        /// <summary>
        /// Specifies that metrics should be returned with query results.
        /// </summary>
        /// <param name="includeMetrics">True to return query metrics.</param>
        /// <returns>
        /// A reference to the current <see cref="IAnalyticsRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IAnalyticsRequest IncludeMetrics(bool includeMetrics)
        {
            _includeMetrics = includeMetrics;
            return this;
        }

        /// <summary>
        /// Adds a named parameter to be used with the statement.
        /// </summary>
        /// <param name="key">The paramemeter name.</param>
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
        /// Adds a named parameter to be used with the statement.
        /// </summary>
        /// <param name="key">The paramemeter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        [Obsolete("Please use AddNamedParameter(key, value) instead. This method may be removed in a future version.")]
        public IAnalyticsRequest AddNamedParamter(string key, object value)
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
        public IAnalyticsRequest Timeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
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

        /// <summary>
        /// Gets or sets the timeout value of the <see cref="AnalyticsRequest"/>.
        /// </summary>
        internal uint TimeoutValue => Lifespan.Duration * 1000;

        /// <summary>
        /// Gets a value indicating whether the query is deferred.
        /// </summary>
        /// <value>
        /// <c>true</c> if the query was deferred; otherwise, <c>false</c>.
        /// </value>
        public bool IsDeferred { get; private set;}

        /// <summary>
        /// Sets the query as deferred.
        /// </summary>
        /// <param name="deferred">if set to <c>true</c> the query will be executed in a deferred method.</param>
        /// <returns>
        /// A reference to the current <see cref="T:Couchbase.Analytics.IAnalyticsRequest" /> for method chaining.
        /// </returns>
        public IAnalyticsRequest Deferred(bool deferred)
        {
            IsDeferred = deferred;
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
