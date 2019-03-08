using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Couchbase.UnitTests.Utils
{
    internal static class ConfigUtil
    {
        public static readonly Configuration ClientConfig;
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

                var resources = assembly.GetManifestResourceNames()
                    .Where(p => p.StartsWith(ConfigurationResourcePrefix));
                foreach (var resourceName in resources)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        Debug.Assert(stream != null, "stream != null");

                        var newFilePath = Path.Combine(configurationPath,
                            resourceName.Substring(ConfigurationResourcePrefix.Length));
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
