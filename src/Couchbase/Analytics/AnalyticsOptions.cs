using System;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Analytics
{
    public class AnalyticsOptions
    {
        public string ClientContextId { get; set; }
        public bool Pretty { get; set; }
        public bool IncludeMetrics { get; set; }
        public List<Tuple<string, string, bool>> Credentials { get; set; } = new List<Tuple<string, string, bool>>();
        public Dictionary<string, object> NamedParameters { get; set; } = new Dictionary<string, object>();
        public List<object> PositionalParameters { get; set; } = new List<object>();
        public TimeSpan? Timeout { get; set; }
        public int Priority { get; set; }
        public bool Deferred { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public AnalyticsOptions WithClientContextId(string clientContextId)
        {
            ClientContextId = clientContextId;
            return this;
        }

        public AnalyticsOptions WithPretty(bool pretty)
        {
            Pretty = pretty;
            return this;
        }

        public AnalyticsOptions WithIncludeMetrics(bool includeMetrics)
        {
            IncludeMetrics = includeMetrics;
            return this;
        }

        public AnalyticsOptions WithCredential(string username, string password, bool isAdmin)
        {
            Credentials.Add(Tuple.Create(username, password, isAdmin));
            return this;
        }

        public AnalyticsOptions WithNamedParameter(string parameterName, object value)
        {
            if (NamedParameters == null)
            {
                NamedParameters = new Dictionary<string, object>();
            }

            NamedParameters[parameterName] = value;
            return this;
        }

        public AnalyticsOptions WithPositionalParameter(object value)
        {
            if (PositionalParameters == null)
            {
                PositionalParameters = new List<object>();
            }

            PositionalParameters.Add(value);
            return this;
        }

        public AnalyticsOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public AnalyticsOptions WithPriority(bool priority)
        {
            Priority = priority ? -1 : 0;
            return this;
        }

        public AnalyticsOptions WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        public AnalyticsOptions WithDeferred(bool deferred)
        {
            Deferred = deferred;
            return this;
        }

        public AnalyticsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}
