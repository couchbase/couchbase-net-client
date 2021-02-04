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
    public class CollectionOperationTests
    {
        [Fact]
        public void Test_GetCid_WithValue()
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

            var result = op.GetValue();

            Assert.True(result.HasValue);
            Assert.Equal(0x17u, result.Value);
        }

        [Fact]
        public void Test_GetSid_WithValue()
        {
            /*
            Magic(0)            : 0x81
            Opcode(1)           : 0xBB
            Frame ext(2)        : 0x00
            Key len(3)          : 0x00
            Ext len(4)          : 0x0C
            Data type(5)        : 0x00
            Status(6, 7)        : 0x0000
            Total body(8 - 11)  : 0x0000000C
            Opaque(12 - 15)     : 0x00001210
            CAS(16 - 23)        : 0x0000000000000000
            manifest(24 - 31)   : 0x0000000000000005
            Cid(32 - 35)        : 0x00000031
            */
            var packet = new byte[]
            {
                0x81, 0xBB, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x12, 0x10,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 00, 0x05, 0x00,
                0x00, 0x00, 0x31
            };

            var response = MemoryPool<byte>.Shared.RentAndSlice(packet.Length);
            packet.AsMemory(0, packet.Length).CopyTo(response.Memory);
            var op = new GetSid
            {
                Transcoder = new LegacyTranscoder()
            };
            op.Read(response);

            var result = op.GetValue();

            Assert.True(result.HasValue);
            Assert.Equal(0x31u, result.Value);
        }
    }
}
