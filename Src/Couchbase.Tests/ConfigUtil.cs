using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Providers.FileSystem;
using Couchbase.Tests.Configuration.Client;

namespace Couchbase.Tests
{
    internal static class ConfigUtil
    {
        public static readonly ClientConfiguration ClientConfig;
        public static readonly IServerConfig ServerConfig ;
        public const string BootstrapPath = @"Data\\Configuration\\bootstrap.json";

        static ConfigUtil()
        {
            ServerConfig = new FileSystemConfig(BootstrapPath);
            ServerConfig.Initialize();

            ClientConfig = new FakeClientConfig();
        }
    }
}
