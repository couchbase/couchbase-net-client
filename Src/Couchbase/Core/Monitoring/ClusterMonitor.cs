using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Annotations;
using Couchbase.Configuration;
using Couchbase.IO.Http;
using Couchbase.Logging;

namespace Couchbase.Core.Monitoring
{
    /// <summary>
    /// Performs regular monitoring of down services so they can be reactivated
    /// when they become available.
    /// </summary>
    internal class ClusterMonitor : IDisposable
    {
        private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(5);

        private readonly ILog _log = LogManager.GetLogger<ClusterMonitor>();

        private readonly HttpClient _httpClient;
        private readonly ClusterController _clusterController;
        private readonly QueryUriTester _queryUriTester;
        private readonly SearchUriTester _searchUriTester;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ClusterMonitor([NotNull] ClusterController clusterController)
        {
            if (clusterController == null)
            {
                throw new ArgumentNullException("clusterController");
            }

            // Use Couchbase HTTP client even though we don't need authentication
            // So that we get our custom server certificate validation if SSL is being used
            _httpClient = new CouchbaseHttpClient("any-bucket", "")
            {
                Timeout = PingTimeout
            };

            _clusterController = clusterController;
            _queryUriTester = new QueryUriTester(_httpClient);
            _searchUriTester = new SearchUriTester(_httpClient);
        }

        public void StartMonitoring()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Test at every interval.  Wait before first test.
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(_clusterController.Configuration.NodeAvailableCheckInterval),
                            _cancellationTokenSource.Token);

                        // Create async tasks to test each down query node
                        var tests = ConfigContextBase.QueryUris
                            .Where(p => !p.IsHealthy(_clusterController.Configuration.QueryFailedThreshold))
                            .Select(p => _queryUriTester.TestUri(p, _cancellationTokenSource.Token));

                        // Create async tasks to test each down search node
                        tests = tests.Concat(
                            ConfigContextBase.SearchUris
                                .Where(p => !p.IsHealthy(ConfigContextBase.SearchNodeFailureThreshold))
                                .Select(p => _searchUriTester.TestUri(p, _cancellationTokenSource.Token)));

                        // Enumerate the collection to start the tests
                        // Wait for all tests to succeed or fail before looping again
                        var testList = tests.ToList();
                        if (testList.Any())
                        {
                            await Task.WhenAll(testList);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _log.Debug("Unhandled error in ClusterMonitor, ignoring", ex);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _httpClient.Dispose();
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
