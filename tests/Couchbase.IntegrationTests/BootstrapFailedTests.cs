using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class BootstrapFailedTests
    {
        [Fact]
        public async Task Test_BootStrap_Error_Propagates_To_Collection_Operations()
        {
            const string id = "key;";
            var value = new {x = "y"};

            var settings = ClusterFixture.GetSettings();
            var cluster = await Cluster.ConnectAsync(settings.ConnectionString, "Administrator", "password");
            var bucket = await cluster.BucketAsync("doesnotexist");
            var defaultCollection = bucket.DefaultCollection();

           await Assert.ThrowsAsync<AuthenticationFailureException>(async ()=>
           {
               await ThrowAuthenticationException(()=> defaultCollection.GetAsync(id));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.ExistsAsync(id));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAndLockAsync(id, TimeSpan.MaxValue));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAndTouchAsync(id, TimeSpan.MaxValue));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.GetAnyReplicaAsync(id));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.LookupInAsync(id, null));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.MutateInAsync(id, null));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.RemoveAsync(id));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.TouchAsync(id, TimeSpan.Zero));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.InsertAsync(id,value));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.ReplaceAsync(id,value));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.UnlockAsync<dynamic>(id, 0));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(() => defaultCollection.UpsertAsync(id, value));
           });
           await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
           {
               await ThrowAuthenticationException(()=> defaultCollection.GetAllReplicasAsync(id).First());
           });
        }

        [Fact]
        public async Task Test_BootStrap_Error_Propagates_To_View_Operations()
        {
            var cluster = await Cluster.ConnectAsync("couchbase://10.143.194.101", "Administrator", "password");
            var bucket = await cluster.BucketAsync("doesnotexist");

            await Assert.ThrowsAsync<AuthenticationFailureException>(async () =>
            {
                await ThrowAuthenticationException(() => bucket.ViewQueryAsync<object,object>("designdoc", "viewname"));
            });
        }

        async Task ThrowAuthenticationException(Func<Task> func)
        {
            try
            {
                await func();
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
