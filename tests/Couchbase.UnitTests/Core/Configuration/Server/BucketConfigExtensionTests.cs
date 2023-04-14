using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;
using Couchbase.UnitTests.Utils;
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
}
