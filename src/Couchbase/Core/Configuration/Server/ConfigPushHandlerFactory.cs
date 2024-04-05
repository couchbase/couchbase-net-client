using System;
using System.Security.Authentication;
using System.Security.Cryptography;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server;

internal class ConfigPushHandlerFactory : IConfigPushHandlerFactory
{
    private readonly ILogger<ConfigPushHandler> _logger;
    private readonly TypedRedactor _redactor;

    public ConfigPushHandlerFactory(ILogger<ConfigPushHandler> logger, TypedRedactor redactor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    public ConfigPushHandler Create(CouchbaseBucket bucket, ClusterContext clusterContext)
    {
        return  new ConfigPushHandler(bucket, clusterContext, _logger, _redactor);
    }
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
