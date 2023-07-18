using Couchbase.Core.Configuration.Server;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration;

public class ConfigVersionTests
{
    [Theory]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(2, 1, 1, 1, true)]
    [InlineData(1, 2, 1, 1, true)]
    [InlineData(1, 1, 2, 1, false)]
    [InlineData(1, 1, 1, 2, false)]
    public void Test_GreaterThanOrEqual(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool greaterThanOrEqual)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (greaterThanOrEqual)
        {
            Assert.True(configVersion1 >= configVersion2);
        }
        else
        {
            Assert.False(configVersion1 >= configVersion2);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(1, 1, 2, 1, true)]
    [InlineData(1, 1, 1, 2, true)]
    [InlineData(2, 1, 1, 1, false)]
    [InlineData(1, 2, 1, 1, false)]
    public void Test_LessThanOrEqual(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool greaterThanOrEqual)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (greaterThanOrEqual)
        {
            Assert.True(configVersion1 <= configVersion2);
        }
        else
        {
            Assert.False(configVersion1 <= configVersion2);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(2, 1, 1, 1, false)]
    [InlineData(1, 2, 1, 1, false)]
    [InlineData(1, 1, 2, 1, false)]
    [InlineData(1, 1, 1, 2, false)]
    public void Test_EqualTo(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool equal)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (equal)
        {
            Assert.True(configVersion1 == configVersion2);
        }
        else
        {
            Assert.False(configVersion1 == configVersion2);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, false)]
    [InlineData(2, 1, 1, 1, true)]
    [InlineData(1, 2, 1, 1, true)]
    [InlineData(1, 1, 2, 1, true)]
    [InlineData(1, 1, 1, 2, true)]
    public void Test_NotEqualTo(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool equal)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (equal)
        {
            Assert.True(configVersion1 != configVersion2);
        }
        else
        {
            Assert.False(configVersion1 != configVersion2);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(2, 1, 1, 1, false)]
    [InlineData(1, 2, 1, 1, false)]
    [InlineData(1, 1, 2, 1, false)]
    [InlineData(1, 1, 1, 2, false)]
    public void Test_Equals(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool equal)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (equal)
        {
            Assert.True(configVersion1.Equals(configVersion2));
        }
        else
        {
            Assert.False(configVersion1.Equals(configVersion2));
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, false)]
    [InlineData(2, 1, 1, 1, true)]
    [InlineData(1, 2, 1, 1, true)]
    [InlineData(1, 1, 2, 1, false)]
    [InlineData(1, 1, 1, 2, false)]
    public void Test_GreaterThan(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool equal)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (equal)
        {
            Assert.True(configVersion1 > configVersion2);
        }
        else
        {
            Assert.False(configVersion1 > configVersion2);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, false)]
    [InlineData(2, 1, 1, 1, false)]
    [InlineData(1, 2, 1, 1, false)]
    [InlineData(1, 1, 2, 1, true)]
    [InlineData(1, 1, 1, 2, true)]
    public void Test_LessThan(ulong epoch1, ulong revision1, ulong epoch2, ulong revision2, bool equal)
    {
        var configVersion1 = new ConfigVersion(epoch1, revision1);
        var configVersion2 = new ConfigVersion(epoch2, revision2);

        if (equal)
        {
            Assert.True(configVersion1 < configVersion2);
        }
        else
        {
            Assert.False(configVersion1 < configVersion2);
        }
    }

    [Fact]
    public void Test_ToString()
    {
        var configVersion = new ConfigVersion(1, 2);
        var expected = "1/2";
        Assert.Equal(expected, configVersion.ToString());
    }
}
