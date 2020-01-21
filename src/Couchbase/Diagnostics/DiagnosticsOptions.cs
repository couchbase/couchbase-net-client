namespace Couchbase.Diagnostics
{
    public class DiagnosticsOptions
    {
        public string ReportIdValue { get; set; }

        public DiagnosticsOptions ReportId(string reportId)
        {
            ReportIdValue = reportId;
            return this;
        }
    }
}
