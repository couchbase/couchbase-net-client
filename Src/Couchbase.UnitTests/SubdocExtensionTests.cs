using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Serialization;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class SubdocExtensionTests
    {
        #region LookupInBuilder

        [Test]
        public void Get_WithExpression_CallsGetWithString()
        {
            // Arrange

            var builder = new Mock<ILookupInBuilder<MyDoc>>();
            builder.Setup(m => m.Get(It.IsAny<string>())).Returns((string path) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Get(p => p.Prop);

            // Assert

            builder.Verify(m => m.Get("`prop`"), Times.Once);
        }

        [Test]
        public void Get_WithDynamicExpression_CallsGetWithString()
        {
            // Arrange

            var builder = new Mock<ILookupInBuilder<MyDoc>>();
            builder.Setup(m => m.Get(It.IsAny<string>())).Returns((string path) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Get(p => p.Dynamic);

            // Assert

            builder.Verify(m => m.Get("`dynamic`"), Times.Once);
        }

        [Test]
        public void Exists_WithExpression_CallsExistsWithString()
        {
            // Arrange

            var builder = new Mock<ILookupInBuilder<MyDoc>>();
            builder.Setup(m => m.Exists(It.IsAny<string>())).Returns((string path) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Exists(p => p.Prop);

            // Assert

            builder.Verify(m => m.Exists("`prop`"), Times.Once);
        }

        #endregion

        #region MutateInBuilder

        [Test]
        public void Insert_WithExpression_CallsInsertWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Insert(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Insert(p => p.Prop, fakeValue, true);

            // Assert

            builder.Verify(m => m.Insert("`prop`", fakeValue, true), Times.Once);
        }

        [Test]
        public void Upsert_WithExpression_CallsUpsertWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Upsert(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Upsert(p => p.Prop, fakeValue, true);

            // Assert

            builder.Verify(m => m.Upsert("`prop`", fakeValue, true), Times.Once);
        }

        [Test]
        public void Upsert_WithDynamicExpression_CallsUpsertWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Upsert(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Upsert(p => p.Dynamic, fakeValue, true);

            // Assert

            builder.Verify(m => m.Upsert("`dynamic`", It.IsAny<object>(), true), Times.Once);
        }

        [Test]
        public void Replace_WithExpression_CallsReplaceWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Replace(It.IsAny<string>(), It.IsAny<object>()))
                .Returns((string path, object value) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Replace(p => p.Prop, fakeValue);

            // Assert

            builder.Verify(m => m.Replace("`prop`", fakeValue), Times.Once);
        }

        [Test]
        public void Remove_WithExpression_CallsRemoveWithString()
        {
            // Arrange

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Remove(It.IsAny<string>()))
                .Returns((string path) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Remove(p => p.Prop);

            // Assert

            builder.Verify(m => m.Remove("`prop`"), Times.Once);
        }

        [Test]
        public void ArrayAppend_WithExpression_CallsPushBackWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.ArrayAppend(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.ArrayAppend(p => p.Array, fakeValue, true);

            // Assert

            builder.Verify(m => m.ArrayAppend("`array`", fakeValue, true), Times.Once);
        }

        [Test]
        public void ArrayPrepend_WithExpression_CallsPushFrontWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.ArrayPrepend(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.ArrayPrepend(p => p.Array, fakeValue, true);

            // Assert

            builder.Verify(m => m.ArrayPrepend("`array`", fakeValue, true), Times.Once);
        }

        [Test]
        public void ArrayInsert_WithExpression_CallsArrayInsertWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.ArrayInsert(It.IsAny<string>(), It.IsAny<object>()))
                .Returns((string path, object value) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.ArrayInsert(p => p.Array[3], fakeValue);

            // Assert

            builder.Verify(m => m.ArrayInsert("`array`[3]", fakeValue), Times.Once);
        }

        [Test]
        public void AddUnique_WithExpression_CallsAddUniqueWithString()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.ArrayAddUnique(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                .Returns((string path, object value, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.ArrayAddUnique(p => p.Array, fakeValue, true);

            // Assert

            builder.Verify(m => m.ArrayAddUnique("`array`", fakeValue, true), Times.Once);
        }

        [Test]
        public void Counter_WithExpression_CallsCounterWithString()
        {
            // Arrange

            var fakeDelta = 123L;

            var builder = new Mock<IMutateInBuilder<MyDoc>>();
            builder.Setup(m => m.Counter(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Returns((string path, long delta, bool createParents) => builder.Object);
            builder.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            builder.Object.Counter(p => p.Prop, fakeDelta, true);

            // Assert

            builder.Verify(m => m.Counter("`prop`", fakeDelta, true), Times.Once);
        }

        #endregion

        #region DocumentFragment

        [Test]
        public void DocumentFragmentContent_WithExpression_CallsContentWithString()
        {
            // Arrange

            var fakeContent = "abc";

            var fragment = new Mock<IDocumentFragment<MyDoc>>();
            fragment.Setup(m => m.Content<string>(It.IsAny<string>())).Returns(fakeContent);
            fragment.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            var result = fragment.Object.Content(p => p.Prop);

            // Assert

            fragment.Verify(m => m.Content<string>("`prop`"), Times.Once);
            Assert.AreEqual(fakeContent, result);
        }

        [Test]
        public void DocumentFragmentExists_WithExpression_CallsExistsWithString()
        {
            // Arrange

            var fragment = new Mock<IDocumentFragment<MyDoc>>();
            fragment.Setup(m => m.Exists(It.IsAny<string>())).Returns(true);
            fragment.As<ITypeSerializerProvider>().Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            var result = fragment.Object.Exists(p => p.Prop);

            // Assert

            fragment.Verify(m => m.Exists("`prop`"), Times.Once);
            Assert.AreEqual(true, result);
        }

        #endregion

        #region Helpers

        public class MyDoc
        {
            public string Prop { get; set; }
            public string[] Array { get; set; }
            public dynamic Dynamic { get; set; }
        }

        #endregion
    }
}
