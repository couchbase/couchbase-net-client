using Couchbase.Configuration;
using Couchbase.Configuration.Server.Providers.FileSystem;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.FileSystem
{
    [TestFixture]
    public class FileSystemConfigProviderTests
    {
        private const string PoolsPath = @"Data\\Configuration\\bootstrap.json";
        private FileSystemConfig _serverConfig;
        private FileSystemConfigProvider _provider;

        [OneTimeSetUp]
        public void TestFixureSetup()
        {
            ConfigUtil.EnsureConfigExtracted();

            _serverConfig = new FileSystemConfig(PoolsPath);
            _serverConfig.Initialize();
            _provider = new FileSystemConfigProvider(_serverConfig, null, PoolsPath);
        }

        [Test]
        public void Test_Get()
        {
            const string bucketName = "default";
            IConfigInfo configInfo = _provider.GetConfig(bucketName);
            Assert.IsNotNull(configInfo);
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