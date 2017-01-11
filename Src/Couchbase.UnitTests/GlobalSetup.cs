#if NETCORE
using Couchbase.Logging;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void Setup()
        {
            var factory = new LoggerFactory();
            factory.AddDebug();
            LogManager.ConfigureLoggerFactory(factory);
        }
    }
}
#endif
