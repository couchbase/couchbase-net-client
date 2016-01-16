using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
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
            var builder = new LookupInBuilder(mockedInvoker.Object, "mykey");

            var count = ((LookupInBuilder) builder.Get("boo.foo").Exists("foo.boo").Get("boo.foo")).GetEnumerator().Count();
            Assert.AreEqual(3, count);
        }
    }
}
