using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase
{
    public interface IDocument<T>  
    {
        string Id { get; set; }

        ulong Cas { get; set; }

        uint Expiry { get; set; }

        T Value { get; set; }
    }
}
