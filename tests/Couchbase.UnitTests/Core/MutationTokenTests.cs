using System.Linq;
using Couchbase.Core;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    public class MutationTokenTests
    {
        [Fact]
        public void GetHashCode_Returns_Unique_Values()
        {
          var hashes = Enumerable.Range(1, 100).Select(i => new MutationToken("a", (short) i, i, i).GetHashCode()).ToList();
          Assert.True(hashes.Distinct().Count() == hashes.Count);
        }
    }
}
