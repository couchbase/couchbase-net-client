using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
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

        private class FakeOperation : OperationBase
        {
            public override OpCode OpCode => OpCode.Get;
        }
    }
}
