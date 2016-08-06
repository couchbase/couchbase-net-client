using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public static readonly IServerConfig ServerConfig;
        public const string ConfigurationResourcePrefix = "Couchbase.Tests.Data.Configuration.";
        public const string RelativeConfigurationPath = @"Data\Configuration\";

        private static bool _configExtracted;

        static ConfigUtil()
        {
            EnsureConfigExtracted();

            ServerConfig = new FileSystemConfig(Path.Combine(RelativeConfigurationPath, "bootstrap.json"));
            ServerConfig.Initialize();

            ClientConfig = new FakeClientConfig();
        }

        public static void EnsureConfigExtracted()
        {
            if (!_configExtracted)
            {
                var assembly = Assembly.GetExecutingAssembly();

                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);
                GlobalSetup.TearDownSteps.Add(() => Directory.Delete(tempPath, true));

                var configurationPath = Path.Combine(tempPath, RelativeConfigurationPath);
                Directory.CreateDirectory(configurationPath);

                var resources = assembly.GetManifestResourceNames().Where(p => p.StartsWith(ConfigurationResourcePrefix));
                foreach (var resourceName in resources)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        Debug.Assert(stream != null, "stream != null");

                        var newFilePath = Path.Combine(configurationPath, resourceName.Substring(ConfigurationResourcePrefix.Length));
                        using (var destStream = File.Create(newFilePath))
                        {
                            stream.CopyTo(destStream);
                        }
                    }
                }

                Directory.SetCurrentDirectory(tempPath);

                _configExtracted = true;
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion