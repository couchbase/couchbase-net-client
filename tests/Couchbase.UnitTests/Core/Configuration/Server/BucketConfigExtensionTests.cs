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
using Couchbase.KeyValue.ZoneAware;
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

    // Host 1 runs KV but the nodes list does not hold it yet, as when it joins in a rebalance.
    [Theory]
    [InlineData(BucketConfig.GlobalBucketName)]
    [InlineData("default")]
    public void GetClusterNodes_ListsEveryNodesExtEntryWithItsOwnServices(string name)
    {
        var config = JoiningKvNodeConfig(name, nodesList: ["10.0.0.1"]);

        var clusterNodes = config.GetClusterNodes();

        Assert.Equal(["10.0.0.1", "10.0.0.2", "10.0.0.3"], clusterNodes.ConvertAll(node => node.Hostname));
        Assert.Equal([11210, 11210, 0], clusterNodes.ConvertAll(node => node.KeyValue));
        Assert.Equal([0, 0, 8093], clusterNodes.ConvertAll(node => node.N1Ql));
        Assert.Equal([true, true, false], clusterNodes.ConvertAll(node => node.IsKvNode));
    }

    // NCBC-2422. The bucket map drops a KV entry that the nodes list does not hold.
    [Fact]
    public void GetNodes_KvEntryMissingFromNodes_IsDropped()
    {
        var config = JoiningKvNodeConfig("default", nodesList: ["10.0.0.1"]);

        var mapNodes = config.GetNodes();

        Assert.Equal(["10.0.0.1", "10.0.0.3"], mapNodes.ConvertAll(node => node.Hostname));
    }

    // With no nodes list the bucket map has no KV entry. The cluster list keeps KV, and the config is not changed.
    [Fact]
    public void GetNodes_BucketConfigWithNoNodesList_DropsKvOnlyInTheAdapters()
    {
        var config = JoiningKvNodeConfig("default", nodesList: []);
        var copy = JoiningKvNodeConfig("default", nodesList: []);

        var mapNodes = config.GetNodes();
        var clusterNodes = config.GetClusterNodes();

        Assert.All(mapNodes, node => Assert.False(node.IsKvNode));
        Assert.Equal([true, true, false], clusterNodes.ConvertAll(node => node.IsKvNode));
        Assert.Equal([11210, 11210, 0], config.NodesExt.ConvertAll(nodeExt => nodeExt.Services.Kv));
        Assert.Equal(copy.NodesExt, config.NodesExt);
    }

    private static BucketConfig JoiningKvNodeConfig(string name, string[] nodesList)
    {
        var config = new BucketConfig
        {
            Name = name,
            Rev = 1,
            NodesExt =
            [
                new NodesExt { Hostname = "10.0.0.1", Services = new Services { Mgmt = 8091, Kv = 11210 } },
                new NodesExt { Hostname = "10.0.0.2", Services = new Services { Mgmt = 8091, Kv = 11210 } },
                new NodesExt { Hostname = "10.0.0.3", Services = new Services { Mgmt = 8091, N1Ql = 8093 } }
            ],
            Nodes = [.. Array.ConvertAll(nodesList, host => new Node { Hostname = $"{host}:8091" })]
        };
        config.OnDeserialized();
        return config;
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
        nodeFactory.Verify(x => x.CreateBucketNode(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
            It.IsAny<IBucket>()), Times.Never);
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
    public async Task ConfigUpdatedAsync_OnlyServerGroupChange_IsSeenByNextReplicaRead()
    {
        // Records the replica index of each read, null for the active copy, and fails it.
        var reads = new List<short?>();
        var retryOrchestrator = new Mock<IRetryOrchestrator>();
        retryOrchestrator.Setup(x => x.RetryAsync(It.IsAny<BucketBase>(), It.IsAny<IOperation>(),
                It.IsAny<CancellationTokenPair>()))
            .Callback((BucketBase _, IOperation op, CancellationTokenPair _) => reads.Add(op.ReplicaIdx))
            .Returns(Task.FromException<ResponseStatus>(new TemporaryFailureException()));
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), CreateKeyMapperFactory(),
            retryOrchestrator: retryOrchestrator.Object);
        // The active copy is on node 1 and the replica on node 0.
        var current = CreateConfig(rev: 2, activeNode: 1);
        current.NodesExt[0].ServerGroup = "group-1";
        current.NodesExt[1].ServerGroup = "group-2";
        await bucket.ConfigUpdatedAsync(current).WaitAsync(Deadline);
        bucket.Context.ClusterOptions.PreferredServerGroup = "group-1";
        var collection = CreateCollection(bucket);
        var options = new GetAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroup);

        async Task<short?[]> ReadAllReplicasAsync()
        {
            reads.Clear();
            foreach (var read in collection.GetAllReplicasAsync("key", options))
            {
                await Assert.ThrowsAsync<TemporaryFailureException>(() => read.WaitAsync(Deadline));
            }
            return reads.ToArray();
        }

        Assert.Equal([(short)0], await ReadAllReplicasAsync());

        var incoming = CreateConfig(rev: 3, activeNode: 1);
        incoming.NodesExt[0].ServerGroup = "group-1";
        incoming.NodesExt[1].ServerGroup = "group-1";
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.Equal([(short)0, null], await ReadAllReplicasAsync());
    }

    [Fact]
    public async Task ConfigUpdatedAsync_OnlyServerGroupChange_StoresSnapshotWithSameKeyMapper()
    {
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), CreateKeyMapperFactory());
        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);
        var before = bucket.ConfigSnapshot;

        var incoming = CreateConfig(rev: 3, activeNode: 1);
        incoming.NodesExt[0].ServerGroup = "group-1";
        incoming.NodesExt[1].ServerGroup = "group-2";
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        var after = bucket.ConfigSnapshot;
        Assert.NotSame(before, after);
        Assert.Same(incoming, after.Config);
        Assert.Same(before.KeyMapper, after.KeyMapper);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_NewMap_StoresSnapshotWithNewKeyMapper()
    {
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), CreateKeyMapperFactory());
        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);
        var before = bucket.ConfigSnapshot;
        Assert.NotNull(before.KeyMapper);

        var incoming = CreateConfig(rev: 3, activeNode: 0);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        var after = bucket.ConfigSnapshot;
        Assert.Same(incoming, after.Config);
        Assert.NotNull(after.KeyMapper);
        Assert.NotSame(before.KeyMapper, after.KeyMapper);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_FailedApply_KeepsSnapshot()
    {
        var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
        keyMapperFactory.Setup(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException());
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), keyMapperFactory.Object);
        var before = bucket.ConfigSnapshot;

        await bucket.ConfigUpdatedAsync(CreateConfig(rev: 2, activeNode: 1)).WaitAsync(Deadline);

        Assert.Same(before, bucket.ConfigSnapshot);
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
    // The cluster node keeps the view, and the bucket node follows its bucket config.
    [Fact]
    public async Task ConfigUpdatedAsync_BucketConfigOlderThanNodeView_KeepsClusterNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var clusterNodeA = ClusterNodeAt(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 15, queryPort: 8093);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(clusterNodeA, rev: 20, queryPort: 9093);
        AssertNodeView(GetClusterNode(bucket, NodeA), rev: 15, queryPort: 8093);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_ForcedBucketConfigOlderThanNodeView_ReplacesClusterNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var clusterNodeA = ClusterNodeAt(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 15, queryPort: 8093);
        incoming.IgnoreRev = true;
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(clusterNodeA, rev: 15, queryPort: 8093);
        AssertNodeView(GetClusterNode(bucket, NodeA), rev: 15, queryPort: 8093);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_BucketConfigNewerThanNodeView_ReplacesClusterNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var clusterNodeA = ClusterNodeAt(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 21, queryPort: 8093);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(clusterNodeA, rev: 21, queryPort: 8093);
        AssertNodeView(GetClusterNode(bucket, NodeA), rev: 21, queryPort: 8093);
    }

    // A bucket config at the revision of the cluster view is not the view, so the cluster node keeps the view of the
    // cluster config. The bucket node follows its bucket config. The server sends the same nodes at one revision.
    [Fact]
    public async Task ConfigUpdatedAsync_BucketConfigAtTheRevisionOfTheNodeView_KeepsClusterNodeView()
    {
        var bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093));
        var clusterNodeA = ClusterNodeAt(bucket, NodeA);
        await bucket.Context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 20, queryPort: 9093)).WaitAsync(Deadline);

        var incoming = QueryConfig(rev: 20, queryPort: 8093);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        AssertNodeView(clusterNodeA, rev: 20, queryPort: 9093);
        AssertNodeView(GetClusterNode(bucket, NodeA), rev: 20, queryPort: 8093);
    }

    private static ClusterNode ClusterNodeAt(CouchbaseBucket bucket, HostEndpointWithPort endPoint) =>
        Assert.IsType<ClusterNode>(Assert.Single(bucket.Context.Nodes, node => node.EndPoint.Equals(endPoint)));

    // The config handler applies one config at a time, so the test runs the bucket update in the middle of the cluster
    // reconcile, as a bucket open does while a cluster config applies.
    [Fact]
    public async Task ConfigUpdatedAsync_OlderBucketConfigWhileClusterConfigApplies_KeepsNewerNodeView()
    {
        var nodeC = new HostEndpointWithPort("nodeC", 0);
        CouchbaseBucket bucket = null;
        ClusterNode nodeA = null;
        Task bucketUpdate = null;
        var nodeAViewBefore = default(ConfigVersion?);
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        // The new query node nodeC comes after nodeA, so the reconcile creates it after it sets nodeA.
        nodeFactory.Setup(f => f.CreateClusterNode(nodeC, It.IsAny<NodeAdapter>()))
            .Returns(() =>
            {
                nodeAViewBefore = nodeA.ConfigVersion;
                bucketUpdate = bucket.ConfigUpdatedAsync(QueryConfig(rev: 15, queryPort: 8093));
                return Mock.Of<IClusterNode>(node => node.EndPoint == nodeC);
            });
        bucket = CreateBucket(QueryConfig(rev: 10, queryPort: 8093), nodeFactory: nodeFactory);
        nodeA = ClusterNodeAt(bucket, NodeA);

        var clusterConfig = ClusterQueryConfig(rev: 20, queryPort: 9093);
        clusterConfig.NodesExt.Add(new NodesExt { Hostname = "nodeC", Services = new Services { N1Ql = 8093 } });
        await bucket.Context.ConfigUpdatedAsync(clusterConfig).WaitAsync(Deadline);
        await bucketUpdate.WaitAsync(Deadline);

        Assert.Equal(new ConfigVersion(0, 20), nodeAViewBefore);
        AssertNodeView(nodeA, rev: 20, queryPort: 9093);
    }

    private static readonly HostEndpointWithPort NodeC = new("nodeC", 11210);

    // The cluster config adds a cluster node at nodeC before the bucket map has it. The bucket then creates a node of its
    // own there, and the cluster node keeps no owner.
    [Fact]
    public async Task ConfigUpdatedAsync_NewNodeFromClusterConfig_GetsABucketNodeOfItsOwn()
    {
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), nodeFactory: nodeFactory);
        SetupCreateNode(nodeFactory, bucket.Context, NodeC);
        await bucket.Context.ConfigUpdatedAsync(AsClusterConfig(CreateConfigWithNodeC(rev: 2))).WaitAsync(Deadline);
        var clusterNodeC = Assert.Single(bucket.Context.Nodes, node => node.EndPoint.Equals(NodeC));

        var incoming = CreateConfigWithNodeC(rev: 3);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.True(bucket.Nodes.TryGet(NodeC, out var bucketNodeC));
        Assert.NotSame(clusterNodeC, bucketNodeC);
        Assert.Same(bucket, bucketNodeC.Owner);
        Assert.Null(clusterNodeC.Owner);
        Assert.Same(clusterNodeC, Assert.Single(bucket.Context.Nodes, node => node.EndPoint.Equals(NodeC)));
        nodeFactory.Verify(x => x.CreateClusterNode(NodeC, It.IsAny<NodeAdapter>()), Times.Once);
        nodeFactory.Verify(x => x.CreateBucketNode(NodeC, It.IsAny<NodeAdapter>(), bucket), Times.Once);
    }

    // NCBC-4414. The new node joins before its first connect ends, and the config, the key mapper and the node list do
    // not wait for it. The VBucket of nodeC maps to the new node.
    [Fact]
    public async Task ConfigUpdatedAsync_MapWithANewNode_AppliesBeforeTheNodeConnects()
    {
        var connects = 0;
        var pool = new Mock<IConnectionPool>();
        pool.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);
        pool.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref connects))
            .Returns(new TaskCompletionSource<bool>().Task);
        var poolFactory = new Mock<IConnectionPoolFactory>();
        poolFactory.Setup(x => x.Create(It.IsAny<ClusterNode>())).Returns(pool.Object);
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var keyMapperFactory = new VBucketKeyMapperFactory(new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
        var bucket = CreateBucket(CreateConfig(rev: 1, activeNode: 0), keyMapperFactory, nodeFactory);
        var realFactory = new ClusterNodeFactory(bucket.Context, poolFactory.Object, new Mock<ILogger<ClusterNode>>().Object,
            new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()), new Mock<ISaslMechanismFactory>().Object,
            new Redactor(RedactionLevel.None), NoopRequestTracer.Instance, new Mock<IOperationConfigurator>().Object);
        nodeFactory.Setup(x => x.CreateClusterNode(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>()))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter) => CreateNode(bucket.Context, ep, nodeAdapter));
        nodeFactory.Setup(x => x.CreateBucketNode(NodeC, It.IsAny<NodeAdapter>(), bucket))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter, IBucket owner) =>
                realFactory.CreateBucketNode(ep, nodeAdapter, owner));
        var keyMapper = bucket.KeyMapper;

        var incoming = CreateConfigWithNodeC(rev: 2);
        await bucket.ConfigUpdatedAsync(incoming).WaitAsync(Deadline);

        Assert.Same(incoming, bucket.CurrentConfig);
        Assert.NotSame(keyMapper, bucket.KeyMapper);
        Assert.Equal(NodeC, ((VBucketKeyMapper)bucket.KeyMapper)[(short)3].LocatePrimary());
        Assert.True(bucket.Nodes.TryGet(NodeC, out var nodeC));
        Assert.True(nodeC.IsAwaitingFirstConnection);
        Assert.Same(bucket, nodeC.Owner);
        Assert.Equal(1, Volatile.Read(ref connects));
        Assert.Equal(3, bucket.Nodes.Count);
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
        Assert.True(nodeC.IsDead);
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
        Assert.Single(bucket.Nodes, node => node.EndPoint.Equals(newEndPoint));
        Assert.True(bucket.Nodes.TryGet(newEndPoint, out _));
        Assert.True(oldNode.IsDead);
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

    // A recreated cluster behind the same host names starts again at low revisions.
    [Fact]
    public async Task ConfigUpdatedAsync_ClusterConfigAfterRebootstrap_ReplacesViewOfOldCluster()
    {
        var (context, oldNode, nodeFactory) = CreateContextWithOldClusterNode();

        await RebootstrapToNewClusterAsync(context, nodeFactory);
        await context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 6, queryPort: 7093)).WaitAsync(Deadline);

        AssertNodeView(oldNode, rev: 6, queryPort: 7093);
    }

    [Fact]
    public async Task ConfigUpdatedAsync_ClusterConfigOlderThanNodeViewWithoutRebootstrap_KeepsNodeView()
    {
        var (context, oldNode, nodeFactory) = CreateContextWithOldClusterNode();
        SetupCreateNode(nodeFactory, context, new HostEndpointWithPort("nodeB", 11210));

        await context.ConfigUpdatedAsync(ClusterQueryConfig(rev: 6, queryPort: 7093)).WaitAsync(Deadline);

        AssertNodeView(oldNode, rev: 100, queryPort: 9093);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NodesAdapter_OlderViewAfterRebootstrapAndOneView_KeepsNodeView(bool forced)
    {
        var (context, oldNode, nodeFactory) = CreateContextWithOldClusterNode();
        await RebootstrapToNewClusterAsync(context, nodeFactory);

        // A forced view, as from a bucket rebootstrap, also ends the reset.
        var taken = ClusterQueryConfig(rev: 6, queryPort: 7093);
        taken.IgnoreRev = forced;
        oldNode.NodesAdapter = taken.GetNodes()[0];
        oldNode.NodesAdapter = ClusterQueryConfig(rev: 4, queryPort: 6093).GetNodes()[0];

        AssertNodeView(oldNode, rev: 6, queryPort: 7093);
    }

    /// <summary>
    /// A context with a nodeA node that no bucket owns and that holds a view of an old cluster at rev 100.
    /// </summary>
    private static (ClusterContext Context, ClusterNode OldNode, Mock<IClusterNodeFactory> NodeFactory) CreateContextWithOldClusterNode()
    {
        var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
        var options = new ClusterOptions().WithConnectionString("couchbase://nodeB")
            .WithPasswordAuthentication("username", "password");
        options.EnableDnsSrvResolution = false;
        options.AddClusterService(nodeFactory.Object);
        var context = new ClusterContext(new CancellationTokenSource(), options);

        var oldNode = CreateNode(context, NodeA, ClusterQueryConfig(rev: 100, queryPort: 9093).GetNodes()[0]);
        context.AddNode(oldNode);
        return (context, oldNode, nodeFactory);
    }

    /// <summary>
    /// Bootstraps from the seed nodeB of a new cluster at rev 2. The bootstrap adds a new nodeA node after the old one.
    /// </summary>
    private static async Task RebootstrapToNewClusterAsync(ClusterContext context, Mock<IClusterNodeFactory> nodeFactory)
    {
        var seedEndPoint = new HostEndpointWithPort("nodeB", 11210);
        var seed = new Mock<IClusterNode>();
        seed.Setup(x => x.EndPoint).Returns(seedEndPoint);
        seed.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ClusterQueryConfig(rev: 2, queryPort: 8093));
        nodeFactory.Setup(x => x.CreateAndConnectAsync(seedEndPoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.Object);
        SetupCreateNode(nodeFactory, context, NodeA);

        await context.BootstrapGlobalAsync().WaitAsync(Deadline);
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
        Mock<IClusterNodeFactory> nodeFactory = null, IRetryOrchestrator retryOrchestrator = null)
    {
        ClusterContext clusterCtx = null;

        // A factory from the test keeps its own setups. This one creates a cluster node or a bucket node anywhere.
        if (nodeFactory is null)
        {
            nodeFactory = new Mock<IClusterNodeFactory>();
            nodeFactory.Setup(x => x.CreateClusterNode(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>()))
                .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter) => CreateNode(clusterCtx, ep, nodeAdapter));
            nodeFactory.Setup(x => x.CreateBucketNode(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<IBucket>()))
                .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter, IBucket owner) =>
                    CreateNode(clusterCtx, ep, nodeAdapter, owner));
        }

        var options = new ClusterOptions().AddClusterService(nodeFactory.Object).WithPasswordAuthentication("username", "password");
        clusterCtx = new ClusterContext(new CancellationTokenSource(), options);

        var bucket = new CouchbaseBucket("default",
            clusterCtx,
            new Mock<IScopeFactory>().Object,
            retryOrchestrator ?? new Mock<IRetryOrchestrator>().Object,
            keyMapperFactory ?? new Mock<IVBucketKeyMapperFactory>().Object,
            new Mock<ILogger<CouchbaseBucket>>().Object,
            new Redactor(RedactionLevel.None),
            new Mock<IBootstrapperFactory>().Object,
            NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object,
            new BestEffortRetryStrategy(),
            bootstrapConfig,
            clusterCtx.ServiceProvider.GetRequiredService<IConfigRefresherFactory>());

        // Like a bootstrapped bucket, it has a node of its own for each KV node of its config. The cluster has a node
        // for each node of the config.
        foreach (var nodeAdapter in bootstrapConfig.GetNodes())
        {
            if (!nodeAdapter.IsKvNode)
            {
                continue;
            }

            var bucketNode = CreateNode(clusterCtx, HostEndpointWithPort.Create(nodeAdapter, options), nodeAdapter);
            bucketNode.Owner = bucket;
            bucket.Nodes.Add(bucketNode);
        }

        foreach (var nodeAdapter in bootstrapConfig.GetClusterNodes())
        {
            clusterCtx.AddNode(CreateNode(clusterCtx, HostEndpointWithPort.Create(nodeAdapter, options), nodeAdapter));
        }

        return bucket;
    }

    /// <summary>
    /// A node with a mock connection pool, so it opens no connection. A bucket node gets its owner as the factory sets it.
    /// </summary>
    private static ClusterNode CreateNode(ClusterContext context, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter,
        IBucket owner)
    {
        var node = CreateNode(context, endPoint, nodeAdapter);
        node.Owner = owner;
        return node;
    }

    /// <summary>
    /// A node with a mock connection pool, so it opens no connection.
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
    // Answers the call for a cluster node, the awaited call for a seed and the call for a bucket node.
    private static void SetupCreateNode(Mock<IClusterNodeFactory> nodeFactory, ClusterContext context,
        HostEndpointWithPort endPoint)
    {
        nodeFactory.Setup(x => x.CreateClusterNode(endPoint, It.IsAny<NodeAdapter>()))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter) => CreateNode(context, ep, nodeAdapter));
        nodeFactory.Setup(x => x.CreateAndConnectAsync(endPoint, It.IsAny<NodeAdapter>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter, string _, CancellationToken _) =>
                Task.FromResult<IClusterNode>(CreateNode(context, ep, nodeAdapter)));
        nodeFactory.Setup(x => x.CreateBucketNode(endPoint, It.IsAny<NodeAdapter>(), It.IsAny<IBucket>()))
            .Returns((HostEndpointWithPort ep, NodeAdapter nodeAdapter, IBucket owner) =>
                CreateNode(context, ep, nodeAdapter, owner));
    }

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
