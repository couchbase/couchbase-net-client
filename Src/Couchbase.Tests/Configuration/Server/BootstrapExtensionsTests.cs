using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Providers.FileSystem;
using Couchbase.Configuration.Server.Serialization;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server
{
    [TestFixture]
    public class BootstrapExtensionsTests
    {
        [Test]
        public void Test_GetPoolsUri()
        {
            var bootstrap = new Bootstrap
            {
                Pools = new[]
                {
                    new Pool {Uri = @"/pools/default?uuid=7453ffa825acb58612182ed719eaf9a4"}
                }
            };
            var baseUri = new Uri("http://localhost:8091/pools");
            var actual = bootstrap.GetPoolsUri(baseUri);
            var expected = new Uri(@"http://localhost:8091/pools/default?uuid=7453ffa825acb58612182ed719eaf9a4");
            Assert.AreEqual(expected, actual);
        }
    }
}
