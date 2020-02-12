using System;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class ExistsResult : IExistsResult
    {
        public bool Exists { get; set; }

        public ulong Cas { get; set; }
    }
}
