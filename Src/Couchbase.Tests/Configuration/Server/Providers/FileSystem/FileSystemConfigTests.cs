using Couchbase.Configuration.Server.Providers.FileSystem;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.FileSystem
{
    [TestFixture]
    public class FileSystemConfigTests
    {
        private const string BootstrapFilePath = @"Data\\Configuration\\bootstrap.json";
        private FileSystemConfig _config;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _config = new FileSystemConfig(BootstrapFilePath);
            _config.Initialize();
        }

        [Test]
        public void Test_Boostrap_Is_Not_Null()
        {
            Assert.IsNotNull(_config.Bootstrap);
        }

        [Test]
        public void Test_Buckets_Is_Not_Null()
        {
            Assert.IsNotNull(_config.Buckets);
            Assert.IsNotEmpty(_config.Buckets);
            Assert.AreEqual(_config.Buckets.Count, _config.StreamingHttp.Count);
        }

        [Test]
        public void Test_Pools_Is_Not_Null()
        {
            Assert.IsNotNull(_config.Pools);
        }

        [Test]
        public void Test_Streaming_Is_Not_Null()
        {
             Assert.IsNotNull(_config.StreamingHttp);
             Assert.IsNotEmpty(_config.StreamingHttp);
             Assert.AreEqual(_config.StreamingHttp.Count, 3);
        }
    }
}
