using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server;

public class BucketConfigExtensionTests
{
    [Fact]
    public void Test_HasVBucketChanged_OldConfig_IsNull()
    {
        var newConfig = new BucketConfig();
        Assert.True(newConfig.HasVBucketMapChanged(null));
    }

    [Fact]
    public void Test_HasVBucketChanged_VBucketHasChanged()
    {
        var oldConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{1,0}, new short[]{0,1}},
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{0,1}},
                ServerList = new []{"localhost1", "localhost2"}
            }
        };

        var newConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{0,0}, new short[]{0,1}},//changed
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{0,1}},
                ServerList = new []{"localhost1", "localhost2"}
            }
        };
        Assert.True(newConfig.HasVBucketMapChanged(oldConfig));
    }

    [Fact]
    public void Test_HasVBucketChanged_VBucketHasNotChanged()
    {
        var oldConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{0,0}, new short[]{0,1}},
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{0,1}},
                ServerList = new []{"localhost1", "localhost2"}
            }
        };

        var newConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{0,0}, new short[]{0,1}},
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{0,1}},
                ServerList = new []{"localhost1", "localhost2"}
            }
        };
        Assert.False(newConfig.HasVBucketMapChanged(oldConfig));
    }

    [Fact]
    public void Test_HasVBucketChanged_VBucketMapForward_HasChanged()
    {
        var oldConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{0,0}, new short[]{0,1}},
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{0,1}},
                ServerList = new []{"localhost1", "localhost2"}
            }
        };

        var newConfig = new BucketConfig
        {
            VBucketServerMap = new VBucketServerMapDto
            {
                VBucketMap = new[] { new short[]{0,0}, new short[]{0,1}},
                VBucketMapForward = new[] { new short[]{1,0}, new short[]{1,0}},//changed
                ServerList = new []{"localhost1", "localhost2"}
            }
        };
        Assert.True(newConfig.HasVBucketMapChanged(oldConfig));
    }

    [Fact]
    public void Test_HasClusterNodesChanged_True()
    {
        var oldConfig = new BucketConfig
        {
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            }
        };

        var newConfig = new BucketConfig
        {
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "localhost2"
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "localhost2"
                }
            }
        };

        Assert.True(newConfig.HasClusterNodesChanged(oldConfig));
    }

    [Fact]
    public void Test_HasClusterNodesChanged_False()
    {
        var oldConfig = new BucketConfig
        {
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            }
        };

        var newConfig = new BucketConfig
        {
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "localhost1"
                }
            }
        };

        Assert.False(newConfig.HasClusterNodesChanged(oldConfig));
    }

    [Fact]
    public void Test_HasClusterNodesChanged_OldConfig_IsNull()
    {
        var newConfig = new BucketConfig();
        Assert.True(newConfig.HasClusterNodesChanged(null));
    }

    [Fact]
    public void Test_HasConfigChanges_When_Configs_Are_Same_Return_False()
    {
        var oldConfig = new BucketConfig
        {
            Name = "default",
            Rev = 806
        };

        var newConfig = new BucketConfig
        {
            Name = "default",
            Rev = 806
        };

        Assert.False(newConfig.HasConfigChanges(oldConfig, "default"));
    }

    [Fact]
    public void Test_HasConfigChanges_When_Buckets_Not_Same_Return_False()
    {
        var oldConfig = new BucketConfig
        {
            Name = "default1",
            Rev = 806
        };

        var newConfig = new BucketConfig
        {
            Name = "default2",
            Rev = 806
        };

        Assert.False(newConfig.HasConfigChanges(oldConfig, "default"));
        Assert.False(new BucketConfig { Name = null }.HasConfigChanges(oldConfig, "default"));
    }

    [Fact]
    public void Test_HasConfigChanges_When_Revision_LessThan_Return_False()
    {
        var oldConfig = new BucketConfig
        {
            Name = "default1",
            Rev = 806
        };

        var newConfig = new BucketConfig
        {
            Name = "default2",
            Rev = 804
        };

        Assert.False(newConfig.HasConfigChanges(oldConfig, "default"));
    }

    [Fact]
    public void Test_HasConfigChanges_When_Revision_GreaterThan_Return_True()
    {
        var oldConfig = new BucketConfig
        {
            Name = "default1",
            Rev = 804
        };

        var newConfig = new BucketConfig
        {
            Name = "default2",
            Rev = 806
        };

        Assert.False(newConfig.HasConfigChanges(oldConfig, "default"));
    }

    [Fact]
    public void Test_HasConfigChanges_When_OldConfig_Null_Return_True()
    {
        BucketConfig oldConfig = null;

        var newConfig = new BucketConfig
        {
            Name = "default2",
            Rev = 806
        };

        Assert.False(newConfig.HasConfigChanges(oldConfig, "default"));
    }

    [Theory]
    [InlineData(806UL, false, false, false)]
    [InlineData(806UL, true, false, true)]
    [InlineData(806UL, true, true, false)]
    [InlineData(804UL, true, false, true)]
    public void Test_HasConfigChanges_When_Revision_Is_Same_Or_Older(ulong newRev, bool ignoreRev, bool sameMap, bool expected)
    {
        var oldConfig = new BucketConfig
        {
            Name = "default",
            Rev = 806,
            VBucketServerMap = new VBucketServerMapDto { ServerList = ["nodeA:11210"], VBucketMap = [[0]] }
        };

        var newConfig = new BucketConfig
        {
            Name = "default",
            Rev = newRev,
            IgnoreRev = ignoreRev,
            VBucketServerMap = new VBucketServerMapDto { ServerList = ["nodeA:11210"], VBucketMap = [[sameMap ? (short)0 : (short)-1]] }
        };

        Assert.Equal(expected, newConfig.HasConfigChanges(oldConfig, "default"));
    }

    [Theory]
    [InlineData(null, "config_higher_rev_no_epoch.json", true)]
    [InlineData("config_higher_rev_no_epoch.json", "config_lower_rev_no_epoch.json", false)]
    [InlineData("config_lower_rev_no_epoch.json", "config_higher_rev_no_epoch.json", true)]
    [InlineData("config_higher_rev_higher_epoch.json", "config_lower_rev_higher_epoch.json", false)]
    [InlineData("config_higher_rev_no_epoch.json", "config_higher_rev_higher_epoch.json", true)]
    [InlineData("config_lower_rev_lower_epoch.json", "config_higher_rev_higher_epoch.json", true)]
    [InlineData("config_lower_rev_higher_epoch.json", "config_higher_rev_lower_epoch.json", false)]
    public void Test_Compare_Config_Revisions_And_Epochs(string oldConfigResource, string newConfigResource, bool newConfigIsHigher)
    {
        var oldConfig = oldConfigResource == null
            ? null
            : ResourceHelper.ReadResource(oldConfigResource, InternalSerializationContext.Default.BucketConfig);
        var newConfig = ResourceHelper.ReadResource(newConfigResource, InternalSerializationContext.Default.BucketConfig);

        if (newConfigIsHigher)
        {
            Assert.True(newConfig.HasConfigChanges(oldConfig, "travel-sample"));
        }
        else
        {
            Assert.False(newConfig.HasConfigChanges(oldConfig, "travel-sample"));
        }
    }

    [Fact]
    public void Test_GlobalConfig_With_No_Nodes()
    {
       var bucketConfig = ResourceHelper.ReadResource("config-global-no-nodes.json", InternalSerializationContext.Default.BucketConfig);
       var nodeAdapters = bucketConfig.GetNodes();
    }

    [Fact]
    public async Task Test_HasConfigChanges()
    {
        #region Setup configs
        var configA = new BucketConfig
        {
            Rev = 1,
            Name = "default",
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "nodeA:8091",
                    CouchApiBase = "http://nodeA:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "nodeA",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                }
            },
            VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                ServerList = new []{"nodeA:11210"},
                VBucketMap = new[]
                {
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                    new short[]{0, -1},
                }
            }
        };
        configA.OnDeserialized();

        var configB = new BucketConfig
        {
            Rev = 2,
            Name = "default",
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "nodeA:8091",
                    CouchApiBase = "http://nodeA:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                },
                new()
                {
                    Hostname = "nodeB:8091",
                    CouchApiBase = "http://nodeB:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "nodeA",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                },
                new()
                {
                    Hostname = "nodeB",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                }
            },
            VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                ServerList = new []{"nodeA:11210", "nodeB:11210"},
                VBucketMap = new[]
                {
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                }
            }
        };
        configB.OnDeserialized();

        var configC = new BucketConfig
        {
            Rev = 3,
            Name = "default",
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "nodeA:8091",
                    CouchApiBase = "http://nodeA:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                },
                new()
                {
                    Hostname = "nodeB:8091",
                    CouchApiBase = "http://nodeB:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "nodeA",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                },
                new()
                {
                    Hostname = "nodeB",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                }
            },
            VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                ServerList = new []{"nodeA:11210", "nodeB:11210"},
                VBucketMap = new[]
                {
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                }
            }
        };
        configC.OnDeserialized();

        var configD = new BucketConfig
        {
            Rev = 4,
            Name = "someOtherBucket",
            Nodes = new List<Node>
            {
                new()
                {
                    Hostname = "nodeB:8091",
                    CouchApiBase = "http://nodeB:8092/default%2Ba4b0a6a479ce517c8f5a9d5637addc9f",
                    Ports = new Ports
                    {
                        Direct = 11210,
                        SslDirect = 11207
                    }
                }
            },
            NodesExt = new List<NodesExt>
            {
                new()
                {
                    Hostname = "nodeA",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                },
                new()
                {
                    Hostname = "nodeB",
                    Services = new Services
                    {
                        Kv =  11210,
                        KvSsl = 11207
                    }
                }
            },
            VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                ServerList = new []{"nodeA:11210", "nodeB:11210"},
                VBucketMap = new[]
                {
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{1, 0},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                    new short[]{0, 1},
                }
            }
        };
        configD.OnDeserialized();
        #endregion

        var bucket = CreateBucket(configA);
        Assert.Equal(configA, bucket.CurrentConfig);
        Assert.True(configA.HasVBucketMapChanged(null));
        Assert.True(configA.HasClusterNodesChanged(null));

        await bucket.ConfigUpdatedAsync(configB);
        Assert.Equal(configB, bucket.CurrentConfig);
        Assert.True(configB.HasVBucketMapChanged(configA));
        Assert.True(configB.HasClusterNodesChanged(configA));

        await bucket.ConfigUpdatedAsync(configC);
        Assert.Equal(configC, bucket.CurrentConfig);
        Assert.True(configC.HasVBucketMapChanged(configB));
        Assert.True(configC.HasVBucketMapChanged(configB));

        // should skip a config belonging to the wrong bucket
        await bucket.ConfigUpdatedAsync(configD);
        Assert.Equal(configC, bucket.CurrentConfig);
    }

    // Bounds each config update, so a regression fails instead of hanging.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ConfigUpdatedAsync_AfterApplyFailedOnce_AppliesSameRevWhenPublishedAgain()
    {
        var keyMapper = new Mock<IKeyMapper>().Object;
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        keyMapperFactory.SetupSequence(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException())
            .Returns(keyMapper);
        var current = CreateConfig(rev: 1, activeNode: 0);
        var bucket = CreateBucket(current, keyMapperFactory.Object);

        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);
        Assert.Same(current, bucket.CurrentConfig);

        var again = CreateConfig(rev: 2, activeNode: 1);
        await bucket.ConfigUpdatedAsync(again).WaitAsync(Deadline);

        Assert.Same(again, bucket.CurrentConfig);
        Assert.Same(keyMapper, bucket.KeyMapper);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_IgnoreRevWithSameRevAndNewMap_IsApplied()
    {
        var bucket = CreateBucket(CreateConfig(rev: 5, activeNode: 0));
        var incoming = CreateConfig(rev: 5, activeNode: 1);
        incoming.IgnoreRev = true;

        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_IgnoreRevWithOlderRev_IsApplied()
    {
        // A DNS SRV rebootstrap can move the bucket to a cluster with lower revisions.
        var bucket = CreateBucket(CreateConfig(rev: 5, activeNode: 0));
        var incoming = CreateConfig(rev: 3, activeNode: 1);
        incoming.IgnoreRev = true;

        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_SameRevWithoutIgnoreRev_IsDropped()
    {
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        var current = CreateConfig(rev: 5, activeNode: 0);
        var bucket = CreateBucket(current, keyMapperFactory.Object);

        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 5, activeNode: 1)).WaitAsync(Deadline);

        Assert.Same(current, bucket.CurrentConfig);
        keyMapperFactory.Verify(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(3ul)]
    [InlineData(2ul)]
    public async Task ConfigUpdatedAsync_WaitingConfigNoLongerNewer_IsDropped(ulong waitingRev)
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        keyMapperFactory.Setup(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                // Holds the first apply inside the config lock.
                entered.TrySetResult(true);
                release.Wait(Deadline);
                return new Mock<IKeyMapper>().Object;
            });
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), keyMapperFactory.Object);
        var applied = CreateConfig(rev: 3, activeNode: 1);

        var first = Task.Run(() => bucket.ConfigUpdatedAsync(applied));
        await entered.Task.WaitAsync(Deadline);

        // Newer than rev 1, so it passes the check before the lock and waits for the lock.
        var waiting = bucket.ConfigUpdatedAsync(CreateConfig(waitingRev, activeNode: 0));
        Assert.False(waiting.IsCompleted);

        release.Set();
        await Task.WhenAll(first, waiting).WaitAsync(Deadline);

        Assert.Same(applied, bucket.CurrentConfig);
        keyMapperFactory.Verify(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(3UL, false, false)]
    [InlineData(2UL, true, false)]
    [InlineData(3UL, false, true)]
    public async Task ConfigUpdatedAsync_EmptyMapWithNewNode_IsDropped(ulong rev, bool ignoreRev, bool nullMap)
    {
        var keyMapper = new Mock<IKeyMapper>().Object;
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        keyMapperFactory.Setup(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
            .Returns(keyMapper);
        var nodeFactory = new Mock<IClusterNodeFactory>();
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), keyMapperFactory.Object, nodeFactory);
        var current = CreateConfig(rev: 2, activeNode: 1);
        await bucket.ConfigUpdatedAsync(current).WaitAsync(Deadline);
        Assert.Same(keyMapper, bucket.KeyMapper);
        keyMapperFactory.Invocations.Clear();

        var incoming = CreateConfigWithEmptyMap(rev, nullMap);
        incoming.IgnoreRev = ignoreRev;
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(current, bucket.CurrentConfig);
        Assert.Same(keyMapper, bucket.KeyMapper);
        keyMapperFactory.Verify(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        nodeFactory.Verify(x => x.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_OnlyNewerRev_IsStoredWithSameKeyMapper()
    {
        var keyMapper = new Mock<IKeyMapper>().Object;
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        keyMapperFactory.Setup(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
            .Returns(keyMapper);
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), keyMapperFactory.Object);
        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);
        Assert.Same(keyMapper, bucket.KeyMapper);
        keyMapperFactory.Invocations.Clear();

        var incoming = CreateConfig(rev: 3, activeNode: 1);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.Same(keyMapper, bucket.KeyMapper);
        keyMapperFactory.Verify(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_OnlyNewBucketCapability_IsSeenByNextRequest()
    {
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), CreateKeyMapperFactory());
        // Replica reads map the key, so the bucket needs the key mapper that a new map builds.
        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);
        var collection = CreateCollection(bucket);
        LookupInSpec[] specs = [LookupInSpec.Get("name")];
        Assert.Throws<FeatureNotAvailableException>(() => collection.LookupInAllReplicasAsync("key", specs));

        var incoming = CreateConfig(rev: 3, activeNode: 1);
        incoming.BucketCapabilities = [BucketCapabilities.SUBDOC_REPLICA_READ];
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.NotNull(collection.LookupInAllReplicasAsync("key", specs));
    }

    [Fact]
    public async Task ConfigUpdatedAsync_OnlyServerGroupChange_IsStored()
    {
        var current = CreateConfig(rev: 1, activeNode: 0);
        current.NodesExt[0].ServerGroup = "group-1";
        current.NodesExt[1].ServerGroup = "group-2";
        var bucket = CreateBucket(current);

        var incoming = CreateConfig(rev: 2, activeNode: 0);
        incoming.NodesExt[0].ServerGroup = "group-1";
        incoming.NodesExt[1].ServerGroup = "group-1";
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        var groups = bucket.CurrentConfig!.ServerGroupNodeIndexes;
        Assert.Equal(new[] { 0, 1 }, groups["group-1"]);
        Assert.False(groups.ContainsKey("group-2"));
    }

    [Fact]
    public async Task ConfigUpdatedAsync_WakesConfigRefresherOnlyForNewMap()
    {
        // The refresher loop is not started, so a wake-up stays pending.
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), CreateKeyMapperFactory());

        var newerRev = CreateConfig(rev: 2, activeNode: 0);
        await bucket.ConfigUpdatedAsync(newerRev).WaitAsync(Deadline);
        Assert.Same(newerRev, bucket.CurrentConfig);
        Assert.False(bucket.ConfigRefresher.HasPendingWakeUp);

        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 3, activeNode: 1)).WaitAsync(Deadline);
        Assert.True(bucket.ConfigRefresher.HasPendingWakeUp);
    }

    private static IVBucketKeyMapperFactory CreateKeyMapperFactory() =>
        new VBucketKeyMapperFactory(new VBucketFactory(new Mock<ILogger<VBucket>>().Object));

    private static CouchbaseCollection CreateCollection(BucketBase bucket) =>
        new(bucket, new Mock<IOperationConfigurator>().Object, new Mock<ILogger<CouchbaseCollection>>().Object,
            new Mock<ILogger<GetResult>>().Object, new Redactor(RedactionLevel.None),
            CouchbaseCollection.DefaultCollectionName,
            Mock.Of<IScope>(scope => scope.IsDefaultScope && scope.Name == Scope.DefaultScopeName),
            NoopRequestTracer.Instance, NullFallbackTypeSerializerProvider.Instance, Mock.Of<IServiceProvider>());

    [Fact]
    public async Task ConfigUpdatedAsync_OnlyNewSearchPorts_UpdatesTheNodeAndCreatesNone()
    {
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), nodeFactory: nodeFactory);
        Assert.True(bucket.Nodes.TryGet(new HostEndpointWithPort("nodeA", 11210), out var nodeA));
        Assert.False(nodeA.HasSearch);

        // The node compare skips the search ports.
        var incoming = CreateConfig(rev: 2, activeNode: 0);
        incoming.NodesExt[0].Services.Fts = 8094;
        Assert.False(incoming.HasClusterNodesChanged(bucket.CurrentConfig));
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.True(nodeA.HasSearch);
        Assert.True(bucket.Nodes.TryGet(new HostEndpointWithPort("nodeA", 11210), out var found));
        Assert.Same(nodeA, found);
        Assert.Equal(2, bucket.Nodes.Count);
        nodeFactory.VerifyNoOtherCalls();
    }

    private static readonly HostEndpointWithPort NodeA = new("nodeA", 11210);

    // The cluster config at rev 20 moves the query port of nodeA. The bucket at rev 10 then accepts a config at rev 15.
    [Fact]
    public async Task ConfigUpdatedAsync_BucketConfigOlderThanNodeView_KeepsNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var nodeA = GetClusterNode(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 15, queryPort: 8093);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(nodeA, rev: 20, queryPort: 9093);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_ForcedBucketConfigOlderThanNodeView_ReplacesNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var nodeA = GetClusterNode(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 15, queryPort: 8093);
        incoming.IgnoreRev = true;
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(nodeA, rev: 15, queryPort: 8093);
    }

    [Theory]
    [InlineData(20UL)]
    [InlineData(21UL)]
    public async Task ConfigUpdatedAsync_BucketConfigEqualOrNewerThanNodeView_ReplacesNodeView(ulong rev)
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var nodeA = GetClusterNode(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev, queryPort: 8093);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(nodeA, rev, queryPort: 8093);
    }

    // The config handler applies one config at a time, so the test calls both updates, as a bucket open does while a
    // cluster config applies.
    [Fact]
    public async Task ConfigUpdatedAsync_OlderBucketConfigWhileClusterConfigApplies_KeepsNewerNodeView()
    {
        var nodeC = new HostEndpointWithPort("nodeC", 0);
        var nodeCConnect = new TaskCompletionSource<IClusterNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        nodeFactory.Setup(f => f.CreateAndConnectAsync(nodeC, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()))
            .Returns(nodeCConnect.Task);
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093), nodeFactory: nodeFactory);
        var nodeA = GetClusterNode(bucket, NodeA);

        // The new query node nodeC comes after nodeA, so the cluster config waits on its connect after it sets nodeA.
        var clusterConfig = ClusterQueryConfig(rev: 20, queryPort: 9093);
        clusterConfig.NodesExt.Add(new NodesExt { Hostname = "nodeC", Services = new Services { N1Ql = 8093 } });
        var clusterUpdate = bucket.Context.ConfigUpdatedAsync(clusterConfig);
        Assert.False(clusterUpdate.IsCompleted);
        AssertNodeView(nodeA, rev: 20, queryPort: 9093);

        await bucket.ConfigUpdatedAsync(QueryConfig(rev: 15, queryPort: 8093)).WaitAsync(Deadline);
        nodeCConnect.SetResult(Mock.Of<IClusterNode>(node => node.EndPoint == nodeC));
        await clusterUpdate.WaitAsync(Deadline);

        AssertNodeView(nodeA, rev: 20, queryPort: 9093);
    }

    private static readonly HostEndpointWithPort NodeC = new("nodeC", 11210);

    // The cluster config adds nodeC with no owner before the bucket map has it.
    [Fact]
    public async Task ConfigUpdatedAsync_NewNodeFromClusterConfig_IsClaimed()
    {
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), nodeFactory: nodeFactory);
        SetupCreateNode(nodeFactory, bucket.Context, NodeC);
        await bucket.Context.ConfigUpdatedAsync(AsClusterConfig(CreateConfigWithNodeC(rev: 2))).WaitAsync(Deadline);
        var nodeC = Assert.Single(bucket.Context.Nodes, node => node.EndPoint.Equals(NodeC));
        Assert.False(nodeC.IsAssigned);

        var incoming = CreateConfigWithNodeC(rev: 3);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.Same(bucket, nodeC.Owner);
        Assert.True(bucket.Nodes.TryGet(NodeC, out var found));
        Assert.Same(nodeC, found);
        nodeFactory.Verify(x => x.CreateAndConnectAsync(NodeC, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_NodeLeftTheMap_IsRemoved()
    {
        var bucket = CreateBucket(CreateConfigWithNodeC(rev: 1),
            nodeFactory: new Mock<IClusterNodeFactory>(MockBehavior.Strict));
        Assert.True(bucket.Nodes.TryGet(NodeC, out var nodeC));

        var incoming = CreateConfig(rev: 2, activeNode: 0);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.DoesNotContain(bucket.Context.Nodes, node => ReferenceEquals(node, nodeC));
        Assert.DoesNotContain(bucket.Nodes, node => ReferenceEquals(node, nodeC));
        Assert.False(bucket.Nodes.TryGet(NodeC, out _));
        Assert.Equal(2, bucket.Nodes.Count);
    }

    // The node compare skips the alternate addresses, so only the vbucket map shows the new host.
    [Fact]
    public async Task ConfigUpdatedAsync_NewExternalHost_MovesTheNodeAndItsVBuckets()
    {
        var oldEndPoint = new HostEndpointWithPort("extA", 11210);
        var newEndPoint = new HostEndpointWithPort("extA2", 11210);
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var keyMapperFactory = new VBucketKeyMapperFactory(new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
        var bucket = CreateBucket(ExternalConfig(rev: 1, nodeAHost: "extA"), keyMapperFactory, nodeFactory);
        SetupCreateNode(nodeFactory, bucket.Context, newEndPoint);
        Assert.True(bucket.Nodes.TryGet(oldEndPoint, out var oldNode));

        var incoming = ExternalConfig(rev: 2, nodeAHost: "extA2");
        Assert.False(incoming.HasClusterNodesChanged(bucket.CurrentConfig));
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        var newNode = Assert.Single(bucket.Context.Nodes, node => node.EndPoint.Equals(newEndPoint));
        Assert.Same(bucket, newNode.Owner);
        Assert.True(bucket.Nodes.TryGet(newEndPoint, out var found));
        Assert.Same(newNode, found);
        Assert.DoesNotContain(bucket.Context.Nodes, node => ReferenceEquals(node, oldNode));
        Assert.DoesNotContain(bucket.Nodes, node => ReferenceEquals(node, oldNode));

        // An operation finds the node of its vbucket the same way.
        var keyMapper = Assert.IsType<VBucketKeyMapper>(bucket.KeyMapper);
        for (short i = 0; i < incoming.VBucketServerMap.VBucketMap.Length; i++)
        {
            var primary = keyMapper[i].LocatePrimary();
            Assert.NotNull(primary);
            Assert.True(bucket.Nodes.TryGet(primary.Value, out _), $"No bucket node for vbucket {i} at {primary}.");
        }
    }

    // A rebootstrap adds a node with no owner and no view at a bootstrap host, then applies the bucket config.
    [Fact]
    public async Task ConfigUpdatedAsync_RebootstrapNodeAtHostOfBucketNode_IsClaimed()
    {
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0),
            nodeFactory: new Mock<IClusterNodeFactory>(MockBehavior.Strict));
        var rebootstrapNode = CreateNode(bucket.Context, NodeA, nodeAdapter: null);
        bucket.Context.AddNode(rebootstrapNode);

        var incoming = CreateConfig(rev: 2, activeNode: 0);
        incoming.IgnoreRev = true;
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.Same(bucket, rebootstrapNode.Owner);
        Assert.True(rebootstrapNode.HasKv);
        Assert.Contains(bucket.Nodes, node => ReferenceEquals(node, rebootstrapNode));
    }

    /// <summary>
    /// A config of the bucket where nodeA runs the query service on <paramref name="queryPort"/>.
    /// </summary>
    private static BucketConfig QueryConfig(ulong rev, int queryPort)
    {
        var config = CreateConfig(rev, activeNode: 0);
        config.NodesExt[0].Services.N1Ql = queryPort;
        return config;
    }

    /// <summary>
    /// The cluster config that matches <see cref="QueryConfig"/>.
    /// </summary>
    private static BucketConfig ClusterQueryConfig(ulong rev, int queryPort) => AsClusterConfig(QueryConfig(rev, queryPort));

    /// <summary>
    /// Makes a bucket config the cluster config with the same nodes. A cluster config has no vbucket map.
    /// </summary>
    private static BucketConfig AsClusterConfig(BucketConfig config)
    {
        config.Name = BucketConfig.GlobalBucketName;
        config.VBucketServerMap = new VBucketServerMapDto();
        return config;
    }

    private static ClusterNode GetClusterNode(CouchbaseBucket bucket, HostEndpointWithPort endPoint)
    {
        Assert.True(bucket.Nodes.TryGet(endPoint, out var node));
        return Assert.IsType<ClusterNode>(node);
    }

    private static void AssertNodeView(ClusterNode node, ulong rev, int queryPort)
    {
        Assert.Equal(new ConfigVersion(0, rev), node.ConfigVersion);
        Assert.Equal(queryPort, node.QueryUri.Port);
    }

    /// <summary>
    /// Two data nodes and four vbuckets, all active on <paramref name="activeNode"/>.
    /// </summary>
    private static BucketConfig CreateConfig(ulong rev, short activeNode)
    {
        var config = new BucketConfig
        {
            Rev = rev,
            Name = "default",
            Nodes = new List<Node>
            {
                new() { Hostname = "nodeA:8091", Ports = new Ports { Direct = 11210 } },
                new() { Hostname = "nodeB:8091", Ports = new Ports { Direct = 11210 } }
            },
            NodesExt = new List<NodesExt>
            {
                new() { Hostname = "nodeA", Services = new Services { Kv = 11210 } },
                new() { Hostname = "nodeB", Services = new Services { Kv = 11210 } }
            },
            VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "nodeA:11210", "nodeB:11210" },
                VBucketMap = new[]
                {
                    new[] { activeNode, (short)(1 - activeNode) },
                    new[] { activeNode, (short)(1 - activeNode) },
                    new[] { activeNode, (short)(1 - activeNode) },
                    new[] { activeNode, (short)(1 - activeNode) }
                }
            }
        };
        config.OnDeserialized();
        return config;
    }

    /// <summary>
    /// <see cref="CreateConfig"/> with a third data node, nodeC, that is active for the last vbucket.
    /// </summary>
    private static BucketConfig CreateConfigWithNodeC(ulong rev)
    {
        var config = CreateConfig(rev, activeNode: 0);
        config.Nodes.Add(new Node { Hostname = "nodeC:8091", Ports = new Ports { Direct = 11210 } });
        config.NodesExt.Add(new NodesExt { Hostname = "nodeC", Services = new Services { Kv = 11210 } });
        config.VBucketServerMap.ServerList = ["nodeA:11210", "nodeB:11210", "nodeC:11210"];
        config.VBucketServerMap.VBucketMap[3] = [2, 0];
        return config;
    }

    /// <summary>
    /// Three data nodes and no vbucket map, like the config of a new bucket on a server before 7.6.2 (MB-60405).
    /// </summary>
    private static BucketConfig CreateConfigWithEmptyMap(ulong rev, bool nullMap = false)
    {
        var config = CreateConfigWithNodeC(rev);
        config.VBucketServerMap.VBucketMap = nullMap ? null : [];
        return config;
    }

    /// <summary>
    /// <see cref="CreateConfig"/> on the external network, where nodeA has the external host <paramref name="nodeAHost"/>.
    /// </summary>
    private static BucketConfig ExternalConfig(ulong rev, string nodeAHost)
    {
        var config = CreateConfig(rev, activeNode: 0);
        config.NodesExt[0].AlternateAddresses = ExternalAddress(nodeAHost);
        config.NodesExt[1].AlternateAddresses = ExternalAddress("extB");

        // Set as ConfigHandler does. It also moves the vbucket servers to the external hosts.
        config.NetworkResolution = NetworkResolution.External;
        return config;
    }

    private static Dictionary<string, ExternalAddressesConfig> ExternalAddress(string hostname) => new()
    {
        [NetworkResolution.External] = new ExternalAddressesConfig { Hostname = hostname, Ports = new Services { Kv = 11210 } }
    };

    CouchbaseBucket CreateBucket(BucketConfig bootstrapConfig, IVBucketKeyMapperFactory keyMapperFactory = null,
        Mock<IClusterNodeFactory> nodeFactory = null)
    {
        var node = new Mock<IClusterNode>();
        node.Setup(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort>
            { new("127.0.0.1", 11210) });

        // A factory from the test keeps its own setups.
        if (nodeFactory is null)
        {
            nodeFactory = new Mock<IClusterNodeFactory>();
            nodeFactory.Setup(x => x.CreateAndConnectAsync(
                    It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(node.Object));
        }

        var options = new ClusterOptions().AddClusterService(nodeFactory.Object).WithPasswordAuthentication("username", "password");
        var clusterCtx = new ClusterContext(new CancellationTokenSource(), options);

        var bucket = new CouchbaseBucket("default",
            clusterCtx,
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            keyMapperFactory ?? new Mock<IVBucketKeyMapperFactory>().Object,
            new Mock<ILogger<CouchbaseBucket>>().Object,
            new Redactor(RedactionLevel.None),
            new Mock<IBootstrapperFactory>().Object,
            NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object,
            new BestEffortRetryStrategy(),
            bootstrapConfig,
            clusterCtx.ServiceProvider.GetRequiredService<IConfigRefresherFactory>());

        node.Setup(x => x.Owner).Returns(bucket);

        // Like a bootstrapped bucket, it owns a node for each node of its config.
        foreach (var nodeAdapter in bootstrapConfig.GetNodes())
        {
            var clusterNode = CreateNode(clusterCtx, HostEndpointWithPort.Create(nodeAdapter, options), nodeAdapter);
            clusterNode.Owner = bucket;
            clusterCtx.AddNode(clusterNode);
            bucket.Nodes.Add(clusterNode);
        }

        return bucket;
    }

    /// <summary>
    /// A node with a mock connection pool, so a bucket can claim it and select itself on it.
    /// </summary>
    private static ClusterNode CreateNode(ClusterContext context, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter)
    {
        var poolFactory = new Mock<IConnectionPoolFactory>();
        poolFactory.Setup(x => x.Create(It.IsAny<ClusterNode>())).Returns(new Mock<IConnectionPool>().Object);

        return new ClusterNode(context, poolFactory.Object,
            new Mock<ILogger<ClusterNode>>().Object,
            new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
            new Mock<ICircuitBreaker>().Object,
            new Mock<ISaslMechanismFactory>().Object,
            new Redactor(RedactionLevel.None),
            endPoint,
            nodeAdapter,
            NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object);
    }

    /// <summary>
    /// Makes the factory create a node at <paramref name="endPoint"/>, as for a node new to the cluster.
    /// </summary>
    private static void SetupCreateNode(Mock<IClusterNodeFactory> nodeFactory, ClusterContext context,
        HostEndpointWithPort endPoint) =>
        nodeFactory.Setup(x => x.CreateAndConnectAsync(endPoint, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter, CancellationToken _) =>
                Task.FromResult<IClusterNode>(CreateNode(context, ep, nodeAdapter)));

    #region MergeClusterLabels

    [Fact]
    public void MergeClusterLabels_IncomingHasNewName_BothConfigsGetNewName()
    {
        var current = new BucketConfig { ClusterName = "OldCluster", ClusterUuid = "uuid-1" };
        var incoming = new BucketConfig { ClusterName = "NewCluster", ClusterUuid = "uuid-1" };

        incoming.MergeClusterLabels(current);

        Assert.Equal("NewCluster", current.ClusterName);
        Assert.Equal("NewCluster", incoming.ClusterName);
    }

    [Fact]
    public void MergeClusterLabels_IncomingHasNullName_BothConfigsKeepCurrentName()
    {
        var current = new BucketConfig { ClusterName = "OldCluster", ClusterUuid = "uuid-1" };
        var incoming = new BucketConfig { ClusterName = null, ClusterUuid = "uuid-1" };

        incoming.MergeClusterLabels(current);

        Assert.Equal("OldCluster", current.ClusterName);
        Assert.Equal("OldCluster", incoming.ClusterName);
    }

    [Fact]
    public void MergeClusterLabels_BothNull_BothRemainNull()
    {
        var current = new BucketConfig { ClusterName = null, ClusterUuid = null };
        var incoming = new BucketConfig { ClusterName = null, ClusterUuid = null };

        incoming.MergeClusterLabels(current);

        Assert.Null(current.ClusterName);
        Assert.Null(incoming.ClusterName);
    }

    [Fact]
    public void MergeClusterLabels_LabelsEqual_NoMutation()
    {
        var current = new BucketConfig { ClusterName = "Same", ClusterUuid = "uuid-1" };
        var incoming = new BucketConfig { ClusterName = "Same", ClusterUuid = "uuid-1" };

        incoming.MergeClusterLabels(current);

        Assert.Equal("Same", current.ClusterName);
        Assert.Equal("Same", incoming.ClusterName);
    }

    [Fact]
    public void MergeClusterLabels_IncomingHasNewUuid_BothConfigsGetNewUuid()
    {
        var current = new BucketConfig { ClusterName = "Cluster", ClusterUuid = "old-uuid" };
        var incoming = new BucketConfig { ClusterName = "Cluster", ClusterUuid = "new-uuid" };

        incoming.MergeClusterLabels(current);

        Assert.Equal("new-uuid", current.ClusterUuid);
        Assert.Equal("new-uuid", incoming.ClusterUuid);
    }

    [Fact]
    public void MergeClusterLabels_IncomingHasNewNameAndNullUuid_MergesBothFields()
    {
        var current = new BucketConfig { ClusterName = "OldCluster", ClusterUuid = "old-uuid" };
        var incoming = new BucketConfig { ClusterName = "NewCluster", ClusterUuid = null };

        incoming.MergeClusterLabels(current);

        Assert.Equal("NewCluster", current.ClusterName);
        Assert.Equal("NewCluster", incoming.ClusterName);
        Assert.Equal("old-uuid", current.ClusterUuid);
        Assert.Equal("old-uuid", incoming.ClusterUuid);
    }

    #endregion
}
