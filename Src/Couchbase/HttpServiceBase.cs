using System;
using System.Net.Http;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase
{
    /// <summary>
    /// Base class for HTTP services to inherit from to provide consistent access to configuration,
    /// http client and data mapper.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal abstract class HttpServiceBase : IDisposable
    {
        private const string ConnectionIdHeaderName = "cb-client-id";

        /// <summary>
        /// Gets the client configuration.
        /// </summary>
        protected ClientConfiguration ClientConfiguration { get; set; }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        protected HttpClient HttpClient { get; set; }

        /// <summary>
        /// The <see cref="IDataMapper"/> to use for mapping the output stream to a Type.
        /// </summary>
        protected IDataMapper DataMapper { get; set; }

        /// <summary>
        /// Gets or sets the last activity.
        /// </summary>
        public DateTime? LastActivity { get; private set; }

        /// <summary>
        /// Gets the connection identifier for this HTTP service instance.
        /// </summary>
        public ulong ConnectionId { get; } = SequenceGenerator.GetRandomLong();

        /// <summary>
        /// The configuration context for this instance.
        /// </summary>
        protected ConfigContextBase Context { get; set; }

        protected HttpServiceBase(HttpClient httpClient, IDataMapper dataMapper, ConfigContextBase context)
        {
            HttpClient = httpClient;
            DataMapper = dataMapper;
            Context = context;
            ClientConfiguration = context.ClientConfig;

            // set custom header for client / connection ID
            httpClient.DefaultRequestHeaders.Add(ConnectionIdHeaderName, ClientIdentifier.FormatConnectionString(ConnectionId));
        }

        public void Dispose()
        {
            if (HttpClient != null)
            {
                HttpClient.Dispose();
            }
        }

        protected void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
