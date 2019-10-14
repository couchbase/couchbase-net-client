using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Diagnostics
{
    public class PingOptions
    {
        public string ReportId { get; set; }

        public IList<ServiceType> ServiceTypes { get; set; } = new List<ServiceType>();

        public CancellationToken Token { get; set; } = CancellationToken.None;

        public PingOptions WithReportId(string reportId)
        {
            ReportId = reportId;
            return this;
        }

        public PingOptions WithServiceTypes(params ServiceType[] serviceTypes)
        {
            ServiceTypes = serviceTypes;
            return this;
        }

        public PingOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }
}
