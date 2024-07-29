using Xunit;

namespace Couchbase.Test.Common
{
    [CollectionDefinition(Name, DisableParallelization = true)]

    public class NonParallelDefinition
    {
        public const string Name = "NonParallel";
    }
}
