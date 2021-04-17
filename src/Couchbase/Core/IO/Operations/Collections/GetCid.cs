using System;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal class GetCid : OperationBase<string>
    {
        public override bool RequiresVBucketId => false;

        public override OpCode OpCode => OpCode.GetCidByName;

        public override string GetValue()
        {
            throw new NotImplementedException("Use GetValueAsUint() instead for GetCid result.");
        }

        public uint? GetValueAsUint()
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
        protected override void WriteExtras(OperationBuilder builder)
        {
        }
    }
}
