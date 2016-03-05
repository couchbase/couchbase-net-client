using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO.Operations.SubDocument;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class SubDocumentAPITests
    {
        [Test]
        public void Test()
        {
            var subDocResultMocked = new Mock<IDocumentFragment<object>>();
            subDocResultMocked.Setup(x => x.Content<string>(It.IsAny<string>())).Returns("foo");
            subDocResultMocked.Setup(x => x.Content<string>(It.IsAny<int>())).Returns("bar");
            subDocResultMocked.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);

            var lookupInMocked = new Mock<ILookupInBuilder<object>>();
            lookupInMocked.Setup(x => x.Get(It.IsAny<string>())).Returns(lookupInMocked.Object);
            lookupInMocked.Setup(x => x.Execute()).Returns(subDocResultMocked.Object);

            var bucketMocked = new Mock<IBucket>();
            bucketMocked.Setup(x => x.LookupIn<object>(It.IsAny<string>())).Returns(lookupInMocked.Object);

            var bucket = bucketMocked.Object;
            var subDocResult = bucket.LookupIn<object>("thekey").
                Get("some.json.path").
                Execute();

            var content = subDocResult.Content<string>("somepath");
            var item = subDocResult.Content<string>(1);
            var exists = subDocResult.Exists("somepathtosomewhere");

            Assert.AreEqual("foo", content);
            Assert.AreEqual("bar", item);
            Assert.IsTrue(exists);
        }

        [Test]
        public void Get_AnyKey_WithPath_ExepectedResult()
        {
            var mockSubdocResult = new Mock<IDocumentFragment<object>>();

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            //mockRequestExecuter.Setup(x => x.SendWithRetry(It.IsAny<SubGet<object>>())).Returns(mockSubdocResult.Object);
        }
    }
}
