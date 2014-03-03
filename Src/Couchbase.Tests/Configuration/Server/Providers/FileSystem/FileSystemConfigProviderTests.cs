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
        
        [TestFixtureSetUp]
        public void TestFixureSetup()
        {
            _serverConfig = new FileSystemConfig(PoolsPath);
            _serverConfig.Initialize();
            _provider = new FileSystemConfigProvider(_serverConfig, null);
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
