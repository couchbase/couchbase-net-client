using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase
{
    public class Document<T> : IDocument<T>
    {
        public string Id { get; set; }

        public ulong Cas { get; set; }

        public uint Expiry { get; set; }

        public T Value { get; set; }
    }
}
