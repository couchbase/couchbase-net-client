namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal class NullOrphanReporter : IOrphanReporter
    {
        public static IOrphanReporter Instance = new NullOrphanReporter();

        public void Add(OrphanSummary orphanSummary) { }
    }
}
