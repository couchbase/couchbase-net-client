using System.Collections.Generic;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.UnitTests.Management;

public class ScopeSpecTests
{
    [Fact]
    public void Test_Equals()
    {
        var scope1 = new ScopeSpec("scope1");
        var scope2 = new ScopeSpec("scope1");

        Assert.True(scope1.Equals(scope2));
    }

    [Fact]
    public void Test_NotEquals()
    {
        var scope1 = new ScopeSpec("scope2");
        var scope2 = new ScopeSpec("scope1");

        Assert.False(scope1.Equals(scope2));
    }

    [Fact]
    public void Test_Contains_True()
    {
        var list = new List<ScopeSpec>{new ScopeSpec("scope1"), new ScopeSpec("scope2")};
        Assert.Contains(new ScopeSpec("scope2"), list);
    }

    [Fact]
    public void Test_Contains_False()
    {
        var list = new List<ScopeSpec>{new ScopeSpec("scope1"), new ScopeSpec("scope2")};
        Assert.DoesNotContain(new ScopeSpec("scope3"), list);
    }
}
