using System;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core
{
    [TestFixture]
    public class MutateInBuilderTests
    {
        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Replace_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.Replace(path, "somevalue"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Upsert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.Upsert(path, "somevalue", false));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Insert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.Insert(path, "somevalue", false));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Remove_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.Remove(path));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Counter_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.Counter(path, 0, false));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ArrayAddUnique_DoesNotThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.ArrayAddUnique(path, 0);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ArrayInsert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            Assert.Throws<ArgumentException>(() => builder.ArrayInsert(path, 0));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ArrayAppend_DoesNotThrowArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(),  "thekey");

            builder.ArrayAppend(path, 1, false);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ArrayPrepend_DoesNotThrowArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.ArrayPrepend(path, 1, false);
        }

        [Test]
        public void Insert_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.Insert("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubDictAdd &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void Replace_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.Replace("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubReplace &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void Upsert_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.Upsert("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubDictUpsert &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void Remove_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.Remove("path", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubDelete &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags
                    )
                ), Times.Once
            );
        }

        [Test]
        public void Counter_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.Counter("path", 100, pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubCounter &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (long) builder.FirstSpec().Value == 100
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayAddUnique_Single_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.ArrayAddUnique("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayAddUnique &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayAppend_Single_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.ArrayAppend("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayPushLast &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayAppend_Multiple_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var value = new object[] { 1, 2, 3};
            var result = mutateBuilder.ArrayAppend("path", pathFlags, docFlags, value)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayPushLast &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        builder.FirstSpec().Value == value
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayPrepend_Single_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.ArrayPrepend("path", "value", pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayPushFirst &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (string) builder.FirstSpec().Value == "value"
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayPrepend_Multiple_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<
                IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var value = new object[] { 1, 2, 3 };
            var result = mutateBuilder.ArrayPrepend("path", pathFlags, docFlags, value)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayPushFirst &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        builder.FirstSpec().Value == value
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayInsert_Single_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var result = mutateBuilder.ArrayInsert("path", 1, pathFlags, docFlags)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayInsert &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        (int) builder.FirstSpec().Value == 1
                    )
                ), Times.Once
            );
        }

        [Test]
        public void ArrayInsert_Multiple_For_Xattr_Sets_Correct_Flag()
        {
            const SubdocPathFlags pathFlags = SubdocPathFlags.Xattr;
            const SubdocDocFlags docFlags = SubdocDocFlags.InsertDocument;

            var mockResult = new Mock<IDocumentFragment<dynamic>>();

            var mockedInvoker = new Mock<ISubdocInvoker>();
            mockedInvoker.Setup(x => x.Invoke(It.IsAny<MutateInBuilder<dynamic>>()))
                .Returns(mockResult.Object);

            var mutateBuilder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "mykey");

            var value = new object[] { 1, 2, 3 };
            var result = mutateBuilder.ArrayInsert("path", pathFlags, docFlags, value)
                .Execute();

            Assert.AreSame(mockResult.Object, result);
            mockedInvoker.Verify(
                invoker => invoker.Invoke(It.Is<MutateInBuilder<dynamic>>(
                    builder =>
                        builder.FirstSpec().OpCode == OperationCode.SubArrayInsert &&
                        builder.FirstSpec().Path == "path" &&
                        builder.FirstSpec().PathFlags == pathFlags &&
                        builder.FirstSpec().DocFlags == docFlags &&
                        builder.FirstSpec().Value == value
                    )
                ), Times.Once
            );
        }
    }
}
