using System;
using Couchbase.Core.Version;
using Xunit;

namespace Couchbase.UnitTests.Core.Version
{
    public class ClusterVersionTests
    {
        #region ctor

        [Fact]
        public void ctor_BuildLessThanZero_AlwaysNegativeOne()
        {
            // Act

            var version = new ClusterVersion(new System.Version(1, 0, 0), -2);

            // Assert

            Assert.Equal(-1, version.Build);
        }

        [Fact]
        public void ctor_BuildLessThanZeroAndSuffix_AlwaysNegativeOne()
        {
            // Act

            var version = new ClusterVersion(new System.Version(1, 0, 0), -2, "suffix");

            // Assert

            Assert.Equal(-1, version.Build);
        }

        #endregion

        #region ToString

        [Fact]
        public void ToString_VersionOnly_ReturnsVersion()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0));

            // Act

            var result = version.ToString();

            // Assert

            Assert.Equal("1.0.0", result);
        }

        [Fact]
        public void ToString_NoBuild_ReturnsVersionAndSuffix()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), -1, "suffix");

            // Act

            var result = version.ToString();

            // Assert

            Assert.Equal("1.0.0-suffix", result);
        }

        [Fact]
        public void ToString_NoSuffix_ReturnsVersionAndBuild()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100);

            // Act

            var result = version.ToString();

            // Assert

            Assert.Equal("1.0.0-100", result);
        }

        [Fact]
        public void ToString_NullSuffix_ReturnsVersionAndBuild()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100, null);

            // Act

            var result = version.ToString();

            // Assert

            Assert.Equal("1.0.0-100", result);
        }

        #endregion

        #region Deconstruct

        [Fact]
        public void Deconstruct_Double_GetsValue()
        {
            // Arrange

            var clusterVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Act

            var (version, build) = clusterVersion;

            // Assert

            Assert.Equal(new System.Version(1, 0, 0), version);
            Assert.Equal(100, build);
        }

        [Fact]
        public void Deconstruct_Triple_GetsValue()
        {
            // Arrange

            var clusterVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Act

            var (version, build, suffix) = clusterVersion;

            // Assert

            Assert.Equal(new System.Version(1, 0, 0), version);
            Assert.Equal(100, build);
            Assert.Equal("suffix", suffix);
        }

        #endregion

        #region Equals

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0));

            // Act

            var result = version.Equals(null);

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void Equals_NotClusterVersion_ReturnsFalse()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0));

            // Act

            var result = version.Equals(new object());

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void Equals_SameVersion_ReturnsTrue()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0));

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 0, 0)));

            // Assert

            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentVersion_ReturnsTrue()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0));

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 1, 0)));

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void Equals_SameBuild_ReturnsTrue()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100);

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 0, 0), 100));

            // Assert

            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentBuild_ReturnsFalse()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100);

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 0, 0), 101));

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void Equals_SameSuffix_ReturnsTrue()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix"));

            // Assert

            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentSuffix_ReturnsFalse()
        {
            // Arrange

            var version = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Act

            var result = version.Equals(new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix2"));

            // Assert

            Assert.False(result);
        }

        #endregion

        #region ComparisonOperators

        [Fact]
        public void ComparisonOperators_LowerVersion()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0));
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 1));

            // Assert

            Assert.True(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.False(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_EqualVersion()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0));
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0));

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.True(leftVersion == rightVersion);
            Assert.False(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_GreaterVersion()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 1));
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0));

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.False(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.True(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_LowerBuild()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 100);
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 101);

            // Assert

            Assert.True(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.False(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_EqualBuild()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 100);
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 100);

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.True(leftVersion == rightVersion);
            Assert.False(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_GreaterBuild()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 101);
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 100);

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.False(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.True(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_LowerSuffix()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix2");

            // Assert

            Assert.True(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.False(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_EqualSuffix()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.True(leftVersion <= rightVersion);
            Assert.True(leftVersion == rightVersion);
            Assert.False(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.False(leftVersion > rightVersion);
        }

        [Fact]
        public void ComparisonOperators_GreaterSuffix()
        {
            // Arrange

            var leftVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix2");
            var rightVersion = new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix");

            // Assert

            Assert.False(leftVersion < rightVersion);
            Assert.False(leftVersion <= rightVersion);
            Assert.False(leftVersion == rightVersion);
            Assert.True(leftVersion != rightVersion);
            Assert.True(leftVersion >= rightVersion);
            Assert.True(leftVersion > rightVersion);
        }

        #endregion

        #region Parse

        [Fact]
        public void Parse_BadVersion_ThrowsFormatException()
        {
            // Act/Assert

            Assert.Throws<FormatException>(() => ClusterVersion.Parse("abcd"));
        }

        [Fact]
        public void Parse_GoodVersion_ReturnsVersion()
        {
            // Act

            var version = ClusterVersion.Parse("1.0.0");

            // Assert

            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0)), version);
        }

        #endregion

        #region TryParse

        [Fact]
        public void TryParse_BadVesion_ReturnsFalse()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("abcd", out version);

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void TryParse_GoodVersion_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0)), version);
        }

        [Fact]
        public void TryParse_WithBuild_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0-100", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0), 100), version);
        }

        [Fact]
        public void TryParse_WithBuildAndSuffix_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0-100-suffix", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix"), version);
        }

        [Fact]
        public void TryParse_WithSuffix_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0-suffix", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0), -1, "suffix"), version);
        }

        [Fact]
        public void TryParse_WithBuildAndSegmentedSuffix_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0-100-suffix-suffix2", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0), 100, "suffix-suffix2"), version);
        }

        [Fact]
        public void TryParse_WithSegmentedSuffix_ReturnsTrueAndVersion()
        {
            // Act

            ClusterVersion version;
            var result = ClusterVersion.TryParse("1.0.0-suffix-suffix2", out version);

            // Assert

            Assert.True(result);
            Assert.Equal(new ClusterVersion(new System.Version(1, 0, 0), -1, "suffix-suffix2"), version);
        }

        #endregion
    }
}
