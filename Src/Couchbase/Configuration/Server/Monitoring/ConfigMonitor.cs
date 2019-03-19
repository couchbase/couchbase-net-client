using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Annotations;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Logging;
using Couchbase.Tracing;
using Couchbase.Utils;

namespace Couchbase.Configuration.Server.Monitoring
{
    internal class ConfigMonitor : IDisposable
    {
        private readonly ILog _log = LogManager.GetLogger<ConfigMonitor>();
        private readonly CancellationTokenSource _cts;

        public IClusterController ClusterController { get; private set; }
        public ClientConfiguration Configuration { get; private set; }

        public ConfigMonitor([NotNull] IClusterController clusterController)
            : this(clusterController, new CancellationTokenSource())
        {
        }

        public ConfigMonitor([NotNull] IClusterController clusterController, CancellationTokenSource cts)
        {
            _cts = cts;
            ClusterController = clusterController;
            Configuration = ClusterController.Configuration;
        }

        public void StartMonitoring()
        {
            Task.Run(async () =>
            {
                Thread.CurrentThread.Name = "CM";
                var index = 0;
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        _log.Debug("Waiting to check configs...");

                        // Test at every interval.  Wait before first test.
                        await Task.Delay(TimeSpan.FromMilliseconds(Configuration.ConfigPollInterval), _cts.Token).
                            ContinueOnAnyContext();

                        _log.Debug("Checking configs...");

                        var now = DateTime.Now;
                        var lastCheckedPlus = ClusterController.
                            LastConfigCheckedTime.
                            AddMilliseconds(Configuration.ConfigPollCheckFloor);

                        if (lastCheckedPlus > now)
                        {
                            _log.Debug("By-passing config checks because {0} > {1}", lastCheckedPlus, now);
                            continue;
                        }

                        foreach (var provider in ClusterController.ConfigProviders.OfType<CarrierPublicationProvider>())
                        {
                            var contexts = provider.ConfigContexts;
                            foreach (var ctx in contexts)
                            {
                                var servers = ctx.Servers.Where(x => x.IsDataNode && !x.IsDown).ToList();
                                // ReSharper disable once PossibleMultipleEnumeration
                                if (!servers.Any())
                                {
                                    _log.Debug("No servers with Data service available for bucket {0}", ctx.BucketName);
                                    continue;
                                }

                                index = (index + 1) % servers.Count;
                                var server = servers[index];

                                _log.Debug("Using index {0} - server {1}", index, server);

                                var operation = new Config(
                                    ClusterController.Transcoder,
                                    Configuration.DefaultOperationLifespan,
                                    server.EndPoint);

                                IOperationResult<BucketConfig> configResult;
                                using (Configuration.Tracer.StartParentScope(operation, addIgnoreTag: true, ignoreActiveSpan: true))
                                {
                                    configResult = server.Send(operation);
                                }

                                if (configResult.Success && configResult.Status == ResponseStatus.Success)
                                {
                                    var config = configResult.Value;
                                    if (config != null)
                                    {
                                        _log.Debug("Checking config with revision #{0}", config.Rev);
                                        ClusterController.EnqueueConfigForProcessing(config);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        /*ignore*/
                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.Info("Unhandled error in ConfigMonitor, ignoring", ex);
                    }
                    finally
                    {
                        ClusterController.LastConfigCheckedTime = DateTime.Now;
                    }
                }
            }, _cts.Token).ContinueOnAnyContext();
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
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
