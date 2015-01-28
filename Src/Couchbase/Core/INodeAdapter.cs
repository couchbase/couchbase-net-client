using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core
{
    internal interface INodeAdapter
    {
        string Hostname { get; set; }

        string CouchbaseApiBase { get; set; }

        int MgmtApi { get; set; }

        int MgmtApiSsl { get; set; }

        int Views { get; set; }

        int ViewsSsl{ get; set; }

        int Moxi { get; set; }

        int KeyValue { get; set; }

        int KeyValueSsl { get; set; }
    }
}
