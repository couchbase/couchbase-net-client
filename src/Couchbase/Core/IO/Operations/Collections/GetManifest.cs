using System;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal class GetManifest :  OperationBase<Manifest>
    {
        public override OpCode OpCode  => OpCode.GetCollectionsManifest;

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
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            //force it to treat the result as JSON for serialization
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.Object
            };
        }
    }
}
