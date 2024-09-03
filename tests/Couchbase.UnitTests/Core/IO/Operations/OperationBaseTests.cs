using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.UnitTests.Helpers;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class OperationBaseTests
    {
        [Fact]
        public async Task StopOperation_StopsStopwatch()
        {
            // Arrange

            var operation = new FakeOperation();

            // Act

            await Task.Delay(500);

            operation.StopRecording();
            var elapsed = operation.Elapsed;

            await Task.Delay(500);

            // Assert

            Assert.Equal(elapsed, operation.Elapsed);
        }

        [Fact]
        public async Task StopOperation_CalledTwice_KeepsOriginalTime()
        {
            // Arrange

            var operation = new FakeOperation();

            // Act

            await Task.Delay(500);

            operation.StopRecording();
            var elapsed = operation.Elapsed;

            await Task.Delay(500);

            operation.StopRecording();

            // Assert

            Assert.Equal(elapsed, operation.Elapsed);
        }

        [Fact]
        public void OperationBase_ReadMutationTokenDoesNotThrowOnShortExtras()
        {
            // NCBC-3852
            var responseBytes = new byte[47];
            var fakeOp = new FakeMutationOperation()
            {
                VBucketId = 0x1,
                Header = new OperationHeader()
                {
                    ExtrasLength = 4,
                    FramingExtrasLength = 3,
                    BodyLength = 23,
                }
            };

            fakeOp.ForceTryReadMutationToken(responseBytes.AsSpan());
            Assert.Null(fakeOp.MutationToken);
        }

        private class FakeMutationOperation : MutationOperationBase
        {
            public override OpCode OpCode => OpCode.Delete;

            public void ForceTryReadMutationToken(ReadOnlySpan<byte> buffer)
            {
                TryReadMutationToken(buffer);
            }
        }

        private class FakeOperation : OperationBase
        {
            public override OpCode OpCode => OpCode.Get;
        }
    }
}
