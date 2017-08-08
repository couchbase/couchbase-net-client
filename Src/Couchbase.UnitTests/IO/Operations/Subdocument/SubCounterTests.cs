using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations.SubDocument;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Subdocument
{
    [TestFixture]
    public class SubCounterTests
    {
        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var invoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(invoker.Object, () => new DefaultSerializer(), "key",
                new List<OperationSpec> { new OperationSpec() });

            var op = new SubCounter<dynamic>(builder, "key", null, new DefaultTranscoder(), 0)
            {
                Expires = 10
            };

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as SubCounter<dynamic>;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
