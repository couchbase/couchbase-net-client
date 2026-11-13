using System;
using System.Collections.Generic;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server;

/// <summary>
/// Keeps the config of a Couchbase or Ephemeral bucket up to date from the bucket nodes.
/// </summary>
internal sealed class BucketConfigRefresher : ConfigRefresher
{
    /// <summary>
    /// The longest poll interval of a bucket with a forward vbucket map. The map changes often during a rebalance,
    /// so the Java SDK polls such a bucket every second.
    /// </summary>
    internal static readonly TimeSpan RebalancePollInterval = TimeSpan.FromSeconds(1);

    private readonly BucketBase _bucket;

    public BucketConfigRefresher(BucketBase bucket, IConfigHandler configHandler, ILogger logger, Redactor redactor,
        TimeProvider timeProvider)
        : base(bucket.Context, configHandler, logger, redactor, timeProvider)
    {
        _bucket = bucket;
    }

    protected override string Name => _bucket.Name;

    protected override IEnumerable<IClusterNode> Nodes => _bucket.Nodes;

    protected override ConfigVersion? CurrentVersion => _bucket.CurrentConfig?.ConfigVersion;

    protected override TimeSpan PollInterval
    {
        get
        {
            var interval = base.PollInterval;
            var rebalancing = _bucket.CurrentConfig?.VBucketServerMap?.VBucketMapForward is { Length: > 0 };
            return rebalancing && interval > RebalancePollInterval ? RebalancePollInterval : interval;
        }
    }

    protected override void StartHttpStreaming() => ConfigHandler.StartHttpStreaming(Name);
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
