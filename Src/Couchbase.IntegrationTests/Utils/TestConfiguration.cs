using System;
using System.Collections.Generic;
using System.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Microsoft.Extensions.Configuration;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// Provides the configurations defined in app.config.
    /// </summary>
    public static class TestConfiguration
    {
        private static IConfigurationRoot _jsonConfiguration;

        public static ClientConfiguration GetDefaultConfiguration()
        {
            return new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    BuildBootStrapUrl()
                }
            };
        }

        /// <summary>
        /// Gets the configuration for the "current" appSettings setting. The hostname and port will be pulled from the appsettings as well.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ConfigurationErrorsException">A configuration file could not be loaded.</exception>
        public static ClientConfiguration GetCurrentConfiguration()
        {
            return GetConfiguration(ConfigurationManager.AppSettings["current"]);
        }

        /// <summary>
        /// Gets the configuration for a config section. The hostname and port will be pulled from the appsettings.
        /// </summary>
        /// <param name="sectionName"></param>
        /// <returns></returns>
        /// <exception cref="ConfigurationErrorsException">A configuration file could not be loaded.</exception>
        public static ClientConfiguration GetConfiguration(string sectionName)
        {
            var configuration = new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/" + sectionName))
                {
                    Servers = new List<Uri>
                    {
                        BuildBootStrapUrl()
                    }
                };
            return configuration;
        }

        public static Uri BuildBootStrapUrl()
        {
            var hostname = ConfigurationManager.AppSettings["hostname"];
            var port = ConfigurationManager.AppSettings["bootport"];
            return new Uri(string.Format("http://{0}:{1}/", hostname, port));
        }

        public static ClientConfiguration GetJsonConfiguration(string name)
        {
            if (_jsonConfiguration == null)
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("config.json");
                _jsonConfiguration = builder.Build();
            }

            return new ClientConfiguration(_jsonConfiguration.Get<CouchbaseClientDefinition>(name));
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
