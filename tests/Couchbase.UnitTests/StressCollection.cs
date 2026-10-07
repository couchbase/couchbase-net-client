using Xunit;

namespace Couchbase.UnitTests
{
    /// <summary>
    /// For tests that load the machine on purpose, such as many threads blocked on a barrier to provoke a race.
    /// Such a test can block the thread pool for several seconds, and a test that runs beside it then times
    /// out while it waits for a thread (NCBC-4317). xUnit runs this collection after all the others, with no
    /// other test in parallel.
    /// </summary>
    /// <remarks>
    /// Whether a stress test finds a defect depends on how the threads are scheduled, so prefer a
    /// deterministic test where one is possible. Tag each test class in this collection with
    /// <c>[Trait("Category", "Stress")]</c> so that a run can include or exclude them as a group.
    /// </remarks>
    [CollectionDefinition("Stress", DisableParallelization = true)]
    public class StressCollection
    {
    }
}
