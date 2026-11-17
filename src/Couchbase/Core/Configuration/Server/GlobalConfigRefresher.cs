using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server;

/// <summary>
/// Keeps the cluster config up to date from the KV nodes that no bucket owns. Once buckets own every KV node, the
/// bucket refreshers fetch the configs.
/// </summary>
internal sealed class GlobalConfigRefresher : ConfigRefresher
{
    public GlobalConfigRefresher(ClusterContext context, IConfigHandler configHandler, ILogger logger,
        Redactor redactor, TimeProvider timeProvider)
        : base(context, configHandler, logger, redactor, timeProvider)
    {
    }

    protected override string Name => BucketConfig.GlobalBucketName;

    protected override IEnumerable<IClusterNode> Nodes => Context.Nodes.Where(node => node.Owner is null);

    protected override ConfigVersion? CurrentVersion => Context.GlobalConfig?.ConfigVersion;

    protected override void Publish(BucketConfig config)
    {
        // A node with no bucket selected returns the cluster config with no name.
        config.Name ??= BucketConfig.GlobalBucketName;
        base.Publish(config);
    }

    // A node that does not serve the cluster config means a cluster of mixed versions.
    protected override bool AnyKeyNotFoundSwitchesToHttpStreaming => true;

    // With no config over KV, the cluster and every bucket switch to HTTP streaming.
    protected override void StartHttpStreaming() => ConfigHandler.StartHttpStreaming();
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
