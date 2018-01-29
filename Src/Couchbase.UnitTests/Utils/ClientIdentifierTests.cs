using Couchbase.IO.Operations;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.Utils
{
    [TestFixture]
    public class ClientIdentifierTests
    {
        [Test]
        public void InstanceId_is_not_empty()
        {
            Assert.IsTrue(ClientIdentifier.InstanceId > 0);
        }

        [Test]
        public void BuidConnectionId_returns_expected_json_structure()
        {
            var connectionId = SequenceGenerator.GetRandomLong();
            var expected = $"{ClientIdentifier.InstanceId:x16}/{connectionId:x16}";

            Assert.AreEqual(expected, ClientIdentifier.FormatConnectionString(connectionId));
        }
    }
}
