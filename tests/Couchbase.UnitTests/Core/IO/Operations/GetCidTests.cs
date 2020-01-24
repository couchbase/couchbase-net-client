using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Operations.Collections;
using Xunit;
using Couchbase.Utils;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class GetCidTests
    {
        [Fact]
        public void Test_GetWithValue()
        {
            var packet = new byte[]
            {
                129, 187, 0, 0, 12, 0, 0, 0, 0, 0, 0, 12, 0, 0, 0, 45, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 85, 0, 0, 0, 34
            };

            var response = MemoryPool<byte>.Shared.RentAndSlice(packet.Length);
            packet.AsMemory(0, packet.Length).CopyTo(response.Memory);
            var op = new GetCid();
            op.ReadAsync(response);

            var result = op.GetResultWithValue();

            Assert.True(result.Content.HasValue);
            Assert.Equal(85u, result.Content.Value);
        }
    }
}
