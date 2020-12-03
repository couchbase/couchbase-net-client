using System;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal class GetCid : OperationBase<uint?>
    {
        public override OpCode OpCode => OpCode.GetCidByName;

        public override bool Idempotent { get; } = true;

        public override uint? GetValue()
        {
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data;
                    ReadExtras(buffer.Span);
                    return Converters.ByteConverter.ToUInt32(buffer.Span.Slice(Header.ExtrasOffset + 8, 4));
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return 0u;
        }

        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override void WriteBody(OperationBuilder builder)
        {
        }
    }
}
