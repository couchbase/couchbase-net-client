using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils;

public class QueryContextTests
{
    [Fact]
    public void Test_Default()
    {
        var context = QueryContext.CreateOrDefault();
        Assert.Equal("default:", context);
    }

    [Fact]
    public void Test_BucketName()
    {
        var context = QueryContext.CreateOrDefault("travel-sample");
        Assert.Equal("default:`travel-sample`", context);
    }

    [Fact]
    public void Test_BucketName_And_ScopeName()
    {
        var context = QueryContext.CreateOrDefault("travel-sample", "scope1");
        Assert.Equal("default:`travel-sample`.`scope1`", context);
    }

    [Fact]
    public void Test_ScopeName()
    {
        var context = QueryContext.CreateOrDefault(null, "scope1");
        Assert.Equal("default:`default`.`scope1`", context);
    }
}
