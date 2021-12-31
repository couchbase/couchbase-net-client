using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class LookupInResultTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public async Task When_Index_Invalid_IndexNotFoundException_Thrown(int index)
        {
            var bytes = new byte[]
            {
                0x18, 0xd0, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x40,
                0x15, 0xfb, 0x22, 0x64, 0x3e, 0x29, 0x00, 0x00, 0x02, 0x00, 0x0a, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x05, 0x22, 0x62, 0x61, 0x72, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var specs = new LookupInSpecBuilder().Get("foo").Exists("bar").Specs;

            var op = new MultiLookup<byte[]>("thekey", specs);
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));

            var result = new LookupInResult(op.GetCommandValues(), 0, TimeSpan.FromHours(1),  new DefaultSerializer());

            Assert.Throws<InvalidIndexException>(() => result.ContentAs<string>(index));
            var value = result.ContentAs<string>(0);
            Assert.Equal("bar", value);
        }

        [Fact]
        public async Task ContentAs_WhenPathNotFound_ThrowsPathNotFoundException()
        {
            var bytes = new byte[]
            {
                0x18, 0xd0, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x40,
                0x15, 0xfb, 0x22, 0x64, 0x3e, 0x29, 0x00, 0x00, 0x02, 0x00, 0x0a,

                // Index 0 = Success
                0x00, 0x00,
                // Spec Body Length = 5
                0x00, 0x00, 0x00, 0x05,
                // "bar"
                0x22, 0x62, 0x61, 0x72, 0x22,

                // Index 1 = SubDocPathNotFound
                0x00,  0xc0,
                // Spec Body Length = 0
                0x00, 0x00, 0x00, 0x00
            };

            var specs = new LookupInSpecBuilder().Get("foo").Get("bar").Specs;

            var op = new MultiLookup<byte[]>("thekey", specs);
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));

            var result = new LookupInResult(op.GetCommandValues(), 0, TimeSpan.FromHours(1), new DefaultSerializer());

            Assert.Throws<PathNotFoundException>(() => result.ContentAs<string>(1));
        }
    }
}
