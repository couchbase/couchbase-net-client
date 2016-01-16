using System;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
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
        [ExpectedException(typeof(ArgumentException))]
        public void Replace_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.Replace(path, "somevalue");
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void Upsert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.Upsert(path, "somevalue", false);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void Insert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.Insert(path, "somevalue", false);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void Remove_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.Remove(path);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void Counter_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.Counter(path, 0, false);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void AddUnique_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.AddUnique(path, 0);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void ArrayInsert_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.ArrayInsert(path, 0);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [ExpectedException(typeof(ArgumentException))]
        public void PushBack_ThrowsArgumentException_WhenPathIsEmpty(string path)
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder(mockedInvoker.Object, "thekey");

            builder.PushBack(path, 0, false);
        }
    }
}
