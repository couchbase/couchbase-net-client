using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Sharding;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Sharding
{
    public class VBucketKeyMapperTests
    {
        private const string Key = "XXXXX";

        [Theory]
        [InlineData(545, "thekey0")]
        [InlineData(294, "thekey1")]
        [InlineData(47, "thekey2")]
        [InlineData(808, "thekey3")]
        [InlineData(332, "thekey4")]
        [InlineData(587, "thekey5")]
        [InlineData(834, "thekey6")]
        [InlineData(69, "thekey7")]
        [InlineData(250, "thekey8")]
        [InlineData(1021, "thekey9")]
        [InlineData(230, "thekey10")]
        [InlineData(993, "thekey11")]
        [InlineData(495, "thekey13")]
        [InlineData(907, "thekey14")]
        [InlineData(140, "thekey15")]
        [InlineData(389, "thekey16")]
        [InlineData(642, "thekey17")]
        [InlineData(573, "thekey18")]
        [InlineData(314, "thekey19")]
        [InlineData(971, "thekey20")]
        [InlineData(204, "thekey21")]
        [InlineData(453, "thekey22")]
        [InlineData(706, "thekey23")]
        [InlineData(166, "thekey24")]
        [InlineData(929, "thekey25")]
        [InlineData(680, "thekey26")]
        [InlineData(431, "thekey27")]
        [InlineData(272, "thekey28")]
        [InlineData(535, "thekey29")]
        [InlineData(720, "thekey30")]
        [InlineData(471, "thekey31")]
        [InlineData(222, "thekey32")]
        [InlineData(985, "thekey33")]
        [InlineData(445, "thekey34")]
        [InlineData(698, "thekey35")]
        [InlineData(947, "thekey36")]
        [InlineData(180, "thekey37")]
        [InlineData(11, "thekey38")]
        [InlineData(780, "thekey39")]
        [InlineData(401, "thekey40")]
        [InlineData(662, "thekey41")]
        [InlineData(927, "thekey42")]
        [InlineData(152, "thekey43")]
        [InlineData(764, "thekey44")]
        [InlineData(507, "thekey45")]
        [InlineData(242, "thekey46")]
        [InlineData(1013, "thekey47")]
        [InlineData(842, "thekey48")]
        [InlineData(77, "thekey49")]
        [InlineData(138, "thekey50")]
        [InlineData(909, "thekey51")]
        [InlineData(644, "thekey52")]
        [InlineData(387, "thekey53")]
        [InlineData(999, "thekey54")]
        [InlineData(224, "thekey55")]
        [InlineData(489, "thekey56")]
        [InlineData(750, "thekey57")]
        [InlineData(593, "thekey58")]
        [InlineData(342, "thekey59")]
        [InlineData(935, "thekey60")]
        [InlineData(160, "thekey61")]
        [InlineData(425, "thekey62")]
        [InlineData(686, "thekey63")]
        [InlineData(202, "thekey64")]
        [InlineData(973, "thekey65")]
        [InlineData(708, "thekey66")]
        [InlineData(451, "thekey67")]
        [InlineData(380, "thekey68")]
        [InlineData(635, "thekey69")]
        [InlineData(700, "thekey70")]
        [InlineData(443, "thekey71")]
        [InlineData(178, "thekey72")]
        [InlineData(949, "thekey73")]
        [InlineData(465, "thekey74")]
        [InlineData(726, "thekey75")]
        [InlineData(991, "thekey76")]
        [InlineData(216, "thekey77")]
        [InlineData(103, "thekey78")]
        [InlineData(864, "thekey79")]
        [InlineData(292, "thekey80")]
        [InlineData(547, "thekey81")]
        [InlineData(810, "thekey82")]
        [InlineData(45, "thekey83")]
        [InlineData(585, "thekey84")]
        [InlineData(334, "thekey85")]
        [InlineData(71, "thekey86")]
        [InlineData(832, "thekey87")]
        [InlineData(1023, "thekey88")]
        [InlineData(248, "thekey89")]
        [InlineData(63, "thekey90")]
        [InlineData(824, "thekey91")]
        [InlineData(561, "thekey92")]
        [InlineData(310, "thekey93")]
        [InlineData(850, "thekey94")]
        [InlineData(85, "thekey95")]
        [InlineData(348, "thekey96")]
        [InlineData(603, "thekey97")]
        [InlineData(740, "thekey98")]
        [InlineData(483, "thekey99")]
        [InlineData(38, "thekey100")]
        public void TestMapKey(int index, string key)
        {
            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");

            var (vBucketServerMap, _) = GetServerMapAndIpEndPoints(config.VBucketServerMap);

            IKeyMapper mapper = new VBucketKeyMapper(config, vBucketServerMap,
                new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            var vBucket = (VBucket) mapper.MapKey(key);
            Assert.Equal(index, vBucket.Index);
        }

        [Fact]
        public void Test_That_Key_XXXXX_Maps_To_VBucket_389()
        {
            const int actual = 389;
            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");

            var (vBucketServerMap, _) = GetServerMapAndIpEndPoints(config.VBucketServerMap);

            IKeyMapper mapper = new VBucketKeyMapper(config, vBucketServerMap,
                new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            var vBucket = (IVBucket) mapper.MapKey(Key);
            Assert.Equal(vBucket.Index, actual);
        }

        [Fact]
        public void VBucket_HasCorrectBucketname()
        {
            var expected = "default";

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");

            var (vBucketServerMap, _) = GetServerMapAndIpEndPoints(config.VBucketServerMap);

            IKeyMapper mapper = new VBucketKeyMapper(config, vBucketServerMap,
                new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            var vBucket = (IVBucket) mapper.MapKey(Key);

            Assert.Equal(expected, vBucket.BucketName);
        }

        [Fact]
        public void VBucket_Supports_LocalHost()
        {
            var expected = "default";

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\configs\config-localhost.json");
            config.ReplacePlaceholderWithBootstrapHost("127.0.0.1");

            var (vBucketServerMap, _) = GetServerMapAndIpEndPoints(config.VBucketServerMap);

            IKeyMapper mapper = new VBucketKeyMapper(config, vBucketServerMap,
                new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            var vBucket = (IVBucket) mapper.MapKey(Key);

            Assert.Equal(expected, vBucket.BucketName);
        }

        [Theory]
        [InlineData("192.168.67.102:11210", ResponseStatus.VBucketBelongsToAnotherServer)]
        [InlineData("192.168.67.101:11210", ResponseStatus.None)]
        public void Config_With_FFMaps_Uses_Them_When_NMVB(string nodeIp,  ResponseStatus responseStatus)
        {
            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\configs\config-with-ffmaps.json");

            var (vBucketServerMap, _) = GetServerMapAndIpEndPoints(config.VBucketServerMap);

            IKeyMapper mapper = new VBucketKeyMapper(config, vBucketServerMap,
                new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            var vBucket = (IVBucket)mapper.MapKey(Key);

            var op = new Get<dynamic>()
            {
                Header = new OperationHeader
                {
                    Status = responseStatus
                }
            };

            var mappedKey = mapper.MapKey("mykey", op.WasNmvb());
            Assert.Equal(nodeIp, mappedKey.LocatePrimary().ToString());
        }

        #region Helpers

        #region Helpers

        private static (VBucketServerMap serverMap, List<IPEndPoint> ipEndPoints) GetServerMapAndIpEndPoints(
            VBucketServerMapDto vBucketServerMapDto)
        {
            var ipEndPoints = vBucketServerMapDto.ServerList
                .Select(p =>
                {
                    var split = p.Split(':');
                    return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
                })
                .ToList();

            return (new VBucketServerMap(vBucketServerMapDto, ipEndPoints), ipEndPoints);
        }

        #endregion

        #endregion
    }
}
