using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using NUnit.Framework;
using Couchbase.IO.Operations.SubDocument;
using Moq;

namespace Couchbase.UnitTests.IO.Operations.Subdocument
{
    [TestFixture]
    public class SubDocDictUpsertTests
    {
        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var invoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(invoker.Object, () => new DefaultSerializer(), "key",
                new List<OperationSpec> { new OperationSpec() });

            var op = new SubDocDictUpsert<dynamic>(builder, "key", null, new DefaultTranscoder(), 0)
            {
                Expires = 10
            };

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as SubDocDictUpsert<dynamic>;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
