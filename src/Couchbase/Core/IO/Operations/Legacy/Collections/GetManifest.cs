using System;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Core.IO.Operations.Legacy.Collections
{
    internal class GetManifest :  OperationBase<Manifest>
    {
        public override OpCode OpCode  => OpCode.GetCollectionsManifest;

        public override byte[] CreateExtras()
        {
            Format = DataFormat.Json;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.Object
            };
            return Array.Empty<byte>();
        }

        public override void ReadExtras(byte[] buffer)
        {
            //force it to treat the result as JSON for serialization
            Format = DataFormat.Json;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.Object
            };
        }
    }
}
