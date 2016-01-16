using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.SubDocument;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    [TestFixture]
    public class MultiLookupTests
    {
        [Test]
        public void Test()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder(mockedInvoker.Object, "mykey");

            var op = new MultiLookup<MyDoc>(builder.Key, builder, new VBucket(null, 1, 1, null, 0, null),
                new DefaultTranscoder(new DefaultConverter()), 10);

            builder.Get("foo.bar");

            var bytes = op.Write();
        }

        [Test]
        public void When_Success_Return_Fragment()
        {
            var response = new byte[]
            {
                129, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 51, 0, 0, 0, 1, 0,
                0, 0, 0, 0, 0, 0, 0, 83, 67, 82, 65, 77, 45, 83,
                72, 65, 53, 49, 50, 32, 83, 67, 82, 65, 77, 45, 83, 72,
                65, 50, 53, 54, 32, 83, 67, 82, 65, 77, 45, 83, 72,
                65, 49, 32, 67, 82, 65, 77,  45, 77, 68, 53, 32,
                80, 76, 65, 73, 78
            };

            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder(mockedInvoker.Object, "mykey");

            var op = new MultiLookup<MyDoc>(builder.Key, builder, new VBucket(null, 1, 1, null, 0, null),
                new DefaultTranscoder(new DefaultConverter()), 10);

            op.Data.Write(response, 0, response.Length);
            var result = op.GetResultWithValue();
        }

        public class MyDoc
        {

        }
    }
}
