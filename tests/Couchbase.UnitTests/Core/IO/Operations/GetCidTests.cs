using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Transcoders;
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
                0x18, 0xbb, 0x03, 0x00, 0x0c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x1a,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x17
            };

            var response = MemoryPool<byte>.Shared.RentAndSlice(packet.Length);
            packet.AsMemory(0, packet.Length).CopyTo(response.Memory);
            var op = new GetCid()
            {
                Transcoder = new LegacyTranscoder()
            };
            op.Read(response);

            var result = op.GetResultWithValue();

            Assert.True(result.Content.HasValue);
            Assert.Equal(0x17u, result.Content.Value);
        }
    }
}
