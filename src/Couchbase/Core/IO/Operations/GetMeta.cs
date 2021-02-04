using System;
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
                    Exception = e;
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
