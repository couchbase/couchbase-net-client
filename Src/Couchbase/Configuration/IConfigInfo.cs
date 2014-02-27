using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;

namespace Couchbase.Configuration
{
    internal interface IConfigInfo
    {
        DateTime CreationTime { get; }

        IKeyMapper GetKeyMapper(string bucketName);

        IServerConfig ServerConfig { get; }

        ClientConfiguration ClientConfig { get; }

        IBucketConfig BucketConfig { get; }
    }
}
