using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class GetReplicaStrategyTests
    {
        #region SelectReplica

        // RFC worked examples first, then edge cases.
        [Theory]
        [InlineData(new short[] { 1, 2 }, 2, ReplicaIndex.Second, false, 10, 2)]
        [InlineData(new short[] { 1, 2 }, 2, ReplicaIndex.First, false, 10, 1)]
        [InlineData(new short[] { 1 }, 1, ReplicaIndex.First, false, 10, 1)]
        [InlineData(new short[] { 7, 3 }, 2, ReplicaIndex.Third, true, 10, 7)]
        [InlineData(new short[] { 1, -1, 2 }, 3, ReplicaIndex.Third, true, 10, 2)]
        [InlineData(new short[] { -1, 1, 2 }, 3, ReplicaIndex.First, true, 10, 1)]
        [InlineData(new short[] { 7, 3, 9 }, 1, ReplicaIndex.Second, true, 10, 7)]
        [InlineData(new short[] { 7 }, 3, ReplicaIndex.Second, true, 10, 7)]
        [InlineData(new short[] { 2, 1 }, 2, ReplicaIndex.Second, true, 10, 1)]
        [InlineData(new short[] { 1, -1, 2 }, 3, ReplicaIndex.Second, true, 10, 2)]
        [InlineData(new short[] { 2, -1, -1 }, 3, ReplicaIndex.Third, true, 10, 2)]
        [InlineData(new short[] { 1 }, 2, ReplicaIndex.Second, true, 10, 1)]
        [InlineData(new short[] { 1, 9 }, 2, ReplicaIndex.First, false, 4, 1)]
        [InlineData(new short[] { 9, 2 }, 2, ReplicaIndex.First, true, 4, 2)]
        public void SelectReplica_Returns_Node_Index(short[] replicaChain, int numReplicas, ReplicaIndex index,
            bool wrap, int serverCount, short expected)
        {
            var strategy = CreateStrategy(index, wrap);

            Assert.Equal(expected, strategy.SelectReplica(replicaChain, numReplicas, serverCount));
        }

        [Theory]
        [InlineData(new short[] { 1, -1 }, 2, ReplicaIndex.Second, false, 10, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[] { 1, 2 }, 2, ReplicaIndex.Third, false, 10, typeof(ReplicaIndexOutOfBoundsException))]
        [InlineData(new short[] { 7, 3, 9 }, 1, ReplicaIndex.Second, false, 10, typeof(ReplicaIndexOutOfBoundsException))]
        [InlineData(new short[] { 7 }, 3, ReplicaIndex.Second, false, 10, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[] { -1, -1, -1 }, 3, ReplicaIndex.First, true, 10, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[0], 0, ReplicaIndex.First, false, 10, typeof(ReplicaIndexOutOfBoundsException))]
        [InlineData(new short[0], 0, ReplicaIndex.First, true, 10, typeof(ReplicaIndexOutOfBoundsException))]
        [InlineData(new short[] { -1, -1 }, 2, ReplicaIndex.Third, true, 10, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[0], 1, ReplicaIndex.First, true, 10, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[] { 1, 9 }, 2, ReplicaIndex.Second, false, 4, typeof(ReplicaIndexCurrentlyUnavailableException))]
        [InlineData(new short[] { 9, 9 }, 2, ReplicaIndex.First, true, 4, typeof(ReplicaIndexCurrentlyUnavailableException))]
        public void SelectReplica_Throws(short[] replicaChain, int numReplicas, ReplicaIndex index, bool wrap,
            int serverCount, Type expected)
        {
            var strategy = CreateStrategy(index, wrap);

            var exception = Assert.Throws(expected, () => strategy.SelectReplica(replicaChain, numReplicas, serverCount));
            Assert.NotEmpty(exception.Message);
        }

        #endregion

        #region FromIndex

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(5)]
        public void FromIndex_Undefined_Index_Throws(int index)
        {
            Assert.Throws<InvalidArgumentException>(() => GetReplicaStrategy.FromIndex((ReplicaIndex) index));
        }

        [Theory]
        [InlineData(ReplicaIndex.First)]
        [InlineData(ReplicaIndex.Second)]
        [InlineData(ReplicaIndex.Third)]
        public void FromIndex_Defined_Index_Does_Not_Throw(ReplicaIndex index)
        {
            Assert.NotNull(GetReplicaStrategy.FromIndex(index));
        }

        [Fact]
        public void FromIndex_Without_Options_Does_Not_Wrap()
        {
            Assert.Equal("FromIndex(Second, wrap: false)", GetReplicaStrategy.FromIndex(ReplicaIndex.Second).ToString());
        }

        [Fact]
        public void FromIndex_With_Wrap_Option_Wraps()
        {
            var strategy = GetReplicaStrategy.FromIndex(ReplicaIndex.Second,
                new GetReplicaStrategyFromIndexOptions().Wrap());

            Assert.Equal("FromIndex(Second, wrap: true)", strategy.ToString());
        }

        [Fact]
        public void FromIndex_With_Wrap_False_Does_Not_Wrap()
        {
            var strategy = GetReplicaStrategy.FromIndex(ReplicaIndex.Third,
                new GetReplicaStrategyFromIndexOptions().Wrap(false));

            Assert.Equal("FromIndex(Third, wrap: false)", strategy.ToString());
        }

        #endregion

        private static GetReplicaStrategy CreateStrategy(ReplicaIndex index, bool wrap) =>
            GetReplicaStrategy.FromIndex(index, new GetReplicaStrategyFromIndexOptions().Wrap(wrap));
    }
}
