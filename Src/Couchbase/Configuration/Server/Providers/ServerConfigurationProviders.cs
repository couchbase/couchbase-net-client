using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Providers
{
    [Flags]
    internal enum ServerConfigurationProviders
    {
        None = 0,
        CarrierPublication = 1,
        HttpStreaming = 2
    }
}
