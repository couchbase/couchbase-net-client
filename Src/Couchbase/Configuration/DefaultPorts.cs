using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration
{
    public enum DefaultPorts
    {
        MgmtApi = 8091,
        RestApi = 8092,
        Direct = 11210,
        Proxy = 11211,
        SslDirect = 11207,
        HttpsCApi = 18092,
        HttpsMgmt = 18091
    }
}
