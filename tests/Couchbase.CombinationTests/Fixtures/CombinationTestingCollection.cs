using Couchbase.CombinationTests.Fixtures;
using Xunit;

namespace Couchbase.CombinationTests
{
    [CollectionDefinition(Name)]
    public class CombinationTestingCollection : ICollectionFixture<CouchbaseFixture>
    {
        public const string Name = "CombinationTestingCollection";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
