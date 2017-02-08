using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Analytics
{
    public class AnalyticsRequest : IAnalyticsRequest
    {
        private string _clientContextId;
        private string _requestContextId;
        private bool _pretty;
        private bool _includeMetrics;
        private readonly Dictionary<string, string> _credentials = new Dictionary<string, string>();

        public AnalyticsRequest(string statement = null)
        {
            OriginalStatement = statement;
            _clientContextId = Guid.NewGuid().ToString();
            _requestContextId = Guid.NewGuid().ToString();
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
        public string CurrentContextId
        {
            get
            {
                return string.Format("{0}::{1}", _clientContextId, _requestContextId);
            }
        }

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

            if (_credentials.Any())
            {
                var creds = new List<dynamic>();
                foreach (var credential in _credentials)
                {
                    creds.Add(new { user = credential.Key, pass = credential.Value });
                }
                formValues.Add("creds", creds);
            }

            _requestContextId = Guid.NewGuid().ToString();
            formValues.Add("client_context_id", CurrentContextId);

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
            return Lifespan.TimedOut();
        }

        internal Lifespan Lifespan { get; private set; }

        internal void ConfigureLifespan(uint duration)
        {
            Lifespan = new Lifespan
            {
                CreationTime = DateTime.UtcNow,
                Duration = duration
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
        public IAnalyticsRequest Credentials(string username, string password, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                const string usernameParameter = "username";
                throw new ArgumentOutOfRangeException(username, ExceptionUtil.GetMessage(ExceptionUtil.ParameterCannotBeNullOrEmptyFormat, usernameParameter));
            }

            if (isAdmin && !username.StartsWith("admin:"))
            {
                username = "admin:" + username;
            }
            else if (!username.StartsWith("local:"))
            {
                username = "local:" + username;
            }
            _credentials.Add(username, password);
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
    }
}
