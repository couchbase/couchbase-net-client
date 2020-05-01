using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Couchbase.Configuration.Client;
using Couchbase.Logging;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Http;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    internal delegate void ConfigChanged(IBucketConfig streamingHttp);

    internal delegate void ErrorOccurred(IBucketConfig streamingHttp);

    /// <summary>
    /// Represents a long-lived comet style connection to an HTTP service.
    /// </summary>
    internal sealed class ConfigThreadState
    {
        private static readonly ILog Log = LogManager.GetLogger<ConfigThreadState>();
        private readonly BucketConfig _bucketConfig;
        private readonly ConfigChanged _configChangedDelegate;
        private readonly ErrorOccurred _errorOccurredDelegate;
        private CancellationToken _cancellationToken;
        private ClientConfiguration _clientConfiguration;

        public ConfigThreadState(BucketConfig bucketConfig, ConfigChanged configChangedDelegate,
            ErrorOccurred errorOccurredDelegate, CancellationToken cancellationToken, ClientConfiguration clientConfiguration)
        {
            _bucketConfig = bucketConfig;
            _configChangedDelegate += configChangedDelegate;
            _errorOccurredDelegate += errorOccurredDelegate;
            _cancellationToken = cancellationToken;
            _clientConfiguration = clientConfiguration;
        }

        /// <summary>
        ///     This is to support $HOST variable in the URI in _some_ cases
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private string GetSurrogateHost(Uri uri)
        {
            return uri.Host;
        }

        /// <summary>
        /// Starts the streaming connection to couchbase server that will
        /// listen for configuration changes and then update the client as needed.
        /// </summary>
        /// <remarks>
        /// Should not be used when a <see cref="SynchronizationContext" /> is present on the thread, as this
        /// could cause deadlocks.  This method is currently only used from within a dedicated thread,
        /// created by <see cref="HttpStreamingProvider.RegisterObserver"/>, so it is safe because there will not
        /// be a SynchronizationContext present on the thread.
        /// </remarks>
        public void ListenForConfigChanges()
        {
            var count = 0;

            //Make a copy of the nodes and shuffle them for randomness
            var nodes = _bucketConfig.Nodes.ToList();

            //if RBAC is being used with >= CB 5.0, then use the username otherwise use the bucket name
            var bucketNameOrUserName = string.IsNullOrWhiteSpace(_bucketConfig.Username)
                ? _bucketConfig.Name
                : _bucketConfig.Username;

            using (var httpClient = new CouchbaseHttpClient(bucketNameOrUserName, _bucketConfig.Password, _clientConfiguration))
            {
                httpClient.Timeout = Timeout.InfiniteTimeSpan;

                //This will keep trying until it runs out of servers to try in the cluster
                while (nodes.ToList().Any())
                {
                    try
                    {
                        //If the main thread has canceled, break out of the loop otherwise
                        //the next node in the server list will be tried; but in this case
                        //we want to shut things down and terminate the thread
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        nodes = nodes.Shuffle();
                        var node = nodes[0];
                        nodes.Remove(node);

                        var streamingUri = _bucketConfig.GetTerseStreamingUri(node, _bucketConfig.UseSsl);
                        Log.Info("Listening to {0}", streamingUri);

                        var response =
                            httpClient.GetAsync(streamingUri, HttpCompletionOption.ResponseHeadersRead,
                                _cancellationToken)
                                .Result;

                        response.EnsureSuccessStatusCode();

                        using (var stream = response.Content.ReadAsStreamAsync().Result)
                        {
                            //this will cancel the infinite wait below
                            _cancellationToken.Register(stream.Dispose);

                            if (stream.CanTimeout)
                            {
                                stream.ReadTimeout = Timeout.Infinite;
                            }

                            using (var reader = new StreamReader(stream, Encoding.UTF8, false))
                            {
                                string config;
                                while (!_cancellationToken.IsCancellationRequested &&
                                       ((config = reader.ReadLineAsync().Result) != null))
                                {
                                    if (config != String.Empty)
                                    {
                                        Log.Info("configuration changed count: {0}", count++);
                                        Log.Info("Worker Thread: {0}", Thread.CurrentThread.ManagedThreadId);
                                        var config1 = config;
                                        Log.Debug("{0}", config1);

                                        config = config.Replace("$HOST", streamingUri.Host);
                                        var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(config);
                                        bucketConfig.SurrogateHost = GetSurrogateHost(streamingUri);
                                        if (_configChangedDelegate != null)
                                        {
                                            bucketConfig.Password = _bucketConfig.Password;
                                            bucketConfig.Username = _bucketConfig.Username;
                                            _configChangedDelegate(bucketConfig);
                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch (AggregateException e)
                    {
                        var exceptions = e.Flatten().InnerExceptions;
                        if (exceptions.OfType<ObjectDisposedException>().Any())
                        {
                            Log.Info("The config listener has shut down.");
                        }
                        foreach (var ex in exceptions.Where(x => x.GetType() != typeof(ObjectDisposedException)))
                        {
                            Log.Error(ex);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            //We tried all nodes in the current configuration, alert the provider that we
            //need to try to re-bootstrap from the beginning
            if (nodes.Count == 0)
            {
                _errorOccurredDelegate(_bucketConfig);
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
