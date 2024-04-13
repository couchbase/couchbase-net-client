using System;
using System.Runtime.ExceptionServices;
using Couchbase.Core.IO.Converters;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    internal sealed class GetMeta : OperationBase<MetaData>
    {
        public override OpCode OpCode => OpCode.GetMeta;

        protected override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[1];
            extras[0] = 0x02;
            builder.Write(extras);
        }

        public override MetaData GetValue()
        {
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span.Slice(Header.ExtrasOffset);
                    return new MetaData
                    {
                        Deleted = ByteConverter.ToUInt32(buffer.Slice(0, 4), true) > 0,
                        Flags = ByteConverter.ToUInt32(buffer.Slice(4, 4), true),
                        Expiry = ByteConverter.ToUInt32(buffer.Slice(8, 4), true),
                        SeqNo = ByteConverter.ToUInt64(buffer.Slice(12, 8), true),
                        DataType = (DataType) buffer[20]
                    };
                }
                catch (Exception e)
                {
                    Exception = ExceptionDispatchInfo.Capture(e);
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return new MetaData();
        }
    }

    internal sealed class MetaData
    {
        public bool Deleted { get; internal set; }

        public uint Flags { get; internal set; }

        public uint Expiry { get; internal set; }

        public ulong SeqNo { get; set; }

        public DataType DataType { get; internal set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
