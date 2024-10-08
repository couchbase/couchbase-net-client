using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Helpers;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class GetResultTests
    {
        readonly byte[] _lookupInPacket = new byte[901]
            {
                0x81, 0xd0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x6d, 0x00, 0x00, 0x00, 0x19, 0x15,
                0x87, 0x10, 0x16, 0x4e, 0xf7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x22, 0x45, 0x6d, 0x6d,
                0x79, 0x2d, 0x6c, 0x6f, 0x75, 0x20, 0x44, 0x69, 0x63, 0x6b, 0x65, 0x72, 0x73, 0x6f, 0x6e, 0x22, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x02, 0x32, 0x36, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x5b, 0x22, 0x63, 0x61,
                0x74, 0x22, 0x2c, 0x20, 0x22, 0x64, 0x6f, 0x67, 0x22, 0x2c, 0x20, 0x22, 0x70, 0x61, 0x72, 0x72, 0x6f,
                0x74, 0x22, 0x5d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x22, 0x64, 0x6f, 0x67, 0x22, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x58, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x22, 0x68, 0x61, 0x69, 0x72, 0x22, 0x3a, 0x20, 0x22,
                0x62, 0x72, 0x6f, 0x77, 0x6e, 0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x22, 0x64, 0x69, 0x6d, 0x65, 0x6e,
                0x73, 0x69, 0x6f, 0x6e, 0x73, 0x22, 0x3a, 0x20, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x22, 0x68, 0x65,
                0x69, 0x67, 0x68, 0x74, 0x22, 0x3a, 0x20, 0x36, 0x37, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x22, 0x77,
                0x65, 0x69, 0x67, 0x68, 0x74, 0x22, 0x3a, 0x20, 0x31, 0x37, 0x35, 0x0d, 0x0a, 0x09, 0x09, 0x7d, 0x2c,
                0x0d, 0x0a, 0x09, 0x09, 0x22, 0x68, 0x6f, 0x62, 0x62, 0x69, 0x65, 0x73, 0x22, 0x3a, 0x20, 0x5b, 0x7b,
                0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x74, 0x79, 0x70, 0x65, 0x22, 0x3a, 0x20, 0x22, 0x77, 0x69,
                0x6e, 0x74, 0x65, 0x72, 0x20, 0x73, 0x70, 0x6f, 0x72, 0x74, 0x73, 0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09,
                0x09, 0x09, 0x22, 0x6e, 0x61, 0x6d, 0x65, 0x22, 0x3a, 0x20, 0x22, 0x63, 0x75, 0x72, 0x6c, 0x69, 0x6e,
                0x67, 0x22, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7d, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7b, 0x0d, 0x0a,
                0x09, 0x09, 0x09, 0x09, 0x22, 0x74, 0x79, 0x70, 0x65, 0x22, 0x3a, 0x20, 0x22, 0x73, 0x75, 0x6d, 0x6d,
                0x65, 0x72, 0x20, 0x73, 0x70, 0x6f, 0x72, 0x74, 0x73, 0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09,
                0x22, 0x6e, 0x61, 0x6d, 0x65, 0x22, 0x3a, 0x20, 0x22, 0x77, 0x61, 0x74, 0x65, 0x72, 0x20, 0x73, 0x6b,
                0x69, 0x69, 0x6e, 0x67, 0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x64, 0x65, 0x74, 0x61,
                0x69, 0x6c, 0x73, 0x22, 0x3a, 0x20, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6c, 0x6f,
                0x63, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0x22, 0x3a, 0x20, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09,
                0x09, 0x22, 0x6c, 0x61, 0x74, 0x22, 0x3a, 0x20, 0x34, 0x39, 0x2e, 0x32, 0x38, 0x32, 0x37, 0x33, 0x30,
                0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6c, 0x6f, 0x6e, 0x67, 0x22, 0x3a, 0x20,
                0x2d, 0x31, 0x32, 0x33, 0x2e, 0x31, 0x32, 0x30, 0x37, 0x33, 0x35, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09,
                0x09, 0x7d, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x7d, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7d, 0x0d, 0x0a,
                0x09, 0x09, 0x5d, 0x0d, 0x0a, 0x09, 0x7d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x22, 0x62, 0x72, 0x6f,
                0x77, 0x6e, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2a, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x22, 0x68,
                0x65, 0x69, 0x67, 0x68, 0x74, 0x22, 0x3a, 0x20, 0x36, 0x37, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x22,
                0x77, 0x65, 0x69, 0x67, 0x68, 0x74, 0x22, 0x3a, 0x20, 0x31, 0x37, 0x35, 0x0d, 0x0a, 0x09, 0x09, 0x7d,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x36, 0x37, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x37, 0x35,
                0x00, 0x00, 0x00, 0x00, 0x00, 0xf3, 0x5b, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x74, 0x79,
                0x70, 0x65, 0x22, 0x3a, 0x20, 0x22, 0x77, 0x69, 0x6e, 0x74, 0x65, 0x72, 0x20, 0x73, 0x70, 0x6f, 0x72,
                0x74, 0x73, 0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6e, 0x61, 0x6d, 0x65, 0x22, 0x3a,
                0x20, 0x22, 0x63, 0x75, 0x72, 0x6c, 0x69, 0x6e, 0x67, 0x22, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7d, 0x2c,
                0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x74, 0x79, 0x70, 0x65,
                0x22, 0x3a, 0x20, 0x22, 0x73, 0x75, 0x6d, 0x6d, 0x65, 0x72, 0x20, 0x73, 0x70, 0x6f, 0x72, 0x74, 0x73,
                0x22, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6e, 0x61, 0x6d, 0x65, 0x22, 0x3a, 0x20, 0x22,
                0x77, 0x61, 0x74, 0x65, 0x72, 0x20, 0x73, 0x6b, 0x69, 0x69, 0x6e, 0x67, 0x22, 0x2c, 0x0d, 0x0a, 0x09,
                0x09, 0x09, 0x09, 0x22, 0x64, 0x65, 0x74, 0x61, 0x69, 0x6c, 0x73, 0x22, 0x3a, 0x20, 0x7b, 0x0d, 0x0a,
                0x09, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6c, 0x6f, 0x63, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0x22, 0x3a, 0x20,
                0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6c, 0x61, 0x74, 0x22, 0x3a, 0x20, 0x34,
                0x39, 0x2e, 0x32, 0x38, 0x32, 0x37, 0x33, 0x30, 0x2c, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09,
                0x22, 0x6c, 0x6f, 0x6e, 0x67, 0x22, 0x3a, 0x20, 0x2d, 0x31, 0x32, 0x33, 0x2e, 0x31, 0x32, 0x30, 0x37,
                0x33, 0x35, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x7d, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x7d,
                0x0d, 0x0a, 0x09, 0x09, 0x09, 0x7d, 0x0d, 0x0a, 0x09, 0x09, 0x5d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0f,
                0x22, 0x77, 0x69, 0x6e, 0x74, 0x65, 0x72, 0x20, 0x73, 0x70, 0x6f, 0x72, 0x74, 0x73, 0x22, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x0e, 0x22, 0x77, 0x61, 0x74, 0x65, 0x72, 0x20, 0x73, 0x6b, 0x69, 0x69, 0x6e, 0x67,
                0x22, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3d, 0x7b, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x22,
                0x6c, 0x61, 0x74, 0x22, 0x3a, 0x20, 0x34, 0x39, 0x2e, 0x32, 0x38, 0x32, 0x37, 0x33, 0x30, 0x2c, 0x0d,
                0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x22, 0x6c, 0x6f, 0x6e, 0x67, 0x22, 0x3a, 0x20, 0x2d, 0x31,
                0x32, 0x33, 0x2e, 0x31, 0x32, 0x30, 0x37, 0x33, 0x35, 0x0d, 0x0a, 0x09, 0x09, 0x09, 0x09, 0x09, 0x7d,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x0b, 0x2d, 0x31, 0x32, 0x33, 0x2e, 0x31, 0x32, 0x30, 0x37, 0x33, 0x35
            };

        private List<LookupInSpec> _lookupInSpecs = new List<LookupInSpec>
        {
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "name"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "age"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "animals"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "animals[1]"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hair"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.dimensions"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.dimensions.height"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.dimensions.weight"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hobbies"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hobbies[0].type"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hobbies[1].name"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hobbies[1].details.location"},
            new LookupInSpec {OpCode = OpCode.SubGet, Path = "attributes.hobbies[1].details.location.long"}
        };

        public class Dimensions
        {
            public int height { get; set; }
            public int weight { get; set; }
        }

        public class Location
        {
            public double lat { get; set; }
            public double @long { get; set; }
        }

        public class Details
        {
            public Location location { get; set; }
        }

        public class Hobby
        {
            public string type { get; set; }
            public string name { get; set; }
            public Details details { get; set; }
        }

        public class Attributes
        {
            public string hair { get; set; }
            public Dimensions dimensions { get; set; }
            public List<Hobby> hobbies { get; set; }
        }

        public class Person
        {
            public string name { get; set; }
            public int age { get; set; }
            public List<string> animals { get; set; }
            public Attributes attributes { get; set; }
        }

        [Fact]
        public void Test_Projection()
        {
            var getRequest = new MultiLookup<byte[]>("thekey", Array.Empty<LookupInSpec>());
            getRequest.Read(new FakeMemoryOwner<byte>(_lookupInPacket));

            var readResult = new GetResult(getRequest.ExtractBody(),
                new LegacyTranscoder(), new Mock<ILogger<GetResult>>().Object, NullFallbackTypeSerializerProvider.Instance,
                _lookupInSpecs)
            {
                OpCode = OpCode.MultiLookup,
                Flags = getRequest.Flags,
                Header = getRequest.Header
            };

            var result = readResult.ContentAs<dynamic>();
            Assert.Equal("Emmy-lou Dickerson",result.name.Value);
        }

        [Fact]
        public void Test_Projection_With_Poco()
        {
            var getRequest = new MultiLookup<byte[]>("thekey", Array.Empty<LookupInSpec>());
            getRequest.Read(new FakeMemoryOwner<byte>(_lookupInPacket));

            var readResult = new GetResult(getRequest.ExtractBody(),
                new LegacyTranscoder(), new Mock<ILogger<GetResult>>().Object, NullFallbackTypeSerializerProvider.Instance,
                _lookupInSpecs)
            {
                OpCode = OpCode.MultiLookup,
                Flags = getRequest.Flags,
                Header = getRequest.Header
            };

            var result = readResult.ContentAs<Person>();
            Assert.Equal("Emmy-lou Dickerson",result.name);
        }

        [Fact]
        public void Test_Projection_With_Dictionary()
        {
            var getRequest = new MultiLookup<byte[]>("thekey", Array.Empty<LookupInSpec>());
            getRequest.Read(new FakeMemoryOwner<byte>(_lookupInPacket));

            var readResult = new GetResult(getRequest.ExtractBody(),
                new LegacyTranscoder(), new Mock<ILogger<GetResult>>().Object, NullFallbackTypeSerializerProvider.Instance,
                _lookupInSpecs)
            {
                OpCode = OpCode.MultiLookup,
                Flags = getRequest.Flags,
                Header = getRequest.Header
            };

            var result = readResult.ContentAs<Dictionary<string, dynamic>>();
            Assert.Equal(result["name"], "Emmy-lou Dickerson");
        }

        [Fact]
        public void Test_ExpiryTime_Returns_Null_When_Expiry_Not_An_Option()
        {
            var getRequest = new MultiLookup<byte[]>("thekey", Array.Empty<LookupInSpec>());
            getRequest.Read(new FakeMemoryOwner<byte>(_lookupInPacket));

            var readResult = new GetResult(getRequest.ExtractBody(),
                new LegacyTranscoder(), new Mock<ILogger<GetResult>>().Object, NullFallbackTypeSerializerProvider.Instance,
                _lookupInSpecs)
            {
                OpCode = OpCode.MultiLookup,
                Flags = getRequest.Flags,
                Header = getRequest.Header
            };

            var expiryTime = readResult.ExpiryTime;
            Assert.Null(expiryTime);
        }

        [Fact]
        public void Test_OperationBase_Throws_InvalidArgumentException_When_Key_Is_Too_Big()
        {
            var bytes = Enumerable.Repeat((byte)0, 251).ToArray();
            var key = Encoding.UTF8.GetString(bytes);
            try
            {
                var getOp = new Get<byte[]>
                {
                    Key = key
                };
            }
            catch (Exception e)
            {
                Assert.IsType<InvalidArgumentException>(e);
            }
        }

        [Fact]
        public void Test_OperationBase_Throws_InvalidArgumentException_When_Key_Is_Null()
        {
            try
            {
                var getOp = new Get<byte[]>
                {
                    Key = null
                };
            }
            catch (Exception e)
            {
                Assert.IsType<InvalidArgumentException>(e);
            }
        }

        [Fact]
        public void Test_GetAndTouch_ReadsExpiryNotMutationToken()
        {
            // NCBC-3852
            byte[] responseBytes =
            [
                0x18, 0x1d, 0x03, 0x00,
                0x04, 0x01, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x17,
                0x1c, 0x00, 0x00, 0x00,
                0x17, 0xf1, 0xc7, 0xcb,
                0xf7, 0x9f, 0x00, 0x00,
                0x02, 0x00, 0x0d, 0x02,
                0x00, 0x00, 0x01, 0x5b,
                0x22, 0x41, 0x42, 0x43,
                0x44, 0x45, 0x31, 0x32,
                0x33, 0x34, 0x35, 0x36,
                0x37, 0x22, 0x5d
            ];

            var getAndTouch = new GetT<byte[]>("default", "Test123");
            getAndTouch.Expires = 1234567;
            SlicedMemoryOwner<byte> responsePacket =  new(new FakeMemoryOwner<byte>(new Memory<byte>(responseBytes)));
            getAndTouch.Read(responsePacket);
            Assert.NotEqual((uint)1234567, getAndTouch.Expires);
            Assert.Null(getAndTouch.MutationToken);
        }
    }
}
