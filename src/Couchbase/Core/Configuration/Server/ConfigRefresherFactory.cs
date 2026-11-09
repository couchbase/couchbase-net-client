using System;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server;

internal class ConfigRefresherFactory : IConfigRefresherFactory
{
    private readonly IConfigHandler _configHandler;
    private readonly ILogger<BucketConfigRefresher> _bucketLogger;
    private readonly ILogger<GlobalConfigRefresher> _globalLogger;
    private readonly Redactor _redactor;
    private readonly TimeProvider _timeProvider;

    public ConfigRefresherFactory(IConfigHandler configHandler, ILogger<BucketConfigRefresher> bucketLogger,
        ILogger<GlobalConfigRefresher> globalLogger, Redactor redactor, TimeProvider timeProvider)
    {
        _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
        _bucketLogger = bucketLogger ?? throw new ArgumentNullException(nameof(bucketLogger));
        _globalLogger = globalLogger ?? throw new ArgumentNullException(nameof(globalLogger));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public BucketConfigRefresher CreateBucketRefresher(BucketBase bucket) =>
        new(bucket, _configHandler, _bucketLogger, _redactor, _timeProvider);

    public GlobalConfigRefresher CreateGlobalRefresher(ClusterContext context) =>
        new(context, _configHandler, _globalLogger, _redactor, _timeProvider);
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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
