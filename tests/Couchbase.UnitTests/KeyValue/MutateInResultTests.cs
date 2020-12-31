using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class MutateInResultTests
    {
        [Fact]
        public async Task Can_MutateIn_Parse_Flexible_Header_Args()
        {
            var responsePacket = new byte[]
            {
                0x18, 0xd1, 0x03, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x25, 0x00, 0x00, 0x00, 0x44, 0x15, 0xfa,
                0xc3, 0x29, 0x58, 0x19, 0x00, 0x00, 0x02, 0x00, 0x12,
                0x00, 0x00, 0xad, 0x17, 0x52, 0xf2, 0x38, 0x3e, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x16, 0x03, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x02, 0x31, 0x30, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x02, 0x2d, 0x35
            };

            var specs = new MutateInSpecBuilder().
                Upsert("name", "mike").
                Replace("bar", "bar").
                Insert("bah", 0).
                Increment("zzz", 10, true).
                Decrement("xxx", 5, true).Specs;

            var op = new MultiMutation<byte[]>
            {
                Builder = new MutateInBuilder<byte[]>(null, null, "thekey", specs),
                Transcoder = new JsonTranscoder()
            };
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(responsePacket));

            var result = new MutateInResult(op.GetCommandValues(), 0, MutationToken.Empty, new DefaultSerializer());
            Assert.Equal(10, result.ContentAs<int>(3));
            Assert.Equal(-5, result.ContentAs<int>(4));
        }

        [Fact]
        public async Task When_Result_Contains_Values_AllowRead()
        {
            var bytes = new byte[]
            {
                129, 209, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 0, 0, 0, 16, 21, 240,
                165, 234, 46, 107, 0, 0, 3, 0, 0, 0, 0, 0, 2, 49, 48, 4, 0, 0,
                0, 0, 0, 2, 45, 53
            };

            var specs = new MutateInSpecBuilder().Upsert("name", "mike").Replace("bar", "bar").Insert("bah", 0)
                .Increment("zzz", 10, true).Decrement("xxx", 5, true).Specs;

            var op = new MultiMutation<byte[]>
            {
                Builder = new MutateInBuilder<byte[]>(null, null, "thekey", specs),
                Transcoder = new JsonTranscoder()
            };
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));

            var result = new MutateInResult(op.GetCommandValues(), 0, MutationToken.Empty, new DefaultSerializer());

            Assert.Equal(0, result.ContentAs<int>(0));
            Assert.Equal(0, result.ContentAs<int>(1));
            Assert.Equal(0, result.ContentAs<int>(2));
            Assert.Equal(10, result.ContentAs<int>(3));
            Assert.Equal(-5, result.ContentAs<int>(4));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public async Task When_Index_Invalid_IndexNotFoundException_Thrown(int index)
        {
            var bytes = new byte[]
            {
                129, 209, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 0, 0, 0, 16, 21, 240,
                165, 234, 46, 107, 0, 0, 3, 0, 0, 0, 0, 0, 2, 49, 48, 4, 0, 0,
                0, 0, 0, 2, 45, 53
            };

            var specs = new MutateInSpecBuilder().Upsert("name", "mike").Replace("bar", "bar").Insert("bah", 0)
                .Increment("zzz", 10, true).Decrement("xxx", 5, true).Specs;

            var op = new MultiMutation<byte[]>
            {
                Builder = new MutateInBuilder<byte[]>(null, null, "thekey", specs),
                Transcoder = new JsonTranscoder()
            };
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));

            var result = new MutateInResult(op.GetCommandValues(), 0, MutationToken.Empty, new DefaultSerializer());

            Assert.Throws<InvalidIndexException>(() => result.ContentAs<string>(index));
        }
    }
}
