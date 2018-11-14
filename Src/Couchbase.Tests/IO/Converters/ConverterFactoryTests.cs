#if NET452
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
using Couchbase.IO.Converters;
using Couchbase.Tests.Fakes;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Converters
{
    [TestFixture]
    public class ConverterFactoryTests
    {
        [Test]
        public void When_Custom_Converter_Configured_In_AppConfig_It_Is_Returned()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            var converter = ConverterFactory.GetConverter(section.Converter);
            Assert.IsNotNull(converter);
            Assert.IsInstanceOf<FakeConverter>(converter());
        }
    }
}
#endif
