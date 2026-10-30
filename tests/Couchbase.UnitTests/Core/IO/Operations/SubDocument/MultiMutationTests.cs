using System;
using System.Collections.Generic;
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

            var op = new MultiMutation<byte[]>("thekey", builder.Specs)
            {
                Transcoder = new JsonTranscoder()
            };
            op.OperationBuilderPool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());

            await op.SendAsync(Mock.Of<IConnection>(c => c.ServerFeatures == ServerFeatureSet.Empty));
            op.Read(new FakeMemoryOwner<byte>(bytes));

            using (op.ParseCommandValues())
            {
                Assert.Equal(10, op.MutateCommands.Count);
            }

            op.Reset();
            await op.SendAsync(Mock.Of<IConnection>(c => c.ServerFeatures == ServerFeatureSet.Empty));
            op.Read(new FakeMemoryOwner<byte>(bytes));
            Assert.Equal(10, op.MutateCommands.Count);

#pragma warning disable CS0618
            var result = new MutateInResult(op);
#pragma warning restore CS0618
        }

        [Fact]
        public async Task SendAsync_FirstSpecEndsInLastEightBytesOfBuffer_DoesNotThrow()
        {
            // NCBC-4376: BeginOperationSpec must grow the buffer before writing the spec header
            var failedLengths = new List<int>();

            for (var length = 16_200; length < 16_400; length++)
            {
                var builder = new MutateInSpecBuilder();
                builder.Upsert("a", new string('x', length));
                builder.Upsert("b", 1);

                var op = new MultiMutation<byte[]>("thekey", builder.Specs)
                {
                    Transcoder = new JsonTranscoder()
                };
                // A fresh pool each time, so every send starts from an unexpanded buffer. The provider's pool
                // disposes the builders it holds, which returns their buffers to ArrayPool<byte>.Shared.
                var pool = new DefaultObjectPoolProvider().Create(new OperationBuilderPoolPolicy());
                op.OperationBuilderPool = pool;

                try
                {
                    await op.SendAsync(Mock.Of<IConnection>(c => c.ServerFeatures == ServerFeatureSet.Empty));
                }
                catch (InvalidOperationException)
                {
                    failedLengths.Add(length);
                }
                finally
                {
                    ((IDisposable)pool).Dispose();
                }
            }

            Assert.Empty(failedLengths);
        }
    }
}
