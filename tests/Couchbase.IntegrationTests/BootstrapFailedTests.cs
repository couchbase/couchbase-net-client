using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class BootstrapFailedTests
    {
        [Fact(Skip = "NCBC-2559")]
        public async Task Test_BootStrap_Error_Propagates_To_Collection_Operations()
        {
            const string id = "key;";
            var value = new { x = "y" };

            var settings = ClusterFixture.GetSettings();
            var cluster = await Couchbase.Cluster.ConnectAsync(settings.ConnectionString, "Administrator", "password").ConfigureAwait(true);

            // This test may be invalid.  It is throwing BucketNotFoundException here, and that is appropriate,. as far as I can tell.
            var bucket = await cluster.BucketAsync("doesnotexist").ConfigureAwait(true);
            var defaultCollection = await bucket.DefaultCollectionAsync();

           // We would have to inject a bootstrapping error *after* BucketAsync succeeds, to continue with the test.

           await Assert.ThrowsAsync<AuthenticationFailureException>(async ()=>
           {
               await ThrowAuthenticationException(()=> defaultCollection.GetAsync(id)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.ExistsAsync(id)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAndLockAsync(id, TimeSpan.MaxValue)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAndTouchAsync(id, TimeSpan.MaxValue)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAnyReplicaAsync(id)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.LookupInAsync(id, null)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.MutateInAsync(id, null)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.RemoveAsync(id)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.TouchAsync(id, TimeSpan.Zero)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.InsertAsync(id,value)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.ReplaceAsync(id,value)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.UnlockAsync(id, 0)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.UpsertAsync(id, value)).ConfigureAwait(true);
           }).ConfigureAwait(true);
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(()=> defaultCollection.GetAllReplicasAsync(id).First()).ConfigureAwait(true);
           }).ConfigureAwait(true);
        }

        [Fact(Skip = "NCBC-2559")]
        public async Task Test_BootStrap_Error_Propagates_To_View_Operations()
        {
            var settings = ClusterFixture.GetSettings();
            var cluster = await Couchbase.Cluster.ConnectAsync(settings.ConnectionString, "Administrator", "password").ConfigureAwait(true);
            var bucket = await cluster.BucketAsync("doesnotexist").ConfigureAwait(true);

            await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
            {
                await ThrowAuthenticationException(() => bucket.ViewQueryAsync<object,object>("designdoc", "viewname")).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        async Task ThrowAuthenticationException(Func<Task> func)
        {
            try
            {
                await func().ConfigureAwait(true);
            }
            catch (AggregateException e)
            {
                e.Flatten().Handle((x) =>
                {
                    if (x is AuthenticationFailureException)
                    {
                        throw x;
                    }

                    return false;
                });
            }
        }
    }
}
