using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
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
    }

    CouchbaseBucket CreateBucket(BucketConfig bootstrapConfig)
    {
        var clusterNodeFactory = new Mock<IClusterNodeFactory>();
        var node = new Mock<IClusterNode>();
        node.Setup(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort>
            { new("127.0.0.1", 11210) });

        clusterNodeFactory.Setup(x => x.CreateAndConnectAsync(
                It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                It.IsAny<CancellationToken>())).
            Returns(Task.FromResult(node.Object));

        var options = new ClusterOptions().AddClusterService(clusterNodeFactory.Object);
        var clusterCtx = new ClusterContext(new CancellationTokenSource(), options)
        {
            SupportsCollections = true
        };

        var bucket = new CouchbaseBucket("default",
            clusterCtx,
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            new Mock<IVBucketKeyMapperFactory>().Object,
            new Mock<ILogger<CouchbaseBucket>>().Object,
            new TypedRedactor(RedactionLevel.None),
            new Mock<IBootstrapperFactory>().Object,
            NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object,
            new BestEffortRetryStrategy(),
            bootstrapConfig);

        node.Setup(x => x.Owner).Returns(bucket);


        return bucket;
    }
}
