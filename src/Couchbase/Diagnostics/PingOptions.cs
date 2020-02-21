using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Diagnostics
{
    public class PingOptions
    {
        internal string ReportIdValue { get; set; }

        internal IList<ServiceType> ServiceTypesValue { get; set; } = new List<ServiceType>
        {
            ServiceType.Analytics,
            ServiceType.KeyValue,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Views
        };

        internal CancellationToken Token { get; set; } = System.Threading.CancellationToken.None;

        public PingOptions ReportId(string reportId)
        {
            ReportIdValue = reportId;
            return this;
        }

        public PingOptions ServiceTypes(params ServiceType[] serviceTypes)
        {
            ServiceTypesValue = serviceTypes;
            return this;
        }

        public PingOptions CancellationToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }
}
