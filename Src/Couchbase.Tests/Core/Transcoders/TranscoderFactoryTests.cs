using System.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core.Transcoders;
using Couchbase.Tests.Fakes;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Transcoders
{
    [TestFixture]
    public class TranscoderFactoryTests
    {
        [Test]
        public void When_Custom_Converter_Configured_In_AppConfig_It_Is_Returned()
        {
            var config = new ClientConfiguration();
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            var transcoder = TranscoderFactory.GetTranscoder(config, section.Transcoder);
            Assert.IsNotNull(transcoder);
            Assert.IsInstanceOf<FakeTranscoder>(transcoder());
        }
    }
}
