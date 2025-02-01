using System.Collections.Generic;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.UnitTests.Management;

public class CollectionSpecTests
{
    [Fact]
    public void Test_Equals()
    {
        var collection1 = new CollectionSpec("scope1", "collection1");
        var collection2 = new CollectionSpec("scope1", "collection1");

        Assert.True(collection1.Equals(collection2));
    }

    [Fact]
    public void Test_NotEquals()
    {
        var collection1 = new CollectionSpec("scope1", "collection2");
        var collection2 = new CollectionSpec("scope1", "collection1");

        Assert.False(collection1.Equals(collection2));
    }

    [Fact]
    public void Test_Contains_True()
    {
        var list = new List<CollectionSpec>{new CollectionSpec("scope1", "collection1"), new CollectionSpec("scope2", "collection2")};
        Assert.Contains(new CollectionSpec("scope2", "collection2"), list);
    }

    [Fact]
    public void Test_Contains_False()
    {
        var list = new List<CollectionSpec>{new CollectionSpec("scope1", "collection1"), new CollectionSpec("scope2", "collection2")};
        Assert.DoesNotContain(new CollectionSpec("scope1", "collection2"), list);
    }
}
