using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Configuration.Server
{
    internal interface IServerConfig : IDisposable
    {
        Pools Pools { get; }

        List<BucketConfig> Buckets { get; }

        Bootstrap Bootstrap { get; }

        List<BucketConfig> StreamingHttp { get; set; }

        Uri BootstrapServer { get; }

        void Initialize();
    }
}
