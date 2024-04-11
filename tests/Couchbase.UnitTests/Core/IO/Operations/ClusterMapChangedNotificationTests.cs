using System;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations;

public class ClusterMapChangedNotificationTests
{
    private readonly byte[] _responsePacket = {
        0x82, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x42,
        0x01, 0x02, 0x03, 0x04,
        0x05, 0x06, 0x07, 0x08
    };

    protected readonly byte[] packet =
    {
        130, 1, 0, 0,
        16, 0, 0, 0,
        0, 0, 0, 16,
        0, 0,0,0,
        0, 0, 0, 0,
        0, 0,0, 0,
        0, 0, 0,0,
        0, 0,0, 1,
        0,0,0,0,
        0,0,4,78
    };

    [Fact]
    public void When_DataType_Raw_Extras_Are_Included()
    {
        //Setup
        var clusterMapChangeNotificationOp =
            new Couchbase.Core.IO.Operations.Configuration.ClusterMapChangeNotification();

        SlicedMemoryOwner<byte> responsePacket =  new(new FakeMemoryOwner<byte>(new Memory<byte>(_responsePacket)));

        //Act
        clusterMapChangeNotificationOp.HandleOperationCompleted(responsePacket);

        //Assert
        Assert.Equal(66ul, clusterMapChangeNotificationOp.GetConfigVersion.Epoch);
        Assert.Equal(72623859790382856ul, clusterMapChangeNotificationOp.GetConfigVersion.Revision);
    }

    [Fact]
    public void When_DataType_Raw_Extras_Are_Included2()
    {
        //Setup
        var clusterMapChangeNotificationOp =
            new Couchbase.Core.IO.Operations.Configuration.ClusterMapChangeNotification();

        SlicedMemoryOwner<byte> responsePacket =  new(new FakeMemoryOwner<byte>(new Memory<byte>(packet)));

        //Act
        clusterMapChangeNotificationOp.HandleOperationCompleted(responsePacket);

        //Assert
        Assert.Equal(1ul, clusterMapChangeNotificationOp.GetConfigVersion.Epoch);
        Assert.Equal(1102ul, clusterMapChangeNotificationOp.GetConfigVersion.Revision);
    }

    [Fact]
    public void When_Replica_Changes_Should_Not_Throw()
    {
        var preChangeJson = ResourceHelper.ReadResource(@"Documents\Configs\config-pre-replica-change.json");
        var postChangeJson = ResourceHelper.ReadResource(@"Documents\Configs\config-post-replica-change.json");
        var preChange = JsonConvert.DeserializeObject<BucketConfig>(preChangeJson);
        var postChange = JsonConvert.DeserializeObject<BucketConfig>(postChangeJson);

        var selfCompare = postChange.HasVBucketMapChanged(postChange, out var emptyVBucketMap);
        Assert.False(selfCompare);
        Assert.False(emptyVBucketMap);

        Assert.True(preChange.HasVBucketMapChanged(postChange, out _));
    }
}
