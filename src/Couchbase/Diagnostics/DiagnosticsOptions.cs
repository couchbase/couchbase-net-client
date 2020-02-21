#nullable enable

namespace Couchbase.Diagnostics
{
    public class DiagnosticsOptions
    {
        internal string? ReportIdValue { get; set; }

        public DiagnosticsOptions ReportId(string reportId)
        {
            ReportIdValue = reportId;
            return this;
        }
    }
}
