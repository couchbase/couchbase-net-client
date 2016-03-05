using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core
{
    [TestFixture]
    public class LookupBuilderTests
    {
        [Test]
        public void GetCommands_Enumerates_ExactlyThreeLookups()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var count = ((LookupInBuilder<dynamic>) builder.Get("boo.foo").Exists("foo.boo").Get("boo.foo")).GetEnumerator().Count();
            Assert.AreEqual(3, count);
        }
    }
}
