using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Couchbase.Analytics
{
    public class AnalyticsOptions
    {
        //note in a future commit this will be private and AnalyticsOptions will be sent to AnalyticsClient instead of AnalyticsRequest (legacy)
        internal string ClientContextIdValue;
        internal Dictionary<string, object> NamedParameters  = new Dictionary<string, object>();
        internal List<object> PositionalParameters = new List<object>();
        internal TimeSpan TimeoutValue = TimeSpan.FromMilliseconds(75000);
        internal CancellationToken Token = System.Threading.CancellationToken.None;
        internal AnalyticsScanConsistency ScanConsistencyValue = Analytics.AnalyticsScanConsistency.NotBounded;
        internal bool ReadonlyValue;
        internal int PriorityValue { get; set; } = 0;

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
        /// Gets a <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the analytics service.
        /// </summary>
        /// <returns>
        /// The <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the analytics service.
        /// </returns>
        internal IDictionary<string, object> GetFormValues(string statement)
        {
            var formValues = new Dictionary<string, object>
            {
                {"statement", statement}
            };

            foreach (var parameter in NamedParameters)
            {
                formValues.Add(parameter.Key, parameter.Value);
            }

            if (PositionalParameters.Any())
            {
                formValues.Add("args", PositionalParameters.ToArray());
            }

            formValues.Add("timeout", $"{TimeoutValue.TotalMilliseconds}ms");
            formValues.Add("client_context_id", ClientContextIdValue);

            return formValues;
        }

    }
}
