using System;
using System.Runtime.ExceptionServices;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    /*
     *   Byte/     0       |       1       |       2       |       3       |
     /              |               |               |               |
    |0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|
    +---------------+---------------+---------------+---------------+
   0| 0x80          | 0xb5          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
   4| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
   8| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  12| 0xde          | 0xad          | 0xbe          | 0xef          |
    +---------------+---------------+---------------+---------------+
  16| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  20| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  24| 0x42          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  28| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  32| 0x00          | 0x00          | 0x00          | 0x00          |
    +---------------+---------------+---------------+---------------+
  36| 0x08          | 0x07          | 0x06          | 0x05          |
    +---------------+---------------+---------------+---------------+
  40| 0x04          | 0x03          | 0x02          | 0x01          |
    +---------------+---------------+---------------+---------------+
    GET_CLUSTER_CONFIG command
    Field        (offset) (value)
    Magic        (0)    : 0x80 (client request, SDK -> kv_engine)
    Opcode       (1)    : 0xb5
    Key length   (2,3)  : 0x0000
    Extra length (4)    : 0x10 (16 bytes, two int64_t fields in extras)
    Data type    (5)    : 0x00 (RAW)
    Vbucket      (6,7)  : 0x0000
    Total body   (8-11) : 0x00000010 (16 bytes)
    Opaque       (12-15): 0xdeadbeef
    CAS          (16-23): 0x0000000000000000
    Epoch        (24-31): 0x0000000000000042 (66 in base-10)
    Revision     (32-39): 0x0102030405060708 (72623859790382856 in base-10)
    */

    internal sealed class Config : OperationBase<BucketConfig>
    {
        internal HostEndpointWithPort EndPoint { get; set; }

        internal ulong? Epoch { get; set; }

        internal ulong? Revision { get; set; }

        protected override void BeginSend()
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.Object
            };
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            //We have GetClusterConfigWithKnownVersion enabled and the epoch
            //and revision can be sent to the server for deduping
            if (Epoch.HasValue && Revision.HasValue)
            {
                Span<byte> extras = stackalloc byte[16];
                ByteConverter.FromUInt64(Epoch.Value, extras.Slice(0));
                ByteConverter.FromUInt64(Revision.Value, extras.Slice(8));
                builder.Write(extras);
            }
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.Object
            };

            TryReadServerDuration(buffer);
        }

        public override OpCode OpCode => OpCode.GetClusterConfig;

        public override BucketConfig GetValue()
        {
            BucketConfig bucketConfig = null;
            if (GetSuccess() && Data.Length > 0)
            {
                try
                {
                    var buffer = Data;
                    ReadExtras(buffer.Span);

                    var offset = Header.BodyOffset;
                    var length = Header.TotalLength - Header.BodyOffset;

                    if ((Header.DataType & DataType.Snappy) != DataType.None)
                    {
                        using var decompressed = OperationCompressor.Decompress(buffer.Slice(offset, length), Span);
                        bucketConfig = Transcoder.Decode<BucketConfig>(decompressed.Memory, Flags, OpCode);
                    }
                    else
                    {
                        bucketConfig = Transcoder.Decode<BucketConfig>(buffer.Slice(offset, length), Flags, OpCode);
                    }
                }
                catch (Exception e)
                {
                    Exception = ExceptionDispatchInfo.Capture(e);
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return bucketConfig;
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion [ License information ]
