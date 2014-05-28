using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Strategies.EAP;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class SaslFactoryTests
    {
        [Test]
        public void Test_GetFactory()
        {
            var factory = SaslFactory.GetFactory();
            Assert.IsNotNull(factory);
        }

        [Test]
        public void When_PlainText_Provided_Factory_Returns_PlainTextMechanism()
        {
            var factory = SaslFactory.GetFactory();
            var mechanism = factory("authenticated", "secret", "PLAIN");
            Assert.IsTrue(mechanism is PlainTextMechanism);
        }

        [Test]
        public void When_PlainText_Provided_Factory_Returns_CramMd5Mechanism()
        {
            var factory = SaslFactory.GetFactory();
            var mechanism = factory("authenticated", "secret", "CRAMMD5");
            Assert.IsTrue(mechanism is CramMd5Mechanism);
        }
    }
}
