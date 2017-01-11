using NUnit.Framework;

#if NETCORE
using Couchbase.Logging;
using Microsoft.Extensions.Logging;
#endif

namespace Couchbase.IntegrationTests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void Setup()
        {
#if NETCORE
            var factory = new LoggerFactory();
            factory.AddDebug();
            LogManager.ConfigureLoggerFactory(factory);
#endif

            ClusterHelper.Initialize(Utils.TestConfiguration.GetCurrentConfiguration());
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
