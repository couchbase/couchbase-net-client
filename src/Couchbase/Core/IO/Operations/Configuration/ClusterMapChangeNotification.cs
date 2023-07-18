using System;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.Configuration;

internal sealed class ClusterMapChangeNotification : OperationBase<BucketConfig>
{
    public override OpCode OpCode => OpCode.ClusterMapChangeNotification;

    protected override void ReadExtras(ReadOnlySpan<byte> buffer)
    {
        Epoch =  ByteConverter.ToUInt64(buffer.Slice(Header.ExtrasOffset, 8));
        Revision = ByteConverter.ToUInt64(buffer.Slice(Header.ExtrasOffset + 8, 8));
    }

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
                bucketConfig = Transcoder.Decode<BucketConfig>(buffer.Slice(offset, length), Flags, OpCode);
            }
            catch (Exception e)
            {
                Exception = e;
                HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
        }

        return bucketConfig;
    }

    public bool HasExtras => Header.DataType == DataType.None;

    private ulong Revision { get; set; }

    private ulong Epoch { get; set; }

    public ConfigVersion GetConfigVersion => new(Epoch, Revision);
}
