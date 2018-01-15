using Couchbase.IO.Services;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Services
{
    [TestFixture]
    public class IOServiceBaseTests
    {
        [Test]
        public void Hello_key_is_formatted_with_json_properties()
        {
            const ulong connectionId = 123;
            var result = IOServiceBase.BuildHelloKey(connectionId);

            var expected = JsonConvert.SerializeObject(new
            {
                i = string.Join("/", ClientIdentifier.InstanceId.ToString("x16"), connectionId.ToString("x16")),
                a = ClientIdentifier.GetClientDescription()
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }
    }
}
