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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion