namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    public interface IOrphanReporter
    {
        void Add(OrphanSummary orphanSummary);
    }
}
