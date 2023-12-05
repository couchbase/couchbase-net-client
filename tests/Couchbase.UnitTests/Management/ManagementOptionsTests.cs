using System;
using System.Threading;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Xunit;

namespace Couchbase.UnitTests.Management;

public class ManagementOptionsTests
{
    [Theory]
    [InlineData(typeof(CreateBucketOptions))]
    [InlineData(typeof(DropBucketOptions))]
    [InlineData(typeof(FlushBucketOptions))]
    [InlineData(typeof(GetBucketOptions))]
    [InlineData(typeof(GetAllBucketsOptions))]
    [InlineData(typeof(UpdateBucketOptions))]

    [InlineData(typeof(DropCollectionOptions))]
    [InlineData(typeof(CreateCollectionOptions))]
    [InlineData(typeof(UpdateCollectionOptions))]

    [InlineData(typeof(CreateScopeOptions))]
    [InlineData(typeof(GetScopeOptions))]
    [InlineData(typeof(DropScopeOptions))]
    [InlineData(typeof(GetAllScopesOptions))]

    [InlineData(typeof(BuildDeferredQueryIndexOptions))]
    [InlineData(typeof(CreatePrimaryQueryIndexOptions))]
    [InlineData(typeof(CreateQueryIndexOptions))]
    [InlineData(typeof(DropPrimaryQueryIndexOptions))]
    [InlineData(typeof(DropQueryIndexOptions))]
    [InlineData(typeof(GetAllQueryIndexOptions))]
    [InlineData(typeof(WatchQueryIndexOptions))]

    [InlineData(typeof(AllowQueryingSearchIndexOptions))]
    [InlineData(typeof(DisallowQueryingSearchIndexOptions))]
    [InlineData(typeof(DropSearchIndexOptions))]
    [InlineData(typeof(FreezePlanSearchIndexOptions))]
    [InlineData(typeof(GetAllSearchIndexesOptions))]
    [InlineData(typeof(GetSearchIndexDocumentCountOptions))]
    [InlineData(typeof(GetSearchIndexOptions))]
    [InlineData(typeof(PauseIngestSearchIndexOptions))]
    [InlineData(typeof(ResumeIngestSearchIndexOptions))]
    [InlineData(typeof(UnfreezePlanSearchIndexOptions))]
    [InlineData(typeof(UpsertSearchIndexOptions))]

    [InlineData(typeof(AvailableRolesOptions))]
    [InlineData(typeof(ChangePasswordOptions))]
    [InlineData(typeof(DropGroupOptions))]
    [InlineData(typeof(DropUserOptions))]
    [InlineData(typeof(GetAllUsersOptions))]
    [InlineData(typeof(GetGroupOptions))]
    [InlineData(typeof(GetUserOptions))]
    [InlineData(typeof(UpsertUserOptions))]
    [InlineData(typeof(UpsertGroupOptions))]

    //Asserts that Timeout()s does not issue a CancellationToken to the option's
    //TokenValue field, and that CancellationToken() does.
    private void ManagementOptions_Timeout_Cancellation_Test(Type type)
    {
        var mgmgtOption = Activator.CreateInstance(type)! as dynamic;

        mgmgtOption.Timeout(TimeSpan.FromSeconds(24));

        Assert.Equal(CancellationToken.None, mgmgtOption.TokenValue);
        Assert.Equal(TimeSpan.FromSeconds(24), mgmgtOption.TimeoutValue);

        var token = new CancellationTokenSource(TimeSpan.FromSeconds(24)).Token;
        mgmgtOption.CancellationToken(token);

        Assert.Equal(token, mgmgtOption.TokenValue);
    }
}
