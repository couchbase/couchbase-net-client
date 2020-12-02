using BenchmarkDotNet.Jobs;

namespace Couchbase.LoadTests
{
    public class DontForceGcCollectionsConfig : StandardConfig
    {
        public DontForceGcCollectionsConfig()
        {
            AddJob(Job.Default
                .WithGcMode(new GcMode
                {
                    Force = false
                }));
        }
    }
}
