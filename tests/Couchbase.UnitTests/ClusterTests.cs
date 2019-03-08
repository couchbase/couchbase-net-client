using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterTests
    {
        [Fact]
        public void Authenticate_Throws_ArgumentNullException_When_Credentials_Not_Provided()
        {
            Assert.Throws<ArgumentNullException>(() => new Cluster(new Configuration()));
        }
    }
}
