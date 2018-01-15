namespace Couchbase.Tracing
{
    internal class NullOrphanedOperationReporter : IOrphanedOperationReporter
    {
        public static IOrphanedOperationReporter Instance = new NullOrphanedOperationReporter();

        private NullOrphanedOperationReporter()
        { }

        public void Add(string endpoint, string operationId, long? serverDuration)
        { }
    }
}
