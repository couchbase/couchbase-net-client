using System;
using System.Threading.Tasks;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterTests
    {
        [Fact]
        public async Task Authenticate_Throws_ArgumentNullException_When_Crendentials_Not_Provided()
        {

            var cluster = new Cluster();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await cluster.Initialize(new Configuration()).ConfigureAwait(false));
        }
    }
}
