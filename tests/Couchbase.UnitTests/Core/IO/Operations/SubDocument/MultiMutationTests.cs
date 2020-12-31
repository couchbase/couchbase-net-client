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

namespace Couchbase.UnitTests.Core.IO.Operations.SubDocument
{
    public class MultiMutationTests
    {
        [Fact]
        public async Task WriteBody_Repeats_Safely()
        {
            var bytes = new byte[]
            {
                            129, 209, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 0, 0, 0, 16, 21, 240,
                            165, 234, 46, 107, 0, 0, 3, 0, 0, 0, 0, 0, 2, 49, 48, 4, 0, 0,
                            0, 0, 0, 2, 45, 53
            };

            var builder = new MutateInSpecBuilder();
            for (int i = 0; i < 10; i++)
            {
                builder.Upsert("upsert_" + i, i);
            }

            var op = new MultiMutation<byte[]>
            {
                Builder = new MutateInBuilder<byte[]>(null, null, "thekey", builder.Specs),
                Transcoder = new JsonTranscoder()
            };
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());

            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));
            Assert.Equal(10, op.GetCommandValues().Count);
            op.Reset();
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes));
            Assert.Equal(10, op.GetCommandValues().Count);

            var result = new MutateInResult(op.GetCommandValues(), 0, MutationToken.Empty, new DefaultSerializer());


        }
    }
}
