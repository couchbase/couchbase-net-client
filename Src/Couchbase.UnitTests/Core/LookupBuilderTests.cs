using System.Linq;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;
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

            var count = ((LookupInBuilder<dynamic>) builder.Get("boo.foo").Exists("foo.boo").Get("boo.foo")).Count();
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Get_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<LookupInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var lookupBuilder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = lookupBuilder.Get("path", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<LookupInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubGet &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags)
                ), Times.Once
            );
        }

        [Test]
        public void Exists_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<LookupInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var lookupBuilder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = lookupBuilder.Exists("path", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<LookupInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubExist &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags)
                ), Times.Once
            );
        }

        [Test]
        public void GetCount_Sends_Correct_OperationCode_And_Path()
        {
            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<LookupInBuilder<dynamic>>())).Returns(mockResult.Object);

            var lookupBuilder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = lookupBuilder.GetCount("path")
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<LookupInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubGetCount &&
                        builder.FirstSpec().Path == "path")
                ), Times.Once
            );
        }
    }
}
