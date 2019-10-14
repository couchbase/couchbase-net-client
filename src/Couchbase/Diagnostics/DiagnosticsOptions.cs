namespace Couchbase.Diagnostics
{
    public class DiagnosticsOptions
    {
        public string ReportId { get; set; }

        public DiagnosticsOptions WithReportId(string reportId)
        {
            ReportId = reportId;
            return this;
        }
    }
}
