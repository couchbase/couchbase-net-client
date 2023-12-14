using Xunit;

namespace Couchbase.Stellar.CombinationTests.Fixtures
{
    [CollectionDefinition(Name)]
    public class StellarTestCollection : ICollectionFixture<StellarFixture>
    {
        public const string Name = "XUnitCollection";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
